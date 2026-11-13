using System.Collections.Generic;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class RunDebriefTests
    {
        [Test]
        public void CriticalsComeFirstThenMissedThenMistakes()
        {
            List<string> lines = RunDebrief.Build(new[] { "Reset from inside the zone" }, new[] { "Lock and tag" }, new[] { "Too close" }, 5);
            CollectionAssert.AreEqual(new[] { "Critical: Reset from inside the zone", "Missed: Lock and tag", "Mistake: Too close" }, lines);
        }

        [Test]
        public void ACleanRunHasNoLines()
        {
            Assert.IsEmpty(RunDebrief.Build(null, new string[0], new[] { " ", null }));
        }

        [Test]
        public void LongListsEndWithHowManyMore()
        {
            List<string> lines = RunDebrief.Build(null, new[] { "a", "b", "c", "d", "e" }, new[] { "a" }, 3);
            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual("Missed: a", lines[0]);
            StringAssert.Contains("3 more", lines[2]);
        }

        [Test]
        public void ALevelRunReportsWhatItMissed()
        {
            var score = new DrillScore(TrainingArea.RobotCell, 1,
                new[] { new DrillStep("warn", "Step into the warning zone", "ISO 13855"), new DrillStep("stop", "Step into the protective zone", "ISO 13855") }, 60d);
            score.Start(0d);
            score.Done("warn", 1d);
            score.Mistake("Walked in front of the arm", "ISO 10218-2", 20, 2d);
            List<string> lines = RunDebrief.For(score);
            CollectionAssert.AreEqual(new[] { "Missed: Step into the protective zone", "Mistake: Walked in front of the arm" }, lines);
        }
    }
}
