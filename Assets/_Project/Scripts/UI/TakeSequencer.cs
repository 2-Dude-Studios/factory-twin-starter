using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Capture tools: what a take does at one moment.</summary>
    public enum TakeAction
    {
        Shot,
        WearToolOut,
        ChangeTool,
    }

    /// <summary>Capture tools: one timed action in a take, <see cref="At"/> seconds of game time after the take starts.</summary>
    public readonly struct TakeStep
    {
        public float At { get; }
        public TakeAction Action { get; }
        public int Shot { get; }

        public TakeStep(float at, TakeAction action, int shot = -1)
        {
            At = at;
            Action = action;
            Shot = shot;
        }
    }

    /// <summary>
    /// Capture tools: plays the video's takes on a fixed timeline so every recording is the same. One warm-up
    /// (not recorded) lets parts/hour settle, then for each take a pre-roll (not recorded) gives a fresh take and
    /// cuts to the take's opening shot, and the take itself runs its steps. <see cref="TakeStarted"/> and
    /// <see cref="TakeEnded"/> mark the recorded part; an Editor hook turns them into Unity Recorder MP4s.
    /// Takes follow docs/voiceover.md: A beats 1–4 (ignore it), B beat 5 (act on it), C beat 6 inserts.
    /// </summary>
    public sealed class TakeSequencer : MonoBehaviour
    {
        // Shot indices follow ShotDirector: 0 hall dolly, 1 cell, 2 line, 3 CNC, 4 control panel.
        private static readonly TakeStep[] TakeA =
        {
            new TakeStep(0f, TakeAction.Shot, 0),
            new TakeStep(8f, TakeAction.Shot, 1),
            new TakeStep(14f, TakeAction.Shot, 2),
            new TakeStep(20f, TakeAction.WearToolOut),
            new TakeStep(20f, TakeAction.Shot, 3),
            new TakeStep(100f, TakeAction.Shot, 1),
        };
        private static readonly TakeStep[] TakeB =
        {
            new TakeStep(0f, TakeAction.Shot, 3),
            new TakeStep(2f, TakeAction.WearToolOut),
            new TakeStep(52f, TakeAction.ChangeTool),
            new TakeStep(56f, TakeAction.Shot, 1),
        };
        private static readonly TakeStep[] TakeC =
        {
            new TakeStep(0f, TakeAction.Shot, 4),
            new TakeStep(10f, TakeAction.Shot, 1),
            new TakeStep(20f, TakeAction.Shot, 2),
        };
        private const float LengthA = 114f;
        private const float LengthB = 72f;
        private const float LengthC = 28f;

        [SerializeField] private CellContext context;
        [SerializeField] private TakeControls takeControls;
        [SerializeField] private ShotDirector shotDirector;
        [Tooltip("Takes to run, in order, as letters: A, B, C.")]
        [SerializeField] private string takes = "ABC";
        [Tooltip("Running time before the first take so parts/hour settles (not recorded).")]
        [SerializeField, Min(0f)] private float warmUpSeconds = 150f;
        [Tooltip("Pre-roll before each take: fresh take, opening shot, settle (not recorded).")]
        [SerializeField, Min(1f)] private float preRollSeconds = 12f;
        [Tooltip("Tick in Play mode to run the takes.")]
        [SerializeField] private bool runNow;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private string phase = "idle";
        [SerializeField] private float phaseSeconds;

        private enum Phase { Idle, WarmUp, PreRoll, Take, Done }

        private Phase _phase = Phase.Idle;
        private float _phaseStart;
        private int _takeIndex;
        private int _nextStep;
        private TakeStep[] _steps;
        private float _length;
        private bool _started;

        /// <summary>A recorded take begins (argument: its letter).</summary>
        public event Action<string> TakeStarted;

        /// <summary>A recorded take ends (argument: its letter).</summary>
        public event Action<string> TakeEnded;

        /// <summary>All takes are done.</summary>
        public event Action Finished;

        public bool Running => _phase != Phase.Idle && _phase != Phase.Done;

        private void Start()
        {
            _started = true;
        }

        private void OnValidate()
        {
            if (!runNow || !Application.isPlaying || !_started)
                return;
            runNow = false;
            Run();
        }

        /// <summary>Starts the warm-up, then the takes. Ignored while already running.</summary>
        public void Run()
        {
            if (Running || string.IsNullOrEmpty(takes))
                return;
            _takeIndex = 0;
            Enter(warmUpSeconds > 0f ? Phase.WarmUp : Phase.PreRoll);
            if (_phase == Phase.PreRoll)
                BeginPreRoll();
        }

        private void Update()
        {
            if (!Running)
                return;

            float t = Time.time - _phaseStart;
            phaseSeconds = t;
            switch (_phase)
            {
                case Phase.WarmUp:
                    if (t >= warmUpSeconds)
                        BeginPreRoll();
                    break;
                case Phase.PreRoll:
                    if (t >= preRollSeconds)
                        BeginTake();
                    break;
                case Phase.Take:
                    while (_nextStep < _steps.Length && _steps[_nextStep].At <= t)
                        Do(_steps[_nextStep++]);
                    if (t >= _length)
                        EndTake();
                    break;
            }
        }

        private void BeginPreRoll()
        {
            if (!TryGetTake(CurrentLetter(), out _steps, out _length))
            {
                Debug.LogWarning($"[TakeSequencer] Unknown take '{CurrentLetter()}'; stopping.");
                Enter(Phase.Done);
                return;
            }
            Enter(Phase.PreRoll);
            takeControls.FreshTake();
            shotDirector.Apply(_steps[0].Shot);
        }

        private void BeginTake()
        {
            Enter(Phase.Take);
            _nextStep = 0;
            TakeStarted?.Invoke(CurrentLetter());
        }

        private void EndTake()
        {
            TakeEnded?.Invoke(CurrentLetter());
            _takeIndex++;
            if (_takeIndex < takes.Length)
            {
                BeginPreRoll();
                return;
            }
            Enter(Phase.Done);
            shotDirector.Apply(1);
            Finished?.Invoke();
        }

        private void Do(TakeStep step)
        {
            switch (step.Action)
            {
                case TakeAction.Shot:
                    shotDirector.Apply(step.Shot);
                    break;
                case TakeAction.WearToolOut:
                    Send(CellTopics.PressDegrade);
                    break;
                case TakeAction.ChangeTool:
                    Send(CellTopics.PressToolChange);
                    break;
            }
        }

        private void Send(TopicPath command)
        {
            context.Bus.Publish(command, SignalValue.FromBool(true), CellContext.NowUnixMs);
        }

        private void Enter(Phase next)
        {
            _phase = next;
            _phaseStart = Time.time;
            phase = next == Phase.Take || next == Phase.PreRoll ? next + " " + CurrentLetter() : next.ToString();
        }

        private string CurrentLetter()
        {
            return _takeIndex < takes.Length ? takes[_takeIndex].ToString().ToUpperInvariant() : string.Empty;
        }

        /// <summary>The steps and length of take A, B or C.</summary>
        public static bool TryGetTake(string letter, out TakeStep[] steps, out float length)
        {
            switch (letter)
            {
                case "A": steps = TakeA; length = LengthA; return true;
                case "B": steps = TakeB; length = LengthB; return true;
                case "C": steps = TakeC; length = LengthC; return true;
                default: steps = null; length = 0f; return false;
            }
        }
    }
}
