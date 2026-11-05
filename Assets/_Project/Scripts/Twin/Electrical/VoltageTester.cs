using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Electrical safety: what the tester's display shows for one measurement.</summary>
    public readonly struct TesterDisplay
    {
        public TesterDisplay(double volts, bool dc, bool blank, bool overRange)
        {
            Volts = volts;
            Dc = dc;
            Blank = blank;
            OverRange = overRange;
        }

        /// <summary>Volts shown, rounded to the display's resolution (0 while blank).</summary>
        public double Volts { get; }

        public bool Dc { get; }

        /// <summary>Nothing on the display: the battery is flat. Easy to mistake for "no voltage".</summary>
        public bool Blank { get; }

        /// <summary>Above the tester's rating: it shows OL instead of a number.</summary>
        public bool OverRange { get; }

        public override string ToString() => Blank ? "" : OverRange ? "OL" : $"{Volts:0} V {(Dc ? "DC" : "AC")}";
    }

    /// <summary>
    /// Electrical safety (M19): a two-probe voltage tester (CAT III 600 V) as a pure model. It shows what
    /// <see cref="ElectricalModel.Read"/> gives it, to whole volts, until its battery goes flat; then its display is
    /// blank, which looks like a dead circuit. That is E4: only proving the tester on a known source before and after
    /// the test (70E 120.6(7)) catches it. Its readings go to the tester's own display and never onto the bus.
    /// </summary>
    public sealed class VoltageTester
    {
        /// <summary>A proving unit's known output, the source the tester is checked on before and after a test.</summary>
        public const double ProvingUnitVolts = 240d;

        /// <summary>How close a proof must read to the known source to pass.</summary>
        public const double ProofTolerance = 0.05d;

        private readonly int _failsAfterMeasurements;

        /// <param name="ratedVolts">The tester's rating; above it the display shows OL.</param>
        /// <param name="failsAfterMeasurements">The battery goes flat after this many measurements (proofs count); negative never.</param>
        public VoltageTester(double ratedVolts = 600d, int failsAfterMeasurements = -1)
        {
            if (!(ratedVolts > 0d))
                throw new ArgumentOutOfRangeException(nameof(ratedVolts), "Must be above 0.");
            RatedVolts = ratedVolts;
            _failsAfterMeasurements = failsAfterMeasurements;
        }

        public double RatedVolts { get; }

        public int Measurements { get; private set; }

        public bool BatteryFlat => _failsAfterMeasurements >= 0 && Measurements >= _failsAfterMeasurements;

        /// <summary>Measures a reading and shows it, unless the battery has gone flat.</summary>
        public TesterDisplay Measure(ProbeReading reading)
        {
            bool flat = BatteryFlat;
            Measurements++;
            if (flat)
                return new TesterDisplay(0d, false, blank: true, overRange: false);
            double volts = Math.Round(reading.Volts);
            if (volts > RatedVolts * 1.1d)
                return new TesterDisplay(0d, reading.IsDc, blank: false, overRange: true);
            return new TesterDisplay(volts, reading.IsDc, blank: false, overRange: false);
        }

        /// <summary>
        /// Proves the tester on the proving unit: true when it shows the known voltage. A flat tester fails, and so
        /// does the test it was meant to bracket.
        /// </summary>
        public bool Prove(double knownVolts = ProvingUnitVolts)
        {
            TesterDisplay shown = Measure(new ProbeReading(knownVolts, 0d));
            return !shown.Blank && !shown.OverRange && Math.Abs(shown.Volts - knownVolts) <= knownVolts * ProofTolerance;
        }
    }
}
