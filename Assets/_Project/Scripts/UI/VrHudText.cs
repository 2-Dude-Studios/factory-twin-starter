using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the words on the headset HUD and the training board. The status line puts safety first: while
    /// a protective stop or the warning zone applies, that is what the person in the headset needs to read.
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

        /// <summary>The step line under the status: empty while no lockout run is on.</summary>
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
