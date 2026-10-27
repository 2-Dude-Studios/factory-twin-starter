using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class PressIsolationTests
    {
        private static SignalMessage Num(TopicPath topic, double value) =>
            new SignalMessage(topic, SignalValue.FromNumber(value), 1L);

        [Test]
        public void Lock_FitsOnlyWhileTheIsolatorIsOff()
        {
            var hasp = new IsolatorLock();

            Assert.AreEqual(IsolationResult.RefusedNotIsolated, hasp.SetLock(true));
            Assert.IsFalse(hasp.Locked);

            Assert.AreEqual(IsolationResult.Done, hasp.SetIsolator(true));
            Assert.AreEqual(IsolationResult.Done, hasp.SetLock(true));
            Assert.IsTrue(hasp.Isolated);
            Assert.IsTrue(hasp.Locked);
        }

        [Test]
        public void Isolator_CannotBeSwitchedOnWhileLocked()
        {
            var hasp = new IsolatorLock();
            hasp.SetIsolator(true);
            hasp.SetLock(true);

            Assert.AreEqual(IsolationResult.RefusedLocked, hasp.SetIsolator(false));
            Assert.IsTrue(hasp.Isolated);

            Assert.AreEqual(IsolationResult.Done, hasp.SetLock(false));
            Assert.AreEqual(IsolationResult.Done, hasp.SetIsolator(false));
            Assert.IsFalse(hasp.Isolated);
        }

        [Test]
        public void SameStateAgain_IsNoChange()
        {
            var hasp = new IsolatorLock();

            Assert.AreEqual(IsolationResult.NoChange, hasp.SetIsolator(false));
            Assert.AreEqual(IsolationResult.NoChange, hasp.SetLock(false));
        }

        [Test]
        public void StackLight_IsDarkWithoutPower()
        {
            StackLightOutput dark = StackLightOutput.Dark;
            Assert.AreEqual(0f, dark.Green);
            Assert.AreEqual(0f, dark.Amber);
            Assert.AreEqual(0f, dark.Red);
            Assert.AreEqual(0f, dark.Glow);
        }

        [Test]
        public void EventLog_TellsTheLockoutRunStepByStep()
        {
            var log = new EventLog(30);
            log.Observe(Num(CellTopics.TrainingStep, 0));
            log.Observe(Num(CellTopics.TrainingErrors, 0));
            log.Observe(Num(CellTopics.TrainingResult, (int)TrainingOutcome.Idle));
            log.Observe(Num(CellTopics.PressIsolated, 0));

            log.Observe(Num(CellTopics.TrainingStep, 1));
            log.Observe(Num(CellTopics.TrainingResult, (int)TrainingOutcome.InProgress));
            log.Observe(Num(CellTopics.TrainingAction, (int)LockoutAction.ReviewProcedure));
            log.Observe(Num(CellTopics.TrainingStep, 2));
            // The controller reacts inside the action's delivery: the mistake and the count arrive before the action.
            log.Observe(Num(CellTopics.TrainingMistake, (int)LockoutAction.ChangeTool));
            log.Observe(Num(CellTopics.TrainingErrors, 1));
            log.Observe(Num(CellTopics.TrainingAction, (int)LockoutAction.ChangeTool));
            log.Observe(Num(CellTopics.PressIsolated, 1));

            Assert.AreEqual("CNC isolated at its main isolator", log.Entries[0].Text);
            Assert.AreEqual("Mistake: Change tool before: Notify affected employees", log.Entries[1].Text);
            Assert.AreEqual(LogSeverity.Warning, log.Entries[1].Severity);
            Assert.AreEqual("Step 1 done: Prepare: read the CNC's energy control procedure", log.Entries[2].Text);
            Assert.AreEqual("Lockout training started", log.Entries[3].Text);
        }

        [Test]
        public void EventLog_NamesTheResult()
        {
            var log = new EventLog(10);
            log.Observe(Num(CellTopics.TrainingErrors, 0));
            log.Observe(Num(CellTopics.TrainingResult, (int)TrainingOutcome.InProgress));
            log.Observe(Num(CellTopics.TrainingErrors, 2));
            log.Observe(Num(CellTopics.TrainingResult, (int)TrainingOutcome.PassedWithErrors));

            Assert.AreEqual("Lockout training finished with 2 mistakes", log.Entries[0].Text);
        }
    }
}
