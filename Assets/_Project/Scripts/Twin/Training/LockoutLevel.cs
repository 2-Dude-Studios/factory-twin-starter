using System.Collections.Generic;
using System.Globalization;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training areas (M24): scores a lockout run (M11's <see cref="LockoutProcedure"/>) as level 3 of the lockout
    /// drill, with the shared stars. Each of the 14 steps is a required step. A step out of order is a minor mistake.
    /// Two mistakes are critical and fail the level whatever the points: starting the work before the try-start has
    /// verified the isolation (1910.147(d)(6)), and calling the area clear with someone still in it
    /// (1910.147(e)(2)(i)). The lockout's own record (TrainingRecord) is unchanged.
    /// </summary>
    public static class LockoutLevel
    {
        public const int Level = 3;
        public const int StepPoints = 25;
        public const int MistakePoints = 25;

        /// <summary>The par time for three stars, in seconds: a brisk guided run takes about three minutes.</summary>
        public const double ParSeconds = 300d;

        public const string BadgeTryStart = "Try-start";
        public const string BadgeCleanRelease = "Clean release";

        public const string WorkBeforeVerify = "Work started before the isolation was verified";
        public const string AreaNotClear = "Area called clear with someone still in it";

        // The try-start is step 7; the release steps begin with the inspection, step 9.
        private const int VerifyStep = 7;
        private const int FirstReleaseStep = 9;

        public static List<DrillStep> RequiredSteps()
        {
            var steps = new List<DrillStep>(LockoutProcedure.Steps.Count);
            for (int i = 0; i < LockoutProcedure.Steps.Count; i++)
            {
                LockoutStep step = LockoutProcedure.Steps[i];
                steps.Add(new DrillStep(StepId(i + 1), step.Title, step.Regulation, StepPoints));
            }
            return steps;
        }

        public static string StepId(int number) => number.ToString(CultureInfo.InvariantCulture);

        /// <summary>Whether a mistake in a lockout run is one of the two that fail the level.</summary>
        public static bool IsCritical(TrainingEvent e, out string text, out string regulation)
        {
            text = regulation = string.Empty;
            if (!e.Mistake)
                return false;
            if (e.Action == LockoutAction.ChangeTool && e.Step <= VerifyStep)
            {
                text = WorkBeforeVerify;
                regulation = "1910.147(d)(6)";
                return true;
            }
            if (e.Action == LockoutAction.CheckArea && e.Step == 10)
            {
                text = AreaNotClear;
                regulation = "1910.147(e)(2)(i)";
                return true;
            }
            return false;
        }

        /// <summary>The run as a finished level score: passed with its stars, or failed and why.</summary>
        public static DrillScore Score(LockoutProcedure run)
        {
            var score = new DrillScore(TrainingArea.Lockout, Level, RequiredSteps(), ParSeconds);
            score.Start(0d);
            double last = 0d;
            bool verifiedFirst = true, cleanRelease = true;
            foreach (TrainingEvent e in run.Events)
            {
                last = e.AtSeconds;
                if (!e.Mistake)
                {
                    score.Done(StepId(e.Step), e.AtSeconds);
                    continue;
                }
                if (e.Step >= FirstReleaseStep)
                    cleanRelease = false;
                if (IsCritical(e, out string text, out string regulation))
                {
                    if (e.Action == LockoutAction.ChangeTool)
                        verifiedFirst = false;
                    score.Critical(text, regulation, e.AtSeconds);
                }
                else
                {
                    int index = System.Math.Min(System.Math.Max(e.Step, 1), LockoutProcedure.Steps.Count) - 1;
                    score.Mistake(e.Text, LockoutProcedure.Steps[index].Regulation, MistakePoints, e.AtSeconds);
                }
            }
            if (verifiedFirst && score.IsDone(StepId(VerifyStep)))
                score.Badge(BadgeTryStart, last);
            if (cleanRelease && run.IsComplete)
                score.Badge(BadgeCleanRelease, last);
            score.Finish(last);
            return score;
        }
    }
}
