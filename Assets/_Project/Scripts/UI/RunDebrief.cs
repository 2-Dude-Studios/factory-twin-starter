using System;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub's game layer (M29): what to look at again after a run, for the results card. Critical mistakes
    /// first (they fail the run), then required steps not done, then minor mistakes; each once, at most a few lines,
    /// with a last line saying how many more are in the training record. Pure, so it is tested.
    /// </summary>
    public static class RunDebrief
    {
        public const int DefaultLines = 3;

        public static List<string> Build(IEnumerable<string> criticals, IEnumerable<string> missed, IEnumerable<string> mistakes,
            int maxLines = DefaultLines)
        {
            maxLines = Math.Max(1, maxLines);
            var all = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Add(all, seen, criticals, "Critical: ");
            Add(all, seen, missed, "Missed: ");
            Add(all, seen, mistakes, "Mistake: ");
            if (all.Count <= maxLines)
                return all;
            List<string> shown = all.GetRange(0, maxLines - 1);
            int more = all.Count - shown.Count;
            shown.Add($"…and {more} more in the training record.");
            return shown;
        }

        /// <summary>A level run's debrief: its critical mistakes, the steps it missed and its minor mistakes.</summary>
        public static List<string> For(DrillScore score, int maxLines = DefaultLines)
        {
            if (score == null)
                return new List<string>();
            var criticals = new List<string>();
            var mistakes = new List<string>();
            foreach (DrillEvent e in score.Events)
            {
                if (e.Kind == DrillEventKind.Critical)
                    criticals.Add(e.Text);
                else if (e.Kind == DrillEventKind.Mistake)
                    mistakes.Add(e.Text);
            }
            var missed = new List<string>();
            foreach (DrillStep step in score.Missing())
                missed.Add(step.Text);
            return Build(criticals, missed, mistakes, maxLines);
        }

        private static void Add(List<string> into, HashSet<string> seen, IEnumerable<string> texts, string prefix)
        {
            if (texts == null)
                return;
            foreach (string text in texts)
            {
                if (string.IsNullOrWhiteSpace(text) || !seen.Add(text.Trim()))
                    continue;
                into.Add(prefix + text.Trim());
            }
        }
    }
}
