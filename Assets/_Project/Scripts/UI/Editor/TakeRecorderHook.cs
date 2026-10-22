using System;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Capture tools (Editor): records each take the <see cref="TakeSequencer"/> plays to its own MP4 with Unity
    /// Recorder: Game View at 1920×1080, H.264 high quality, with audio. PdM takes record at a constant 60 fps (game
    /// time follows the frames, so the video is smooth however fast the Editor renders). VC takes record at a variable
    /// rate capped at 60 fps, so game time follows the wall clock the soft PLC counts in. Files go to
    /// Builds/Recordings/ (git-ignored).
    /// </summary>
    [InitializeOnLoad]
    internal static class TakeRecorderHook
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const float FrameRate = 60f;

        private static TakeSequencer _sequencer;
        private static RecorderController _controller;
        private static string _outputFile;

        static TakeRecorderHook()
        {
            EditorApplication.update += FindSequencer;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void FindSequencer()
        {
            if (!EditorApplication.isPlaying || _sequencer != null)
                return;
            _sequencer = UnityEngine.Object.FindFirstObjectByType<TakeSequencer>();
            if (_sequencer == null)
                return;
            _sequencer.TakeStarted += StartRecording;
            _sequencer.TakeEnded += StopRecording;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingPlayMode)
                return;
            StopRecording(null);
            if (_sequencer != null)
            {
                _sequencer.TakeStarted -= StartRecording;
                _sequencer.TakeEnded -= StopRecording;
            }
            _sequencer = null;
        }

        private static void StartRecording(string take)
        {
            StopRecording(null);

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "Take " + take;
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = Width, OutputHeight = Height };
            movie.AudioInputSettings.PreserveAudio = true;
            _outputFile = "Builds/Recordings/2DS_FactoryTwin_Take" + take + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            movie.OutputFile = _outputFile;

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = FrameRate;
            bool realTime = TakeSequencer.IsRealTimeTake(take);
            settings.FrameRatePlayback = realTime ? FrameRatePlayback.Variable : FrameRatePlayback.Constant;
            settings.CapFrameRate = true;

            _controller = new RecorderController(settings);
            _controller.PrepareRecording();
            bool started = _controller.StartRecording();
            Debug.Log(started
                ? $"[TakeRecorder] Recording take {take} to {_outputFile}.mp4 ({Width}×{Height}, {(realTime ? "variable, up to " : "")}{FrameRate} fps, audio)."
                : $"[TakeRecorder] Recorder did not start for take {take}; see the Recorder's errors above.");
        }

        private static void StopRecording(string take)
        {
            if (_controller == null)
                return;
            if (_controller.IsRecording())
            {
                _controller.StopRecording();
                Debug.Log($"[TakeRecorder] Stopped take {take ?? "(play mode ended)"}: {_outputFile}.mp4");
            }
            _controller = null;
        }
    }
}
