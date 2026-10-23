using System;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class SafetyZoneTests
    {
        [Test]
        public void StartsNormalAtFullSpeed()
        {
            var zone = new SafetyZone();

            Assert.AreEqual(SafetyState.Normal, zone.State);
            Assert.IsFalse(zone.ProtectiveStop);
            Assert.AreEqual(1d, zone.SpeedScale);
        }

        [Test]
        public void WarningZone_SlowsTheArmWithoutStoppingIt()
        {
            var zone = new SafetyZone(0.25d);
            zone.Update(true, false, false);

            Assert.AreEqual(SafetyState.Reduced, zone.State);
            Assert.IsFalse(zone.ProtectiveStop);
            Assert.AreEqual(0.25d, zone.SpeedScale);
        }

        [Test]
        public void WarningZone_FullSpeedAgainOnceItClears()
        {
            var zone = new SafetyZone();
            zone.Update(true, false, false);
            zone.Update(false, false, false);

            Assert.AreEqual(SafetyState.Normal, zone.State);
            Assert.AreEqual(1d, zone.SpeedScale);
        }

        [Test]
        public void ProtectiveZone_StopsOnTheSameUpdate()
        {
            var zone = new SafetyZone();
            zone.Update(true, true, false);

            Assert.AreEqual(SafetyState.Stopped, zone.State);
            Assert.IsTrue(zone.ProtectiveStop);
            Assert.AreEqual(0d, zone.SpeedScale);
        }

        [Test]
        public void LeavingTheZone_DoesNotRestart()
        {
            var zone = new SafetyZone();
            zone.Update(true, true, false);
            zone.Update(true, false, false);
            zone.Update(false, false, false);

            Assert.AreEqual(SafetyState.AwaitingReset, zone.State);
            Assert.IsTrue(zone.ProtectiveStop);
            Assert.AreEqual(0d, zone.SpeedScale);
        }

        [Test]
        public void Reset_WithTheZoneClear_ReleasesTheStop()
        {
            var zone = new SafetyZone();
            zone.Update(false, true, false);
            zone.Update(false, false, false);

            Assert.AreEqual(SafetyResetResult.Accepted, zone.Update(false, false, true));
            Assert.AreEqual(SafetyState.Normal, zone.State);
            Assert.IsFalse(zone.ProtectiveStop);
        }

        [Test]
        public void Reset_WithAPersonStillInTheWarningZone_ReleasesToReducedSpeed()
        {
            var zone = new SafetyZone(0.25d);
            zone.Update(true, true, false);
            zone.Update(true, false, false);
            zone.Update(true, false, true);

            Assert.AreEqual(SafetyState.Reduced, zone.State);
            Assert.AreEqual(0.25d, zone.SpeedScale);
        }

        [Test]
        public void Reset_WhileThePersonIsInTheProtectiveZone_IsRefused()
        {
            var zone = new SafetyZone();
            zone.Update(false, true, false);

            Assert.AreEqual(SafetyResetResult.RefusedZoneOccupied, zone.Update(false, true, true));
            Assert.AreEqual(SafetyState.Stopped, zone.State);
            Assert.IsTrue(zone.ProtectiveStop);
        }

        [Test]
        public void Reset_CountsOnItsRisingEdgeOnly()
        {
            var zone = new SafetyZone();
            zone.Update(false, true, true);   // pressed while inside: refused
            zone.Update(false, false, true);  // still held after leaving: not a new press

            Assert.AreEqual(SafetyState.AwaitingReset, zone.State);

            zone.Update(false, false, false);
            Assert.AreEqual(SafetyResetResult.Accepted, zone.Update(false, false, true));
            Assert.AreEqual(SafetyState.Normal, zone.State);
        }

        [Test]
        public void Reset_WithNoStop_DoesNothing()
        {
            var zone = new SafetyZone();

            Assert.AreEqual(SafetyResetResult.None, zone.Update(false, false, true));
            Assert.AreEqual(SafetyState.Normal, zone.State);
        }

        [Test]
        public void ReenteringAfterAReset_StopsAgain()
        {
            var zone = new SafetyZone();
            zone.Update(false, true, false);
            zone.Update(false, false, false);
            zone.Update(false, false, true);
            zone.Update(false, true, true);

            Assert.AreEqual(SafetyState.Stopped, zone.State);
            Assert.IsTrue(zone.ProtectiveStop);
        }

        [Test]
        public void ReducedSpeedScale_OutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SafetyZone(0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SafetyZone(1.5d));
        }
    }
}
