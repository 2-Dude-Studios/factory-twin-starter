using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class FpsMeterTests
    {
        [Test]
        public void SteadyFrames_ReportTheirRateAtTheEndOfEachWindow()
        {
            var meter = new FpsMeter(0.5);
            int finished = 0;
            for (int i = 0; i < 120; i++)
                if (meter.AddFrame(1d / 60d)) finished++;

            // 30 or 31 frames per half-second window, depending on float rounding.
            Assert.That(finished, Is.InRange(3, 4));
            Assert.AreEqual(60d, meter.Fps, 0.5);
            Assert.AreEqual(1000d / 60d, meter.WorstFrameMs, 0.01);
        }

        [Test]
        public void OneLongFrame_ShowsAsTheWorstFrameOfItsWindow()
        {
            var meter = new FpsMeter(0.5);
            meter.AddFrame(0.1);
            for (int i = 0; i < 30; i++)
                meter.AddFrame(1d / 60d);

            Assert.AreEqual(100d, meter.WorstFrameMs, 1e-9);
            Assert.Less(meter.Fps, 60d);
        }

        [Test]
        public void ZeroOrNegativeFrames_AreIgnored()
        {
            var meter = new FpsMeter(0.5);
            Assert.IsFalse(meter.AddFrame(0));
            Assert.IsFalse(meter.AddFrame(-1));
            Assert.AreEqual(0d, meter.Fps);
        }
    }

    public sealed class FreshTakeTests
    {
        [Test]
        public void EventLogClear_EmptiesTheLogAndForgetsBaselines()
        {
            var log = new EventLog(10);
            log.Observe(new SignalMessage(CellTopics.LineState, SignalValue.FromNumber((int)CellState.Held), 1L));
            log.Observe(new SignalMessage(CellTopics.LineState, SignalValue.FromNumber((int)CellState.Feeding), 2L));
            Assert.AreEqual(1, log.Entries.Count);

            log.Clear();
            // After a clear the next value is a new baseline, so recovering from the old take logs nothing.
            log.Observe(new SignalMessage(CellTopics.LineState, SignalValue.FromNumber((int)CellState.Held), 3L));

            Assert.AreEqual(0, log.Entries.Count);
        }

        [Test]
        public void ResetPartsLost_ZeroesTheCountButKeepsTheRunningRate()
        {
            var metrics = new LineMetrics(50);
            for (int tick = 1; tick <= 6000; tick++)
                metrics.Tick(0.02, CellState.Feeding, tick / 300);
            for (int tick = 6001; tick <= 7800; tick++)
                metrics.Tick(0.02, CellState.Held, 20);
            Assert.Greater(metrics.PartsLost, 6d);

            metrics.ResetPartsLost();
            Assert.AreEqual(0d, metrics.PartsLost);

            for (int tick = 7801; tick <= 9600; tick++)
                metrics.Tick(0.02, CellState.Held, 20);
            Assert.AreEqual(36.0 * 9 * 3600.0 / 50.0 / 3600.0, metrics.PartsLost, 0.01, "same pre-hold rate as before the reset");
        }

        [Test]
        public void ViewModel_ClearsItsLogOnAFreshTake()
        {
            var bus = new SignalBus();
            using (var model = new DashboardViewModel(bus))
            {
                bus.Publish(CellTopics.PressToolChange, SignalValue.FromBool(true), 1L);
                Assert.AreEqual(1, model.Log.Entries.Count);

                bus.Publish(CellTopics.DemoFreshTake, SignalValue.FromBool(true), 2L);

                Assert.AreEqual(0, model.Log.Entries.Count);
            }
        }
    }
}
