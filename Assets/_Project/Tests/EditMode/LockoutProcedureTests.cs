using System.Linq;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class LockoutProcedureTests
    {
        private static LockoutAction[] InOrder() => LockoutProcedure.Steps.Select(s => s.Action).ToArray();

        private static LockoutProcedure Started()
        {
            var run = new LockoutProcedure();
            run.Begin("Demo Trainee", 1_700_000_000_000L);
            return run;
        }

        [Test]
        public void Steps_FollowTheEnergyControlOrder_AndStartComesTwice()
        {
            LockoutAction[] order = InOrder();
            Assert.AreEqual(12, order.Length);
            Assert.AreEqual(LockoutAction.Notify, order[0]);
            Assert.Less(System.Array.IndexOf(order, LockoutAction.IsolatorOff), System.Array.IndexOf(order, LockoutAction.ApplyLock));
            Assert.Less(System.Array.IndexOf(order, LockoutAction.ApplyLock), System.Array.IndexOf(order, LockoutAction.ChangeTool));
            Assert.AreEqual(LockoutAction.Start, order[5], "verify the isolation before servicing");
            Assert.AreEqual(LockoutAction.Start, order[11], "restart last");
            Assert.AreEqual("1910.147(d)(6)", LockoutProcedure.Steps[5].Regulation);
        }

        [Test]
        public void BeforeARun_NothingCounts()
        {
            var run = new LockoutProcedure();

            Assert.IsFalse(run.Attempt(LockoutAction.Notify, 0d));
            Assert.AreEqual(0, run.StepNumber);
            Assert.AreEqual(TrainingOutcome.Idle, run.Result);
            Assert.IsNull(run.Expected);
        }

        [Test]
        public void InOrder_PassesWithNoMistakes()
        {
            LockoutProcedure run = Started();
            double t = 0d;
            foreach (LockoutAction action in InOrder())
                Assert.IsTrue(run.Attempt(action, t += 5d), action.ToString());

            Assert.IsTrue(run.IsComplete);
            Assert.IsFalse(run.IsRunning);
            Assert.AreEqual(13, run.StepNumber);
            Assert.AreEqual(0, run.Errors);
            Assert.AreEqual(TrainingOutcome.Passed, run.Result);
            Assert.AreEqual(12, run.Events.Count);
        }

        [Test]
        public void OutOfOrder_IsCountedNamedAndDoesNotMoveTheProcedureOn()
        {
            LockoutProcedure run = Started();
            run.Attempt(LockoutAction.Notify, 1d);
            run.Attempt(LockoutAction.Stop, 2d);

            Assert.IsFalse(run.Attempt(LockoutAction.ChangeTool, 3d));
            Assert.AreEqual(1, run.Errors);
            Assert.AreEqual(3, run.StepNumber, "still on: isolate");
            TrainingEvent mistake = run.Events.Last();
            Assert.IsTrue(mistake.Mistake);
            Assert.AreEqual(3, mistake.Step);
            Assert.AreEqual("Change tool before: Isolate the CNC at its main isolator", mistake.Text);
        }

        [Test]
        public void RestartingWithTheLockStillOn_IsAMistake_ThenTheRunCanStillFinish()
        {
            LockoutProcedure run = Started();
            LockoutAction[] order = InOrder();
            for (int i = 0; i < 9; i++)
                run.Attempt(order[i], i);

            // Step 10 is "remove your lock"; pressing Start now is the classic mistake.
            Assert.IsFalse(run.Attempt(LockoutAction.Start, 20d));
            Assert.AreEqual(1, run.Errors);

            for (int i = 9; i < order.Length; i++)
                Assert.IsTrue(run.Attempt(order[i], 30d + i));
            Assert.AreEqual(TrainingOutcome.PassedWithErrors, run.Result);
        }

        [Test]
        public void TheVerificationStart_IsTheStepDue_NotAMistake()
        {
            LockoutProcedure run = Started();
            LockoutAction[] order = InOrder();
            for (int i = 0; i < 5; i++)
                run.Attempt(order[i], i);

            Assert.AreEqual(LockoutAction.Start, run.Expected.Value.Action);
            Assert.IsTrue(run.Attempt(LockoutAction.Start, 6d));
            Assert.AreEqual(0, run.Errors);
        }

        [Test]
        public void OnlyStart_AlwaysReachesThePlant()
        {
            foreach (LockoutAction action in System.Enum.GetValues(typeof(LockoutAction)).Cast<LockoutAction>())
                Assert.AreEqual(action == LockoutAction.Start, LockoutProcedure.AlwaysReachesThePlant(action), action.ToString());
        }

        [Test]
        public void Begin_StartsAFreshRun()
        {
            LockoutProcedure run = Started();
            run.Attempt(LockoutAction.Stop, 1d);
            run.Begin("Second Trainee", 1_700_000_100_000L);

            Assert.AreEqual(0, run.Errors);
            Assert.AreEqual(1, run.StepNumber);
            Assert.AreEqual(0, run.Events.Count);
            Assert.AreEqual("Second Trainee", run.Trainee);
        }

        [Test]
        public void Abort_EndsTheRunAndKeepsWhatWasDone()
        {
            LockoutProcedure run = Started();
            run.Attempt(LockoutAction.Notify, 1d);
            run.Abort();

            Assert.IsFalse(run.Attempt(LockoutAction.Stop, 2d));
            Assert.AreEqual(1, run.Done);
            Assert.AreEqual(TrainingOutcome.Idle, run.Result);
        }

        [Test]
        public void Record_HasARowPerEventAndASummary()
        {
            LockoutProcedure run = Started();
            run.Attempt(LockoutAction.Notify, 1.31d);
            run.Attempt(LockoutAction.IsolatorOff, 2d);

            string[] lines = TrainingRecord.ToCsv(run).TrimEnd('\n').Split('\n');

            Assert.AreEqual(TrainingRecord.Header, lines[0]);
            Assert.AreEqual(4, lines.Length);
            Assert.AreEqual("Demo Trainee,2023-11-14 22:13:20,1.3,1,1910.147(c)(9),done,Notify affected employees", lines[1]);
            StringAssert.StartsWith("Demo Trainee,2023-11-14 22:13:20,2.0,2,1910.147(d)(2),mistake,", lines[2]);
            Assert.AreEqual("Demo Trainee,2023-11-14 22:13:20,,,,not finished,\"1 of 12 steps, 1 mistake\"", lines[3]);
        }
    }
}
