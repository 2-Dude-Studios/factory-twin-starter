# Factory Twin — project blueprint

As of 2026-10-12, commit `40aa013` (M3 closed, M4 next). Written from the code, the scene file and the docs in this repo; where the docs and the code disagree, the code is described and the difference is listed in section 12. Objective, milestones and remaining work revised 2026-10-19 for build plan Rev 3 (predictive maintenance only, M4–M6); the code sections are unchanged.

Companion documents: `docs/build-plan.md` (why and in what order), `docs/STATUS.md` (running log and decisions), `docs/demo-script.md` (the 90-second video), `docs/LICENSES.md` (what may ship), `CLAUDE.md` (rules for working in the repo).

---

## 1. Objective

### What we are creating

A Unity prototype of one working production cell: an infeed conveyor, a sensor gate, a six-axis pick-and-place arm, an outfeed conveyor, and a press reporting machine telemetry. Every drive, sensor and joint publishes its state over MQTT to a live dashboard.

It proves one use case, **predictive maintenance**: the twin sees a press failure coming, shows where it is and what it will cost the line, and recommends a tool change in time. It was chosen as the most-asked-for digital-twin use in manufacturing and the one this build is closest to. Five other use cases (virtual commissioning, full monitoring, training and safety, layout what-if, pre-planning) are parked under "Later" in `docs/build-plan.md`.

It ships as a Windows build and a 90-second walkthrough video for the Manufacturing page on 2dudestudios.com, and it is run live in front of manufacturing prospects.

### What it is solving

**For the studio.** 2 Dude Studios is selling factory digital twins ahead of its first manufacturing project. A prospect looking at a studio's reel cannot tell a factory that was rendered from one that is connected to plant data, and the second is what they would be paying for. This demo is the evidence: a twin whose 3D scene and dashboard are driven by the same signals, over the protocol a real plant would use.

**For the prospect watching it.** It shows what a twin gives a maintenance or plant manager: a failure seen minutes before it happens, on the machine where it will happen, with what it will cost the line if nobody acts. It also shows the route to their own machines: their sensors over MQTT or OPC UA, their failure history, the same twin.

### The moment it has to deliver

The press's tool wears. The failure risk climbs, the press turns amber and the twin recommends a tool change while the line is still running. Then two outcomes. Ignored, the press fails: it goes red in 3D, the arm finishes its pick, both conveyors stop and the dashboard counts parts lost. Acted on, the tool is changed and the line never stops.

Everything in the architecture exists to make that moment real rather than staged. The line stops because a signal says the press failed, and the dashboard shows it because it reads the same signal. The detection half already works (section 5); M5 adds the prediction half.

### Done when

- The M4 and M5 exit conditions in `docs/build-plan.md` are met.
- There is a Windows build locked to 60 fps and the 90-second video has been captured from it (M6).
- The same build can be driven live in a prospect meeting, with or without a broker on the network.

### Not in scope

- Client work. This is the studio's own marketing asset; a client project on this codebase needs a Unity Industry seat and a fresh licence pass.
- A real PLC or plant connection. The press telemetry is replayed from a public dataset, and the cell's "PLC" is a state machine in C#.
- A whole plant. One cell, built well.
- The other five use cases, until a prospect asks (build plan, "Later"). Also not in scope: a VR product, and robot or sensor validation of the Unity Simulation Pro kind (Industry licence only).

### Where it stands

| Milestone | Scope | State |
|---|---|---|
| M0 Setup | Project, URP, third-party imports, broker | Done |
| M1 Cell loop | Infeed → gate → pick → outfeed, 10-minute soak | Done |
| M2 Arm | De-branded UR5e, baked pick-and-place | Done |
| M3 Data spine | Signal bus, MQTT bridge, press telemetry, fault injector | Done 2026-10-12 |
| M4 Dashboard | Lean UI Toolkit dashboard for the press and the line, fault beat on camera | Done 2026-10-20 |
| M5 Prediction | Degradation mode, failure risk, remaining tool life, recommendation, tool change | Done 2026-10-20 |
| M6 Polish and capture | Lighting, VFX, audio, camera path, Windows build, 90-second video | **Next** |

---

## 2. System at a glance

```
                         Unity process (Cell.unity)
 ┌────────────────────────────────────────────────────────────────────────────┐
 │                                                                            │
 │  3D simulation (realvirtual + PhysX)          Logic (our code, no 3D)      │
 │  ┌──────────────────────────────┐             ┌──────────────────────────┐ │
 │  │ Source → Infeed conveyor     │             │ CellSequencer            │ │
 │  │ Gate1 light barrier          │             │   └ CellSequence (PLC)   │ │
 │  │ Arm (6 ArticulationBodies)   │             │ PressStation             │ │
 │  │ Outfeed → ExitSensor → Sink  │             │   └ DatasetReplayer      │ │
 │  └──────────────┬───────────────┘             │ FaultInjector            │ │
 │                 │ realvirtual Signal          │ (M4) Dashboard           │ │
 │                 │ components (19)             └────────────┬─────────────┘ │
 │         ┌───────┴────────┐                                 │               │
 │         │  TwinSignals   │◄────────── SignalBus ──────────►│               │
 │         └────────────────┘      cell/<station>/<signal>                    │
 │                                        ▲                                   │
 │                                        │ SignalPublisher (10 Hz, deadband) │
 │                                  ┌─────┴──────┐                            │
 │                                  │ MqttBridge │  MQTTnet 4.3.7, MQTT 5     │
 │                                  └─────┬──────┘                            │
 └────────────────────────────────────────┼───────────────────────────────────┘
                                          │ TCP 1883, JSON {"v":…,"t":…}
                                ┌─────────┴──────────┐
                                │ Mosquitto 2 broker │  tools/broker (Docker)
                                └─────────┬──────────┘
                     ┌────────────────────┼─────────────────────┐
              mosquitto_sub/pub     tools/replay/replay.py   any other client
              (inspection, commands) (optional press feed)   (future dashboard,
                                                              kiosk, headset)
```

