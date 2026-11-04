using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class FireAlarmTests
    {
        private static SignalMessage Num(TopicPath topic, double value) =>
            new SignalMessage(topic, SignalValue.FromNumber(value), 1L);

        private static SignalMessage Flag(TopicPath topic, bool value) =>
            new SignalMessage(topic, SignalValue.FromBool(value), 1L);

        [Test]
        public void Detector_TripsOnlyAfterTheResponseTime_AndStaysTripped()
        {
            var head = new DetectorLatch(threshold: 0.3, responseSeconds: 3);
            bool trippedOnce = false;
            for (int i = 0; i < 29; i++)
                trippedOnce |= head.Tick(0.5, 0.1);
            Assert.IsFalse(head.Tripped);

            for (int i = 0; i < 3; i++)
                trippedOnce |= head.Tick(0.5, 0.1);
            Assert.IsTrue(head.Tripped);
            Assert.IsTrue(trippedOnce);
            Assert.IsFalse(head.Tick(0.5, 0.1), "trips once");

            head.Tick(0d, 0.1);
            Assert.IsTrue(head.Tripped, "latched");
        }

        [Test]
        public void Detector_ADipBelowThreshold_RestartsTheResponseTime()
        {
            var head = new DetectorLatch(0.3, 1);
            for (int i = 0; i < 8; i++)
                head.Tick(0.5, 0.1);
            head.Tick(0.1, 0.1);
            for (int i = 0; i < 8; i++)
                head.Tick(0.5, 0.1);

            Assert.IsFalse(head.Tripped);
        }

        [Test]
        public void Detector_Reset_RefusedWhileStillSensing()
        {
            var head = new DetectorLatch(0.3, 0);
            head.Tick(0.5, 0.1);

            Assert.IsFalse(head.Reset());
            head.Tick(0.1, 0.1);
            Assert.IsTrue(head.Reset());
            Assert.IsFalse(head.Tripped);
        }

        [Test]
        public void Alarm_KeepsTheFirstSource_AndRefusesResetWhileCalledFor()
        {
            var alarm = new FireAlarmLatch();

            Assert.IsTrue(alarm.Raise(AlarmSource.Detector));
            Assert.IsFalse(alarm.Raise(AlarmSource.Manual));
            Assert.AreEqual(AlarmSource.Detector, alarm.Source);

            Assert.IsFalse(alarm.Reset(anyInputActive: true));
            Assert.IsTrue(alarm.Active);
            Assert.IsTrue(alarm.Reset(anyInputActive: false));
            Assert.IsFalse(alarm.Active);
            Assert.AreEqual(AlarmSource.None, alarm.Source);
        }

        [Test]
        public void LineHeldWhileTheFireAlarmIsOn_ReadsAsTheFireAlarm()
        {
            Assert.AreEqual("Stopped — fire alarm", DashboardViewModel.LineStateWords(CellState.Held, false, true));
            Assert.AreEqual("Stopped — CNC fault", DashboardViewModel.LineStateWords(CellState.Held, false, false));
            Assert.AreEqual("Stopped — PLC link lost", DashboardViewModel.LineStateWords(CellState.Held, true, true));
            Assert.AreEqual("Running", DashboardViewModel.LineStateWords(CellState.Feeding, false, true));
        }

        [Test]
        public void AlarmAndDetectorWords()
        {
            Assert.AreEqual("Alarm off", DashboardViewModel.FireAlarmWords(false, AlarmSource.Detector));
            Assert.AreEqual("FIRE ALARM · detector", DashboardViewModel.FireAlarmWords(true, AlarmSource.Detector));
            Assert.AreEqual("FIRE ALARM · manual call point", DashboardViewModel.FireAlarmWords(true, AlarmSource.Manual));
            Assert.AreEqual("Smoke and heat detected", DashboardViewModel.DetectorWords(true, true));
            Assert.AreEqual("Detectors clear", DashboardViewModel.DetectorWords(false, false));
        }

        [Test]
        public void EventLog_TellsTheFireStory()
        {
            var log = new EventLog(30);
            log.Observe(Num(CellTopics.FireStage, (int)FireStage.None));
            log.Observe(Num(CellTopics.FireClass, (int)FireClass.None));
            log.Observe(Num(CellTopics.FireLocation, (int)FireLocation.None));
            log.Observe(Flag(CellTopics.FireAlarm, false));
            log.Observe(Flag(CellTopics.FireSmokeDetected, false));
            log.Observe(Flag(CellTopics.CabinetOverheat, false));
            log.Observe(Num(CellTopics.LineState, (int)CellState.Feeding));
            log.Observe(Num(CellTopics.CabinetTemp, 32));
            Assume.That(log.Entries.Count, Is.EqualTo(0));

            log.Observe(Num(CellTopics.FireScenario, 1));
            log.Observe(Num(CellTopics.FireClass, (int)FireClass.C));
            log.Observe(Num(CellTopics.FireLocation, (int)FireLocation.DriveCabinet));
            log.Observe(Num(CellTopics.CabinetTemp, 61));
            log.Observe(Flag(CellTopics.CabinetOverheat, true));
            log.Observe(Num(CellTopics.FireStage, (int)FireStage.Smoke));
            log.Observe(Num(CellTopics.FireStage, (int)FireStage.Incipient));
            log.Observe(Flag(CellTopics.FireSmokeDetected, true));
            log.Observe(Num(CellTopics.FireAlarmSource, (int)AlarmSource.Detector));
            log.Observe(Flag(CellTopics.FireAlarm, true));
            log.Observe(Num(CellTopics.LineState, (int)CellState.Held));
            log.Observe(Num(CellTopics.FireStage, (int)FireStage.Out));
            log.Observe(Num(CellTopics.FireStage, (int)FireStage.Incipient));

            string[] expected =
            {
                "Fire reflashed at the drive cabinet",
                "Fire out: watch for reflash",
                "Line stopped: fire alarm",
                "Fire alarm raised by the detector",
                "Smoke detector tripped",
                "Fire at the drive cabinet: incipient stage, Class C, live electrical",
                "Smoke at the drive cabinet",
                "Drive cabinet overheating: 61 °C",
                "Fire drill F1 started",
            };
            Assert.AreEqual(expected.Length, log.Entries.Count);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], log.Entries[i].Text, $"line {i}");
            Assert.AreEqual(LogSeverity.Fault, log.Entries[3].Severity);
            Assert.AreEqual(LogSeverity.Warning, log.Entries[7].Severity);
        }

        [Test]
        public void ViewModel_ShowsTheFirePanelOnlyDuringADrill()
        {
            var bus = new SignalBus();
            using (var model = new DashboardViewModel(bus))
            {
                bus.Publish(Num(CellTopics.FireStage, (int)FireStage.None));
                Assert.IsFalse(model.FireDrillActive);

                bus.Publish(Num(CellTopics.FireClass, (int)FireClass.B));
                bus.Publish(Num(CellTopics.FireLocation, (int)FireLocation.Cnc));
                bus.Publish(Num(CellTopics.FireStage, (int)FireStage.Smoke));
                Assert.IsTrue(model.FireDrillActive);
                Assert.AreEqual("CNC · Class B, flammable liquids", model.FireWhereText);
                Assert.AreEqual("Smoke", model.FireStageText);

                bus.Publish(Num(CellTopics.FireAlarmSource, (int)AlarmSource.Manual));
                bus.Publish(Flag(CellTopics.FireAlarm, true));
                bus.Publish(Num(CellTopics.LineState, (int)CellState.Held));
                Assert.AreEqual("FIRE ALARM · manual call point", model.FireAlarmText);
                Assert.AreEqual("Stopped — fire alarm", model.LineStateText);
            }
        }
    }
}
