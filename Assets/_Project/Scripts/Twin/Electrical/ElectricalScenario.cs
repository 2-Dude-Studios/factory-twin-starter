using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Electrical safety (M19): one training scenario's data (E1–E4 in docs/electrical-safety-training.md), with no
    /// Unity dependency, so the scenarios can be built and tested as plain data. E5 and E6 are people, not cabinet
    /// states, and arrive with M21 and M22.
    /// </summary>
    [Serializable]
    public sealed class ElectricalScenarioSettings
    {
        [SerializeField] private int number = 1;
        [SerializeField] private string title = "Replace a failed infeed VFD";
        [Tooltip("Seconds after the start until the infeed VFD trips and stops the line.")]
        [SerializeField] private double driveFaultAfterSeconds = 5;
        [Tooltip("E2: the hall's UPS circuit is live, so the control terminals stay at 120 V with Q1 open.")]
        [SerializeField] private bool upsBackfeed;
        [Tooltip("E4: the tester's battery goes flat after this many measurements, proofs included; -1 never.")]
        [SerializeField] private int testerFailsAfter = -1;
        [SerializeField] private ElectricalSettings settings = new ElectricalSettings();

        public ElectricalScenarioSettings()
        {
        }

        public ElectricalScenarioSettings(int number, string title, bool upsBackfeed, int testerFailsAfter,
            double dcDischargeSeconds, double driveFaultAfterSeconds = 5)
        {
            this.number = number;
            this.title = title;
            this.upsBackfeed = upsBackfeed;
            this.testerFailsAfter = testerFailsAfter;
            this.driveFaultAfterSeconds = driveFaultAfterSeconds;
            settings = new ElectricalSettings(dcDischargeSeconds);
        }

        public int Number => number;
        public string Title => title;
        public double DriveFaultAfterSeconds => driveFaultAfterSeconds;
        public bool UpsBackfeed => upsBackfeed;
        public int TesterFailsAfter => testerFailsAfter;
        public ElectricalSettings Settings => settings;

        public ElectricalModel CreateModel() => new ElectricalModel(settings, upsBackfeed);

        /// <summary>The tester the trainee picks up first: flat after <see cref="TesterFailsAfter"/> in E4.</summary>
        public VoltageTester CreateTester() => new VoltageTester(failsAfterMeasurements: testerFailsAfter);

        /// <summary>The spare tester on the PPE board, always good.</summary>
        public static VoltageTester CreateSpareTester() => new VoltageTester();
    }

    /// <summary>
    /// Electrical safety: the four cabinet scenarios as data. Every one starts with E1's fault; E2–E4 each seed one
    /// lesson into it, so M22's random assessment can combine them.
    /// </summary>
    public static class ElectricalScenarios
    {
        /// <summary>E4's tester passes the proof before the test and three tests, then goes flat.</summary>
        public const int E4FailsAfter = 4;

        public static ElectricalScenarioSettings E1() =>
            new ElectricalScenarioSettings(1, "Replace a failed infeed VFD", upsBackfeed: false, testerFailsAfter: -1, dcDischargeSeconds: 20);

        public static ElectricalScenarioSettings E2() =>
            new ElectricalScenarioSettings(2, "Backfeed from the UPS control feed", upsBackfeed: true, testerFailsAfter: -1, dcDischargeSeconds: 20);

        public static ElectricalScenarioSettings E3() =>
            new ElectricalScenarioSettings(3, "Stored energy in the DC bus", upsBackfeed: false, testerFailsAfter: -1, dcDischargeSeconds: 60);

        public static ElectricalScenarioSettings E4() =>
            new ElectricalScenarioSettings(4, "Dead tester", upsBackfeed: false, testerFailsAfter: E4FailsAfter, dcDischargeSeconds: 20);

        public static ElectricalScenarioSettings ByNumber(int number)
        {
            switch (number)
            {
                case 1: return E1();
                case 2: return E2();
                case 3: return E3();
                case 4: return E4();
                default: return null;
            }
        }
    }

    /// <summary>
    /// Electrical safety (M19): one cabinet scenario as an asset, started by number on cell/elec/scenario. Wraps
    /// <see cref="ElectricalScenarioSettings"/>; <c>Build Electrical Cabinet</c> fills E1–E4 from <see cref="ElectricalScenarios"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Factory Twin/Electrical Scenario", fileName = "ElectricalScenario")]
    public sealed class ElectricalScenario : ScriptableObject
    {
        [SerializeField] private ElectricalScenarioSettings scenario = new ElectricalScenarioSettings();

        public ElectricalScenarioSettings Scenario => scenario;
        public int Number => scenario.Number;
        public string Title => scenario.Title;

        /// <summary>Editor setup: fills a new asset with one scenario's values.</summary>
        public void Configure(ElectricalScenarioSettings settings) => scenario = settings ?? new ElectricalScenarioSettings();
    }
}
