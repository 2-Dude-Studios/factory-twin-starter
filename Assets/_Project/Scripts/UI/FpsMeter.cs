using System;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Capture tools: frame rate over fixed windows of real time. Each finished window gives the average frames per
    /// second and the longest single frame in it, which is what shows a dropped frame in a 60 fps capture. Pure.
    /// </summary>
    public sealed class FpsMeter
    {
        private readonly double _windowSeconds;
        private double _elapsed;
        private int _frames;
        private double _worst;

        public FpsMeter(double windowSeconds)
        {
            if (!(windowSeconds > 0d))
                throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            _windowSeconds = windowSeconds;
        }

        /// <summary>Average frames per second over the last finished window; 0 before the first.</summary>
        public double Fps { get; private set; }

        /// <summary>Longest frame in the last finished window, in milliseconds.</summary>
        public double WorstFrameMs { get; private set; }

        /// <summary>Counts one frame of <paramref name="unscaledDeltaSeconds"/>. Returns true when a window has just finished.</summary>
        public bool AddFrame(double unscaledDeltaSeconds)
        {
            if (!(unscaledDeltaSeconds > 0d))
                return false;

            _elapsed += unscaledDeltaSeconds;
            _frames++;
            if (unscaledDeltaSeconds > _worst)
                _worst = unscaledDeltaSeconds;
            if (_elapsed < _windowSeconds)
                return false;

            Fps = _frames / _elapsed;
            WorstFrameMs = _worst * 1000d;
            _elapsed = 0d;
            _frames = 0;
            _worst = 0d;
            return true;
        }
    }
}
