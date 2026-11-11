using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class RobotCellDrillTests
    {
        // The trainee walks into the protective zone (through the warning zone) and the zone latches its stop.
        private static void Enter(RobotCellDrill drill, double t)
        {
            drill.ZoneWarning(true, t);
            drill.ProtectiveZone(true, t + 0.5);
            drill.ProtectiveStop(true, t + 0.5);
        }

        private static void Leave(RobotCellDrill drill, double t)
        {
            drill.ProtectiveZone(false, t);
            drill.ZoneWarning(false, t + 0.5);
        }

        private static void Reset(RobotCellDrill drill, double t)
        {
            drill.ResetPressed(t);
            drill.ProtectiveStop(false, t);
        }

        // Stops the line, goes in, waits at the gate, comes out, resets and restarts; ends at t0 + 30.
        private static void ClearTheJam(RobotCellDrill drill, double t0)
        {
            drill.LineStopPressed(t0);
            Enter(drill, t0 + 5);
            for (double t = t0 + 6; t <= t0 + 10; t += 0.5)
                drill.Tick(t);
            Leave(drill, t0 + 15);
            Reset(drill, t0 + 20);
            drill.LineStartPressed(t0 + 30);
        }

        [Test]
        public void LevelOne_WalkBothZonesAndResetFromOutside_ThreeStars()
        {
            var drill = new RobotCellDrill();
            drill.Start(1, 0d);
            Assert.AreEqual("Step into the warning zone: the arm slows", drill.Prompt);
            drill.ZoneWarning(true, 5d);
            drill.ZoneWarning(false, 10d);
            Assert.AreEqual("Step into the protective zone: the arm stops and the line holds", drill.Prompt);
            Enter(drill, 15d);
            Leave(drill, 20d);
            Assert.IsTrue(drill.Running, "leaving does not finish it: the reset does");
            Reset(drill, 30d);

            Assert.IsFalse(drill.Running);
            Assert.AreEqual(DrillResult.Passed, drill.Score.Result);
            Assert.AreEqual(3, drill.Score.Stars);
            CollectionAssert.AreEqual(new[] { RobotCellDrill.BadgeZoneAware }, drill.Badges());
            Assert.AreEqual(string.Empty, drill.Prompt);
        }

        [Test]
        public void LevelOne_ResetFromInside_IsRefusedAndAMistake()
        {
            var drill = new RobotCellDrill();
            drill.Start(1, 0d);
            drill.ZoneWarning(true, 5d);
            drill.ZoneWarning(false, 10d);
            Enter(drill, 15d);
            drill.ResetPressed(17d);
            Assert.AreEqual(1, drill.Score.Mistakes);
            Assert.IsFalse(drill.Score.IsDone(RobotCellDrill.StepReset));
            Leave(drill, 20d);
            Reset(drill, 30d);
            Assert.AreEqual(DrillResult.Passed, drill.Score.Result);
            Assert.AreEqual(1, drill.Score.Stars);
            CollectionAssert.IsEmpty(drill.Badges());
        }

        [Test]
        public void LevelOne_LeavingTheWarningZoneOnALatchedStop_DoesNotCountAsFullSpeed()
        {
            var drill = new RobotCellDrill();
            drill.Start(1, 0d);
            Enter(drill, 5d);
            Leave(drill, 10d);
            Assert.IsTrue(drill.Score.IsDone(RobotCellDrill.StepWarnIn));
            Assert.IsFalse(drill.Score.IsDone(RobotCellDrill.StepWarnOut), "the arm is still stopped");
            Reset(drill, 15d);
            drill.ZoneWarning(true, 20d);
            drill.ZoneWarning(false, 25d);
            Assert.AreEqual(DrillResult.Passed, drill.Score.Result);
        }

        [Test]
        public void LevelTwo_StopEnterExitResetRestart()
        {
            var drill = new RobotCellDrill();
            drill.Start(2, 0d);
            Assert.AreEqual(5, drill.Score.StepsRequired);
            drill.LineStopPressed(5d);
            Enter(drill, 10d);
            Leave(drill, 20d);
            drill.LineStartPressed(22d);
            Assert.AreEqual(1, drill.Score.Mistakes, "start before the reset");
            Reset(drill, 25d);
            drill.LineStartPressed(30d);
            Assert.AreEqual(DrillResult.Passed, drill.Score.Result);
            Assert.AreEqual(1, drill.Score.Stars);
            CollectionAssert.AreEqual(new[] { RobotCellDrill.BadgeResetRight }, drill.Badges());
        }

        [Test]
        public void LevelTwo_EnteringWhileTheArmMoves_IsAMinorMistake_AndDoesNotCountAsEntering()
        {
            var drill = new RobotCellDrill();
            drill.Start(2, 0d);
            Enter(drill, 5d);
            Assert.AreEqual(1, drill.Score.Mistakes);
            Assert.AreEqual(0, drill.Score.Criticals);
            Assert.IsFalse(drill.Score.IsDone(RobotCellDrill.StepEnter));
            Leave(drill, 10d);
            Reset(drill, 12d);
            drill.LineStopPressed(15d);
            Enter(drill, 20d);
            Leave(drill, 25d);
            Reset(drill, 30d);
            drill.LineStartPressed(35d);
            Assert.AreEqual(DrillResult.Passed, drill.Score.Result);
            Assert.AreEqual(1, drill.Score.Stars);
        }

        [Test]
        public void LevelThree_TheGateClearsAfterTheDwell_AndTheColleagueSetsOffAfterTheRestart()
        {
            var drill = new RobotCellDrill();
            drill.Start(3, 0d);
            Assert.IsTrue(drill.HasJam && drill.HasWalker);
            Assert.AreEqual(8, drill.Score.StepsRequired);
            drill.LineStopPressed(5d);
            Enter(drill, 10d);
            Assert.IsFalse(drill.TakeClearGate());
            drill.Tick(11d);
            drill.Tick(12.5d);
            Assert.Greater(drill.ClearSecondsLeft, 0d);
            Assert.IsFalse(drill.Score.IsDone(RobotCellDrill.StepClear));
            drill.Tick(14.1d);
            Assert.IsTrue(drill.Score.IsDone(RobotCellDrill.StepClear));
            Assert.IsTrue(drill.TakeClearGate(), "the scene clears the gate once");
            Assert.IsFalse(drill.TakeClearGate());
            Leave(drill, 20d);
            Reset(drill, 25d);
            drill.LineStartPressed(30d);
            Assert.IsTrue(drill.Running, "the colleague is still to come");

            Assert.AreEqual(-1, drill.TakeWalkerCommand());
            drill.Tick(31d);
            Assert.AreEqual(-1, drill.TakeWalkerCommand(), "two seconds after the restart");
            drill.Tick(32.1d);
            Assert.AreEqual(2, drill.TakeWalkerCommand(), "into the cell");
            drill.ZoneWarning(true, 36d);
            drill.LineStopPressed(37d);
            Assert.AreEqual(0, drill.TakeWalkerCommand(), "sent home once the line is stopped");
            drill.LineStartPressed(38d);
            Assert.AreEqual(1, drill.Score.Mistakes, "the colleague is still in the warning zone");
            Assert.IsTrue(drill.Running);
        }

        [Test]
        public void LevelThree_CleanRun_ThreeStarsAndBothBadges()
        {
            var drill = new RobotCellDrill();
            drill.Start(3, 0d);
            ClearTheJam(drill, 0d);
            drill.Tick(33d);
            Assert.AreEqual(2, drill.TakeWalkerCommand());
            drill.ZoneWarning(true, 36d);
            drill.LineStopPressed(37d);
            drill.ZoneWarning(false, 42d);
            drill.LineStartPressed(45d);
            Assert.AreEqual(DrillResult.Passed, drill.Score.Result);
            Assert.AreEqual(3, drill.Score.Stars);
            CollectionAssert.AreEquivalent(new[] { RobotCellDrill.BadgeCleanClear, RobotCellDrill.BadgeEagleEye }, drill.Badges());
        }

        [Test]
        public void LevelThree_EnteringWhileTheArmMoves_FailsTheLevel()
        {
            var drill = new RobotCellDrill();
            drill.Start(3, 0d);
            Enter(drill, 5d);
            Assert.AreEqual(1, drill.Score.Criticals);
            Leave(drill, 8d);
            Reset(drill, 9d);
            ClearTheJam(drill, 10d);
            drill.Tick(43d);
            drill.TakeWalkerCommand();
            drill.ZoneWarning(true, 46d);
            drill.LineStopPressed(47d);
            drill.ZoneWarning(false, 52d);
            drill.LineStartPressed(55d);
            Assert.IsFalse(drill.Running);
            Assert.AreEqual(DrillResult.Failed, drill.Score.Result, "whatever the points");
            Assert.AreEqual(0, drill.Score.Stars);
            Assert.AreEqual("Critical mistake: " + RobotCellDrill.EnteredMoving, drill.Score.FailReason());
            CollectionAssert.DoesNotContain(drill.Badges(), RobotCellDrill.BadgeCleanClear);
        }

        [Test]
        public void LevelThree_ColleagueReachesTheArm_IsAMistake_AndTheZoneMustBeResetBeforeRestart()
        {
            var drill = new RobotCellDrill();
            drill.Start(3, 0d);
            ClearTheJam(drill, 0d);
            drill.Tick(33d);
            Assert.AreEqual(2, drill.TakeWalkerCommand());
            Enter(drill, 36d); // the colleague, all the way in
            Assert.AreEqual(1, drill.Score.Mistakes);
            drill.ResetPressed(37d);
            Assert.AreEqual(2, drill.Score.Mistakes, "reset with someone in the zone");
            drill.LineStopPressed(38d);
            Assert.AreEqual(0, drill.TakeWalkerCommand());
            Leave(drill, 42d);
            drill.LineStartPressed(44d);
            Assert.AreEqual(3, drill.Score.Mistakes, "start before the reset");
            Reset(drill, 46d);
            drill.LineStartPressed(48d);
            Assert.AreEqual(DrillResult.Passed, drill.Score.Result);
            Assert.AreEqual(1, drill.Score.Stars);
            CollectionAssert.AreEqual(new[] { RobotCellDrill.BadgeCleanClear }, drill.Badges(), "no Eagle eye");
        }

        [Test]
        public void Assessment_PicksItsTasksFromTheSeed_GivesNoHints_AndHasItsOwnBadge()
        {
            RobotCellDrill.AssessmentTasks(0, out bool jam, out bool walker);
            Assert.IsTrue(jam && !walker);
            RobotCellDrill.AssessmentTasks(1, out jam, out walker);
            Assert.IsTrue(!jam && walker);
            RobotCellDrill.AssessmentTasks(2, out jam, out walker);
            Assert.IsTrue(jam && walker);
            RobotCellDrill.AssessmentTasks(-1, out jam, out walker);
            Assert.IsTrue(jam && walker);

            var drill = new RobotCellDrill();
            drill.Start(4, 0d, seed: 0);
            Assert.IsFalse(drill.Guided);
            Assert.AreEqual(string.Empty, drill.Prompt);
            Assert.AreEqual(6, drill.Score.StepsRequired);
            ClearTheJam(drill, 0d);
            Assert.AreEqual(DrillResult.Passed, drill.Score.Result);
            Assert.AreEqual(3, drill.Score.Stars);
            CollectionAssert.AreEquivalent(new[] { RobotCellDrill.BadgeCleanClear, RobotCellDrill.BadgeCellKeeper }, drill.Badges());

            // The walker alone: the colleague sets off two seconds in.
            drill.Start(4, 100d, seed: 1);
            Assert.AreEqual(2, drill.Score.StepsRequired);
            CollectionAssert.IsEmpty(drill.Badges(), "a new run starts clean");
            drill.Tick(102.1d);
            Assert.AreEqual(2, drill.TakeWalkerCommand());
            drill.ZoneWarning(true, 105d);
            drill.LineStopPressed(106d);
            drill.ZoneWarning(false, 110d);
            drill.LineStartPressed(112d);
            Assert.AreEqual(DrillResult.Passed, drill.Score.Result);
            CollectionAssert.AreEquivalent(new[] { RobotCellDrill.BadgeEagleEye, RobotCellDrill.BadgeCellKeeper }, drill.Badges());
        }

        [Test]
        public void EndingEarly_FailsWithTheStepsMissing_AndSendsTheColleagueHome()
        {
            var drill = new RobotCellDrill();
            drill.Start(3, 0d);
            ClearTheJam(drill, 0d);
            drill.Tick(33d);
            Assert.AreEqual(2, drill.TakeWalkerCommand());
            drill.End(40d);
            Assert.IsFalse(drill.Running);
            Assert.AreEqual(DrillResult.Failed, drill.Score.Result);
            Assert.AreEqual(0, drill.TakeWalkerCommand());
            StringAssert.StartsWith("Not done: A colleague is walking into the cell", drill.Score.FailReason());

            // Nothing counts once it is over.
            drill.LineStopPressed(41d);
            Assert.AreEqual(2, drill.Score.Missing().Count);
            Assert.AreEqual(120d, RobotCellDrill.ParSeconds(1));
            Assert.AreEqual(240d, RobotCellDrill.ParSeconds(4));
        }
    }
}
