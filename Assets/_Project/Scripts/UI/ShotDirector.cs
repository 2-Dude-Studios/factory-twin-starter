using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Capture tools: switches the main camera between operator control (realvirtual's orbit navigation) and the
    /// Cinemachine shots. Keys 4–8 pick shots 1–5 (shot 1 replays its dolly move), 9 hands control back to the
    /// operator. A live shot enables the CinemachineBrain on the main camera and disables the navigation
    /// behaviours, so the two never fight over the view; the camera itself stays enabled, so Camera.main keeps
    /// working for everything else. The shot can also be set from the Inspector in Play mode.
    /// </summary>
    public sealed class ShotDirector : MonoBehaviour
    {
        private const int LivePriority = 20;

        [Tooltip("In key order: 4, 5, 6, 7, 8.")]
        [SerializeField] private CinemachineCamera[] shots;
        [Tooltip("Plays when the first shot is selected.")]
        [SerializeField] private DollyMove openingDolly;
        [Tooltip("The CinemachineBrain on the main camera. Enabled only while a shot is live.")]
        [SerializeField] private Behaviour cinemachineBrain;
        [Tooltip("The main camera's navigation behaviours (realvirtual's mouse and touch control). Disabled while a shot is live.")]
        [SerializeField] private Behaviour[] operatorNavigation;
        [Tooltip("−1 is the operator camera. Change it in Play mode to cut to a shot.")]
        [SerializeField] private int shot = -1;

        private int _applied = int.MinValue;

        private void Start()
        {
            Apply(shot);
        }

        private void OnValidate()
        {
            if (Application.isPlaying && _applied != int.MinValue && shot != _applied)
                Apply(shot);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit4Key.wasPressedThisFrame) Apply(0);
            if (keyboard.digit5Key.wasPressedThisFrame) Apply(1);
            if (keyboard.digit6Key.wasPressedThisFrame) Apply(2);
            if (keyboard.digit7Key.wasPressedThisFrame) Apply(3);
            if (keyboard.digit8Key.wasPressedThisFrame) Apply(4);
            if (keyboard.digit9Key.wasPressedThisFrame) Apply(-1);
        }

        /// <summary>Cuts to shot <paramref name="index"/> (0-based), or back to the operator camera with −1.</summary>
        public void Apply(int index)
        {
            bool capture = shots != null && index >= 0 && index < shots.Length;
            shot = capture ? index : -1;
            _applied = shot;

            if (cinemachineBrain != null)
                cinemachineBrain.enabled = capture;
            if (operatorNavigation != null)
            {
                foreach (Behaviour behaviour in operatorNavigation)
                {
                    if (behaviour != null)
                        behaviour.enabled = !capture;
                }
            }
            if (!capture)
                return;

            for (int i = 0; i < shots.Length; i++)
            {
                if (shots[i] != null)
                    shots[i].Priority = i == index ? LivePriority : 0;
            }
            if (index == 0 && openingDolly != null)
                openingDolly.Play();
        }
    }
}
