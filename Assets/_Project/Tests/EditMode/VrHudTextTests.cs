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
            Assert.AreEqual("Step 4 of 12: Apply your lock and tag", VrHudText.Step(4, TrainingOutcome.InProgress));
            Assert.AreEqual("Lockout done, with mistakes", VrHudText.Step(13, TrainingOutcome.PassedWithErrors));
        }

        [Test]
        public void Current_And_Next_NameTheTaskAtHandAndTheOneAfter()
        {
            Assert.AreEqual("NOW · STEP 4 OF 12", VrHudText.NowLabel(4, TrainingOutcome.InProgress));
            Assert.AreEqual("Apply your lock and tag", VrHudText.Current(4, TrainingOutcome.InProgress));
            Assert.AreEqual(LockoutProcedure.Steps[4].Title, VrHudText.Next(4, TrainingOutcome.InProgress));
            Assert.AreEqual("Last step", VrHudText.Next(12, TrainingOutcome.InProgress));
        }

        [Test]
        public void Current_And_Next_OutsideARun()
        {
            Assert.AreEqual("LOCKOUT TRAINING", VrHudText.NowLabel(0, TrainingOutcome.Idle));
            Assert.AreEqual("Not started: press Lockout training", VrHudText.Current(0, TrainingOutcome.Idle));
            Assert.AreEqual(string.Empty, VrHudText.Next(0, TrainingOutcome.Idle));
            Assert.AreEqual("LOCKOUT COMPLETE", VrHudText.NowLabel(13, TrainingOutcome.PassedWithErrors));
            Assert.AreEqual("Done, with mistakes", VrHudText.Current(13, TrainingOutcome.PassedWithErrors));
            Assert.AreEqual(string.Empty, VrHudText.Next(13, TrainingOutcome.Passed));
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
            Assert.AreEqual("Control station: blue intercom button. Press it with your finger, or point and pull the trigger.", VrHudText.Where(1, TrainingOutcome.InProgress));
            Assert.AreEqual("CNC, east end: red handle on the yellow isolator. Grip the handle and twist it a quarter turn.", VrHudText.Where(3, TrainingOutcome.InProgress));
            Assert.AreEqual("CNC, east end: the hasp under the isolator; your lock is on the board beside it. Grip your lock and hang it on the hasp; take it off the same way.", VrHudText.Where(4, TrainingOutcome.InProgress));
            Assert.AreEqual(string.Empty, VrHudText.Where(0, TrainingOutcome.Idle));
            Assert.AreEqual(string.Empty, VrHudText.Where(13, TrainingOutcome.Passed));
        }

        [Test]
        public void Prompt_SaysHowToUseThePoint()
        {
            Assert.AreEqual("CNC isolator: switch off  ·  pull the trigger", VrHudText.Prompt("CNC isolator: switch off"));
            Assert.AreEqual(string.Empty, VrHudText.Prompt(string.Empty));
        }
    }
}
