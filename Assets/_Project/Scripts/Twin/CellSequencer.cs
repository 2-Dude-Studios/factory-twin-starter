using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Production cell: runs <see cref="CellSequence"/> on the fixed timestep. Reads sensor state from the bus and
    /// publishes drive and pick commands back to it; TwinSignals turns those into realvirtual signal writes.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CellSequencer : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField, Min(0.1f)] private float ackTimeoutSeconds = 2f;
        [SerializeField, Min(1f)] private float transferTimeoutSeconds = 15f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private CellState state;
        [SerializeField] private int partsOut;
        [SerializeField] private float lastCycleSeconds;
        [SerializeField] private int pickRetries;
        [SerializeField] private int stalls;
        [SerializeField] private float runSeconds;

        private CellSequence _sequence;
        private SignalBus _bus;

        private bool _published;
        private CellOutputs _lastOutputs;
        private CellState _lastState;
        private int _lastPartsOut;
        private double _lastCycleSeconds;

        public CellSequence Sequence => _sequence;

        private void Awake()
        {
            _sequence = new CellSequence(ackTimeoutSeconds, transferTimeoutSeconds);
        }

        private void Start()
        {
            _bus = context.Bus;
        }

        private void FixedUpdate()
        {
            var inputs = new CellInputs(
                ReadBool(CellTopics.GatePartPresent),
                ReadBool(CellTopics.ArmBusy),
                ReadBool(CellTopics.OutfeedPartExit));

            _sequence.Tick(inputs, Time.fixedDeltaTime);
            PublishChanges();
            MirrorStatus();
        }

        private void MirrorStatus()
        {
            state = _sequence.State;
            partsOut = _sequence.PartsOut;
            lastCycleSeconds = double.IsNaN(_sequence.LastCycleSeconds) ? 0f : (float)_sequence.LastCycleSeconds;
            pickRetries = _sequence.PickRetries;
            stalls = _sequence.Stalls;
            runSeconds = (float)_sequence.ElapsedSeconds;
        }

        private bool ReadBool(TopicPath topic)
        {
            return _bus.TryGetLast(topic, out SignalMessage message) && message.Value.AsNumber != 0d;
        }

        private void PublishChanges()
        {
            long now = CellContext.NowUnixMs;
            CellOutputs outputs = _sequence.Outputs;

            if (!_published || outputs.InfeedRun != _lastOutputs.InfeedRun)
                _bus.Publish(CellTopics.InfeedDriveForward, SignalValue.FromBool(outputs.InfeedRun), now);
            if (!_published || outputs.OutfeedRun != _lastOutputs.OutfeedRun)
                _bus.Publish(CellTopics.OutfeedDriveForward, SignalValue.FromBool(outputs.OutfeedRun), now);
            if (!_published || outputs.PickCommand != _lastOutputs.PickCommand)
                _bus.Publish(CellTopics.ArmPickCommand, SignalValue.FromBool(outputs.PickCommand), now);

            if (!_published || _sequence.State != _lastState)
            {
                _bus.Publish(CellTopics.LineState, SignalValue.FromNumber((int)_sequence.State), now);
                _bus.Publish(CellTopics.LineStalled, SignalValue.FromBool(_sequence.State == CellState.Stalled), now);
            }
            if (!_published || _sequence.PartsOut != _lastPartsOut)
                _bus.Publish(CellTopics.LinePartsOut, SignalValue.FromNumber(_sequence.PartsOut), now);

            double cycle = _sequence.LastCycleSeconds;
            if (!double.IsNaN(cycle) && !cycle.Equals(_lastCycleSeconds))
                _bus.Publish(CellTopics.LineCycleTime, SignalValue.FromNumber(cycle), now);

            _published = true;
            _lastOutputs = outputs;
            _lastState = _sequence.State;
            _lastPartsOut = _sequence.PartsOut;
            _lastCycleSeconds = cycle;
        }
    }
}
