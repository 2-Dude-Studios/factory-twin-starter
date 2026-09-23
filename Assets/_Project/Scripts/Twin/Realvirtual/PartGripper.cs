using realvirtual;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Pick station: the arm's gripper. On a closing command it fixes the part standing in the gate sensor to
    /// the tool flange; on opening it releases the part onto whatever is below. Mirrors how a real vacuum
    /// gripper reacts to a PLC output.
    /// </summary>
    public sealed class PartGripper : MonoBehaviour
    {
        [SerializeField] private Sensor pickSensor;
        [SerializeField] private Transform attachPoint;
        [SerializeField] private PLCOutputBool gripCommand;
        [SerializeField] private PLCInputBool holding;
        [Tooltip("A part further from the flange than this is not grabbed, so a mistimed command grips nothing.")]
        [SerializeField, Min(0.01f)] private float maxGripDistance = 0.2f;

        private MU _part;

        /// <summary>True while a part is attached to the flange.</summary>
        public bool IsHolding => _part != null;

        private void FixedUpdate()
        {
            bool close = gripCommand != null && gripCommand.Value;

            if (close && _part == null)
                TryGrip();
            else if (!close && _part != null)
                Release();

            if (holding != null)
                holding.Value = _part != null;
        }

        private void TryGrip()
        {
            MU part = FindPart();
            if (part == null)
                return;
            _part = part;
            part.Fix(attachPoint.gameObject);
        }

        private void Release()
        {
            _part.Unfix();
            _part = null;
        }

        private MU FindPart()
        {
            MU best = null;
            float bestDistance = maxGripDistance;

            // A raycast sensor (our light barrier) never fills CollidingMus, so fall back to what it last saw.
            for (int i = 0; i < pickSensor.CollidingMus.Count; i++)
                Consider(pickSensor.CollidingMus[i], ref best, ref bestDistance);
            if (best == null && pickSensor.Occupied && pickSensor.LastTriggeredBy != null)
                Consider(pickSensor.LastTriggeredBy.GetComponentInParent<MU>(), ref best, ref bestDistance);

            return best;
        }

        private void Consider(MU candidate, ref MU best, ref float bestDistance)
        {
            if (candidate == null)
                return;
            float distance = Vector3.Distance(candidate.transform.position, attachPoint.position);
            if (distance > bestDistance)
                return;
            best = candidate;
            bestDistance = distance;
        }
    }
}
