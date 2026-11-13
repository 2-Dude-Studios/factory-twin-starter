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

        // Lockout training (M12): true while anyone other than the trainee is in the CNC's work area. The area check
        // (1910.147(e)(2)(i)) is refused while it is true.
        public static readonly TopicPath TrainingAreaOccupied = new TopicPath("training", "area_occupied");

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

        // The headset hub (M28, out): 1 when the trainee is back at the hub from a training, the cell put back for the
        // next; drill pages, the PPE bench and the hub menu reset themselves on it.
        public static readonly TopicPath TrainingHub = new TopicPath("training", "hub");

        // The LockoutAction the trainee just got wrong, published for every refused action, before training/errors
        // goes up (it does not for a repeat of a mistake already made on that step). Subscribers see the count
        // before they see training/action itself (the controller reacts inside that delivery), so the event log
        // names the mistake from here, and the headset HUD says what is due instead.
        public static readonly TopicPath TrainingMistake = new TopicPath("training", "mistake");
        public static readonly TopicPath TrainingAction = new TopicPath("training", "action");
        public static readonly TopicPath TrainingStep = new TopicPath("training", "step");
        public static readonly TopicPath TrainingErrors = new TopicPath("training", "errors");
        public static readonly TopicPath TrainingResult = new TopicPath("training", "result");

        // Robot cell safety drill (M24). training/robot_level (in) starts a level, 1 to 4 (0 ends the one running);
        // training/robot_step is the count of its required steps done; training/robot_stars the stars of the level
        // just finished (0 failed, 1 to 3 passed), published once at its end.
        public static readonly TopicPath TrainingRobotLevel = new TopicPath("training", "robot_level");
        public static readonly TopicPath TrainingRobotStep = new TopicPath("training", "robot_step");
        public static readonly TopicPath TrainingRobotStars = new TopicPath("training", "robot_stars");

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

        // Fire safety training (M14), a simulated fire for a training drill. fire/scenario (in) starts scenario n
        // (1–6, a FireScenario's number) and 0 puts any fire out and clears it. fire/stage is a FireStage, fire/size
        // and fire/smoke 0–1, fire/class a FireClass, fire/location a FireLocation. cabinet/temp_c is the drive
        // cabinet's temperature, the precursor the dashboard shows before any smoke; cabinet/overheat is true at or
        // above the scenario's overheat level.
        public static readonly TopicPath FireScenario = new TopicPath("fire", "scenario");
        public static readonly TopicPath FireStage = new TopicPath("fire", "stage");
        public static readonly TopicPath FireSize = new TopicPath("fire", "size");
        public static readonly TopicPath FireSmoke = new TopicPath("fire", "smoke");
        public static readonly TopicPath FireClass = new TopicPath("fire", "class");
        public static readonly TopicPath FireLocation = new TopicPath("fire", "location");
        public static readonly TopicPath CabinetTemp = new TopicPath("cabinet", "temp_c");
        public static readonly TopicPath CabinetOverheat = new TopicPath("cabinet", "overheat");

        // Fire alarm panel (M14). Detectors latch fire/smoke_detected and fire/heat_detected; fire/call_point (in) is a
        // manual call point (M16). fire/alarm is the panel's latched alarm, which holds the line; fire/alarm_source
        // an AlarmSource; fire/alarm_reset (in, true) resets it, refused while a detector or call point is active.
        public static readonly TopicPath FireSmokeDetected = new TopicPath("fire", "smoke_detected");
        public static readonly TopicPath FireHeatDetected = new TopicPath("fire", "heat_detected");
        public static readonly TopicPath FireCallPoint = new TopicPath("fire", "call_point");
        public static readonly TopicPath FireAlarm = new TopicPath("fire", "alarm");
        public static readonly TopicPath FireAlarmSource = new TopicPath("fire", "alarm_source");
        public static readonly TopicPath FireAlarmReset = new TopicPath("fire", "alarm_reset");

        // The trainee's extinguisher (M15). fire/agent is the FireAgent in hand (0 none); fire/discharge true while
        // the trigger is held with charge left; fire/charge 0–1 left in it; fire/aim and fire/standoff 0–1, how well
        // the stream meets the base of the fire and how well the distance suits the agent (ExtinguisherRules).
        public static readonly TopicPath FireAgent = new TopicPath("fire", "agent");
        public static readonly TopicPath FireDischarge = new TopicPath("fire", "discharge");
        public static readonly TopicPath FireCharge = new TopicPath("fire", "charge");
        public static readonly TopicPath FireAim = new TopicPath("fire", "aim");
        public static readonly TopicPath FireStandoff = new TopicPath("fire", "standoff");

        // True once the fire is out and soaked, so it cannot reflash (FireModel.Extinguished).
        public static readonly TopicPath FireExtinguished = new TopicPath("fire", "extinguished");

        // Electrical safety training (M19), a simulated cabinet for a training drill. elec/scenario (in) starts
        // scenario n (1–4, an ElectricalScenario's number) and 0 ends it, putting the cabinet back to normal.
        // cabinet/door_open 0/1; cabinet/disconnect is Q1, 0 closed (on) and 1 open (off); cabinet/ups_feed is the UPS
        // feed's breaker in the hall panel, 0 closed and 1 open; cabinet/locked and cabinet/ups_locked count the locks
        // on each. cabinet/bus_v is the main AC bus (L1–L2), all the dashboard knows; cabinet/dc_bus_v the infeed
        // VFD's DC bus; cabinet/control_v the 120 V control circuit, still live from the UPS in E2.
        // infeed/drive_fault is the VFD trip that starts E1 and stops the line.
        public static readonly TopicPath ElecScenario = new TopicPath("elec", "scenario");
        public static readonly TopicPath CabinetDoorOpen = new TopicPath("cabinet", "door_open");
        public static readonly TopicPath CabinetDisconnect = new TopicPath("cabinet", "disconnect");
        public static readonly TopicPath CabinetUpsFeed = new TopicPath("cabinet", "ups_feed");
        public static readonly TopicPath CabinetLocked = new TopicPath("cabinet", "locked");
        public static readonly TopicPath CabinetUpsLocked = new TopicPath("cabinet", "ups_locked");
        public static readonly TopicPath CabinetBusVolts = new TopicPath("cabinet", "bus_v");
        public static readonly TopicPath CabinetDcBusVolts = new TopicPath("cabinet", "dc_bus_v");
        public static readonly TopicPath CabinetControlVolts = new TopicPath("cabinet", "control_v");
        public static readonly TopicPath InfeedDriveFault = new TopicPath("infeed", "drive_fault");

        // Commands to the cabinet (in), carried out by CabinetStation under ElectricalModel's rules, like M11's
        // press/isolator_cmd: door_cmd 1 open, 0 shut; disconnect_cmd and ups_cmd 1 open (off), 0 close (on);
        // lock_cmd and ups_lock_cmd 1 hang one more lock, 0 take one off.
        public static readonly TopicPath CabinetDoorCommand = new TopicPath("cabinet", "door_cmd");
        public static readonly TopicPath CabinetDisconnectCommand = new TopicPath("cabinet", "disconnect_cmd");
        public static readonly TopicPath CabinetUpsCommand = new TopicPath("cabinet", "ups_cmd");
        public static readonly TopicPath CabinetLockCommand = new TopicPath("cabinet", "lock_cmd");
        public static readonly TopicPath CabinetUpsLockCommand = new TopicPath("cabinet", "ups_lock_cmd");

        // A command the cabinet refused (out, M27): the SwitchResult (2 and up), so the headset's tablet can say why
        // nothing moved, as a real handle that will not turn does.
        public static readonly TopicPath CabinetRefused = new TopicPath("cabinet", "refused");

        // The person (M19): elec/boundary is an ApproachBoundary, the deepest point of the trainee's head and hands
        // (0 outside, 1 arc flash, 2 limited, 3 restricted); elec/ppe_cat the PPE category worn (0–4, from M20);
        // elec/tester_ok 0/1, the tester as last proved (from M20). The tester's readings are never published: they
        // go from ElectricalModel straight to the tester's display, so the dashboard cannot give the trainee the answer.
        public static readonly TopicPath ElecBoundary = new TopicPath("elec", "boundary");
        public static readonly TopicPath ElecPpeCategory = new TopicPath("elec", "ppe_cat");
        public static readonly TopicPath ElecTesterOk = new TopicPath("elec", "tester_ok");

        // Gearing up (M27, ahead of M20): elec/gear_up (in) 1 starts the PPE stage at the bay's gear bench (the start
        // menu's Electrical card sends it); elec/ppe_worn (out) how many kit items are on, in order; elec/ppe_refused
        // (out) the index of an item offered out of order. When the kit is complete elec/ppe_cat goes to its category.
        public static readonly TopicPath ElecGearUp = new TopicPath("elec", "gear_up");
        public static readonly TopicPath ElecPpeWorn = new TopicPath("elec", "ppe_worn");
        public static readonly TopicPath ElecPpeRefused = new TopicPath("elec", "ppe_refused");

        // Capture tools (M6): true starts a new take. Parts lost goes back to 0 and the dashboard's event log empties.
        public static readonly TopicPath DemoFreshTake = new TopicPath("demo", "fresh_take");
    }
}
