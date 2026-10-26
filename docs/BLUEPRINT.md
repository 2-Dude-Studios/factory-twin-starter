# Factory Twin — project blueprint

As of 2026-10-24, commit `0c3b0fc`. Both use cases are built: predictive maintenance (M4–M6, video approved) and virtual commissioning (M7–M8 closed, M9 waiting on the voiceover). A third, workforce training and safety (Rev 5, M10–M13), was approved on 2026-10-24; its safety zone (M10) closed the same day and the lockout procedure (M11) on 2026-10-26; the Quest 3 view (M12) is next. Written from the code, `Cell.unity` and the docs in this repo. Where they disagree, the code is described and the difference is listed in section 13.

Companion documents: `docs/build-plan.md` (why and in what order, Rev 5), `docs/STATUS.md` (running log and decisions), `docs/demo-script.md` (video beats), `docs/voiceover.md` and `docs/voiceover_vc*.md` (scripts and cut plans), `docs/LICENSES.md` (what may ship), `CLAUDE.md` (rules for working in the repo).

---

## 1. Objective

### What we are creating

A Unity prototype of one working production cell: an infeed conveyor, a sensor gate, a six-axis pick-and-place arm, an outfeed conveyor, and a CNC machining centre reporting machine telemetry. Every drive, sensor and joint publishes its state over MQTT to a live dashboard, and the cell can be run either by its own C# sequence or by a soft PLC over Modbus TCP.

It proves two use cases, each with its own video, and is adding a third:

- **Predictive maintenance.** The twin sees a CNC tool failure coming, shows where it is and what it will cost the line, and recommends a tool change in time. Ignored, the machine fails and the line stops; acted on, the line never stops.
- **Virtual commissioning.** A soft PLC outside Unity runs the cell from Structured Text. A commissioning suite catches a seeded interlock bug, visible in 3D as parts queuing into the pick station, and passes once the one line is fixed.
- **Workforce training and safety** (Rev 5, planned). A trainee stepping into the arm's protective zone freezes the arm and holds the line until a deliberate reset. A CNC fault is cleared only through the OSHA 1910.147 lockout steps; the soft PLC refuses to start the line while the lock is on, and every step and error is logged as a training record. Desktop first, then Quest 3 over Link. A simulated safety function and a training demo: not safety-rated, not certified training.

It ships as a Windows build for live demos and a walkthrough video per use case for the Manufacturing page on 2dudestudios.com.

### What it is solving

**For the studio.** 2 Dude Studios is selling factory digital twins ahead of its first manufacturing project. A prospect looking at a reel cannot tell a factory that was rendered from one that is connected to plant data and plant logic, and the second is what they would pay for. This build is the evidence: the 3D scene, the dashboard and the PLC all work from the same signals, over the protocols a real plant uses (MQTT, Modbus TCP).

**For a maintenance or plant manager.** A failure seen about half a minute ahead in the demo (hours of machining compressed into a minute), on the machine where it will happen, with the parts it will cost the line. The route to their own machines: their sensors over MQTT or OPC UA, their failure history, the same twin.

**For a machine builder or system integrator.** The PLC program tested against the twin before any hardware exists: forced sensors, a dropped part, an arm that never answers, a machine fault, a lost PLC link, each graded pass or fail, with an I/O list exported the way commissioning engineers work. A logic bug found on day 1, not in commissioning week on site.

**For an EHS or training manager (Rev 5).** New starters practise lockout and robot-zone behaviour on the plant's own cell, driven by its own PLC, with a record of every step and mistake, before they stand next to the real machine.

### Not in scope

- Client work. This is the studio's own marketing asset; a client project on this codebase needs a Unity Industry seat and a fresh licence pass.
- A plant connection. CNC telemetry is replayed from a public dataset (AI4I 2020). The PLC is OpenPLC, not a vendor PLC or fieldbus.
- A whole plant. One cell, built well.
- Three other use cases (full monitoring, layout what-if, pre-planning), standalone Quest 3 and the kiosk view, until a prospect asks. See "Later" in `docs/build-plan.md`.

### Where it stands

| Milestone | Scope | State |
|---|---|---|
| M0 Setup | Project, URP, third-party imports, broker | Done |
| M1 Cell loop | Infeed → gate → pick → outfeed, 10-minute soak | Done |
| M2 Arm | De-branded UR5e, baked pick and place | Done |
| M3 Data spine | Signal bus, MQTT bridge, CNC telemetry, fault injector | Done 2026-10-12 |
| M4 Dashboard | UI Toolkit dashboard, CNC machine and stack light, fault beat on camera | Done 2026-10-20 |
| M5 Prediction | Degradation mode, failure risk, tool life, recommendation, tool change | Done 2026-10-20 |
| M6 Polish and capture | Camera rig, VFX, audio, Windows build, PdM video v02 (111 s, approved) | Done 2026-10-21 |
| M7 Soft PLC link | OpenPLC Runtime v4 runs the cell over Modbus TCP; logic-source switch; watchdogs | Done 2026-10-23 |
| M8 Commissioning suite | Six scenarios against either logic source, I/O list, seeded bug caught | Done 2026-10-23 |
| M9 VC capture | Takes D and E, 76 s picture-only rough cut | **Waiting on your recorded voiceover** |
| M10 Safety zone | Warning and protective zones, arm freeze, reset, PLC hold, first-person trainee, two suite scenarios | Done 2026-10-24 |
| M11 Lockout procedure | 1910.147 steps on the CNC, PLC refuses start with the lock on, training record, trainee points and step panel | Done 2026-10-26 |
| M12 Quest 3 view | OpenXR + XR Interaction Toolkit over Link, same signals | **Current** (Rev 5) |
| M13 Training capture | Training clip | Planned |

---

## 2. System at a glance

