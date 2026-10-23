using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class SafetyEventLogTests
    {
        private static SignalMessage Num(TopicPath topic, double value, long ms = 1L) =>
            new SignalMessage(topic, SignalValue.FromNumber(value), ms);

        private static SignalMessage Reset() =>
            new SignalMessage(CellTopics.SafetyReset, SignalValue.FromBool(true), 1L);

        private static EventLog LogFromNormal()
        {
            var log = new EventLog(20);
            log.Observe(Num(CellTopics.SafetyState, (int)SafetyState.Normal));
            return log;
        }

        [Test]
        public void ProtectiveStop_ClearZone_Reset_ReadsAsThreeLines()
        {
            EventLog log = LogFromNormal();
            log.Observe(Num(CellTopics.SafetyState, (int)SafetyState.Stopped));
            log.Observe(Num(CellTopics.SafetyState, (int)SafetyState.AwaitingReset));
            log.Observe(Reset());
            log.Observe(Num(CellTopics.SafetyState, (int)SafetyState.Normal));

            Assert.AreEqual(3, log.Entries.Count);
            Assert.AreEqual("Safety reset: arm released", log.Entries[0].Text);
            Assert.AreEqual("Robot zone clear: press reset to restart", log.Entries[1].Text);
            Assert.AreEqual("Protective stop: person in robot zone", log.Entries[2].Text);
            Assert.AreEqual(LogSeverity.Fault, log.Entries[2].Severity);
        }

        [Test]
        public void Reset_WhileThePersonIsStillInside_IsLoggedAsRefused()
        {
            EventLog log = LogFromNormal();
            log.Observe(Num(CellTopics.SafetyState, (int)SafetyState.Stopped));
            log.Observe(Reset());
            log.Observe(Reset());

            Assert.AreEqual(3, log.Entries.Count, "every press is logged");
            Assert.AreEqual("Reset refused: person still in the robot zone", log.Entries[0].Text);
            Assert.AreEqual(LogSeverity.Warning, log.Entries[0].Severity);
        }

        [Test]
        public void Reset_WithNoStop_IsNotLogged()
        {
            EventLog log = LogFromNormal();
            log.Observe(Reset());

            Assert.AreEqual(0, log.Entries.Count);
        }

        [Test]
        public void WarningZone_InAndOut()
        {
            EventLog log = LogFromNormal();
            log.Observe(Num(CellTopics.SafetyState, (int)SafetyState.Reduced));
            log.Observe(Num(CellTopics.SafetyState, (int)SafetyState.Normal));

            Assert.AreEqual(2, log.Entries.Count);
            Assert.AreEqual("Warning zone clear: arm at full speed", log.Entries[0].Text);
            Assert.AreEqual("Person in warning zone: arm slowed", log.Entries[1].Text);
        }

        [Test]
        public void LineBackFromAProtectiveStop_ReadsAsRunningAgain()
        {
            var log = new EventLog(10);
            log.Observe(Num(CellTopics.LineState, (int)CellState.Feeding));
            log.Observe(Num(CellTopics.LineState, (int)CellState.SafetyStop));
            log.Observe(Num(CellTopics.LineState, (int)CellState.Feeding));

            Assert.AreEqual(1, log.Entries.Count, "the stop itself is logged from the safety state");
            Assert.AreEqual("Line running again", log.Entries[0].Text);
        }

        [Test]
        public void Dashboard_NamesTheProtectiveStop()
        {
            Assert.AreEqual("Stopped — person in robot zone", DashboardViewModel.LineStateWords(CellState.SafetyStop));
        }
    }
}
