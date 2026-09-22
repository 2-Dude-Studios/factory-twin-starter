using realvirtual;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Pick station (M1 placeholder, replaced by the six-axis arm in M2): on a rising pick command it lifts the part
    /// sitting in the gate sensor and sets it down at the place point, reporting busy while it moves. Talks to the
    /// rest of the cell only through its two realvirtual signals, exactly as the arm will.
    /// </summary>
    public sealed class PartTransfer : MonoBehaviour
    {
        [SerializeField] private Sensor pickSensor;
        [SerializeField] private Transform placePoint;
        [SerializeField] private PLCOutputBool command;
        [SerializeField] private PLCInputBool busy;
        [SerializeField, Min(0.1f)] private float durationSeconds = 2.5f;
        [SerializeField, Min(0f)] private float liftMeters = 0.3f;

        private Transform _carrier;
        private MU _part;
        private Vector3 _fromPosition;
        private Quaternion _fromRotation;
        private float _progress;
        private bool _lastCommand;

        private void Awake()
        {
            _carrier = new GameObject("TransferCarrier").transform;
            _carrier.SetParent(transform, false);
        }

        private void FixedUpdate()
        {
            bool commanded = command.Value;
            if (commanded && !_lastCommand && _part == null)
                TryBegin();
            _lastCommand = commanded;

            if (_part == null)
                return;

            _progress = Mathf.Min(1f, _progress + Time.fixedDeltaTime / durationSeconds);
            float s = Mathf.SmoothStep(0f, 1f, _progress);
            Vector3 position = Vector3.Lerp(_fromPosition, placePoint.position, s) + Vector3.up * (liftMeters * Mathf.Sin(Mathf.PI * s));
            _carrier.SetPositionAndRotation(position, Quaternion.Slerp(_fromRotation, placePoint.rotation, s));

            if (_progress >= 1f)
            {
                _part.Unfix();
                _part = null;
                busy.Value = false;
            }
        }

        private void TryBegin()
        {
            MU part = FindPart();
            if (part == null)
                return; // No ack: the sequencer re-pulses the command.

            _part = part;
            _progress = 0f;
            _fromPosition = part.transform.position;
            _fromRotation = part.transform.rotation;
            _carrier.SetPositionAndRotation(_fromPosition, _fromRotation);
            part.Fix(_carrier.gameObject);
            busy.Value = true;
        }

        private MU FindPart()
        {
            for (int i = 0; i < pickSensor.CollidingMus.Count; i++)
            {
                MU mu = pickSensor.CollidingMus[i];
                if (mu != null)
                    return mu;
            }
            return pickSensor.LastTriggeredBy != null && pickSensor.Occupied
                ? pickSensor.LastTriggeredBy.GetComponentInParent<MU>()
                : null;
        }
    }
}
