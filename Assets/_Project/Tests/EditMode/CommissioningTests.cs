using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class ScenarioCheckTests
    {
        private static CellSnapshot Cell(bool infeed = true, bool armBusy = false, bool gate = false,
            CellState state = CellState.Feeding, int partsOut = 0, bool link = true, double ratio = 1d)
            => new CellSnapshot(infeed, true, armBusy, false, gate, state, partsOut, link, ratio);

        [Test]
        public void Never_ToleratesAViolationShorterThanTheGrace()
        {
            ScenarioCheck check = ScenarioCheck.Never("no overlap", 0d, 10d, c => c.InfeedRun && c.ArmBusy, 0.3d);
            check.Observe(1.0d, Cell(armBusy: true));
            check.Observe(1.2d, Cell(armBusy: true));
            check.Observe(1.3d, Cell(armBusy: false));
            check.Finish(10d);
            Assert.AreEqual(CheckOutcome.Passed, check.Outcome);
        }

        [Test]
        public void Never_FailsOnASustainedViolation()
        {
            ScenarioCheck check = ScenarioCheck.Never("no overlap", 0d, 10d, c => c.InfeedRun && c.ArmBusy, 0.3d);
            for (double t = 2d; t < 3d; t += 0.1d)
                check.Observe(t, Cell(armBusy: true));
            Assert.AreEqual(CheckOutcome.Failed, check.Outcome);
            StringAssert.Contains("2.0 s", check.Detail);
        }

        [Test]
        public void Never_IgnoresViolationsOutsideItsWindow()
        {
            ScenarioCheck check = ScenarioCheck.Never("stopped while forced", 1d, 5d, c => c.InfeedRun, 0d);
            check.Observe(0.5d, Cell());
            check.Observe(0.9d, Cell());
            check.Observe(6d, Cell());
            check.Observe(7d, Cell());
            check.Finish(8d);
            Assert.AreEqual(CheckOutcome.Passed, check.Outcome);
        }

        [Test]
        public void Within_PassesWhenTheConditionArrivesInTime()
        {
            ScenarioCheck check = ScenarioCheck.Within("holds", 0d, 2d, c => c.State == CellState.Held);
            check.Observe(0.5d, Cell());
            check.Observe(1.5d, Cell(state: CellState.Held));
            Assert.AreEqual(CheckOutcome.Passed, check.Outcome);
        }

        [Test]
        public void Within_FailsWhenTheConditionIsLate()
        {
            ScenarioCheck check = ScenarioCheck.Within("holds", 0d, 2d, c => c.State == CellState.Held);
            check.Observe(1d, Cell());
            check.Observe(2.5d, Cell(state: CellState.Held));
            Assert.AreEqual(CheckOutcome.Failed, check.Outcome);
        }

        [Test]
        public void PartsOut_CountsFromTheWindowStart()
        {
            ScenarioCheck check = ScenarioCheck.PartsOut("two parts", 5d, 20d, 2);
            check.Observe(1d, Cell(partsOut: 40));
            check.Observe(5d, Cell(partsOut: 41));
            check.Observe(10d, Cell(partsOut: 42));
            Assert.AreEqual(CheckOutcome.Pending, check.Outcome);
            check.Observe(15d, Cell(partsOut: 43));
            Assert.AreEqual(CheckOutcome.Passed, check.Outcome);
        }

        [Test]
        public void Clone_StartsFresh()
        {
            ScenarioCheck check = ScenarioCheck.Within("holds", 0d, 1d, c => c.State == CellState.Held);
            check.Observe(2d, Cell());
            Assert.AreEqual(CheckOutcome.Failed, check.Outcome);
            Assert.AreEqual(CheckOutcome.Pending, check.Clone().Outcome);
        }
    }

    public sealed class ScenarioRunTests
    {
        private static readonly TopicPath Force = new TopicPath("gate1", "force");

        [Test]
        public void Actions_FireAtTheirTime_AndConditionalOnesWaitForTheCondition()
        {
            var scenario = new CommissioningScenario("s", "", 10d,
                new[]
                {
                    new ScenarioAction(1d, Force, SignalValue.FromNumber(2)),
                    new ScenarioAction(0d, Force, SignalValue.FromNumber(0), c => c.ArmHolding),
                },
                new ScenarioCheck[0]);
            var run = new ScenarioRun(scenario);
            var due = new List<ScenarioAction>();
            var idle = new CellSnapshot(true, true, false, false, false, CellState.Feeding, 0);
            var holding = new CellSnapshot(false, true, true, true, false, CellState.Transferring, 0);

            run.Tick(0.5d, idle, due);
            Assert.AreEqual(0, due.Count);
            run.Tick(0.6d, idle, due);
            Assert.AreEqual(1, due.Count);
            Assert.AreEqual(2d, due[0].Value.AsNumber);
            run.Tick(0.5d, holding, due);
            Assert.AreEqual(1, due.Count);
            Assert.AreEqual(0d, due[0].Value.AsNumber);
            run.Tick(0.5d, holding, due);
            Assert.AreEqual(0, due.Count);
        }

        [Test]
        public void Run_EndsAtItsDuration_AndGradesEveryCheck()
        {
            var scenario = new CommissioningScenario("s", "", 2d, null,
                new[]
                {
                    ScenarioCheck.Never("no stall", 0d, 2d, c => c.State == CellState.Stalled),
                    ScenarioCheck.Within("holds", 0d, 1d, c => c.State == CellState.Held),
                });
            var run = new ScenarioRun(scenario);
            var due = new List<ScenarioAction>();
            var feeding = new CellSnapshot(true, true, false, false, false, CellState.Feeding, 0);

            for (int i = 0; i < 20 && !run.IsDone; i++)
                run.Tick(0.2d, feeding, due);

            Assert.IsTrue(run.IsDone);
            Assert.IsFalse(run.Passed);
            StringAssert.StartsWith("holds:", run.FirstFailure());
        }

        [Test]
        public void TwoRunsOfOneScenario_DoNotShareCheckState()
        {
            var scenario = new CommissioningScenario("s", "", 1d, null,
                new[] { ScenarioCheck.Never("no hold", 0d, 1d, c => c.State == CellState.Held, 0d) });
            var due = new List<ScenarioAction>();

            var held = new CellSnapshot(false, false, false, false, false, CellState.Held, 0);
            var feeding = new CellSnapshot(true, true, false, false, false, CellState.Feeding, 0);
            var first = new ScenarioRun(scenario);
            first.Tick(0.5d, held, due);
            first.Tick(0.5d, held, due);
            var second = new ScenarioRun(scenario);
            second.Tick(0.5d, feeding, due);
            second.Tick(0.5d, feeding, due);

            Assert.IsFalse(first.Passed);
            Assert.IsTrue(second.Passed);
        }
    }

    public sealed class CommissioningScenariosTests
    {
        [Test]
        public void DefaultSuite_HasSixScenarios_OnlyThePlcLossOneNeedsThePlc()
        {
            IReadOnlyList<CommissioningScenario> suite = CommissioningScenarios.Default();
            Assert.AreEqual(6, suite.Count);
            CollectionAssert.AreEqual(new[] { "PLC link lost" }, suite.Where(s => s.PlcOnly).Select(s => s.Name).ToArray());
        }

        [Test]
        public void EveryScenario_ChecksBothInterlocksAndRealTime()
        {
            foreach (CommissioningScenario scenario in CommissioningScenarios.Default())
            {
                string[] names = scenario.Checks.Select(c => c.Name).ToArray();
                CollectionAssert.Contains(names, "Interlock: infeed stops for a part at the gate", scenario.Name);
                CollectionAssert.Contains(names, "Interlock: infeed stops while the arm works", scenario.Name);
                CollectionAssert.Contains(names, "Plant ran in real time", scenario.Name);
            }
        }

        [Test]
        public void EveryStimulus_IsPutBackBeforeTheScenarioEnds()
        {
            foreach (CommissioningScenario scenario in CommissioningScenarios.Default())
            {
                var last = new Dictionary<TopicPath, ScenarioAction>();
                foreach (ScenarioAction action in scenario.Actions)
                {
                    if (!last.TryGetValue(action.Topic, out ScenarioAction previous) || action.At >= previous.At)
                        last[action.Topic] = action;
                }
                foreach (ScenarioAction action in last.Values)
                {
                    Assert.AreEqual(0d, action.Value.AsNumber, $"{scenario.Name}: {action.Topic} is left set");
                    Assert.Less(action.At, scenario.DurationSeconds, $"{scenario.Name}: {action.Topic} resets too late");
                }
            }
        }

        [Test]
        public void TheSeededBug_FailsTheNormalRunOnTheArmInterlock()
        {
            // The bugged program restarts the infeed once the arm acknowledges the pick.
            CommissioningScenario normal = CommissioningScenarios.Default()[0];
            var run = new ScenarioRun(normal);
            var due = new List<ScenarioAction>();
            int parts = 0;
            for (double t = 0d; !run.IsDone; t += 0.1d)
            {
                double phase = t % 7d;
                bool armBusy = phase >= 2d && phase < 5.5d;
                bool gate = phase >= 1.9d && phase < 2.5d;
                bool infeed = !gate;                     // the bug: infeed back on while the arm is still busy
                if (phase < 0.1d && t > 1d)
                    parts++;
                var cell = new CellSnapshot(infeed, true, armBusy, armBusy, gate,
                    armBusy ? CellState.Transferring : CellState.Feeding, parts);
                run.Tick(0.1d, cell, due);
            }

            Assert.IsFalse(run.Passed);
            Assert.AreEqual(CheckOutcome.Failed,
                run.Checks.Single(c => c.Name == "Interlock: infeed stops while the arm works").Outcome);
        }
    }

    public sealed class SignalForcesTests
    {
        private static readonly TopicPath Gate = new TopicPath("gate1", "part_present");

        [Test]
        public void Unforced_FollowsThePlant_ForcedOverridesIt()
        {
            var forces = new SignalForces();
            Assert.AreEqual(SignalValue.FromBool(true), forces.Resolve(Gate, SignalValue.FromBool(true)));

            forces.Apply(Gate, ForceMode.Off);
            Assert.AreEqual(SignalValue.FromBool(false), forces.Resolve(Gate, SignalValue.FromBool(true)));
            forces.Apply(Gate, ForceMode.On);
            Assert.AreEqual(SignalValue.FromBool(true), forces.Resolve(Gate, SignalValue.FromBool(false)));

            forces.Apply(Gate, ForceMode.None);
            Assert.IsFalse(forces.IsForced(Gate));
            Assert.AreEqual(SignalValue.FromBool(false), forces.Resolve(Gate, SignalValue.FromBool(false)));
        }

        [Test]
        public void ParseMode_TreatsUnknownValuesAsNoForce()
        {
            Assert.AreEqual(ForceMode.None, SignalForces.ParseMode(0d));
            Assert.AreEqual(ForceMode.Off, SignalForces.ParseMode(1d));
            Assert.AreEqual(ForceMode.On, SignalForces.ParseMode(2d));
            Assert.AreEqual(ForceMode.None, SignalForces.ParseMode(7d));
        }
    }

    public sealed class CommissioningOutputTests
    {
        [Test]
        public void Report_HasOneRowPerCheck_AndOneForNotApplicable()
        {
            var check = ScenarioCheck.Within("holds", 0d, 1d, c => c.State == CellState.Held);
            check.Observe(2d, new CellSnapshot(true, true, false, false, false, CellState.Feeding, 0));
            var results = new[]
            {
                new ScenarioResult(1, "CNC fault", ScenarioOutcome.Failed, "holds: not within 1 s", new[] { check }),
                new ScenarioResult(6, "PLC link lost", ScenarioOutcome.NotApplicable, "needs the soft PLC", null),
            };

            string[] lines = CommissioningReport.ToCsv("Unity", "cell.st", results).TrimEnd('\n').Split('\n');

            Assert.AreEqual(CommissioningReport.Header, lines[0]);
            Assert.AreEqual("Unity,cell.st,CNC fault,holds,FAIL,not within 1 s", lines[1]);
            Assert.AreEqual("Unity,cell.st,PLC link lost,,n/a,needs the soft PLC", lines[2]);
        }

        [Test]
        public void IoList_ListsEveryPointPlusTheTwinHeartbeat()
        {
            List<PlcPoint> points = PlcIoMap.DefaultPoints().ToList();
            string[] lines = PlcIoList.ToCsv(points, 1028).TrimEnd('\n').Split('\n');

            Assert.AreEqual(PlcIoList.Header, lines[0]);
            Assert.AreEqual(points.Count + 2, lines.Length);
            StringAssert.StartsWith("pick_cmd,%QX0.2,coil,2,output (PLC → twin),BOOL,cell/arm/pick_cmd,", lines[7]);
            StringAssert.StartsWith("twin_heartbeat,%MW4,holding register,1028,", lines[lines.Length - 1]);
            StringAssert.Contains("UINT ×0.01", lines[12]);
        }
    }
}
