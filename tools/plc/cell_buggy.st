(* Factory twin — production cell sequence with ONE SEEDED BUG, for the commissioning suite (M8).
   Identical to cell.st except the infeed interlock below; the suite must catch it.

   Original header follows.

   Factory twin — production cell sequence for the soft PLC (OpenPLC Runtime v4).

   The same logic as CellSequence.cs in the Unity project: four states, the pick handshake with re-pulse, the
   transfer timeout and the hold, plus the safety zone's protective stop (M10) and the operator's start and stop
   with the lockout interlock (M11). CellSequence stays the reference this program is tested against (M8).

   I/O over Modbus TCP, as listed in Assets/_Project/Data/PlcIoMap.asset (OpenPLC's Modbus slave layout):
     %MW0..%MW9  holding registers 1024..1033, written by the twin
     %QX0.0..0.3 coils 0..3, read by the twin
     %QW0..%QW4  holding registers 0..4, read by the twin

   States (line_state): 0 feeding, 1 transferring, 2 stalled, 3 held, 4 protective stop, 5 stopped.

   Start and stop arrive as counts (start_count, stop_count): a press is the count going up by exactly one, so it
   cannot fall between two Modbus polls, and a count that jumps (the twin restarting) only moves the baseline. *)

PROGRAM cell
  VAR
    (* Written by the twin *)
    part_at_gate AT %MW0 : UINT;     (* gate1/part_present: light barrier in front of the pick station *)
    arm_busy AT %MW1 : UINT;         (* arm/busy: the arm acknowledged the pick and is moving *)
    part_at_exit AT %MW2 : UINT;     (* outfeed/part_exit: exit sensor *)
    press_health AT %MW3 : UINT;     (* press/health: 0 ok, 1 warning, 2 fault *)
    twin_heartbeat AT %MW4 : UINT;   (* counts up once a second while the twin runs *)
    safety_stop AT %MW5 : UINT;      (* safety/protective_stop: latched by the safety zone, cleared by its reset *)
    start_count AT %MW6 : UINT;      (* line/start_count: +1 per Start press *)
    stop_count AT %MW7 : UINT;       (* line/stop_count: +1 per Stop press *)
    press_isolated AT %MW8 : UINT;   (* press/isolated: the CNC's main isolator is off *)
    press_lockout AT %MW9 : UINT;    (* press/lockout: a lock and tag hang on the isolator *)

    (* Read by the twin *)
    infeed_run AT %QX0.0 : BOOL;     (* infeed/drive_fwd *)
    outfeed_run AT %QX0.1 : BOOL;    (* outfeed/drive_fwd *)
    pick_cmd AT %QX0.2 : BOOL;       (* arm/pick_cmd, rising edge starts a pick *)
    line_stalled AT %QX0.3 : BOOL;   (* line/stalled *)
    line_state AT %QW0 : UINT;       (* line/state *)
    plc_heartbeat AT %QW1 : UINT;    (* plc/heartbeat: +1 every scan *)
    parts_out AT %QW2 : UINT;        (* line/parts_out *)
    cycle_time_cs AT %QW3 : UINT;    (* line/cycle_time_s, in 1/100 s *)
    starts_refused AT %QW4 : UINT;   (* line/starts_refused: starts refused while the CNC was locked out or isolated *)
  END_VAR

  VAR CONSTANT
    FEEDING : UINT := 0;
    TRANSFERRING : UINT := 1;
    STALLED : UINT := 2;
    HELD : UINT := 3;
    PROTECTIVE_STOP : UINT := 4;
    STOPPED : UINT := 5;
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
    first_scan : BOOL := TRUE;
    last_start_count : UINT := 0;
    last_stop_count : UINT := 0;
    start_pressed : BOOL;
    stop_requested : BOOL := FALSE;   (* stop pressed and not yet honoured: a pick under way finishes first *)
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

  hold := press_health >= PRESS_FAULT OR twin_quiet_ms > TWIN_TIMEOUT_MS OR press_isolated <> 0;

  (* Operator commands. A press is the count going up by exactly one; any other change only moves the baseline. *)
  IF first_scan THEN
    last_start_count := start_count; last_stop_count := stop_count; first_scan := FALSE;
  END_IF;
  start_pressed := start_count = last_start_count + 1;
  IF stop_count = last_stop_count + 1 THEN
    stop_requested := TRUE;          (* latched before the protective stop, honoured after its reset *)
  END_IF;
  last_start_count := start_count;
  last_stop_count := stop_count;

  (* Protective stop: the safety zone has already frozen the arm, so stop at once in whatever state the line is in,
     with every output off and the timers paused, and resume that state on the reset. *)
  IF safety_stop <> 0 THEN
    IF state <> PROTECTIVE_STOP THEN
      suspended_state := state; suspended_ms := state_ms;
      state := PROTECTIVE_STOP; state_ms := 0;
    END_IF;
  ELSIF state = PROTECTIVE_STOP THEN
    state := suspended_state; state_ms := suspended_ms;
  END_IF;

  (* Start in STOPPED runs the line, unless the CNC is locked out or isolated: then it is refused and counted. *)
  IF start_pressed AND state = STOPPED THEN
    IF press_lockout <> 0 OR press_isolated <> 0 THEN
      starts_refused := starts_refused + 1;
    ELSE
      stop_requested := FALSE;
      state := FEEDING; state_ms := 0;
    END_IF;
  END_IF;

  IF state <> HELD AND state <> PROTECTIVE_STOP AND state <> STOPPED THEN
    run_ms := run_ms + dt_ms;
  END_IF;
  IF state <> PROTECTIVE_STOP THEN
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
      IF stop_requested THEN
        state := STOPPED; state_ms := 0;
      ELSIF hold THEN
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
        ELSIF stop_requested THEN
          state := STOPPED; state_ms := 0;
        ELSIF hold THEN
          state := HELD; state_ms := 0;
        ELSE
          state := FEEDING; state_ms := 0;
        END_IF;
      END_IF;

    2: (* STALLED *)
      IF stop_requested THEN
        state := STOPPED; state_ms := 0;
      ELSIF part_at_gate = 0 AND arm_busy = 0 THEN
        IF hold THEN
          state := HELD;
        ELSE
          state := FEEDING;
        END_IF;
        state_ms := 0;
      END_IF;

    3: (* HELD *)
      IF stop_requested THEN
        state := STOPPED; state_ms := 0;
      ELSIF NOT hold THEN
        state := FEEDING; state_ms := 0;
      END_IF;

    (* 4, PROTECTIVE_STOP: nothing moves until the reset, handled above.
       5, STOPPED: nothing moves until an allowed start, handled above. *)
  END_CASE;

  (* SEEDED BUG (M8): the infeed restarts as soon as the arm acknowledges the pick, before the arm has left the
     gate. The next part rides into the gate under the arm. The correct line is: infeed_run := state = FEEDING; *)
  infeed_run := state = FEEDING OR (state = TRANSFERRING AND ack_seen);
  outfeed_run := state <> HELD AND state <> PROTECTIVE_STOP AND state <> STOPPED;
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
