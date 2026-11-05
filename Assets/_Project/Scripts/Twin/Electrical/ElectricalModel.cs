using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Electrical safety (M19): a named point in the drive cabinet that a tester probe can touch. Each one is a named
    /// transform on the cabinet model, so a probe tip on L1 and another on L2 reads the L1–L2 voltage.
    /// </summary>
    public enum ProbePoint
    {
        None = 0,

        /// <summary>Line side of the main disconnect Q1 (shrouded). Live from the hall supply even with Q1 open.</summary>
        LineL1 = 1,
        LineL2 = 2,
        LineL3 = 3,

        /// <summary>Load side of Q1: the main AC bus on terminal strip X1, which feeds both VFDs and the transformer.</summary>
        L1 = 4,
        L2 = 5,
        L3 = 6,

        /// <summary>The cabinet's ground bar (PE).</summary>
        Ground = 7,

        /// <summary>The 120 V control circuit on terminal strip X2: hot (X2:1) and grounded neutral (X2:2).</summary>
        ControlHot = 8,
        ControlNeutral = 9,

        /// <summary>The UPS control feed from the hall panel, landed on X3: hot (X3:1) and grounded neutral (X3:2).</summary>
        UpsHot = 10,
        UpsNeutral = 11,

        /// <summary>DC bus terminals of the infeed VFD (U1) and the outfeed VFD (U2).</summary>
        Vfd1DcPlus = 12,
        Vfd1DcMinus = 13,
        Vfd2DcPlus = 14,
        Vfd2DcMinus = 15,
    }

    /// <summary>Electrical safety: where the 120 V control circuit is getting its power from.</summary>
    public enum ControlSource
    {
        None = 0,

        /// <summary>The control transformer T1, from the main AC bus.</summary>
        Transformer = 1,

        /// <summary>The UPS feed through the transfer relay: the E2 backfeed once Q1 is open.</summary>
        Ups = 2,
    }

    /// <summary>Electrical safety: what a command to a disconnect, lock or the cabinet door did.</summary>
    public enum SwitchResult
    {
        Done = 0,

        /// <summary>Already in the state asked for, or no lock to remove.</summary>
        NoChange = 1,

        /// <summary>A disconnect cannot be closed while any lock hangs on it.</summary>
        RefusedLocked = 2,

        /// <summary>A lock fits only while the disconnect is open (the handle locks in OFF only).</summary>
        RefusedNotOpen = 3,

        /// <summary>The door interlock: the door opens only with Q1 open (or with the defeater), and Q1 does not close with the door open.</summary>
        RefusedInterlock = 4,
    }

    /// <summary>Electrical safety: the cabinet's two VFDs.</summary>
    public enum Vfd
    {
        Infeed = 0,
        Outfeed = 1,
    }

    /// <summary>
    /// Electrical safety: what a perfect meter would read between two probe points. Both parts are given, since a
    /// pair can carry AC, DC or both: AC as RMS volts, DC as signed volts (first point minus second).
    /// </summary>
    public readonly struct ProbeReading
    {
        public ProbeReading(double acVolts, double dcVolts)
        {
            AcVolts = acVolts;
            DcVolts = dcVolts;
        }

        public double AcVolts { get; }
        public double DcVolts { get; }

        /// <summary>The larger part, as a tester's display would show it.</summary>
        public double Volts => Math.Max(AcVolts, Math.Abs(DcVolts));

        /// <summary>True when the DC part is the larger one.</summary>
        public bool IsDc => Math.Abs(DcVolts) > AcVolts;

        public override string ToString() => IsDc ? $"{DcVolts:0.#} V DC" : $"{AcVolts:0.#} V AC";
    }

    /// <summary>
    /// Electrical safety: the numbers behind the drive cabinet. A US 480 V three-phase, three-wire supply, 120 V
    /// control with a grounded neutral, VFD DC buses of about 650 V. The DC discharge time is shortened for a
    /// training drill (a real drive's label typically says 5 minutes or more). Serialised inside an
    /// <see cref="ElectricalScenario"/>.
    /// </summary>
    [Serializable]
    public sealed class ElectricalSettings
    {
        [SerializeField] private double nominalVolts = 480;
        [SerializeField] private double controlVolts = 120;
        [SerializeField] private double dcBusVolts = 650;
        [Tooltip("Seconds after the VFD loses its supply until its DC bus is below safeDcVolts (shortened for the demo).")]
        [SerializeField] private double dcDischargeSeconds = 20;
        [SerializeField] private double safeDcVolts = 50;

        /// <summary>The defaults: 480 V, 120 V control, 650 V DC bus, 20 s to below 50 V.</summary>
        public ElectricalSettings()
        {
        }

        /// <summary>Defaults, with the discharge time a scenario changes. Negative means keep the default.</summary>
        public ElectricalSettings(double dcDischargeSeconds)
        {
            if (dcDischargeSeconds >= 0) this.dcDischargeSeconds = dcDischargeSeconds;
        }

        public double NominalVolts => nominalVolts;
        public double ControlVolts => controlVolts;
        public double DcBusVolts => dcBusVolts;
        public double DcDischargeSeconds => dcDischargeSeconds;
        public double SafeDcVolts => safeDcVolts;

        /// <summary>Phase-to-ground on a grounded-wye supply: 480 / √3 ≈ 277 V.</summary>
        public double PhaseToGroundVolts => nominalVolts / Math.Sqrt(3d);
    }

    /// <summary>
    /// Electrical safety (M19): the drive cabinet as a pure, tested model. Two sources: the hall's 480 V supply
    /// through the main disconnect Q1, and a separate 120 V UPS feed from a hall panel through its breaker, landed
    /// on the control circuit so the PLC and HMI ride through a power cut. A transfer relay puts the control circuit
    /// on the transformer while the main bus is live, and on the UPS when it is not, so the UPS never backfeeds the
    /// transformer's primary. That is the E2 trap: Q1 open, the main bus and the dashboard read 0, the control
    /// terminals still read 120 V. Each VFD's DC bus decays after its supply is lost. Locks count per source.
    /// Simulated for training, not an electrical design.
    /// </summary>
    public sealed class ElectricalModel
    {
        // The control transformer is wired L1–L2, so its secondary leads L1 by 30°. The UPS is not synchronised with
        // the hall supply; 0° stands in for wherever its output happens to be.
        private const double TransformerAngleDeg = 30d;
        private const double UpsAngleDeg = 0d;

        private readonly ElectricalSettings _s;
        private readonly double[] _dcBus = new double[2];
        private readonly double _tau;

        public ElectricalModel(ElectricalSettings settings, bool upsFeedLive)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            UpsFeedLive = upsFeedLive;
            DisconnectClosed = true;
            UpsBreakerClosed = true;
            double ratio = _s.DcBusVolts / _s.SafeDcVolts;
            _tau = ratio > 1d && _s.DcDischargeSeconds > 0d ? _s.DcDischargeSeconds / Math.Log(ratio) : 0d;
            _dcBus[0] = _s.DcBusVolts;
            _dcBus[1] = _s.DcBusVolts;
        }

        public ElectricalSettings Settings => _s;

        /// <summary>Whether the hall's UPS circuit is energised in this scenario (E2's second source).</summary>
        public bool UpsFeedLive { get; }

        /// <summary>Q1, the through-door rotary disconnect: true while closed (ON).</summary>
        public bool DisconnectClosed { get; private set; }

        public int DisconnectLocks { get; private set; }

        /// <summary>The UPS feed's breaker in the hall panel: true while closed (ON).</summary>
        public bool UpsBreakerClosed { get; private set; }

        public int UpsLocks { get; private set; }

        public bool DoorOpen { get; private set; }

        /// <summary>The infeed VFD has tripped on a fault (E1); the line stops on it.</summary>
        public bool InfeedDriveFault { get; private set; }

        /// <summary>The main AC bus (load side of Q1) is live.</summary>
        public bool MainBusLive => DisconnectClosed;

        /// <summary>The UPS terminals on X3 are live.</summary>
        public bool UpsLive => UpsFeedLive && UpsBreakerClosed;

        public ControlSource ControlSource => MainBusLive ? ControlSource.Transformer : UpsLive ? ControlSource.Ups : ControlSource.None;

        /// <summary>What the dashboard sees: the main bus, L1–L2. It knows nothing about the UPS feed.</summary>
        public double BusVolts => Read(ProbePoint.L1, ProbePoint.L2).AcVolts;

        /// <summary>The control circuit, X2:1–X2:2.</summary>
        public double ControlVolts => Read(ProbePoint.ControlHot, ProbePoint.ControlNeutral).AcVolts;

        public double DcBusVolts(Vfd vfd) => _dcBus[(int)vfd];

        /// <summary>Seconds a DC bus takes to fall from <paramref name="volts"/> to the safe level with no supply.</summary>
        public double SecondsToSafe(double volts) =>
            volts <= _s.SafeDcVolts || _tau <= 0d ? 0d : _tau * Math.Log(volts / _s.SafeDcVolts);

        // ---- Switching -------------------------------------------------------------------------------------------

        /// <summary>Opens (<paramref name="closed"/> false) or closes Q1. It opens at any time; it closes only with no lock on it and the door shut.</summary>
        public SwitchResult SetDisconnect(bool closed)
        {
            if (closed == DisconnectClosed)
                return SwitchResult.NoChange;
            if (closed && DisconnectLocks > 0)
                return SwitchResult.RefusedLocked;
            if (closed && DoorOpen)
                return SwitchResult.RefusedInterlock;
            DisconnectClosed = closed;
            return SwitchResult.Done;
        }

        /// <summary>Hangs one more lock on Q1's handle; it fits only in OFF.</summary>
        public SwitchResult AddDisconnectLock()
        {
            if (DisconnectClosed)
                return SwitchResult.RefusedNotOpen;
            DisconnectLocks++;
            return SwitchResult.Done;
        }

        public SwitchResult RemoveDisconnectLock()
        {
            if (DisconnectLocks == 0)
                return SwitchResult.NoChange;
            DisconnectLocks--;
            return SwitchResult.Done;
        }

        /// <summary>Opens or closes the UPS feed's breaker in the hall panel, under the same lock rule as Q1.</summary>
        public SwitchResult SetUpsBreaker(bool closed)
        {
            if (closed == UpsBreakerClosed)
                return SwitchResult.NoChange;
            if (closed && UpsLocks > 0)
                return SwitchResult.RefusedLocked;
            UpsBreakerClosed = closed;
            return SwitchResult.Done;
        }

        public SwitchResult AddUpsLock()
        {
            if (UpsBreakerClosed)
                return SwitchResult.RefusedNotOpen;
            UpsLocks++;
            return SwitchResult.Done;
        }

        public SwitchResult RemoveUpsLock()
        {
            if (UpsLocks == 0)
                return SwitchResult.NoChange;
            UpsLocks--;
            return SwitchResult.Done;
        }

        /// <summary>
        /// Opens or shuts the door. The through-door handle interlocks it: the door opens only with Q1 open, unless
        /// a qualified person uses the defeater (energised diagnostics). Shutting is always allowed.
        /// </summary>
        public SwitchResult SetDoor(bool open, bool useDefeater = false)
        {
            if (open == DoorOpen)
                return SwitchResult.NoChange;
            if (open && DisconnectClosed && !useDefeater)
                return SwitchResult.RefusedInterlock;
            DoorOpen = open;
            return SwitchResult.Done;
        }

        public void SetInfeedDriveFault(bool fault) => InfeedDriveFault = fault;

        // ---- Time ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Advances the DC buses by <paramref name="dt"/> seconds: charged while the main bus is live, an exponential
        /// decay (through the drive's bleed resistors) once it is not, reaching the safe level after the discharge time.
        /// </summary>
        public void Tick(double dt)
        {
            if (dt <= 0d)
                return;
            for (int i = 0; i < _dcBus.Length; i++)
            {
                if (MainBusLive)
                    _dcBus[i] = _s.DcBusVolts;
                else
                    _dcBus[i] = _tau <= 0d ? 0d : _dcBus[i] * Math.Exp(-dt / _tau);
            }
        }

        // ---- Readings --------------------------------------------------------------------------------------------

        /// <summary>
        /// The voltage between two probe points, as a perfect meter would see it. Each point's potential to ground
        /// is an AC phasor plus a DC offset; the reading is the difference. A DC bus on a grounded-wye supply sits
        /// about half above and half below ground.
        /// </summary>
        public ProbeReading Read(ProbePoint a, ProbePoint b)
        {
            Potential pa = PotentialOf(a);
            Potential pb = PotentialOf(b);
            double re = pa.Re - pb.Re;
            double im = pa.Im - pb.Im;
            return new ProbeReading(Math.Sqrt(re * re + im * im), pa.Dc - pb.Dc);
        }

        private Potential PotentialOf(ProbePoint point)
        {
            double phase = _s.PhaseToGroundVolts;
            switch (point)
            {
                case ProbePoint.LineL1: return Potential.Ac(phase, 0d);
                case ProbePoint.LineL2: return Potential.Ac(phase, -120d);
                case ProbePoint.LineL3: return Potential.Ac(phase, 120d);
                case ProbePoint.L1: return MainBusLive ? Potential.Ac(phase, 0d) : default;
                case ProbePoint.L2: return MainBusLive ? Potential.Ac(phase, -120d) : default;
                case ProbePoint.L3: return MainBusLive ? Potential.Ac(phase, 120d) : default;
                case ProbePoint.ControlHot:
                    switch (ControlSource)
                    {
                        case ControlSource.Transformer: return Potential.Ac(_s.ControlVolts, TransformerAngleDeg);
                        case ControlSource.Ups: return Potential.Ac(_s.ControlVolts, UpsAngleDeg);
                        default: return default;
                    }
                case ProbePoint.UpsHot: return UpsLive ? Potential.Ac(_s.ControlVolts, UpsAngleDeg) : default;
                case ProbePoint.Vfd1DcPlus: return Potential.DcOnly(_dcBus[0] / 2d);
                case ProbePoint.Vfd1DcMinus: return Potential.DcOnly(-_dcBus[0] / 2d);
                case ProbePoint.Vfd2DcPlus: return Potential.DcOnly(_dcBus[1] / 2d);
                case ProbePoint.Vfd2DcMinus: return Potential.DcOnly(-_dcBus[1] / 2d);
                default: return default; // ground, the grounded neutrals, None
            }
        }

        private readonly struct Potential
        {
            private Potential(double re, double im, double dc)
            {
                Re = re;
                Im = im;
                Dc = dc;
            }

            public double Re { get; }
            public double Im { get; }
            public double Dc { get; }

            public static Potential Ac(double rms, double angleDeg)
            {
                double rad = angleDeg * Math.PI / 180d;
                return new Potential(rms * Math.Cos(rad), rms * Math.Sin(rad), 0d);
            }

            public static Potential DcOnly(double volts) => new Potential(0d, 0d, volts);
        }
    }

    /// <summary>Electrical safety: names for the probe points as the cabinet's drawings and terminal labels show them.</summary>
    public static class ProbePoints
    {
        public static readonly ProbePoint[] All =
        {
            ProbePoint.LineL1, ProbePoint.LineL2, ProbePoint.LineL3,
            ProbePoint.L1, ProbePoint.L2, ProbePoint.L3, ProbePoint.Ground,
            ProbePoint.ControlHot, ProbePoint.ControlNeutral, ProbePoint.UpsHot, ProbePoint.UpsNeutral,
            ProbePoint.Vfd1DcPlus, ProbePoint.Vfd1DcMinus, ProbePoint.Vfd2DcPlus, ProbePoint.Vfd2DcMinus,
        };

        public static string Label(ProbePoint point)
        {
            switch (point)
            {
                case ProbePoint.LineL1: return "Q1 line L1";
                case ProbePoint.LineL2: return "Q1 line L2";
                case ProbePoint.LineL3: return "Q1 line L3";
                case ProbePoint.L1: return "X1:L1";
                case ProbePoint.L2: return "X1:L2";
                case ProbePoint.L3: return "X1:L3";
                case ProbePoint.Ground: return "PE";
                case ProbePoint.ControlHot: return "X2:1";
                case ProbePoint.ControlNeutral: return "X2:2";
                case ProbePoint.UpsHot: return "X3:1";
                case ProbePoint.UpsNeutral: return "X3:2";
                case ProbePoint.Vfd1DcPlus: return "U1 DC+";
                case ProbePoint.Vfd1DcMinus: return "U1 DC-";
                case ProbePoint.Vfd2DcPlus: return "U2 DC+";
                case ProbePoint.Vfd2DcMinus: return "U2 DC-";
                default: return "";
            }
        }
    }
}
