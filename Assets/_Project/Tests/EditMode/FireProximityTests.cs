using System.Linq;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class FireProximityTests
    {
        [Test]
        public void DangerZone_GrowsWithTheFire()
        {
            Assert.AreEqual(0.9, FireProximity.TooCloseMetres(0), 1e-9);
            Assert.AreEqual(1.5, FireProximity.TooCloseMetres(1), 1e-9);
            Assert.AreEqual(1.5, FireProximity.TooCloseMetres(3), 1e-9, "size is clamped");
            Assert.AreEqual(2.3, FireProximity.WarnMetres(1), 1e-9);
        }

        [Test]
        public void Heat_RisesFromTheWarningDistance_AndIsFullInsideTheZone()
        {
            Assert.AreEqual(0, FireProximity.Heat(3, 0), 1e-9);
            Assert.AreEqual(0, FireProximity.Heat(1.7, 0), 1e-9);
            Assert.AreEqual(0.5, FireProximity.Heat(1.3, 0), 1e-9);
            Assert.AreEqual(1, FireProximity.Heat(0.9, 0), 1e-9);
            Assert.AreEqual(1, FireProximity.Heat(0.2, 0), 1e-9);
            Assert.AreEqual(0, FireProximity.Heat(double.NaN, 0), 1e-9);
            Assert.IsTrue(FireProximity.TooClose(0.8, 0));
            Assert.IsFalse(FireProximity.TooClose(1.0, 0));
        }

        [Test]
        public void OnlyFlamesGiveOffHeat()
        {
            Assert.IsFalse(FireProximity.Burning(FireStage.None));
            Assert.IsFalse(FireProximity.Burning(FireStage.Smoke));
            Assert.IsTrue(FireProximity.Burning(FireStage.Incipient));
            Assert.IsTrue(FireProximity.Burning(FireStage.Growing));
            Assert.IsTrue(FireProximity.Burning(FireStage.BeyondIncipient));
            Assert.IsFalse(FireProximity.Burning(FireStage.Out));
        }

        [Test]
        public void TooClose_CostsPointsEachTime_UpToThreeARun_AndSpoilsTheCleanKnockdown()
        {
            var score = new FireDrillScore();
            score.StartRun(FireClass.B, false, 0);
            score.StageChanged(FireStage.Incipient, 1);
            score.AlarmRaised(2);
            for (int i = 0; i < 5; i++)
                score.TooClose(3 + i);

            Assert.AreEqual(FireDrillScore.TooCloseMaxPerRun, score.TooCloseCount);
            Assert.AreEqual(100 + FireDrillScore.TooCloseMaxPerRun * FireDrillScore.TooClosePoints, score.Total);
            Assert.IsTrue(score.Events.Where(e => e.Points < 0).All(e => e.Mistake));

            score.Discharging(FireAgent.Co2, 0.9, 0.9, 0.1, 9);
            score.StageChanged(FireStage.Out, 10);
            score.Extinguished(11);
            CollectionAssert.DoesNotContain(score.Badges.ToList(), FireDrillScore.BadgeCleanKnockdown);
        }

        [Test]
        public void TooClose_IsCountedAfreshForTheNextFire()
        {
            var score = new FireDrillScore();
            score.StartRun(FireClass.A, false, 0);
            score.TooClose(1);
            score.StartRun(FireClass.B, false, 2);
            Assert.AreEqual(0, score.TooCloseCount);
            score.TooClose(3);
            Assert.AreEqual(1, score.TooCloseCount);
        }

        [Test]
        public void Advice_SaysStepBack_WhenTooClose_WithTheAgentsDistance()
        {
            string advice = FireDrillText.Advice(FireStage.Incipient, FireClass.B, false, false, true, FireAgent.Co2, 1f,
                true, 0.9f, 0.9f, 0.6f, true, true, tooClose: true);
            StringAssert.StartsWith("Too close", advice);
            StringAssert.Contains("1–2.5 m", advice);

            string noFlame = FireDrillText.Advice(FireStage.Smoke, FireClass.B, false, false, true, FireAgent.Co2, 1f,
                false, 0f, 0f, 0.5f, true, true, tooClose: true);
            StringAssert.DoesNotStartWith("Too close", noFlame);
        }
    }
}
