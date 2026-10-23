using realvirtual;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Pick station: the operator clearing a jam. On a rising edge of cell/gate1/clear it removes the part sitting
    /// in the gate sensor, as a person would lift it off the belt, so a stalled line can run again.
    /// </summary>
    public sealed class JamClearer : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private Sensor gateSensor;
        [SerializeField, Min(0f)] private float dissolveSeconds = 0.3f;

        private bool _last;

        private void FixedUpdate()
        {
            bool clear = context.Bus.TryGetLast(CellTopics.GateClear, out SignalMessage message) && message.Value.AsNumber != 0d;
            if (clear && !_last)
                ClearGate();
            _last = clear;
        }

        private void ClearGate()
        {
            MU part = null;
            if (gateSensor.CollidingMus.Count > 0)
                part = gateSensor.CollidingMus[0];
            else if (gateSensor.Occupied && gateSensor.LastTriggeredBy != null)
                part = gateSensor.LastTriggeredBy.GetComponentInParent<MU>();

            if (part == null)
            {
                Debug.Log("[JamClearer] Nothing in the gate to clear.", this);
                return;
            }
            Debug.Log($"[JamClearer] Removed {part.name} from the gate.", this);
            part.Dissolve(dissolveSeconds);
        }
    }
}