```
                               Unity process (Cell.unity)
 ┌───────────────────────────────────────────────────────────────────────────────────────┐
 │  3D simulation                    Logic                          Presentation          │
 │  ┌─────────────────────────┐      ┌──────────────────────────┐   ┌──────────────────┐  │
 │  │ Source → Infeed         │      │ CellSequencer ─┐ one of   │   │ Dashboard        │  │
 │  │ Gate1 light barrier     │      │ PlcLink ───────┘ the two  │   │ Commissioning    │  │
 │  │ Arm (6 ArticulationBody)│      │ PressStation + replayer   │   │ Stack light, VFX │  │
 │  │ Outfeed → Exit → Sink   │      │ Risk / health reporters   │   │ Capture rig      │  │
 │  │ CNC machine (prefab)    │      │ CommissioningSuite        │   └────────┬─────────┘  │
 │  └───────────┬─────────────┘      └────────────┬─────────────┘            │            │
 │              │ 19 realvirtual signals          │                          │            │
 │       ┌──────┴──────┐                          │                          │            │
 │       │ TwinSignals │◄──────────────── SignalBus ─────────────────────────┘            │
 │       │ + force tbl │            cell/<station>/<signal>, last value kept              │
 │       └─────────────┘                    │                       │                     │
 │                                 SignalPublisher            PlcLink poll thread         │
 │                                  + MqttBridge               (50 Hz, own socket)        │
 └──────────────────────────────────────────┼───────────────────────┼─────────────────────┘
                                            │ MQTT 5, TCP 1883       │ Modbus TCP, port 502
                                  ┌─────────┴─────────┐   ┌──────────┴────────────┐
                                  │ Mosquitto 2       │   │ OpenPLC Runtime v4    │
                                  │ (Docker)          │   │ cell.st / cell_buggy  │
                                  └─────────┬─────────┘   └───────────────────────┘
                              mosquitto_sub/pub, replay.py,
                              any other MQTT client
```

Three ideas hold the design together:

1. **Signals are the only bridge.** Nothing reads a transform to learn the state of the cell. The simulation exposes realvirtual `Signal` components; `TwinSignals` mirrors them to and from the bus; everything else talks to the bus.
2. **The bus is the single source of truth inside Unity.** The broker mirrors it and the PLC reads and writes a slice of it. Remove the broker and the cell still runs. Remove the PLC and `CellSequencer` takes the line back.
3. **Decisions live in plain C# classes; MonoBehaviours only host them.** The cell sequence, arm program, risk model, line metrics, Modbus codec, PLC image, watchdog, scenarios and their checks have no Unity dependency and carry the tests.

---

## 3. The signal contract

### Topic and payload

- Topic: `cell/<station>/<signal>`. Exactly three levels. Segments are lower-case letters, digits and `_` only (`TopicPath` enforces this).
- Payload: `{"v": <number|true|false>, "t": <unix_ms>}`. No strings, no nesting, no NaN or Infinity. `SignalPayload` is a strict hand-written parser.
- Subscriptions use MQTT wildcards; `TopicFilter` implements them for the in-process bus.
- QoS 0, not retained. A client that connects mid-run sees a signal only when it next changes.
- Enumerations travel as numbers: `line/state` 0 feeding, 1 transferring, 2 stalled, 3 held; `press/health` 0 ok, 1 warning, 2 fault; `plc/source` 0 Unity, 1 PLC.

### Topic catalogue (83 topics)

**out** is state leaving the cell; **in** is a command or stimulus the cell obeys. Commands are visible on the broker too. The station is called `press` in topics and code; on screen it is a CNC machining centre (AI4I models milling).

**Production cell**, the 19 topics bound to realvirtual signals in `TwinSignals`:

| Topic | Type | Dir | Written by | Meaning |
|---|---|---|---|---|
| `infeed/drive_fwd` | bool | in | Logic source | Run the infeed belt |
| `infeed/drive_speed` | number | in | Scene value or broker | Belt speed setpoint, mm/s |
| `infeed/running`, `infeed/speed_mm_s` | bool, number | out | realvirtual drive | Belt moving; measured speed |
| `gate1/part_present` | bool | out | Gate1 sensor (or the force table) | Part in the light barrier |
| `arm/pick_cmd` | bool | in | Logic source | Rising edge starts one pick and place |
| `arm/busy` | bool | out | ArmSignalBridge | Arm mid-cycle; the pick acknowledgement |
| `arm/grip_cmd` | bool | in | ArmSignalBridge | Close the gripper |
| `arm/holding` | bool | out | PartGripper | A part is on the flange |
| `arm/j1_deg` … `j6_deg` | number | out | ArmSignalBridge | Measured joint angles |
| `outfeed/drive_fwd` | bool | in | Logic source | Run the outfeed belt |
| `outfeed/drive_speed` | number | in | Scene value or broker | Belt speed setpoint, mm/s |
| `outfeed/running` | bool | out | realvirtual drive | Belt moving |
| `outfeed/part_exit` | bool | out | Exit sensor | Part leaving; counted on the rising edge |

**Line**:

| Topic | Type | Dir | Written by | Meaning |
|---|---|---|---|---|
| `line/state` | number | out | Logic source | 0 feeding, 1 transferring, 2 stalled, 3 held, 4 protective stop (M10), 5 stopped (M11) |
| `line/stalled` | bool | out | Logic source | State 2 |
| `line/parts_out` | number | out | Logic source | Parts that left the cell this run |
| `line/cycle_time_s` | number | out | Logic source | Seconds between the last two exits, held time left out |
| `line/parts_per_hour` | number | out | LineMetricsReporter | Exits over a 120 s rolling window |
| `line/parts_lost` | number | out | LineMetricsReporter | Parts not made while held for a CNC fault, at the pre-hold rate |

**CNC (`press`)**:

| Topic | Type | Dir | Written by | Meaning |
|---|---|---|---|---|
| `press/product_quality` | number | out | DatasetReplayer | Row's product type, 0 L, 1 M, 2 H; sets the overstrain limit. Published first in each row |
| `press/air_temp_k`, `process_temp_k`, `speed_rpm`, `torque_nm`, `tool_wear_min` | number | out | DatasetReplayer | AI4I telemetry |
| `press/fault_twf`, `_hdf`, `_pwf`, `_osf`, `_rnf` | bool | out | DatasetReplayer | The five AI4I failure modes |
| `press/machine_failure` | bool | out | DatasetReplayer | The dataset's overall failure flag |
| `press/failure_risk` | number | out | PressRiskReporter | Threshold-margin risk, 0–1 |
| `press/tool_rul_min` | number | out | PressRiskReporter | Tool wear left before the 200 min failure window |
| `press/maint_due` | bool | out | PressRiskReporter | Risk at warning, or ≤ 20 min tool life left |
| `press/health` | number | out | PressHealthReporter | 0 ok, 1 warning (on at risk 0.5, off below 0.4), 2 fault |
| `press/degrade` | bool | in | Dashboard, broker, PressDegradation | Start or stop wearing the tool out over about 75 s |
| `press/tool_change` | bool | in | Dashboard, broker, ToolChanger | Fit a fresh tool |
| `press/inject_fault` | number | in | Dashboard, broker, FaultInjector | `FaultMode` bitmask 1 TWF, 2 HDF, 4 PWF, 8 OSF, 16 RNF; 0 clears |

