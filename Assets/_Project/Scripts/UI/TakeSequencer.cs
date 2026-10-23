using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Capture tools: what a take does at one moment.</summary>
    public enum TakeAction
    {
        Shot,
        WearToolOut,
        ChangeTool,
        RunSuite,
        RunSuiteStopAtFailure,
        ShowCode,
        HideCode,
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
    /// Virtual commissioning takes (docs/demo-script.md, VC clip): D runs the suite against the seeded-bug program
    /// and stops at its first failure; E runs the whole suite against the fixed program. Load the matching program
    /// into the soft PLC before each (tools/plc/README.md). VC takes hand the line to the PLC, hide the CNC
    /// dashboard and show the commissioning panel; PdM takes do the opposite. VC takes are recorded in real time,
    /// because the PLC's timers count wall-clock time.
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
        // Shot 5 is the pick-station close-up.
        private static readonly TakeStep[] TakeD =
        {
            new TakeStep(0f, TakeAction.Shot, 0),
            new TakeStep(8f, TakeAction.Shot, 1),
            new TakeStep(12f, TakeAction.ShowCode),
            new TakeStep(17f, TakeAction.Shot, 5),
            new TakeStep(18f, TakeAction.RunSuiteStopAtFailure),
            new TakeStep(36f, TakeAction.Shot, 1),
        };

        private static readonly TakeStep[] TakeE =
        {
            new TakeStep(0f, TakeAction.Shot, 1),
            new TakeStep(0f, TakeAction.ShowCode),
            new TakeStep(4f, TakeAction.RunSuite),
            new TakeStep(10f, TakeAction.Shot, 5),
            new TakeStep(40f, TakeAction.Shot, 1),
            new TakeStep(100f, TakeAction.Shot, 2),
            new TakeStep(140f, TakeAction.Shot, 1),
            new TakeStep(200f, TakeAction.Shot, 5),
            new TakeStep(230f, TakeAction.Shot, 1),
        };

        private const float LengthA = 114f;
        private const float LengthB = 72f;
        private const float LengthC = 28f;
        private const float LengthD = 46f;
        private const float LengthE = 330f;

        [SerializeField] private CellContext context;
        [SerializeField] private TakeControls takeControls;
        [SerializeField] private ShotDirector shotDirector;

        [Header("Virtual commissioning takes")]
        [SerializeField] private CommissioningSuite suite;
        [SerializeField] private CommissioningPanel commissioningPanel;
        [SerializeField] private UIDocument commissioningDocument;
        [SerializeField] private UIDocument dashboardDocument;
        [Tooltip("Takes to run, in order, as letters: A, B, C (PdM), D, E (VC).")]
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
            PrepareFor(CurrentLetter());
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
                case TakeAction.RunSuite:
                    Publish(CellTopics.VcRun, 1);
                    break;
                case TakeAction.RunSuiteStopAtFailure:
                    Publish(CellTopics.VcRun, 2);
                    break;
                case TakeAction.ShowCode:
                case TakeAction.HideCode:
                    if (commissioningPanel != null)
                        commissioningPanel.ShowCode(step.Action == TakeAction.ShowCode);
                    break;
            }
        }

        private void Send(TopicPath command)
        {
            context.Bus.Publish(command, SignalValue.FromBool(true), CellContext.NowUnixMs);
        }

        private void Publish(TopicPath topic, double value)
        {
            context.Bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);
        }

        // PdM takes show the CNC dashboard; VC takes show the commissioning panel, run on the soft PLC and name
        // the program under test.
        private void PrepareFor(string letter)
        {
            bool vc = IsRealTimeTake(letter);
            SetVisible(dashboardDocument, !vc);
            SetVisible(commissioningDocument, vc);
            if (!vc)
                return;
            Publish(CellTopics.PlcSource, (int)LogicSource.Plc);
            if (suite != null)
                suite.ProgramLabel = ProgramFor(letter);
            if (commissioningPanel != null)
                commissioningPanel.ShowCode(false);
        }

        private static void SetVisible(UIDocument document, bool visible)
        {
            if (document != null && document.rootVisualElement != null)
                document.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>VC takes (D, E) run against the soft PLC, so they must be recorded in real time.</summary>
        public static bool IsRealTimeTake(string letter) => letter == "D" || letter == "E";

        /// <summary>The PLC program a VC take expects in the soft PLC; empty for PdM takes.</summary>
        public static string ProgramFor(string letter) =>
            letter == "D" ? "cell_buggy.st" : letter == "E" ? "cell.st" : string.Empty;

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

        /// <summary>The steps and length of take A, B, C, D or E.</summary>
        public static bool TryGetTake(string letter, out TakeStep[] steps, out float length)
        {
            switch (letter)
            {
                case "A": steps = TakeA; length = LengthA; return true;
                case "B": steps = TakeB; length = LengthB; return true;
                case "C": steps = TakeC; length = LengthC; return true;
                case "D": steps = TakeD; length = LengthD; return true;
                case "E": steps = TakeE; length = LengthE; return true;
                default: steps = null; length = 0f; return false;
            }
        }
    }
}