Three ideas hold the design together:

1. **Signals are the only bridge.** Nothing reads a transform to learn the state of the cell. The simulation exposes realvirtual `Signal` components; `TwinSignals` mirrors them to and from the bus; everything else talks to the bus.
2. **The bus is the single source of truth inside Unity; the broker is its mirror.** Logic works with no broker running. MQTT adds visibility and remote control, not function.
3. **Decisions live in plain C# classes, MonoBehaviours only host them.** `CellSequence`, `ArmProgram`, `SignalPublisher`, `DatasetReplayer` and the topic/payload types have no Unity dependency and carry the tests.

---

## 3. The signal contract

### Topic and payload

- Topic: `cell/<station>/<signal>`. Exactly three levels. Segments are lower-case letters, digits and `_` only (`TopicPath` enforces this, so wildcards cannot leak into a concrete topic).
- Payload: `{"v": <number|true|false>, "t": <unix_ms>}`. No strings, no nesting, no NaN/Infinity. `SignalPayload` is a strict hand-written parser that accepts either key order and JSON whitespace and rejects anything else.
- Subscriptions use MQTT wildcards (`+` one level, `#` everything below); `TopicFilter` implements the matching for the in-process bus.
- QoS 0, not retained. A client that connects mid-run sees a signal only when it next changes.

### Topic catalogue (46 topics)

Direction is from the simulation's point of view: **out** is state leaving the cell, **in** is a command the cell obeys. Commands are also visible on the broker, because `TwinSignals` publishes outputs that a station writes directly.

| Topic | Type | Dir | Written by | Meaning |
|---|---|---|---|---|
| `cell/infeed/drive_fwd` | bool | in | CellSequencer | Run the infeed belt |
| `cell/infeed/drive_speed` | number | in | Scene value / broker | Belt speed setpoint, mm/s |
| `cell/infeed/running` | bool | out | realvirtual drive | Belt is moving |
| `cell/infeed/speed_mm_s` | number | out | realvirtual drive | Measured belt speed |
| `cell/gate1/part_present` | bool | out | Gate1 sensor | Part standing in the light barrier |
| `cell/arm/pick_cmd` | bool | in | CellSequencer | Rising edge starts one pick-and-place |
| `cell/arm/busy` | bool | out | ArmSignalBridge | Arm is mid-cycle (the pick acknowledgement) |
| `cell/arm/grip_cmd` | bool | in | ArmSignalBridge | Close the gripper |
| `cell/arm/holding` | bool | out | PartGripper | A part is attached to the flange |
| `cell/arm/j1_deg` … `j6_deg` | number | out | ArmSignalBridge | Measured joint angles, base to tool |
| `cell/outfeed/drive_fwd` | bool | in | CellSequencer | Run the outfeed belt |
| `cell/outfeed/drive_speed` | number | in | Scene value / broker | Belt speed setpoint, mm/s |
| `cell/outfeed/running` | bool | out | realvirtual drive | Belt is moving |
| `cell/outfeed/part_exit` | bool | out | ExitSensor | Part leaving the cell (counted on the rising edge) |
| `cell/line/state` | number | out | CellSequencer | 0 feeding, 1 transferring, 2 stalled, 3 held |
| `cell/line/stalled` | bool | out | CellSequencer | Convenience flag for state 2 |
| `cell/line/parts_out` | number | out | CellSequencer | Parts that have left the cell this run |
| `cell/line/cycle_time_s` | number | out | CellSequencer | Seconds between the last two exits |
| `cell/line/parts_per_hour` | number | out | LineMetricsReporter | Exits per hour over a 120 s rolling window, whole parts (M4) |
| `cell/line/parts_lost` | number | out | LineMetricsReporter | Parts not made while a press fault held the line, at the pre-hold rate, whole parts (M4) |
| `cell/link/connected` | bool | out | MqttBridge | Broker link is up |
| `cell/demo/fresh_take` | bool | in | TakeControls | New capture take: parts lost back to 0, event log emptied (M6) |
| `cell/press/product_quality` | number | out | DatasetReplayer | `ProductQuality` of the row: 0 L, 1 M, 2 H (sets the overstrain limit). Published first in each row (M5) |
| `cell/press/air_temp_k` | number | out | DatasetReplayer | AI4I air temperature, K |
| `cell/press/process_temp_k` | number | out | DatasetReplayer | AI4I process temperature, K |
| `cell/press/speed_rpm` | number | out | DatasetReplayer | AI4I rotational speed |
| `cell/press/torque_nm` | number | out | DatasetReplayer | AI4I torque |
| `cell/press/tool_wear_min` | number | out | DatasetReplayer | AI4I tool wear, minutes |
| `cell/press/fault_twf` `_hdf` `_pwf` `_osf` `_rnf` | bool | out | DatasetReplayer | Tool wear, heat dissipation, power, overstrain, random failure |
| `cell/press/machine_failure` | bool | out | DatasetReplayer | The dataset's overall failure flag |
| `cell/press/health` | number | out | PressHealthReporter | `MachineHealth`: 0 ok, 1 warning (risk ≥ 0.5, clears below 0.4), 2 fault |
| `cell/press/failure_risk` | number | out | PressRiskReporter | Threshold-margin failure risk, 0–1, three decimals (M5) |
| `cell/press/tool_rul_min` | number | out | PressRiskReporter | Tool wear left before the 200 min failure window opens (M5) |
| `cell/press/maint_due` | bool | out | PressRiskReporter | Risk at warning, or ≤ 20 min tool life left (M5) |
| `cell/press/degrade` | bool | in | Dashboard / broker / PressDegradation | true starts the tool-wear degradation scenario, false stops it (M5) |
| `cell/press/tool_change` | bool | in | Dashboard / broker / ToolChanger | true puts a fresh tool in: degradation stops, wear restarts low, a tool-wear failure clears (M5) |
| `cell/press/inject_fault` | number | in | FaultInjector / broker | `FaultMode` bitmask: 1 TWF, 2 HDF, 4 PWF, 8 OSF, 16 RNF; 0 clears |