**Soft PLC, simulation clock and commissioning**:

| Topic | Type | Dir | Written by | Meaning |
|---|---|---|---|---|
| `plc/source` | number | in | PlcLink (start value, and 0 when disabled), TakeSequencer, broker | Who runs the line: 0 `CellSequencer`, 1 soft PLC |
| `plc/link` | bool | out | PlcLink | PLC answers and its heartbeat moves |
| `plc/heartbeat` | number | out | PlcLink | PLC scan counter |
| `plc/simulate_loss` | bool | in | CommissioningSuite | PlcLink stops talking to the PLC |
| `sim/time_scale` | number | out | SimClockReporter | `Time.timeScale` |
| `sim/realtime_ratio` | number | out | SimClockReporter | Simulated seconds per wall-clock second, once a second |
| `gate1/force` | number | in | CommissioningSuite | Force `gate1/part_present`: 0 none, 1 off, 2 on |
| `gate1/clear` | bool | in | CommissioningSuite | Rising edge: JamClearer lifts the part out of the gate |
| `arm/inhibit` | bool | in | CommissioningSuite | Arm ignores pick commands |
| `arm/drop_part` | bool | in | CommissioningSuite | Gripper lets go of its part |
| `vc/run` | number | in | TakeSequencer, broker | 1 runs the suite; 2 runs it and stops at the first failure. The panel's Run tests button calls the suite directly |
| `vc/test` | number | out | CommissioningSuite | Scenario under test, 1-based; 0 idle |
| `vc/result` | number | out | CommissioningSuite | Each finished scenario's outcome |

**Link and capture**: `link/connected` (bool, out, MqttBridge: broker link up) and `demo/fresh_take` (bool, in, TakeControls: parts lost back to 0, event log emptied).

"Logic source" means `CellSequencer` or, while `plc/source` = 1, `PlcLink` publishing what the PLC decided. `CellTopics` holds every constant; the CNC telemetry names are also built in `DatasetReplayer` and must stay identical to `tools/replay/replay.py`.


**Safety zone (M10)**, a simulated safety function, not safety-rated:

| Topic | Type | Dir | Written by | Meaning |
|---|---|---|---|---|
| `safety/zone_warning` | bool | out | WarningZone sensor (or the force table) | A person is in the warning zone |
| `safety/arm_zone_occupied` | bool | out | ProtectiveZone sensor (or the force table) | A person is in the protective zone |
| `safety/protective_stop` | bool | out | SafetyZoneController | Latched stop; to the PLC as `safety_stop` (`%MW5`) |
| `safety/state` | number | out | SafetyZoneController | 0 normal, 1 reduced, 2 stopped, 3 awaiting reset |
| `safety/reset` | bool | in | Dashboard, F6, suite | Each true is one press; accepted only with the protective zone clear |
| `arm/speed_scale` | number | in | SafetyZoneController | Arm speed fraction: 1, 0.25 in the warning zone, 0 on the stop |
| `safety/zone_force` | number | in | Commissioning suite | ForceMode on both zone sensors (a person) |

**Lockout training (M11)**, a training demo, not certified training:

| Topic | Type | Dir | Written by | Meaning |
|---|---|---|---|---|
| `line/start`, `line/stop` | bool | in | Training, dashboard, F7/F8, suite | Each true is one press |
| `line/start_count`, `line/stop_count` | number | out | LineCommands | Press counts both logic sources read; a press is +1 (to the PLC as `%MW6`, `%MW7`) |
| `line/starts_refused` | number | out | Logic source | Starts refused while the CNC was locked out or isolated (`%QW4`) |
| `press/isolator_cmd`, `press/lock_cmd` | number | in | TrainingController | 1 off / apply, 0 on / remove, under the hasp rules |
| `press/isolated`, `press/lockout` | bool | out | PressIsolation | Isolator off; lock and tag on (to the PLC as `%MW8`, `%MW9`) |
| `training/run` | bool | in | Dashboard button | Starts a lockout run |
| `training/action` | number | in | TrainingPoint (a click) | A LockoutAction |
| `training/step` | number | out | TrainingController | Step due, 1–12 (0 before a run, 13 when done) |
| `training/errors` | number | out | TrainingController | Mistakes so far |
| `training/mistake` | number | out | TrainingController | The LockoutAction just judged a mistake, published before the count |
| `training/result` | number | out | TrainingController | TrainingOutcome: 0 idle, 1 in progress, 2 passed, 3 passed with mistakes |

---

## 4. Runtime architecture

### Layers and who may talk to whom

```
 Layer 4  Presentation   Dashboard, Commissioning panel, StackLight,     reads the bus; buttons publish commands
                         FaultEffects, capture rig
 Layer 3  Logic          CellSequencer or PlcLink, PressStation,         reads the bus, publishes to it
                         reporters, FaultInjector, PressDegradation,
                         ToolChanger, CommissioningSuite
 Layer 2  Data spine     SignalBus, SignalPublisher, MqttBridge,         the bus and its two mirrors
                         Modbus client, TwinConfig, CellContext
 Layer 1  Bridge         TwinSignals, ArmSignalBridge, PartGripper,      the only code that references realvirtual
                         JamClearer
 Layer 0  Simulation     realvirtual drives, sensors, parts; PhysX;      third-party, never edited
                         ArmController on ArticulationBodies
```

The assembly definitions enforce it: `TwoDude.FactoryTwin.Twin`, `.Robot` and `.UI` have no reference to realvirtual. Only `TwoDude.FactoryTwin.Twin.Realvirtual` references `realvirtual.base`.

### Who runs the line

```
                     plc/source = 0                        plc/source = 1
 ┌───────────────────────────────────────┐  ┌──────────────────────────────────────────────┐
 │ CellSequencer ticks CellSequence and   │  │ CellSequencer stands aside (PlcLink enabled   │
 │ publishes drive, pick and line topics. │  │ and source says PLC). PlcLink publishes the   │
 │ PlcLink still polls: the PLC shadows   │  │ PLC's coils and registers as those topics.    │
 │ the line and counts parts, drives      │  │ PLC heartbeat quiet > 0.5 s → PlcLink stops   │
 │ nothing.                               │  │ both belts, drops the pick, line state held.  │
 └───────────────────────────────────────┘  └──────────────────────────────────────────────┘
```

