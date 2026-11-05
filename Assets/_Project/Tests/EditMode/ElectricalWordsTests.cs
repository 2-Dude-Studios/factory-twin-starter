using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class ElectricalWordsTests
    {
        [Test]
        public void Dashboard_Words_ForTheCabinet()
        {
            Assert.AreEqual("480 V", ElectricalWords.Volts(480));
            Assert.AreEqual("0 V", ElectricalWords.Volts(0.2));
            Assert.AreEqual("612 V DC", ElectricalWords.Volts(612.4, dc: true));
            Assert.AreEqual("Q1 ON", ElectricalWords.Disconnect(false));
            Assert.AreEqual("Q1 OFF (open)", ElectricalWords.Disconnect(true));
            Assert.AreEqual("No lock on Q1", ElectricalWords.Locks(0));
            Assert.AreEqual("1 lock on Q1", ElectricalWords.Locks(1));
            Assert.AreEqual("2 locks on Q1", ElectricalWords.Locks(2));
            Assert.AreEqual("Door open", ElectricalWords.Door(true));
            StringAssert.Contains("U1", ElectricalWords.DriveFault(true));
            Assert.AreEqual("E2 · Backfeed from the UPS control feed", ElectricalWords.Scenario(2));
            Assert.AreEqual("", ElectricalWords.Scenario(0));
        }

        [Test]
        public void UpsFeed_IsNotNamed_UntilTheTraineeActsOnIt()
        {
            Assert.AreEqual("", ElectricalWords.UpsFeed(open: false, locks: 0), "the dashboard must not give E2 away");
            Assert.AreEqual("UPS control feed OFF", ElectricalWords.UpsFeed(true, 0));
            Assert.AreEqual("UPS control feed OFF · 1 lock", ElectricalWords.UpsFeed(true, 1));
        }

        [Test]
        public void Panel_ShowsOnlyOutOfNormalService()
        {
            Assert.IsFalse(ElectricalWords.PanelActive(0, false, false, false, 0, false, 0));
            Assert.IsTrue(ElectricalWords.PanelActive(1, false, false, false, 0, false, 0));
            Assert.IsTrue(ElectricalWords.PanelActive(0, true, false, false, 0, false, 0));
            Assert.IsTrue(ElectricalWords.PanelActive(0, false, true, false, 0, false, 0));
            Assert.IsTrue(ElectricalWords.PanelActive(0, false, false, false, 0, false, 1));
        }

        [Test]
        public void Boundary_And_Refusal_Words()
        {
            Assert.AreEqual("Outside the boundaries", ElectricalWords.Boundary(ApproachBoundary.Outside));
            StringAssert.Contains("restricted", ElectricalWords.Boundary(ApproachBoundary.Restricted));
            StringAssert.Contains("lock", ElectricalWords.Refusal(SwitchResult.RefusedLocked));
            StringAssert.Contains("interlock", ElectricalWords.Refusal(SwitchResult.RefusedInterlock));
        }
    }
}
