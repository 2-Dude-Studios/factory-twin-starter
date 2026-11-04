using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Fire safety (M14): runs the scenario's <see cref="FireModel"/> and publishes it on the bus: stage, size, smoke,
    /// class, location, and the drive cabinet's temperature. cell/fire/scenario (in) starts scenario n and 0 clears
    /// the fire. Effects, detectors and the dashboard all read these signals, never this component.
    /// </summary>
    public sealed class FireStation : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private FireScenario[] scenarios;
        [SerializeField, Min(0f)] private float sizeDeadband = 0.01f;
        [SerializeField, Min(0f)] private float smokeDeadband = 0.01f;
        [SerializeField, Min(0f)] private float temperatureDeadband = 0.5f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private int scenario;
        [SerializeField] private FireStage stage;
        [SerializeField] private float size;
        [SerializeField] private float smoke;
        [SerializeField] private float cabinetTempC;

        private SignalBus _bus;
        private IDisposable _subscription;
        private FireModel _model;
        private FireScenario _active;

        private FireStage _lastStage;
        private double _lastSize;
        private double _lastSmoke;
        private double _lastTemp;
        private bool _lastOverheat;

        /// <summary>The fire burning now, or null. M15's extinguishers discharge onto it.</summary>
        public FireModel Model => _model;

        public FireScenario Active => _active;

        private void OnEnable()
        {
            if (context == null)
            {
                Debug.LogError("[FireStation] Assign Context (Cell).", this);
                enabled = false;
                return;
            }
            _bus = context.Bus;
            _subscription = _bus.Subscribe(CellTopics.FireScenario.ToString(), OnScenario);
            PublishIdle();
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void Update()
        {
            if (_model == null)
                return;
            _model.Tick(Time.deltaTime);
            Publish(force: false);
            MirrorStatus();
        }

        private void OnScenario(SignalMessage message)
        {
            int number = (int)message.Value.AsNumber;
            if (number <= 0)
            {
                _model = null;
                _active = null;
                scenario = 0;
                PublishIdle();
                MirrorStatus();
                return;
            }

            FireScenario found = Find(number);
            if (found == null)
            {
                Debug.LogWarning($"[FireStation] No fire scenario {number}.", this);
                return;
            }
            _active = found;
            _model = found.CreateModel();
            scenario = number;
            Publish(force: true);
            MirrorStatus();
        }

        private FireScenario Find(int number)
        {
            if (scenarios == null)
                return null;
            foreach (FireScenario s in scenarios)
            {
                if (s != null && s.Number == number)
                    return s;
            }
            return null;
        }

        private void PublishIdle()
        {
            long now = CellContext.NowUnixMs;
            double ambient = new FireModelSettings().AmbientC;
            _bus.Publish(CellTopics.FireClass, SignalValue.FromNumber((int)FireClass.None), now);
            _bus.Publish(CellTopics.FireLocation, SignalValue.FromNumber((int)FireLocation.None), now);
            _bus.Publish(CellTopics.FireSize, SignalValue.FromNumber(0d), now);
            _bus.Publish(CellTopics.FireSmoke, SignalValue.FromNumber(0d), now);
            _bus.Publish(CellTopics.FireStage, SignalValue.FromNumber((int)FireStage.None), now);
            _bus.Publish(CellTopics.CabinetTemp, SignalValue.FromNumber(ambient), now);
            _bus.Publish(CellTopics.CabinetOverheat, SignalValue.FromBool(false), now);
            _lastStage = FireStage.None;
            _lastSize = 0d;
            _lastSmoke = 0d;
            _lastTemp = ambient;
            _lastOverheat = false;
        }

        // Class and location go first, so a subscriber reacting to the stage can already name them.
        private void Publish(bool force)
        {
            long now = CellContext.NowUnixMs;
            if (force)
            {
                _bus.Publish(CellTopics.FireClass, SignalValue.FromNumber((int)_model.Class), now);
                _bus.Publish(CellTopics.FireLocation, SignalValue.FromNumber((int)_active.Location), now);
            }

            if (force || Math.Abs(_model.TemperatureC - _lastTemp) >= temperatureDeadband)
            {
                _lastTemp = _model.TemperatureC;
                _bus.Publish(CellTopics.CabinetTemp, SignalValue.FromNumber(Math.Round(_lastTemp, 1)), now);
            }
            if (force || _model.Overheating != _lastOverheat)
            {
                _lastOverheat = _model.Overheating;
                _bus.Publish(CellTopics.CabinetOverheat, SignalValue.FromBool(_lastOverheat), now);
            }
            if (force || Math.Abs(_model.Smoke - _lastSmoke) >= smokeDeadband)
            {
                _lastSmoke = _model.Smoke;
                _bus.Publish(CellTopics.FireSmoke, SignalValue.FromNumber(Math.Round(_lastSmoke, 3)), now);
            }
            // Size always goes out on reaching 0, so a fire knocked down never reads as a small flame.
            bool sizeToZero = _model.Size <= 0d && _lastSize > 0d;
            if (force || sizeToZero || Math.Abs(_model.Size - _lastSize) >= sizeDeadband)
            {
                _lastSize = _model.Size;
                _bus.Publish(CellTopics.FireSize, SignalValue.FromNumber(Math.Round(_lastSize, 3)), now);
            }
            if (force || _model.Stage != _lastStage)
            {
                _lastStage = _model.Stage;
                _bus.Publish(CellTopics.FireStage, SignalValue.FromNumber((int)_lastStage), now);
            }
        }

        private void MirrorStatus()
        {
            stage = _model != null ? _model.Stage : FireStage.None;
            size = _model != null ? (float)_model.Size : 0f;
            smoke = _model != null ? (float)_model.Smoke : 0f;
            cabinetTempC = (float)_lastTemp;
        }
    }
}