The 19 infeed, gate1, arm and outfeed topics each have a realvirtual signal component and are bound in the `TwinSignals` Inspector list on the `Cell` object. The line, link and press topics exist only on the bus. `CellTopics` holds the constants the logic uses; the press topic names are built inside `DatasetReplayer` and must stay identical to `tools/replay/replay.py`.

---

## 4. Runtime architecture

### Layers and who may talk to whom

```
 Layer 4  Presentation     (M4) dashboard, 3D fault highlight      reads bus, publishes commands
 Layer 3  Stations/logic   CellSequencer, PressStation,            reads bus, publishes to bus
                           FaultInjector
 Layer 2  Data spine       SignalBus, SignalPublisher, MqttBridge, the bus and its mirror
                           TopicPath/TopicFilter/SignalPayload,
                           TwinConfig, CellContext
 Layer 1  Bridge           TwinSignals, ArmSignalBridge,           the only code that references
                           PartGripper                             realvirtual types
 Layer 0  Simulation       realvirtual drives/sensors/MUs, PhysX,  third-party, never edited
                           ArmController on ArticulationBodies
```

The assembly definitions enforce this: `TwoDude.FactoryTwin.Twin` and `TwoDude.FactoryTwin.Robot` have no reference to realvirtual, so logic cannot reach into the simulation even by accident. Only `TwoDude.FactoryTwin.Twin.Realvirtual` references `realvirtual.base`.

### Components in the scene

| Object | Component | Order | Responsibility |
|---|---|---|---|
| `Cell` | `CellContext` | −1000 | Owns the `SignalBus` (created lazily) and the `TwinConfig` reference. Every other component gets both through a serialized reference to this. |
| `Cell` | `MqttBridge` | −200 | Owns the MQTT client and the `SignalPublisher`. Connect/reconnect loop on a background task; inbound messages are queued and replayed onto the bus in `Update`. |
| `Cell` | `TwinSignals` | −100 | Polls the 19 bound realvirtual signals each `FixedUpdate` and publishes changes; writes PLC outputs when their topic is published. |
| `Cell` | `CellSequencer` | 100 | Builds `CellInputs` from the bus, ticks `CellSequence`, publishes changed outputs and line status. |
| `Cell/Signals/*` | realvirtual `PLCInput*` / `PLCOutput*` | — | One GameObject per signal, named `<station>.<signal>`. |
| `Cell/Press` | `PressStation` | 0 | Parses `ai4i2020.csv`, hosts `DatasetReplayer`, 2 rows/s, healthy baseline. |
| `Cell/Press` | `FaultInjector` | 0 | Turns `press/inject_fault` into the press's fault filter. |
| `Cell/Press` | `PressDegradation` | −10 | Runs `DegradationScenario` on `press/degrade`; overrides published tool wear, then hands over to TWF rows (M5). |
| `Cell/Press` | `ToolChanger` | 0 | On `press/tool_change`: stops degradation, `PressStation.ChangeTool`, keeps `FaultInjector` consistent (M5). |
| `Cell/Press` | `PressRiskReporter` | 40 (LateUpdate) | Runs `FailureRiskModel` when new telemetry arrives; publishes risk, tool life left and maintenance due (M5). |
| `Cell/Press` | `PressHealthReporter` | 50 (LateUpdate) | Publishes `press/health` on change: fault flags, or the risk warning band from `PressRiskConfig`. |
| `Cell` | `LineMetricsReporter` | 110 | Ticks `LineMetrics` after the sequencer; publishes parts/hour and parts lost. |
| `Arm` | `ArmController` | 0 | Reads joint positions, ticks `ArmProgram`, writes drive targets. |
| `Arm` | `ArmSignalBridge` | 50 | Pick command edge → `RequestPick()`; writes busy, grip command and joint angles to signals. |
| `Arm/…/tool0` | `PartGripper` | 0 | On grip command, fixes the part at the gate to the flange; releases on open. |
| `Cell/Press/PressMachine` | realvirtual `CNC.prefab` | — | The machine shown for the press station (M4). |
| `Cell/Press/StackLight` | `StackLight` | 0 | Andon tower on the machine roof plus `Cell/Press/StackLightGlow` (M4). |
| `Dashboard` | `TakeControls`, `FpsReadout` | 0, 210 | Capture keys (1, 2, 3, 0, F9, F10) and the FPS readout / `-logfps` log (M6). |
| `Dashboard` | `UIDocument` + `DashboardController` | 200 | `Dashboard.uxml` on `DashboardPanelSettings`; view model bound to the bus; fault buttons call `FaultInjector`. |
| `PickStation` (prefab instance) | `PartTransfer` | — | M1 placeholder, **disabled**. Same `pick_cmd`/`busy` contract the arm uses. |

`Cell` also parents seven prefab instances that make up the physical line: `PartSource`, `Infeed`, `Gate1`, `PickStation`, `Outfeed`, `ExitSensor` and `Sink`.

### One physics tick

Execution order gives a fixed read → act → decide sequence every `FixedUpdate`:

