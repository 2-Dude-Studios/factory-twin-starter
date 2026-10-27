using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class TrainingPointTests
    {
        [Test]
        public void Isolator_OffersTheOppositeOfItsState()
        {
            Assert.AreEqual(LockoutAction.IsolatorOff, TrainingPoints.ActionFor(TrainingPointKind.Isolator, false, false));
            Assert.AreEqual(LockoutAction.IsolatorOn, TrainingPoints.ActionFor(TrainingPointKind.Isolator, true, false));
            Assert.AreEqual("CNC isolator: switch off", TrainingPoints.Prompt(TrainingPointKind.Isolator, false, false));
        }

        [Test]
        public void LockHasp_OffersTheOppositeOfItsState()
        {
            Assert.AreEqual(LockoutAction.ApplyLock, TrainingPoints.ActionFor(TrainingPointKind.LockHasp, true, false));
            Assert.AreEqual(LockoutAction.RemoveLock, TrainingPoints.ActionFor(TrainingPointKind.LockHasp, true, true));
        }

        [Test]
        public void EveryAction_CanBeReachedFromSomePoint()
        {
            var reachable = new System.Collections.Generic.HashSet<LockoutAction>();
            foreach (TrainingPointKind kind in System.Enum.GetValues(typeof(TrainingPointKind)))
            {
                reachable.Add(TrainingPoints.ActionFor(kind, false, false));
                reachable.Add(TrainingPoints.ActionFor(kind, true, true));
            }
            foreach (LockoutStep step in LockoutProcedure.Steps)
                Assert.IsTrue(reachable.Contains(step.Action), step.Title);
        }

        [Test]
        public void StepPanel_ShowsTheStepDueWithItsParagraph()
        {
            Assert.AreEqual("Step 4 of 14", DashboardViewModel.TrainingStepWords(4, TrainingOutcome.InProgress));
            Assert.AreEqual("Isolate the CNC at its main isolator · 1910.147(d)(3)",
                DashboardViewModel.TrainingNextWords(4, TrainingOutcome.InProgress));
            Assert.AreEqual("14 of 14 steps", DashboardViewModel.TrainingStepWords(15, TrainingOutcome.PassedWithErrors));
            Assert.AreEqual("Lockout complete. Training record saved.", DashboardViewModel.TrainingNextWords(15, TrainingOutcome.Passed));
            Assert.AreEqual("No mistakes", DashboardViewModel.MistakeWords(0));
            Assert.AreEqual("1 mistake", DashboardViewModel.MistakeWords(1));
            Assert.AreEqual("Done, with mistakes", DashboardViewModel.TrainingResultWords(TrainingOutcome.PassedWithErrors));
        }
    }
}
