using System;
using System.Collections.Generic;
using System.Text;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub's credits (M29): splits the credits and the third-party notices into pages that fit the hub
    /// panel, each notice starting on a page of its own, so the full licence texts can be read in the headset with
    /// Previous and Next instead of a scroll view (dragging a scroll with a ray is awkward). Pure, so it is tested.
    /// </summary>
    public static class CreditPages
    {
        /// <param name="texts">The credits page first, then each notice in order; empty ones are skipped.</param>
        /// <param name="linesPerPage">Lines that fit the panel.</param>
        /// <param name="charsPerLine">Characters that fit one line; a longer line counts as the lines it wraps to.</param>
        public static List<string> Split(IEnumerable<string> texts, int linesPerPage, int charsPerLine)
        {
            linesPerPage = Math.Max(1, linesPerPage);
            charsPerLine = Math.Max(1, charsPerLine);
            var pages = new List<string>();
            if (texts == null)
                return pages;
            foreach (string text in texts)
            {
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                var page = new StringBuilder();
                int used = 0;
                foreach (string raw in text.Replace("\r\n", "\n").TrimEnd().Split('\n'))
                {
                    string line = raw.TrimEnd();
                    int cost = Math.Max(1, (line.Length + charsPerLine - 1) / charsPerLine);
                    if (used > 0 && used + cost > linesPerPage)
                    {
                        pages.Add(page.ToString().TrimEnd());
                        page.Clear();
                        used = 0;
                        if (line.Length == 0)
                            continue; // no blank line at the top of a page
                    }
                    page.Append(line).Append('\n');
                    used += cost;
                }
                if (page.Length > 0)
                    pages.Add(page.ToString().TrimEnd());
            }
            return pages;
        }
    }
}
