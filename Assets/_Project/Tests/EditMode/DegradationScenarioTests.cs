using System;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class DegradationScenarioTests
    {
        [Test]
        public void Wear_ClimbsLinearlyAndStopsAtTheFailureWear()
        {
            var s = new DegradationScenario(160, 215, 75);
            Assert.AreEqual(160d, s.WearMin);
            Assert.IsFalse(s.Complete);

            s.Tick(37.5);
            Assert.AreEqual(187.5, s.WearMin, 1e-9);

            s.Tick(40);
            Assert.AreEqual(215d, s.WearMin);
            Assert.IsTrue(s.Complete);
        }

        [Test]
        public void NegativeOrZeroTicks_DoNothing()
        {
            var s = new DegradationScenario(160, 215, 75);
            s.Tick(-5);
            s.Tick(0);
            Assert.AreEqual(0d, s.ElapsedSeconds);
        }

        [Test]
        public void Constructor_RejectsABackwardsOrInstantScenario()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DegradationScenario(200, 150, 75));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DegradationScenario(160, 215, 0));
        }

        [Test]
        public void DefaultSchedule_WarnsAtLeast30SecondsBeforeTheToolFails()
        {
            // The defaults on PressDegradation and FailureRiskSettings: the M5 exit condition by construction.
            var s = new DegradationScenario(160, 215, 75);
            var settings = new FailureRiskSettings();

            // Tool-wear risk reaches the warning level at this wear.
            double warnWear = settings.ToolRiskStartMin
                              + settings.WarningRisk * (settings.ToolFailureStartMin - settings.ToolRiskStartMin);
            double lead = s.DurationSeconds - s.SecondsToReach(warnWear);

            Assert.AreEqual(190d, warnWear, 1e-9);
            Assert.GreaterOrEqual(lead, 30d, $"warning only {lead:0.0} s before failure");

            // And the model agrees: at that wear, on a calm reading, the warning is on; just before, it is off.
            var at = FailureRiskModel.Evaluate(new PressReading(298, 308.5, 1500, 40, warnWear, ProductQuality.Low), settings);
            var before = FailureRiskModel.Evaluate(new PressReading(298, 308.5, 1500, 40, warnWear - 1, ProductQuality.Low), settings);
            Assert.IsTrue(at.Warning);
            Assert.IsFalse(before.Warning);
        }
    }
}
