using System;
using System.Collections.Generic;
using System.Text;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Dashboard: how loud an event log line is. The view colours lines by it.</summary>
    public enum LogSeverity
    {
        Info,
        Warning,
        Fault,
    }

    /// <summary>Dashboard: one event log line. Time is kept raw so the view decides how to show it.</summary>
    public readonly struct LogEntry
    {
        public long UnixMs { get; }
        public string Text { get; }
        public LogSeverity Severity { get; }

        public LogEntry(long unixMs, string text, LogSeverity severity)
        {
            UnixMs = unixMs;
            Text = text;
            Severity = severity;
        }
    }

    /// <summary>
    /// Dashboard: turns state changes on the bus into readable event lines (newest first). Only transitions are
    /// logged, never steady telemetry; the first value seen for a topic is its baseline and is not logged, except
    /// the broker link, whose first state is worth a line, and fault injections, which are commands.
    /// </summary>
    public sealed class EventLog
    {
        // Tool life at or below this reads as tool-change advice rather than a general maintenance warning.
        // Above the risk model's 20 min change lead, so its due flag always reads as a tool change.
        private const double ToolChangeAdviceMin = 30d;

        private readonly int _capacity;
        private readonly List<LogEntry> _entries = new List<LogEntry>();
        private readonly Dictionary<TopicPath, double> _last = new Dictionary<TopicPath, double>();

        public EventLog(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        /// <summary>Newest first. Hand it to a ListView as its items source.</summary>
        public IReadOnlyList<LogEntry> Entries => _entries;

        /// <summary>The same list, typed for ListView.itemsSource, which wants an IList.</summary>
        public System.Collections.IList ItemsSource => _entries;

        public event Action Changed;

        /// <summary>Empties the log and forgets every baseline, so the next value of each topic is a new baseline.</summary>
        public void Clear()
        {
            _entries.Clear();
            _last.Clear();
            Changed?.Invoke();
        }

        public void Observe(SignalMessage message)
        {
            TopicPath topic = message.Topic;
            double value = message.Value.AsNumber;

            // A tool change is a command: every one is news, even two in a row.
            if (topic == CellTopics.PressToolChange)
            {
                if (value != 0d)
                    Add(message.UnixMs, "Tool changed", LogSeverity.Info);
                return;
            }
            // A safety reset is a button press: every one is news. An accepted reset shows as the safety state change.
            if (topic == CellTopics.SafetyReset)
            {
                if (value != 0d && _last.TryGetValue(CellTopics.SafetyState, out double safety) && (int)safety == (int)SafetyState.Stopped)
                    Add(message.UnixMs, "Reset refused: person still in the robot zone", LogSeverity.Warning);
                return;
            }
            bool seen = _last.TryGetValue(topic, out double previous);
            if (seen && previous.Equals(value))
                return;
            _last[topic] = value;

            if (topic == CellTopics.PlcSource)
            {
                if (seen || value != 0d)
                    Add(message.UnixMs, value >= 0.5d ? "Soft PLC runs the line" : "Unity sequencer runs the line", LogSeverity.Info);
                return;
            }
            if (topic == CellTopics.PlcLink)
            {
                if (seen && PlcRunsLine())
                    Add(message.UnixMs, value != 0d ? "PLC link back" : "PLC link lost", value != 0d ? LogSeverity.Info : LogSeverity.Fault);
                return;
            }
            if (topic == CellTopics.LinkConnected)
            {
                Add(message.UnixMs, value != 0d ? "Broker connected" : "Broker disconnected",
                    value != 0d ? LogSeverity.Info : LogSeverity.Warning);
                return;
            }
            // Injections are commands, published only when someone acts, so the first one is news too.
            if (topic == CellTopics.PressInjectFault)
            {
                if (seen || value != 0d)
                    LogInjection((FaultMode)(int)value, message.UnixMs);
                return;
            }
            if (!seen)
                return;

            if (topic == CellTopics.LineState)
                LogLineState((CellState)(int)previous, (CellState)(int)value, message.UnixMs);
            else if (topic == CellTopics.PressIsolated)
                Add(message.UnixMs, value != 0d ? "CNC isolated at its main isolator" : "CNC energy restored", LogSeverity.Info);
            else if (topic == CellTopics.PressLockout)
                Add(message.UnixMs, value != 0d ? "Lock and tag on the CNC isolator" : "Lock and tag removed", LogSeverity.Info);
            else if (topic == CellTopics.TrainingStep)
                LogTrainingStep((int)previous, (int)value, message.UnixMs);
            else if (topic == CellTopics.TrainingErrors && value > previous)
                Add(message.UnixMs, MistakeText(), LogSeverity.Warning);
            else if (topic == CellTopics.TrainingResult)
                LogTrainingResult((TrainingOutcome)(int)value, message.UnixMs);
            else if (topic == CellTopics.LineStartsRefused && value > previous)
                Add(message.UnixMs, StartRefusedText(), LogSeverity.Warning);
            else if (topic == CellTopics.SafetyState)
                LogSafetyState((SafetyState)(int)previous, (SafetyState)(int)value, message.UnixMs);
            else if (topic == CellTopics.PressHealth)
                LogPressHealth((MachineHealth)(int)previous, (MachineHealth)(int)value, message.UnixMs);
            else if (topic == CellTopics.PressMaintenanceDue && value != 0d)
                LogMaintenanceDue(message.UnixMs);
        }

        // The risk reporter publishes tool life before the due flag, so the remaining life here is this reading's.
        // Near the end of tool life the advice is a tool change; further out, a warning from another mode.
        private void LogMaintenanceDue(long unixMs)
        {
            if (!_last.TryGetValue(CellTopics.PressToolRemaining, out double remaining) || remaining > ToolChangeAdviceMin)
            {
                Add(unixMs, "Maintenance due", LogSeverity.Warning);
                return;
            }
            Add(unixMs, remaining <= 0d
                ? "Tool change due now"
                : "Tool change due: " + remaining.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " min of tool life left",
                LogSeverity.Warning);
        }

        private void LogLineState(CellState from, CellState to, long unixMs)
        {
            if (to == CellState.Held)
                Add(unixMs, PlcRunsLine() && !PlcLinkUp() ? "Line stopped: PLC link lost" : "Line stopped: CNC fault", LogSeverity.Fault);
            else if (to == CellState.Stalled)
                Add(unixMs, "Line stalled: part not picked", LogSeverity.Warning);
            else if (to == CellState.Stopped)
                Add(unixMs, "Line stopped by the operator", LogSeverity.Info);
            else if (from == CellState.Stopped)
                Add(unixMs, "Line started", LogSeverity.Info);
            else if (from == CellState.Held || from == CellState.Stalled || from == CellState.SafetyStop)
                Add(unixMs, "Line running again", LogSeverity.Info);
            // Entering a protective stop is logged from the safety state, which also covers a PLC-run line.
        }

        private void LogSafetyState(SafetyState from, SafetyState to, long unixMs)
        {
            switch (to)
            {
                case SafetyState.Stopped:
                    Add(unixMs, "Protective stop: person in robot zone", LogSeverity.Fault);
                    break;
                case SafetyState.AwaitingReset:
                    Add(unixMs, "Robot zone clear: press reset to restart", LogSeverity.Warning);
                    break;
                case SafetyState.Reduced:
                    Add(unixMs, from == SafetyState.AwaitingReset
                        ? "Safety reset: arm released at reduced speed"
                        : "Person in warning zone: arm slowed", LogSeverity.Info);
                    break;
                case SafetyState.Normal:
                    Add(unixMs, from == SafetyState.AwaitingReset ? "Safety reset: arm released" : "Warning zone clear: arm at full speed",
                        LogSeverity.Info);
                    break;
            }
        }

        // training/step is the step due: going up by one means the step before it was done; going back to 1 is a new run.
        private void LogTrainingStep(int from, int to, long unixMs)
        {
            if (to == 1 && from != 1)
                Add(unixMs, "Lockout training started", LogSeverity.Info);
            else if (to == from + 1 && from >= 1 && from <= LockoutProcedure.Steps.Count)
                Add(unixMs, $"Step {from} done: {LockoutProcedure.Steps[from - 1].Title}", LogSeverity.Info);
        }

        // The action the controller judged a mistake, against the step that was due when the trainee did it.
        private string MistakeText()
        {
            var action = _last.TryGetValue(CellTopics.TrainingMistake, out double a) ? (LockoutAction)(int)a : LockoutAction.None;
            int due = _last.TryGetValue(CellTopics.TrainingStep, out double s) ? (int)s : 0;
            return due >= 1 && due <= LockoutProcedure.Steps.Count
                ? $"Mistake: {LockoutProcedure.Describe(action)} before: {LockoutProcedure.Steps[due - 1].Title}"
                : "Mistake: " + LockoutProcedure.Describe(action);
        }

        private void LogTrainingResult(TrainingOutcome outcome, long unixMs)
        {
            if (outcome == TrainingOutcome.Passed)
                Add(unixMs, "Lockout training passed", LogSeverity.Info);
            else if (outcome == TrainingOutcome.PassedWithErrors)
            {
                int mistakes = _last.TryGetValue(CellTopics.TrainingErrors, out double e) ? (int)e : 0;
                Add(unixMs, $"Lockout training finished with {mistakes} mistake{(mistakes == 1 ? "" : "s")}", LogSeverity.Warning);
            }
        }

        // The logic source refuses a start while the CNC is locked out or isolated; the lock is the reason to name.
        private string StartRefusedText()
        {
            if (_last.TryGetValue(CellTopics.PressLockout, out double locked) && locked != 0d)
                return "Start refused: CNC locked out";
            if (_last.TryGetValue(CellTopics.PressIsolated, out double isolated) && isolated != 0d)
                return "Start refused: CNC isolated";
            return "Start refused";
        }

        private bool PlcRunsLine() => _last.TryGetValue(CellTopics.PlcSource, out double source) && source >= 0.5d;

        private bool PlcLinkUp() => !_last.TryGetValue(CellTopics.PlcLink, out double link) || link != 0d;

        // The press station is shown as a CNC machining centre (topics stay cell/press/*).
        // A failure is logged once, when the press's health turns to Fault, naming every mode active at that moment.
        // The dataset's failure rows often carry several flags that come and go row to row, so logging each flag
        // would fill the log while one fault is in progress.
        private void LogPressHealth(MachineHealth from, MachineHealth to, long unixMs)
        {
            if (to == MachineHealth.Fault)
            {
                FaultMode active = ActiveFaults();
                Add(unixMs, active == FaultMode.None ? "CNC failure" : ModeList("CNC failure: ", active), LogSeverity.Fault);
            }
            else if (to == MachineHealth.Warning)
                Add(unixMs, "CNC warning: failure risk rising", LogSeverity.Warning);
            else if (to == MachineHealth.Ok && from != MachineHealth.Ok)
                Add(unixMs, "CNC healthy again", LogSeverity.Info);
        }

        private void LogInjection(FaultMode mode, long unixMs)
        {
            if (mode == FaultMode.None)
                Add(unixMs, "Injected fault cleared", LogSeverity.Info);
            else
                Add(unixMs, ModeList("Fault injected: ", mode), LogSeverity.Warning);
        }

        private FaultMode ActiveFaults()
        {
            FaultMode active = FaultMode.None;
            for (int i = 0; i < CellTopics.PressFaults.Count; i++)
            {
                if (_last.TryGetValue(CellTopics.PressFaults[i], out double flag) && flag != 0d)
                    active |= (FaultMode)(1 << i);
            }
            return active;
        }

        private static string ModeList(string prefix, FaultMode modes)
        {
            var text = new StringBuilder(prefix);
            bool first = true;
            for (int i = 0; i < CellTopics.PressFaults.Count; i++)
            {
                var flag = (FaultMode)(1 << i);
                if ((modes & flag) == 0)
                    continue;
                if (!first)
                    text.Append(", ");
                text.Append(FaultName(flag));
                first = false;
            }
            return text.ToString();
        }

        /// <summary>The AI4I failure modes in words a plant manager uses.</summary>
        public static string FaultName(FaultMode mode)
        {
            switch (mode)
            {
                case FaultMode.ToolWear: return "tool wear";
                case FaultMode.HeatDissipation: return "heat dissipation";
                case FaultMode.Power: return "power";
                case FaultMode.Overstrain: return "overstrain";
                case FaultMode.Random: return "random failure";
                default: return mode.ToString();
            }
        }

        private void Add(long unixMs, string text, LogSeverity severity)
        {
            _entries.Insert(0, new LogEntry(unixMs, text, severity));
            if (_entries.Count > _capacity)
                _entries.RemoveAt(_entries.Count - 1);
            Changed?.Invoke();
        }
    }
}
