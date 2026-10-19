using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: runs a <see cref="DegradationScenario"/> when cell/press/degrade turns true. While it runs, the
    /// press publishes the scheduled tool wear instead of the dataset's; when the tool reaches its failure wear the
    /// press switches to the dataset's real tool-wear failure rows, so the line holds the usual way. Any fault
    /// command (inject or clear) or degrade = false stops it.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class PressDegradation : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private PressStation press;
        [Tooltip("Tool wear when the scenario starts. Below the risk band so the climb is visible.")]
        [SerializeField, Min(0f)] private float startWearMin = 160f;
        [Tooltip("Tool wear at which the tool fails. Inside AI4I's 200–240 min failure window.")]
        [SerializeField, Min(1f)] private float failureWearMin = 215f;
        [Tooltip("Seconds from start to failure.")]
        [SerializeField, Min(1f)] private float durationSeconds = 75f;

        [Tooltip("Tick in Play mode to start the scenario, like the dashboard's Wear tool out button.")]
        [SerializeField] private bool startNow;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool running;
        [SerializeField] private float elapsedSeconds;
        [SerializeField] private float wearMin;

        private DegradationScenario _scenario;
        private IDisposable _degradeSubscription;
        private IDisposable _faultSubscription;

        public bool Running => running;

        private void OnEnable()
        {
            if (context == null || press == null)
            {
                Debug.LogError("[PressDegradation] Assign Context (Cell) and Press (Cell/Press).", this);
                enabled = false;
                return;
            }
            _degradeSubscription = context.Bus.Subscribe(CellTopics.PressDegrade.ToString(), OnDegrade);
            _faultSubscription = context.Bus.Subscribe(CellTopics.PressInjectFault.ToString(), _ => Stop());
        }

        private void OnDisable()
        {
            _degradeSubscription?.Dispose();
            _faultSubscription?.Dispose();
            _degradeSubscription = null;
            _faultSubscription = null;
            Stop();
        }

        // Inspector edits in Play mode go out as the same command everything else uses.
        private void OnValidate()
        {
            if (!startNow || !Application.isPlaying || _degradeSubscription == null)
                return;
            startNow = false;
            Begin();
        }

        /// <summary>Starts the scenario from its start wear, as if the command had come over the bus.</summary>
        public void Begin()
        {
            context.Bus.Publish(CellTopics.PressDegrade, SignalValue.FromBool(true), CellContext.NowUnixMs);
        }

        private void OnDegrade(SignalMessage message)
        {
            if (message.Value.AsNumber == 0d)
            {
                Stop();
                return;
            }

            _scenario = new DegradationScenario(startWearMin, failureWearMin, durationSeconds);
            press.FaultFilter = FaultMode.None;
            running = true;
            Apply();
        }

        private void Update()
        {
            if (!running)
                return;

            _scenario.Tick(Time.deltaTime);
            if (_scenario.Complete)
            {
                // The tool has worn out: hand over to the dataset's own tool-wear failure rows.
                Stop();
                press.FaultFilter = FaultMode.ToolWear;
                return;
            }
            Apply();
        }

        private void Apply()
        {
            elapsedSeconds = (float)_scenario.ElapsedSeconds;
            wearMin = (float)_scenario.WearMin;
            press.ToolWearOverride = _scenario.WearMin;
        }

        private void Stop()
        {
            running = false;
            if (press != null)
                press.ToolWearOverride = double.NaN;
        }
    }
}
