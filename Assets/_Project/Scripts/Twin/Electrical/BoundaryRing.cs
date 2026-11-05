using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Electrical safety (M19): one boundary's floor marking round the drive cabinet. Builds its own strip mesh in
    /// Awake from the <see cref="ArcFlashStudy"/> (so the ring always matches the label) and lights it while the
    /// trainee is at or inside that boundary (cell/elec/boundary), like the M10 zone outlines. A strip on the floor
    /// rather than a decal: one mesh, one material, no projector cost in the headset.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BoundaryRing : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private CellContext context;
        [SerializeField] private ArcFlashStudy study;
        [SerializeField] private ApproachBoundary boundary = ApproachBoundary.ArcFlash;
        [Tooltip("The exposed parts' footprint in this transform's space (x and z, metres); the ring is it grown by the boundary.")]
        [SerializeField] private Vector2 footprintMin = new Vector2(-0.37f, -0.2f);
        [SerializeField] private Vector2 footprintMax = new Vector2(0.37f, 0.2f);
        [SerializeField, Min(0.005f)] private float width = 0.05f;
        [SerializeField, Min(0f)] private float height = 0.004f;
        [SerializeField, Range(1, 16)] private int arcSteps = 8;
        [SerializeField] private Color color = new Color(1f, 0.45f, 0f);
        [SerializeField, Range(0f, 1f)] private float dimLevel = 0.5f;
        [SerializeField, Min(0f)] private float litEmission = 1.2f;

        private MaterialPropertyBlock _block;
        private MeshRenderer _renderer;
        private IDisposable _subscription;
        private Mesh _mesh;
        private bool _lit;
        private bool _shown = true;

        public ApproachBoundary Boundary => boundary;

        /// <summary>The distance this ring is drawn at, in millimetres from the exposed parts.</summary>
        public int OffsetMm
        {
            get
            {
                ArcFlashValues v = study.Values;
                switch (boundary)
                {
                    case ApproachBoundary.Restricted: return v.Shock.RestrictedMm;
                    case ApproachBoundary.Limited: return v.Shock.LimitedMm;
                    default: return v.ArcFlashBoundaryMm;
                }
            }
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _renderer = GetComponent<MeshRenderer>();
            if (study != null)
                BuildMesh();
        }

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
        }

        private void OnEnable()
        {
            if (context == null)
                return;
            SignalBus bus = context.Bus;
            if (bus.TryGetLast(CellTopics.ElecBoundary, out SignalMessage last))
                OnBoundary(last);
            _subscription = bus.Subscribe(CellTopics.ElecBoundary.ToString(), OnBoundary);
            Apply(_lit);
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void OnBoundary(SignalMessage message) => _lit = (int)message.Value.AsNumber >= (int)boundary;

        private void Update()
        {
            if (_lit != _shown)
                Apply(_lit);
        }

        private void Apply(bool lit)
        {
            _block.Clear();
            _block.SetColor(BaseColorId, color * (lit ? 1f : dimLevel));
            _block.SetColor(EmissionColorId, color * (lit ? litEmission : 0f));
            _renderer.SetPropertyBlock(_block);
            _shown = lit;
        }

        // A flat strip along the outline, its centre line at the boundary, lifted just off the floor.
        private void BuildMesh()
        {
            double offset = OffsetMm / 1000d;
            double[] line = ApproachBoundaries.Outline(footprintMin.x, footprintMax.x, footprintMin.y, footprintMax.y, offset, arcSteps);
            int n = line.Length / 2;
            var vertices = new Vector3[n * 2];
            var triangles = new int[(n - 1) * 6];
            float half = width * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var p = new Vector2((float)line[i * 2], (float)line[i * 2 + 1]);
                Vector2 prev = i > 0 ? new Vector2((float)line[(i - 1) * 2], (float)line[(i - 1) * 2 + 1]) : p;
                Vector2 next = i < n - 1 ? new Vector2((float)line[(i + 1) * 2], (float)line[(i + 1) * 2 + 1]) : p;
                Vector2 dir = (next - prev).normalized;
                var normal = new Vector2(dir.y, -dir.x); // inward: the U runs clockwise seen from above
                vertices[i * 2] = new Vector3(p.x - normal.x * half, height, p.y - normal.y * half);
                vertices[i * 2 + 1] = new Vector3(p.x + normal.x * half, height, p.y + normal.y * half);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2, t = i * 6;
                // Clockwise seen from above, so the strip faces up.
                triangles[t] = a;
                triangles[t + 1] = a + 2;
                triangles[t + 2] = a + 1;
                triangles[t + 3] = a + 1;
                triangles[t + 4] = a + 2;
                triangles[t + 5] = a + 3;
            }
            _mesh = new Mesh { name = "BoundaryRing_" + boundary };
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
    }
}