Watchdogs run both ways. The twin holds the line if the PLC's heartbeat stops for 0.5 s (`TwinConfig`). `cell.st` holds the line if the twin's heartbeat register stops for 3 s. Disabling `PlcLink` hands the line back to Unity, which starts a fresh sequence.

### Components in the scene

| Object | Component | Order | Responsibility |
|---|---|---|---|
| `Cell` | `CellContext` | −1000 | Owns the bus, the `TwinConfig` reference and the force table |
| `Cell` | `MqttBridge` | −200 | MQTT client, reconnect loop, inbound queue replayed on the main thread |
| `Cell` | `TwinSignals` | −100 | Mirrors the 19 realvirtual signals; applies forced values to inputs |
| `Cell` | `CellSequencer` | 100 | Ticks `CellSequence` unless the PLC runs the line |
| `Cell` | `PlcLink` | 100 | Modbus poll thread, PLC outputs onto the bus, heartbeat watchdog |
| `Cell` | `LineMetricsReporter` | 110 | Parts per hour and parts lost |
| `Cell` | `CommissioningSuite` | 150 | Plays scenarios, grades checks, writes the CSV report |
| `Cell` | `SignalForcer` | 0 | `gate1/force` → force table |
| `Cell` | `SimClockReporter` | 0 | Time scale and real-time ratio, warns while the PLC runs and the ratio drifts |
| `Cell/Signals/*` | realvirtual `PLCInput*` / `PLCOutput*` | — | One object per signal, named `<station>.<signal>` |
| `Cell/Press` | `PressStation` | 0 | Parses `ai4i2020.csv`, hosts `DatasetReplayer`, 2 rows/s, calm healthy baseline |
| `Cell/Press` | `FaultInjector` | 0 | `press/inject_fault` → the CNC's fault filter |
| `Cell/Press` | `PressDegradation` | −10 | Runs `DegradationScenario`, overrides published tool wear, hands over to real TWF rows |
| `Cell/Press` | `ToolChanger` | 0 | `press/tool_change` → fresh tool, tool-wear failure cleared |
| `Cell/Press` | `PressRiskReporter` | 40 | Telemetry → `FailureRiskModel` → risk topics |
| `Cell/Press` | `PressHealthReporter` | 50 | Faults and risk warning → `press/health` |
| `Cell/Press/PressMachine` | realvirtual `CNC.prefab` | — | The machine shown for the station, at (−0.3, 0.37, 3.3) |
| `Cell/Press/StackLight` | `StackLight` | 0 | Green / amber / red flashing at 1.5 Hz, plus a glow light on the door |
| `Cell/Press/FaultFx` | `FaultEffects` | 0 | Sparks, steam and spark crackle while health is Fault |
| `Cell/HallAmbience` | `AudioSource` | — | Factory ambience loop |
| `[Gate1]` | `JamClearer` | 0 | `gate1/clear` → removes the part in the gate |
| `[PartSource]`, `[Infeed]`, `[Outfeed]` | `VisibleLayerCopy` | 0 | Draws parts and belt surfaces on a layer the sample's renderer shows |
| `Arm` | `ArmController` | 0 | Joints, baked program, drive gains |
| `Arm` | `ArmSignalBridge` | 50 | Pick edge → arm (ignored under `arm/inhibit`); busy, grip, joints → signals |
| `Arm/…/tool0` | `PartGripper` | 0 | Grips the part at the gate; releases on open or `arm/drop_part` |
| `[PickStation]` | `PartTransfer` | — | M1 placeholder, **disabled** |
| `Dashboard` | `UIDocument`, `DashboardController` | 200 | `Dashboard.uxml`; CNC and line panels, event log, buttons |
| `Dashboard` | `TakeControls`, `FpsReadout`, `LicenseTextsView` | 0, 210, 220 | Capture keys, FPS readout and `-logfps`, full licence texts in Credits |
| `Commissioning` | `UIDocument`, `CommissioningPanel` | 200 | Scenario rows, logic source, program, code card, Run tests |
| `CaptureRig` | `ShotDirector`, `TakeSequencer`; `HallPath` (`SplinePath`); `Shot1_HallDolly` (`DollyMove`) … `Shot6_PickStation` | 0 | Six Cinemachine 3 shots and scripted takes for Unity Recorder |

### One physics tick

Execution order gives a fixed read → act → decide sequence in every `FixedUpdate`:

1. `TwinSignals` (−100) reads the 19 signals, substitutes forced values, publishes what changed.
2. `ArmController` and `PartGripper` (0) move the arm and act on the grip command.
3. `ArmSignalBridge` (50) passes a pick edge to the arm and writes its feedback to signals.
4. `CellSequencer` or `PlcLink` (100) publishes the logic source's commands. `TwinSignals` writes them into the simulation inside the publish call.
5. `LineMetricsReporter` (110) and `CommissioningSuite` (150) observe the result.

Arm feedback reaches the bus one fixed step late, which the 2 s acknowledgement timeout absorbs. The PLC adds its own latency: one 50 Hz poll plus one 20 ms PLC scan.

### The two links

**MQTT** (`MqttBridge`, `SignalPublisher`). A topic leaves only when it has moved past the deadband and at most 10 times a second; changes inside the window coalesce. While the broker is down the latest value per topic is kept, and the whole cell is resent on reconnect. Inbound uses MQTT 5 no-local, and an inbound message is never echoed back. One warning when the broker is unreachable, then quiet retries.

**Modbus TCP** (`PlcLink`, `ModbusClient`, `ModbusTcp`, `PlcImage`). Our own client, no package: a pure frame codec for the four function codes needed, and a blocking socket client on one background thread per enable. Each poll writes the sensor block (holding registers 1024–1028) and reads the command coils 0–3 and status registers 0–3. Values cross to the main thread under a lock. The map lives in `Data/PlcIoMap.asset` and matches the located variables in `cell.st`:

| Tag | Address | Direction | Topic |
|---|---|---|---|
| `part_at_gate`, `arm_busy`, `part_at_exit`, `press_health` | `%MW0`–`%MW3`, registers 1024–1027 | twin → PLC | `gate1/part_present`, `arm/busy`, `outfeed/part_exit`, `press/health` |
| `twin_heartbeat` | `%MW4`, register 1028 | twin → PLC | counts once a second |
| `infeed_run`, `outfeed_run`, `pick_cmd`, `line_stalled` | `%QX0.0`–`%QX0.3`, coils 0–3 | PLC → twin | `infeed/drive_fwd`, `outfeed/drive_fwd`, `arm/pick_cmd`, `line/stalled` |
| `line_state`, `plc_heartbeat`, `parts_out`, `cycle_time_cs` | `%QW0`–`%QW3`, registers 0–3 | PLC → twin | `line/state`, `plc/heartbeat`, `line/parts_out`, `line/cycle_time_s` (×0.01) |

