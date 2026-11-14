using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class DrillCoachTests
    {
        [Test]
        public void RobotCoachesItsNextStepButNotTheAssessment()
        {
            Assert.AreEqual("coach_robot_warn_in", DrillCoach.Robot(1, RobotCellDrill.StepWarnIn));
            Assert.AreEqual("coach_robot_line_stop", DrillCoach.Robot(2, RobotCellDrill.StepLineStop));
            Assert.IsNull(DrillCoach.Robot(4, RobotCellDrill.StepLineStop), "the assessment is the test");
            Assert.IsNull(DrillCoach.Robot(1, null), "nothing left");
            Assert.IsNull(DrillCoach.Robot(0, RobotCellDrill.StepWarnIn));
        }

        [Test]
        public void LockoutCoachesEveryStepOfTheProcedure()
        {
            Assert.AreEqual("coach_lockout_0", DrillCoach.Lockout(0));
            Assert.AreEqual($"coach_lockout_{LockoutProcedure.Steps.Count - 1}", DrillCoach.Lockout(LockoutProcedure.Steps.Count - 1));
            Assert.IsNull(DrillCoach.Lockout(LockoutProcedure.Steps.Count), "done");
            Assert.IsNull(DrillCoach.Lockout(-1), "not running");
        }

        [Test]
        public void FireWalksThePassSequenceAfterTheAlarm()
        {
            Assert.IsNull(DrillCoach.Fire(false, false, false, false, false, false), "the alarm call is its own line");
            Assert.AreEqual("coach_fire_pin", DrillCoach.Fire(true, false, false, false, false, false));
            Assert.AreEqual("coach_fire_aim", DrillCoach.Fire(true, true, false, false, false, false));
            Assert.AreEqual("coach_fire_squeeze", DrillCoach.Fire(true, true, true, false, false, false));
            Assert.IsNull(DrillCoach.Fire(true, true, true, true, false, false), "sweeping: no nagging");
            Assert.AreEqual("coach_fire_keep", DrillCoach.Fire(true, true, true, true, true, false));
            Assert.IsNull(DrillCoach.Fire(true, false, false, false, false, true), "past incipient: only leave");
        }

        [Test]
        public void ElectricalDressesThenLocksOutThenChecksTheSecondSource()
        {
            var s = new CabinetState { GearingUp = true };
            Assert.AreEqual("coach_ppe_1", DrillCoach.Electrical(s));
            s.PpeWorn = PpeDressing.Kit.Count;
            Assert.IsNull(DrillCoach.Electrical(s), "dressed, no scenario yet");
            s.ScenarioRunning = true;
            s.ControlVolts = 120d;
            Assert.AreEqual("coach_elec_q1", DrillCoach.Electrical(s));
            s.Q1Open = true;
            Assert.AreEqual("coach_elec_lock", DrillCoach.Electrical(s));
            s.Q1Locks = 1;
            Assert.AreEqual("coach_elec_sources", DrillCoach.Electrical(s), "control still live from the UPS");
            s.DoorOpen = true;
            Assert.AreEqual("coach_elec_backfeed", DrillCoach.Electrical(s));
            s.UpsOpen = true;
            s.ControlVolts = 0d;
            s.DcBusVolts = 300d;
            Assert.AreEqual("coach_elec_dc", DrillCoach.Electrical(s));
            s.DcBusVolts = 5d;
            Assert.AreEqual("coach_elec_safe", DrillCoach.Electrical(s));
        }

        [Test]
        public void ElectricalWithNoSecondSourceGoesStraightToTheDoor()
        {
            var s = new CabinetState { ScenarioRunning = true, PpeWorn = PpeDressing.Kit.Count, Q1Open = true, Q1Locks = 1 };
            Assert.AreEqual("coach_elec_door", DrillCoach.Electrical(s));
            Assert.IsNull(DrillCoach.Electrical(default), "nothing running");
        }
    }
}
