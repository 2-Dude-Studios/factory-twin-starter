using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: forces a failure mode on demand. A FaultMode value published on cell/press/inject_fault
    /// (0 clears) switches the press to dataset rows that carry that failure, whether it comes from the dashboard,
    /// this Inspector or the broker. The line reacts to the press's own fault signals, not to this component.
    /// </summary>
    public sealed class FaultInjector : MonoBehaviour
    {
        private const FaultMode AllFaults = FaultMode.ToolWear | FaultMode.HeatDissipation | FaultMode.Power
                                            | FaultMode.Overstrain | FaultMode.Random;

        [SerializeField] private CellContext context;
        [SerializeField] private PressStation press;
        [Tooltip("The injected failure. Change it in Play mode to inject; None clears.")]
        [SerializeField] private FaultMode fault = FaultMode.None;

        private IDisposable _subscription;
        private FaultMode _applied;

        /// <summary>The failure currently injected; None when the press runs its baseline.</summary>
        public FaultMode Active => _applied;

        private void OnEnable()
        {
            _subscription = context.Bus.Subscribe(CellTopics.PressInjectFault.ToString(), OnCommand);
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        // Inspector edits in Play mode go out as the same command everything else uses.
        private void OnValidate()
        {
            if (Application.isPlaying && _subscription != null && fault != _applied)
                Inject(fault);
        }

        public void Inject(FaultMode mode)
        {
            context.Bus.Publish(CellTopics.PressInjectFault, SignalValue.FromNumber((int)(mode & AllFaults)), CellContext.NowUnixMs);
        }

        public void Clear()
        {
            Inject(FaultMode.None);
        }

        private void OnCommand(SignalMessage message)
        {
            _applied = (FaultMode)(int)message.Value.AsNumber & AllFaults;
            fault = _applied;
            press.FaultFilter = _applied;
        }
    }
}
