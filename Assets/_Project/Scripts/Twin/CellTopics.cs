using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Production cell: every topic the cell loop reads or writes, in one place.</summary>
    public static class CellTopics
    {
        // Infeed conveyor
        public static readonly TopicPath InfeedDriveForward = new TopicPath("infeed", "drive_fwd");
        public static readonly TopicPath InfeedDriveSpeed = new TopicPath("infeed", "drive_speed");
        public static readonly TopicPath InfeedRunning = new TopicPath("infeed", "running");
        public static readonly TopicPath InfeedSpeed = new TopicPath("infeed", "speed_mm_s");

        // Sensor gate in front of the pick station
        public static readonly TopicPath GatePartPresent = new TopicPath("gate1", "part_present");

        // Pick station (placeholder transfer in M1, six-axis arm from M2)
        public static readonly TopicPath ArmPickCommand = new TopicPath("arm", "pick_cmd");
        public static readonly TopicPath ArmBusy = new TopicPath("arm", "busy");

        // Outfeed conveyor
        public static readonly TopicPath OutfeedDriveForward = new TopicPath("outfeed", "drive_fwd");
        public static readonly TopicPath OutfeedDriveSpeed = new TopicPath("outfeed", "drive_speed");
        public static readonly TopicPath OutfeedRunning = new TopicPath("outfeed", "running");
        public static readonly TopicPath OutfeedPartExit = new TopicPath("outfeed", "part_exit");

        // Press station (AI4I telemetry). The line holds while the press reports any of these.
        public static readonly TopicPath PressMachineFailure = new TopicPath("press", "machine_failure");
        public static readonly IReadOnlyList<TopicPath> PressFaults = new[]
        {
            new TopicPath("press", "fault_twf"),
            new TopicPath("press", "fault_hdf"),
            new TopicPath("press", "fault_pwf"),
            new TopicPath("press", "fault_osf"),
            new TopicPath("press", "fault_rnf"),
        };

        // Press health for the dashboard and the 3D press: a MachineHealth value (0 ok, 1 warning, 2 fault)
        public static readonly TopicPath PressHealth = new TopicPath("press", "health");

        // Fault injector command: a FaultMode value (1 TWF, 2 HDF, 4 PWF, 8 OSF, 16 RNF; 0 clears)
        public static readonly TopicPath PressInjectFault = new TopicPath("press", "inject_fault");

        // Broker link, published by the MQTT bridge so the dashboard can show the connection
        public static readonly TopicPath LinkConnected = new TopicPath("link", "connected");

        // Line-level status published by the sequencer
        public static readonly TopicPath LineState = new TopicPath("line", "state");
        public static readonly TopicPath LineStalled = new TopicPath("line", "stalled");
        public static readonly TopicPath LinePartsOut = new TopicPath("line", "parts_out");
        public static readonly TopicPath LineCycleTime = new TopicPath("line", "cycle_time_s");

        // Line figures derived by LineMetricsReporter for the dashboard
        public static readonly TopicPath LinePartsPerHour = new TopicPath("line", "parts_per_hour");
        public static readonly TopicPath LinePartsLost = new TopicPath("line", "parts_lost");
    }
}