1. `TwinSignals` (−100) reads every bound signal and publishes what changed. The bus now holds the simulation state as of the previous tick.
2. `ArmController` (0) reads joints, advances the program, sets drive targets. `PartGripper` (0) acts on the grip command.
3. `ArmSignalBridge` (50) passes a pick-command edge to the arm and writes busy / grip / joint angles to their signals.
4. `CellSequencer` (100) reads the bus, ticks the sequence and publishes changed commands. `TwinSignals`' subscriptions write those straight into the realvirtual outputs, synchronously, inside the publish call.

Consequence: anything the arm bridge writes in step 3 reaches the bus in step 1 of the next tick. One fixed step of latency on arm feedback is by design and the sequence's acknowledgement timeout (2 s) dwarfs it.

`Update` carries the non-physics work: `MqttBridge` drains inbound messages and flushes the publisher; `PressStation` advances the dataset replay.

### The MQTT link

- Outbound: `SignalPublisher` subscribes to `cell/#` on the bus and decides what leaves. A topic is sent only when it has moved beyond the deadband (booleans: on any change) and at most at `publishRateHz` per topic; changes inside the window are coalesced to the latest value.
- Link down: nothing is flushed, the publisher keeps the latest value per topic, and on reconnect `ResendAll()` queues the whole cell again (except topics last written by the far side).
- Inbound: the client subscribes to `cell/#` with MQTT 5 no-local, so the broker does not echo the cell's own publishes. Each inbound message is marked with `ExpectInbound` before it goes on the bus, so it is not sent back out.
- Messages that do not parse as our topic and payload are counted as dropped and ignored — other traffic on the same broker is harmless.
- Publishing is fire-and-forget: a slow broker never stalls the simulation.
- All settings come from `Assets/_Project/Data/TwinConfig.asset`: host `127.0.0.1`, port `1883`, client id `factory-twin-unity`, reconnect 5 s, 10 Hz, deadband 0.001.

---

## 5. Control logic

### Cell sequence (the virtual PLC)

`CellSequence` is a four-state machine ticked with the fixed timestep.

```
                  part at gate                    arm done, gate clear
      ┌─────────┐ ───────────► ┌──────────────┐ ─────────────────────► (Feeding)
      │ Feeding │              │ Transferring │
      │   (0)   │ ◄─────────── │     (1)      │ ── arm done, part still at gate:
      └────┬────┘              └──────┬───────┘    retry the pick (PickRetries++)
           │  ▲                       │
      hold │  │ hold cleared          │ no completion within 15 s (Stalls++)
           ▼  │                       ▼
      ┌─────────┐              ┌──────────────┐
      │  Held   │ ◄─────────── │   Stalled    │ ── gate clear and arm idle ──► Feeding
      │   (3)   │  (if hold)   │     (2)      │
      └─────────┘              └──────────────┘
```

| State | Infeed | Outfeed | Pick command |
|---|---|---|---|
| Feeding | run | run | off |
| Transferring | stop | run | on until the arm reports busy |
| Stalled | stop | run | off |
| Held | stop | stop | off |

- **Handshake.** `pick_cmd` goes high; the arm answers with `busy`. If no `busy` arrives within 2 s the command is dropped for one tick and raised again so the arm sees a fresh rising edge.
- **Hold.** Asserted while `press/machine_failure` or any `press/fault_*` is true. A hold never interrupts a transfer: the pick finishes, then the line goes to Held, so the arm never freezes with a part in the gripper.
- **Counting.** `parts_out` increments on the rising edge of `outfeed/part_exit`; `cycle_time_s` is the time between the last two exits.

### Arm program

`ArmProgram` plays eight baked poses: `home → pick_approach → pick (close, dwell 0.25 s) → lift → place_approach → place (open, dwell 0.25 s) → retreat → home_end`. Commanded angles ramp at 110 °/s per joint. A waypoint completes only when the commanded pose has arrived, every measured joint is within 1.5° and the dwell has elapsed — so the gripper acts where the arm actually is, not where it was told to be.

The poses were solved offline from the UR5e's closed-form kinematics and stored on the `ArmController` in the scene. There is no runtime IK. Changing the conveyor layout, the part size or the arm base means re-baking them.

`ArmController` also sets the joint drive gains on `Awake` (stiffness 100 000, damping 10 000, force limit 1 000) because the URDF import leaves them at zero.

### Fault injection, end to end

```
 trigger                    press                         line
 ───────                    ─────                         ────
 inject_fault = 1  ──►  FaultInjector sets the
 (broker, Inspector,    press's fault filter
  or Inject() from          │
  the M4 button)            ▼
                        next dataset row (≤ 0.5 s) is
                        a tool-wear failure row:
                        fault_twf = true,
                        machine_failure = true  ──►  CellSequencer sees a press fault
                                                     → Hold. A pick in progress finishes,
                                                     then line/state = 3, both belts stop.

 inject_fault = 0  ──►  filter cleared; healthy
                        baseline rows resume,
                        faults drop to false     ──►  Hold released → Feeding.
```

The line reacts to the press's fault signals, not to the injector. A fault arriving from the Python replayer or any other client on the broker holds the line in exactly the same way.

Measured in the M3 exit run: fault reported 0.5 s after injection; faults cleared within 0.3 s of the clear command.

---

## 6. Code map

All project code is under `Assets/_Project/`. About 2,400 lines of runtime and editor C#, 1,150 lines of tests.

### Assemblies

