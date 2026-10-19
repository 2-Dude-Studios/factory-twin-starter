using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Capture tools: shows the frame rate in the dashboard's "fps" label (hidden until switched on) and, when the
    /// player is started with -logfps, writes it to the player log every 10 s so a build's 60 fps lock can be checked
    /// without looking at the screen.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed class FpsReadout : MonoBehaviour
    {
        private const double WindowSeconds = 0.5;
        private const double LogEverySeconds = 10d;

        [SerializeField] private UIDocument document;
        [SerializeField] private bool visibleOnStart;

        private readonly FpsMeter _meter = new FpsMeter(WindowSeconds);
        private Label _label;
        private bool _logToPlayerLog;
        private double _sinceLog;
        private double _logFrames;
        private double _logSeconds;
        private double _logWorstMs;

        public bool Visible
        {
            get => _label != null && !_label.ClassListContains("hidden");
            set => _label?.EnableInClassList("hidden", !value);
        }

        private void OnEnable()
        {
            _label = document != null && document.rootVisualElement != null ? document.rootVisualElement.Q<Label>("fps") : null;
            Visible = visibleOnStart;
            _logToPlayerLog = Array.IndexOf(Environment.GetCommandLineArgs(), "-logfps") >= 0;
        }

        private void Update()
        {
            double dt = Time.unscaledDeltaTime;
            if (_logToPlayerLog)
                AccumulateForLog(dt);

            if (!_meter.AddFrame(dt) || _label == null || !Visible)
                return;
            _label.text = _meter.Fps.ToString("0", CultureInfo.InvariantCulture) + " fps · worst "
                          + _meter.WorstFrameMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms";
        }

        private void AccumulateForLog(double dt)
        {
            if (!(dt > 0d))
                return;
            _logFrames++;
            _logSeconds += dt;
            _logWorstMs = Math.Max(_logWorstMs, dt * 1000d);
            _sinceLog += dt;
            if (_sinceLog < LogEverySeconds)
                return;

            Debug.Log("[FPS] " + (_logFrames / _logSeconds).ToString("0.0", CultureInfo.InvariantCulture)
                      + " fps over " + _logSeconds.ToString("0", CultureInfo.InvariantCulture) + " s, worst frame "
                      + _logWorstMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms, target "
                      + Application.targetFrameRate + ", vSync " + QualitySettings.vSyncCount);
            _sinceLog = 0d;
            _logFrames = 0d;
            _logSeconds = 0d;
            _logWorstMs = 0d;
        }
    }
}
