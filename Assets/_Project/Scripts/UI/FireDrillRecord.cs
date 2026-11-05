using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): the fire drill's training record as CSV, the same columns as the lockout record
    /// (TrainingRecord): one row per scored step or mistake with the rule it follows, one per badge, and the total.
    /// A demo record, not a certification.
    /// </summary>
    public static class FireDrillRecord
    {
        public const string Header = "trainee,trainer,started_utc,at_s,step,regulation,kind,detail";

        public static string ToCsv(string trainee, string trainer, DateTime startedUtc, FireDrillScore score)
        {
            if (score == null)
                throw new ArgumentNullException(nameof(score));
            string started = startedUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csv = new StringBuilder();
            csv.Append(Header).Append('\n');
            int step = 0;
            foreach (ScoreEvent e in score.Events)
            {
                string at = e.AtSeconds.ToString("0.0", CultureInfo.InvariantCulture);
                if (e.Points != 0)
                {
                    step++;
                    string kind = e.Mistake ? "mistake" : "done";
                    string points = (e.Points > 0 ? "+" : string.Empty) + e.Points.ToString(CultureInfo.InvariantCulture);
                    Row(csv, trainee, trainer, started, at, step.ToString(CultureInfo.InvariantCulture), e.Regulation, kind, $"{e.Text} ({points})");
                }
                if (e.Badge != null)
                    Row(csv, trainee, trainer, started, at, string.Empty, string.Empty, "badge", e.Badge);
            }
            Row(csv, trainee, trainer, started, string.Empty, string.Empty, string.Empty, "result",
                $"{score.Total} points over {score.Runs} fire{(score.Runs == 1 ? "" : "s")}, {score.Badges.Count} badge{(score.Badges.Count == 1 ? "" : "s")}");
            return csv.ToString();
        }

        private static void Row(StringBuilder csv, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0)
                    csv.Append(',');
                string cell = cells[i] ?? string.Empty;
                bool quote = cell.IndexOfAny(new[] { ',', '"', '\n' }) >= 0;
                csv.Append(quote ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell);
            }
            csv.Append('\n');
        }

        /// <summary>The badges, in the order they were earned.</summary>
        public static List<string> BadgesInOrder(FireDrillScore score)
        {
            var list = new List<string>();
            foreach (ScoreEvent e in score.Events)
            {
                if (e.Badge != null)
                    list.Add(e.Badge);
            }
            return list;
        }
    }
}
