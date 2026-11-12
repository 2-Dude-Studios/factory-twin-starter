using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone Quest (M26): fixed foveated rendering on the headset, so the edges of each eye are drawn at lower
    /// resolution. Needs OpenXR's Foveated Rendering feature (set by Tools > Factory Twin > Quest > 2 Configure for
    /// Quest). Applied whenever a display subsystem starts, since VR is stopped and started again across scene loads.
    /// Android only; on the PC the Link runtime decides.
    /// </summary>
    public sealed class QuestDisplay : MonoBehaviour
    {
        [Tooltip("0 off, 1 the strongest. Quest maps it to its own levels (0.5 medium).")]
        [SerializeField, Range(0f, 1f)] private float foveation = 1f;

        private readonly List<XRDisplaySubsystem> _displays = new List<XRDisplaySubsystem>();
        private XRDisplaySubsystem _applied;

        private void Awake()
        {
            if (Application.platform != RuntimePlatform.Android)
                enabled = false;
        }

        private void Update()
        {
            SubsystemManager.GetSubsystems(_displays);
            XRDisplaySubsystem display = null;
            foreach (XRDisplaySubsystem d in _displays)
            {
                if (d.running)
                {
                    display = d;
                    break;
                }
            }
            if (display == null)
            {
                _applied = null;
                return;
            }
            if (display == _applied)
                return;
            display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
            display.foveatedRenderingLevel = foveation;
            _applied = display;
            Debug.Log($"[QuestDisplay] Foveated rendering level {foveation:F1}.");
        }
    }
}