| Assembly | Folder | References | Contains |
|---|---|---|---|
| `TwoDude.FactoryTwin.Twin` | `Scripts/Twin/` (incl. `Mqtt/`) | MQTTnet DLL (auto) | Data spine, cell logic, press, MQTT bridge |
| `TwoDude.FactoryTwin.Robot` | `Scripts/Robot/` | — | Arm program and controller |
| `TwoDude.FactoryTwin.Twin.Realvirtual` | `Scripts/Twin/Realvirtual/` | Twin, Robot, `realvirtual.base` | The bridge layer |
| `TwoDude.FactoryTwin.UI` | `Scripts/UI/` | Twin, Unity.InputSystem | Dashboard view model, sparkline element, controller (M4); take controls and FPS readout (M6) |
| `TwoDude.FactoryTwin.Twin.Editor` | `Scripts/Twin/Editor/` | — (Editor only) | `ProjectSettingsGuard` |
| `TwoDude.FactoryTwin.Robot.Editor` | `Scripts/Robot/Editor/` | URDF-Importer (Editor only) | `ArmImporter` |
| `TwoDude.FactoryTwin.Tests.EditMode` | `Tests/EditMode/` | Twin, Robot, UI, NUnit | 183 tests |

UXML and USS live in `Assets/_Project/UI/` (`Dashboard.uxml`, `Dashboard.uss`, `Credits.uxml`).

### Files

| File | Kind | Role |
|---|---|---|
| `Twin/TopicPath.cs` | struct | A validated `cell/<station>/<signal>` topic |
| `Twin/TopicFilter.cs` | static | MQTT wildcard validation and matching |
| `Twin/SignalValue.cs` | structs | `SignalValue` (number or bool) and `SignalMessage` (topic, value, time) |
| `Twin/SignalPayload.cs` | static | JSON payload serialise / strict parse |
| `Twin/SignalBus.cs` | class | In-process pub/sub with last-value cache; main thread only; safe to subscribe or dispose during dispatch |
| `Twin/SignalPublisher.cs` | class | Rate limit, deadband, coalescing, resend-on-reconnect, no echo of inbound |
| `Twin/Mqtt/MqttBridge.cs` | MonoBehaviour | MQTT client lifecycle and thread marshalling |
| `Twin/TwinConfig.cs` | ScriptableObject | Broker and publishing settings |
| `Twin/CellContext.cs` | MonoBehaviour | Bus and config owner; `NowUnixMs` |
| `Twin/CellTopics.cs` | static | Topic constants for the cell loop |
| `Twin/CellSequence.cs` | class | The virtual PLC state machine |
| `Twin/CellSequencer.cs` | MonoBehaviour | Hosts `CellSequence`; Inspector status mirror |
| `Twin/Ai4iDataset.cs` | types | `FaultMode`, `Ai4iRow`, `Ai4iCsv` parser (columns found by header name) |
| `Twin/DatasetReplayer.cs` | class | Rows → bus at N rows/s; loop, fault filter, healthy baseline, backlog cap |
| `Twin/PressStation.cs` | MonoBehaviour | Hosts `DatasetReplayer`; Inspector controls live in Play mode |
| `Twin/FaultInjector.cs` | MonoBehaviour | `inject_fault` command → press fault filter; `Inject()` / `Clear()` API |
| `Twin/PressHealthRule.cs` | enum + static | `MachineHealth`; `IsFaulted(bus)` (shared with `CellSequencer`) and `Evaluate(faulted, warning)` |
| `Twin/PressHealthReporter.cs` | MonoBehaviour | Publishes `press/health` on change: faults (M4), risk warning with hysteresis (M5) |
| `Twin/DegradationScenario.cs` | class | Tool wear on a schedule, start → failure wear over a fixed time (M5) |
| `Twin/PressDegradation.cs` | MonoBehaviour | `press/degrade` → scenario → `PressStation.ToolWearOverride`, then TWF rows (M5) |
| `Twin/ToolChanger.cs` | MonoBehaviour | `press/tool_change` → fresh tool, tool-wear failure cleared (M5) |
| `Twin/PressRiskConfig.cs` | ScriptableObject | `FailureRiskSettings` and the warning clear level; instance at `Data/PressRiskConfig.asset` (M5) |
| `Twin/PressRiskReporter.cs` | MonoBehaviour | Telemetry → `FailureRiskModel` → risk topics (M5) |
| `Twin/FailureRiskModel.cs` | static + types | `PressReading`, `FailureRiskSettings`, `RiskAssessment`; threshold-margin failure risk, remaining tool life, maintenance due (M5) |
| `Twin/StackLightPattern.cs` | static | Lamp and glow levels for a health value; red flashes at 1.5 Hz (M4) |
| `Twin/StackLight.cs` | MonoBehaviour | Health topic → stack-light lamps (MaterialPropertyBlock) and glow light (M4) |
| `Twin/LineMetrics.cs` | class | Rolling parts/hour; parts lost while held at the frozen pre-hold rate (M4) |
| `Twin/LineMetricsReporter.cs` | MonoBehaviour | Hosts `LineMetrics`; publishes parts/hour and parts lost as whole numbers (M4) |
| `Twin/FrameRateLock.cs` | static | 60 fps and vSync off in player builds (not in the Editor) |
| `Twin/Realvirtual/TwinSignals.cs` | MonoBehaviour | Signal ↔ bus mirror |
| `Twin/Realvirtual/ArmSignalBridge.cs` | MonoBehaviour | Arm ↔ signals |
| `Twin/Realvirtual/PartGripper.cs` | MonoBehaviour | Gripper acting on realvirtual `MU` parts |
| `Twin/Realvirtual/PartTransfer.cs` | MonoBehaviour | M1 placeholder, disabled in the scene |
| `Twin/Editor/ProjectSettingsGuard.cs` | Editor | On every reload: Build Settings = `Cell.unity` only, Run In Background on, realvirtual layers present |
| `UI/DashboardViewModel.cs` | class | Bus → bindable display text, sparkline series and event log (M4) |
| `UI/EventLog.cs` | class | Readable lines for state transitions, newest first (M4) |
| `UI/SparkSeries.cs` | class | Fixed-size sample ring with a version counter (M4) |
| `UI/Sparkline.cs` | VisualElement | `[UxmlElement]` Painter2D line for a `SparkSeries` (M4) |
| `UI/TakeControls.cs` | MonoBehaviour | Capture keyboard shortcuts and fresh take (M6) |
| `UI/FpsMeter.cs` | class | Average fps and worst frame per window (M6) |
| `UI/FpsReadout.cs` | MonoBehaviour | FPS label (F9) and `-logfps` player-log lines (M6) |
| `UI/DashboardController.cs` | MonoBehaviour | Data source, sparkline series, status classes, fault buttons (M4). On the root `Dashboard` object with its `UIDocument`. |
| `Robot/ArmProgram.cs` | class | Waypoint player; `ArmWaypoint`, `GripAction` |
| `Robot/ArmController.cs` | MonoBehaviour | ArticulationBody drive targets and gains |
| `Robot/Editor/ArmImporter.cs` | Editor | `Tools > Factory Twin > Import Cell Arm (UR5e)`; pins the articulation root |

