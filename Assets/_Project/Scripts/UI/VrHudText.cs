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

        /// <summary>The prompt for the training point under the controller ray.</summary>
        public static string Prompt(string pointPrompt) =>
            string.IsNullOrEmpty(pointPrompt) ? string.Empty : pointPrompt + "  ·  pull the trigger";
    }
}
