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
            run.Begin("Apoorv Garg", 1_700_000_000_000L, "Akaash Garg");
            return run;
        }

        // Started, with step 1 (read the procedure) done, so Notify is due.
        private static LockoutProcedure Prepared()
        {
            LockoutProcedure run = Started();
            run.Attempt(LockoutAction.ReviewProcedure, 0.5d);
            return run;
        }

        [Test]
        public void Steps_FollowTheEnergyControlOrder_AndStartComesTwice()
        {
            LockoutAction[] order = InOrder();
            Assert.AreEqual(14, order.Length);
            Assert.AreEqual(LockoutAction.ReviewProcedure, order[0], "prepare for shutdown first, (d)(1)");
            Assert.AreEqual(LockoutAction.Notify, order[1]);
            Assert.Less(System.Array.IndexOf(order, LockoutAction.IsolatorOff), System.Array.IndexOf(order, LockoutAction.ApplyLock));
            Assert.Less(System.Array.IndexOf(order, LockoutAction.ApplyLock), System.Array.IndexOf(order, LockoutAction.ChangeTool));
            Assert.AreEqual(LockoutAction.Start, order[6], "verify the isolation before servicing");
            Assert.AreEqual(LockoutAction.Start, order[13], "restart last");
            Assert.AreEqual("1910.147(d)(6)", LockoutProcedure.Steps[6].Regulation);
        }

        [Test]
        public void Release_ChecksTheAreaBeforeTheLockComesOff_AndNotifiesAfter()
        {
            LockoutAction[] order = InOrder();
            int check = System.Array.IndexOf(order, LockoutAction.CheckArea);
            int remove = System.Array.IndexOf(order, LockoutAction.RemoveLock);
            int notify = System.Array.LastIndexOf(order, LockoutAction.Notify);
            int restore = System.Array.IndexOf(order, LockoutAction.IsolatorOn);
            Assert.Less(check, remove, "(e)(2)(i) before (e)(3)");
            Assert.Less(remove, notify, "(e)(2)(ii): notified after the lock is off");
            Assert.Less(notify, restore, "and before energy comes back");
            Assert.AreEqual("1910.147(e)(2)(ii)", LockoutProcedure.Steps[notify].Regulation);
        }

        [Test]
        public void TheAreaCheck_IsRefusedWhileSomeoneIsInTheWorkArea()
        {
            LockoutProcedure run = Started();
            LockoutAction[] order = InOrder();
            int check = System.Array.IndexOf(order, LockoutAction.CheckArea);
            for (int i = 0; i < check; i++)
                run.Attempt(order[i], i);

            Assert.IsFalse(run.Attempt(LockoutAction.CheckArea, 20d, workAreaClear: false));
            Assert.AreEqual(1, run.Errors);
            Assert.AreEqual("Area checked with someone still in the work area", run.Events.Last().Text);
            Assert.IsFalse(run.Attempt(LockoutAction.CheckArea, 21d, workAreaClear: false));
            Assert.AreEqual(1, run.Errors, "checking again before they leave counts once");
            Assert.IsTrue(run.Attempt(LockoutAction.CheckArea, 25d, workAreaClear: true));
        }

        [Test]
        public void AskingAColleagueToStandClear_IsNeitherAStepNorAMistake()
        {
            LockoutProcedure run = Prepared();
            Assert.IsFalse(LockoutProcedure.IsProcedureAction(LockoutAction.AskStandClear));
            Assert.IsFalse(run.Attempt(LockoutAction.AskStandClear, 1d));
            Assert.AreEqual(0, run.Errors);
            Assert.AreEqual(2, run.StepNumber);
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
            Assert.AreEqual(15, run.StepNumber);
            Assert.AreEqual(0, run.Errors);
            Assert.AreEqual(TrainingOutcome.Passed, run.Result);
            Assert.AreEqual(14, run.Events.Count);
        }

        [Test]
        public void TheSameMistakeOnTheSameStep_IsCountedOnce()
        {
            LockoutProcedure run = Prepared();
            run.Attempt(LockoutAction.Notify, 1d);
            for (int i = 0; i < 5; i++)
                Assert.IsFalse(run.Attempt(LockoutAction.ChangeTool, 2d + i));
            Assert.IsTrue(run.LastWasRepeat);
            Assert.AreEqual(1, run.Errors);
            Assert.AreEqual(2, run.Events.Count(e => e.Mistake || e.Step == 1));

            Assert.IsFalse(run.Attempt(LockoutAction.Inspect, 10d));
            Assert.IsFalse(run.LastWasRepeat, "a different wrong action is a new mistake");
            Assert.AreEqual(2, run.Errors);

            run.Attempt(LockoutAction.Stop, 11d);
            Assert.IsFalse(run.Attempt(LockoutAction.ChangeTool, 12d));
            Assert.IsFalse(run.LastWasRepeat, "the same wrong action on a later step counts again");
            Assert.AreEqual(3, run.Errors);
        }

        [Test]
        public void OutOfOrder_IsCountedNamedAndDoesNotMoveTheProcedureOn()
        {
            LockoutProcedure run = Prepared();
            run.Attempt(LockoutAction.Notify, 1d);
            run.Attempt(LockoutAction.Stop, 2d);

            Assert.IsFalse(run.Attempt(LockoutAction.ChangeTool, 3d));
            Assert.AreEqual(1, run.Errors);
            Assert.AreEqual(4, run.StepNumber, "still on: isolate");
            TrainingEvent mistake = run.Events.Last();
            Assert.IsTrue(mistake.Mistake);
            Assert.AreEqual(4, mistake.Step);
            Assert.AreEqual("Change tool before: Isolate the CNC at its main isolator", mistake.Text);
        }

        [Test]
        public void RestartingWithTheLockStillOn_IsAMistake_ThenTheRunCanStillFinish()
        {
            LockoutProcedure run = Started();
            LockoutAction[] order = InOrder();
            int remove = System.Array.IndexOf(order, LockoutAction.RemoveLock);
            for (int i = 0; i < remove; i++)
                run.Attempt(order[i], i);

            // "Remove your lock" is due; pressing Start now is the classic mistake.
            Assert.IsFalse(run.Attempt(LockoutAction.Start, 20d));
            Assert.AreEqual(1, run.Errors);

            for (int i = remove; i < order.Length; i++)
                Assert.IsTrue(run.Attempt(order[i], 30d + i));
            Assert.AreEqual(TrainingOutcome.PassedWithErrors, run.Result);
        }

        [Test]
        public void TheVerificationStart_IsTheStepDue_NotAMistake()
        {
            LockoutProcedure run = Started();
            LockoutAction[] order = InOrder();
            for (int i = 0; i < 6; i++)
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
            run.Begin("Second Trainee", 1_700_000_100_000L, "Akaash Garg");

            Assert.AreEqual(0, run.Errors);
            Assert.AreEqual(1, run.StepNumber);
            Assert.AreEqual(0, run.Events.Count);
            Assert.AreEqual("Second Trainee", run.Trainee);
        }

        [Test]
        public void Abort_EndsTheRunAndKeepsWhatWasDone()
        {
            LockoutProcedure run = Started();
            run.Attempt(LockoutAction.ReviewProcedure, 1d);
            run.Abort();

            Assert.IsFalse(run.Attempt(LockoutAction.Stop, 2d));
            Assert.AreEqual(1, run.Done);
            Assert.AreEqual(TrainingOutcome.Idle, run.Result);
        }

        [Test]
        public void Record_HasARowPerEventAndASummary()
        {
            LockoutProcedure run = Started();
            run.Attempt(LockoutAction.ReviewProcedure, 1.31d);
            run.Attempt(LockoutAction.IsolatorOff, 2d);

            string[] lines = TrainingRecord.ToCsv(run).TrimEnd('\n').Split('\n');

            Assert.AreEqual(TrainingRecord.Header, lines[0]);
            Assert.AreEqual(4, lines.Length);
            Assert.AreEqual("Apoorv Garg,Akaash Garg,2023-11-14 22:13:20,1.3,1,1910.147(d)(1),done,Prepare: read the CNC's energy control procedure", lines[1]);
            StringAssert.StartsWith("Apoorv Garg,Akaash Garg,2023-11-14 22:13:20,2.0,2,1910.147(c)(9),mistake,", lines[2]);
            Assert.AreEqual("Apoorv Garg,Akaash Garg,2023-11-14 22:13:20,,,,not finished,\"1 of 14 steps, 1 mistake\"", lines[3]);
        }
    }
}