### Other project assets

| Path | What |
|---|---|
| `Scenes/Cell.unity` | The only scene (2.6 MB). A Save As of the Factory Training hall with the cell built on its keep-clear zone. |
| `Data/TwinConfig.asset` | The one config instance |
| `Data/PressRiskConfig.asset` | Risk margins and warning band (M5) |
| `Data/ai4i2020.csv` | 10,000 rows; 339 failures (TWF 46, HDF 115, PWF 95, OSF 98, RNF 19) |
| `Robots/ur/` | `cell_arm.urdf`, UR5e visual (`.dae`) and collision (`.stl`) meshes, materials, BSD-3 licence, provenance README |
| `Settings/` | Stock URP assets, kept as a fallback. The active pipeline asset is the sample's `PC_RPAsset_DC` under `ThirdParty/FactoryTraining`. |
| `Prefabs/` | Empty. The cell is assembled directly in the scene. |

---

## 7. Scene layout

The cell sits on the hall's hatched keep-clear zone at (−5…0.5, −1…3.9). It is L-shaped so the arm swings about 90° between pick and place, each 0.55 m from its base (UR5e reach is 0.85 m). Both belts flow toward −x.

```
     z
     ▲
 1.2 │  Sink ◄ ExitSensor ◄═══ Outfeed ═══ [place] ── Arm base (−2.0, 0.6, 1.2)
     │                                                    │   on a 0.6 m stand
 0.65│                                                 [pick] ◄ Gate1 ◄═══ Infeed ═══ PartSource
     └──────────────────────────────────────────────────────────────────────────────► x
```

- Parts are realvirtual `MU`s spawned by a `Source` every 400 mm of belt travel. A part rests at the gate at x = −1.71 with its centre at 0.575 m (belt top 0.525 m) — measured values the arm poses depend on.
- `Gate1` is a raycast light barrier. Raycast sensors never fill `CollidingMus`, which is why `PartGripper` falls back to `LastTriggeredBy` with a 0.2 m distance check.
- `Cell/Press` is an empty GameObject carrying `PressStation` and `FaultInjector`. **There is no press geometry in the scene.**
- Camera is realvirtual's orbit camera for now; its uGUI runtime toolbar is visible in Play.

---

## 8. Tooling outside Unity

| Tool | Path | Purpose |
|---|---|---|
| Mosquitto 2 broker | `tools/broker/` | `docker compose up -d` → container `factory-twin-broker`; MQTT on 1883, WebSockets on 9001; anonymous, no persistence. LAN only. |
| Dataset replayer | `tools/replay/replay.py` | Publishes the same 11 press topics straight to the broker. `--rate`, `--fault TWF…`, `--once`, `--host`. Needs `paho-mqtt>=2.0`. |
| UnitySkills | package, `localhost:8090` | REST control of the Editor for Claude Code (play, inspect, run tests). Dev tooling, not shipped. |
| Claude Code commands | `.claude/commands/` | `/milestone`, `/test`, `/licence-check` |
| Claude Code permissions | `.claude/settings.json` | Writes allowed under `Assets/_Project`, `docs`, `tools`; denied under `ThirdParty`, `realvirtual`, `Library` |
| Git | `.gitattributes`, `.gitignore` | LFS for meshes, textures, audio, video and DLLs; Unity YAML merge; Asset Store imports and realvirtual ignored |

The in-Unity `PressStation` and `replay.py` write the same topics. Run one or the other, not both.

Inspect the cell:

```bash
docker exec factory-twin-broker mosquitto_sub -t 'cell/#' -v
```

Inject and clear a tool-wear fault from outside Unity:

```bash
docker exec factory-twin-broker mosquitto_pub -t cell/press/inject_fault -m '{"v":1,"t":0}'
```

```bash
docker exec factory-twin-broker mosquitto_pub -t cell/press/inject_fault -m '{"v":0,"t":0}'
```

---

## 9. Stack and licensing

