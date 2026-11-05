using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Electrical safety (M19): how deep the trainee's head and hands are inside the drive cabinet's boundaries
    /// (arc flash, limited and restricted approach, sized from the <see cref="ArcFlashStudy"/>), measured from the
    /// cabinet's exposed parts while its door is open. Publishes the deepest point on cell/elec/boundary and raises
    /// <see cref="PointChanged"/> for each tracked point, so a later scorer can log a hand crossing separately from
    /// the head. Tracked points are the desktop trainee's eye and the headset's camera and controllers; inactive ones
    /// (the rig not in use) are skipped. These are the trainee's own pose, not machine state.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class BoundaryMonitor : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private ArcFlashStudy study;
        [Tooltip("The exposed parts' box: centred on this transform (unscaled), its +Z face behind the door.")]
        [SerializeField] private Transform exposedParts;
        [SerializeField] private Vector3 exposedSize = new Vector3(0.74f, 1.84f, 0.4f);
        [SerializeField] private Transform[] tracked = Array.Empty<Transform>();
        [Tooltip("Only while the door is open: with it shut nothing is exposed.")]
        [SerializeField] private bool onlyWhileDoorOpen = true;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private ApproachBoundary deepest;
        [SerializeField] private ApproachBoundary[] perPoint = Array.Empty<ApproachBoundary>();

        private IDisposable _doorSubscription;
        private bool _doorOpen;
        private bool _published;

        /// <summary>A tracked point moved to a new depth (the transform, its new depth).</summary>
        public event Action<Transform, ApproachBoundary> PointChanged;

        public ApproachBoundary Deepest => deepest;

        private void OnEnable()
        {
            if (context == null || study == null || exposedParts == null)
            {
                Debug.LogError("[BoundaryMonitor] Assign Context (Cell), the Arc Flash Study and Exposed Parts.", this);
                enabled = false;
                return;
            }
            if (perPoint.Length != tracked.Length)
                perPoint = new ApproachBoundary[tracked.Length];
            SignalBus bus = context.Bus;
            if (bus.TryGetLast(CellTopics.CabinetDoorOpen, out SignalMessage last))
                _doorOpen = last.Value.AsNumber != 0d;
            _doorSubscription = bus.Subscribe(CellTopics.CabinetDoorOpen.ToString(), m => _doorOpen = m.Value.AsNumber != 0d);
            _published = false;
        }

        private void OnDisable()
        {
            _doorSubscription?.Dispose();
            _doorSubscription = null;
        }

        private void Update()
        {
            ArcFlashValues values = study.Values;
            bool exposed = _doorOpen || !onlyWhileDoorOpen;
            Vector3 half = exposedSize * 0.5f;
            ApproachBoundary worst = ApproachBoundary.Outside;
            for (int i = 0; i < tracked.Length; i++)
            {
                Transform t = tracked[i];
                ApproachBoundary depth = ApproachBoundary.Outside;
                if (exposed && t != null && t.gameObject.activeInHierarchy)
                {
                    Vector3 p = exposedParts.InverseTransformPoint(t.position);
                    double metres = ApproachBoundaries.DistanceToExposedParts(p.x, p.y, p.z, -half.x, -half.y, -half.z, half.x, half.y, half.z);
                    depth = ApproachBoundaries.Classify(metres * 1000d, values);
                }
                if (depth != perPoint[i])
                {
                    perPoint[i] = depth;
                    PointChanged?.Invoke(t, depth);
                }
                worst = ApproachBoundaries.Deepest(worst, depth);
            }
            if (_published && worst == deepest)
                return;
            deepest = worst;
            _published = true;
            context.Bus.Publish(CellTopics.ElecBoundary, SignalValue.FromNumber((int)deepest), CellContext.NowUnixMs);
        }

        private void OnDrawGizmosSelected()
        {
            if (exposedParts == null)
                return;
            Gizmos.matrix = exposedParts.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.4f, 0f);
            Gizmos.DrawWireCube(Vector3.zero, exposedSize);
        }
    }
}
