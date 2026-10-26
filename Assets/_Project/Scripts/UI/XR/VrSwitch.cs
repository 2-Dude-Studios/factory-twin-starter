using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): switches between the desktop view and the Quest 3 over Link. XR is never started at launch
    /// (the desktop demo and the videos stay as they are); <c>-vr</c> on the command line, or F11, starts OpenXR,
    /// turns the XR rig on and the desktop trainee and desktop camera off. F11 again, or quitting, stops XR and gives
    /// the desktop back. The frame-rate lock is lifted while in VR so the headset runs at its own rate, and a lighter
    /// render pipeline (forward, no extra-light shadows, no SSAO) replaces the hall's Deferred+ one until VR stops.
    /// </summary>
    public sealed class VrSwitch : MonoBehaviour
    {
        public const string CommandLineFlag = "-vr";

        [Tooltip("The XR Origin rig. Inactive in the scene; on only while VR runs.")]
        [SerializeField] private GameObject xrRig;
        [Tooltip("Desktop-only objects (the desktop trainee, its HUD). Off while VR runs.")]
        [SerializeField] private GameObject[] desktopOnly = Array.Empty<GameObject>();
        [Tooltip("The desktop camera and its audio listener. Off while VR runs: the headset camera mirrors to the window.")]
        [SerializeField] private Camera desktopCamera;
        [SerializeField] private AudioListener desktopListener;
        [Tooltip("Render pipeline used while VR runs (Assets/_Project/Settings/VR_RPAsset). Empty keeps the desktop one.")]
        [SerializeField] private RenderPipelineAsset vrPipeline;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool vrRunning;
        [SerializeField] private string lastError;

        private bool _switching;
        private int _frameRateBefore;
        private RenderPipelineAsset _pipelineBefore;

        public bool VrRunning => vrRunning;

        private void Start()
        {
            if (xrRig != null)
                xrRig.SetActive(false);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), CommandLineFlag) >= 0)
                StartVr();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f11Key.wasPressedThisFrame)
            {
                if (vrRunning)
                    StopVr();
                else
                    StartVr();
            }
        }

        private void OnDisable()
        {
            if (vrRunning)
                StopVr();
        }

        public void StartVr()
        {
            if (!vrRunning && !_switching)
                StartCoroutine(StartVrRoutine());
        }

        private IEnumerator StartVrRoutine()
        {
            _switching = true;
            XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null)
            {
                Fail("XR is not configured for this platform (Tools > Factory Twin > Configure XR).");
                yield break;
            }

            yield return manager.InitializeLoader();
            if (manager.activeLoader == null)
            {
                Fail("OpenXR did not start: is the Quest connected over Link and Meta Horizon Link set as the OpenXR runtime?");
                yield break;
            }

            manager.StartSubsystems();
            _frameRateBefore = Application.targetFrameRate;
            Application.targetFrameRate = -1;
            if (vrPipeline != null)
            {
                _pipelineBefore = QualitySettings.renderPipeline;
                QualitySettings.renderPipeline = vrPipeline;
            }
            SetDesktop(false);
            if (xrRig != null)
                xrRig.SetActive(true);
            vrRunning = true;
            lastError = string.Empty;
            _switching = false;
            Debug.Log($"[VrSwitch] VR on ({manager.activeLoader.name}).", this);
        }

        public void StopVr()
        {
            XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (xrRig != null)
                xrRig.SetActive(false);
            if (manager != null && manager.isInitializationComplete)
            {
                manager.StopSubsystems();
                manager.DeinitializeLoader();
            }
            Application.targetFrameRate = _frameRateBefore;
            if (vrPipeline != null)
                QualitySettings.renderPipeline = _pipelineBefore;
            SetDesktop(true);
            vrRunning = false;
            Debug.Log("[VrSwitch] VR off; desktop view back.", this);
        }

        private void SetDesktop(bool on)
        {
            foreach (GameObject go in desktopOnly)
            {
                if (go != null)
                    go.SetActive(on);
            }
            if (desktopCamera != null)
                desktopCamera.enabled = on;
            if (desktopListener != null)
                desktopListener.enabled = on;
        }

        private void Fail(string message)
        {
            lastError = message;
            _switching = false;
            Debug.LogWarning("[VrSwitch] " + message, this);
        }
    }
}