| Layer | Item | Version | In git | Notes |
|---|---|---|---|---|
| Engine | Unity | 6000.3.19f1 | settings | URP 17.3.0, Linear, Deferred+ via the sample's pipeline asset |
| Base scene | Factory Training sample | 1.0 | no | `Assets/ThirdParty/FactoryTraining` |
| Twin logic | realvirtual Starter | 6.0.11 | no | `Assets/realvirtual`, `Assets/Gizmos/realvirtual`; compiled-only distribution |
| MQTT | MQTTnet | 4.3.7 | yes (LFS) | Vendored netstandard2.1 DLL; 5.x cannot load in Unity |
| Robot | UR5e from `ros-industrial/universal_robot` | 39ad110 | yes | De-branded; BSD-3; credit owed |
| Robot import | URDF-Importer | 0.5.2 | manifest | Apache-2.0; notice owed |
| Telemetry | AI4I 2020 dataset | 2020 | yes | CC BY 4.0; credit owed in app and on the web page |
| Packages we chose | Splines 2.9.0, Cinemachine 3.1.7, Input System 1.19.0, Test Framework 1.6.0 | | manifest | Cinemachine is 3.x (`Unity.Cinemachine`) |
| Imported, unused so far | Fast IK 1.2, Particle Pack 3.1 | | no | Particle Pack is for M6 |
| Brought in by the sample | Timeline, uGUI, Post Processing v2, ProGrids, AI Navigation, Burst/Collections/Mathematics, Asset Manager | | manifest | Not chosen; prune once it is clear what the scene needs |

Obligations that affect the build:

1. Asset Store content ships only inside a built application.
2. realvirtual source, prefabs and scenes are never shared.
3. A Credits panel must carry: AI4I 2020 (CC BY 4.0, doi:10.24432/C5HS5C), the UR5e description (BSD-3), URDF-Importer (Apache-2.0), MQTTnet (MIT). Not built yet.
4. No vendor branding on the arm in anything public.
5. A client project on this codebase needs a Unity Industry seat and a re-run of the licence list.

---

## 10. Tests and verification

117 EditMode tests in `Assets/_Project/Tests/EditMode/`, all against the pure classes:

| File | Tests | Covers |
|---|---|---|
| `TopicTests.cs` | 40 | Topic parsing, segment validation, filter matching |
| `SignalPayloadTests.cs` | 27 | Serialise, parse, malformed input |
| `SignalPublisherTests.cs` | 13 | Rate limit, deadband, coalescing, reconnect resend, no echo |
| `CellSequenceTests.cs` | 11 | Handshake, retries, stall, hold |
| `DatasetReplayerTests.cs` | 11 | CSV parsing, pacing, loop, fault filter, healthy baseline |
| `SignalBusTests.cs` | 8 | Dispatch, wildcards, subscribe/dispose during dispatch |
| `ArmProgramTests.cs` | 7 | Ramp, arrival tolerance, dwell, grip actions |

Not covered by automated tests: `MqttBridge`, `TwinSignals`, the arm bridge and gripper, and anything in Play mode. Those are verified by the milestone exit runs recorded in `docs/STATUS.md`:

| Run | Result |
|---|---|
| M1, 10 min, placeholder pick | 143 parts, 4.58 s cycle (≈771 parts/h), 0 retries, 0 stalls |
| M2, 5.6 min, arm | 33 parts, 7.0 s cycle (≈516 parts/h), 0 retries, 0 stalls |
| M3, 80 s, broker attached | 36 topics, ≈2,500 messages, fault held the line for 18 s and recovered |

The arm cycle (7.0 s) is the cell's bottleneck and sets the parts/hour figure the dashboard will show.

---

## 11. What is left to build

Exit conditions and day estimates are in `docs/build-plan.md`; the video beats each piece serves are in `docs/demo-script.md`. This section lists what each milestone needs from, or changes in, the code described above.

### M4 — Dashboard (exit: the "inject fault" beat looks good on camera)

Nothing exists yet. Only what the predictive-maintenance story needs:

| Piece | Notes |
|---|---|
| UI assembly and folders | `Scripts/UI/` (namespace `TwoDude.FactoryTwin.UI`, references Twin only) and `Assets/_Project/UI/` for UXML/USS. UI Toolkit with runtime data binding; no uGUI. |
| Bus → view model | A component that subscribes to the bus and exposes bindable values. The dashboard reads the bus, never scene objects. |
| Press panel | Health, sparklines for the five telemetry topics, and the failure-risk gauge (fed from M5). |
| Line panel | Line state, parts out, parts/hour, parts lost. Parts/hour and parts lost have to be derived. Parts lost is held time × the running rate. |
| Event log | `SignalBus.MessagePublished` is there for it. Needs readable text for numeric states. |
| Fault buttons | Inject (TWF, HDF, PWF, OSF, RNF) and Clear, calling `FaultInjector.Inject()` / `Clear()`. |
| 3D reaction | The press turning amber then red. Needs press geometry first (gap 1), then a highlighter driven by `cell/press/health`. |
| Broker link indicator | From `cell/link/connected`. |
| Credits panel | The four notices in section 9. `/licence-check` already expects `Assets/_Project/UI/Credits.uxml`. |
| Hide realvirtual's runtime toolbar | It is uGUI and visible in Play. |
| New signals | `cell/press/health` (0 ok, 1 warning, 2 fault), `cell/line/parts_per_hour`, `cell/line/parts_lost`. |

Dropped from the Rev 2 M4: OEE, health for every station, hover-to-inspect and retained MQTT state. They are now under "Later".

### M5 — Prediction

| Piece | Notes |
|---|---|
| Degradation mode | A third press mode beside healthy baseline and fault filter: rows chosen so tool wear and torque climb through a warning band to failure over about a minute. Lives with `DatasetReplayer` / `FaultInjector`; the hold path in section 5 is unchanged. |
| Risk model | A pure, tested class turning press telemetry into `cell/press/failure_risk` (0–1), `cell/press/tool_rul_min` and `cell/press/maint_due`. Definition is the M5 open decision in the build plan. Parameters in a ScriptableObject, not code. |
| Tool change | `cell/press/tool_change` (in) switches the replay back to low-tool-wear rows, so acting on the recommendation visibly resets wear and risk. |
| Press health | `cell/press/health` derived from risk as well as faults; the dashboard's gauge and the 3D amber state read it. |

