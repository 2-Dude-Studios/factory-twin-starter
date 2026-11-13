using System;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The AI trainer's voice (M29): which recorded line answers which moment. A drill's intro from its mode and level;
    /// the end of a run as a verdict (a critical mistake, the stars, or the points for a drill without stars) followed
    /// by at most a couple of extras, most important first (rank up, daily challenge, a new badge, a new best). Cue ids
    /// match the clip names in Assets/_Project/Audio/Trainer (cue_n.mp3) and tools/voice/trainer_lines.json. Pure, so
    /// it is tested.
    /// </summary>
    public static class TrainerLines
    {
        public const string GreetFirst = "greet_first", GreetBack = "greet_back", HubBack = "hub_back";
        public const string Step = "step", Mistake = "mistake", Refused = "refused";
        public const string FireStart = "fire_start", FireLeave = "fire_leave", PpeDone = "ppe_done";
        public const string ResultCritical = "result_critical", ResultFail = "result_fail", ResultScored = "result_scored";
        public const string NewBest = "new_best", NewBadge = "new_badge", RankUp = "rank_up", DailyDone = "daily_done", Streak = "streak";

        /// <summary>The intro for a training started from the hub; null for none.</summary>
        public static string Intro(TrainingMode mode, int level)
        {
            switch (mode)
            {
                case TrainingMode.RobotZone: return RobotIntro(level);
                case TrainingMode.Lockout: return "intro_lockout";
                case TrainingMode.FireSafety: return "intro_fire";
                case TrainingMode.Electrical: return "intro_electrical";
                case TrainingMode.FreeRoam: return "intro_free";
                default: return null;
            }
        }

        public static string RobotIntro(int level) => level >= 1 && level <= DrillLevels.Count ? $"intro_robot_{level}" : null;

        /// <summary>The lines for a finished run, in the order to say them.</summary>
        public static List<string> Result(RunResult result, int maxExtras = 2)
        {
            var lines = new List<string>();
            if (result == null)
                return lines;
            bool critical = false;
            if (result.Debrief != null)
            {
                foreach (string line in result.Debrief)
                    critical |= line != null && line.StartsWith("Critical:", StringComparison.Ordinal);
            }
            if (critical)
                lines.Add(ResultCritical);
            else if (result.Stars < 0)
                lines.Add(ResultScored);
            else if (result.Stars == 0)
                lines.Add(ResultFail);
            else
                lines.Add($"result_{Math.Min(result.Stars, DrillStars.Max)}");

            var extras = new List<string>();
            if (TraineeRank.For(result.XpAfter).Name != TraineeRank.For(result.XpBefore).Name && result.XpAfter > result.XpBefore)
                extras.Add(RankUp);
            if (result.Gain.Daily)
                extras.Add(DailyDone);
            if (result.NewBadges != null && result.NewBadges.Count > 0)
                extras.Add(NewBadge);
            if (result.NewBest && result.Points > 0 && !critical)
                extras.Add(NewBest);
            if (result.Gain.Daily && result.Gain.Streak > 1)
                extras.Add(Streak);
            for (int i = 0; i < extras.Count && i < Math.Max(0, maxExtras); i++)
                lines.Add(extras[i]);
            return lines;
        }
    }
}
