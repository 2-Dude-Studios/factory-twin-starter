using System.IO;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class FailureRiskModelTests
    {
        private static readonly FailureRiskSettings Defaults = new FailureRiskSettings();

        // A comfortable reading: 10.5 K spread, 1500 rpm, 40 Nm (about 6.3 kW), fresh tool.
        private static PressReading Reading(double wear = 0, double torque = 40, double rpm = 1500,
            double air = 298, double process = 308.5, ProductQuality quality = ProductQuality.Low) =>
            new PressReading(air, process, rpm, torque, wear, quality);

        private static RiskAssessment Evaluate(PressReading reading) => FailureRiskModel.Evaluate(reading, Defaults);

        [Test]
        public void ComfortableReading_HasNoRisk()
        {
            RiskAssessment a = Evaluate(Reading());

            Assert.AreEqual(0d, a.Risk);
            Assert.AreEqual(FaultMode.None, a.LikelyMode);
            Assert.IsFalse(a.Warning);
            Assert.IsFalse(a.MaintenanceDue);
            Assert.AreEqual(200d, a.ToolRemainingMin);
        }

        [TestCase(170, 0.0)]
        [TestCase(190, 0.5)]
        [TestCase(200, 1.0)]
        [TestCase(230, 1.0)]
        public void ToolWear_ClimbsFrom180To200Min(double wear, double expected)
        {
            // Low torque so overstrain stays out of it.
            RiskAssessment a = Evaluate(Reading(wear: wear, torque: 30, rpm: 2000));

            Assert.AreEqual(expected, a.ToolWearRisk, 1e-9);
            Assert.AreEqual(System.Math.Max(0, 200 - wear), a.ToolRemainingMin, 1e-9);
        }

        [Test]
        public void ToolChange_IsRecommendedWithin20MinOfTheWindowBeforeTheWarning()
        {
            RiskAssessment a = Evaluate(Reading(wear: 182, torque: 30, rpm: 2000));

            Assert.IsFalse(a.Warning);
            Assert.IsTrue(a.MaintenanceDue);
        }

        [Test]
        public void Overstrain_UsesTheProductQualityLimit()
        {
            // 150 min × 77 Nm = 11,550 minNm: past the low-quality limit (11,000), but still below where the
            // high-quality margin starts (0.9 × 13,000 = 11,700).
            var low = Evaluate(Reading(wear: 150, torque: 77, rpm: 1100, quality: ProductQuality.Low));
            var high = Evaluate(Reading(wear: 150, torque: 77, rpm: 1100, quality: ProductQuality.High));

            Assert.AreEqual(1d, low.OverstrainRisk);
            Assert.AreEqual(FaultMode.Overstrain, low.LikelyMode);
            Assert.AreEqual(0d, high.OverstrainRisk);
        }

        [Test]
        public void Power_RisesNearBothLimits()
        {
            // 3650 W: half way between 3800 and 3500.
            double lowTorque = 3650d / (1500d * 2d * System.Math.PI / 60d);
            // 8850 W: half way between 8700 and 9000.
            double highTorque = 8850d / (2000d * 2d * System.Math.PI / 60d);

            Assert.AreEqual(0.5, Evaluate(Reading(torque: lowTorque, rpm: 1500)).PowerRisk, 1e-6);
            Assert.AreEqual(0.5, Evaluate(Reading(torque: highTorque, rpm: 2000)).PowerRisk, 1e-6);
            Assert.AreEqual(FaultMode.Power, Evaluate(Reading(torque: highTorque, rpm: 2000)).LikelyMode);
        }

        [Test]
        public void Heat_NeedsBothASmallSpreadAndLowSpeed()
        {
            var both = Evaluate(Reading(air: 300, process: 308.6, rpm: 1380));
            var fastEnough = Evaluate(Reading(air: 300, process: 308.6, rpm: 1500));
            var coolEnough = Evaluate(Reading(air: 300, process: 310, rpm: 1380));

            Assert.AreEqual(1d, both.HeatRisk);
            Assert.AreEqual(FaultMode.HeatDissipation, both.LikelyMode);
            Assert.AreEqual(0d, fastEnough.HeatRisk);
            Assert.AreEqual(0d, coolEnough.HeatRisk);
        }

        [Test]
        public void Ai4iDataset_EveryLabelledFailureIsWarnedAndProcessAlarmsStayRare()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Data/ai4i2020.csv");
            var rows = Ai4iCsv.Parse(File.ReadAllText(path));

            int labelled = 0, labelledWarned = 0, failures = 0, failuresWarned = 0;
            int healthy = 0, processAlarms = 0, toolAdvice = 0;
            foreach (Ai4iRow row in rows)
            {
                RiskAssessment a = Evaluate(PressReading.From(row));
                FaultMode predictable = row.Faults & ~FaultMode.Random;
                if (predictable != FaultMode.None)
                {
                    labelled++;
                    if (a.Warning) labelledWarned++;
                }
                if (row.MachineFailure)
                {
                    failures++;
                    if (a.Warning) failuresWarned++;
                }
                else if (row.Faults == FaultMode.None)
                {
                    healthy++;
                    bool process = a.OverstrainRisk >= Defaults.WarningRisk || a.PowerRisk >= Defaults.WarningRisk
                                   || a.HeatRisk >= Defaults.WarningRisk;
                    if (process) processAlarms++;
                    if (a.ToolWearRisk >= Defaults.WarningRisk) toolAdvice++;
                }
            }

            TestContext.WriteLine($"Rows with a TWF/HDF/PWF/OSF label warned: {labelledWarned}/{labelled}");
            TestContext.WriteLine($"Machine-failure rows warned: {failuresWarned}/{failures}");
            TestContext.WriteLine($"Healthy rows: {healthy}; process alarms {processAlarms} ({100.0 * processAlarms / healthy:0.0}%), " +
                                  $"tool past {Defaults.ToolRiskStartMin + 0.5 * (Defaults.ToolFailureStartMin - Defaults.ToolRiskStartMin):0} min {toolAdvice} ({100.0 * toolAdvice / healthy:0.0}%)");

            Assert.AreEqual(labelled, labelledWarned, "every row with a predictable failure mode should be warned");
            Assert.GreaterOrEqual(failuresWarned, failures - 8, "only the 8 failures with no mode label may be missed");
            Assert.Less(processAlarms, healthy * 0.03, "process-margin alarms on healthy rows should stay under 3 %");
        }
    }
}
