using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the words on the headset HUD and the training board. The HUD leads with the task at hand and
    /// the one after it; the status line puts safety first, because while a protective stop or the warning zone
    /// applies, that is what the person in the headset needs to read.
    /// </summary>
    public static class VrHudText
    {
        public static string Status(SafetyState safety, CellState line)
        {
            switch (safety)
            {
                case SafetyState.Stopped: return "Protective stop: you are in the robot zone";
                case SafetyState.AwaitingReset: return "Robot zone clear: press Safety reset";
                case SafetyState.Reduced: return "Warning zone: the arm has slowed";
            }
            return "Line: " + DashboardViewModel.LineStateWords(line);
        }

        /// <summary>The small label over the current task: which step of how many, or how the run ended.</summary>
        public static string NowLabel(int step, TrainingOutcome outcome)
        {
            switch (outcome)
            {
                case TrainingOutcome.InProgress:
                    return step >= 1 && step <= LockoutProcedure.Steps.Count ? $"NOW · STEP {step} OF {LockoutProcedure.Steps.Count}" : "NOW";
                case TrainingOutcome.Passed:
                case TrainingOutcome.PassedWithErrors:
                    return "LOCKOUT COMPLETE";
                default:
                    return "LOCKOUT TRAINING";
            }
        }

        /// <summary>The task at hand.</summary>
        public static string Current(int step, TrainingOutcome outcome)
        {
            switch (outcome)
            {
                case TrainingOutcome.InProgress:
                    return step >= 1 && step <= LockoutProcedure.Steps.Count ? LockoutProcedure.Steps[step - 1].Title : string.Empty;
                case TrainingOutcome.Passed:
                    return "Passed, no mistakes";
                case TrainingOutcome.PassedWithErrors:
                    return DashboardViewModel.TrainingResultWords(outcome);
                default:
                    return "Not started: press Lockout training";
            }
        }

        /// <summary>The task after it; empty when there is none.</summary>
        public static string Next(int step, TrainingOutcome outcome)
        {
            if (outcome != TrainingOutcome.InProgress)
                return string.Empty;
            return step >= 1 && step < LockoutProcedure.Steps.Count ? LockoutProcedure.Steps[step].Title : "Last step";
        }

        /// <summary>Where the point for the step due is, and how to use it; empty outside a run.</summary>
        public static string Where(int step, TrainingOutcome outcome)
        {
            if (outcome != TrainingOutcome.InProgress || step < 1 || step > LockoutProcedure.Steps.Count)
                return string.Empty;
            return TrainingPoints.TryKindFor(LockoutProcedure.Steps[step - 1].Action, out TrainingPointKind kind)
                ? TrainingPoints.Where(kind) + ". " + How(kind)
                : string.Empty;
        }

        /// <summary>
        /// What the HUD says when an action is refused: what was refused, and the step due with where to do it;
        /// empty outside a run.
        /// </summary>
        public static string Refusal(LockoutAction refused, int step, TrainingOutcome outcome, bool areaOccupied = false)
        {
            if (outcome != TrainingOutcome.InProgress || step < 1 || step > LockoutProcedure.Steps.Count)
                return string.Empty;
            LockoutStep due = LockoutProcedure.Steps[step - 1];
            if (refused == LockoutAction.CheckArea && due.Action == LockoutAction.CheckArea && areaOccupied)
                return "Not clear: someone is still in the work area. Follow the beacon to them, point and pull the trigger to ask them to stand clear.";
            string where = TrainingPoints.TryKindFor(due.Action, out TrainingPointKind kind) ? " (" + TrainingPoints.Where(kind) + ")" : string.Empty;
            return $"Not yet: {LockoutProcedure.Describe(refused)}. First: {due.Title}{where}.";
        }

        /// <summary>How to use a point in the headset, in a sentence, for the HUD's where line.</summary>
        public static string How(TrainingPointKind kind)
        {
            switch (kind)
            {
                case TrainingPointKind.StartButton:
                case TrainingPointKind.StopButton:
                case TrainingPointKind.NotifyButton:
                case TrainingPointKind.AllClearButton: return "Press it with your finger, or point and pull the trigger.";
                case TrainingPointKind.Isolator: return "Grip the handle and twist it a quarter turn.";
                case TrainingPointKind.LockHasp: return "Grip your lock and hang it on the hasp; take it off the same way.";
                case TrainingPointKind.AirDumpValve: return "Grip the lever and twist it a quarter turn.";
                case TrainingPointKind.ToolChange: return "Grip the old tool out of the spindle and put the new one in.";
                case TrainingPointKind.ProcedureCard: return "Read it, then point at it and pull the trigger.";
                default: return "Point and pull the trigger.";
            }
        }

        /// <summary>How to use a point, in a few words, for the prompt while a controller is on it.</summary>
        public static string ShortHow(TrainingPointKind kind)
        {
            switch (kind)
            {
                case TrainingPointKind.StartButton:
                case TrainingPointKind.StopButton:
                case TrainingPointKind.NotifyButton:
                case TrainingPointKind.AllClearButton: return "press it or pull the trigger";
                case TrainingPointKind.Isolator:
                case TrainingPointKind.AirDumpValve: return "grip and twist";
                case TrainingPointKind.LockHasp: return "grip the lock, hang it on the hasp";
                case TrainingPointKind.ToolChange: return "grip the tool";
                default: return "pull the trigger";
            }
        }

        /// <summary>The step line under the status on the training board: empty while no lockout run is on.</summary>
        public static string Step(int step, TrainingOutcome outcome)
        {
            if (outcome == TrainingOutcome.Idle)
                return string.Empty;
            if (outcome != TrainingOutcome.InProgress)
                return "Lockout " + DashboardViewModel.TrainingResultWords(outcome).ToLowerInvariant();
            return step >= 1 && step <= LockoutProcedure.Steps.Count
                ? $"Step {step} of {LockoutProcedure.Steps.Count}: {LockoutProcedure.Steps[step - 1].Title}"
                : string.Empty;
        }

        /// <summary>The prompt for the training point under a controller, with how to use it.</summary>
        public static string Prompt(string pointPrompt, string how = "pull the trigger") =>
            string.IsNullOrEmpty(pointPrompt) ? string.Empty : pointPrompt + "  ·  " + how;
    }
}
