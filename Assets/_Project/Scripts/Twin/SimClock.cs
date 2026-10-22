using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Production cell: how fast simulated time runs against wall-clock time, measured over fixed windows. A soft
    /// PLC counts its timers in wall-clock time, so the twin only tests it fairly while this ratio stays near 1.
    /// A time-scale change or the frame loop catching up after a stall pushes it away from 1. Pure.
    /// </summary>
    public sealed class SimClock
    {
        private readonly double _windowSeconds;
        private double _simStart = double.NaN;
        private double _wallStart;

        public SimClock(double windowSeconds = 1d)
        {
            if (!(windowSeconds > 0d))
                throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            _windowSeconds = windowSeconds;
        }

        /// <summary>Simulated seconds per wall-clock second over the last full window; 1 until one has passed.</summary>
        public double Ratio { get; private set; } = 1d;

        /// <summary>Feed the current simulated and wall-clock times. Returns true when a window closed and Ratio changed.</summary>
        public bool Sample(double simSeconds, double wallSeconds)
        {
            if (double.IsNaN(_simStart))
            {
                _simStart = simSeconds;
                _wallStart = wallSeconds;
                return false;
            }

            double wall = wallSeconds - _wallStart;
            if (wall < _windowSeconds)
                return false;

            Ratio = (simSeconds - _simStart) / wall;
            _simStart = simSeconds;
            _wallStart = wallSeconds;
            return true;
        }

        /// <summary>True when simulated time kept within <paramref name="tolerance"/> (e.g. 0.1 = ±10 %) of wall-clock time.</summary>
        public bool IsRealTime(double tolerance) => Math.Abs(Ratio - 1d) <= tolerance;
    }
}
