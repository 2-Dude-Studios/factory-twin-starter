using System;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class CellSequenceTests
    {
        private const double Dt = 0.02;

        private static readonly CellInputs Idle = new CellInputs(false, false, false);
        private static readonly CellInputs AtGate = new CellInputs(true, false, false);
        private static readonly CellInputs AtGateBusy = new CellInputs(true, true, false);
        private static readonly CellInputs Busy = new CellInputs(false, true, false);

        private static void Run(CellSequence sequence, CellInputs inputs, double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += Dt)
                sequence.Tick(inputs, Dt);
        }

        [Test]
        public void Starts_FeedingWithBothConveyorsRunning()
        {
            var sequence = new CellSequence();
            sequence.Tick(Idle, Dt);

            Assert.AreEqual(CellState.Feeding, sequence.State);
            Assert.IsTrue(sequence.Outputs.InfeedRun);
            Assert.IsTrue(sequence.Outputs.OutfeedRun);
            Assert.IsFalse(sequence.Outputs.PickCommand);
        }

        [Test]
        public void PartAtGate_StopsInfeedAndCommandsPick()
        {
            var sequence = new CellSequence();
            sequence.Tick(Idle, Dt);
            sequence.Tick(AtGate, Dt);

            Assert.AreEqual(CellState.Transferring, sequence.State);
            Assert.IsFalse(sequence.Outputs.InfeedRun);
            Assert.IsTrue(sequence.Outputs.OutfeedRun);
            Assert.IsTrue(sequence.Outputs.PickCommand);
        }

        [Test]
        public void FullCycle_AckDropsCommand_DoneResumesFeeding()
        {
            var sequence = new CellSequence();
            sequence.Tick(AtGate, Dt);
            Assert.IsTrue(sequence.Outputs.PickCommand);

            sequence.Tick(AtGateBusy, Dt);
            Assert.IsFalse(sequence.Outputs.PickCommand, "command must drop once the pick station acknowledges");
            Assert.AreEqual(CellState.Transferring, sequence.State);

            Run(sequence, Busy, 2d);
            Assert.AreEqual(CellState.Transferring, sequence.State);
            Assert.IsFalse(sequence.Outputs.InfeedRun);

            sequence.Tick(Idle, Dt);
            Assert.AreEqual(CellState.Feeding, sequence.State);
            Assert.IsTrue(sequence.Outputs.InfeedRun);
            Assert.AreEqual(0, sequence.PickRetries);
        }

        [Test]
        public void MissingAck_RepulsesCommandWithAFreshRisingEdge()
        {
            var sequence = new CellSequence(ackTimeoutSeconds: 1d, transferTimeoutSeconds: 10d);
            sequence.Tick(AtGate, Dt);

            bool sawDrop = false;
            bool sawRiseAfterDrop = false;
            for (int i = 0; i < 80; i++)
            {
                sequence.Tick(AtGate, Dt);
                if (!sequence.Outputs.PickCommand)
                    sawDrop = true;
                else if (sawDrop)
                    sawRiseAfterDrop = true;
            }

            Assert.IsTrue(sawDrop);
            Assert.IsTrue(sawRiseAfterDrop);
            Assert.AreEqual(1, sequence.PickRetries);
            Assert.AreEqual(CellState.Transferring, sequence.State);
        }

        [Test]
        public void DoneWithPartStillAtGate_RetriesThePick()
        {
            var sequence = new CellSequence();
            sequence.Tick(AtGate, Dt);
            sequence.Tick(AtGateBusy, Dt);
            sequence.Tick(AtGate, Dt);

            Assert.AreEqual(CellState.Transferring, sequence.State);
            Assert.IsTrue(sequence.Outputs.PickCommand);
            Assert.AreEqual(1, sequence.PickRetries);
        }

        [Test]
        public void Hold_StopsBothConveyorsUntilReleased()
        {
            var holding = new CellInputs(false, false, false, hold: true);
            var atGateHolding = new CellInputs(true, false, false, hold: true);

            var sequence = new CellSequence(ackTimeoutSeconds: 1d, transferTimeoutSeconds: 5d);
            sequence.Tick(Idle, Dt);
            sequence.Tick(holding, Dt);

            Assert.AreEqual(CellState.Held, sequence.State);
            Assert.IsFalse(sequence.Outputs.InfeedRun);
            Assert.IsFalse(sequence.Outputs.OutfeedRun);
            Assert.IsFalse(sequence.Outputs.PickCommand);

            Run(sequence, atGateHolding, 30d);
            Assert.AreEqual(CellState.Held, sequence.State, "a part waiting at the gate is not picked while held");
            Assert.IsFalse(sequence.Outputs.PickCommand);
            Assert.AreEqual(0, sequence.Stalls, "a hold is not a stall, however long it lasts");

            sequence.Tick(AtGate, Dt);
            Assert.AreEqual(CellState.Feeding, sequence.State);
            Assert.IsTrue(sequence.Outputs.OutfeedRun);

            sequence.Tick(AtGate, Dt);
            Assert.AreEqual(CellState.Transferring, sequence.State);
            Assert.IsTrue(sequence.Outputs.PickCommand, "the waiting part is picked once the hold lifts");
        }

        [Test]
        public void Hold_DuringATransfer_LetsThePickFinishFirst()
        {
            var holding = new CellInputs(false, false, false, hold: true);
            var busyHolding = new CellInputs(false, true, false, hold: true);

            var sequence = new CellSequence();
            sequence.Tick(AtGate, Dt);
            sequence.Tick(AtGateBusy, Dt);

            Run(sequence, busyHolding, 2d);
            Assert.AreEqual(CellState.Transferring, sequence.State);
            Assert.IsTrue(sequence.Outputs.OutfeedRun, "the outfeed must still take the part the arm is carrying");

            sequence.Tick(holding, Dt);
            Assert.AreEqual(CellState.Held, sequence.State);
            Assert.IsFalse(sequence.Outputs.InfeedRun);
            Assert.IsFalse(sequence.Outputs.OutfeedRun);
        }

        [Test]
        public void TransferTimeout_StallsUntilGateIsClearedAndStationIdle()
        {
            var sequence = new CellSequence(ackTimeoutSeconds: 1d, transferTimeoutSeconds: 5d);
            sequence.Tick(AtGate, Dt);
            sequence.Tick(AtGateBusy, Dt);
            Run(sequence, AtGateBusy, 5.5d);

            Assert.AreEqual(CellState.Stalled, sequence.State);
            Assert.AreEqual(1, sequence.Stalls);
            Assert.IsFalse(sequence.Outputs.InfeedRun);
            Assert.IsFalse(sequence.Outputs.PickCommand);

            Run(sequence, AtGate, 1d);
            Assert.AreEqual(CellState.Stalled, sequence.State, "must hold while the part is still in the gate");
            Run(sequence, Busy, 1d);
            Assert.AreEqual(CellState.Stalled, sequence.State, "must hold while the pick station is still busy");

            sequence.Tick(Idle, Dt);
            Assert.AreEqual(CellState.Feeding, sequence.State);
            Assert.IsTrue(sequence.Outputs.InfeedRun);
        }

        [Test]
        public void ExitSensor_CountsRisingEdgesAndMeasuresCycleTime()
        {
            var sequence = new CellSequence();
            var exit = new CellInputs(false, false, true);

            Run(sequence, Idle, 1d);
            Run(sequence, exit, 0.5d);
            Assert.AreEqual(1, sequence.PartsOut);
            Assert.IsTrue(double.IsNaN(sequence.LastCycleSeconds));

            Run(sequence, Idle, 3.5d);
            sequence.Tick(exit, Dt);
            Assert.AreEqual(2, sequence.PartsOut);
            Assert.AreEqual(4d, sequence.LastCycleSeconds, 0.05d);
        }

        [Test]
        public void CycleTime_LeavesOutTimeHeldForAPressFault()
        {
            var sequence = new CellSequence();
            var exit = new CellInputs(false, false, true);
            var held = new CellInputs(false, false, false, true);

            Run(sequence, exit, 0.5d);
            Run(sequence, Idle, 2d);
            Run(sequence, held, 10d);
            Assert.AreEqual(CellState.Held, sequence.State);
            Run(sequence, Idle, 1.5d);
            sequence.Tick(exit, Dt);

            Assert.AreEqual(2, sequence.PartsOut);
            Assert.AreEqual(4d, sequence.LastCycleSeconds, 0.1d, "2 s + 1.5 s running plus the 0.5 s on the first exit; the 10 s hold is left out");
        }

        [Test]
        public void SafetyStop_StopsEverythingOnTheSameTick()
        {
            var sequence = new CellSequence();
            sequence.Tick(new CellInputs(false, false, false, safetyStop: true), Dt);

            Assert.AreEqual(CellState.SafetyStop, sequence.State);
            Assert.IsFalse(sequence.Outputs.InfeedRun);
            Assert.IsFalse(sequence.Outputs.OutfeedRun);
            Assert.IsFalse(sequence.Outputs.PickCommand);
        }

        [Test]
        public void SafetyStop_MidTransfer_DoesNotWaitForThePickAndDoesNotStall()
        {
            var stoppedBusy = new CellInputs(false, true, false, safetyStop: true);

            var sequence = new CellSequence(ackTimeoutSeconds: 2d, transferTimeoutSeconds: 15d);
            sequence.Tick(AtGate, Dt);
            sequence.Tick(AtGateBusy, Dt);
            Run(sequence, Busy, 10d);

            sequence.Tick(stoppedBusy, Dt);
            Assert.AreEqual(CellState.SafetyStop, sequence.State, "a protective stop does not let the pick finish");
            Assert.IsFalse(sequence.Outputs.OutfeedRun);

            Run(sequence, stoppedBusy, 30d);
            Assert.AreEqual(CellState.SafetyStop, sequence.State, "time stopped does not count toward the transfer timeout");
            Assert.AreEqual(0, sequence.Stalls);

            sequence.Tick(Busy, Dt);
            Assert.AreEqual(CellState.Transferring, sequence.State, "reset resumes the transfer that was under way");
            Assert.IsTrue(sequence.Outputs.OutfeedRun);
            Assert.IsFalse(sequence.Outputs.PickCommand, "the pick was already acknowledged");

            sequence.Tick(Idle, Dt);
            Assert.AreEqual(CellState.Feeding, sequence.State);
            Assert.AreEqual(0, sequence.Stalls);
        }

        [Test]
        public void SafetyStop_BeforeTheAck_RaisesThePickCommandAgainOnReset()
        {
            var stoppedAtGate = new CellInputs(true, false, false, safetyStop: true);

            var sequence = new CellSequence();
            sequence.Tick(AtGate, Dt);
            Assert.IsTrue(sequence.Outputs.PickCommand);

            sequence.Tick(stoppedAtGate, Dt);
            Assert.IsFalse(sequence.Outputs.PickCommand);

            sequence.Tick(AtGate, Dt);
            Assert.AreEqual(CellState.Transferring, sequence.State);
            Assert.IsTrue(sequence.Outputs.PickCommand, "a fresh rising edge for the arm");
        }

        [Test]
        public void SafetyStop_OutranksAPressHold_AndTheHoldStillAppliesAfterTheReset()
        {
            var sequence = new CellSequence();
            sequence.Tick(new CellInputs(false, false, false, hold: true, safetyStop: true), Dt);
            Assert.AreEqual(CellState.SafetyStop, sequence.State);

            sequence.Tick(new CellInputs(false, false, false, hold: true), Dt);
            Assert.AreEqual(CellState.Held, sequence.State);
        }

        [Test]
        public void CycleTime_LeavesOutTimeInAProtectiveStop()
        {
            var exit = new CellInputs(false, false, true);
            var sequence = new CellSequence();

            sequence.Tick(exit, Dt);
            Run(sequence, Idle, 4d);
            Run(sequence, new CellInputs(false, false, false, safetyStop: true), 20d);
            Run(sequence, Idle, 1d);
            sequence.Tick(exit, Dt);

            Assert.AreEqual(5d + Dt, sequence.LastCycleSeconds, 0.05d);
        }

        [Test]
        public void Constructor_RejectsInconsistentTimeouts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CellSequence(0d, 5d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CellSequence(5d, 5d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CellSequence().Tick(Idle, -1d));
        }

        [Test]
        public void LongRun_ManyCyclesNeverStallsOrRetries()
        {
            var sequence = new CellSequence();
            for (int part = 0; part < 200; part++)
            {
                Run(sequence, Idle, 1.5d);          // part travels to the gate
                sequence.Tick(AtGate, Dt);           // gate trips
                sequence.Tick(AtGateBusy, Dt);       // station picks
                Run(sequence, Busy, 2.5d);           // station moves it
                sequence.Tick(Idle, Dt);             // placed
                Run(sequence, new CellInputs(false, false, true), 0.2d); // leaves the cell
            }

            Assert.AreEqual(200, sequence.PartsOut);
            Assert.AreEqual(0, sequence.PickRetries);
            Assert.AreEqual(0, sequence.Stalls);
            Assert.AreEqual(CellState.Feeding, sequence.State);
        }
    }
}
