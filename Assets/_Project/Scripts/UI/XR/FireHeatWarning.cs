using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): the heat of a burning fire in the headset. Measures, on the floor, how far the trainee's head
    /// is from the base of the fire and turns the view red as they come close (<see cref="FireProximity"/>): a red
    /// edge from the warning distance, a pulsing red wash inside the danger zone. The score docks points for each
    /// step inside it (FireDrillScorer reads <see cref="TooClose"/>); the floor guide and the HUD read the distance.
    /// </summary>
    public sealed class FireHeatWarning : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("Knows where each fire's base is.")]
        [SerializeField] private FireExtinguisher extinguisher;
        [Tooltip("The headset camera.")]
        [SerializeField] private Transform head;
        [Tooltip("A quad just in front of the eyes with the heat vignette material.")]
        [SerializeField] private Renderer overlay;
        [SerializeField, Range(0.1f, 1f)] private float maxAlpha = 0.75f;
        [SerializeField, Min(0.5f)] private float fadeSpeed = 6f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private float distance = float.NaN;
        [SerializeField] private float heat;
        [SerializeField] private bool tooClose;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;
        private float _shown;

        /// <summary>Metres on the floor from the head to the base of the burning fire; NaN with no fire burning.</summary>
        public float Distance => distance;

        /// <summary>Inside the burning fire's danger zone.</summary>
        public bool TooClose => tooClose;

        /// <summary>The burning fire's base, or null.</summary>
        public Transform FireBase { get; private set; }

        /// <summary>The danger zone's radius round the burning fire, metres.</summary>
        public float TooCloseRadius { get; private set; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (overlay != null)
                overlay.enabled = false;
        }

        private void Update()
        {
            Sense();
            _shown = Mathf.MoveTowards(_shown, heat, fadeSpeed * Time.deltaTime);
            if (overlay == null)
                return;
            bool visible = _shown > 0.005f && head != null && head.gameObject.activeInHierarchy;
            overlay.enabled = visible;
            if (!visible)
                return;
            // A slow throb inside the danger zone, steady while only warm.
            float pulse = tooClose ? 0.85f + 0.15f * Mathf.Sin(Time.time * 9f) : 1f;
            overlay.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, new Color(1f, 0.08f, 0.02f, _shown * maxAlpha * pulse));
            overlay.SetPropertyBlock(_block);
        }

        private void Sense()
        {
            FireBase = null;
            distance = float.NaN;
            heat = 0f;
            tooClose = false;
            if (context == null || extinguisher == null || head == null)
                return;
            var stage = (FireStage)(int)Read(CellTopics.FireStage);
            if (!FireProximity.Burning(stage))
                return;
            Transform fireBase = extinguisher.BaseOf((FireLocation)(int)Read(CellTopics.FireLocation));
            if (fireBase == null)
                return;
            double size = Read(CellTopics.FireSize);
            Vector3 d = head.position - fireBase.position;
            d.y = 0f;
            FireBase = fireBase;
            distance = d.magnitude;
            TooCloseRadius = (float)FireProximity.TooCloseMetres(size);
            heat = (float)FireProximity.Heat(distance, size);
            tooClose = FireProximity.TooClose(distance, size);
        }

        private double Read(TopicPath topic) =>
            context.Bus.TryGetLast(topic, out SignalMessage message) ? message.Value.AsNumber : 0d;
    }
}
