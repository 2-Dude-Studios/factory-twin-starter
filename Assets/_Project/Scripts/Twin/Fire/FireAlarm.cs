namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Fire safety: what raised the alarm, published on cell/fire/alarm_source.</summary>
    public enum AlarmSource
    {
        None = 0,
        Manual = 1,
        Detector = 2,
    }

    /// <summary>
    /// Fire safety (M14): a detector's trip, pure and tested. Trips once its reading has stayed at or above the
    /// threshold for the response time, and stays tripped (latched, like a real detector head) until reset with the
    /// reading back below the threshold.
    /// </summary>
    public sealed class DetectorLatch
    {
        private readonly double _threshold;
        private readonly double _responseSeconds;
        private double _aboveSeconds;

        public DetectorLatch(double threshold, double responseSeconds)
        {
            _threshold = threshold;
            _responseSeconds = responseSeconds < 0d ? 0d : responseSeconds;
        }

        public bool Tripped { get; private set; }

        public double Reading { get; private set; }

        /// <summary>Returns true on the tick the detector trips.</summary>
        public bool Tick(double reading, double dt)
        {
            Reading = reading;
            if (Tripped)
                return false;
            _aboveSeconds = reading >= _threshold ? _aboveSeconds + dt : 0d;
            if (_aboveSeconds < _responseSeconds || reading < _threshold)
                return false;
            Tripped = true;
            return true;
        }

        /// <summary>Clears the latch, but only once the reading is below the threshold. Returns whether it cleared.</summary>
        public bool Reset()
        {
            if (Reading >= _threshold)
                return false;
            Tripped = false;
            _aboveSeconds = 0d;
            return true;
        }
    }

    /// <summary>
    /// Fire safety (M14): the fire alarm panel's latch, pure and tested. Any detector or a manual call point raises
    /// the alarm; it stays on until a reset, which is refused while a detector is still tripped or a call point is
    /// still pulled. The first source to raise it is kept for the record.
    /// </summary>
    public sealed class FireAlarmLatch
    {
        public bool Active { get; private set; }

        public AlarmSource Source { get; private set; }

        /// <summary>Raises the alarm from a source. Returns true if this raised it (it was not already on).</summary>
        public bool Raise(AlarmSource source)
        {
            if (source == AlarmSource.None || Active)
                return false;
            Active = true;
            Source = source;
            return true;
        }

        /// <summary>Resets the alarm. Refused (returns false) while any input still calls for it.</summary>
        public bool Reset(bool anyInputActive)
        {
            if (!Active)
                return true;
            if (anyInputActive)
                return false;
            Active = false;
            Source = AlarmSource.None;
            return true;
        }
    }
}
