using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Commissioning: runs the acceptance suite against whichever logic source runs the line. Starts on
    /// cell/vc/run = 1 (or <see cref="Run"/>), waits for the line to run before each scenario, plays its stimuli
    /// on the bus and grades its checks. cell/vc/run = 2 stops at the first failed check, as an engineer would
    /// on site, so the failure shows within seconds. Publishes the scenario under test on cell/vc/test (1-based, 0 when idle)
    /// and each outcome on cell/vc/result, and writes a CSV report to Builds/Commissioning.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class CommissioningSuite : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("Written into the report's program column, e.g. cell.st or cell_buggy.st.")]
        [SerializeField] private string programLabel = "cell.st";
        [Tooltip("How long to wait for the line to run again before a scenario is reported as not run.")]
        [SerializeField, Min(5f)] private float settleTimeoutSeconds = 40f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private string current;
        [SerializeField] private int passed;
        [SerializeField] private int failed;

        private readonly List<ScenarioAction> _due = new List<ScenarioAction>();
        private readonly List<ScenarioResult> _results = new List<ScenarioResult>();
        private IReadOnlyList<CommissioningScenario> _scenarios;
        private IDisposable _runSubscription;
        private SignalBus _bus;

        private int _index = -1;
        private ScenarioRun _run;
        private double _settling = -1d;
        private LogicSource _source;
        private bool _stopAtFirstFailure;

        /// <summary>Raised when a run starts, with its logic source.</summary>
        public event Action<LogicSource> SuiteStarted;

        /// <summary>Raised when a scenario starts playing (after the line has settled), with its 1-based index.</summary>
        public event Action<int> ScenarioStarted;

        /// <summary>Raised as each scenario finishes (passed, failed, not applicable or not run).</summary>
        public event Action<ScenarioResult> ScenarioFinished;

        /// <summary>Raised when the run ends, with the report's path.</summary>
        public event Action<string> SuiteFinished;

        public IReadOnlyList<CommissioningScenario> Scenarios => _scenarios;
        /// <summary>The PLC program under test, as written in the report; set it when a different program is loaded.</summary>
        public string ProgramLabel
        {
            get => programLabel;
            set => programLabel = value ?? string.Empty;
        }

        /// <summary>True if the last run ended at its first failure rather than running every scenario.</summary>
        public bool StoppedEarly => _stopAtFirstFailure && failed > 0;
        public bool IsRunning => _index >= 0;

        private void Awake()
        {
            _scenarios = CommissioningScenarios.Default();
        }

        private void OnEnable()
        {
            _bus = context.Bus;
            _runSubscription = _bus.Subscribe(CellTopics.VcRun.ToString(), m =>
            {
                if (m.Value.AsNumber != 0d)
                    Run(m.Value.AsNumber >= 1.5d);
            });
        }

        private void OnDisable()
        {
            _runSubscription?.Dispose();
            _runSubscription = null;
            if (IsRunning)
                Abort();
        }

        /// <summary>Starts the full suite against the current logic source. Ignored while a run is going.</summary>
        public void Run() => Run(false);

        /// <summary>Starts the suite; with <paramref name="stopAtFirstFailure"/> it ends at the first failed check.</summary>
        public void Run(bool stopAtFirstFailure)
        {
            if (IsRunning)
                return;
            _stopAtFirstFailure = stopAtFirstFailure;
            _results.Clear();
            passed = failed = 0;
            _source = LogicSourceRule.Read(_bus);
            SuiteStarted?.Invoke(_source);
            Begin(0);
        }

        private void FixedUpdate()
        {
            if (!IsRunning)
                return;

            CellSnapshot cell = CellSnapshot.Read(_bus);
            if (_run == null)
            {
                Settle(cell);
                return;
            }

            _run.Tick(Time.fixedDeltaTime, cell, _due);
            if (_stopAtFirstFailure && _run.AnyFailed)
                _run.FinishNow();
            long now = CellContext.NowUnixMs;
            for (int i = 0; i < _due.Count; i++)
                _bus.Publish(_due[i].Topic, _due[i].Value, now);

            if (_run.IsDone)
                Finish(_run.Passed ? ScenarioOutcome.Passed : ScenarioOutcome.Failed, _run.FirstFailure(), _run.Checks);
        }

        private void Begin(int index)
        {
            _index = index;
            _run = null;
            if (_index >= _scenarios.Count)
            {
                End();
                return;
            }

            CommissioningScenario scenario = _scenarios[_index];
            current = scenario.Name;
            _bus.Publish(CellTopics.VcTest, SignalValue.FromNumber(_index + 1), CellContext.NowUnixMs);
            if (scenario.PlcOnly && _source != LogicSource.Plc)
            {
                Finish(ScenarioOutcome.NotApplicable, "needs the soft PLC", null);
                return;
            }
            _settling = 0d;
        }

        // Each scenario starts from a running line, so one scenario's aftermath cannot fail the next.
        private void Settle(in CellSnapshot cell)
        {
            if (cell.Running && !cell.PartAtGate && !cell.ArmBusy)
            {
                _run = new ScenarioRun(_scenarios[_index]);
                ScenarioStarted?.Invoke(_index + 1);
                return;
            }
            _settling += Time.fixedDeltaTime;
            if (_settling > settleTimeoutSeconds)
                Finish(ScenarioOutcome.NotRun, $"line not running after {settleTimeoutSeconds:0} s", null);
        }

        private void Finish(ScenarioOutcome outcome, string detail, IReadOnlyList<ScenarioCheck> checks)
        {
            var result = new ScenarioResult(_index + 1, _scenarios[_index].Name, outcome, detail, checks);
            _results.Add(result);
            if (outcome == ScenarioOutcome.Passed)
                passed++;
            else if (outcome == ScenarioOutcome.Failed || outcome == ScenarioOutcome.NotRun)
                failed++;

            ResetStimuli();
            _bus.Publish(CellTopics.VcResult, SignalValue.FromNumber((int)outcome), CellContext.NowUnixMs);
            Debug.Log($"[CommissioningSuite] {result.Index}. {result.Name}: {CommissioningReport.Words(outcome)}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}", this);
            ScenarioFinished?.Invoke(result);
            if (_stopAtFirstFailure && outcome == ScenarioOutcome.Failed)
                End();
            else
                Begin(_index + 1);
        }

        private void End()
        {
            _index = -1;
            current = string.Empty;
            _bus.Publish(CellTopics.VcTest, SignalValue.FromNumber(0), CellContext.NowUnixMs);
            string path = WriteReport();
            Debug.Log($"[CommissioningSuite] {_source}: {passed} passed, {failed} failed. Report: {path}", this);
            SuiteFinished?.Invoke(path);
        }

        private void Abort()
        {
            ResetStimuli();
            _index = -1;
            _run = null;
        }

        // Puts every stimulus back to neutral, so a scenario that ends early never leaves a force or a fault behind.
        private void ResetStimuli()
        {
            long now = CellContext.NowUnixMs;
            _bus.Publish(CellTopics.GateForce, SignalValue.FromNumber((int)ForceMode.None), now);
            _bus.Publish(CellTopics.ArmInhibit, SignalValue.FromBool(false), now);
            _bus.Publish(CellTopics.ArmDropPart, SignalValue.FromBool(false), now);
            _bus.Publish(CellTopics.GateClear, SignalValue.FromBool(false), now);
            _bus.Publish(CellTopics.PlcSimulateLoss, SignalValue.FromBool(false), now);
            _bus.Publish(CellTopics.PressInjectFault, SignalValue.FromNumber((int)FaultMode.None), now);
            _bus.Publish(CellTopics.SafetyZoneForce, SignalValue.FromNumber((int)ForceMode.None), now);
            // One reset press, taken on the next tick once the zone reads clear, so a stop left latched by a scenario
            // that ended early cannot keep the next one from starting.
            _bus.Publish(CellTopics.SafetyReset, SignalValue.FromBool(true), now);
            _bus.Publish(CellTopics.SafetyReset, SignalValue.FromBool(false), now);
        }

        private string WriteReport()
        {
            string folder = Application.isEditor
                ? Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Builds", "Commissioning")
                : Path.Combine(Application.persistentDataPath, "Commissioning");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, $"vc_{_source.ToString().ToLowerInvariant()}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            File.WriteAllText(file, CommissioningReport.ToCsv(_source.ToString(), programLabel, _results));
            return file;
        }
    }
}
