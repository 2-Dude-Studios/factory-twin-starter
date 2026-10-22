using System;
using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Commissioning: the cell's acceptance suite. Six scenarios, each with the stimuli a commissioning engineer
    /// would apply on site and the behaviour the line must show. Every scenario also checks the two conveyor
    /// interlocks and that the plant ran in real time, since the soft PLC's timers count wall-clock time.
    /// </summary>
    public static class CommissioningScenarios
    {
        /// <summary>How far simulated time may drift from wall-clock time before a run does not count.</summary>
        public const double RealTimeTolerance = 0.1d;

        public static IReadOnlyList<CommissioningScenario> Default()
        {
            return new[]
            {
                Scenario("Normal run", "Parts flow gate → arm → outfeed with no stall and no hold.", 60d,
                    Array.Empty<ScenarioAction>(),
                    ScenarioCheck.PartsOut("At least 6 parts out in 60 s", 0d, 60d, 6),
                    ScenarioCheck.Never("No stall", 0d, 60d, c => c.State == CellState.Stalled),
                    ScenarioCheck.Never("No hold", 0d, 60d, c => c.State == CellState.Held)),

                Scenario("Stuck gate sensor", "The gate reports a part for 25 s that is not there; the infeed must stay stopped, and the line must recover.", 50d,
                    new[]
                    {
                        Act(0d, CellTopics.GateForce, SignalValue.FromNumber((int)ForceMode.On)),
                        Act(25d, CellTopics.GateForce, SignalValue.FromNumber((int)ForceMode.None)),
                    },
                    ScenarioCheck.Never("Infeed stays stopped while forced", 1d, 25d, c => c.InfeedRun),
                    ScenarioCheck.Within("Line feeds again after release", 25d, 15d, c => c.State == CellState.Feeding && c.InfeedRun),
                    ScenarioCheck.PartsOut("Parts flow after release", 25d, 50d, 1)),

                Scenario("Part dropped", "The gripper loses its part on the way to the outfeed; the line must carry on.", 45d,
                    new[]
                    {
                        Act(0d, CellTopics.ArmDropPart, SignalValue.FromBool(true), c => c.ArmHolding),
                        Act(30d, CellTopics.ArmDropPart, SignalValue.FromBool(false)),
                    },
                    ScenarioCheck.Never("No stall", 0d, 45d, c => c.State == CellState.Stalled),
                    ScenarioCheck.Never("No hold", 0d, 45d, c => c.State == CellState.Held),
                    ScenarioCheck.PartsOut("Parts keep flowing", 0d, 45d, 3)),

                Scenario("Arm not ready", "The arm ignores pick commands; the line must stall safely and run again once the jam is cleared.", 50d,
                    new[]
                    {
                        Act(0d, CellTopics.ArmInhibit, SignalValue.FromBool(true)),
                        Act(30d, CellTopics.ArmInhibit, SignalValue.FromBool(false)),
                        Act(31d, CellTopics.GateClear, SignalValue.FromBool(true)),
                        Act(32d, CellTopics.GateClear, SignalValue.FromBool(false)),
                    },
                    ScenarioCheck.Within("Line stalls when the arm never answers", 0d, 28d, c => c.State == CellState.Stalled),
                    ScenarioCheck.Within("Line feeds again after the jam is cleared", 31d, 12d, c => c.State == CellState.Feeding && c.InfeedRun)),

                Scenario("CNC fault", "A tool-wear failure on the CNC must hold the line, and clearing it must restart it.", 30d,
                    new[]
                    {
                        Act(0d, CellTopics.PressInjectFault, SignalValue.FromNumber((int)FaultMode.ToolWear)),
                        Act(15d, CellTopics.PressInjectFault, SignalValue.FromNumber((int)FaultMode.None)),
                    },
                    ScenarioCheck.Within("Line holds for the fault", 0d, 9d, c => c.State == CellState.Held),
                    ScenarioCheck.Never("Conveyors stay stopped while held", 0d, 15d, c => c.State == CellState.Held && (c.InfeedRun || c.OutfeedRun), 0.3d),
                    ScenarioCheck.Within("Line runs again after the fault clears", 15d, 6d, c => c.Running)),

                Scenario("PLC link lost", "The twin stops hearing the PLC for 6 s; the line must hold within 1.5 s and resume when the link is back.", 20d,
                    new[]
                    {
                        Act(0d, CellTopics.PlcSimulateLoss, SignalValue.FromBool(true)),
                        Act(6d, CellTopics.PlcSimulateLoss, SignalValue.FromBool(false)),
                    },
                    plcOnly: true,
                    checks: new[]
                    {
                        ScenarioCheck.Within("Line holds without the PLC", 0d, 1.5d, c => !c.PlcLink && c.State == CellState.Held),
                        ScenarioCheck.Never("Conveyors stay stopped without the PLC", 1.5d, 6d, c => c.InfeedRun || c.OutfeedRun, 0.3d),
                        ScenarioCheck.Within("Line runs again with the PLC back", 6d, 4d, c => c.PlcLink && c.Running),
                    }),
            };
        }

        private static ScenarioAction Act(double at, TopicPath topic, SignalValue value, Func<CellSnapshot, bool> when = null)
            => new ScenarioAction(at, topic, value, when);

        private static CommissioningScenario Scenario(string name, string purpose, double duration, ScenarioAction[] actions,
            params ScenarioCheck[] checks)
            => Scenario(name, purpose, duration, actions, false, checks);

        private static CommissioningScenario Scenario(string name, string purpose, double duration, ScenarioAction[] actions,
            bool plcOnly, ScenarioCheck[] checks)
        {
            var all = new List<ScenarioCheck>(checks);
            all.AddRange(BaseChecks(duration));
            return new CommissioningScenario(name, purpose, duration, actions, all, plcOnly);
        }

        // Checks that hold in every scenario: the two conveyor interlocks and a real-time plant.
        private static IEnumerable<ScenarioCheck> BaseChecks(double duration)
        {
            yield return ScenarioCheck.Never("Interlock: infeed stops for a part at the gate", 0d, duration,
                c => c.InfeedRun && c.PartAtGate, 0.3d);
            yield return ScenarioCheck.Never("Interlock: infeed stops while the arm works", 0d, duration,
                c => c.InfeedRun && c.ArmBusy, 0.3d);
            yield return ScenarioCheck.Never("Plant ran in real time", 0d, duration,
                c => Math.Abs(c.RealtimeRatio - 1d) > RealTimeTolerance, 0d);
        }
    }
}
