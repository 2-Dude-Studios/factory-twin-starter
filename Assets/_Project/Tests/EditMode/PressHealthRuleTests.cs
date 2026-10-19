using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class PressHealthRuleTests
    {
        [Test]
        public void IsFaulted_FalseBeforeThePressReportsAnything()
        {
            Assert.IsFalse(PressHealthRule.IsFaulted(new SignalBus()));
        }

        [Test]
        public void IsFaulted_TrueOnTheOverallFailureFlag()
        {
            var bus = new SignalBus();
            bus.Publish(CellTopics.PressMachineFailure, SignalValue.FromBool(true), 1L);

            Assert.IsTrue(PressHealthRule.IsFaulted(bus));
        }

        [Test]
        public void IsFaulted_TrueOnAnySingleFailureMode()
        {
            for (int i = 0; i < CellTopics.PressFaults.Count; i++)
            {
                var bus = new SignalBus();
                bus.Publish(CellTopics.PressFaults[i], SignalValue.FromBool(true), 1L);

                Assert.IsTrue(PressHealthRule.IsFaulted(bus), CellTopics.PressFaults[i].ToString());
            }
        }

        [Test]
        public void IsFaulted_FalseOnceTheFlagsClear()
        {
            var bus = new SignalBus();
            bus.Publish(CellTopics.PressFaults[0], SignalValue.FromBool(true), 1L);
            bus.Publish(CellTopics.PressMachineFailure, SignalValue.FromBool(true), 1L);
            bus.Publish(CellTopics.PressFaults[0], SignalValue.FromBool(false), 2L);
            bus.Publish(CellTopics.PressMachineFailure, SignalValue.FromBool(false), 2L);

            Assert.IsFalse(PressHealthRule.IsFaulted(bus));
        }

        [Test]
        public void UpdateWarning_ComesOnAtTheWarningLevelAndClearsOnlyBelowTheClearLevel()
        {
            bool w = false;
            w = PressHealthRule.UpdateWarning(w, 0.49, 0.5, 0.4);
            Assert.IsFalse(w, "below the warning level");
            w = PressHealthRule.UpdateWarning(w, 0.5, 0.5, 0.4);
            Assert.IsTrue(w, "at the warning level");
            w = PressHealthRule.UpdateWarning(w, 0.45, 0.5, 0.4);
            Assert.IsTrue(w, "noise just under the warning level keeps it on");
            w = PressHealthRule.UpdateWarning(w, 0.39, 0.5, 0.4);
            Assert.IsFalse(w, "below the clear level");
        }

        [TestCase(false, false, MachineHealth.Ok)]
        [TestCase(false, true, MachineHealth.Warning)]
        [TestCase(true, false, MachineHealth.Fault)]
        [TestCase(true, true, MachineHealth.Fault)]
        public void Evaluate_FaultOutranksWarning(bool faulted, bool warning, MachineHealth expected)
        {
            Assert.AreEqual(expected, PressHealthRule.Evaluate(faulted, warning));
        }
    }
}
