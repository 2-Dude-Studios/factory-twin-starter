using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: runs <see cref="FailureRiskModel"/> on the press's telemetry and publishes cell/press/failure_risk,
    /// cell/press/tool_rul_min and cell/press/maint_due when they change. Evaluates once per frame, after the
    /// replayer, and only when new telemetry arrived. Reads the bus, never the scene.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class PressRiskReporter : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private PressRiskConfig config;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private float risk;
        [SerializeField] private FaultMode likelyMode;
        [SerializeField] private float toolRemainingMin;
        [SerializeField] private bool maintenanceDue;

        private SignalBus _bus;
        private IDisposable _subscription;
        private bool _dirty;
        private bool _published;
        private double _lastRisk;
        private double _lastRemaining;
        private bool _lastDue;

        private void OnEnable()
        {
            if (context == null || config == null)
            {
                Debug.LogError("[PressRiskReporter] Assign Context (Cell) and Config (PressRiskConfig).", this);
                enabled = false;
                return;
            }
            _bus = context.Bus;
            _subscription = _bus.Subscribe("cell/press/+", OnPressMessage);
            _dirty = true;
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void OnPressMessage(SignalMessage message)
        {
            TopicPath topic = message.Topic;
            if (topic == CellTopics.PressToolWear || topic == CellTopics.PressTorque || topic == CellTopics.PressSpeed
                || topic == CellTopics.PressAirTemp || topic == CellTopics.PressProcessTemp
                || topic == CellTopics.PressProductQuality)
                _dirty = true;
        }

        private void LateUpdate()
        {
            if (!_dirty || !TryRead(out PressReading reading))
                return;
            _dirty = false;

            RiskAssessment a = FailureRiskModel.Evaluate(reading, config.Settings);
            risk = (float)a.Risk;
            likelyMode = a.LikelyMode;
            toolRemainingMin = (float)a.ToolRemainingMin;
            maintenanceDue = a.MaintenanceDue;

            // Three decimals is finer than any gauge shows and keeps sensor noise from flooding the bus.
            double roundedRisk = Math.Round(a.Risk, 3);
            double roundedRemaining = Math.Round(a.ToolRemainingMin, 1);
            long now = CellContext.NowUnixMs;
            if (!_published || !roundedRisk.Equals(_lastRisk))
                _bus.Publish(CellTopics.PressFailureRisk, SignalValue.FromNumber(roundedRisk), now);
            if (!_published || !roundedRemaining.Equals(_lastRemaining))
                _bus.Publish(CellTopics.PressToolRemaining, SignalValue.FromNumber(roundedRemaining), now);
            if (!_published || a.MaintenanceDue != _lastDue)
                _bus.Publish(CellTopics.PressMaintenanceDue, SignalValue.FromBool(a.MaintenanceDue), now);

            _published = true;
            _lastRisk = roundedRisk;
            _lastRemaining = roundedRemaining;
            _lastDue = a.MaintenanceDue;
        }

        private bool TryRead(out PressReading reading)
        {
            reading = default;
            if (!_bus.TryGetLast(CellTopics.PressAirTemp, out SignalMessage air)
                || !_bus.TryGetLast(CellTopics.PressProcessTemp, out SignalMessage process)
                || !_bus.TryGetLast(CellTopics.PressSpeed, out SignalMessage speed)
                || !_bus.TryGetLast(CellTopics.PressTorque, out SignalMessage torque)
                || !_bus.TryGetLast(CellTopics.PressToolWear, out SignalMessage wear))
                return false;

            // Without a product type (an older replayer) the low-quality limit is used: it warns earliest.
            var quality = ProductQuality.Low;
            if (_bus.TryGetLast(CellTopics.PressProductQuality, out SignalMessage q))
                quality = (ProductQuality)(int)q.Value.AsNumber;

            reading = new PressReading(air.Value.AsNumber, process.Value.AsNumber, speed.Value.AsNumber,
                torque.Value.AsNumber, wear.Value.AsNumber, quality);
            return true;
        }
    }
}
