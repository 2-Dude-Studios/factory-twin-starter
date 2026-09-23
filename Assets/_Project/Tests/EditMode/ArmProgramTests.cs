using System;
using System.Collections.Generic;
using NUnit.Framework;
using TwoDude.FactoryTwin.Robot;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class ArmProgramTests
    {
        private const float Dt = 0.02f;

        private static float[] Pose(float a) => new[] { a, a, a, a, a, a };

        private static List<ArmWaypoint> Program() => new List<ArmWaypoint>
        {
            new ArmWaypoint("home", Pose(0f), 0f, GripAction.None),
            new ArmWaypoint("pick", Pose(10f), 0.1f, GripAction.Close),
            new ArmWaypoint("place", Pose(-10f), 0.1f, GripAction.Open),
        };

        /// <summary>Runs the program with joints that follow the commanded angles exactly.</summary>
        private static int RunToCompletion(ArmProgram program, int maxTicks = 10000)
        {
            int ticks = 0;
            var measured = new float[6];
            while (program.IsRunning && ticks < maxTicks)
            {
                for (int j = 0; j < 6; j++)
                    measured[j] = program.Commanded[j];
                program.Tick(measured, Dt);
                ticks++;
            }
            return ticks;
        }

        [Test]
        public void Starts_Idle_AndOnlyRunsWhenStarted()
        {
            var program = new ArmProgram(Program(), 90f, 1f);

            Assert.IsFalse(program.IsRunning);
            Assert.IsTrue(program.Start());
            Assert.IsTrue(program.IsRunning);
            Assert.IsFalse(program.Start(), "a second start while running must be refused");
        }

        [Test]
        public void Commanded_RampsAtTheJointSpeedLimit()
        {
            var program = new ArmProgram(Program(), 90f, 1f);
            program.Start();
            var measured = new float[6];

            program.Tick(measured, 0.1d);

            Assert.AreEqual(0f, program.Commanded[0], 1e-4f, "first waypoint is the current pose");
            program.Tick(measured, 0.1d);
            Assert.LessOrEqual(Math.Abs(program.Commanded[0]), 9f + 1e-3f, "never faster than 90 deg/s");
        }

        [Test]
        public void GripCloses_AtPick_AndOpens_AtPlace()
        {
            var program = new ArmProgram(Program(), 180f, 1f);
            program.Start();
            var measured = new float[6];
            bool closedDuringCarry = false;

            while (program.IsRunning)
            {
                for (int j = 0; j < 6; j++)
                    measured[j] = program.Commanded[j];
                program.Tick(measured, Dt);
                if (program.CurrentWaypoint == "place" && program.GripClosed)
                    closedDuringCarry = true;
            }

            Assert.IsTrue(closedDuringCarry, "the part must stay gripped on the way to the place pose");
            Assert.IsFalse(program.GripClosed, "the gripper opens at the place pose");
            Assert.AreEqual(1, program.CompletedCycles);
        }

        [Test]
        public void WaitsForTheJointsToArrive_NotJustTheCommand()
        {
            var program = new ArmProgram(Program(), 180f, 1f);
            program.Start();
            var stuck = new float[6];       // joints never move

            for (int i = 0; i < 500; i++)
                program.Tick(stuck, Dt);

            Assert.IsTrue(program.IsRunning, "must not advance while the joints lag behind");
            Assert.AreEqual("pick", program.CurrentWaypoint);
            Assert.IsFalse(program.GripClosed, "must not grip before the arm has arrived");
        }

        [Test]
        public void Dwell_HoldsAtTheWaypoint()
        {
            var slow = new List<ArmWaypoint>
            {
                new ArmWaypoint("home", Pose(0f), 0f, GripAction.None),
                new ArmWaypoint("pick", Pose(0f), 1f, GripAction.Close),
            };
            var program = new ArmProgram(slow, 180f, 1f);
            program.Start();
            var measured = new float[6];

            for (int i = 0; i < 20; i++)           // 0.4 s < 1 s dwell
                program.Tick(measured, Dt);
            Assert.IsTrue(program.IsRunning);

            for (int i = 0; i < 60; i++)
                program.Tick(measured, Dt);
            Assert.IsFalse(program.IsRunning);
            Assert.AreEqual(1, program.CompletedCycles);
        }

        [Test]
        public void CompletesAndReturnsHome_ThenCanRunAgain()
        {
            var program = new ArmProgram(Program(), 180f, 1f);
            program.Start();
            int ticks = RunToCompletion(program);

            Assert.Less(ticks, 10000);
            Assert.IsFalse(program.IsRunning);
            Assert.AreEqual(0, program.Index, "the program rewinds to the home waypoint");
            Assert.IsTrue(program.Start());
            RunToCompletion(program);
            Assert.AreEqual(2, program.CompletedCycles);
        }

        [Test]
        public void Constructor_RejectsBadInput()
        {
            Assert.Throws<ArgumentException>(() => new ArmProgram(new List<ArmWaypoint>(), 90f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArmProgram(Program(), 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArmProgram(Program(), 90f, 0f));
            Assert.Throws<ArgumentException>(() => new ArmWaypoint("bad", new[] { 1f, 2f }, 0f, GripAction.None));
            Assert.Throws<ArgumentException>(() => new ArmProgram(Program(), 90f, 1f).Tick(new float[3], Dt));
        }
    }
}
