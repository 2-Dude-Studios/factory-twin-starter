using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Press station: one telemetry sample as the risk model sees it, in AI4I units (K, rpm, Nm, min).</summary>
    public readonly struct PressReading
    {
        public double AirTempK { get; }
        public double ProcessTempK { get; }
        public double SpeedRpm { get; }
        public double TorqueNm { get; }
        public double ToolWearMin { get; }
        public ProductQuality Quality { get; }

        public PressReading(double airTempK, double processTempK, double speedRpm, double torqueNm, double toolWearMin,
            ProductQuality quality)
        {
            AirTempK = airTempK;
            ProcessTempK = processTempK;
            SpeedRpm = speedRpm;
            TorqueNm = torqueNm;
            ToolWearMin = toolWearMin;
            Quality = quality;
        }

        public static PressReading From(Ai4iRow row) =>
            new PressReading(row.AirTempK, row.ProcessTempK, row.SpeedRpm, row.TorqueNm, row.ToolWearMin, row.Quality);
    }

    /// <summary>
    /// Press station: the failure limits AI4I 2020 publishes for each mode, and how far ahead of each limit the risk
    /// starts to climb. Serializable so a ScriptableObject can carry it; the defaults are the values measured against
    /// the dataset (see docs/STATUS.md, M5).
    /// </summary>
    [Serializable]
    public sealed class FailureRiskSettings
    {
        [Header("Tool wear (TWF): the tool fails somewhere between 200 and 240 min")]
        [Tooltip("Tool wear at which the failure window opens.")]
        [SerializeField] private float toolFailureStartMin = 200f;
        [Tooltip("Tool wear at which tool-wear risk starts to climb from 0.")]
        [SerializeField] private float toolRiskStartMin = 180f;
        [Tooltip("Recommend a tool change once this little tool life is left before the failure window.")]
        [SerializeField] private float toolChangeLeadMin = 20f;

        [Header("Overstrain (OSF): tool wear × torque over a limit set by product quality")]
        [SerializeField] private float overstrainLimitLow = 11000f;
        [SerializeField] private float overstrainLimitMedium = 12000f;
        [SerializeField] private float overstrainLimitHigh = 13000f;
        [Tooltip("Overstrain risk starts at this fraction of the limit.")]
        [SerializeField, Range(0f, 1f)] private float overstrainRiskStart = 0.9f;

        [Header("Power (PWF): torque × speed outside 3500–9000 W")]
        [SerializeField] private float powerLowLimitW = 3500f;
        [SerializeField] private float powerLowRiskStartW = 3800f;
        [SerializeField] private float powerHighLimitW = 9000f;
        [SerializeField] private float powerHighRiskStartW = 8700f;

        [Header("Heat dissipation (HDF): process − air under 8.6 K while speed is under 1380 rpm")]
        [SerializeField] private float heatDeltaLimitK = 8.6f;
        [Tooltip("Heat risk climbs over this many kelvin above the limit.")]
        [SerializeField] private float heatDeltaMarginK = 0.5f;
        [SerializeField] private float heatSpeedLimitRpm = 1380f;
        [Tooltip("Heat risk climbs over this many rpm above the limit.")]
        [SerializeField] private float heatSpeedMarginRpm = 50f;

        [Header("Warning")]
        [Tooltip("Risk at or above this raises the warning and the maintenance recommendation.")]
        [SerializeField, Range(0f, 1f)] private float warningRisk = 0.5f;

        public float ToolFailureStartMin => toolFailureStartMin;
        public float ToolRiskStartMin => toolRiskStartMin;
        public float ToolChangeLeadMin => toolChangeLeadMin;
        public float OverstrainLimitLow => overstrainLimitLow;
        public float OverstrainLimitMedium => overstrainLimitMedium;
        public float OverstrainLimitHigh => overstrainLimitHigh;
        public float OverstrainRiskStart => overstrainRiskStart;
        public float PowerLowLimitW => powerLowLimitW;
        public float PowerLowRiskStartW => powerLowRiskStartW;
        public float PowerHighLimitW => powerHighLimitW;
        public float PowerHighRiskStartW => powerHighRiskStartW;
        public float HeatDeltaLimitK => heatDeltaLimitK;
        public float HeatDeltaMarginK => heatDeltaMarginK;
        public float HeatSpeedLimitRpm => heatSpeedLimitRpm;
        public float HeatSpeedMarginRpm => heatSpeedMarginRpm;
        public float WarningRisk => warningRisk;

        public float OverstrainLimit(ProductQuality quality)
        {
            switch (quality)
            {
                case ProductQuality.Medium: return overstrainLimitMedium;
                case ProductQuality.High: return overstrainLimitHigh;
                default: return overstrainLimitLow;
            }
        }
    }

    /// <summary>Press station: the risk model's verdict on one reading.</summary>
    public readonly struct RiskAssessment
    {
        /// <summary>0 = far from every failure limit, 1 = at or past one. The highest of the four mode risks.</summary>
        public double Risk { get; }

        /// <summary>The failure mode whose limit is closest; None while every mode risk is 0.</summary>
        public FaultMode LikelyMode { get; }

        public double ToolWearRisk { get; }
        public double OverstrainRisk { get; }
        public double PowerRisk { get; }
        public double HeatRisk { get; }

        /// <summary>Minutes of tool wear left before the tool-failure window opens; 0 once inside it.</summary>
        public double ToolRemainingMin { get; }

        /// <summary>Risk has reached the warning level.</summary>
        public bool Warning { get; }

        /// <summary>Act now: risk is at the warning level, or the tool is within its change lead.</summary>
        public bool MaintenanceDue { get; }

        public RiskAssessment(double toolWear, double overstrain, double power, double heat, double toolRemainingMin,
            double warningRisk, double toolChangeLeadMin)
        {
            ToolWearRisk = toolWear;
            OverstrainRisk = overstrain;
            PowerRisk = power;
            HeatRisk = heat;

            double risk = toolWear;
            FaultMode mode = toolWear > 0d ? FaultMode.ToolWear : FaultMode.None;
            if (overstrain > risk) { risk = overstrain; mode = FaultMode.Overstrain; }
            if (power > risk) { risk = power; mode = FaultMode.Power; }
            if (heat > risk) { risk = heat; mode = FaultMode.HeatDissipation; }
            Risk = risk;
            LikelyMode = mode;

            ToolRemainingMin = toolRemainingMin;
            Warning = Risk >= warningRisk;
            MaintenanceDue = Warning || toolRemainingMin <= toolChangeLeadMin;
        }
    }

    /// <summary>
    /// Press station: failure risk as the distance to AI4I 2020's published failure limits. Each mode's risk is 0
    /// until its reading enters a margin ahead of the limit, then climbs linearly to 1 at the limit. Random failures
    /// (RNF) have no limit and cannot be predicted. Pure and allocation-free.
    /// </summary>
    public static class FailureRiskModel
    {
        public static RiskAssessment Evaluate(in PressReading reading, FailureRiskSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            double wear = reading.ToolWearMin;
            double toolRisk = Ramp(wear, settings.ToolRiskStartMin, settings.ToolFailureStartMin);

            double limit = settings.OverstrainLimit(reading.Quality);
            double overstrain = Ramp(wear * reading.TorqueNm, settings.OverstrainRiskStart * limit, limit);

            double powerW = reading.TorqueNm * reading.SpeedRpm * (2d * Math.PI / 60d);
            double power = Math.Max(
                Ramp(-powerW, -settings.PowerLowRiskStartW, -settings.PowerLowLimitW),
                Ramp(powerW, settings.PowerHighRiskStartW, settings.PowerHighLimitW));

            // Heat dissipation needs both conditions, so its risk is the smaller of the two.
            double delta = reading.ProcessTempK - reading.AirTempK;
            double heat = Math.Min(
                Ramp(-delta, -(settings.HeatDeltaLimitK + settings.HeatDeltaMarginK), -settings.HeatDeltaLimitK),
                Ramp(-reading.SpeedRpm, -(settings.HeatSpeedLimitRpm + settings.HeatSpeedMarginRpm), -settings.HeatSpeedLimitRpm));

            double remaining = Math.Max(0d, settings.ToolFailureStartMin - wear);
            return new RiskAssessment(toolRisk, overstrain, power, heat, remaining,
                settings.WarningRisk, settings.ToolChangeLeadMin);
        }

        // 0 at or below start, 1 at or above limit, linear between. A margin of zero is a step at the limit.
        private static double Ramp(double value, double start, double limit)
        {
            if (value >= limit)
                return 1d;
            if (value <= start || limit <= start)
                return 0d;
            return (value - start) / (limit - start);
        }
    }
}
