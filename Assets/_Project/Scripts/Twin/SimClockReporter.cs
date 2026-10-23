using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Production cell: publishes Time.timeScale and the simulated-to-wall-clock time ratio once a second
    /// (cell/sim/time_scale, cell/sim/realtime_ratio), and warns when the ratio leaves the tolerance while the
    /// soft PLC runs the line, because the PLC's timers then no longer match the plant.
    /// </summary>
    public sealed class SimClockReporter : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("Allowed drift of simulated time from wall-clock time, e.g. 0.1 = ±10 %.")]
        [SerializeField, Range(0.01f, 1f)] private float tolerance = 0.1f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private float realtimeRatio = 1f;

        private readonly SimClock _clock = new SimClock();
        private SignalBus _bus;
        private float _lastTimeScale = -1f;

        private void Start()
        {
            _bus = context.Bus;
        }

        private void Update()
        {
            if (_bus == null)
                return;

            long now = CellContext.NowUnixMs;
            if (!Mathf.Approximately(Time.timeScale, _lastTimeScale))
            {
                _lastTimeScale = Time.timeScale;
                _bus.Publish(CellTopics.SimTimeScale, SignalValue.FromNumber(Time.timeScale), now);
            }

            if (!_clock.Sample(Time.timeAsDouble, Time.realtimeSinceStartupAsDouble))
                return;

            realtimeRatio = (float)_clock.Ratio;
            _bus.Publish(CellTopics.SimRealtimeRatio, SignalValue.FromNumber(System.Math.Round(_clock.Ratio, 3)), now);
            if (!_clock.IsRealTime(tolerance) && LogicSourceRule.Read(_bus) == LogicSource.Plc)
                Debug.LogWarning($"[SimClockReporter] Simulated time ran at {_clock.Ratio:0.00}× wall-clock time while the soft PLC runs the line (time scale {Time.timeScale:0.##}).", this);
        }
    }
}
