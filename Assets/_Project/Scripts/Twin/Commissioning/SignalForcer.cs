using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Commissioning: turns force commands on the bus into entries in the cell's force table. Two commands, each a
    /// ForceMode (0 none, 1 off, 2 on): cell/gate1/force forces cell/gate1/part_present, and cell/safety/zone_force
    /// (M10) forces both safety zone sensors, since a person in the protective zone is in the warning zone too.
    /// </summary>
    public sealed class SignalForcer : MonoBehaviour
    {
        [SerializeField] private CellContext context;

        private IDisposable _gateSubscription;
        private IDisposable _zoneSubscription;

        private void OnEnable()
        {
            _gateSubscription = context.Bus.Subscribe(CellTopics.GateForce.ToString(), OnGateForce);
            _zoneSubscription = context.Bus.Subscribe(CellTopics.SafetyZoneForce.ToString(), OnZoneForce);
        }

        private void OnDisable()
        {
            _gateSubscription?.Dispose();
            _gateSubscription = null;
            _zoneSubscription?.Dispose();
            _zoneSubscription = null;
            context.Forces.ClearAll();
        }

        private void OnGateForce(SignalMessage message)
        {
            Force(CellTopics.GatePartPresent, SignalForces.ParseMode(message.Value.AsNumber));
        }

        private void OnZoneForce(SignalMessage message)
        {
            ForceMode mode = SignalForces.ParseMode(message.Value.AsNumber);
            Force(CellTopics.SafetyArmZoneOccupied, mode);
            Force(CellTopics.SafetyZoneWarning, mode);
        }

        private void Force(TopicPath input, ForceMode mode)
        {
            context.Forces.Apply(input, mode);
            if (mode != ForceMode.None)
                Debug.Log($"[SignalForcer] {input} forced {(mode == ForceMode.On ? "on" : "off")}.", this);
        }
    }
}
