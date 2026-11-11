using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training areas (M24): the words on the hub's badge wall and on each area's score board, from the trainee's
    /// badge book: stars per level, the best score, the badges, and when the drill is due again. Stars are written as
    /// "**-" so any font shows them.
    /// </summary>
    public static class DrillBoardText
    {
        private static readonly TrainingArea[] Drills =
        {
            TrainingArea.RobotCell, TrainingArea.Lockout, TrainingArea.Fire, TrainingArea.Electrical,
        };

        /// <summary>The hub's badge wall: every drill's stars, best score, badges and refresher date for one trainee.</summary>
        public static string BadgeWall(BadgeBook book, DateTime nowUtc)
        {
            var text = new StringBuilder();
            text.Append(book.Trainee).Append('\n');
            foreach (TrainingArea area in Drills)
            {
                TrainingMode mode = TrainingLaunch.ModeFor(area);
                IReadOnlyList<int> stars = book.Stars(mode);
                text.Append('\n').Append(TrainingAreaDefaults.For(area).Title.ToUpperInvariant()).Append('\n');
                for (int level = 1; level <= DrillLevels.Count; level++)
                    text.Append(level > 1 ? "   " : string.Empty).Append('L').Append(level).Append(' ').Append(DrillStars.Text(stars[level - 1]));
                text.Append("   best ").Append(book.BestScore(mode)).Append('\n');
                IReadOnlyList<string> badges = book.Earned(mode);
                if (badges.Count > 0)
                    text.Append("Badges: ").Append(string.Join(", ", badges)).Append('\n');
                text.Append(Due(book.AssessedUtc(mode), area, nowUtc)).Append('\n');
            }
            text.Append('\n').Append(DrillLevels.DemoNotice);
            return text.ToString();
        }

        /// <summary>One area's score board: its four levels with stars (or locked), the best score, badges, the star rules.</summary>
        public static string AreaBoard(BadgeBook book, TrainingArea area, DateTime nowUtc)
        {
            TrainingMode mode = TrainingLaunch.ModeFor(area);
            IReadOnlyList<int> stars = book.Stars(mode);
            var text = new StringBuilder();
            text.Append(book.Trainee).Append('\n').Append('\n');
            for (int level = 1; level <= DrillLevels.Count; level++)
            {
                text.Append('L').Append(level).Append(' ').Append(DrillLevels.LevelName(level)).Append(":  ");
                // A level with stars shows them even if an earlier level has none yet (a drill built level by level).
                bool open = stars[level - 1] > 0 || DrillProgress.Unlocked(stars, level);
                text.Append(open ? DrillStars.Text(stars[level - 1]) : "locked").Append('\n');
            }
            text.Append("Best score: ").Append(book.BestScore(mode)).Append('\n');
            IReadOnlyList<string> badges = book.Earned(mode);
            text.Append("Badges: ").Append(badges.Count > 0 ? string.Join(", ", badges) : "none yet").Append('\n');
            text.Append(Due(book.AssessedUtc(mode), area, nowUtc)).Append('\n');
            text.Append('\n').Append(TrainingAreaText.Stars());
            return text.ToString();
        }

        /// <summary>When a drill is due again, or that it is overdue, or that its assessment is still to pass.</summary>
        public static string Due(DateTime? assessedUtc, TrainingArea area, DateTime nowUtc)
        {
            DateTime? due = DrillProgress.DueAgain(assessedUtc, area);
            if (!due.HasValue)
                return "Assessment not passed yet";
            string month = due.Value.ToString("MMM yyyy", CultureInfo.InvariantCulture);
            return nowUtc > due.Value ? "Refresher overdue since " + month : "Due again: " + month;
        }
    }
}
