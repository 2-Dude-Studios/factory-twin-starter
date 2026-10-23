using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Safety zone: which of the two zones a floor marking outlines.</summary>
    public enum ZoneRole
    {
        Warning = 0,
        Protective = 1,
    }

    /// <summary>
    /// Safety zone: how bright a zone's floor marking is for a safety state (0 dim, 1 lit). The warning outline lights
    /// while a person is in the warning or protective zone; the protective outline lights during a protective stop
    /// and flashes while the zone is clear and the stop waits for its reset.
    /// </summary>
    public static class ZoneMarkingPattern
    {
        public const double FlashHz = 1.5d;

        public static float Level(ZoneRole role, SafetyState state, double timeSeconds)
        {
            if (role == ZoneRole.Warning)
                return state == SafetyState.Reduced || state == SafetyState.Stopped ? 1f : 0f;

            switch (state)
            {
                case SafetyState.Stopped:
                    return 1f;
                case SafetyState.AwaitingReset:
                    return (timeSeconds * FlashHz) % 1d < 0.5d ? 1f : 0f;
                default:
                    return 0f;
            }
        }
    }

    /// <summary>
    /// Safety zone: tints one zone's floor outline from cell/safety/state, so the 3D cell shows what the dashboard
    /// shows. The strips are tinted through a MaterialPropertyBlock (_BaseColor and _EmissionColor on a URP Lit
    /// material with emission on), like the stack light; nothing reads the zone sensors directly.
    /// </summary>
    public sealed class ZoneMarking : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private CellContext context;
        [SerializeField] private ZoneRole role;
        [SerializeField] private Renderer[] strips = Array.Empty<Renderer>();
        [SerializeField] private Color color = new Color(1f, 0.68f, 0.05f);
        [Tooltip("Base colour brightness while dim, so the outline still reads on the floor.")]
        [SerializeField, Range(0f, 1f)] private float dimLevel = 0.45f;
        [Tooltip("HDR multiplier on a lit strip's emission, so bloom picks it up.")]
        [SerializeField, Min(0f)] private float litEmission = 1.2f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private SafetyState state;

        private MaterialPropertyBlock _block;
        private IDisposable _subscription;
        private float _shown = -1f;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            SignalBus bus = context.Bus;
            if (bus.TryGetLast(CellTopics.SafetyState, out SignalMessage last))
                OnState(last);
            _subscription = bus.Subscribe(CellTopics.SafetyState.ToString(), OnState);
            Apply(0f);
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void OnState(SignalMessage message)
        {
            state = (SafetyState)(int)message.Value.AsNumber;
        }

        private void Update()
        {
            float level = ZoneMarkingPattern.Level(role, state, Time.timeAsDouble);
            if (!level.Equals(_shown))
                Apply(level);
        }

        private void Apply(float level)
        {
            _block.Clear();
            _block.SetColor(BaseColorId, color * Mathf.Lerp(dimLevel, 1f, level));
            _block.SetColor(EmissionColorId, color * (level * litEmission));
            foreach (Renderer strip in strips)
            {
                if (strip != null)
                    strip.SetPropertyBlock(_block);
            }
            _shown = level;
        }
    }
}
