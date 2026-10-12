# Factory Twin — project blueprint

As of 2026-10-12, commit `40aa013` (M3 closed, M4 next). Written from the code, the scene file and the docs in this repo; where the docs and the code disagree, the code is described and the difference is listed in section 12.

Companion documents: `docs/build-plan.md` (why and in what order), `docs/STATUS.md` (running log and decisions), `docs/demo-script.md` (the 90-second video), `docs/LICENSES.md` (what may ship), `CLAUDE.md` (rules for working in the repo).

---

## 1. What this is

A Unity 6000.3 / URP demo of one production cell for the Manufacturing page on 2dudestudios.com and for in-person prospect demos. It is 2 Dude Studios' own marketing asset, not client work.

The cell: parts arrive on an infeed conveyor, stop at a light-barrier gate, a six-axis arm picks each one and places it on an outfeed conveyor, parts exit. A press station alongside it reports machine telemetry replayed from a public dataset. Every drive, sensor and joint is a signal, and every signal is published over MQTT.

The pitch is one moment: a fault is injected, and the 3D cell and the dashboard react together. Everything in the architecture exists to make that moment real rather than staged — the line stops because a signal says the press failed, and the dashboard will show it because it reads the same signal.

| Milestone | Scope | State |
|---|---|---|
| M0 Setup | Project, URP, third-party imports, broker | Done |
| M1 Cell loop | Infeed → gate → pick → outfeed, 10-minute soak | Done |
| M2 Arm | De-branded UR5e, baked pick-and-place | Done |
| M3 Data spine | Signal bus, MQTT bridge, press telemetry, fault injector | Done 2026-10-12 |
| M4 Dashboard | UI Toolkit overlay, fault beat on camera | **Next — nothing built yet** |
| M5 Polish and capture | Lighting, VFX, audio, camera path, Windows build | Not started |

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

### Topic catalogue (36 topics)

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
| `cell/link/connected` | bool | out | MqttBridge | Broker link is up |
| `cell/press/air_temp_k` | number | out | DatasetReplayer | AI4I air temperature, K |
| `cell/press/process_temp_k` | number | out | DatasetReplayer | AI4I process temperature, K |
| `cell/press/speed_rpm` | number | out | DatasetReplayer | AI4I rotational speed |
| `cell/press/torque_nm` | number | out | DatasetReplayer | AI4I torque |
| `cell/press/tool_wear_min` | number | out | DatasetReplayer | AI4I tool wear, minutes |
| `cell/press/fault_twf` `_hdf` `_pwf` `_osf` `_rnf` | bool | out | DatasetReplayer | Tool wear, heat dissipation, power, overstrain, random failure |
| `cell/press/machine_failure` | bool | out | DatasetReplayer | The dataset's overall failure flag |
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
| `Arm` | `ArmController` | 0 | Reads joint positions, ticks `ArmProgram`, writes drive targets. |
| `Arm` | `ArmSignalBridge` | 50 | Pick command edge → `RequestPick()`; writes busy, grip command and joint angles to signals. |
| `Arm/…/tool0` | `PartGripper` | 0 | On grip command, fixes the part at the gate to the flange; releases on open. |
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
| `TwoDude.FactoryTwin.Twin.Editor` | `Scripts/Twin/Editor/` | — (Editor only) | `ProjectSettingsGuard` |
| `TwoDude.FactoryTwin.Robot.Editor` | `Scripts/Robot/Editor/` | URDF-Importer (Editor only) | `ArmImporter` |
| `TwoDude.FactoryTwin.Tests.EditMode` | `Tests/EditMode/` | Twin, Robot, NUnit | 117 tests |

`Scripts/UI/` exists and is empty. There is no UI assembly and no `Assets/_Project/UI/` folder yet.

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
| `Twin/FrameRateLock.cs` | static | 60 fps and vSync off in player builds (not in the Editor) |
| `Twin/Realvirtual/TwinSignals.cs` | MonoBehaviour | Signal ↔ bus mirror |
| `Twin/Realvirtual/ArmSignalBridge.cs` | MonoBehaviour | Arm ↔ signals |
| `Twin/Realvirtual/PartGripper.cs` | MonoBehaviour | Gripper acting on realvirtual `MU` parts |
| `Twin/Realvirtual/PartTransfer.cs` | MonoBehaviour | M1 placeholder, disabled in the scene |
| `Twin/Editor/ProjectSettingsGuard.cs` | Editor | On every reload: Build Settings = `Cell.unity` only, Run In Background on, realvirtual layers present |
| `Robot/ArmProgram.cs` | class | Waypoint player; `ArmWaypoint`, `GripAction` |
| `Robot/ArmController.cs` | MonoBehaviour | ArticulationBody drive targets and gains |
| `Robot/Editor/ArmImporter.cs` | Editor | `Tools > Factory Twin > Import Cell Arm (UR5e)`; pins the articulation root |