The twin writes only holding registers, because Modbus cannot write `%IX` inputs. Host, port, unit id, poll rate (50 Hz), watchdog (0.5 s) and I/O timeout come from `TwinConfig`.

---

## 5. Control logic

### Cell sequence (the C# PLC, and the reference for `cell.st`)

```
                  part at gate                    arm done, gate clear
      ┌─────────┐ ───────────► ┌──────────────┐ ─────────────────────► (Feeding)
      │ Feeding │              │ Transferring │
      │   (0)   │ ◄─────────── │     (1)      │ ── arm done, part still at gate: retry
      └────┬────┘              └──────┬───────┘
      hold │  ▲ hold cleared          │ no finish within 15 s
           ▼  │                       ▼
      ┌─────────┐              ┌──────────────┐
      │  Held   │ ◄─ if hold ─ │   Stalled    │ ── gate clear and arm idle ──► Feeding
      │   (3)   │              │     (2)      │
      └─────────┘              └──────────────┘
```

| State | Infeed | Outfeed | Pick command |
|---|---|---|---|
| Feeding | run | run | off |
| Transferring | stop | run | on until the arm reports busy |
| Stalled | stop | run | off |
| Held | stop | stop | off |

- **Handshake.** No `busy` within 2 s → the command drops for one tick and rises again.
- **Hold.** While the CNC is faulted (`PressHealthRule.IsFaulted`). A pick under way finishes first.
- **Counting.** Parts out on the rising edge of `outfeed/part_exit`; cycle time on a running clock that leaves out held time.

`tools/plc/cell.st` is the same machine in Structured Text: four states, handshake with re-pulse, 15 s transfer timeout, hold on `press_health` = 2, plus the twin-heartbeat check. Its key line is `infeed_run := state = FEEDING;` (line 148).

### Arm program

Eight baked poses (`home → pick_approach → pick (close) → lift → place_approach → place (open) → retreat → home_end`), joints ramped at 110 °/s, each pose done only when the measured joints are within 1.5° and the dwell has passed. Poses were solved offline from the UR5e's closed-form kinematics; there is no runtime IK, so a layout change means re-baking.

### Predictive maintenance chain

```
 Wear tool out (key 1, button, press/degrade)
   │
   ▼
 PressDegradation: tool wear 160 → 215 min over 75 s; calm rows keep the other modes quiet
   │
   ▼
 PressRiskReporter → FailureRiskModel: margins to AI4I's published limits
   │   tool wear from 180 min (window opens at 200)   overstrain from 90 % of wear × torque limit
   │   power below 3800 W or above 8700 W             heat: within 0.5 K and 50 rpm of both limits
   │   risk = the highest mode; tool life = minutes to 200; maint_due at warning or ≤ 20 min left
   ▼
 PressHealthReporter: Warning at risk ≥ 0.5 (clears < 0.4) ──► amber lamp, "Tool change due", "CNC warning"
   │
   ├── act: Change tool (key 2) → ToolChanger → fresh tool row, risk 0 → line never stops, 0 parts lost
   │
   └── ignore: at 215 min the real TWF failure rows arrive → health Fault → red flashing lamp, sparks
              → line holds after the current pick → parts lost counts at the pre-hold rate
```

Measured (M5 exit): warning 34 s before the failure; tool change at the recommendation, 0 parts lost. Against all 10,000 AI4I rows (nothing is trained, so every row is held out): all 330 rows labelled TWF, HDF, PWF or OSF are warned; process-margin alarms on 2.4 % of healthy rows. AI4I's failures follow published rules, so margins to those rules catch them; the demo's value is the lead time and acting on it.

Injected faults (`press/inject_fault`, key 3 clears) still jump straight to failure rows, for the detection beat and the commissioning suite.

### Commissioning suite

`CommissioningSuite` runs six scenarios against whichever logic source runs the line, waiting for a running line before each. Every scenario also checks both conveyor interlocks and that the plant ran in real time, because the PLC's timers count wall-clock time.

| Scenario | Stimulus | Must happen |
|---|---|---|
| Normal run (60 s) | none | ≥ 6 parts out, no stall, no hold |
| Stuck gate sensor (50 s) | gate forced on for 25 s | infeed stays stopped while forced; feeds again within 15 s of release |
| Part dropped (45 s) | gripper lets go once it holds a part | no stall, no hold, ≥ 3 parts out |
| Arm not ready (50 s) | arm ignores picks for 30 s, then the jam is cleared | stalls within 28 s; feeds again within 12 s of clearing |
| CNC fault (30 s) | tool-wear fault for 15 s | holds within 9 s, belts stay stopped, runs within 6 s of clearing |
| PLC link lost (20 s, PLC only) | twin stops talking to the PLC for 6 s | holds within 1.5 s, belts stay stopped, runs within 4 s of the link returning |

Base checks in every scenario: "Interlock: infeed stops for a part at the gate", "Interlock: infeed stops while the arm works" (both 0.3 s grace), "Plant ran in real time" (ratio within ±10 %).

`vc/run` = 2 stops at the first failed check, as an engineer would on site. Results go to `vc/test`, `vc/result`, the Commissioning panel and `Builds/Commissioning/vc_<source>_<time>.csv`.

**The seeded bug.** `tools/plc/cell_buggy.st` differs from `cell.st` in one line: `infeed_run := state = FEEDING OR (state = TRANSFERRING AND ack_seen);`. The infeed restarts as soon as the arm acknowledges the pick, so the next part rides into the gate under the arm. The suite fails the interlock checks from 1.5 s; in 3D, parts queue nose-to-tail into the pick station and parts out stops climbing.

| Logic source | Program | Result (M8, 2026-10-23) |
|---|---|---|
| Unity `CellSequencer` | — | 5 passed, PLC link lost not applicable |
| Soft PLC | `cell.st` | 6 of 6 passed |
| Soft PLC | `cell_buggy.st` | all 6 failed; 0 of 6 parts out in the normal run |

---

## 6. Code map

All project code is under `Assets/_Project/`: about 7,800 lines of runtime and editor C#, 2,700 lines of tests and 700 lines of UXML/USS.

### Assemblies

