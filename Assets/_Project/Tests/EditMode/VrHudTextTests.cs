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
        public void Prompt_SaysHowToUseThePoint()
        {
            Assert.AreEqual("CNC isolator: switch off  ·  pull the trigger", VrHudText.Prompt("CNC isolator: switch off"));
            Assert.AreEqual(string.Empty, VrHudText.Prompt(string.Empty));
        }
    }
}
