using System;
using System.Collections.Generic;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class SparkSeriesTests
    {
        [Test]
        public void Indexer_ReturnsOldestFirstBeforeAndAfterWrapping()
        {
            var series = new SparkSeries(3);
            series.Add(1);
            series.Add(2);
            Assert.AreEqual(2, series.Count);
            Assert.AreEqual(1f, series[0]);
            Assert.AreEqual(2f, series[1]);

            series.Add(3);
            series.Add(4);
            Assert.AreEqual(3, series.Count);
            CollectionAssert.AreEqual(new[] { 2f, 3f, 4f }, new[] { series[0], series[1], series[2] });
        }

        [Test]
        public void Version_ChangesOnEveryAddAndClear()
        {
            var series = new SparkSeries(2);
            int v0 = series.Version;
            series.Add(5);
            int v1 = series.Version;
            series.Clear();

            Assert.AreNotEqual(v0, v1);
            Assert.AreNotEqual(v1, series.Version);
            Assert.AreEqual(0, series.Count);
        }

        [Test]
        public void Indexer_RejectsOutOfRange()
        {
            var series = new SparkSeries(4);
            series.Add(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => { float _ = series[1]; });
        }
    }

    public sealed class EventLogTests
    {
        private static SignalMessage Num(TopicPath topic, double value, long ms = 1L) =>
            new SignalMessage(topic, SignalValue.FromNumber(value), ms);

        private static SignalMessage Flag(TopicPath topic, bool value, long ms = 1L) =>
            new SignalMessage(topic, SignalValue.FromBool(value), ms);

        [Test]
        public void FirstValues_AreBaselinesAndNotLogged()
        {
            var log = new EventLog(10);
            log.Observe(Num(CellTopics.LineState, (int)CellState.Held));
            log.Observe(Num(CellTopics.PressHealth, (int)MachineHealth.Fault));
            log.Observe(Flag(CellTopics.PressFaults[0], true));

            Assert.AreEqual(0, log.Entries.Count);
        }

        [Test]
        public void PressFailure_IsLoggedOnceWhenHealthTurnsToFaultAndNamesTheActiveModes()
        {
            var log = new EventLog(10);
            log.Observe(Num(CellTopics.PressHealth, (int)MachineHealth.Ok));
            log.Observe(Flag(CellTopics.PressFaults[0], false));
            log.Observe(Flag(CellTopics.PressFaults[3], false));

            log.Observe(Flag(CellTopics.PressFaults[0], true, 2L));
            log.Observe(Flag(CellTopics.PressFaults[3], true, 2L));
            log.Observe(Num(CellTopics.PressHealth, (int)MachineHealth.Fault, 2L));

            // Later rows of the same fault flip the secondary flag off and on again.
            log.Observe(Flag(CellTopics.PressFaults[3], false, 3L));
            log.Observe(Flag(CellTopics.PressFaults[3], true, 4L));

            Assert.AreEqual(1, log.Entries.Count);
            Assert.AreEqual("CNC failure: tool wear, overstrain", log.Entries[0].Text);
            Assert.AreEqual(LogSeverity.Fault, log.Entries[0].Severity);
            Assert.AreEqual(2L, log.Entries[0].UnixMs);
        }

        [Test]
        public void PressFailure_WithoutAModeFlagIsStillLogged()
        {
            var log = new EventLog(10);
            log.Observe(Num(CellTopics.PressHealth, (int)MachineHealth.Ok));
            log.Observe(Num(CellTopics.PressHealth, (int)MachineHealth.Fault, 2L));

            Assert.AreEqual("CNC failure", log.Entries[0].Text);
        }

        [Test]
        public void LineHoldAndRestart_AreLoggedNewestFirst()
        {
            var log = new EventLog(10);
            log.Observe(Num(CellTopics.LineState, (int)CellState.Feeding));
            log.Observe(Num(CellTopics.LineState, (int)CellState.Transferring));
            log.Observe(Num(CellTopics.LineState, (int)CellState.Held));
            log.Observe(Num(CellTopics.LineState, (int)CellState.Feeding));

            Assert.AreEqual(2, log.Entries.Count);
            Assert.AreEqual("Line running again", log.Entries[0].Text);
            Assert.AreEqual("Line stopped: CNC fault", log.Entries[1].Text);
        }

        [Test]
        public void FirstInjection_IsLoggedAndNamesEveryMode()
        {
            var log = new EventLog(10);
            log.Observe(Num(CellTopics.PressInjectFault, (int)(FaultMode.ToolWear | FaultMode.Power)));
            log.Observe(Num(CellTopics.PressInjectFault, 0));

            Assert.AreEqual("Injected fault cleared", log.Entries[0].Text);
            Assert.AreEqual("Fault injected: tool wear, power", log.Entries[1].Text);
        }

        [Test]
        public void BrokerLink_FirstStateIsLogged()
        {
            var log = new EventLog(10);
            log.Observe(Flag(CellTopics.LinkConnected, true));
            log.Observe(Flag(CellTopics.LinkConnected, false));

            Assert.AreEqual("Broker disconnected", log.Entries[0].Text);
            Assert.AreEqual("Broker connected", log.Entries[1].Text);
        }

        [Test]
        public void SteadyTelemetry_IsNeverLogged()
        {
            var log = new EventLog(10);
            for (int i = 0; i < 5; i++)
                log.Observe(Num(CellTopics.PressTorque, 40 + i));

            Assert.AreEqual(0, log.Entries.Count);
        }

        [Test]
        public void Capacity_DropsTheOldestLines()
        {
            var log = new EventLog(2);
            log.Observe(Flag(CellTopics.LinkConnected, true, 1L));
            log.Observe(Flag(CellTopics.LinkConnected, false, 2L));
            log.Observe(Flag(CellTopics.LinkConnected, true, 3L));

            Assert.AreEqual(2, log.Entries.Count);
            Assert.AreEqual(3L, log.Entries[0].UnixMs);
            Assert.AreEqual(2L, log.Entries[1].UnixMs);
        }
    }

    public sealed class DashboardViewModelTests
    {
        [Test]
        public void Telemetry_UpdatesTextAndSparkline()
        {
            var bus = new SignalBus();
            using (var model = new DashboardViewModel(bus))
            {
                bus.Publish(CellTopics.PressAirTemp, SignalValue.FromNumber(298.15), 1L);
                bus.Publish(CellTopics.PressSpeed, SignalValue.FromNumber(1551), 1L);
                bus.Publish(CellTopics.PressTorque, SignalValue.FromNumber(42.84), 1L);
                bus.Publish(CellTopics.PressToolWear, SignalValue.FromNumber(108), 1L);

                Assert.AreEqual("25.0 °C", model.AirTempText);
                Assert.AreEqual("1,551 rpm", model.SpeedText);
                Assert.AreEqual("42.8 N·m", model.TorqueText);
                Assert.AreEqual("108 min", model.ToolWearText);
                Assert.AreEqual(1, model.AirTemp.Count);
                Assert.AreEqual(298.15f, model.AirTemp[0], 1e-3f);
            }
        }

        [Test]
        public void LineAndPress_ShowWordsAndRaiseStatusChanged()
        {
            var bus = new SignalBus();
            using (var model = new DashboardViewModel(bus))
            {
                int statusChanges = 0;
                model.StatusChanged += () => statusChanges++;

                bus.Publish(CellTopics.LineState, SignalValue.FromNumber((int)CellState.Held), 1L);
                bus.Publish(CellTopics.PressHealth, SignalValue.FromNumber((int)MachineHealth.Fault), 1L);

                Assert.AreEqual("Stopped — CNC fault", model.LineStateText);
                Assert.AreEqual("Fault", model.PressHealthText);
                Assert.AreEqual(MachineHealth.Fault, model.PressHealth);
                Assert.AreEqual(2, statusChanges);
            }
        }

        [Test]
        public void FailureRisk_ShowsADashUntilM5PublishesIt()
        {
            var bus = new SignalBus();
            using (var model = new DashboardViewModel(bus))
            {
                Assert.AreEqual(DashboardViewModel.NoValue, model.FailureRiskText);

                bus.Publish(CellTopics.PressFailureRisk, SignalValue.FromNumber(0.734), 1L);
                Assert.AreEqual("73 %", model.FailureRiskText);
                Assert.AreEqual(73.4f, model.FailureRiskPercent, 1e-3f);

                bus.Publish(CellTopics.PressFailureRisk, SignalValue.FromNumber(1.7), 2L);
                Assert.AreEqual(100f, model.FailureRiskPercent);
            }
        }

        [Test]
        public void Construction_SeedsFromValuesAlreadyOnTheBus()
        {
            var bus = new SignalBus();
            bus.Publish(CellTopics.LinePartsOut, SignalValue.FromNumber(143), 1L);
            bus.Publish(CellTopics.LinkConnected, SignalValue.FromBool(true), 1L);

            using (var model = new DashboardViewModel(bus))
            {
                Assert.AreEqual("143", model.PartsOutText);
                Assert.IsTrue(model.LinkUp);
                Assert.AreEqual("Broker connected", model.LinkText);
            }
        }

        [Test]
        public void PropertyChanged_FiresOnlyWhenTheTextChanges()
        {
            var bus = new SignalBus();
            using (var model = new DashboardViewModel(bus))
            {
                var changed = new List<string>();
                model.propertyChanged += (_, e) => changed.Add(e.propertyName.ToString());
                long before = model.GetViewHashCode();

                bus.Publish(CellTopics.LinePartsLost, SignalValue.FromNumber(3), 1L);
                bus.Publish(CellTopics.LinePartsLost, SignalValue.FromNumber(3), 2L);

                CollectionAssert.AreEqual(new[] { nameof(DashboardViewModel.PartsLostText) }, changed);
                Assert.AreNotEqual(before, model.GetViewHashCode());
            }
        }

        [Test]
        public void Dispose_StopsListening()
        {
            var bus = new SignalBus();
            var model = new DashboardViewModel(bus);
            model.Dispose();

            bus.Publish(CellTopics.LinePartsOut, SignalValue.FromNumber(7), 1L);

            Assert.AreEqual(DashboardViewModel.NoValue, model.PartsOutText);
        }
    }
}
