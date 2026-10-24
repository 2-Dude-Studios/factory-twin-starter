(* Factory twin — production cell sequence for the soft PLC (OpenPLC Runtime v4).

   The same logic as CellSequence.cs in the Unity project: four states, the pick handshake with re-pulse, the
   transfer timeout and the hold, plus the safety zone's protective stop (M10). CellSequence stays the reference
   this program is tested against (M8).

   I/O over Modbus TCP, as listed in Assets/_Project/Data/PlcIoMap.asset (OpenPLC's Modbus slave layout):
     %MW0..%MW5  holding registers 1024..1029, written by the twin
     %QX0.0..0.3 coils 0..3, read by the twin
     %QW0..%QW3  holding registers 0..3, read by the twin

   States (line_state): 0 feeding, 1 transferring, 2 stalled, 3 held, 4 protective stop. *)

PROGRAM cell
  VAR
    (* Written by the twin *)
    part_at_gate AT %MW0 : UINT;     (* gate1/part_present: light barrier in front of the pick station *)
    arm_busy AT %MW1 : UINT;         (* arm/busy: the arm acknowledged the pick and is moving *)
    part_at_exit AT %MW2 : UINT;     (* outfeed/part_exit: exit sensor *)
    press_health AT %MW3 : UINT;     (* press/health: 0 ok, 1 warning, 2 fault *)
    twin_heartbeat AT %MW4 : UINT;   (* counts up once a second while the twin runs *)
    safety_stop AT %MW5 : UINT;      (* safety/protective_stop: latched by the safety zone, cleared by its reset *)

    (* Read by the twin *)
    infeed_run AT %QX0.0 : BOOL;     (* infeed/drive_fwd *)
    outfeed_run AT %QX0.1 : BOOL;    (* outfeed/drive_fwd *)
    pick_cmd AT %QX0.2 : BOOL;       (* arm/pick_cmd, rising edge starts a pick *)
    line_stalled AT %QX0.3 : BOOL;   (* line/stalled *)
    line_state AT %QW0 : UINT;       (* line/state *)
    plc_heartbeat AT %QW1 : UINT;    (* plc/heartbeat: +1 every scan *)
    parts_out AT %QW2 : UINT;        (* line/parts_out *)
    cycle_time_cs AT %QW3 : UINT;    (* line/cycle_time_s, in 1/100 s *)
  END_VAR

  VAR CONSTANT
    FEEDING : UINT := 0;
    TRANSFERRING : UINT := 1;
    STALLED : UINT := 2;
    HELD : UINT := 3;
    SAFETY_STOP : UINT := 4;
    ACK_TIMEOUT_MS : DINT := 2000;       (* no busy from the arm: drop the command one scan and raise it again *)
    TRANSFER_TIMEOUT_MS : DINT := 15000; (* pick not finished: stall the line *)
    TWIN_TIMEOUT_MS : DINT := 3000;      (* twin heartbeat frozen: hold the line *)
    PRESS_FAULT : UINT := 2;
  END_VAR

  VAR
    state : UINT := 0;
    scan_clock : TON;
    et_ms : DINT;
    last_et_ms : DINT := 0;
    dt_ms : DINT;
    state_ms : DINT := 0;
    since_cmd_ms : DINT := 0;
    ack_seen : BOOL := FALSE;
    repulse : BOOL := FALSE;
    last_exit : BOOL := FALSE;
    run_ms : DINT := 0;        (* time not held; cycle time is measured on this clock *)
    last_exit_ms : DINT := -1;
    last_twin_heartbeat : UINT := 0;
    twin_quiet_ms : DINT := 0;
    hold : BOOL;
    suspended_state : UINT := 0;   (* the state a protective stop interrupted, and how long it had been in it *)
    suspended_ms : DINT := 0;
  END_VAR

  (* Scan time from a free-running 10 s timer: ET counts up, resets to 0 for one scan, starts again. *)
  scan_clock(IN := NOT scan_clock.Q, PT := T#10s);
  et_ms := TIME_TO_DINT(scan_clock.ET);
  IF et_ms >= last_et_ms THEN
    dt_ms := et_ms - last_et_ms;
  ELSE
    dt_ms := et_ms + 10000 - last_et_ms;
  END_IF;
  last_et_ms := et_ms;

  plc_heartbeat := plc_heartbeat + 1;

  (* The twin's heartbeat: if it stops, nothing it reports can be trusted, so hold. *)
  IF twin_heartbeat <> last_twin_heartbeat THEN
    last_twin_heartbeat := twin_heartbeat;
    twin_quiet_ms := 0;
  ELSE
    twin_quiet_ms := twin_quiet_ms + dt_ms;
  END_IF;

  hold := press_health >= PRESS_FAULT OR twin_quiet_ms > TWIN_TIMEOUT_MS;

  (* Protective stop: the safety zone has already frozen the arm, so stop at once in whatever state the line is in,
     with every output off and the timers paused, and resume that state on the reset. *)
  IF safety_stop <> 0 THEN
    IF state <> SAFETY_STOP THEN
      suspended_state := state; suspended_ms := state_ms;
      state := SAFETY_STOP; state_ms := 0;
    END_IF;
  ELSIF state = SAFETY_STOP THEN
    state := suspended_state; state_ms := suspended_ms;
  END_IF;

  IF state <> HELD AND state <> SAFETY_STOP THEN
    run_ms := run_ms + dt_ms;
  END_IF;
  IF state <> SAFETY_STOP THEN
    state_ms := state_ms + dt_ms;
  END_IF;

  (* Count parts on the rising edge of the exit sensor. *)
  IF part_at_exit <> 0 AND NOT last_exit THEN
    parts_out := parts_out + 1;
    IF last_exit_ms >= 0 THEN
      cycle_time_cs := DINT_TO_UINT((run_ms - last_exit_ms) / 10);
    END_IF;
    last_exit_ms := run_ms;
  END_IF;
  last_exit := part_at_exit <> 0;

  (* Literal labels: not every IEC compiler accepts named constants as CASE labels. *)
  CASE state OF
    0: (* FEEDING *)
      IF hold THEN
        state := HELD; state_ms := 0;
      ELSIF part_at_gate <> 0 THEN
        state := TRANSFERRING; state_ms := 0; ack_seen := FALSE; since_cmd_ms := 0;
      END_IF;

    1: (* TRANSFERRING *)
      IF state_ms > TRANSFER_TIMEOUT_MS THEN
        state := STALLED; state_ms := 0;
      ELSIF NOT ack_seen THEN
        IF arm_busy <> 0 THEN
          ack_seen := TRUE;
        ELSE
          since_cmd_ms := since_cmd_ms + dt_ms;
          IF since_cmd_ms > ACK_TIMEOUT_MS THEN
            repulse := TRUE;          (* one scan low, so the arm sees a fresh rising edge *)
            since_cmd_ms := 0;
          END_IF;
        END_IF;
      ELSIF arm_busy = 0 THEN
        IF part_at_gate <> 0 THEN
          (* The arm is done but the part is still in the gate: the pick missed, try again. *)
          state := TRANSFERRING; state_ms := 0; ack_seen := FALSE; since_cmd_ms := 0;
        ELSIF hold THEN
          state := HELD; state_ms := 0;
        ELSE
          state := FEEDING; state_ms := 0;
        END_IF;
      END_IF;

    2: (* STALLED *)
      IF part_at_gate = 0 AND arm_busy = 0 THEN
        IF hold THEN
          state := HELD;
        ELSE
          state := FEEDING;
        END_IF;
        state_ms := 0;
      END_IF;

    3: (* HELD *)
      IF NOT hold THEN
        state := FEEDING; state_ms := 0;
      END_IF;

    (* 4, SAFETY_STOP: nothing moves until the reset, handled above. *)
  END_CASE;

  infeed_run := state = FEEDING;
  outfeed_run := state <> HELD AND state <> SAFETY_STOP;
  pick_cmd := state = TRANSFERRING AND NOT ack_seen AND NOT repulse;
  line_stalled := state = STALLED;
  line_state := state;
  repulse := FALSE;
END_PROGRAM

CONFIGURATION config
  RESOURCE res ON PLC
    TASK main_task(INTERVAL := T#20ms, PRIORITY := 0);   (* the Editor project uses task0 at 20 ms *)
    PROGRAM instance0 WITH main_task : cell;
  END_RESOURCE
END_CONFIGURATION
