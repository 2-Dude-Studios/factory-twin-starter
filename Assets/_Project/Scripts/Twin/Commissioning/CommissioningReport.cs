using System.Collections.Generic;
using System.Text;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Commissioning: how a scenario ended in one suite run.</summary>
    public enum ScenarioOutcome
    {
        Running = 0,
        Passed = 1,
        Failed = 2,

        /// <summary>Not meaningful for this logic source (e.g. losing the PLC link while Unity runs the line).</summary>
        NotApplicable = 3,

        /// <summary>The line did not come back to a running state in time to start it.</summary>
        NotRun = 4,
    }

    /// <summary>Commissioning: one row of the suite's report.</summary>
    public readonly struct ScenarioResult
    {
        public ScenarioResult(int index, string name, ScenarioOutcome outcome, string detail, IReadOnlyList<ScenarioCheck> checks)
        {
            Index = index;
            Name = name;
            Outcome = outcome;
            Detail = detail ?? string.Empty;
            Checks = checks;
        }

        public int Index { get; }
        public string Name { get; }
        public ScenarioOutcome Outcome { get; }
        public string Detail { get; }
        public IReadOnlyList<ScenarioCheck> Checks { get; }
    }

    /// <summary>Commissioning: writes a suite run as CSV, one line per check, so an engineer can file it.</summary>
    public static class CommissioningReport
    {
        public const string Header = "logic_source,program,scenario,check,outcome,detail";

        public static string ToCsv(string logicSource, string program, IEnumerable<ScenarioResult> results)
        {
            var csv = new StringBuilder(Header).Append('\n');
            foreach (ScenarioResult result in results)
            {
                if (result.Checks == null || result.Checks.Count == 0)
                {
                    Row(csv, logicSource, program, result.Name, string.Empty, Words(result.Outcome), result.Detail);
                    continue;
                }
                foreach (ScenarioCheck check in result.Checks)
                    Row(csv, logicSource, program, result.Name, check.Name, Words(check.Outcome), check.Detail);
            }
            return csv.ToString();
        }

        public static string Words(ScenarioOutcome outcome)
        {
            switch (outcome)
            {
                case ScenarioOutcome.Passed: return "pass";
                case ScenarioOutcome.Failed: return "FAIL";
                case ScenarioOutcome.NotApplicable: return "n/a";
                case ScenarioOutcome.NotRun: return "not run";
                default: return "running";
            }
        }

        private static string Words(CheckOutcome outcome) =>
            outcome == CheckOutcome.Passed ? "pass" : outcome == CheckOutcome.Failed ? "FAIL" : "pending";

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
            cell ??= string.Empty;
            return cell.IndexOfAny(QuoteTriggers) < 0 ? cell : "\"" + cell.Replace("\"", "\"\"") + "\"";
        }

        private static readonly char[] QuoteTriggers = { ',', '"', '\n' };
    }
}
