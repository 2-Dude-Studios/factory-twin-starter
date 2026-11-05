using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Fire safety: what a detector head senses.</summary>
    public enum DetectorKind
    {
        Smoke = 0,
        Heat = 1,
    }

    /// <summary>
    /// Fire safety (M14): one ceiling detector head over the locations it covers. Reads the fire's smoke or size from
    /// the bus, never the flames, and latches through a <see cref="DetectorLatch"/>. Ticked by the
    /// <see cref="FireAlarmPanel"/>; its lamp shows red once it trips.
    /// </summary>
    public sealed class FireDetector : MonoBehaviour
    {
        [SerializeField] private DetectorKind kind = DetectorKind.Smoke;
        [SerializeField] private FireLocation[] covers = { FireLocation.DriveCabinet };
        [Tooltip("Smoke level (0–1) for a smoke head, flame size (0–1) for a heat head.")]
        [SerializeField, Range(0f, 1f)] private float threshold = 0.3f;
        [SerializeField, Min(0f)] private float responseSeconds = 3f;
        [Tooltip("Optional indicator lamp; its emission turns red when the head trips.")]
        [SerializeField] private Renderer lamp;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private float reading;
        [SerializeField] private bool tripped;

        private DetectorLatch _latch;
        private MaterialPropertyBlock _block;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public DetectorKind Kind => kind;
        public bool Tripped => _latch != null && _latch.Tripped;

        private void Awake()
        {
            _latch = new DetectorLatch(threshold, responseSeconds);
            ShowLamp(false);
        }

        /// <summary>Advances the head by dt from the bus. Returns true on the tick it trips.</summary>
        public bool Tick(SignalBus bus, float dt)
        {
            double value = Covered(bus) ? Read(bus, kind == DetectorKind.Smoke ? CellTopics.FireSmoke : CellTopics.FireSize) : 0d;
            bool trippedNow = _latch.Tick(value, dt);
            reading = (float)value;
            if (trippedNow)
                ShowLamp(true);
            tripped = _latch.Tripped;
            return trippedNow;
        }

        /// <summary>A new drill: forget the trip whatever the reading.</summary>
        public void Restart()
        {
            _latch = new DetectorLatch(threshold, responseSeconds);
            tripped = false;
            ShowLamp(false);
        }

        /// <summary>Panel reset: clears the head if the reading is back below its threshold.</summary>
        public bool TryReset()
        {
            bool cleared = _latch.Reset();
            if (cleared)
                ShowLamp(false);
            tripped = _latch.Tripped;
            return cleared;
        }

        private bool Covered(SignalBus bus)
        {
            var at = (FireLocation)(int)Read(bus, CellTopics.FireLocation);
            if (at == FireLocation.None || covers == null)
                return false;
            for (int i = 0; i < covers.Length; i++)
            {
                if (covers[i] == at)
                    return true;
            }
            return false;
        }

        private static double Read(SignalBus bus, TopicPath topic) =>
            bus.TryGetLast(topic, out SignalMessage message) ? message.Value.AsNumber : 0d;

        private void ShowLamp(bool on)
        {
            if (lamp == null)
                return;
            _block ??= new MaterialPropertyBlock();
            lamp.GetPropertyBlock(_block);
            _block.SetColor(EmissionColor, on ? new Color(4f, 0.2f, 0.1f) : new Color(0.05f, 0.25f, 0.05f));
            lamp.SetPropertyBlock(_block);
        }
    }
}