### M6 — Polish and capture

Foundry HDRI and ambientCG materials (`Assets/_Project/Lighting`, `Materials` — folders not created yet), sparks/steam from the Particle Pack, ambience audio (`Assets/_Project/Audio`), a Cinemachine 3 camera path on Unity Splines, the Windows build (`Builds/` is empty; the 60 fps lock only takes effect in a build), then capture at 1920×1080, 60 fps per the demo script.

Still pending by hand from `docs/STATUS.md`: delete the deactivated training objects in `Cell.unity`, delete `Assets/Screenshots/`, restart the Editor once for the colour-space and input-backend switch.

### Later (not scheduled)

The five parked use cases and the touch/kiosk and Quest 3 views; see "Later" in `docs/build-plan.md`. Rev 2's code-level notes for them (soft-PLC logic source, protective stop that interrupts a pick, runtime IK, scenario runner) are in commit `ee852e9`.

---

## 12. Gaps, mismatches and risks

Found while writing this blueprint:

| # | Finding | Why it matters |
|---|---|---|
| 1 | **No press in 3D.** `Cell/Press` has no renderer or children. | Demo beat 4 shows the press turning red. Geometry has to come from somewhere with a known licence before the highlight can be built. **Resolved 2026-10-20:** realvirtual's CNC.prefab plus a stack light (M4). |
| 2 | **The demo script names signals that do not exist**: `cell/*/state`, `outfeed/count`, `press/health`. The real ones are `line/state` and `line/parts_out`; there is no health signal. | Update `docs/demo-script.md` or add the signals; either way decide before building the dashboard against them. **Resolved 2026-10-19:** the rewritten demo script uses the real names and marks planned ones with the milestone that adds them; `press/health` is added in M4. |
| 3 | **OEE and failure risk are undefined.** No reject or quality data exists, and nothing computes risk. | OEE needs availability × performance × quality; quality has no source in this cell. Risk could be derived from tool wear, torque and temperature spread, but it is a design decision, not a wiring task. OEE is parked under Later (Rev 3). The risk definition is the open decision at the start of M5. |
| 4 | **The arm bake script is not in the repo.** `docs/STATUS.md` points at `scratchpad/ur5e_kin.py`; no such file exists here. | The eight poses cannot be regenerated after a layout change without rewriting it. Worth committing under `tools/`. Needed before the parked layout what-if work, where runtime IK would replace the baked poses. |
| 5 | **UnitySkills version drift.** The working tree bumps it to v2.9.0 (uncommitted in `Packages/manifest.json` and the lock file); `docs/LICENSES.md` and `docs/STATUS.md` say v2.8.4. | The licence register must match the manifest in the same commit. |
| 6 | **`/test` command names Editor `6000.3.0f1`**; the project is on `6000.3.19f1`. | The headless command fails as written unless that exact version is installed. |
| 7 | **Untracked `Assets/Temp.meta` and `Assets/UnitySkillsTests_Temp.meta`.** | Leftovers from UnitySkills' test run; delete or ignore rather than commit. |
| 8 | **`Prefabs/` is empty** although the convention is sub-areas as prefabs. | Everything lives in a 2.6 MB scene that realvirtual dirties on load. Extracting `Cell`, `Arm` and the press as prefabs would make diffs and reuse easier; not urgent. |
| 9 | **`PartGripper` reads realvirtual `Sensor` and `MU` directly**, and `ArmSignalBridge` holds signal references of its own, while `.claude/rules/unity-csharp.md` says everything goes through `TwinSignals`. | Contained in the bridge assembly, so the layering holds. The rule text is stricter than the code; reword one or the other. |

Carried over from `docs/STATUS.md` and still open:

- MQTT messages are not retained, so a late-joining client misses steady signals. Matters the day a dashboard reads from the broker instead of the bus.
- The sequencer owns `infeed/drive_fwd` and `outfeed/drive_fwd`; an external write is overwritten within a cycle. External control of the line has to go through a signal the sequence respects, as the hold does.
- `replay.py` replays the dataset's own failure rows (about 1 in 30), each of which holds the line for one row. It has no healthy-only switch.
- Re-importing any project-style Asset Store package can overwrite `ProjectSettings/`; diff before committing. `ProjectSettingsGuard` catches the three settings that broke last time, not all of them.
- realvirtual 6.0.11 reports Unity 6000.3.19f1 as outside its tested range. No problem seen so far.
- The broker is anonymous and unencrypted, and `TwinConfig` has no credential or TLS fields. Fine for a studio LAN; a prospect's network will need both.
- One unexplained Play-mode restart during the first M1 soak; not seen since.

---

## 13. Running it

1. Start the broker (optional — the cell runs without it):

```bash
docker compose -f tools/broker/docker-compose.yml up -d
```

2. Open the project in Unity 6000.3.19f1, open `Assets/_Project/Scenes/Cell.unity`, press Play.
3. Watch the cell on the broker with the `mosquitto_sub` command in section 8. Start the subscriber before Play, because nothing is retained.
4. Inject a fault: in Play mode set `Fault` on `Cell/Press > FaultInjector`, or publish to `cell/press/inject_fault`.
5. Run the EditMode tests from the Unity Test Runner, or `/test` in Claude Code.

Live status while playing is on the Inspector of `CellSequencer` (state, parts out, cycle time, retries, stalls), `MqttBridge` (connected, published, received, dropped), `PressStation` (rows loaded and published) and `ArmController` (current waypoint, busy, grip, cycles).
