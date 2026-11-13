using System.Collections.Generic;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class TrainerLinesTests
    {
        [Test]
        public void IntrosMatchTheTrainingAndLevel()
        {
            Assert.AreEqual("intro_robot_3", TrainerLines.Intro(TrainingMode.RobotZone, 3));
            Assert.IsNull(TrainerLines.Intro(TrainingMode.RobotZone, 0), "the robot intro waits for its level");
            Assert.AreEqual("intro_lockout", TrainerLines.Intro(TrainingMode.Lockout, 0));
            Assert.AreEqual("intro_electrical", TrainerLines.Intro(TrainingMode.Electrical, 0));
            Assert.IsNull(TrainerLines.Intro(TrainingMode.None, 0));
        }

        [Test]
        public void TheVerdictComesFirst()
        {
            Assert.AreEqual("result_3", TrainerLines.Result(new RunResult { Stars = 3, Points = 500 })[0]);
            Assert.AreEqual("result_fail", TrainerLines.Result(new RunResult { Stars = 0 })[0]);
            Assert.AreEqual("result_scored", TrainerLines.Result(new RunResult { Stars = -1, Points = 120 })[0]);
            var critical = new RunResult { Stars = 0, Debrief = new[] { "Critical: Reset from inside the zone" } };
            Assert.AreEqual("result_critical", TrainerLines.Result(critical)[0]);
        }

        [Test]
        public void ExtrasAreCappedMostImportantFirst()
        {
            var result = new RunResult
            {
                Stars = 3, Points = 600, NewBest = true, NewBadges = new[] { "Clean run" },
                Gain = new XpGain(1200, true, 3), XpBefore = 300, XpAfter = 1500,
            };
            List<string> lines = TrainerLines.Result(result);
            CollectionAssert.AreEqual(new[] { "result_3", "rank_up", "daily_done" }, lines);
            Assert.AreEqual(6, TrainerLines.Result(result, 5).Count, "with room: badge, best and streak too");
        }

        [Test]
        public void NoNewBestAfterACriticalMistake()
        {
            var result = new RunResult { Stars = 0, Points = 50, NewBest = true, Debrief = new[] { "Critical: x" } };
            CollectionAssert.AreEqual(new[] { "result_critical" }, TrainerLines.Result(result));
        }
    }
}
