using System;
using System.Collections.Generic;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class DrillScoreTests
    {
        private static DrillScore Level(double parSeconds = 120d) => new DrillScore(TrainingArea.Electrical, 2, new[]
        {
            new DrillStep("ppe", "PPE the label calls for", "1910.335(a)(1)", 100),
            new DrillStep("gloves", "Gloves inspected and air-tested", "1910.137(c)(2)", 50),
            new DrillStep("prove", "Tester proved before and after", "70E 120.6(7)", 100),
        }, parSeconds);

        private static DrillScore AllDone(DrillScore score, double start = 10d)
        {
            score.Start(start);
            score.Done("ppe", start + 5);
            score.Done("gloves", start + 20);
            score.Done("prove", start + 40);
            return score;
        }

        [Test]
        public void Stars_OneForPassing_TwoForNoMistakes_ThreeWithinPar()
        {
            Assert.AreEqual(0, DrillStars.For(false, 0, 0, 10, 60), "a step is missing");
            Assert.AreEqual(0, DrillStars.For(true, 1, 0, 10, 60), "a critical mistake");
            Assert.AreEqual(1, DrillStars.For(true, 0, 2, 10, 60));
            Assert.AreEqual(2, DrillStars.For(true, 0, 0, 61, 60));
            Assert.AreEqual(3, DrillStars.For(true, 0, 0, 60, 60));
            Assert.AreEqual(3, DrillStars.For(true, 0, 0, 999, 0), "no par time set");
            Assert.AreEqual("***", DrillStars.Text(3));
            Assert.AreEqual("**-", DrillStars.Text(2));
            Assert.AreEqual("---", DrillStars.Text(0));
            Assert.AreEqual("***", DrillStars.Text(7));
        }

        [Test]
        public void CleanRunWithinPar_PassesWithThreeStars()
        {
            DrillScore score = AllDone(Level());
            Assert.AreEqual(DrillResult.InProgress, score.Result);
            Assert.AreEqual(3, score.StepsDone);
            score.Finish(70d);
            Assert.AreEqual(DrillResult.Passed, score.Result);
            Assert.AreEqual(3, score.Stars);
            Assert.AreEqual(250, score.Points);
            Assert.AreEqual(60d, score.Duration, 1e-9);
            Assert.AreEqual(string.Empty, score.FailReason());
            CollectionAssert.IsEmpty(score.Missing());
        }

        [Test]
        public void MinorMistake_KeepsThePassButOneStar_AndOverParTwo()
        {
            DrillScore score = AllDone(Level());
            score.Mistake("Label not read before approaching", "70E 130.5(H)", 25, 30d);
            score.Finish(60d);
            Assert.AreEqual(DrillResult.Passed, score.Result);
            Assert.AreEqual(1, score.Stars);
            Assert.AreEqual(225, score.Points);
            Assert.AreEqual(1, score.Mistakes);

            DrillScore slow = AllDone(Level(parSeconds: 30d));
            slow.Finish(200d);
            Assert.AreEqual(2, slow.Stars);
        }

        [Test]
        public void CriticalMistake_FailsTheLevel_WhateverThePoints()
        {
            DrillScore score = AllDone(Level());
            score.Bonus("Fast and tidy", string.Empty, 5000, 45d);
            score.Critical("Opened the cabinet without the label's PPE", "1910.335(a)(1)", 50d);
            score.Finish(60d);
            Assert.Greater(score.Points, 5000, "the points are still counted");
            Assert.AreEqual(DrillResult.Failed, score.Result);
            Assert.AreEqual(0, score.Stars);
            Assert.AreEqual(1, score.Criticals);
            Assert.AreEqual("Critical mistake: Opened the cabinet without the label's PPE", score.FailReason());
        }

        [Test]
        public void MissingStep_FailsTheLevel_AndSaysWhichStep()
        {
            DrillScore score = Level();
            score.Start(0d);
            score.Done("ppe", 5d);
            score.Finish(20d);
            Assert.AreEqual(DrillResult.Failed, score.Result);
            Assert.AreEqual(0, score.Stars);
            Assert.AreEqual(2, score.Missing().Count);
            Assert.AreEqual("Not done: Gloves inspected and air-tested and 1 more", score.FailReason());
        }

        [Test]
        public void Steps_CountOnce_AndOnlyWhileTheRunIsOn()
        {
            DrillScore score = Level();
            Assert.IsFalse(score.Done("ppe", 0d), "not started");
            score.Start(0d);
            Assert.IsTrue(score.Done("ppe", 1d));
            Assert.IsFalse(score.Done("ppe", 2d), "already done");
            Assert.IsFalse(score.Done("no such step", 3d));
            Assert.IsTrue(score.IsDone("ppe"));
            Assert.AreEqual(100, score.Points);
            Assert.IsTrue(score.Badge("Right kit", 4d));
            Assert.IsFalse(score.Badge("Right kit", 5d), "a badge once a run");
            score.Metric("tests_done", "8", 6d);

            List<DrillEvent> fresh = score.TakeNew();
            Assert.AreEqual(3, fresh.Count);
            Assert.AreEqual(DrillEventKind.Badge, fresh[1].Kind);
            Assert.AreEqual(0, score.TakeNew().Count);

            score.Finish(7d);
            score.Mistake("after the end", string.Empty, 10, 8d);
            score.Critical("after the end", string.Empty, 9d);
            Assert.AreEqual(0, score.Mistakes + score.Criticals);

            // Starting again clears the run.
            score.Start(100d);
            Assert.AreEqual(0, score.Points);
            Assert.AreEqual(0, score.Events.Count);
            Assert.AreEqual(DrillResult.InProgress, score.Result);
        }

        [Test]
        public void Record_HasTheSharedColumns_AndReadsByClause()
        {
            DrillScore score = AllDone(Level(), start: 0d);
            score.Mistake("Glove air test skipped, then done", "1910.137(c)(2)", 25, 21d);
            score.Badge("Right kit", 41d);
            score.Metric("ppe_category", "2", 41d);
            score.Finish(50d);
            string csv = DrillRecord.ToCsv("Sam Rivera", "Demo trainer", new DateTime(2026, 11, 11, 9, 30, 0, DateTimeKind.Utc), score, "Electrical safety");
            string[] lines = csv.TrimEnd('\n').Split('\n');

            Assert.AreEqual(TrainingRecord.Header, DrillRecord.Header, "the lockout record's columns");
            Assert.AreEqual(DrillRecord.Header, lines[0]);
            Assert.AreEqual("Sam Rivera,Demo trainer,2026-11-11 09:30:00,5.0,1,1910.335(a)(1),done,PPE the label calls for (+100)", lines[1]);
            Assert.AreEqual("Sam Rivera,Demo trainer,2026-11-11 09:30:00,21.0,4,1910.137(c)(2),mistake,\"Glove air test skipped, then done (-25)\"", lines[4]);
            StringAssert.EndsWith(",,,badge,Right kit", lines[5]);
            StringAssert.EndsWith(",,,metric,ppe_category=2", lines[6]);
            Assert.AreEqual("Sam Rivera,Demo trainer,2026-11-11 09:30:00,,,,result," +
                            "\"Electrical safety level 2 Gear and skills: passed, 1 of 3 stars, 225 points, 1 mistake, 50 s\"", lines[7]);
            Assert.AreEqual(8, lines.Length);

            DrillScore failed = AllDone(Level());
            failed.Critical("Carried on after a reading that was not zero", "1910.333(b)(2)(iv)(B)", 60d);
            failed.Finish(61d);
            StringAssert.Contains("failed (Critical mistake: Carried on after a reading that was not zero)", DrillRecord.Summary(failed, "Electrical safety"));
            StringAssert.Contains(",critical,", DrillRecord.ToCsv("a", "b", DateTime.UtcNow, failed, "Electrical safety"));

            Assert.AreEqual("elec_Sam_Rivera_20261111_093000.csv",
                DrillRecord.FileName(TrainingArea.Electrical, "Sam Rivera", new DateTime(2026, 11, 11, 9, 30, 0, DateTimeKind.Utc)));
            Assert.AreEqual("robot_trainee_20260102_030405.csv",
                DrillRecord.FileName(TrainingArea.RobotCell, " ", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)));
            StringAssert.StartsWith("lockout_A_B_", DrillRecord.FileName(TrainingArea.Lockout, "A/B", DateTime.UtcNow));
        }

        [Test]
        public void Levels_OpenOneAfterAnother_AndTheAssessmentStartsTheRefresherClock()
        {
            Assert.IsTrue(DrillProgress.Unlocked(new[] { 0, 0, 0, 0 }, 1));
            Assert.IsFalse(DrillProgress.Unlocked(new[] { 0, 0, 0, 0 }, 2));
            Assert.IsTrue(DrillProgress.Unlocked(new[] { 1, 0, 0, 0 }, 2));
            Assert.IsFalse(DrillProgress.Unlocked(new[] { 3, 0, 0, 0 }, 3));
            Assert.IsTrue(DrillProgress.Unlocked(new[] { 3, 2, 1, 0 }, 4));
            Assert.IsFalse(DrillProgress.Unlocked(new[] { 3, 2, 1, 3 }, 5));
            Assert.IsFalse(DrillProgress.Unlocked(null, 2));
            Assert.AreEqual(6, DrillProgress.TotalStars(new[] { 3, 2, 1, 0 }));

            var passed = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(new DateTime(2027, 11, 11), DrillProgress.DueAgain(passed, TrainingArea.Fire));
            Assert.AreEqual(new DateTime(2027, 11, 11), DrillProgress.DueAgain(passed, TrainingArea.Lockout));
            Assert.AreEqual(new DateTime(2029, 11, 11), DrillProgress.DueAgain(passed, TrainingArea.Electrical), "70E: within three years");
            Assert.IsNull(DrillProgress.DueAgain(null, TrainingArea.Fire));
        }

        [Test]
        public void BadgeBook_KeepsTheBestStarsPerLevel_AndTheAssessmentDay()
        {
            BadgeBook book = BadgeBook.InMemory("Sam Rivera");
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, book.Stars(TrainingMode.Electrical));
            Assert.IsNull(book.AssessedUtc(TrainingMode.Electrical));

            var day = new DateTime(2026, 11, 11, 14, 0, 0, DateTimeKind.Utc);
            List<string> fresh = book.RecordLevel(TrainingMode.Electrical, 2, 3, 250, new[] { "Right kit" }, day);
            CollectionAssert.AreEqual(new[] { "Right kit" }, fresh);
            book.RecordLevel(TrainingMode.Electrical, 2, 1, 400, new[] { "Right kit", "Glove guardian" }, day);
            CollectionAssert.AreEqual(new[] { 0, 3, 0, 0 }, book.Stars(TrainingMode.Electrical), "the best stars stay");
            Assert.AreEqual(400, book.BestScore(TrainingMode.Electrical));
            Assert.AreEqual(2, book.Earned(TrainingMode.Electrical).Count);
            Assert.IsNull(book.AssessedUtc(TrainingMode.Electrical), "level 2 is not the assessment");

            book.RecordLevel(TrainingMode.Electrical, 4, 0, 90, new string[0], day);
            Assert.IsNull(book.AssessedUtc(TrainingMode.Electrical), "a failed assessment does not count");
            book.RecordLevel(TrainingMode.Electrical, 4, 2, 300, new string[0], day);
            Assert.AreEqual(new DateTime(2026, 11, 11), book.AssessedUtc(TrainingMode.Electrical));
            book.RecordLevel(TrainingMode.Electrical, 9, 3, 1, new string[0], day);
            CollectionAssert.AreEqual(new[] { 0, 3, 0, 2 }, book.Stars(TrainingMode.Electrical));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, book.Stars(TrainingMode.FireSafety), "another training is untouched");
        }

        private static LockoutProcedure LockoutRun(int steps, System.Action<LockoutProcedure, int> before = null)
        {
            var run = new LockoutProcedure();
            run.Begin("Sam Rivera", 0L, "Demo trainer");
            for (int i = 0; i < steps; i++)
            {
                before?.Invoke(run, i + 1);
                Assert.IsTrue(run.Attempt(LockoutProcedure.Steps[i].Action, 10d * (i + 1)), LockoutProcedure.Steps[i].Title);
            }
            return run;
        }

        [Test]
        public void Lockout_CleanRun_IsLevelThreeWithThreeStarsAndBothBadges()
        {
            DrillScore score = LockoutLevel.Score(LockoutRun(LockoutProcedure.Steps.Count));
            Assert.AreEqual(TrainingArea.Lockout, score.Area);
            Assert.AreEqual(3, score.Level);
            Assert.AreEqual(DrillResult.Passed, score.Result);
            Assert.AreEqual(3, score.Stars, "140 s is within the par time");
            Assert.AreEqual(14 * LockoutLevel.StepPoints, score.Points);
            CollectionAssert.AreEqual(new[] { LockoutLevel.BadgeTryStart, LockoutLevel.BadgeCleanRelease }, score.Badges);
            Assert.AreEqual(14, LockoutLevel.RequiredSteps().Count);
            Assert.AreEqual("1910.147(d)(6)", LockoutLevel.RequiredSteps()[6].Regulation);
        }

        [Test]
        public void Lockout_StepOutOfOrder_IsAMinorMistake_OneStar()
        {
            // Tries to remove the lock before the inspection (step 9 is due).
            LockoutProcedure run = LockoutRun(LockoutProcedure.Steps.Count, (r, step) =>
            {
                if (step == 9)
                    Assert.IsFalse(r.Attempt(LockoutAction.RemoveLock, 85d));
            });
            DrillScore score = LockoutLevel.Score(run);
            Assert.AreEqual(DrillResult.Passed, score.Result);
            Assert.AreEqual(1, score.Stars);
            Assert.AreEqual(1, score.Mistakes);
            Assert.AreEqual(14 * LockoutLevel.StepPoints - LockoutLevel.MistakePoints, score.Points);
            CollectionAssert.AreEqual(new[] { LockoutLevel.BadgeTryStart }, score.Badges, "the release was not clean");
        }

        [Test]
        public void Lockout_WorkBeforeTheTryStart_FailsTheLevel()
        {
            // Changes the tool with the lock on but before the try-start (step 7 is due).
            LockoutProcedure run = LockoutRun(LockoutProcedure.Steps.Count, (r, step) =>
            {
                if (step == 7)
                    Assert.IsFalse(r.Attempt(LockoutAction.ChangeTool, 65d));
            });
            Assert.AreEqual(TrainingOutcome.PassedWithErrors, run.Result, "M11's own result is unchanged");
            DrillScore score = LockoutLevel.Score(run);
            Assert.AreEqual(DrillResult.Failed, score.Result);
            Assert.AreEqual(0, score.Stars);
            Assert.AreEqual("Critical mistake: " + LockoutLevel.WorkBeforeVerify, score.FailReason());
            CollectionAssert.DoesNotContain(score.Badges, LockoutLevel.BadgeTryStart);
        }

        [Test]
        public void Lockout_AreaCalledClearWithSomeoneInIt_FailsTheLevel_AndAHalfRunIsNotAPass()
        {
            var run = LockoutRun(9);
            Assert.IsFalse(run.Attempt(LockoutAction.CheckArea, 95d, workAreaClear: false));
            for (int i = 9; i < LockoutProcedure.Steps.Count; i++)
                Assert.IsTrue(run.Attempt(LockoutProcedure.Steps[i].Action, 100d + i));
            DrillScore score = LockoutLevel.Score(run);
            Assert.AreEqual(DrillResult.Failed, score.Result);
            Assert.AreEqual("Critical mistake: " + LockoutLevel.AreaNotClear, score.FailReason());

            DrillScore half = LockoutLevel.Score(LockoutRun(5));
            Assert.AreEqual(DrillResult.Failed, half.Result);
            Assert.AreEqual(9, half.Missing().Count);
            StringAssert.StartsWith("Not done: Release stored energy", half.FailReason());
        }

        [Test]
        public void Boards_ShowStarsLockedLevelsBadgesAndTheRefresher()
        {
            BadgeBook book = BadgeBook.InMemory("Sam Rivera");
            var day = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
            book.RecordLevel(TrainingMode.Electrical, 1, 3, 200, new[] { "Label reader" }, day);
            book.RecordLevel(TrainingMode.Electrical, 2, 2, 250, new[] { "Right kit" }, day);
            book.Record(TrainingMode.FireSafety, 480, new[] { "Alarm first" });

            string board = DrillBoardText.AreaBoard(book, TrainingArea.Electrical, day);
            StringAssert.StartsWith("Sam Rivera", board);
            StringAssert.Contains("L1 Awareness:  ***", board);
            StringAssert.Contains("L2 Gear and skills:  **-", board);
            StringAssert.Contains("L3 Procedure:  ---", board);
            StringAssert.Contains("L4 Assessment:  locked", board);
            StringAssert.Contains("Best score: 250", board);
            StringAssert.Contains("Badges: Label reader, Right kit", board);
            StringAssert.Contains("Assessment not passed yet", board);
            StringAssert.Contains("A critical mistake fails the level, whatever the points.", board);
            StringAssert.Contains("Badges: none yet", DrillBoardText.AreaBoard(book, TrainingArea.Lockout, day));

            // A drill whose first levels are not built yet still shows the level it has stars on.
            book.RecordLevel(TrainingMode.Lockout, 3, 2, 325, new[] { "Try-start" }, day);
            string lockout = DrillBoardText.AreaBoard(book, TrainingArea.Lockout, day);
            StringAssert.Contains("L2 Gear and skills:  locked", lockout);
            StringAssert.Contains("L3 Procedure:  **-", lockout);

            string wall = DrillBoardText.BadgeWall(book, day);
            StringAssert.Contains("ELECTRICAL SAFETY\nL1 ***   L2 **-   L3 ---   L4 ---   best 250\nBadges: Label reader, Right kit", wall);
            StringAssert.Contains("FIRE SAFETY\nL1 ---   L2 ---   L3 ---   L4 ---   best 480\nBadges: Alarm first", wall);
            StringAssert.Contains("ROBOT CELL SAFETY", wall);
            StringAssert.Contains("LOCKOUT / TAGOUT", wall);
            StringAssert.EndsWith(DrillLevels.DemoNotice, wall);

            book.RecordLevel(TrainingMode.Electrical, 3, 1, 100, new string[0], day);
            book.RecordLevel(TrainingMode.Electrical, 4, 1, 100, new string[0], day);
            Assert.AreEqual("Due again: Nov 2029", DrillBoardText.Due(book.AssessedUtc(TrainingMode.Electrical), TrainingArea.Electrical, day));
            Assert.AreEqual("Refresher overdue since Nov 2029",
                DrillBoardText.Due(book.AssessedUtc(TrainingMode.Electrical), TrainingArea.Electrical, new DateTime(2030, 1, 1)));
            Assert.AreEqual(TrainingMode.Electrical, TrainingLaunch.ModeFor(TrainingArea.Electrical));
            Assert.AreEqual(TrainingMode.None, TrainingLaunch.ModeFor(TrainingArea.Hub));
        }
    }
}
