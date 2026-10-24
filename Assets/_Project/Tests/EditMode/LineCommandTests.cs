using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class LineCommandTests
    {
        private static SignalMessage Num(TopicPath topic, double value) =>
            new SignalMessage(topic, SignalValue.FromNumber(value), 1L);

        [Test]
        public void CommandCount_APressIsUpByExactlyOne()
        {
            Assert.IsTrue(CommandCount.IsPress(0, 1));
            Assert.IsTrue(CommandCount.IsPress(41, 42));
            Assert.IsFalse(CommandCount.IsPress(3, 3), "no change");
            Assert.IsFalse(CommandCount.IsPress(3, 5), "two presses missed: resync, do not act twice");
            Assert.IsFalse(CommandCount.IsPress(7, 0), "the twin restarted: resync");
        }

        [Test]
        public void CommandCount_WrapsLikeA16BitRegister()
        {
            Assert.IsTrue(CommandCount.IsPress(65535, 0));
        }

        [Test]
        public void EventLog_NamesTheLockAsTheReasonAStartWasRefused()
        {
            var log = new EventLog(10);
            log.Observe(Num(CellTopics.LineStartsRefused, 0));
            log.Observe(Num(CellTopics.PressIsolated, 1));
            log.Observe(Num(CellTopics.PressLockout, 1));
            log.Observe(Num(CellTopics.LineStartsRefused, 1));

            Assert.AreEqual(1, log.Entries.Count);
            Assert.AreEqual("Start refused: CNC locked out", log.Entries[0].Text);
            Assert.AreEqual(LogSeverity.Warning, log.Entries[0].Severity);
        }

        [Test]
        public void EventLog_IsolatedButNotLocked_SaysIsolated()
        {
            var log = new EventLog(10);
            log.Observe(Num(CellTopics.LineStartsRefused, 2));
            log.Observe(Num(CellTopics.PressIsolated, 1));
            log.Observe(Num(CellTopics.LineStartsRefused, 3));

            Assert.AreEqual("Start refused: CNC isolated", log.Entries[0].Text);
        }

        [Test]
        public void EventLog_StopAndStart()
        {
            var log = new EventLog(10);
            log.Observe(Num(CellTopics.LineState, (int)CellState.Feeding));
            log.Observe(Num(CellTopics.LineState, (int)CellState.Stopped));
            log.Observe(Num(CellTopics.LineState, (int)CellState.Feeding));

            Assert.AreEqual(2, log.Entries.Count);
            Assert.AreEqual("Line started", log.Entries[0].Text);
            Assert.AreEqual("Line stopped by the operator", log.Entries[1].Text);
            Assert.AreEqual("Stopped", DashboardViewModel.LineStateWords(CellState.Stopped));
        }
    }
}
