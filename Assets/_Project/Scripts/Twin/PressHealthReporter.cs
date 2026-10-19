using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: publishes cell/press/health whenever it changes. Fault comes from the press's own fault
    /// signals; Warning from cell/press/failure_risk crossing the risk config's warning band (with hysteresis).
    /// Runs in LateUpdate after <see cref="PressRiskReporter"/>, so it sees this frame's row and its risk.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class PressHealthReporter : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("Warning band. Leave empty to report only Ok and Fault.")]
        [SerializeField] private PressRiskConfig riskConfig;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private MachineHealth health;
        [SerializeField] private bool warning;

        private SignalBus _bus;
        private bool _published;

        public MachineHealth Health => health;

        private void Start()
        {
            _bus = context.Bus;
        }

        private void LateUpdate()
        {
            if (riskConfig != null && _bus.TryGetLast(CellTopics.PressFailureRisk, out SignalMessage risk))
                warning = PressHealthRule.UpdateWarning(warning, risk.Value.AsNumber, riskConfig.WarningRisk, riskConfig.WarningClearRisk);
            else
                warning = false;

            MachineHealth current = PressHealthRule.Evaluate(PressHealthRule.IsFaulted(_bus), warning);
            if (_published && current == health)
                return;

            health = current;
            _published = true;
            _bus.Publish(CellTopics.PressHealth, SignalValue.FromNumber((int)current), CellContext.NowUnixMs);
        }
    }
}
