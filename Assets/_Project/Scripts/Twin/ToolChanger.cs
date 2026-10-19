using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: acts on cell/press/tool_change = true. Stops a running degradation, puts a fresh tool in (the
    /// replay restarts from a low-wear row) and clears a tool-wear failure, whether it came from the degradation or
    /// from an injected TWF. Other injected failure modes stay: a new tool does not fix them.
    /// </summary>
    public sealed class ToolChanger : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private PressStation press;
        [SerializeField] private FaultInjector faultInjector;
        [Tooltip("The replay restarts from the next row with tool wear at or below this.")]
        [SerializeField, Min(0f)] private float freshToolMaxWearMin = 5f;
        [Tooltip("Tick in Play mode to change the tool, like the dashboard's Change tool button.")]
        [SerializeField] private bool changeNow;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private int toolChanges;

        private IDisposable _subscription;

        private void OnEnable()
        {
            if (context == null || press == null || faultInjector == null)
            {
                Debug.LogError("[ToolChanger] Assign Context (Cell), Press and Fault Injector (Cell/Press).", this);
                enabled = false;
                return;
            }
            _subscription = context.Bus.Subscribe(CellTopics.PressToolChange.ToString(), OnToolChange);
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        // Inspector edits in Play mode go out as the same command everything else uses.
        private void OnValidate()
        {
            if (!changeNow || !Application.isPlaying || _subscription == null)
                return;
            changeNow = false;
            Change();
        }

        /// <summary>Changes the tool, as if the command had come over the bus.</summary>
        public void Change()
        {
            context.Bus.Publish(CellTopics.PressToolChange, SignalValue.FromBool(true), CellContext.NowUnixMs);
        }

        private void OnToolChange(SignalMessage message)
        {
            if (message.Value.AsNumber == 0d)
                return;

            long now = CellContext.NowUnixMs;
            context.Bus.Publish(CellTopics.PressDegrade, SignalValue.FromBool(false), now);
            press.ChangeTool(freshToolMaxWearMin);

            // Keep the injector's state honest when the tool-wear failure had been injected.
            FaultMode injected = faultInjector.Active;
            if ((injected & FaultMode.ToolWear) != 0)
                faultInjector.Inject(injected & ~FaultMode.ToolWear);

            toolChanges++;
        }
    }
}
