using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training (M12): shows the trainee where the step due is done. While a lockout run is on, a beacon bobs above
    /// the training point that step needs and that point pulses; between runs, both are off. Read from the bus; it
    /// changes nothing, and it never says more than the procedure's own next step, so mistakes stay possible.
    /// </summary>
    public sealed class TrainingGuide : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private CellContext context;
        [SerializeField] private TrainingPoint[] points = Array.Empty<TrainingPoint>();
        [Tooltip("The marker shown above the point due.")]
        [SerializeField] private Transform beacon;
        [SerializeField] private Renderer[] beaconRenderers = Array.Empty<Renderer>();
        [SerializeField] private Color beaconColor = new Color(1f, 0.68f, 0.05f);
        [SerializeField] private float height = 0.3f;
        [SerializeField] private float bob = 0.05f;

        private IDisposable _stepSubscription, _resultSubscription;
        private int _step;
        private TrainingOutcome _outcome;
        private TrainingPoint _target;
        private Vector3 _anchor;

        private void Awake()
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, beaconColor);
            block.SetColor(EmissionColorId, beaconColor * 3f);
            foreach (Renderer r in beaconRenderers)
            {
                if (r != null)
                    r.SetPropertyBlock(block);
            }
        }

        private void OnEnable()
        {
            SignalBus bus = context.Bus;
            if (bus.TryGetLast(CellTopics.TrainingStep, out SignalMessage step))
                _step = (int)step.Value.AsNumber;
            if (bus.TryGetLast(CellTopics.TrainingResult, out SignalMessage result))
                _outcome = (TrainingOutcome)(int)result.Value.AsNumber;
            _stepSubscription = bus.Subscribe(CellTopics.TrainingStep.ToString(), m => { _step = (int)m.Value.AsNumber; Refresh(); });
            _resultSubscription = bus.Subscribe(CellTopics.TrainingResult.ToString(), m => { _outcome = (TrainingOutcome)(int)m.Value.AsNumber; Refresh(); });
            Refresh();
        }

        private void OnDisable()
        {
            _stepSubscription?.Dispose();
            _resultSubscription?.Dispose();
            _stepSubscription = _resultSubscription = null;
            Point(null);
        }

        private void Update()
        {
            if (_target == null || beacon == null)
                return;
            beacon.position = _anchor + Vector3.up * (Mathf.Sin(Time.time * 3f) * bob);
        }

        private void Refresh()
        {
            TrainingPoint target = null;
            if (_outcome == TrainingOutcome.InProgress && _step >= 1 && _step <= LockoutProcedure.Steps.Count
                && TrainingPoints.TryKindFor(LockoutProcedure.Steps[_step - 1].Action, out TrainingPointKind kind))
            {
                foreach (TrainingPoint point in points)
                {
                    if (point != null && point.Kind == kind)
                        target = point;
                }
            }
            Point(target);
        }

        private void Point(TrainingPoint target)
        {
            _target = target;
            foreach (TrainingPoint point in points)
            {
                if (point != null)
                    point.SetGuided(point == target);
            }
            if (beacon == null)
                return;
            beacon.gameObject.SetActive(target != null);
            if (target != null)
                _anchor = target.transform.position + Vector3.up * height;
        }
    }
}
