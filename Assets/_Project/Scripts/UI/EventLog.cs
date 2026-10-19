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

        public void Observe(SignalMessage message)
        {
            TopicPath topic = message.Topic;
            double value = message.Value.AsNumber;
            bool seen = _last.TryGetValue(topic, out double previous);
            if (seen && previous.Equals(value))
                return;
            _last[topic] = value;

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
            else if (topic == CellTopics.PressHealth)
                LogPressHealth((MachineHealth)(int)previous, (MachineHealth)(int)value, message.UnixMs);
            else if (value != 0d)
            {
                for (int i = 0; i < CellTopics.PressFaults.Count; i++)
                {
                    if (topic == CellTopics.PressFaults[i])
                    {
                        Add(message.UnixMs, "Press failure: " + FaultName((FaultMode)(1 << i)), LogSeverity.Fault);
                        return;
                    }
                }
            }
        }

        private void LogLineState(CellState from, CellState to, long unixMs)
        {
            if (to == CellState.Held)
                Add(unixMs, "Line stopped: press fault", LogSeverity.Fault);
            else if (to == CellState.Stalled)
                Add(unixMs, "Line stalled: part not picked", LogSeverity.Warning);
            else if (from == CellState.Held || from == CellState.Stalled)
                Add(unixMs, "Line running again", LogSeverity.Info);
        }

        private void LogPressHealth(MachineHealth from, MachineHealth to, long unixMs)
        {
            // A fault is logged by its failure-mode flag, which names the cause.
            if (to == MachineHealth.Warning)
                Add(unixMs, "Press warning: failure risk rising", LogSeverity.Warning);
            else if (to == MachineHealth.Ok && from != MachineHealth.Ok)
                Add(unixMs, "Press healthy again", LogSeverity.Info);
        }

        private void LogInjection(FaultMode mode, long unixMs)
        {
            if (mode == FaultMode.None)
            {
                Add(unixMs, "Injected fault cleared", LogSeverity.Info);
                return;
            }

            var text = new StringBuilder("Fault injected: ");
            bool first = true;
            for (int i = 0; i < CellTopics.PressFaults.Count; i++)
            {
                var flag = (FaultMode)(1 << i);
                if ((mode & flag) == 0)
                    continue;
                if (!first)
                    text.Append(", ");
                text.Append(FaultName(flag));
                first = false;
            }
            Add(unixMs, text.ToString(), LogSeverity.Warning);
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
