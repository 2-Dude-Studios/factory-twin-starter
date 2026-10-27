namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training (M12): when the simulated crew around the cell reacts to a lockout run, as pure rules over the step
    /// due. Affected employees stand clear from the moment they are notified of the shutdown until they are notified
    /// that the lock has come off (1910.147(c)(9) and (e)(2)(ii)); a colleague wanders into the work area after the
    /// servicing, so the trainee's area check (1910.147(e)(2)(i)) has something to find.
    /// </summary>
    public static class CrewRules
    {
        /// <summary>1-based number of the first and the last Notify step.</summary>
        public static int FirstNotifyStep => IndexOf(LockoutAction.Notify, first: true) + 1;
        public static int LastNotifyStep => IndexOf(LockoutAction.Notify, first: false) + 1;

        /// <summary>True while the affected employees should be standing clear of the cell.</summary>
        public static bool StandClear(int step, TrainingOutcome outcome) =>
            outcome == TrainingOutcome.InProgress && step > FirstNotifyStep && step <= LastNotifyStep;

        /// <summary>True from the step after the tool change until the area check is done: the colleague comes back.</summary>
        public static bool ColleagueInArea(int step, TrainingOutcome outcome)
        {
            int servicing = IndexOf(LockoutAction.ChangeTool, first: true) + 1;
            int areaCheck = IndexOf(LockoutAction.CheckArea, first: true) + 1;
            return outcome == TrainingOutcome.InProgress && step > servicing && step <= areaCheck;
        }

        private static int IndexOf(LockoutAction action, bool first)
        {
            int found = -1;
            for (int i = 0; i < LockoutProcedure.Steps.Count; i++)
            {
                if (LockoutProcedure.Steps[i].Action != action)
                    continue;
                found = i;
                if (first)
                    break;
            }
            return found;
        }
    }
}
