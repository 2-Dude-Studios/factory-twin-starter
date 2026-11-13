using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Electrical safety (M19): owns the drive cabinet's <see cref="ElectricalModel"/>. Starts a scenario on
    /// cell/elec/scenario (0 ends it and puts the cabinet back to normal), carries out the cabinet commands under the
    /// model's rules (door, Q1, the UPS feed's breaker, locks on each), trips the infeed VFD on the scenario's
    /// schedule, ticks the DC buses, and publishes the cabinet's state. The tester reads <see cref="Read"/> directly;
    /// no reading between probe points is ever published.
    /// </summary>
    public sealed class CabinetStation : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private ElectricalScenario[] scenarios;
        [SerializeField, Min(0f)] private float voltsDeadband = 1f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private int scenario;
        [SerializeField] private bool disconnectOpen;
        [SerializeField] private int locks;
        [SerializeField] private bool upsFeedOpen;
        [SerializeField] private int upsLocks;
        [SerializeField] private bool doorOpen;
        [SerializeField] private bool driveFault;
        [SerializeField] private float busVolts;
        [SerializeField] private float dcBusVolts;
        [SerializeField] private float controlVolts;
        [SerializeField] private int refused;

        private SignalBus _bus;
        private IDisposable[] _subscriptions = Array.Empty<IDisposable>();
        private ElectricalModel _model;
        private ElectricalScenarioSettings _active;
        private double _sinceStart;

        private double _lastBus = double.NaN;
        private double _lastDc = double.NaN;
        private double _lastControl = double.NaN;

        /// <summary>The cabinet as it is now. M20's tester measures through <see cref="Read"/>.</summary>
        public ElectricalModel Model => _model;

        /// <summary>The scenario running, or null when the cabinet is in normal service.</summary>
        public ElectricalScenarioSettings Active => _active;

        /// <summary>The voltage between two probe points, for the tester's display only; never published.</summary>
        public ProbeReading Read(ProbePoint a, ProbePoint b) => _model.Read(a, b);

        private void Awake()
        {
            _model = NormalService();
        }

        private void OnEnable()
        {
            if (context == null)
            {
                Debug.LogError("[CabinetStation] Assign Context (Cell).", this);
                enabled = false;
                return;
            }
            _bus = context.Bus;
            _subscriptions = new[]
            {
                _bus.Subscribe(CellTopics.ElecScenario.ToString(), OnScenario),
                _bus.Subscribe(CellTopics.CabinetDoorCommand.ToString(), OnDoor),
                _bus.Subscribe(CellTopics.CabinetDisconnectCommand.ToString(), m => Apply(_model.SetDisconnect(m.Value.AsNumber == 0d), "switch Q1")),
                _bus.Subscribe(CellTopics.CabinetUpsCommand.ToString(), m => Apply(_model.SetUpsBreaker(m.Value.AsNumber == 0d), "switch the UPS feed")),
                _bus.Subscribe(CellTopics.CabinetLockCommand.ToString(), m => Apply(m.Value.AsNumber != 0d ? _model.AddDisconnectLock() : _model.RemoveDisconnectLock(), "lock Q1")),
                _bus.Subscribe(CellTopics.CabinetUpsLockCommand.ToString(), m => Apply(m.Value.AsNumber != 0d ? _model.AddUpsLock() : _model.RemoveUpsLock(), "lock the UPS feed")),
            };
            PublishAll();
        }

        private void OnDisable()
        {
            foreach (IDisposable s in _subscriptions)
                s?.Dispose();
            _subscriptions = Array.Empty<IDisposable>();
        }

        private void Update()
        {
            _model.Tick(Time.deltaTime);
            if (_active != null && !_model.InfeedDriveFault)
            {
                _sinceStart += Time.deltaTime;
                if (_sinceStart >= _active.DriveFaultAfterSeconds)
                {
                    _model.SetInfeedDriveFault(true);
                    PublishSwitches();
                }
            }
            PublishVolts(force: false);
        }

        // Normal service: E1's cabinet with no backfeed and no fault, so the cabinet always reads like a real one.
        private static ElectricalModel NormalService() => ElectricalScenarios.E1().CreateModel();

        private void OnScenario(SignalMessage message)
        {
            int number = (int)message.Value.AsNumber;
            if (number <= 0)
            {
                _active = null;
                _model = NormalService();
                scenario = 0;
                PublishAll();
                return;
            }
            ElectricalScenarioSettings found = Find(number);
            if (found == null)
            {
                Debug.LogWarning($"[CabinetStation] No electrical scenario {number}.", this);
                return;
            }
            _active = found;
            _model = found.CreateModel();
            _sinceStart = 0d;
            scenario = number;
            PublishAll();
        }

        private ElectricalScenarioSettings Find(int number)
        {
            if (scenarios != null)
            {
                foreach (ElectricalScenario s in scenarios)
                {
                    if (s != null && s.Number == number)
                        return s.Scenario;
                }
            }
            // No asset for it: the built-in data, so a scene without the assets still runs E1–E4.
            return ElectricalScenarios.ByNumber(number);
        }

        // door_cmd: 1 open, 2 open with the interlock defeater (energised diagnostics), 0 shut.
        private void OnDoor(SignalMessage message)
        {
            double v = message.Value.AsNumber;
            Apply(_model.SetDoor(v != 0d, useDefeater: v >= 2d), v != 0d ? "open the door" : "shut the door");
        }

        private void Apply(SwitchResult result, string what)
        {
            if (result == SwitchResult.Done)
            {
                PublishSwitches();
                PublishVolts(force: false);
                return;
            }
            if (result == SwitchResult.NoChange)
                return;
            refused++;
            Debug.Log($"[CabinetStation] Could not {what}: {ElectricalWords.Refusal(result)}.", this);
            _bus.Publish(CellTopics.CabinetRefused, SignalValue.FromNumber((int)result), CellContext.NowUnixMs);
        }

        private void PublishAll()
        {
            PublishSwitches();
            PublishVolts(force: true);
        }

        private void PublishSwitches()
        {
            long now = CellContext.NowUnixMs;
            _bus.Publish(CellTopics.CabinetDoorOpen, SignalValue.FromBool(_model.DoorOpen), now);
            _bus.Publish(CellTopics.CabinetDisconnect, SignalValue.FromNumber(_model.DisconnectClosed ? 0 : 1), now);
            _bus.Publish(CellTopics.CabinetUpsFeed, SignalValue.FromNumber(_model.UpsBreakerClosed ? 0 : 1), now);
            _bus.Publish(CellTopics.CabinetLocked, SignalValue.FromNumber(_model.DisconnectLocks), now);
            _bus.Publish(CellTopics.CabinetUpsLocked, SignalValue.FromNumber(_model.UpsLocks), now);
            _bus.Publish(CellTopics.InfeedDriveFault, SignalValue.FromBool(_model.InfeedDriveFault), now);
            doorOpen = _model.DoorOpen;
            disconnectOpen = !_model.DisconnectClosed;
            upsFeedOpen = !_model.UpsBreakerClosed;
            locks = _model.DisconnectLocks;
            upsLocks = _model.UpsLocks;
            driveFault = _model.InfeedDriveFault;
        }

        private void PublishVolts(bool force)
        {
            long now = CellContext.NowUnixMs;
            PublishIfMoved(CellTopics.CabinetBusVolts, _model.BusVolts, ref _lastBus, force, now);
            PublishIfMoved(CellTopics.CabinetDcBusVolts, _model.DcBusVolts(Vfd.Infeed), ref _lastDc, force, now);
            PublishIfMoved(CellTopics.CabinetControlVolts, _model.ControlVolts, ref _lastControl, force, now);
            busVolts = (float)_lastBus;
            dcBusVolts = (float)_lastDc;
            controlVolts = (float)_lastControl;
        }

        // Rounded to whole volts; a value reaching 0 always goes out, so a dead bus never reads as 1 V.
        private void PublishIfMoved(TopicPath topic, double volts, ref double last, bool force, long now)
        {
            double rounded = Math.Round(volts);
            bool toZero = rounded == 0d && last != 0d;
            if (!force && !toZero && !double.IsNaN(last) && Math.Abs(rounded - last) < voltsDeadband)
                return;
            last = rounded;
            _bus.Publish(topic, SignalValue.FromNumber(rounded), now);
        }
    }
}