| Assembly | Folder | References | Contains |
|---|---|---|---|
| `TwoDude.FactoryTwin.Twin` | `Scripts/Twin/` (with `Mqtt/`, `Plc/`, `Commissioning/`) | MQTTnet DLL | Data spine, cell logic, CNC, risk, PLC link, commissioning |
| `TwoDude.FactoryTwin.Robot` | `Scripts/Robot/` | — | Arm program and controller |
| `TwoDude.FactoryTwin.Twin.Realvirtual` | `Scripts/Twin/Realvirtual/` | Twin, Robot, `realvirtual.base` | The bridge |
| `TwoDude.FactoryTwin.UI` | `Scripts/UI/` | Twin, Input System, Cinemachine, Splines, Mathematics | Dashboard, commissioning panel, capture tools |
| `TwoDude.FactoryTwin.Twin.Editor` | `Scripts/Twin/Editor/` | Editor only | Settings guard, licence-file copy, I/O list export |
| `TwoDude.FactoryTwin.UI.Editor` | `Scripts/UI/Editor/` | UI, Unity Recorder (Editor only) | `TakeRecorderHook` |
| `TwoDude.FactoryTwin.Robot.Editor` | `Scripts/Robot/Editor/` | URDF-Importer (Editor only) | `ArmImporter` |
| `TwoDude.FactoryTwin.Tests.EditMode` | `Tests/EditMode/` | Twin, Robot, UI, NUnit | 228 tests |

### Files by area

**Data spine** (`Twin/`): `TopicPath`, `TopicFilter`, `SignalValue` (value and message), `SignalPayload` (JSON), `SignalBus` (pub/sub with last value), `SignalPublisher` (rate limit, deadband, resend, no echo), `Mqtt/MqttBridge`, `TwinConfig` (broker and PLC settings), `CellContext`, `CellTopics`, `FrameRateLock`.

