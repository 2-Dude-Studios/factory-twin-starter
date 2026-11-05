using System.Linq;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class FireDrillScoreTests
    {
        // A textbook run on a Class B fire: alarm, CO2, pin, hose, good aim and distance, out fast, no reflash.
        private static FireDrillScore CleanRun(FireDrillScore score = null, FireClass cls = FireClass.B, FireAgent agent = FireAgent.Co2)
        {
            score ??= new FireDrillScore();
            score.StartRun(cls, false, 0);
            score.StageChanged(FireStage.Smoke, 1);
            score.StageChanged(FireStage.Incipient, 5);
            score.AlarmRaised(6);
            score.PinPulled(10);
            score.HoseTaken(11);
            for (int i = 0; i < 50; i++)
                score.Discharging(agent, 0.9, 0.9, 0.1, 12 + i * 0.1);
            score.StageChanged(FireStage.Out, 18);
            score.Extinguished(20);
            return score;
        }

        [Test]
        public void CleanRun_ScoresEveryStep_AndEarnsItsBadges()
        {
            FireDrillScore score = CleanRun();

            // alarm 100, right class 100, pin 25, hose 25, knockdown 150, fast 50, aim 50, distance 50, out 100
            Assert.AreEqual(650, score.Total);
            Assert.AreEqual(1, score.Runs);
            CollectionAssert.IsSubsetOf(new[] { FireDrillScore.BadgeAlarmFirst, FireDrillScore.BadgeRightClass,
                FireDrillScore.BadgeSharpAim, FireDrillScore.BadgeCleanKnockdown, FireDrillScore.BadgePerfectDrill }, score.Badges.ToList());
            Assert.IsFalse(score.Events.Any(e => e.Mistake));
        }

        [Test]
        public void WaterOnElectrical_IsAMistake_AndNoCleanBadge()
        {
            var score = new FireDrillScore();
            score.StartRun(FireClass.C, false, 0);
            score.StageChanged(FireStage.Incipient, 2);
            score.AlarmRaised(3);
            score.Discharging(FireAgent.Water, 1, 1, 0.1, 5);
            score.FinishRun(30);

            ScoreEvent wrong = score.Events.Single(e => e.Mistake);
            Assert.AreEqual(-150, wrong.Points);
            StringAssert.Contains("Water on a Class C fire", wrong.Text);
            Assert.AreEqual("1910.157(g)(1)", wrong.Regulation);
            CollectionAssert.DoesNotContain(score.Badges.ToList(), FireDrillScore.BadgeCleanKnockdown);
        }

        [Test]
        public void FightingBeforeTheAlarm_CostsPoints_AndTheAlarmEarnsNone()
        {
            var score = new FireDrillScore();
            score.StartRun(FireClass.A, false, 0);
            score.StageChanged(FireStage.Incipient, 1);
            score.Discharging(FireAgent.Water, 1, 1, 0.1, 2);
            score.AlarmRaised(4);

            Assert.IsTrue(score.Events.Any(e => e.Mistake && e.Text.Contains("before raising the alarm")));
            Assert.IsFalse(score.Events.Any(e => e.Text == "Alarm raised first"));
        }

        [Test]
        public void LeavingAFireThatIsTooBig_EarnsKnewWhenToLeave()
        {
            var score = new FireDrillScore();
            score.StartRun(FireClass.B, false, 0);
            score.StageChanged(FireStage.Incipient, 1);
            score.AlarmRaised(2);
            score.StageChanged(FireStage.BeyondIncipient, 40);
            score.FinishRun(50);

            Assert.IsTrue(score.Events.Any(e => e.Badge == FireDrillScore.BadgeKnewWhenToLeave && e.Points == 150));
        }

        [Test]
        public void FightingTheBattery_IsAMistake_AndEarnsNoLeaveBadge()
        {
            var score = new FireDrillScore();
            score.StartRun(FireClass.C, true, 0);
            score.AlarmRaised(1);
            score.Discharging(FireAgent.Co2, 1, 1, 0.1, 3);
            score.FinishRun(10);

            Assert.IsTrue(score.Events.Any(e => e.Mistake && e.Text.Contains("Lithium-ion")));
            CollectionAssert.DoesNotContain(score.Badges.ToList(), FireDrillScore.BadgeKnewWhenToLeave);
        }

        [Test]
        public void Reflash_CostsPoints()
        {
            var score = new FireDrillScore();
            score.StartRun(FireClass.A, false, 0);
            score.StageChanged(FireStage.Incipient, 1);
            score.StageChanged(FireStage.Out, 10);
            score.StageChanged(FireStage.Incipient, 16);

            Assert.IsTrue(score.Events.Any(e => e.Points == -50 && e.Text.StartsWith("It reflashed")));
        }

        [Test]
        public void ThreeClassesPutOut_EarnFireMarshal_AndBadgesCountOnce()
        {
            var score = new FireDrillScore();
            CleanRun(score, FireClass.A, FireAgent.Water);
            CleanRun(score, FireClass.B, FireAgent.Co2);
            CleanRun(score, FireClass.C, FireAgent.DryChemicalAbc);

            Assert.IsTrue(score.Badges.Contains(FireDrillScore.BadgeFireMarshal));
            Assert.AreEqual(1, score.Events.Count(e => e.Badge == FireDrillScore.BadgeAlarmFirst));
            Assert.AreEqual(3, score.Runs);
        }

        [Test]
        public void TakeNew_ReturnsEachEventOnce()
        {
            var score = new FireDrillScore();
            score.StartRun(FireClass.A, false, 0);
            score.AlarmRaised(1);
            Assert.AreEqual(1, score.TakeNew().Count);
            Assert.AreEqual(0, score.TakeNew().Count);
        }

        [Test]
        public void Record_HasTheLockoutColumns_AStepPerScore_ABadgeRow_AndTheResult()
        {
            string csv = FireDrillRecord.ToCsv("Sam Rivera", "Demo trainer", new System.DateTime(2026, 11, 4, 12, 0, 0), CleanRun());
            string[] lines = csv.TrimEnd('\n').Split('\n');

            Assert.AreEqual(FireDrillRecord.Header, lines[0]);
            Assert.IsTrue(lines.Any(l => l.Contains(",1910.38(c)(1),done,Alarm raised first (+100)")));
            Assert.IsTrue(lines.Any(l => l.Contains(",badge,Clean knockdown")));
            StringAssert.Contains(",result,\"650 points over 1 fire, ", lines.Last());
        }

        [Test]
        public void BadgeBook_KeepsTheBest_AndReportsOnlyNewBadges()
        {
            BadgeBook book = BadgeBook.InMemory("Sam Rivera");
            var first = book.Record(TrainingMode.FireSafety, 400, new[] { "Right class", "Alarm first" });
            var second = book.Record(TrainingMode.FireSafety, 300, new[] { "Right class", "Sharp aim" });

            Assert.AreEqual(400, book.BestScore(TrainingMode.FireSafety));
            CollectionAssert.AreEqual(new[] { "Right class", "Alarm first" }, first);
            CollectionAssert.AreEqual(new[] { "Sharp aim" }, second);
            Assert.AreEqual(3, book.Earned(TrainingMode.FireSafety).Count);
            Assert.AreEqual(0, book.BestScore(TrainingMode.Lockout));
        }

        [TestCase(new[] { "FactoryTwin.exe", "-vr" }, false)]
        [TestCase(new[] { "FactoryTwin.exe", "-cell" }, true)]
        [TestCase(new[] { "FactoryTwin.exe", "-takes", "D,E" }, true)]
        public void Menu_IsSkippedForCaptureAndCell(string[] args, bool skip)
        {
            Assert.AreEqual(skip, TrainingLaunch.SkipMenu(args));
        }

        [Test]
        public void Launch_RequestIsTakenOnce()
        {
            TrainingLaunch.Request(TrainingMode.FireSafety);
            Assert.AreEqual(TrainingMode.FireSafety, TrainingLaunch.Take());
            Assert.AreEqual(TrainingMode.None, TrainingLaunch.Take());
            Assert.IsTrue(TrainingLaunch.NeedsHeadset(TrainingMode.FireSafety));
            Assert.IsFalse(TrainingLaunch.NeedsHeadset(TrainingMode.Lockout));
        }
    }
}
