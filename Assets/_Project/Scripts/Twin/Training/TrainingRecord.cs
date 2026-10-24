using System;
using System.Globalization;
using System.Text;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training: writes a lockout run as CSV, one row per step done or mistake made, in the spirit of the training
    /// certification in 29 CFR 1910.147(c)(7)(iv) (who, when, what). A demo record, not a certification.
    /// </summary>
    public static class TrainingRecord
    {
        public const string Header = "trainee,started_utc,at_s,step,regulation,kind,detail";

        public static string ToCsv(LockoutProcedure run)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));

            string started = DateTimeOffset.FromUnixTimeMilliseconds(run.StartedUnixMs).UtcDateTime
                .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csv = new StringBuilder();
            csv.Append(Header).Append('\n');

            foreach (TrainingEvent e in run.Events)
            {
                int index = Math.Min(Math.Max(e.Step, 1), LockoutProcedure.Steps.Count) - 1;
                Row(csv, run.Trainee, started, e.AtSeconds.ToString("0.0", CultureInfo.InvariantCulture),
                    e.Step.ToString(CultureInfo.InvariantCulture), LockoutProcedure.Steps[index].Regulation,
                    e.Mistake ? "mistake" : "done", e.Text);
            }

            string summary = $"{run.Done} of {LockoutProcedure.Steps.Count} steps, {run.Errors} mistake{(run.Errors == 1 ? "" : "s")}";
            Row(csv, run.Trainee, started, string.Empty, string.Empty, string.Empty, ResultWord(run.Result), summary);
            return csv.ToString();
        }

        public static string ResultWord(TrainingOutcome result)
        {
            switch (result)
            {
                case TrainingOutcome.Passed: return "passed";
                case TrainingOutcome.PassedWithErrors: return "passed with mistakes";
                case TrainingOutcome.InProgress: return "not finished";
                default: return "not started";
            }
        }

        private static void Row(StringBuilder csv, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0)
                    csv.Append(',');
                csv.Append(Quote(cells[i]));
            }
            csv.Append('\n');
        }

        private static string Quote(string cell)
        {
            return cell.IndexOfAny(QuoteTriggers) < 0 ? cell : "\"" + cell.Replace("\"", "\"\"") + "\"";
        }

        private static readonly char[] QuoteTriggers = { ',', '"', '\n' };
    }
}
