using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Fire safety (M14): the fire alarm control panel. Ticks the detector heads, publishes cell/fire/smoke_detected
    /// and heat_detected, and latches cell/fire/alarm from any head or the manual call point (cell/fire/call_point,
    /// M16). The line holds on the alarm (CellSequencer). cell/fire/alarm_reset resets it once nothing calls for it;
    /// every new cell/fire/scenario starts the panel fresh.
    /// </summary>
    public sealed class FireAlarmPanel : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private FireDetector[] detectors;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool alarm;
        [SerializeField] private AlarmSource source;

        private SignalBus _bus;
        private readonly FireAlarmLatch _latch = new FireAlarmLatch();
        private IDisposable _scenarioSubscription;
        private IDisposable _callPointSubscription;
        private IDisposable _resetSubscription;
        private bool _smoke;
        private bool _heat;

        private void OnEnable()
        {
            if (context == null)
            {
                Debug.LogError("[FireAlarmPanel] Assign Context (Cell).", this);
                enabled = false;
                return;
            }
            _bus = context.Bus;
            _scenarioSubscription = _bus.Subscribe(CellTopics.FireScenario.ToString(), _ => Restart());
            _callPointSubscription = _bus.Subscribe(CellTopics.FireCallPoint.ToString(), OnCallPoint);
            _resetSubscription = _bus.Subscribe(CellTopics.FireAlarmReset.ToString(), OnReset);
            Restart();
        }

        private void OnDisable()
        {
            _scenarioSubscription?.Dispose();
            _callPointSubscription?.Dispose();
            _resetSubscription?.Dispose();
            _scenarioSubscription = _callPointSubscription = _resetSubscription = null;
        }

        private void Update()
        {
            if (detectors == null)
                return;
            float dt = Time.deltaTime;
            bool smoke = false;
            bool heat = false;
            for (int i = 0; i < detectors.Length; i++)
            {
                FireDetector head = detectors[i];
                if (head == null)
                    continue;
                head.Tick(_bus, dt);
                if (!head.Tripped)
                    continue;
                if (head.Kind == DetectorKind.Smoke)
                    smoke = true;
                else
                    heat = true;
            }
            PublishDetectors(smoke, heat, force: false);
            if (smoke || heat)
                Raise(AlarmSource.Detector);
        }

        private void OnCallPoint(SignalMessage message)
        {
            if (message.Value.AsNumber != 0d)
                Raise(AlarmSource.Manual);
        }

        private void OnReset(SignalMessage message)
        {
            if (message.Value.AsNumber == 0d)
                return;
            bool anyHeadStillTripped = false;
            if (detectors != null)
            {
                foreach (FireDetector head in detectors)
                {
                    if (head != null && !head.TryReset())
                        anyHeadStillTripped = true;
                }
            }
            PublishDetectors(SmokeTripped(), HeatTripped(), force: false);
            if (_latch.Reset(anyHeadStillTripped || CallPointPulled()))
                PublishAlarm();
        }

        private void Restart()
        {
            if (detectors != null)
            {
                foreach (FireDetector head in detectors)
                    head?.Restart();
            }
            _latch.Reset(false);
            PublishDetectors(false, false, force: true);
            PublishAlarm();
        }

        private void Raise(AlarmSource from)
        {
            if (_latch.Raise(from))
                PublishAlarm();
        }

        private void PublishDetectors(bool smoke, bool heat, bool force)
        {
            long now = CellContext.NowUnixMs;
            if (force || smoke != _smoke)
                _bus.Publish(CellTopics.FireSmokeDetected, SignalValue.FromBool(smoke), now);
            if (force || heat != _heat)
                _bus.Publish(CellTopics.FireHeatDetected, SignalValue.FromBool(heat), now);
            _smoke = smoke;
            _heat = heat;
        }

        // Source first, so a subscriber reacting to the alarm can say what raised it.
        private void PublishAlarm()
        {
            long now = CellContext.NowUnixMs;
            alarm = _latch.Active;
            source = _latch.Source;
            _bus.Publish(CellTopics.FireAlarmSource, SignalValue.FromNumber((int)source), now);
            _bus.Publish(CellTopics.FireAlarm, SignalValue.FromBool(alarm), now);
        }

        private bool SmokeTripped() => AnyTripped(DetectorKind.Smoke);
        private bool HeatTripped() => AnyTripped(DetectorKind.Heat);

        private bool AnyTripped(DetectorKind kind)
        {
            if (detectors == null)
                return false;
            foreach (FireDetector head in detectors)
            {
                if (head != null && head.Kind == kind && head.Tripped)
                    return true;
            }
            return false;
        }

        private bool CallPointPulled() =>
            _bus.TryGetLast(CellTopics.FireCallPoint, out SignalMessage message) && message.Value.AsNumber != 0d;
    }
}
