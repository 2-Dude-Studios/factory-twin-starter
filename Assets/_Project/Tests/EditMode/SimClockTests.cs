using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class SimClockTests
    {
        [Test]
        public void BeforeAWindowCloses_RatioIsOne()
        {
            var clock = new SimClock(1d);
            Assert.IsFalse(clock.Sample(0d, 100d));
            Assert.IsFalse(clock.Sample(0.5d, 100.5d));
            Assert.AreEqual(1d, clock.Ratio);
        }

        [Test]
        public void RealTimeRun_HasRatioOne()
        {
            var clock = new SimClock(1d);
            clock.Sample(10d, 100d);
            Assert.IsTrue(clock.Sample(11d, 101d));
            Assert.AreEqual(1d, clock.Ratio, 1e-9);
            Assert.IsTrue(clock.IsRealTime(0.1d));
        }

        [Test]
        public void FastForward_ShowsAsRatioAboveOne()
        {
            var clock = new SimClock(1d);
            clock.Sample(0d, 0d);
            clock.Sample(8d, 1d);
            Assert.AreEqual(8d, clock.Ratio, 1e-9);
            Assert.IsFalse(clock.IsRealTime(0.1d));
        }

        [Test]
        public void EachWindowStartsWhereTheLastEnded()
        {
            var clock = new SimClock(1d);
            clock.Sample(0d, 0d);
            clock.Sample(2d, 1d);
            clock.Sample(2.5d, 2d);
            Assert.AreEqual(0.5d, clock.Ratio, 1e-9);
        }
    }
}
