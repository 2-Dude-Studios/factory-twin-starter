using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Commissioning: decides when the next scenario may start. The line must be running with no part at the gate
    /// and the arm free, and simulated time must have kept step with wall-clock time for <see cref="SteadySeconds"/>
    /// without a break. The ratio is measured over a second and published once a second, so a hitch as the suite
    /// starts shows up to a second later; two clean seconds keep it out of the first scenario's real-time check.
    /// </summary>
    public sealed class SettleGate
    {
        public const double SteadySeconds = 2d;

        private double _steady;

        /// <summary>Seconds since <see cref="Reset"/>, for the settle timeout.</summary>
        public double Waited { get; private set; }

        /// <summary>True if the last tick saw the line ready but the plant not yet steadily in real time.</summary>
        public bool WaitingForRealTime { get; private set; }

        public void Reset()
        {
            _steady = 0d;
            Waited = 0d;
            WaitingForRealTime = false;
        }

        /// <summary>Advance by <paramref name="deltaSeconds"/>; true once the scenario may start.</summary>
        public bool Tick(double deltaSeconds, in CellSnapshot cell)
        {
            Waited += deltaSeconds;
            _steady = Math.Abs(cell.RealtimeRatio - 1d) <= CommissioningScenarios.RealTimeTolerance ? _steady + deltaSeconds : 0d;
            bool lineReady = cell.Running && !cell.PartAtGate && !cell.ArmBusy;
            // A hair under, so a run of fixed steps that should add up to SteadySeconds is not one tick late.
            bool steady = _steady >= SteadySeconds - 1e-6;
            WaitingForRealTime = lineReady && !steady;
            return lineReady && steady;
        }
    }
}
