using System;
using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Commissioning: a stimulus the suite publishes during a scenario. Fires at <see cref="At"/> seconds, or at
    /// the first tick after that where <see cref="When"/> holds, if a condition is given.
    /// </summary>
    public sealed class ScenarioAction
    {
        public ScenarioAction(double at, TopicPath topic, SignalValue value, Func<CellSnapshot, bool> when = null)
        {
            At = at;
            Topic = topic;
            Value = value;
            When = when;
        }

        public double At { get; }
        public TopicPath Topic { get; }
        public SignalValue Value { get; }
        public Func<CellSnapshot, bool> When { get; }
    }

    /// <summary>Commissioning: one named test, its stimuli and its expectations.</summary>
    public sealed class CommissioningScenario
    {
        private readonly List<ScenarioAction> _actions;
        private readonly List<ScenarioCheck> _checks;

        public CommissioningScenario(string name, string purpose, double durationSeconds, IEnumerable<ScenarioAction> actions,
            IEnumerable<ScenarioCheck> checks, bool plcOnly = false)
        {
            if (!(durationSeconds > 0d))
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Purpose = purpose ?? string.Empty;
            DurationSeconds = durationSeconds;
            PlcOnly = plcOnly;
            _actions = new List<ScenarioAction>(actions ?? Array.Empty<ScenarioAction>());
            _checks = new List<ScenarioCheck>(checks ?? Array.Empty<ScenarioCheck>());
        }

        public string Name { get; }
        public string Purpose { get; }
        public double DurationSeconds { get; }

        /// <summary>Only meaningful while the soft PLC runs the line (e.g. losing the PLC link).</summary>
        public bool PlcOnly { get; }

        public IReadOnlyList<ScenarioAction> Actions => _actions;
        public IReadOnlyList<ScenarioCheck> Checks => _checks;
    }

    /// <summary>
    /// Commissioning: plays one scenario against the cell. Feed it a snapshot every tick; it returns the stimuli
    /// that are due and grades the checks. Pure: the suite component does the publishing and the clock.
    /// </summary>
    public sealed class ScenarioRun
    {
        private readonly CommissioningScenario _scenario;
        private readonly List<ScenarioCheck> _checks = new List<ScenarioCheck>();
        private readonly bool[] _fired;

        public ScenarioRun(CommissioningScenario scenario)
        {
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            foreach (ScenarioCheck check in scenario.Checks)
                _checks.Add(check.Clone());
            _fired = new bool[scenario.Actions.Count];
        }

        public CommissioningScenario Scenario => _scenario;
        public double Elapsed { get; private set; }
        public bool IsDone { get; private set; }
        public IReadOnlyList<ScenarioCheck> Checks => _checks;

        /// <summary>Passed only if every check passed; meaningful once <see cref="IsDone"/>.</summary>
        public bool Passed
        {
            get
            {
                foreach (ScenarioCheck check in _checks)
                {
                    if (check.Outcome != CheckOutcome.Passed)
                        return false;
                }
                return true;
            }
        }

        /// <summary>Advance by <paramref name="deltaSeconds"/>; stimuli due now are added to <paramref name="due"/>.</summary>
        public void Tick(double deltaSeconds, in CellSnapshot cell, List<ScenarioAction> due)
        {
            if (due == null)
                throw new ArgumentNullException(nameof(due));
            due.Clear();
            if (IsDone)
                return;

            Elapsed += deltaSeconds;
            for (int i = 0; i < _fired.Length; i++)
            {
                ScenarioAction action = _scenario.Actions[i];
                if (_fired[i] || Elapsed < action.At || (action.When != null && !action.When(cell)))
                    continue;
                _fired[i] = true;
                due.Add(action);
            }

            foreach (ScenarioCheck check in _checks)
                check.Observe(Elapsed, cell);

            if (Elapsed >= _scenario.DurationSeconds)
            {
                foreach (ScenarioCheck check in _checks)
                    check.Finish(Elapsed);
                IsDone = true;
            }
        }

        /// <summary>The first failed check's name and detail, for a one-line report; empty if all passed.</summary>
        public string FirstFailure()
        {
            foreach (ScenarioCheck check in _checks)
            {
                if (check.Outcome == CheckOutcome.Failed)
                    return $"{check.Name}: {check.Detail}";
            }
            return string.Empty;
        }
    }
}
