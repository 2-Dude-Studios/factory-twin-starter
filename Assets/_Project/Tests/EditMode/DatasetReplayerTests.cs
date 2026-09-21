using System;
using System.Collections.Generic;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class DatasetReplayerTests
    {
        // Synthetic rows in the AI4I 2020 column layout; the real dataset is not needed for tests.
        private const string Csv =
            "﻿UDI,Product ID,Type,Air temperature [K],Process temperature [K],Rotational speed [rpm],Torque [Nm],Tool wear [min],Machine failure,TWF,HDF,PWF,OSF,RNF\r\n" +
            "1,M14860,M,298.1,308.6,1551,42.8,0,0,0,0,0,0,0\r\n" +
            "2,L47181,L,298.2,308.7,1408,46.3,3,0,0,0,0,0,0\r\n" +
            "3,L47182,L,298.3,308.5,1498,49.4,215,1,1,0,0,0,0\r\n" +
            "4,H29424,H,302.0,310.1,1305,60.2,120,1,0,1,0,1,0\r\n" +
            "\r\n";

        private const int TopicsPerRow = 11;

        [Test]
        public void Parse_ReadsColumnsByHeaderName()
        {
            List<Ai4iRow> rows = Ai4iCsv.Parse(Csv);

            Assert.AreEqual(4, rows.Count);
            Assert.AreEqual(298.1, rows[0].AirTempK);
            Assert.AreEqual(308.6, rows[0].ProcessTempK);
            Assert.AreEqual(1551d, rows[0].SpeedRpm);
            Assert.AreEqual(42.8, rows[0].TorqueNm);
            Assert.AreEqual(0d, rows[0].ToolWearMin);
            Assert.IsFalse(rows[0].MachineFailure);
            Assert.AreEqual(FaultMode.None, rows[0].Faults);

            Assert.IsTrue(rows[2].MachineFailure);
            Assert.AreEqual(FaultMode.ToolWear, rows[2].Faults);
            Assert.AreEqual(FaultMode.HeatDissipation | FaultMode.Overstrain, rows[3].Faults);
        }

        [Test]
        public void Parse_ReportsMissingColumnsAndBadCells()
        {
            Assert.Throws<FormatException>(() => Ai4iCsv.Parse(""));
            Assert.Throws<FormatException>(() => Ai4iCsv.Parse("UDI,Torque [Nm]\n1,42.8\n"));

            string badNumber = Csv.Replace("1551", "fast");
            StringAssert.Contains("line 2", Assert.Throws<FormatException>(() => Ai4iCsv.Parse(badNumber)).Message);

            string shortRow = Csv.Replace("2,L47181,L,298.2,308.7,1408,46.3,3,0,0,0,0,0,0", "2,L47181,L");
            StringAssert.Contains("line 3", Assert.Throws<FormatException>(() => Ai4iCsv.Parse(shortRow)).Message);
        }

        [Test]
        public void Tick_PublishesRowsAtTheConfiguredRate()
        {
            var bus = new SignalBus();
            var replayer = new DatasetReplayer(Ai4iCsv.Parse(Csv), bus) { RowsPerSecond = 2d };

            Assert.AreEqual(0, replayer.Tick(0.25, 1000L));
            Assert.AreEqual(1, replayer.Tick(0.25, 1250L));
            Assert.AreEqual(1, replayer.Tick(0.5, 1750L));
            Assert.AreEqual(2, replayer.RowsPublished);
        }

        [Test]
        public void Tick_PublishesTheSameTopicsAsThePythonReplayer()
        {
            var bus = new SignalBus();
            var topics = new List<string>();
            bus.Subscribe("cell/press/#", m => topics.Add(m.Topic.ToString()));
            var replayer = new DatasetReplayer(Ai4iCsv.Parse(Csv), bus) { RowsPerSecond = 1d };

            replayer.Tick(1d, 5000L);

            CollectionAssert.AreEqual(new[]
            {
                "cell/press/air_temp_k",
                "cell/press/process_temp_k",
                "cell/press/speed_rpm",
                "cell/press/torque_nm",
                "cell/press/tool_wear_min",
                "cell/press/fault_twf",
                "cell/press/fault_hdf",
                "cell/press/fault_pwf",
                "cell/press/fault_osf",
                "cell/press/fault_rnf",
                "cell/press/machine_failure",
            }, topics);

            Assert.IsTrue(bus.TryGetLast(new TopicPath("press", "air_temp_k"), out SignalMessage airTemp));
            Assert.AreEqual(298.1, airTemp.Value.Number);
            Assert.AreEqual(5000L, airTemp.UnixMs);
        }

        [Test]
        public void Tick_LoopsByDefaultAndFinishesWhenLoopIsOff()
        {
            List<Ai4iRow> rows = Ai4iCsv.Parse(Csv);

            var looping = new DatasetReplayer(rows, new SignalBus()) { RowsPerSecond = 10d };
            for (int i = 0; i < 6; i++)
                looping.Tick(0.1, i);
            Assert.AreEqual(6, looping.RowsPublished);
            Assert.IsFalse(looping.Finished);

            var once = new DatasetReplayer(rows, new SignalBus()) { RowsPerSecond = 10d, Loop = false };
            for (int i = 0; i < 6; i++)
                once.Tick(0.1, i);
            Assert.AreEqual(4, once.RowsPublished);
            Assert.IsTrue(once.Finished);
        }

        [Test]
        public void FaultFilter_ReplaysOnlyMatchingRows()
        {
            var bus = new SignalBus();
            var toolWear = new List<double>();
            bus.Subscribe("cell/press/tool_wear_min", m => toolWear.Add(m.Value.Number));
            var replayer = new DatasetReplayer(Ai4iCsv.Parse(Csv), bus)
            {
                RowsPerSecond = 10d,
                FaultFilter = FaultMode.ToolWear,
            };

            for (int i = 0; i < 3; i++)
                replayer.Tick(0.1, i);

            CollectionAssert.AreEqual(new[] { 215d, 215d, 215d }, toolWear);
        }

        [Test]
        public void FaultFilter_ThatMatchesNothingFinishesInsteadOfSpinning()
        {
            var replayer = new DatasetReplayer(Ai4iCsv.Parse(Csv), new SignalBus())
            {
                RowsPerSecond = 10d,
                FaultFilter = FaultMode.Random,
            };

            Assert.AreEqual(0, replayer.Tick(1d, 0L));
            Assert.IsTrue(replayer.Finished);
        }

        [Test]
        public void Tick_CapsBacklogAfterALongHitch()
        {
            var bus = new SignalBus();
            int messages = 0;
            bus.MessagePublished += _ => messages++;
            var replayer = new DatasetReplayer(Ai4iCsv.Parse(Csv), bus) { RowsPerSecond = 5d };

            int published = replayer.Tick(30d, 0L);

            Assert.AreEqual(5, published);
            Assert.AreEqual(5 * TopicsPerRow, messages);
        }

        [Test]
        public void RowsPerSecond_MustBePositive()
        {
            var replayer = new DatasetReplayer(Ai4iCsv.Parse(Csv), new SignalBus());

            Assert.Throws<ArgumentOutOfRangeException>(() => replayer.RowsPerSecond = 0d);
            Assert.Throws<ArgumentOutOfRangeException>(() => replayer.RowsPerSecond = double.NaN);
        }
    }
}
