using System;
using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training: one thing the trainee does at the CNC or the line. Published as a number on cell/training/action.
    /// Start is pressed twice in a lockout: once to verify the isolation (nothing may move) and once to restart.
    /// </summary>
    public enum LockoutAction
    {
        None = 0,
        Notify = 1,
        Stop = 2,
        IsolatorOff = 3,
        ApplyLock = 4,
        ReleaseEnergy = 5,
        Start = 6,
        ChangeTool = 7,
        Inspect = 8,
        ClearAndNotify = 9,
        RemoveLock = 10,
        IsolatorOn = 11,
    }

    /// <summary>Training: how a lockout run stands. Published as a number on cell/training/result.</summary>
    public enum TrainingOutcome
    {
        Idle = 0,
        InProgress = 1,
        Passed = 2,
        PassedWithErrors = 3,
    }

    /// <summary>Training: one step of the lockout procedure, with the paragraph of 29 CFR 1910.147 it follows.</summary>
    public readonly struct LockoutStep
    {
        public LockoutAction Action { get; }
        public string Title { get; }
        public string Regulation { get; }

        public LockoutStep(LockoutAction action, string title, string regulation)
        {
            Action = action;
            Title = title;
            Regulation = regulation;
        }
    }

    /// <summary>Training: one line of the run's history, a step done or a mistake, at seconds from the start.</summary>
    public readonly struct TrainingEvent
    {
        public double AtSeconds { get; }

        /// <summary>1-based step the trainee was on (the step done, or the step that was due when the mistake happened).</summary>
        public int Step { get; }
        public LockoutAction Action { get; }
        public bool Mistake { get; }
        public string Text { get; }

        public TrainingEvent(double atSeconds, int step, LockoutAction action, bool mistake, string text)
        {
            AtSeconds = atSeconds;
            Step = step;
            Action = action;
            Mistake = mistake;
            Text = text;
        }
    }

    /// <summary>
    /// Training: the lockout procedure for clearing a CNC fault, as a pure checker with no Unity dependency. It
    /// follows the energy-control sequence of OSHA 29 CFR 1910.147 for this cell. A training demo, not certified
    /// training. The trainee's actions come in one at a time; the expected one moves the procedure on, any other is
    /// a mistake, counted and named, and leaves the procedure where it was.
    /// </summary>
    public sealed class LockoutProcedure
    {
        /// <summary>The cell's procedure, in order.</summary>
        public static readonly IReadOnlyList<LockoutStep> Steps = new[]
        {
            new LockoutStep(LockoutAction.Notify, "Notify affected employees", "1910.147(c)(9)"),
            new LockoutStep(LockoutAction.Stop, "Stop the line", "1910.147(d)(2)"),
            new LockoutStep(LockoutAction.IsolatorOff, "Isolate the CNC at its main isolator", "1910.147(d)(3)"),
            new LockoutStep(LockoutAction.ApplyLock, "Apply your lock and tag", "1910.147(d)(4)"),
            new LockoutStep(LockoutAction.ReleaseEnergy, "Release stored energy: spindle at rest, air dumped", "1910.147(d)(5)"),
            new LockoutStep(LockoutAction.Start, "Verify the isolation: try to start, nothing may move", "1910.147(d)(6)"),
            new LockoutStep(LockoutAction.ChangeTool, "Change the tool", "Servicing"),
            new LockoutStep(LockoutAction.Inspect, "Inspect: tools removed, guards back on", "1910.147(e)(1)"),
            new LockoutStep(LockoutAction.ClearAndNotify, "Clear the area and notify employees", "1910.147(e)(2)"),
            new LockoutStep(LockoutAction.RemoveLock, "Remove your lock and tag", "1910.147(e)(3)"),
            new LockoutStep(LockoutAction.IsolatorOn, "Restore energy at the isolator", "1910.147(e)"),
            new LockoutStep(LockoutAction.Start, "Restart the line", "1910.147(e)"),
        };

        private readonly List<TrainingEvent> _events = new List<TrainingEvent>();

        public string Trainee { get; private set; } = string.Empty;

        /// <summary>Who supervised the run, for the training record.</summary>
        public string Trainer { get; private set; } = string.Empty;

        /// <summary>Wall-clock start of the run (unix ms), for the training record.</summary>
        public long StartedUnixMs { get; private set; }

        /// <summary>Steps done so far; the next one due is Steps[Done] while the run is in progress.</summary>
        public int Done { get; private set; }
        public int Errors { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsComplete => Done == Steps.Count;
        public IReadOnlyList<TrainingEvent> Events => _events;

        /// <summary>The step due next, or null when the run is idle or complete.</summary>
        public LockoutStep? Expected => IsRunning && !IsComplete ? Steps[Done] : (LockoutStep?)null;

        /// <summary>1-based number of the step due next: 0 before a run, Steps.Count + 1 once it is complete.</summary>
        public int StepNumber => IsRunning || IsComplete ? Done + 1 : 0;

        public TrainingOutcome Result
        {
            get
            {
                if (IsComplete)
                    return Errors == 0 ? TrainingOutcome.Passed : TrainingOutcome.PassedWithErrors;
                return IsRunning ? TrainingOutcome.InProgress : TrainingOutcome.Idle;
            }
        }

        /// <summary>Starts a fresh run for <paramref name="trainee"/>; any run in progress is discarded.</summary>
        public void Begin(string trainee, long startedUnixMs, string trainer = "")
        {
            Trainee = trainee ?? string.Empty;
            Trainer = trainer ?? string.Empty;
            StartedUnixMs = startedUnixMs;
            Done = 0;
            Errors = 0;
            _events.Clear();
            IsRunning = true;
        }

        /// <summary>
        /// The trainee did <paramref name="action"/> at <paramref name="atSeconds"/> from the start. True if it was
        /// the step due; false if it was a mistake (counted) or no run is in progress.
        /// </summary>
        public bool Attempt(LockoutAction action, double atSeconds)
        {
            if (!IsRunning || IsComplete || action == LockoutAction.None)
                return false;

            LockoutStep due = Steps[Done];
            if (action == due.Action)
            {
                Done++;
                _events.Add(new TrainingEvent(atSeconds, Done, action, false, due.Title));
                if (IsComplete)
                    IsRunning = false;
                return true;
            }

            Errors++;
            _events.Add(new TrainingEvent(atSeconds, Done + 1, action, true, $"{Describe(action)} before: {due.Title}"));
            return false;
        }

        /// <summary>Ends a run early; the record keeps what was done.</summary>
        public void Abort()
        {
            IsRunning = false;
        }

        /// <summary>
        /// Whether the plant carries out <paramref name="action"/> even when it is out of order. Start always goes
        /// to the line, so the logic source's own interlock is what refuses it while the lock is on; every other
        /// out-of-order action is held back by the training, so a mistake costs an error, not an injury.
        /// </summary>
        public static bool AlwaysReachesThePlant(LockoutAction action) => action == LockoutAction.Start;

        /// <summary>The action in the words the event log and the training record use.</summary>
        public static string Describe(LockoutAction action)
        {
            switch (action)
            {
                case LockoutAction.Notify: return "Notify";
                case LockoutAction.Stop: return "Stop the line";
                case LockoutAction.IsolatorOff: return "Isolator off";
                case LockoutAction.ApplyLock: return "Apply lock and tag";
                case LockoutAction.ReleaseEnergy: return "Release stored energy";
                case LockoutAction.Start: return "Start";
                case LockoutAction.ChangeTool: return "Change tool";
                case LockoutAction.Inspect: return "Inspect";
                case LockoutAction.ClearAndNotify: return "Clear and notify";
                case LockoutAction.RemoveLock: return "Remove lock and tag";
                case LockoutAction.IsolatorOn: return "Isolator on";
                default: return "Nothing";
            }
        }
    }
}
