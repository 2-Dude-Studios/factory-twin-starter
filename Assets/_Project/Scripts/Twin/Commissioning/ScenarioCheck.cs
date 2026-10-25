using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Commissioning: how one check ended.</summary>
    public enum CheckOutcome
    {
        Pending = 0,
        Passed = 1,
        Failed = 2,
    }

    /// <summary>
    /// Commissioning: one expectation inside a scenario, observed tick by tick against the cell's signals.
    /// Times are seconds from the scenario's start.
    /// </summary>
    public abstract class ScenarioCheck
    {
        protected ScenarioCheck(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }
        public CheckOutcome Outcome { get; protected set; }

        /// <summary>What went wrong, in words an engineer reads in the report; empty while passing.</summary>
        public string Detail { get; protected set; } = string.Empty;

        public abstract void Observe(double t, in CellSnapshot cell);

        /// <summary>The scenario ended at <paramref name="t"/>; settle anything still pending.</summary>
        public abstract void Finish(double t);

        /// <summary>A fresh copy for another run, so one scenario definition can run against both logic sources.</summary>
        public abstract ScenarioCheck Clone();

        /// <summary>Fails if <paramref name="violation"/> holds for longer than <paramref name="graceSeconds"/> inside [from, to].</summary>
        public static ScenarioCheck Never(string name, double from, double to, Func<CellSnapshot, bool> violation, double graceSeconds = 0.2d)
            => new NeverCheck(name, from, to, violation, graceSeconds);

        /// <summary>Passes once <paramref name="condition"/> holds at some instant inside [from, from + within].</summary>
        public static ScenarioCheck Within(string name, double from, double within, Func<CellSnapshot, bool> condition)
            => new WithinCheck(name, from, within, condition);

        /// <summary>Passes if at least <paramref name="count"/> parts leave the cell inside [from, to].</summary>
        public static ScenarioCheck PartsOut(string name, double from, double to, int count)
            => new RisesCheck(name, from, to, c => c.PartsOut, count, "parts");

        /// <summary>
        /// Passes if the counter <paramref name="counter"/> reads goes up by at least <paramref name="count"/> inside
        /// [from, to], measured from its value at <paramref name="from"/> (counters such as the PLC's carry over).
        /// </summary>
        public static ScenarioCheck Rises(string name, double from, double to, Func<CellSnapshot, int> counter, int count, string unit)
            => new RisesCheck(name, from, to, counter, count, unit);

        private sealed class NeverCheck : ScenarioCheck
        {
            private readonly double _from, _to, _grace;
            private readonly Func<CellSnapshot, bool> _violation;
            private double _since = double.NaN;

            public NeverCheck(string name, double from, double to, Func<CellSnapshot, bool> violation, double grace) : base(name)
            {
                _from = from;
                _to = to;
                _violation = violation ?? throw new ArgumentNullException(nameof(violation));
                _grace = grace;
            }

            public override void Observe(double t, in CellSnapshot cell)
            {
                if (Outcome != CheckOutcome.Pending || t < _from || t > _to)
                    return;
                if (!_violation(cell))
                {
                    _since = double.NaN;
                    return;
                }
                if (double.IsNaN(_since))
                    _since = t;
                if (t - _since > _grace)
                {
                    Outcome = CheckOutcome.Failed;
                    Detail = $"violated from {_since:0.0} s";
                }
            }

            public override void Finish(double t)
            {
                if (Outcome == CheckOutcome.Pending)
                    Outcome = CheckOutcome.Passed;
            }

            public override ScenarioCheck Clone() => new NeverCheck(Name, _from, _to, _violation, _grace);
        }

        private sealed class WithinCheck : ScenarioCheck
        {
            private readonly double _from, _within;
            private readonly Func<CellSnapshot, bool> _condition;

            public WithinCheck(string name, double from, double within, Func<CellSnapshot, bool> condition) : base(name)
            {
                _from = from;
                _within = within;
                _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            }

            public override void Observe(double t, in CellSnapshot cell)
            {
                if (Outcome != CheckOutcome.Pending || t < _from)
                    return;
                if (t > _from + _within)
                {
                    Fail();
                    return;
                }
                if (_condition(cell))
                {
                    Outcome = CheckOutcome.Passed;
                    Detail = $"after {t - _from:0.0} s";
                }
            }

            public override void Finish(double t)
            {
                if (Outcome == CheckOutcome.Pending)
                    Fail();
            }

            private void Fail()
            {
                Outcome = CheckOutcome.Failed;
                Detail = $"not within {_within:0.#} s";
            }

            public override ScenarioCheck Clone() => new WithinCheck(Name, _from, _within, _condition);
        }

        private sealed class RisesCheck : ScenarioCheck
        {
            private readonly double _from, _to;
            private readonly Func<CellSnapshot, int> _counter;
            private readonly int _count;
            private readonly string _unit;
            private int _start = int.MinValue;
            private int _seen;

            public RisesCheck(string name, double from, double to, Func<CellSnapshot, int> counter, int count, string unit) : base(name)
            {
                _from = from;
                _to = to;
                _counter = counter ?? throw new ArgumentNullException(nameof(counter));
                _count = count;
                _unit = unit ?? string.Empty;
            }

            public override void Observe(double t, in CellSnapshot cell)
            {
                if (Outcome != CheckOutcome.Pending || t < _from)
                    return;
                int value = _counter(cell);
                if (_start == int.MinValue)
                    _start = value;
                if (t <= _to)
                    _seen = Math.Max(_seen, value - _start);
                if (_seen >= _count)
                {
                    Outcome = CheckOutcome.Passed;
                    Detail = $"{_seen} {_unit}";
                }
            }

            public override void Finish(double t)
            {
                if (Outcome != CheckOutcome.Pending)
                    return;
                Outcome = CheckOutcome.Failed;
                Detail = $"{_seen} of {_count} {_unit}";
            }

            public override ScenarioCheck Clone() => new RisesCheck(Name, _from, _to, _counter, _count, _unit);
        }
    }
}
