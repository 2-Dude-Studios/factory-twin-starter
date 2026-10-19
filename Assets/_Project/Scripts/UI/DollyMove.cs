using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Capture tools: moves a CinemachineSplineDolly camera from the start to the end of its spline over a set time,
    /// eased in and out, each time <see cref="Play"/> is called. Used for the opening hall-to-cell shot.
    /// </summary>
    public sealed class DollyMove : MonoBehaviour
    {
        [SerializeField] private CinemachineSplineDolly dolly;
        [SerializeField, Min(0.1f)] private float durationSeconds = 8f;

        private float _elapsed;
        private bool _playing;

        private void Awake()
        {
            if (dolly != null)
                dolly.PositionUnits = PathIndexUnit.Normalized;
        }

        /// <summary>Starts the move from the beginning of the path.</summary>
        public void Play()
        {
            _elapsed = 0f;
            _playing = true;
            Apply(0f);
        }

        private void Update()
        {
            if (!_playing)
                return;
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / durationSeconds);
            Apply(Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f)
                _playing = false;
        }

        private void Apply(float normalized)
        {
            if (dolly != null)
                dolly.CameraPosition = normalized;
        }
    }
}
