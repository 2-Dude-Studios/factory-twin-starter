using System;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Dashboard: a fixed-size ring of recent samples for one sparkline. Allocation-free after construction.</summary>
    public sealed class SparkSeries
    {
        private readonly float[] _samples;
        private int _next;

        public SparkSeries(int capacity)
        {
            if (capacity < 2)
                throw new ArgumentOutOfRangeException(nameof(capacity), "A sparkline needs at least two samples.");
            _samples = new float[capacity];
        }

        public int Capacity => _samples.Length;
        public int Count { get; private set; }

        /// <summary>Bumped on every change, so a view can tell when to redraw without comparing samples.</summary>
        public int Version { get; private set; }

        /// <summary>Sample <paramref name="index"/>, oldest first.</summary>
        public float this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                int start = Count < _samples.Length ? 0 : _next;
                return _samples[(start + index) % _samples.Length];
            }
        }

        public void Add(float value)
        {
            _samples[_next] = value;
            _next = (_next + 1) % _samples.Length;
            if (Count < _samples.Length)
                Count++;
            Version++;
        }

        public void Clear()
        {
            _next = 0;
            Count = 0;
            Version++;
        }
    }
}
