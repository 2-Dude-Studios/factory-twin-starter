using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class CreditPagesTests
    {
        [Test]
        public void EachTextStartsAPageAndNothingIsLost()
        {
            string notice = string.Join("\n", Enumerable.Range(1, 25).Select(i => "line " + i));
            List<string> pages = CreditPages.Split(new[] { "Credits", null, " ", notice }, 10, 80);

            Assert.AreEqual(4, pages.Count, "credits, then the notice over three pages; empty texts skipped");
            Assert.AreEqual("Credits", pages[0]);
            Assert.IsTrue(pages[1].StartsWith("line 1\n"));
            string joined = string.Join("\n", pages.Skip(1));
            for (int i = 1; i <= 25; i++)
                StringAssert.Contains("line " + i, joined);
        }

        [Test]
        public void LongLinesCountAsTheLinesTheyWrapTo()
        {
            string longLine = new string('x', 250); // three lines at 100 characters
            List<string> pages = CreditPages.Split(new[] { longLine + "\n" + longLine }, 4, 100);
            Assert.AreEqual(2, pages.Count);
        }

        [Test]
        public void NoPageStartsWithABlankLine()
        {
            string text = "a\nb\n\nc\nd";
            List<string> pages = CreditPages.Split(new[] { text }, 2, 80);
            CollectionAssert.AreEqual(new[] { "a\nb", "c\nd" }, pages);
        }
    }
}
