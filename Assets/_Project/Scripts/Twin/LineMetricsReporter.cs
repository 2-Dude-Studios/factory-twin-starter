using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Production cell: runs <see cref="LineMetrics"/> on the fixed timestep, right after the sequencer, and
    /// publishes cell/line/parts_per_hour and cell/line/parts_lost as whole numbers whenever they change.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class LineMetricsReporter : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("Parts/hour is the exit rate over this many seconds. Longer is steadier; shorter dips faster on a hold.")]
        [SerializeField, Min(10f)] private float rateWindowSeconds = 120f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private float partsPerHour;
        [SerializeField] private float partsLost;

        private LineMetrics _metrics;
        private IDisposable _freshTake;
        private SignalBus _bus;
        private bool _published;
        private long _lastRate;
        private long _lastLost;

        private void Awake()
        {
            _metrics = new LineMetrics(rateWindowSeconds);
        }

        private void Start()
        {
            _bus = context.Bus;
        }

        private void OnEnable()
        {
            _freshTake = context.Bus.Subscribe(CellTopics.DemoFreshTake.ToString(), OnFreshTake);
        }

        private void OnDisable()
        {
            _freshTake?.Dispose();
            _freshTake = null;
        }

        private void OnFreshTake(SignalMessage message)
        {
            if (message.Value.AsNumber != 0d)
                _metrics.ResetPartsLost();
        }

        private void FixedUpdate()
        {
            var state = (CellState)(int)ReadNumber(CellTopics.LineState);
            _metrics.Tick(Time.fixedDeltaTime, state, (int)ReadNumber(CellTopics.LinePartsOut));

            partsPerHour = (float)_metrics.PartsPerHour;
            partsLost = (float)_metrics.PartsLost;

            long rate = (long)Math.Round(_metrics.PartsPerHour);
            long lost = (long)Math.Floor(_metrics.PartsLost);
            long now = CellContext.NowUnixMs;
            if (!_published || rate != _lastRate)
                _bus.Publish(CellTopics.LinePartsPerHour, SignalValue.FromNumber(rate), now);
            if (!_published || lost != _lastLost)
                _bus.Publish(CellTopics.LinePartsLost, SignalValue.FromNumber(lost), now);

            _published = true;
            _lastRate = rate;
            _lastLost = lost;
        }

        private double ReadNumber(TopicPath topic)
        {
            return _bus.TryGetLast(topic, out SignalMessage message) ? message.Value.AsNumber : 0d;
        }
    }
}
