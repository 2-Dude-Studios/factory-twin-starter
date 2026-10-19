using System;
using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Production cell: derives the dashboard's line figures from the sequencer's own signals. Parts/hour is the
    /// exit rate over a rolling window. Parts lost is what the line would have made while a press fault held it,
    /// at the rate it was running just before the hold. After a hold, that running rate stays frozen until a full
    /// window of unheld time has passed, so a second fault soon after the first is not counted at a rate the first
    /// one dragged down.
    /// </summary>
    public sealed class LineMetrics
    {
        private readonly double _windowSeconds;
        private readonly Queue<double> _exitTimes = new Queue<double>();

        private int _lastPartsOut = -1;
        private double _runningRate;
        private bool _wasHeld;
        private double _holdEndedAt = double.NegativeInfinity;

        public LineMetrics(double windowSeconds)
        {
            if (!(windowSeconds > 0d))
                throw new ArgumentOutOfRangeException(nameof(windowSeconds), "The rate window must be longer than zero seconds.");
            _windowSeconds = windowSeconds;
        }

        public double ElapsedSeconds { get; private set; }

        /// <summary>Exits per hour over the last window (or over the run so far, while it is shorter than the window).</summary>
        public double PartsPerHour { get; private set; }

        /// <summary>Parts not made while the line was held for a press fault. Fractional; the dashboard rounds it.</summary>
        public double PartsLost { get; private set; }

        public void Tick(double deltaSeconds, CellState state, int partsOut)
        {
            if (deltaSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            ElapsedSeconds += deltaSeconds;

            // The first reading is the baseline, so a counter that starts above zero is not one burst of exits.
            if (_lastPartsOut >= 0)
            {
                for (int i = _lastPartsOut; i < partsOut; i++)
                    _exitTimes.Enqueue(ElapsedSeconds);
            }
            _lastPartsOut = partsOut;

            while (_exitTimes.Count > 0 && _exitTimes.Peek() <= ElapsedSeconds - _windowSeconds)
                _exitTimes.Dequeue();

            double span = Math.Min(ElapsedSeconds, _windowSeconds);
            PartsPerHour = span > 0d ? _exitTimes.Count * 3600d / span : 0d;

            if (state == CellState.Held)
            {
                PartsLost += deltaSeconds * _runningRate / 3600d;
                _wasHeld = true;
                return;
            }

            if (_wasHeld)
            {
                _holdEndedAt = ElapsedSeconds;
                _wasHeld = false;
            }
            if (ElapsedSeconds - _holdEndedAt >= _windowSeconds)
                _runningRate = PartsPerHour;
        }
    }
}