### Other project assets

| Path | What |
|---|---|
| `Scenes/Cell.unity` | The only scene (2.6 MB). A Save As of the Factory Training hall with the cell built on its keep-clear zone. |
| `Data/TwinConfig.asset` | The one config instance |
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
| Imported, unused so far | Fast IK 1.2, Particle Pack 3.1 | | no | Particle Pack is for M5 |
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

### M4 — Dashboard (exit: the "inject fault" beat looks good on camera)

Nothing exists yet. From the build plan, the demo script and the rules in `CLAUDE.md`, M4 needs:

| Piece | Notes |
|---|---|
| UI assembly and folders | `Scripts/UI/` (namespace `TwoDude.FactoryTwin.UI`, references Twin only) and `Assets/_Project/UI/` for UXML/USS. UI Toolkit with runtime data binding; no uGUI. |
| Bus → view model | A component that subscribes to the bus and exposes bindable values. The dashboard reads the bus, never scene objects. |
| KPI tiles | Cycle time and parts out are already signals. Parts/hour and OEE have to be derived. |
| Per-machine health | Infeed, gate, arm, outfeed, press — from `running`, `busy`, `line/state` and the press faults. |
| Failure-risk gauge | No signal exists for this; needs a definition (see open decisions). |
| Event log | `SignalBus.MessagePublished` is there for it. Needs readable text for numeric states. |
| Fault buttons | Inject (TWF, HDF, PWF, OSF, RNF) and Clear, calling `FaultInjector.Inject()` / `Clear()`. |
| 3D reaction | The press turning amber then red. Needs press geometry first, then a highlighter driven by the press's fault topics. |
| Hover to inspect | Hovering a conveyor shows its topic and live value (demo beat 3). Needs a mapping from scene objects to topics. |
| Broker link indicator | From `cell/link/connected`. |
| Credits panel | The four notices in section 9. `/licence-check` already expects `Assets/_Project/UI/Credits.uxml`. |
| Hide realvirtual's runtime toolbar | It is uGUI and visible in Play. |

### M5 — Polish and capture

Foundry HDRI and ambientCG materials (`Assets/_Project/Lighting`, `Materials` — folders not created yet), sparks/steam from the Particle Pack, ambience audio (`Assets/_Project/Audio`), a Cinemachine 3 camera path on Unity Splines, the Windows build (`Builds/` is empty; the 60 fps lock only takes effect in a build), then capture at 1920×1080, 60 fps per the demo script.

Still pending by hand from `docs/STATUS.md`: delete the deactivated training objects in `Cell.unity`, delete `Assets/Screenshots/`, restart the Editor once for the colour-space and input-backend switch.

### Later phases (not scheduled)

Touch/kiosk view, Quest 3 view, OPC UA via `OPCFoundation/UA-.NETStandard`, and whether to show realvirtual's S7 interface.

---

## 12. Gaps, mismatches and risks

Found while writing this blueprint:

| # | Finding | Why it matters |
|---|---|---|
| 1 | **No press in 3D.** `Cell/Press` has no renderer or children. | Demo beat 4 shows the press turning red. Geometry has to come from somewhere with a known licence before the highlight can be built. |
| 2 | **The demo script names signals that do not exist**: `cell/*/state`, `outfeed/count`, `press/health`. The real ones are `line/state` and `line/parts_out`; there is no health signal. | Update `docs/demo-script.md` or add the signals; either way decide before building the dashboard against them. |
| 3 | **OEE and failure risk are undefined.** No reject or quality data exists, and nothing computes risk. | OEE needs availability × performance × quality; quality has no source in this cell. Risk could be derived from tool wear, torque and temperature spread, but it is a design decision, not a wiring task. |
| 4 | **The arm bake script is not in the repo.** `docs/STATUS.md` points at `scratchpad/ur5e_kin.py`; no such file exists here. | The eight poses cannot be regenerated after a layout change without rewriting it. Worth committing under `tools/`. |
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