**Cell** (`Twin/`): `CellSequence` (the C# PLC), `CellSequencer`, `LineMetrics` + `LineMetricsReporter`, `SimClock` + `SimClockReporter`, `VisibleLayerCopy`.

**CNC and prediction** (`Twin/`): `Ai4iDataset` (`FaultMode`, `ProductQuality`, `Ai4iRow`, CSV parser), `DatasetReplayer` (pacing, fault filter, calm baseline, tool-wear override, seek fresh tool), `PressStation`, `FaultInjector`, `DegradationScenario` + `PressDegradation`, `ToolChanger`, `FailureRiskModel` + `PressRiskConfig` + `PressRiskReporter`, `PressHealthRule` + `PressHealthReporter`, `StackLightPattern` + `StackLight`, `FaultEffects`.

**Soft PLC** (`Twin/Plc/`): `ModbusTcp` (pure codec), `ModbusClient` (blocking socket), `PlcImage` (I/O map ↔ Modbus blocks ↔ bus values), `PlcWatchdog`, `PlcIoMap` (ScriptableObject), `PlcIoList` (CSV rows), `LogicSource` + `LogicSourceRule`, `PlcLink`.

**Commissioning** (`Twin/Commissioning/`): `CommissioningScenarios` (the six, as data), `CommissioningScenario` + `ScenarioRun` (pure engine), `ScenarioCheck` (never, within, parts-out), `CellSnapshot`, `CommissioningReport` (CSV), `CommissioningSuite`, `SignalForces` (force table) + `SignalForcer`.

**Bridge** (`Twin/Realvirtual/`): `TwinSignals`, `ArmSignalBridge`, `PartGripper`, `JamClearer`, `PartTransfer` (disabled).

**Robot**: `ArmProgram`, `ArmController`; Editor `ArmImporter`.

**UI** (`UI/`): `DashboardViewModel` (bus → bindable text, series, log), `DashboardController`, `EventLog`, `SparkSeries`, `Sparkline` (Painter2D element), `CommissioningPanel`, `LicenseTextsView`, `TakeControls`, `TakeSequencer`, `ShotDirector`, `DollyMove`, `SplinePath`, `FpsMeter` + `FpsReadout`; Editor `TakeRecorderHook`.

**Editor** (`Twin/Editor/`): `ProjectSettingsGuard` (only `Cell.unity` in the build, Run In Background on, realvirtual layers present), `LicenseFilesPostprocessor` (licence texts next to the .exe), `PlcIoListExporter` (Tools > Factory Twin > Export PLC I-O List).

### Other project assets

| Path | What |
|---|---|
| `Scenes/Cell.unity` | The only scene; a Save As of the Factory Training hall with the cell on its keep-clear zone |
| `UI/` | `Dashboard.uxml/.uss`, `Commissioning.uxml/.uss`, `Credits.uxml`, `DashboardPanelSettings.asset` (1920×1080 reference), `DashboardTheme.tss` |
| `Data/` | `TwinConfig.asset`, `PressRiskConfig.asset`, `PlcIoMap.asset` (13 points), `ai4i2020.csv` |
| `Licenses/` | Six notice files shown under Credits and copied next to the build |
| `Audio/` | Factory ambience and spark crackle (CC0, Freesound) |
| `Materials/` | `StackLightLamp.mat` |
| `Robots/ur/` | De-branded UR5e URDF and meshes, BSD-3 licence, provenance README |
| `Settings/` | Stock URP assets, unused fallback; the live pipeline is the sample's `PC_RPAsset_DC` |
| `Prefabs/` | Empty |

---

## 7. Scene layout

```
     z
     ▲                                   CNC machining centre (−0.3, 0.37, 3.3), stack light on the roof
     │
 1.2 │  Sink ◄ ExitSensor ◄═══ Outfeed ═══ [place] ── Arm base (−2.0, 0.6, 1.2) on a 0.6 m stand
     │                                                    │
 0.65│                                                 [pick] ◄ Gate1 ◄═══ Infeed ═══ PartSource
     └──────────────────────────────────────────────────────────────────────────────────► x
```

- The cell sits on the hall's hatched keep-clear zone at (−5…0.5, −1…3.9); both belts flow toward −x; the arm swings about 90° between pick and place, each 0.55 m from its base.
- Parts are realvirtual `MU`s spawned every 400 mm of belt travel. A part rests at the gate at x = −1.71, centre 0.575 m: measured values the arm poses depend on.
- The Factory Training renderer draws layers 0–7 and 9 only, so realvirtual's part and belt layers are invisible in the Game view; `VisibleLayerCopy` draws a collider-free copy on Default.
- Capture shots (keys): 4 hall dolly on a spline, 5 cell, 6 line close, 7 CNC with stack light, 8 CNC control panel, 9 back to the operator camera; shot 6 (pick station close-up) is used by the VC takes.
- realvirtual's runtime toolbar and start-up info box are switched off on its controller.

---

## 8. Tooling outside Unity

| Tool | Path | Purpose |
|---|---|---|
| Mosquitto 2 broker | `tools/broker/` | `docker compose up -d`; container `factory-twin-broker`, MQTT 1883, WebSockets 9001, anonymous, LAN only |
| Dataset replayer | `tools/replay/replay.py` | The CNC topics straight to the broker, including `product_quality`. Don't run it with `PressStation` enabled |
| OpenPLC Runtime v4.3.1 | studio PC, not in the repo | The soft PLC; Modbus TCP slave on 502, 20 ms task |
| OpenPLC Editor v4.3.2 | studio PC, not in the repo | Compiles and uploads the program (runtime v4 has no web upload) |
| PLC project | `tools/plc/` | `cell.st`, `cell_buggy.st`, `openplc_project/`, `sync_project.py` (writes `main.st` from either program), `io_list.csv` |
| Video, PdM | `tools/video/` | `script_v2.py`, `tts2.py` (Kokoro voice), `build2.py` (v02 cut and mix), `build3.py` (v03: mid screens, subtitles, highlighters, 126.5 s) |
| Video, VC | `tools/video/vc/build_vc.py` | Cuts takes D and E, time-lapses the passing run, adds the I/O list card; places your voice blocks and mixes to −14 LUFS |
| Unity Recorder 5.1.7 | package, Editor only | Records the takes `TakeSequencer` plays |
| UnitySkills | package, `localhost:8090` | Lets Claude Code drive the Editor; dev tooling, not shipped |
| Claude Code | `.claude/` | `/milestone`, `/test`, `/licence-check`; write permissions limited to `Assets/_Project`, `docs`, `tools` |

---

## 9. Stack and licensing

| Layer | Item | Version | In git | Notes |
|---|---|---|---|---|
| Engine | Unity | 6000.3.19f1 | settings | URP 17.3.0, Linear, the sample's Deferred+ pipeline |
| Base scene | Factory Training sample | 1.0 | no | Ships only inside a build |
| Twin components | realvirtual Starter | 6.0.11 | no | Compiled-only distribution; CNC prefab used for the station |
| MQTT | MQTTnet | 4.3.7 | yes, LFS | MIT; 5.x cannot load in Unity |
| Robot | UR5e `ur_description` | 39ad110 | yes | BSD-3, de-branded |
| Robot import | URDF-Importer | 0.5.2 | manifest | Apache-2.0 |
| Telemetry | AI4I 2020 | 10,000 rows | yes | CC BY 4.0, attribution with changes in `Licenses/01` |
| Soft PLC | OpenPLC Runtime v4.3.1, Editor v4.3.2 | | no | MIT runtime; GPL-3.0 Editor used as a tool only. **Not yet on the licence register** |
| VFX, audio | Particle Pack 3.1; two Freesound clips; two whooshes in `tools/video/sfx` | | partly | Asset Store EULA; CC0 |
| Voice | Kokoro-82M, `am_michael` | 1.0 | no | Apache-2.0 weights; PdM video only. The VC clip uses your own voice |
| Packages chosen | Splines 2.9.0, Cinemachine 3.1.7, Input System 1.19.0, Recorder 5.1.7, Test Framework 1.6.0 | | manifest | Cinemachine is the 3.x API |
| Imported, unused | Fast IK 1.2 | | no | Runtime IK was never needed |

Obligations, all met in the current build: Asset Store content only inside a build; realvirtual never shared; Credits carry AI4I, UR5e, URDF-Importer and MQTTnet, with full licence texts in the app and in `Builds/Windows/Licenses/`; no vendor branding on the arm or the CNC. A client project still needs a Unity Industry seat and a fresh licence pass.

---

## 10. Tests and verification

228 EditMode tests (counted from the test attributes; the last recorded Editor run was 225/225 at M8), all against the pure classes:

| Area | Files | Tests |
|---|---|---|
| Topics and payload | `TopicTests`, `SignalPayloadTests` | 67 |
| Bus and publisher | `SignalBusTests`, `SignalPublisherTests` | 21 |
| Dashboard model | `DashboardModelTests` | 22 |
| Soft PLC link | `PlcLinkTests` (codec, image, watchdog, loopback socket) | 19 |
| Commissioning | `CommissioningTests` | 19 |
| Dataset replayer | `DatasetReplayerTests` | 14 |
| Cell sequence | `CellSequenceTests` | 12 |
| Risk and health | `FailureRiskModelTests`, `PressHealthRuleTests`, `DegradationScenarioTests` | 23 |
| Line, clock, lamp | `LineMetricsTests`, `SimClockTests`, `StackLightPatternTests` | 16 |
| Capture tools | `TakeToolsTests` | 8 |
| Arm | `ArmProgramTests` | 7 |

One test holds the AI4I hit rate: every labelled TWF/HDF/PWF/OSF row warned, process alarms rare. Not covered by tests: `MqttBridge`, `TwinSignals`, the arm bridge and gripper, and Play mode. Those are proven by the exit runs:

| Run | Result |
|---|---|
| M1, 10 min, placeholder pick | 143 parts, 4.58 s cycle, 0 stalls |
| M2, 5.6 min, arm | 7.0 s cycle, about 516 parts/h, 0 retries, 0 stalls |
| M3, 80 s, broker | 36 topics live; a fault held the line and it recovered |
| M4, fault beat at 1080p | CNC red, line stopped, parts lost counted; clear restores all |
| M5, degradation | warning 34 s ahead; tool change → 0 parts lost |
| M6, Windows build | 60.0 fps, worst frame 16.7–18.9 ms after warm-up |
| M7, 12.4 min on the PLC | 0 stalls; PLC stop held the line in 0.85 s; clean rerun 516 parts/h |
| M8, suite | `cell.st` 6/6, `CellSequencer` 5/5, `cell_buggy.st` caught |

---

## 11. Deliverables

| Deliverable | Where | State |
|---|---|---|
| Windows build | `Builds/Windows/FactoryTwin.exe` (git-ignored) | Last build recorded in `STATUS.md`: 2026-10-20, with the capture features; 60 fps lock confirmed. That predates the PLC and commissioning work |
| PdM video v02 | `Builds/Recordings/2DS_Manufacturing_FactoryTwin_PdM_v02_20261021.mp4` | 111 s, approved |
| PdM video v03 | `tools/video/build3.py` | Script committed (126.5 s cut with mid screens and subtitles); no rendered or approved v03 is recorded in `STATUS.md` |
| VC takes D and E | `Builds/Recordings/2DS_FactoryTwin_Take{D,E}_20261023_*.mp4` | Bug caught (46 s); fix passes 6/6 (330 s) |
| VC rough cut | `…_VC_roughcut_picture_20261023.mp4` | 76.1 s, picture only, voice-block guides burned in |
| VC video v01 | — | Waiting on your six voice blocks |

---

## 12. What is left

**To close M9.** Record `block1.wav` … `block6.wav` from `docs/voiceover_vc_script.md`, then run `tools/video/vc/build_vc.py --voice-dir <folder> --logo "<logo>" <output>` and approve the clip.

**Open items from the status log.**
- Rebuild the Windows build so the live demo includes the PLC link and the commissioning panel.
- Player Settings still say "factory-twin-starter" / "DefaultCompany" with a 1024×768 default; decide on "Factory Twin" / "2 Dude Studios" at 1920×1080.
- OpenPLC showed high latency and many overruns after a stop and start; the suite was unaffected. Restart the runtime before a live demo.
- Manual cleanup: `Assets/Screenshots/`. (The deactivated training objects are already gone from `Cell.unity`; they went with the M2 commit.)

**Rev 5, workforce training and safety (M10–M13, ≈10 days).** Safety zone, lockout procedure, Quest 3 view over Link, training clip. Exit conditions in `docs/build-plan.md`.

**Later.** Full monitoring (hover, health per station, OEE, retained MQTT), layout what-if (runtime IK), pre-planning, kiosk view, standalone Quest 3, OPC UA on the first client project that asks.

---

## 13. Gaps, mismatches and risks

Open:

| # | Finding | Why it matters |
|---|---|---|
| 1 | **OpenPLC is not on the licence register.** `STATUS.md` says the line would be added; `LICENSES.md` has no OpenPLC row. | The Runtime (MIT) and the GPL-3.0 Editor are part of the VC demo and appear in the video. Add both rows before the clip ships. |
| 2 | **The demo script's VC section is the pre-M8 draft.** It says "Stuck sensor" or "missing part" fails and parts pile up at the gate. What happens: the seeded bug fails the interlock checks from the normal run onward, and parts queue into the pick station. Its PdM section still marks M5 signals as not existing yet. | The demo script is the reference for anyone re-cutting the videos. `docs/voiceover_vc.md` has the as-built beats. |
| 3 | **The arm bake script is not in the repo.** `STATUS.md` points at `scratchpad/ur5e_kin.py`. | The poses cannot be regenerated after a layout change; the parked layout what-if work needs it first. |
| 4 | **Uncommitted settings drift.** UnitySkills is now v2.9.1 in the manifest (register says v2.8.4), and `ProjectSettings/EditorSettings.asset` carries an Editor serialisation upgrade. | Commit or revert both; the register must match the manifest. |
| 5 | **The last recorded live-demo build predates M7–M9** (2026-10-20). | A prospect meeting on that .exe shows predictive maintenance only. |
| 6 | **`/test` names Editor 6000.3.0f1**; the project is on 6000.3.19f1. | The headless command fails as written. |
| 7 | **Stray `Assets/Temp.meta` and `Assets/UnitySkillsTests_Temp.meta`** remain untracked. | Delete or ignore. |
| 8 | **`Prefabs/` is empty**; everything lives in one scene that realvirtual dirties on load. | Diffs are noisy; extracting the cell, arm and CNC would help reuse. |
| 9 | **Bridge components read realvirtual directly.** `PartGripper` and now `JamClearer` use `Sensor` and `MU`, while `.claude/rules/unity-csharp.md` says everything goes through `TwinSignals`. | Contained in the bridge assembly, so the layering holds; reword the rule or the code. |
| 10 | **`docs/build-plan.md`'s repo layout** omits `Scripts/Twin/Plc`, `Commissioning`, `UI/` assets and `tools/video`. | Minor; the plan reads as older than the code. |

Resolved since the first blueprint: no CNC in 3D (CNC prefab and stack light, M4); demo signals that did not exist (M4–M5 added them); undefined risk (threshold margins, M5); OEE parked under Later.

Risks carried from `STATUS.md`:

- **Commissioning only counts in real time.** The PLC's timers run on wall-clock time; a frame-rate hitch can make simulated time race ahead (one burst seen in the M7 soak). Every scenario fails if the ratio leaves ±10 %, and VC takes record at a variable frame rate for that reason.
- **The PLC's counters are not retained.** After a PLC stop, its parts out restarts from 1.
- MQTT is not retained, so late-joining clients miss steady signals.
- The sequencer, or the PLC, owns the drive topics; an outside write is overwritten within a cycle.
- `replay.py` replays the dataset's own failure rows, each of which holds the line.
- The broker and the Modbus link are unauthenticated and unencrypted; fine on the studio LAN only.
- Re-importing a project-style Asset Store package can overwrite `ProjectSettings/`.
- realvirtual 6.0.11 reports 6000.3.19f1 as outside its tested range; nothing seen so far.

---

## 14. Running it

**Predictive maintenance.**

1. Optional broker: `docker compose -f tools/broker/docker-compose.yml up -d`.
2. Open `Cell.unity` in Unity 6000.3.19f1 and press Play. Let the line run about 2.5 minutes, so parts per hour settles near 510 and parts lost has a rate to count against.
3. Keys: **1** wear the tool out, **2** change the tool, **3** clear faults, **0** fresh take; **4**–**9** camera shots; **F9** FPS readout, **F10** cursor.

**Virtual commissioning.**

1. Start OpenPLC Runtime; `python tools/plc/sync_project.py` (or `sync_project.py cell_buggy.st <project dir>` for the bug); in OpenPLC Editor open `tools/plc/openplc_project/`, Connect, Build and upload.
2. Play in Unity. The PLC shadows the line at once. Publish `cell/plc/source` = 1 (command below), or set Start Source on `Cell > PlcLink`, to hand it the line.
3. Press Run tests on the Commissioning panel, or publish `cell/vc/run` = 1 (2 stops at the first failure). The report lands in `Builds/Commissioning/`.

Watch everything on the broker:

```bash
docker exec factory-twin-broker mosquitto_sub -t 'cell/#' -v
```

Hand the line to the PLC from outside Unity:

```bash
docker exec factory-twin-broker mosquitto_pub -t cell/plc/source -m '{"v":1,"t":0}'
```
