using System;
using System.Collections.Generic;
using System.Globalization;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>The headset hub's game layer (M28): a trainee's XP, day streak and daily challenge, as kept in the badge book.</summary>
    [Serializable]
    public sealed class ProgressState
    {
        public int xp;
        public int streak;
        public string lastDay = string.Empty;
        public string dailyDoneDay = string.Empty;
    }

    /// <summary>One run's XP: what it earned, whether it was the daily challenge, and the streak after it.</summary>
    public readonly struct XpGain
    {
        public XpGain(int xp, bool daily, int streak)
        {
            Xp = xp;
            Daily = daily;
            Streak = streak;
        }

        public int Xp { get; }
        public bool Daily { get; }
        public int Streak { get; }
    }

    /// <summary>
    /// The headset hub's game layer (M28): every point a run earns is XP; the day's challenge (one training and level,
    /// picked from the date so everyone has the same one) pays double once a day; training on consecutive days builds
    /// a streak. Pure, so the rules are tested outside Unity. A game layer on a training demo, not a qualification.
    /// </summary>
    public static class TraineeProgress
    {
        public const int DailyMultiplier = 2;

        /// <summary>What a daily challenge can be: the trainings and levels that score today (level 0: no levels).</summary>
        public static readonly IReadOnlyList<(TrainingArea Area, int Level)> Challenges = new[]
        {
            (TrainingArea.RobotCell, 1), (TrainingArea.Lockout, 3), (TrainingArea.RobotCell, 2), (TrainingArea.Fire, 0),
            (TrainingArea.RobotCell, 3), (TrainingArea.Lockout, 3), (TrainingArea.RobotCell, 4), (TrainingArea.Fire, 0),
        };

        public static string Day(DateTime utc) => utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>The day's challenge, the same for every trainee on that (UTC) day.</summary>
        public static (TrainingArea Area, int Level) Daily(DateTime utc)
        {
            int days = (int)(utc.Date - new DateTime(2026, 1, 1)).TotalDays;
            int index = ((days % Challenges.Count) + Challenges.Count) % Challenges.Count;
            return Challenges[index];
        }

        /// <summary>
        /// The day's challenge for one trainee: a level they have not unlocked yet becomes the highest level they have
        /// in the same training (level 1 is always open), so the hub never offers a locked level.
        /// </summary>
        public static (TrainingArea Area, int Level) DailyFor(DateTime utc, Func<TrainingArea, int, bool> unlocked)
        {
            (TrainingArea area, int level) = Daily(utc);
            while (unlocked != null && level > 1 && !unlocked(area, level))
                level--;
            return (area, level);
        }

        public static bool DailyDone(ProgressState state, DateTime utc) => state != null && state.dailyDoneDay == Day(utc);

        /// <summary>
        /// Adds a run of <paramref name="points"/> on <paramref name="area"/> (level 0 for trainings without levels):
        /// doubled if it is the day's challenge, not yet done today and scored; the streak counts consecutive days.
        /// <paramref name="unlocked"/> is the trainee's level locks before this run (see <see cref="DailyFor"/>).
        /// </summary>
        public static XpGain Gain(ProgressState state, TrainingArea area, int level, int points, DateTime utc,
            Func<TrainingArea, int, bool> unlocked = null)
        {
            string today = Day(utc);
            if (state.lastDay != today)
                state.streak = state.lastDay == Day(utc.AddDays(-1)) ? state.streak + 1 : 1;
            state.lastDay = today;

            int xp = Math.Max(points, 0);
            (TrainingArea dailyArea, int dailyLevel) = DailyFor(utc, unlocked);
            bool daily = xp > 0 && state.dailyDoneDay != today && area == dailyArea && level == dailyLevel;
            if (daily)
            {
                xp *= DailyMultiplier;
                state.dailyDoneDay = today;
            }
            state.xp += xp;
            return new XpGain(xp, daily, state.streak);
        }
    }
}
