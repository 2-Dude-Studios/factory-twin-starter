using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Soft PLC link: decides whether the PLC is alive. The PLC program counts a heartbeat every scan; if the count
    /// has not changed for longer than the timeout, or no reply has arrived at all, the PLC is treated as gone and
    /// the line must hold. Pure, driven by the caller's clock.
    /// </summary>
    public sealed class PlcWatchdog
    {
        private readonly double _timeoutSeconds;
        private double _lastChangeAt = double.NaN;
        private int _lastHeartbeat = -1;

        public PlcWatchdog(double timeoutSeconds)
        {
            if (!(timeoutSeconds > 0d))
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>A reply from the PLC carrying its heartbeat count.</summary>
        public void Observe(ushort heartbeat, double nowSeconds)
        {
            if (heartbeat != _lastHeartbeat)
            {
                _lastHeartbeat = heartbeat;
                _lastChangeAt = nowSeconds;
            }
        }

        public bool IsAlive(double nowSeconds)
        {
            return !double.IsNaN(_lastChangeAt) && nowSeconds - _lastChangeAt <= _timeoutSeconds;
        }

        /// <summary>Forget the PLC, e.g. after the connection drops; it counts as alive again from its next reply.</summary>
        public void Reset()
        {
            _lastChangeAt = double.NaN;
            _lastHeartbeat = -1;
        }
    }
}
