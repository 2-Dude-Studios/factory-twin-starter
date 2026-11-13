using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone mode (M27): the cell pauses while the headset is off the trainee's head or the app is behind the
    /// system menu, and carries on where it was when they come back. Game time stops (drill timers, the line, fires),
    /// and so does the sound. Read from the headset's presence sensor (CommonUsages.userPresence) where it reports one,
    /// and from the app losing focus or being paused. A recentre from the system menu is logged, so a jump in the view
    /// can be told apart from a teleport.
    /// </summary>
    public sealed class HeadsetPause : MonoBehaviour
    {
        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool paused;

        private readonly List<InputDevice> _heads = new List<InputDevice>();
        private readonly List<XRInputSubsystem> _inputs = new List<XRInputSubsystem>();
        private bool _focused = true;
        private bool _appPaused;
        private bool _present = true;
        private float _timeScale = 1f;

        private void OnEnable()
        {
            SubsystemManager.GetSubsystems(_inputs);
            foreach (XRInputSubsystem input in _inputs)
                input.trackingOriginUpdated += OnRecentred;
        }

        private void OnDisable()
        {
            foreach (XRInputSubsystem input in _inputs)
                input.trackingOriginUpdated -= OnRecentred;
            _inputs.Clear();
            Apply(false);
        }

        private void OnApplicationFocus(bool focus)
        {
            _focused = focus;
            Decide();
        }

        private void OnApplicationPause(bool pause)
        {
            _appPaused = pause;
            Decide();
        }

        private void Update()
        {
            _heads.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, _heads);
            bool present = true;
            foreach (InputDevice head in _heads)
            {
                if (head.isValid && head.TryGetFeatureValue(CommonUsages.userPresence, out bool p))
                {
                    present = p;
                    break;
                }
            }
            if (present != _present)
            {
                _present = present;
                Decide();
            }
        }

        private void Decide() => Apply(!_focused || _appPaused || !_present);

        private void Apply(bool pause)
        {
            if (pause == paused)
                return;
            paused = pause;
            if (pause)
            {
                _timeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _timeScale;
            }
            AudioListener.pause = pause;
            Debug.Log($"[HeadsetPause] {(pause ? "Paused" : "Resumed")} (focus {_focused}, presence {_present}, app paused {_appPaused}).", this);
        }

        private void OnRecentred(XRInputSubsystem input) => Debug.Log("[HeadsetPause] View recentred from the system menu.", this);
    }
}
