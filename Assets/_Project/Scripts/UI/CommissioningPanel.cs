using System;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Commissioning: shows the acceptance suite in Commissioning.uxml. One row per scenario with its status, the
    /// logic source and program under test, a summary line and a Run button. Reads the suite's events and the
    /// logic source from the bus; it never drives the line itself.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class CommissioningPanel : MonoBehaviour
    {
        private static readonly string[] StatusClasses =
            { "vc-status--pending", "vc-status--running", "vc-status--pass", "vc-status--fail", "vc-status--na" };

        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;
        [SerializeField] private CommissioningSuite suite;

        private readonly List<Row> _rows = new List<Row>();
        private Label _source;
        private Label _summary;
        private Label _program;
        private Button _run;
        private IDisposable _sourceSubscription;
        private int _passed;
        private int _failed;

        private void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null || context == null || suite == null)
            {
                Debug.LogError("[CommissioningPanel] Assign the UIDocument (Commissioning.uxml), Context (Cell) and Suite.", this);
                enabled = false;
                return;
            }

            _source = root.Q<Label>("vc-source");
            _summary = root.Q<Label>("vc-summary");
            _program = root.Q<Label>("vc-program");
            _run = root.Q<Button>("vc-run");
            _program.text = "Program: " + suite.ProgramLabel;
            BuildRows(root.Q("vc-rows"));

            _run.clicked += suite.Run;
            suite.SuiteStarted += OnSuiteStarted;
            suite.ScenarioFinished += OnScenarioFinished;
            suite.SuiteFinished += OnSuiteFinished;
            _sourceSubscription = context.Bus.Subscribe(CellTopics.PlcSource.ToString(), m => ShowSource(m.Value.AsNumber >= 0.5d));
            ShowSource(LogicSourceRule.Read(context.Bus) == LogicSource.Plc);
        }

        private void OnDisable()
        {
            if (_run != null && suite != null)
                _run.clicked -= suite.Run;
            if (suite != null)
            {
                suite.SuiteStarted -= OnSuiteStarted;
                suite.ScenarioFinished -= OnScenarioFinished;
                suite.SuiteFinished -= OnSuiteFinished;
            }
            _sourceSubscription?.Dispose();
            _sourceSubscription = null;
        }

        private void BuildRows(VisualElement container)
        {
            container.Clear();
            _rows.Clear();
            IReadOnlyList<CommissioningScenario> scenarios = CommissioningScenarios.Default();
            for (int i = 0; i < scenarios.Count; i++)
            {
                var row = new Row(i + 1, scenarios[i].Name, scenarios[i].Purpose);
                container.Add(row.Element);
                _rows.Add(row);
            }
        }

        private void ShowSource(bool plc)
        {
            _source.text = plc ? "Soft PLC (OpenPLC)" : "Unity sequencer";
            _source.EnableInClassList("link--up", plc);
        }

        private void OnSuiteStarted(LogicSource source)
        {
            _passed = _failed = 0;
            _program.text = "Program: " + suite.ProgramLabel;
            foreach (Row row in _rows)
                row.Show(0, string.Empty);
            _summary.text = "Running…";
            _run.SetEnabled(false);
            if (_rows.Count > 0)
                _rows[0].Show(1, string.Empty);
        }

        private void OnScenarioFinished(ScenarioResult result)
        {
            int i = result.Index - 1;
            if (i < 0 || i >= _rows.Count)
                return;

            int status = result.Outcome == ScenarioOutcome.Passed ? 2
                : result.Outcome == ScenarioOutcome.NotApplicable ? 4 : 3;
            _rows[i].Show(status, result.Outcome == ScenarioOutcome.Passed ? string.Empty : result.Detail);
            if (status == 2)
                _passed++;
            else if (status == 3)
                _failed++;
            if (i + 1 < _rows.Count)
                _rows[i + 1].Show(1, string.Empty);
        }

        private void OnSuiteFinished(string reportPath)
        {
            _summary.text = _failed == 0 ? $"All {_passed} passed" : $"{_failed} failed, {_passed} passed";
            _run.SetEnabled(true);
        }

        private sealed class Row
        {
            private static readonly string[] Words = { "—", "Running", "Pass", "Fail", "n/a" };
            private readonly Label _status;
            private readonly Label _detail;
            private readonly string _purpose;

            public Row(int index, string name, string purpose)
            {
                _purpose = purpose;
                Element = new VisualElement();
                Element.AddToClassList("vc-row");
                var number = new Label(index.ToString());
                number.AddToClassList("vc-row__index");
                var text = new VisualElement();
                text.AddToClassList("vc-row__text");
                var title = new Label(name);
                title.AddToClassList("vc-row__name");
                _detail = new Label(purpose);
                _detail.AddToClassList("vc-row__detail");
                text.Add(title);
                text.Add(_detail);
                _status = new Label(Words[0]);
                _status.AddToClassList("chip");
                Element.Add(number);
                Element.Add(text);
                Element.Add(_status);
                Show(0, string.Empty);
            }

            public VisualElement Element { get; }

            public void Show(int status, string detail)
            {
                _status.text = Words[status];
                for (int c = 0; c < StatusClasses.Length; c++)
                    _status.EnableInClassList(StatusClasses[c], c == status);
                Element.EnableInClassList("vc-row--current", status == 1);
                _detail.text = string.IsNullOrEmpty(detail) ? _purpose : detail;
            }
        }
    }
}
