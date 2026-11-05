using System.Globalization;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Electrical safety: the words the dashboard and the logs use for the cabinet, in one tested place. The dashboard
    /// shows the main bus only: it never names the UPS feed or the control voltage, which is the E2 trap.
    /// </summary>
    public static class ElectricalWords
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Volts(double volts, bool dc = false) =>
            volts.ToString("0", Invariant) + (dc ? " V DC" : " V");

        public static string Disconnect(bool open) => open ? "Q1 OFF (open)" : "Q1 ON";

        public static string Locks(int count) =>
            count <= 0 ? "No lock on Q1" : count == 1 ? "1 lock on Q1" : count.ToString(Invariant) + " locks on Q1";

        /// <summary>The UPS feed's line, shown only once the trainee has acted on it; empty before.</summary>
        public static string UpsFeed(bool open, int locks)
        {
            if (!open && locks <= 0)
                return string.Empty;
            string state = open ? "UPS control feed OFF" : "UPS control feed ON";
            return locks <= 0 ? state : state + " · " + (locks == 1 ? "1 lock" : locks.ToString(Invariant) + " locks");
        }

        public static string Door(bool open) => open ? "Door open" : "Door shut";

        public static string DriveFault(bool fault) => fault ? "Infeed drive U1 tripped · see drive cabinet DCP-1" : "Drives OK";

        public static string Scenario(int number)
        {
            ElectricalScenarioSettings s = ElectricalScenarios.ByNumber(number);
            return s == null ? string.Empty : "E" + s.Number.ToString(Invariant) + " · " + s.Title;
        }

        public static string Boundary(ApproachBoundary depth)
        {
            switch (depth)
            {
                case ApproachBoundary.ArcFlash: return "Inside the arc flash boundary";
                case ApproachBoundary.Limited: return "Inside the limited approach boundary";
                case ApproachBoundary.Restricted: return "Inside the restricted approach boundary";
                default: return "Outside the boundaries";
            }
        }

        public static string Refusal(SwitchResult result)
        {
            switch (result)
            {
                case SwitchResult.RefusedLocked: return "a lock is on it";
                case SwitchResult.RefusedNotOpen: return "a lock fits only in OFF";
                case SwitchResult.RefusedInterlock: return "the door interlock (Q1 must be OFF to open the door, and the door shut to switch Q1 ON)";
                default: return result.ToString();
            }
        }

        /// <summary>The electrical panel shows while anything on the cabinet is out of normal service.</summary>
        public static bool PanelActive(int scenario, bool driveFault, bool disconnectOpen, bool doorOpen, int locks,
            bool upsOpen, int upsLocks) =>
            scenario > 0 || driveFault || disconnectOpen || doorOpen || locks > 0 || upsOpen || upsLocks > 0;
    }
}
