using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Fire safety (M14): where a fire is, published as a number on cell/fire/stage.</summary>
    public enum FireStage
    {
        None = 0,
        Smoke = 1,
        Incipient = 2,
        Growing = 3,
        BeyondIncipient = 4,
        Out = 5,
    }

    /// <summary>Fire safety: the fire classes this cell can have (NFPA 10), published on cell/fire/class.</summary>
    public enum FireClass
    {
        None = 0,
        A = 1,
        B = 2,
        C = 3,
    }

    /// <summary>Fire safety: what an extinguisher puts on the fire, published on cell/fire/agent.</summary>
    public enum FireAgent
    {
        None = 0,
        Water = 1,
        DryChemicalAbc = 2,
        Co2 = 3,
    }

    /// <summary>Fire safety: where the scenario's fire is, published on cell/fire/location.</summary>
    public enum FireLocation
    {
        None = 0,
        DriveCabinet = 1,
        Cnc = 2,
        InfeedWaste = 3,
        Chargers = 4,
        StorageRack = 5,
        ElectricalPanel = 6,
        SolventDrums = 7,
    }

    /// <summary>
    /// Fire safety: the numbers behind one fire, from first heat to burn-out. Durations are wall-clock seconds, sized
    /// for a training drill rather than a real fire's timeline. Serialised inside a <see cref="FireScenario"/>.
    /// </summary>
    [Serializable]
    public sealed class FireModelSettings
    {
        public const double KnockedDownSize = 0.002;

        // Precursor (scenarios with a warning on the dashboard): the drive cabinet heats up before it smokes.
        [SerializeField] private double precursorSeconds = 40;
        [SerializeField] private double ambientC = 32;
        [SerializeField] private double overheatC = 60;
        [SerializeField] private double ignitionC = 95;

        // Smoke before flame, then the flame grows: incipient until growingAt, growing until beyondAt, then on to 1.
        [SerializeField] private double smokeSeconds = 12;
        [SerializeField] private double ignitionSize = 0.05;
        [SerializeField] private double growingAt = 0.3;
        [SerializeField] private double beyondAt = 0.6;
        [SerializeField] private double incipientSeconds = 45;
        [SerializeField] private double growingSeconds = 25;
        [SerializeField] private double beyondSeconds = 40;

        // Smoke level 0–1: climbs to smokeAtIgnition during the smoke stage, then follows the flame; clears slowly once out.
        [SerializeField] private double smokeAtIgnition = 0.35;
        [SerializeField] private double smokeClearPerSecond = 0.02;

        // Suppression: size taken off per second by a matched agent aimed at the base from the right stand-off. A fire
        // past the incipient stage is beyond a portable extinguisher (1910.155(c)(26)): it barely responds.
        [SerializeField] private double knockdownPerSecond = 0.06;
        [SerializeField] private double beyondIncipientFactor = 0.1;

        // A wrong agent spreads the fire: size added per second while it is discharged.
        [SerializeField] private double wrongAgentGrowthPerSecond = 0.03;

        // Reflash: a fire knocked down needs soakSeconds more discharge, or it re-ignites reflashSeconds later.
        [SerializeField] private double soakSeconds = 2;
        [SerializeField] private double reflashSeconds = 6;

        /// <summary>The defaults above: a 40 s precursor, 12 s of smoke, 45 s incipient, 25 s growing.</summary>
        public FireModelSettings()
        {
        }

        /// <summary>Defaults, with the timings a scenario changes. Negative means keep the default.</summary>
        public FireModelSettings(double precursorSeconds = -1, double smokeSeconds = -1, double incipientSeconds = -1,
            double growingSeconds = -1, double knockdownPerSecond = -1, double soakSeconds = -1, double reflashSeconds = -1)
        {
            if (precursorSeconds >= 0) this.precursorSeconds = precursorSeconds;
            if (smokeSeconds >= 0) this.smokeSeconds = smokeSeconds;
            if (incipientSeconds >= 0) this.incipientSeconds = incipientSeconds;
            if (growingSeconds >= 0) this.growingSeconds = growingSeconds;
            if (knockdownPerSecond >= 0) this.knockdownPerSecond = knockdownPerSecond;
            if (soakSeconds >= 0) this.soakSeconds = soakSeconds;
            if (reflashSeconds >= 0) this.reflashSeconds = reflashSeconds;
        }

        public double PrecursorSeconds => precursorSeconds;
        public double AmbientC => ambientC;
        public double OverheatC => overheatC;
        public double IgnitionC => ignitionC;
        public double SmokeSeconds => smokeSeconds;
        public double IgnitionSize => ignitionSize;
        public double GrowingAt => growingAt;
        public double BeyondAt => beyondAt;
        public double IncipientSeconds => incipientSeconds;
        public double GrowingSeconds => growingSeconds;
        public double BeyondSeconds => beyondSeconds;
        public double SmokeAtIgnition => smokeAtIgnition;
        public double SmokeClearPerSecond => smokeClearPerSecond;
        public double KnockdownPerSecond => knockdownPerSecond;
        public double BeyondIncipientFactor => beyondIncipientFactor;
        public double WrongAgentGrowthPerSecond => wrongAgentGrowthPerSecond;
        public double SoakSeconds => soakSeconds;
        public double ReflashSeconds => reflashSeconds;
    }

    /// <summary>
    /// Fire safety (M14): one fire as a pure, tested model. Heat, then smoke, then a flame that grows through the
    /// incipient stage to beyond it. An extinguisher knocks it down by how well its agent suits the class, the aim and
    /// the stand-off. A wrong agent makes it worse. Stopping too soon lets it reflash. Simulated for training; not a
    /// fire-dynamics model.
    /// </summary>
    public sealed class FireModel
    {
        private readonly FireModelSettings _s;
        private double _stageSeconds;
        private double _soakSeconds;
        private double _outSeconds;
        private bool _soaked;

        public FireModel(FireModelSettings settings, FireClass fireClass, bool precursor)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            if (fireClass == FireClass.None)
                throw new ArgumentException("A fire needs a class.", nameof(fireClass));
            Class = fireClass;
            Energised = fireClass == FireClass.C;
            HasPrecursor = precursor;
            TemperatureC = settings.AmbientC;
            Stage = FireStage.None;
        }

        public FireClass Class { get; private set; }
        public bool HasPrecursor { get; }

        /// <summary>Class C while the equipment is live; once isolated the fire burns on as Class A.</summary>
        public bool Energised { get; private set; }

        public FireStage Stage { get; private set; }

        /// <summary>Flame size, 0–1.</summary>
        public double Size { get; private set; }

        /// <summary>Smoke level at the fire, 0–1.</summary>
        public double Smoke { get; private set; }

        /// <summary>The cabinet temperature for a scenario with a precursor; ambient otherwise.</summary>
        public double TemperatureC { get; private set; }

        public double ElapsedSeconds { get; private set; }

        public bool Overheating => TemperatureC >= _s.OverheatC;

        /// <summary>Discharges of an agent that does not suit the class, counted once per discharge.</summary>
        public int WrongAgentUses { get; private set; }

        public int Reflashes { get; private set; }

        /// <summary>True once the fire is out and soaked, so it cannot reflash.</summary>
        public bool Extinguished => Stage == FireStage.Out && _soaked;

        /// <summary>
        /// How well an agent suits a class (1 right, 0 useless, below 0 spreads it): water on A only; CO2 on B and C,
        /// weakly on A; ABC dry chemical on all three. Water on a live electrical fire or on burning oil is the classic
        /// critical mistake, so it scores below 0.
        /// </summary>
        public static double Effectiveness(FireAgent agent, FireClass fireClass)
        {
            switch (agent)
            {
                case FireAgent.Water:
                    return fireClass == FireClass.A ? 1d : -1d;
                case FireAgent.Co2:
                    return fireClass == FireClass.A ? 0.3d : 1d;
                case FireAgent.DryChemicalAbc:
                    return fireClass == FireClass.None ? 0d : 1d;
                default:
                    return 0d;
            }
        }

        /// <summary>The equipment's power is isolated. A Class C fire carries on as Class A.</summary>
        public void DeEnergise()
        {
            if (!Energised)
                return;
            Energised = false;
            Class = FireClass.A;
        }

        /// <summary>
        /// Advances the fire by <paramref name="dt"/> seconds. While an extinguisher discharges, pass its agent and
        /// the aim and stand-off factors (0–1, from the trainee's hands); otherwise <see cref="FireAgent.None"/>.
        /// </summary>
        public void Tick(double dt, FireAgent agent = FireAgent.None, double aim = 1d, double standoff = 1d, bool newDischarge = false)
        {
            if (dt <= 0d)
                return;
            ElapsedSeconds += dt;
            _stageSeconds += dt;

            switch (Stage)
            {
                case FireStage.None:
                    TickPrecursor();
                    return;
                case FireStage.Smoke:
                    TickSmoke();
                    return;
                case FireStage.Out:
                    TickOut(dt, agent);
                    return;
            }

            bool discharging = agent != FireAgent.None;
            double effect = discharging ? Effectiveness(agent, Class) : 0d;
            if (discharging && effect < 0d && newDischarge)
                WrongAgentUses++;

            double change = GrowthPerSecond() * dt;
            if (discharging)
            {
                if (effect < 0d)
                {
                    change += _s.WrongAgentGrowthPerSecond * -effect * dt;
                }
                else
                {
                    double knockdown = _s.KnockdownPerSecond * effect * Clamp01(aim) * Clamp01(standoff);
                    if (Stage == FireStage.BeyondIncipient)
                        knockdown *= _s.BeyondIncipientFactor;
                    change -= knockdown * dt;
                }
            }

            Size = Clamp01(Size + change);
            Smoke = Math.Max(Smoke, _s.SmokeAtIgnition + (1d - _s.SmokeAtIgnition) * Size);

            if (Size <= FireModelSettings.KnockedDownSize)
            {
                Size = 0d;
                SetStage(FireStage.Out);
                _soakSeconds = 0d;
                _outSeconds = 0d;
                _soaked = false;
                return;
            }
            SetStage(StageForSize(Size));
        }

        private void TickPrecursor()
        {
            if (HasPrecursor && _stageSeconds < _s.PrecursorSeconds)
            {
                double progress = _stageSeconds / _s.PrecursorSeconds;
                TemperatureC = _s.AmbientC + (_s.IgnitionC - _s.AmbientC) * progress;
                return;
            }
            if (HasPrecursor)
                TemperatureC = _s.IgnitionC;
            SetStage(FireStage.Smoke);
        }

        private void TickSmoke()
        {
            Smoke = _s.SmokeSeconds <= 0d ? _s.SmokeAtIgnition : Math.Min(1d, _s.SmokeAtIgnition * _stageSeconds / _s.SmokeSeconds);
            if (_stageSeconds < _s.SmokeSeconds)
                return;
            Size = _s.IgnitionSize;
            Smoke = _s.SmokeAtIgnition;
            SetStage(FireStage.Incipient);
        }

        // Out: keep discharging to soak it, or it reflashes. Smoke clears slowly either way.
        private void TickOut(double dt, FireAgent agent)
        {
            Smoke = Math.Max(0d, Smoke - _s.SmokeClearPerSecond * dt);
            if (_soaked)
                return;
            if (agent != FireAgent.None && Effectiveness(agent, Class) > 0d)
            {
                _soakSeconds += dt;
                if (_soakSeconds >= _s.SoakSeconds)
                    _soaked = true;
                return;
            }
            _outSeconds += dt;
            if (_outSeconds < _s.ReflashSeconds)
                return;
            Reflashes++;
            Size = _s.IgnitionSize;
            SetStage(FireStage.Incipient);
        }

        // Size per second for the current stage, so each stage takes its configured time when nobody intervenes.
        private double GrowthPerSecond()
        {
            if (Size < _s.GrowingAt)
                return PerSecond(_s.GrowingAt - _s.IgnitionSize, _s.IncipientSeconds);
            if (Size < _s.BeyondAt)
                return PerSecond(_s.BeyondAt - _s.GrowingAt, _s.GrowingSeconds);
            return PerSecond(1d - _s.BeyondAt, _s.BeyondSeconds);
        }

        private FireStage StageForSize(double size)
        {
            if (size >= _s.BeyondAt)
                return FireStage.BeyondIncipient;
            return size >= _s.GrowingAt ? FireStage.Growing : FireStage.Incipient;
        }

        private void SetStage(FireStage stage)
        {
            if (stage == Stage)
                return;
            Stage = stage;
            _stageSeconds = 0d;
        }

        private static double PerSecond(double amount, double seconds) => seconds <= 0d ? amount : amount / seconds;

        private static double Clamp01(double v) => v < 0d ? 0d : v > 1d ? 1d : v;
    }
}
