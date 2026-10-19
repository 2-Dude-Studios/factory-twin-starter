using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class StackLightPatternTests
    {
        [Test]
        public void Unknown_AllLampsIdleAndNoGlow()
        {
            StackLightOutput o = StackLightPattern.Evaluate(false, MachineHealth.Fault, 0.1);

            Assert.AreEqual(StackLightPattern.Idle, o.Green);
            Assert.AreEqual(StackLightPattern.Idle, o.Amber);
            Assert.AreEqual(StackLightPattern.Idle, o.Red);
            Assert.AreEqual(0f, o.Glow);
        }

        [Test]
        public void Healthy_SteadyGreenNoGlow()
        {
            for (double t = 0; t < 2; t += 0.13)
            {
                StackLightOutput o = StackLightPattern.Evaluate(true, MachineHealth.Ok, t);
                Assert.AreEqual(1f, o.Green);
                Assert.AreEqual(StackLightPattern.Idle, o.Red);
                Assert.AreEqual(0f, o.Glow);
            }
        }

        [Test]
        public void Warning_SteadyAmberWithSoftGlow()
        {
            StackLightOutput o = StackLightPattern.Evaluate(true, MachineHealth.Warning, 0.7);

            Assert.AreEqual(1f, o.Amber);
            Assert.AreEqual(StackLightPattern.Idle, o.Green);
            Assert.AreEqual(0.5f, o.Glow);
        }

        [Test]
        public void Fault_RedFlashesAndTheGlowStaysOn()
        {
            double period = 1.0 / StackLightPattern.FlashHz;
            StackLightOutput on = StackLightPattern.Evaluate(true, MachineHealth.Fault, period * 0.1);
            StackLightOutput off = StackLightPattern.Evaluate(true, MachineHealth.Fault, period * 0.75);

            Assert.AreEqual(1f, on.Red);
            Assert.AreEqual(StackLightPattern.Idle, off.Red);
            Assert.AreEqual(StackLightPattern.Idle, on.Green);
            Assert.Greater(on.Glow, 0.19f);
            Assert.Greater(off.Glow, 0.19f);
            Assert.LessOrEqual(on.Glow, 1f);
        }
    }
}
