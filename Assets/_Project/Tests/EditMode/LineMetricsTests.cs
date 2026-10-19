using System;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class LineMetricsTests
    {
        // 50 Hz fixed step, one part out every 300 ticks (6 s): 600 parts/hour.
        private const double Step = 0.02;
        private const int TicksPerPart = 300;

        private static void Run(LineMetrics metrics, int fromTick, int toTick, CellState state, Func<int, int> partsOut)
        {
            for (int tick = fromTick; tick <= toTick; tick++)
                metrics.Tick(Step, state, partsOut(tick));
        }

        [Test]
        public void PartsPerHour_UsesTheRunSoFarUntilTheWindowIsFull()
        {
            var metrics = new LineMetrics(120);
            Run(metrics, 1, 3000, CellState.Feeding, t => t / TicksPerPart);

            Assert.AreEqual(60.0, metrics.ElapsedSeconds, 1e-6);
            Assert.AreEqual(600.0, metrics.PartsPerHour, 1.0);
        }

        [Test]
        public void PartsPerHour_ForgetsExitsOlderThanTheWindow()
        {
            var metrics = new LineMetrics(100);
            Run(metrics, 1, 12000, CellState.Feeding, t => t / TicksPerPart);

            // Exits in (140 s, 240 s]: 144, 150 … 240 s, which is 17 parts in 100 s.
            Assert.AreEqual(17 * 3600.0 / 100.0, metrics.PartsPerHour, 1.0);
        }

        [Test]
        public void PartsLost_AccruesAtThePreHoldRateWhileHeld()
        {
            var metrics = new LineMetrics(50);
            Run(metrics, 1, 6000, CellState.Feeding, t => t / TicksPerPart);
            double runningRate = metrics.PartsPerHour;
            Assert.AreEqual(9 * 3600.0 / 50.0, runningRate, 1.0); // exits at 72 … 120 s

            Run(metrics, 6001, 7800, CellState.Held, _ => 20); // held 36 s, nothing exits

            Assert.AreEqual(36.0 * runningRate / 3600.0, metrics.PartsLost, 0.01);
            Assert.Less(metrics.PartsPerHour, runningRate, "the rolling rate should dip while the line is held");
        }

        [Test]
        public void PartsLost_StopsGrowingWhenTheHoldEnds()
        {
            var metrics = new LineMetrics(50);
            Run(metrics, 1, 6000, CellState.Feeding, t => t / TicksPerPart);
            Run(metrics, 6001, 7800, CellState.Held, _ => 20);
            double lost = metrics.PartsLost;

            Run(metrics, 7801, 9000, CellState.Feeding, t => 20 + (t - 7800) / TicksPerPart);

            Assert.AreEqual(lost, metrics.PartsLost);
        }

        [Test]
        public void PartsLost_ASecondHoldSoonAfterUsesTheSamePreHoldRate()
        {
            var metrics = new LineMetrics(50);
            Run(metrics, 1, 6000, CellState.Feeding, t => t / TicksPerPart);
            double runningRate = metrics.PartsPerHour;

            Run(metrics, 6001, 7800, CellState.Held, _ => 20);            // first hold, 36 s
            Run(metrics, 7801, 8400, CellState.Feeding, t => 20 + (t - 7800) / TicksPerPart); // 12 s running
            double afterFirst = metrics.PartsLost;
            Run(metrics, 8401, 9300, CellState.Held, _ => 22);            // second hold, 18 s

            Assert.AreEqual(18.0 * runningRate / 3600.0, metrics.PartsLost - afterFirst, 0.01);
        }

        [Test]
        public void PartsLost_StaysZeroWithoutAHold()
        {
            var metrics = new LineMetrics(60);
            Run(metrics, 1, 3000, CellState.Feeding, t => t / TicksPerPart);
            Run(metrics, 3001, 3500, CellState.Stalled, _ => 10);

            Assert.AreEqual(0.0, metrics.PartsLost);
        }

        [Test]
        public void FirstReading_IsTheBaselineNotABurstOfExits()
        {
            var metrics = new LineMetrics(60);
            metrics.Tick(Step, CellState.Feeding, 25);
            metrics.Tick(Step, CellState.Feeding, 25);

            Assert.AreEqual(0.0, metrics.PartsPerHour);
        }

        [Test]
        public void Constructor_RejectsANonPositiveWindow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LineMetrics(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LineMetrics(double.NaN));
        }
    }
}
