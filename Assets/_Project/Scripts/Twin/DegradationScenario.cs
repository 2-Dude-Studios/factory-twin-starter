using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: a tool wearing out on a schedule. Tool wear climbs linearly from a start value to the wear at
    /// which it fails, over a fixed time. This compresses hours of machining into about a minute so the demo can show
    /// the risk rising, the warning, and then the failure. Pure.
    /// </summary>
    public sealed class DegradationScenario
    {
        public DegradationScenario(double startWearMin, double failureWearMin, double durationSeconds)
        {
            if (!(failureWearMin > startWearMin))
                throw new ArgumentOutOfRangeException(nameof(failureWearMin), "The tool must fail above its start wear.");
            if (!(durationSeconds > 0d))
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "The scenario needs a positive duration.");

            StartWearMin = startWearMin;
            FailureWearMin = failureWearMin;
            DurationSeconds = durationSeconds;
        }

        public double StartWearMin { get; }
        public double FailureWearMin { get; }
        public double DurationSeconds { get; }
        public double ElapsedSeconds { get; private set; }

        public double WearMin => StartWearMin + (FailureWearMin - StartWearMin) * Math.Min(1d, ElapsedSeconds / DurationSeconds);

        /// <summary>The tool has reached its failure wear.</summary>
        public bool Complete => ElapsedSeconds >= DurationSeconds;

        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds > 0d)
                ElapsedSeconds += deltaSeconds;
        }

        /// <summary>Seconds into the scenario at which tool wear reaches <paramref name="wearMin"/> (0 if it starts above it).</summary>
        public double SecondsToReach(double wearMin)
        {
            double fraction = (wearMin - StartWearMin) / (FailureWearMin - StartWearMin);
            return DurationSeconds * Math.Max(0d, Math.Min(1d, fraction));
        }
    }
}
