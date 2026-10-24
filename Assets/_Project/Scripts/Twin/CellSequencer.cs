using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Production cell: runs <see cref="CellSequence"/> on the fixed timestep. Reads sensor state from the bus and
    /// publishes drive and pick commands back to it; TwinSignals turns those into realvirtual signal writes.
    /// The line is held for as long as the press station reports a fault, and stops at once on the safety zone's
    /// protective stop. Start and stop come from the operator's command counts (M11); an isolated CNC holds the
    /// line, and a start while it is locked out or isolated is refused and counted on cell/line/starts_refused. While cell/plc/source hands the line to
    /// an enabled soft PLC link, this component stands aside and writes nothing; taking the line back starts a
    /// fresh sequence.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CellSequencer : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("The soft PLC link. The sequencer stands aside only while this is enabled and cell/plc/source says PLC.")]
        [SerializeField] private PlcLink plcLink;
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
        private int _lastStartsRefused;
        // Command counts last seen; -1 until the first one arrives (that one is a baseline, not a press).
        private int _startCount = -1;
        private int _stopCount = -1;
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
            if (plcLink != null && plcLink.isActiveAndEnabled && LogicSourceRule.Read(_bus) == LogicSource.Plc)
            {
                if (_published)
                {
                    _published = false;
                    _sequence = new CellSequence(ackTimeoutSeconds, transferTimeoutSeconds);
                }
                // Keep the command baselines current, so a press the PLC already acted on is not replayed here.
                Pressed(CellTopics.LineStartCount, ref _startCount);
                Pressed(CellTopics.LineStopCount, ref _stopCount);
                return;
            }

            bool isolated = ReadBool(CellTopics.PressIsolated);
            var inputs = new CellInputs(
                ReadBool(CellTopics.GatePartPresent),
                ReadBool(CellTopics.ArmBusy),
                ReadBool(CellTopics.OutfeedPartExit),
                PressHealthRule.IsFaulted(_bus) || isolated,
                ReadBool(CellTopics.SafetyProtectiveStop),
                Pressed(CellTopics.LineStartCount, ref _startCount),
                Pressed(CellTopics.LineStopCount, ref _stopCount),
                isolated || ReadBool(CellTopics.PressLockout));

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

        // A press is the count going up by one since the last tick; any other change only moves the baseline.
        private bool Pressed(TopicPath topic, ref int last)
        {
            if (!_bus.TryGetLast(topic, out SignalMessage message))
                return false;
            int count = (int)message.Value.AsNumber;
            bool pressed = last >= 0 && CommandCount.IsPress(last, count);
            last = count;
            return pressed;
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
            if (!_published || _sequence.StartsRefused != _lastStartsRefused)
                _bus.Publish(CellTopics.LineStartsRefused, SignalValue.FromNumber(_sequence.StartsRefused), now);
            if (!_published || _sequence.PartsOut != _lastPartsOut)
                _bus.Publish(CellTopics.LinePartsOut, SignalValue.FromNumber(_sequence.PartsOut), now);

            double cycle = _sequence.LastCycleSeconds;
            if (!double.IsNaN(cycle) && !cycle.Equals(_lastCycleSeconds))
                _bus.Publish(CellTopics.LineCycleTime, SignalValue.FromNumber(cycle), now);

            _published = true;
            _lastOutputs = outputs;
            _lastState = _sequence.State;
            _lastPartsOut = _sequence.PartsOut;
            _lastStartsRefused = _sequence.StartsRefused;
            _lastCycleSeconds = cycle;
        }
    }
}
