using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): shows on the floor where to stand round a burning fire. A red ring marks the danger zone
    /// (<see cref="FireProximity"/>, from FireHeatWarning); with an extinguisher in hand, a green band marks the
    /// distance its agent works best from (<see cref="ExtinguisherRules.Range"/>: CO2 1–2.5 m, dry chemical
    /// 1.8–3.5 m, water 3–6 m). Both follow the fire and hide when it is out.
    /// </summary>
    public sealed class FireDistanceGuide : MonoBehaviour
    {
        [SerializeField] private FireHeatWarning heat;
        [SerializeField] private FireExtinguisher extinguisher;
        [Tooltip("Flat annulus meshes on the floor, built here; their material is transparent and tinted per renderer.")]
        [SerializeField] private MeshFilter dangerRing;
        [SerializeField] private MeshFilter bestBand;
        [SerializeField] private float floorY = 0.012f;
        [SerializeField, Range(0.02f, 0.2f)] private float dangerRingWidth = 0.06f;
        [SerializeField] private Color dangerColour = new Color(1f, 0.12f, 0.06f, 0.7f);
        [SerializeField] private Color bestColour = new Color(0.2f, 0.95f, 0.4f, 0.32f);
        [SerializeField, Range(16, 128)] private int segments = 72;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private Mesh _danger, _best;
        private float _dangerRadius = -1f;
        private FireAgent _bandAgent = FireAgent.None;

        private void Awake()
        {
            _danger = Prepare(dangerRing, "DangerRing", dangerColour);
            _best = Prepare(bestBand, "BestBand", bestColour);
            Show(dangerRing, false);
            Show(bestBand, false);
        }

        private void OnDestroy()
        {
            if (_danger != null)
                Destroy(_danger);
            if (_best != null)
                Destroy(_best);
        }

        private void LateUpdate()
        {
            Transform fireBase = heat != null ? heat.FireBase : null;
            if (fireBase == null)
            {
                Show(dangerRing, false);
                Show(bestBand, false);
                return;
            }
            transform.position = new Vector3(fireBase.position.x, floorY, fireBase.position.z);
            transform.rotation = Quaternion.identity;

            float radius = heat.TooCloseRadius;
            if (Mathf.Abs(radius - _dangerRadius) > 0.01f)
            {
                _dangerRadius = radius;
                Annulus(_danger, radius - dangerRingWidth, radius);
            }
            Show(dangerRing, true);

            FireAgent agent = extinguisher != null ? extinguisher.Agent : FireAgent.None;
            if (agent != _bandAgent)
            {
                _bandAgent = agent;
                if (agent != FireAgent.None)
                {
                    (double _, double from, double to, double _) = ExtinguisherRules.Range(agent);
                    Annulus(_best, (float)from, (float)to);
                }
            }
            Show(bestBand, agent != FireAgent.None);
        }

        private Mesh Prepare(MeshFilter filter, string name, Color colour)
        {
            if (filter == null)
                return null;
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            var renderer = filter.GetComponent<Renderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColor, colour);
                renderer.SetPropertyBlock(block);
            }
            return mesh;
        }

        private static void Show(MeshFilter filter, bool on)
        {
            if (filter == null)
                return;
            var renderer = filter.GetComponent<Renderer>();
            if (renderer != null && renderer.enabled != on)
                renderer.enabled = on;
        }

        // A flat ring on the floor, facing up, between the two radii.
        private void Annulus(Mesh mesh, float inner, float outer)
        {
            if (mesh == null)
                return;
            inner = Mathf.Max(0f, inner);
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i * 2] = dir * inner;
                vertices[i * 2 + 1] = dir * outer;
                int n = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = n * 2;
                triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1;
                triangles[t + 4] = n * 2;
                triangles[t + 5] = n * 2 + 1;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
