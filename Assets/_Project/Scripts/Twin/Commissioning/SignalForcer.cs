using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Commissioning: turns force commands on the bus into entries in the cell's force table. Today one pair:
    /// cell/gate1/force (0 none, 1 off, 2 on) forces cell/gate1/part_present.
    /// </summary>
    public sealed class SignalForcer : MonoBehaviour
    {
        [SerializeField] private CellContext context;

        private IDisposable _subscription;

        private void OnEnable()
        {
            _subscription = context.Bus.Subscribe(CellTopics.GateForce.ToString(), OnGateForce);
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
            context.Forces.ClearAll();
        }

        private void OnGateForce(SignalMessage message)
        {
            ForceMode mode = SignalForces.ParseMode(message.Value.AsNumber);
            context.Forces.Apply(CellTopics.GatePartPresent, mode);
            if (mode != ForceMode.None)
                Debug.Log($"[SignalForcer] {CellTopics.GatePartPresent} forced {(mode == ForceMode.On ? "on" : "off")}.", this);
        }
    }
}
