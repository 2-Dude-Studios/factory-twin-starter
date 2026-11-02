using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class VrHudTextTests
    {
        [Test]
        public void Status_PutsSafetyBeforeTheLine()
        {
            Assert.AreEqual("Protective stop: you are in the robot zone", VrHudText.Status(SafetyState.Stopped, CellState.SafetyStop));
            Assert.AreEqual("Robot zone clear: press Safety reset", VrHudText.Status(SafetyState.AwaitingReset, CellState.SafetyStop));
            Assert.AreEqual("Warning zone: the arm has slowed", VrHudText.Status(SafetyState.Reduced, CellState.Feeding));
            Assert.AreEqual("Line: Stopped — CNC fault", VrHudText.Status(SafetyState.Normal, CellState.Held));
        }

        [Test]
        public void Step_NamesTheStepDueOnlyWhileARunIsOn()
        {
            Assert.AreEqual(string.Empty, VrHudText.Step(0, TrainingOutcome.Idle));
            Assert.AreEqual("Step 5 of 14: Apply your lock and tag", VrHudText.Step(5, TrainingOutcome.InProgress));
            Assert.AreEqual("Lockout done, with mistakes", VrHudText.Step(15, TrainingOutcome.PassedWithErrors));
        }

        [Test]
        public void Current_And_Next_NameTheTaskAtHandAndTheOneAfter()
        {
            Assert.AreEqual("NOW · STEP 5 OF 14", VrHudText.NowLabel(5, TrainingOutcome.InProgress));
            Assert.AreEqual("Apply your lock and tag", VrHudText.Current(5, TrainingOutcome.InProgress));
            Assert.AreEqual(LockoutProcedure.Steps[5].Title, VrHudText.Next(5, TrainingOutcome.InProgress));
            Assert.AreEqual("Last step", VrHudText.Next(14, TrainingOutcome.InProgress));
        }

        [Test]
        public void Current_And_Next_OutsideARun()
        {
            Assert.AreEqual("LOCKOUT TRAINING", VrHudText.NowLabel(0, TrainingOutcome.Idle));
            Assert.AreEqual("Not started: press Lockout training", VrHudText.Current(0, TrainingOutcome.Idle));
            Assert.AreEqual(string.Empty, VrHudText.Next(0, TrainingOutcome.Idle));
            Assert.AreEqual("LOCKOUT COMPLETE", VrHudText.NowLabel(15, TrainingOutcome.PassedWithErrors));
            Assert.AreEqual("Done, with mistakes", VrHudText.Current(15, TrainingOutcome.PassedWithErrors));
            Assert.AreEqual(string.Empty, VrHudText.Next(15, TrainingOutcome.Passed));
        }

        [Test]
        public void EveryStep_HasAPointToGuideTo()
        {
            foreach (LockoutStep step in LockoutProcedure.Steps)
            {
                Assert.IsTrue(TrainingPoints.TryKindFor(step.Action, out TrainingPointKind kind), step.Title);
                Assert.IsNotEmpty(TrainingPoints.Where(kind), step.Title);
                Assert.AreEqual(step.Action, TrainingPoints.ActionFor(kind,
                    isolated: step.Action == LockoutAction.IsolatorOn, locked: step.Action == LockoutAction.RemoveLock), step.Title);
            }
        }

        [Test]
        public void Where_SaysWhichPointAndHowToUseIt_OnlyDuringARun()
        {
            Assert.AreEqual("Control station: blue intercom button. Press it with your finger, or point and pull the trigger.", VrHudText.Where(2, TrainingOutcome.InProgress));
            Assert.AreEqual("CNC, east end: red handle on the yellow isolator. Grip the handle and twist it a quarter turn.", VrHudText.Where(4, TrainingOutcome.InProgress));
            Assert.AreEqual("CNC, east end: the hasp under the isolator; your lock is on the board beside it. Grip your lock and hang it on the hasp; take it off the same way.", VrHudText.Where(5, TrainingOutcome.InProgress));
            Assert.AreEqual(string.Empty, VrHudText.Where(0, TrainingOutcome.Idle));
            Assert.AreEqual(string.Empty, VrHudText.Where(15, TrainingOutcome.Passed));
        }

        [Test]
        public void Refusal_SaysWhatIsDueAndWhere()
        {
            Assert.AreEqual("Not yet: Remove lock and tag. First: Check the work area: everyone clear (Control station: white area-clear button).",
                VrHudText.Refusal(LockoutAction.RemoveLock, 10, TrainingOutcome.InProgress));
            StringAssert.StartsWith("Not clear: someone is still in the work area.",
                VrHudText.Refusal(LockoutAction.CheckArea, 10, TrainingOutcome.InProgress, areaOccupied: true));
            Assert.AreEqual(string.Empty, VrHudText.Refusal(LockoutAction.Start, 0, TrainingOutcome.Idle));
        }

        [Test]
        public void Crew_StandsClearFromTheFirstNoticeToTheLast_AndTheColleagueComesBackAfterServicing()
        {
            Assert.AreEqual(2, CrewRules.FirstNotifyStep);
            Assert.AreEqual(12, CrewRules.LastNotifyStep);
            Assert.IsFalse(CrewRules.StandClear(2, TrainingOutcome.InProgress), "not notified yet");
            Assert.IsTrue(CrewRules.StandClear(3, TrainingOutcome.InProgress));
            Assert.IsTrue(CrewRules.StandClear(12, TrainingOutcome.InProgress), "still clear until told the lock is off");
            Assert.IsFalse(CrewRules.StandClear(13, TrainingOutcome.InProgress));
            Assert.IsFalse(CrewRules.StandClear(5, TrainingOutcome.Idle));
            Assert.IsFalse(CrewRules.ColleagueInArea(8, TrainingOutcome.InProgress), "servicing still due");
            Assert.IsTrue(CrewRules.ColleagueInArea(9, TrainingOutcome.InProgress));
            Assert.IsTrue(CrewRules.ColleagueInArea(10, TrainingOutcome.InProgress), "there for the area check");
            Assert.IsFalse(CrewRules.ColleagueInArea(11, TrainingOutcome.InProgress));
        }

        [Test]
        public void Tag_WarnsAndNamesWhoAppliedIt()
        {
            Assert.AreEqual("DANGER\nDO NOT\nOPERATE\n\nSam Rivera", TagLabel.Words("Sam Rivera"));
        }

        [Test]
        public void Prompt_SaysHowToUseThePoint()
        {
            Assert.AreEqual("CNC isolator: switch off  ·  pull the trigger", VrHudText.Prompt("CNC isolator: switch off"));
            Assert.AreEqual(string.Empty, VrHudText.Prompt(string.Empty));
        }
    }
}
