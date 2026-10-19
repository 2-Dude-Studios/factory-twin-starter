using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: publishes cell/press/health whenever it changes, from the press's own fault signals.
    /// Runs in LateUpdate so it sees the telemetry row the press published this frame. M5 adds the risk warning.
    /// </summary>
    public sealed class PressHealthReporter : MonoBehaviour
    {
        [SerializeField] private CellContext context;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private MachineHealth health;

        private SignalBus _bus;
        private bool _published;

        public MachineHealth Health => health;

        private void Start()
        {
            _bus = context.Bus;
        }

        private void LateUpdate()
        {
            MachineHealth current = PressHealthRule.Evaluate(PressHealthRule.IsFaulted(_bus), false);
            if (_published && current == health)
                return;

            health = current;
            _published = true;
            _bus.Publish(CellTopics.PressHealth, SignalValue.FromNumber((int)current), CellContext.NowUnixMs);
        }
    }
}
