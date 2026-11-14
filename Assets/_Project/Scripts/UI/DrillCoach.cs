using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>What the electrical bay looks like to the trainer's coaching (from the cabinet's topics).</summary>
    public struct CabinetState
    {
        public bool ScenarioRunning;
        /// <summary>The PPE stage at the gear bench (elec/gear_up), before a scenario starts.</summary>
        public bool GearingUp;
        public int PpeWorn;
        public bool Q1Open;
        public int Q1Locks;
        public bool UpsOpen;
        public int UpsLocks;
        public bool DoorOpen;
        public double ControlVolts;
        public double DcBusVolts;
    }

    /// <summary>
    /// The AI trainer's step-by-step coaching (M29): the line for the next thing to do in each drill. The robot cell
    /// and lockout prompt their next required step (not in the robot cell's assessment, which is the test); the fire
    /// drill walks the PASS sequence after the alarm; the electrical bay dresses the trainee item by item, then locks out
    /// the cabinet and checks for a second source before calling it safe. Cue ids match Audio/Trainer and
    /// tools/voice/trainer_lines.json. Pure, so it is tested.
    /// </summary>
    public static class DrillCoach
    {
        /// <summary>Volts below which a circuit counts as dead for the coaching.</summary>
        public const double DeadVolts = 30d;

        public static string Robot(int level, string nextStepId) =>
            level >= 1 && level < DrillLevels.Count && !string.IsNullOrEmpty(nextStepId) ? $"coach_robot_{nextStepId}" : null;

        /// <summary>The lockout's prompt for the step about to be done (its index in the procedure).</summary>
        public static string Lockout(int nextStep) =>
            nextStep >= 0 && nextStep < LockoutProcedure.Steps.Count ? $"coach_lockout_{nextStep}" : null;

        /// <summary>The fire drill after the alarm: the extinguisher and the pin, aim, squeeze and sweep, keep on it.</summary>
        public static string Fire(bool alarm, bool pin, bool hose, bool discharging, bool knockedDown, bool fleeOnly)
        {
            if (fleeOnly || !alarm)
                return null; // the alarm call and the leave call are lines of their own
            if (!pin)
                return "coach_fire_pin";
            if (!hose)
                return "coach_fire_aim";
            if (!discharging)
                return "coach_fire_squeeze";
            return knockedDown ? "coach_fire_keep" : null;
        }

        /// <summary>The electrical bay's next prompt: the PPE in order, then lock out Q1, the second source, the door.</summary>
        public static string Electrical(CabinetState s)
        {
            if (!s.ScenarioRunning && !s.GearingUp)
                return null;
            if (s.PpeWorn < PpeDressing.Kit.Count)
                return $"coach_ppe_{s.PpeWorn + 1}";
            if (!s.ScenarioRunning)
                return null; // dressed, no scenario picked yet: "suited up, over to the cabinet" is its own line
            if (!s.Q1Open)
                return "coach_elec_q1";
            if (s.Q1Locks <= 0)
                return "coach_elec_lock";
            bool control = s.ControlVolts > DeadVolts;
            if (!s.DoorOpen)
                return control && !s.UpsOpen ? "coach_elec_sources" : "coach_elec_door";
            if (control)
                return "coach_elec_backfeed";
            if (s.DcBusVolts > DeadVolts)
                return "coach_elec_dc";
            return "coach_elec_safe";
        }
    }
}
