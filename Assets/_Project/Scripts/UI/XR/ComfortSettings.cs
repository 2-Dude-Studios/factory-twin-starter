using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub (M29): comfort options, kept on the headset between sessions. Movement is free by default (the
    /// left stick walks, the right stick teleports and turns; you, 2026-11-14: move like free roam), with teleport only
    /// as the comfortable option; turning snaps by default; a comfort vignette darkens the edges during smooth movement;
    /// and the trainer's voice can be switched off. Applied to the rig's two ControllerInputActionManagers and the
    /// vignette; the hub menu's comfort page changes them.
    /// </summary>
    public sealed class ComfortSettings : MonoBehaviour
    {
        [SerializeField] private ControllerInputActionManager leftController;
        [SerializeField] private ControllerInputActionManager rightController;
        [Tooltip("The tunnelling vignette under the headset camera (on while the vignette option is).")]
        [SerializeField] private GameObject vignette;

        private const string SmoothMoveKey = "ft.comfort.smoothMove", SmoothTurnKey = "ft.comfort.smoothTurn";
        private const string VignetteKey = "ft.comfort.vignette", VoiceKey = "ft.comfort.voice";

        /// <summary>Raised when any option changes.</summary>
        public event Action Changed;

        public bool SmoothMove => PlayerPrefs.GetInt(SmoothMoveKey, 1) == 1;
        public bool SmoothTurn => PlayerPrefs.GetInt(SmoothTurnKey, 0) == 1;
        public bool Vignette => PlayerPrefs.GetInt(VignetteKey, 1) == 1;

        /// <summary>The trainer's voice (on by default).</summary>
        public static bool VoiceOn => PlayerPrefs.GetInt(VoiceKey, 1) == 1;

        private void Start() => Apply();

        public void ToggleSmoothMove() => Set(SmoothMoveKey, !SmoothMove);
        public void ToggleSmoothTurn() => Set(SmoothTurnKey, !SmoothTurn);
        public void ToggleVignette() => Set(VignetteKey, !Vignette);
        public void ToggleVoice() => Set(VoiceKey, !VoiceOn);

        private void Set(string key, bool on)
        {
            PlayerPrefs.SetInt(key, on ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        private void Apply()
        {
            // Smooth movement on the left stick (its teleport ray is off then); with it off, the left stick teleports
            // like the right one.
            if (leftController != null)
            {
                leftController.smoothMotionEnabled = SmoothMove;
                leftController.smoothTurnEnabled = SmoothTurn;
            }
            if (rightController != null)
            {
                rightController.smoothMotionEnabled = false;
                rightController.smoothTurnEnabled = SmoothTurn;
            }
            if (vignette != null)
                vignette.SetActive(Vignette && (SmoothMove || SmoothTurn));
        }
    }
}
