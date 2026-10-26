using System;
using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Commissioning: the cell's acceptance suite. Ten scenarios (six from M8, two for the safety zone from M10, one for
    /// the lockout interlock from M11, one with a simulated worker on the real zone sensors from M12), each with the
    /// stimuli a commissioning engineer
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

                // Safety zone (M10). The force stands in for a person; the arm's stop is the safety zone's own
                // function, the line's stop is the logic source's.
                Scenario("Person in the robot zone", "A person steps into the protective zone while the arm works; the arm must stop on the next physics tick, the line must stop, and a reset from outside must restart it.", 35d,
                    new[]
                    {
                        Act(2d, CellTopics.SafetyZoneForce, SignalValue.FromNumber((int)ForceMode.On), c => c.ArmBusy),
                        Act(12d, CellTopics.SafetyZoneForce, SignalValue.FromNumber((int)ForceMode.None)),
                        Act(16d, CellTopics.SafetyReset, SignalValue.FromBool(true)),
                        Act(16.5d, CellTopics.SafetyReset, SignalValue.FromBool(false)),
                    },
                    ScenarioCheck.Within("Protective stop on entry", 2d, 6d, c => c.ProtectiveStop),
                    ScenarioCheck.Never("Arm stops within one physics tick", 0d, 35d, c => c.ZoneOccupied && c.ArmSpeedScale > 0d, OnePhysicsTick),
                    ScenarioCheck.Never("Conveyors stop while the person is inside", 0d, 35d, c => c.ZoneOccupied && (c.InfeedRun || c.OutfeedRun), 0.1d),
                    ScenarioCheck.Within("Line runs again after the reset", 16d, 6d, c => c.Running && c.ArmSpeedScale >= 1d),
                    ScenarioCheck.PartsOut("Parts flow after the reset", 16d, 35d, 1)),

                Scenario("Zone left without a reset", "The person leaves the protective zone; nothing may restart until a reset is pressed from outside, and a reset pressed while they are still inside is refused.", 30d,
                    new[]
                    {
                        Act(2d, CellTopics.SafetyZoneForce, SignalValue.FromNumber((int)ForceMode.On)),
                        Act(4d, CellTopics.SafetyReset, SignalValue.FromBool(true)),
                        Act(4.5d, CellTopics.SafetyReset, SignalValue.FromBool(false)),
                        Act(6d, CellTopics.SafetyZoneForce, SignalValue.FromNumber((int)ForceMode.None)),
                        Act(20d, CellTopics.SafetyReset, SignalValue.FromBool(true)),
                        Act(20.5d, CellTopics.SafetyReset, SignalValue.FromBool(false)),
                    },
                    ScenarioCheck.Never("Reset refused while the person is inside", 2.5d, 6d, c => !c.ProtectiveStop, 0d),
                    ScenarioCheck.Never("No restart without a reset", 6.5d, 20d, c => c.Running || c.InfeedRun || c.OutfeedRun || c.ArmSpeedScale > 0d, 0.1d),
                    ScenarioCheck.Within("Line runs again after the reset", 20d, 5d, c => c.Running && c.ArmSpeedScale >= 1d)),

                // Lockout interlock (M11): the logic source must refuse a start while the CNC is locked out.
                Scenario("Start with the lock on is refused", "The line is stopped and the CNC isolated and locked out; two starts must be refused and counted, and a start once the lock is off must run the line.", 30d,
                    new[]
                    {
                        Act(0d, CellTopics.LineStop, SignalValue.FromBool(true)),
                        Act(0.5d, CellTopics.LineStop, SignalValue.FromBool(false)),
                        Act(6d, CellTopics.PressIsolatorCommand, SignalValue.FromNumber(1)),
                        Act(6.5d, CellTopics.PressLockCommand, SignalValue.FromNumber(1)),
                        Act(8d, CellTopics.LineStart, SignalValue.FromBool(true)),
                        Act(8.5d, CellTopics.LineStart, SignalValue.FromBool(false)),
                        Act(10d, CellTopics.LineStart, SignalValue.FromBool(true)),
                        Act(10.5d, CellTopics.LineStart, SignalValue.FromBool(false)),
                        Act(14d, CellTopics.PressLockCommand, SignalValue.FromNumber(0)),
                        Act(14.5d, CellTopics.PressIsolatorCommand, SignalValue.FromNumber(0)),
                        Act(16d, CellTopics.LineStart, SignalValue.FromBool(true)),
                        Act(16.5d, CellTopics.LineStart, SignalValue.FromBool(false)),
                    },
                    ScenarioCheck.Within("Line stops on the stop command", 0d, 7d, c => c.State == CellState.Stopped),
                    ScenarioCheck.Never("Nothing moves while locked out", 7.5d, 14d, c => c.Running || c.InfeedRun || c.OutfeedRun, 0.1d),
                    ScenarioCheck.Rises("Both starts refused and counted", 7.5d, 13d, c => c.StartsRefused, 2, "refused"),
                    ScenarioCheck.Within("Line runs again once the lock is off", 16d, 5d, c => c.Running && !c.Lockout)),

                // Safety zone on its real sensors (M12): no force; the zone walker, a simulated worker on the Trainee
                // layer, walks in and the zone sensors see them as they would the trainee.
                Scenario("Worker walks into the robot zone", "A simulated worker walks into the warning zone, where the arm must slow, then into the protective zone, where the arm and the line must stop; a reset pressed while they are inside is refused, and one pressed after they walk out restarts the line. The real zone sensors, nothing forced.", 50d,
                    new[]
                    {
                        Act(1d, CellTopics.SafetyWorkerWalk, SignalValue.FromNumber((int)WorkerWalk.WarningZone), c => c.ArmBusy),
                        Act(9d, CellTopics.SafetyWorkerWalk, SignalValue.FromNumber((int)WorkerWalk.ProtectiveZone)),
                        Act(13d, CellTopics.SafetyReset, SignalValue.FromBool(true)),
                        Act(13.5d, CellTopics.SafetyReset, SignalValue.FromBool(false)),
                        Act(17d, CellTopics.SafetyWorkerWalk, SignalValue.FromNumber((int)WorkerWalk.Home)),
                        Act(27d, CellTopics.SafetyReset, SignalValue.FromBool(true)),
                        Act(27.5d, CellTopics.SafetyReset, SignalValue.FromBool(false)),
                    },
                    ScenarioCheck.Within("Arm slows in the warning zone", 1d, 7d, c => c.ArmSpeedScale > 0d && c.ArmSpeedScale < 1d),
                    ScenarioCheck.Never("No stop in the warning zone", 1d, 9d, c => c.ProtectiveStop, 0d),
                    ScenarioCheck.Within("Protective stop when the worker steps in", 9d, 3d, c => c.ProtectiveStop && c.ZoneOccupied),
                    ScenarioCheck.Never("Arm stops within one physics tick", 0d, 50d, c => c.ZoneOccupied && c.ArmSpeedScale > 0d, OnePhysicsTick),
                    ScenarioCheck.Never("Conveyors stop while the worker is inside", 0d, 50d, c => c.ZoneOccupied && (c.InfeedRun || c.OutfeedRun), 0.1d),
                    ScenarioCheck.Never("Reset refused while the worker is inside", 13.5d, 17d, c => !c.ProtectiveStop, 0d),
                    ScenarioCheck.Never("No restart without a reset", 18.5d, 27d, c => c.Running || c.InfeedRun || c.OutfeedRun || c.ArmSpeedScale > 0d, 0.1d),
                    ScenarioCheck.Within("Line runs again after the reset", 27d, 6d, c => c.Running && c.ArmSpeedScale >= 1d),
                    ScenarioCheck.PartsOut("Parts flow after the reset", 27d, 50d, 1)),
            };
        }

        // One fixed timestep (0.02 s), and a little: a stop seen on the tick after the sensor still passes.
        private const double OnePhysicsTick = 0.021d;

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
