using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Safety zone: runs <see cref="SafetyZone"/> on the fixed timestep. Reads the two zone sensors from the bus
    /// (TwinSignals publishes them from the realvirtual zone sensors), takes each true on cell/safety/reset as one
    /// press of the reset button, and publishes the latched stop, the safety state and the arm's speed scale on
    /// change. Runs after TwinSignals and before the arm bridge and the sequencer, so a person entering the
    /// protective zone stops the arm on the next physics tick at most.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class SafetyZoneController : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("Arm speed in the warning zone, as a fraction of full speed.")]
        [SerializeField, Range(0.05f, 1f)] private float reducedSpeedScale = 0.25f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private SafetyState state;
        [SerializeField] private bool protectiveStop;
        [SerializeField] private int protectiveStops;
        [SerializeField] private int resetsRefused;

        private SafetyZone _zone;
        private SignalBus _bus;
        private IDisposable _resetSubscription;
        private bool _resetPressed;
        private bool _published;
        private SafetyState _lastState;
        private bool _lastStop;

        public SafetyState State => state;

        /// <summary>Raised when a reset is pressed while a person is still in the protective zone.</summary>
        public event Action ResetRefused;

        private void Awake()
        {
            _zone = new SafetyZone(reducedSpeedScale);
        }

        private void OnEnable()
        {
            _bus = context.Bus;
            _resetSubscription = _bus.Subscribe(CellTopics.SafetyReset.ToString(), OnReset);
        }

        private void OnDisable()
        {
            _resetSubscription?.Dispose();
            _resetSubscription = null;
        }

        /// <summary>Presses the reset button: publishes cell/safety/reset, the same command F6 and the dashboard send.</summary>
        public void PressReset()
        {
            context.Bus.Publish(CellTopics.SafetyReset, SignalValue.FromBool(true), CellContext.NowUnixMs);
        }

        private void OnReset(SignalMessage message)
        {
            if (message.Value.AsNumber != 0d)
                _resetPressed = true;
        }

        private void FixedUpdate()
        {
            // A press is true for exactly one update, so the zone sees a rising edge for every press.
            SafetyResetResult result = _zone.Update(
                ReadBool(CellTopics.SafetyZoneWarning),
                ReadBool(CellTopics.SafetyArmZoneOccupied),
                _resetPressed);
            _resetPressed = false;

            if (result == SafetyResetResult.RefusedZoneOccupied)
            {
                resetsRefused++;
                ResetRefused?.Invoke();
            }
            if (_zone.ProtectiveStop && !_lastStop)
                protectiveStops++;

            state = _zone.State;
            protectiveStop = _zone.ProtectiveStop;
            PublishChanges();
        }

        private void PublishChanges()
        {
            if (_published && _zone.State == _lastState && _zone.ProtectiveStop == _lastStop)
                return;

            long now = CellContext.NowUnixMs;
            // Speed first: the arm reads it through its signal, and it is the part of the stop that matters most.
            _bus.Publish(CellTopics.ArmSpeedScale, SignalValue.FromNumber(_zone.SpeedScale), now);
            if (!_published || _zone.ProtectiveStop != _lastStop)
                _bus.Publish(CellTopics.SafetyProtectiveStop, SignalValue.FromBool(_zone.ProtectiveStop), now);
            if (!_published || _zone.State != _lastState)
                _bus.Publish(CellTopics.SafetyState, SignalValue.FromNumber((int)_zone.State), now);

            _published = true;
            _lastState = _zone.State;
            _lastStop = _zone.ProtectiveStop;
        }

        private bool ReadBool(TopicPath topic)
        {
            return _bus.TryGetLast(topic, out SignalMessage message) && message.Value.AsNumber != 0d;
        }
    }
}
