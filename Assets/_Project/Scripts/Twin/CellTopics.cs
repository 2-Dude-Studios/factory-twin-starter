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
        public static readonly TopicPath ArmHolding = new TopicPath("arm", "holding");

        // Outfeed conveyor
        public static readonly TopicPath OutfeedDriveForward = new TopicPath("outfeed", "drive_fwd");
        public static readonly TopicPath OutfeedDriveSpeed = new TopicPath("outfeed", "drive_speed");
        public static readonly TopicPath OutfeedRunning = new TopicPath("outfeed", "running");
        public static readonly TopicPath OutfeedPartExit = new TopicPath("outfeed", "part_exit");

        // Press station telemetry. DatasetReplayer builds the same names; tools/replay/replay.py must match too.
        public static readonly TopicPath PressAirTemp = new TopicPath("press", "air_temp_k");
        public static readonly TopicPath PressProcessTemp = new TopicPath("press", "process_temp_k");
        public static readonly TopicPath PressSpeed = new TopicPath("press", "speed_rpm");
        public static readonly TopicPath PressTorque = new TopicPath("press", "torque_nm");
        public static readonly TopicPath PressToolWear = new TopicPath("press", "tool_wear_min");

        // The AI4I product type of the row being machined: a ProductQuality value (0 L, 1 M, 2 H). Sets the
        // overstrain limit. Published first in each row.
        public static readonly TopicPath PressProductQuality = new TopicPath("press", "product_quality");

        // Risk model outputs (M5): failure risk 0–1, tool life left before the failure window (min), act-now flag.
        public static readonly TopicPath PressFailureRisk = new TopicPath("press", "failure_risk");
        public static readonly TopicPath PressToolRemaining = new TopicPath("press", "tool_rul_min");
        public static readonly TopicPath PressMaintenanceDue = new TopicPath("press", "maint_due");

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

        // Degradation command (M5): true starts wearing the tool out on a schedule until it fails, false stops it.
        public static readonly TopicPath PressDegrade = new TopicPath("press", "degrade");

        // Tool change command (M5): true replaces the tool now. Wear restarts from a fresh tool and a tool-wear
        // failure clears; other failure modes are not fixed by a new tool.
        public static readonly TopicPath PressToolChange = new TopicPath("press", "tool_change");

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

        // Soft PLC link (M7). Which logic runs the line: a LogicSource value (0 the in-Unity CellSequencer, 1 the
        // soft PLC). Link is true while the PLC answers and its heartbeat moves; heartbeat is the PLC's scan counter.
        public static readonly TopicPath PlcSource = new TopicPath("plc", "source");
        public static readonly TopicPath PlcLink = new TopicPath("plc", "link");
        public static readonly TopicPath PlcHeartbeat = new TopicPath("plc", "heartbeat");

        // Simulation clock (M8): Time.timeScale, and simulated seconds per wall-clock second over the last second.
        // A soft PLC counts its timers in wall-clock time, so commissioning results only count while the ratio is ~1.
        public static readonly TopicPath SimTimeScale = new TopicPath("sim", "time_scale");
        public static readonly TopicPath SimRealtimeRatio = new TopicPath("sim", "realtime_ratio");

        // Commissioning (M8). Stimuli: gate1/force is a ForceMode (0 none, 1 off, 2 on) on gate1/part_present;
        // gate1/clear removes the part in the gate (operator clears a jam); arm/inhibit makes the arm ignore pick
        // commands; arm/drop_part makes the gripper let go; plc/simulate_loss stops PlcLink talking to the PLC.
        // vc/run = 1 starts the suite; vc/test is the scenario under test (1-based, 0 idle); vc/result is each
        // finished scenario's ScenarioOutcome.
        public static readonly TopicPath GateForce = new TopicPath("gate1", "force");
        public static readonly TopicPath GateClear = new TopicPath("gate1", "clear");
        public static readonly TopicPath ArmInhibit = new TopicPath("arm", "inhibit");
        public static readonly TopicPath ArmDropPart = new TopicPath("arm", "drop_part");
        public static readonly TopicPath PlcSimulateLoss = new TopicPath("plc", "simulate_loss");
        public static readonly TopicPath VcRun = new TopicPath("vc", "run");
        public static readonly TopicPath VcTest = new TopicPath("vc", "test");
        public static readonly TopicPath VcResult = new TopicPath("vc", "result");

        // Safety zone around the arm (M10), a simulated safety function, not safety-rated. zone_warning and
        // arm_zone_occupied are the two sensor volumes (true while a person is inside). protective_stop is the
        // latched stop: set on entering the protective zone, cleared only by safety/reset (in, true) with that zone
        // clear. safety/state is a SafetyState value for the dashboard.
        public static readonly TopicPath SafetyZoneWarning = new TopicPath("safety", "zone_warning");
        public static readonly TopicPath SafetyArmZoneOccupied = new TopicPath("safety", "arm_zone_occupied");
        public static readonly TopicPath SafetyProtectiveStop = new TopicPath("safety", "protective_stop");
        public static readonly TopicPath SafetyReset = new TopicPath("safety", "reset");
        public static readonly TopicPath SafetyState = new TopicPath("safety", "state");

        // Commissioning stimulus (M10): a ForceMode on both zone sensors, standing in for a person. On puts a person in
        // the protective zone (and so in the warning zone too); Off blinds both sensors; None hands them back.
        public static readonly TopicPath SafetyZoneForce = new TopicPath("safety", "zone_force");

        // Commissioning stimulus (M12): where the suite sends the zone walker, a simulated worker on the Trainee layer
        // whom the real zone sensors see (a WorkerWalk: 0 home outside both zones, 1 into the warning zone only,
        // 2 into the protective zone). Unlike zone_force, nothing is forced.
        public static readonly TopicPath SafetyWorkerWalk = new TopicPath("safety", "worker_walk");

        // The arm's speed as a fraction of full speed (0–1), written by the safety zone and read by the arm through
        // its realvirtual signal: 1 normal, reduced in the warning zone, 0 on a protective stop.
        public static readonly TopicPath ArmSpeedScale = new TopicPath("arm", "speed_scale");

        // Lockout training (M11). press/isolated and press/lockout are the CNC's main isolator (true when off) and
        // whether a lock and tag hang on it. training/action (in) is a LockoutAction, one per thing the trainee does;
        // training/step is the 1-based step due (0 before a run, steps + 1 when done), training/errors the mistakes
        // so far, training/result a TrainingOutcome.
        public static readonly TopicPath PressIsolated = new TopicPath("press", "isolated");
        public static readonly TopicPath PressLockout = new TopicPath("press", "lockout");

        // Commands to the CNC's isolator (1 off, 0 on) and lock (1 apply, 0 remove), carried out by PressIsolation
        // under the hasp's rules. training/run = 1 starts a new lockout run for the training record.
        public static readonly TopicPath PressIsolatorCommand = new TopicPath("press", "isolator_cmd");
        public static readonly TopicPath PressLockCommand = new TopicPath("press", "lock_cmd");
        public static readonly TopicPath TrainingRun = new TopicPath("training", "run");

        // The LockoutAction the trainee just got wrong, published before training/errors goes up. Subscribers see the
        // count before they see training/action itself (the controller reacts inside that delivery), so the event
        // log names the mistake from here.
        public static readonly TopicPath TrainingMistake = new TopicPath("training", "mistake");
        public static readonly TopicPath TrainingAction = new TopicPath("training", "action");
        public static readonly TopicPath TrainingStep = new TopicPath("training", "step");
        public static readonly TopicPath TrainingErrors = new TopicPath("training", "errors");
        public static readonly TopicPath TrainingResult = new TopicPath("training", "result");

        // Line start and stop (M11). line/start and line/stop (in) are button presses: each true is one press.
        // LineCommands counts them on line/start_count and line/stop_count, which is what both logic sources read: a
        // press is the count going up by exactly one, so a press cannot fall between two Modbus polls, and a count
        // that jumps (a restart) is not a press. line/starts_refused counts starts refused while the CNC was locked
        // out or isolated.
        public static readonly TopicPath LineStart = new TopicPath("line", "start");
        public static readonly TopicPath LineStop = new TopicPath("line", "stop");
        public static readonly TopicPath LineStartCount = new TopicPath("line", "start_count");
        public static readonly TopicPath LineStopCount = new TopicPath("line", "stop_count");
        public static readonly TopicPath LineStartsRefused = new TopicPath("line", "starts_refused");

        // Capture tools (M6): true starts a new take. Parts lost goes back to 0 and the dashboard's event log empties.
        public static readonly TopicPath DemoFreshTake = new TopicPath("demo", "fresh_take");
    }
}
