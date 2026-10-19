# Status

**Current milestone:** M4 — Dashboard (M3 closed 2026-10-12; plan narrowed to predictive maintenance, M4–M6, on 2026-10-19 — build plan Rev 3)
**Last updated:** 2026-10-12

## Done
- Repo scaffold, CLAUDE.md, licence register, broker + replayer tooling.
- Git repo initialised (LFS hooks installed), scaffold committed.
- URP 17.3.0 enabled on the existing project: stock pipeline assets in `Assets/_Project/Settings/`, assigned in Graphics settings, Linear colour space. Verified in Editor 6000.3.19f1 — clean compile.
- Packages added: Splines 2.9.0, Cinemachine 3.1.7, Input System 1.19.0 (Active Input Handling = Both, so legacy-Input code in third-party assets keeps working), Test Framework 1.6.0, URDF-Importer v0.5.2 (compiles on 6000.3 with no fixes).
- UnitySkills v2.8.4 installed; REST server on localhost:8090 (mode: auto). Lets Claude Code drive the Editor.
- Broker verified: `docker compose up -d`, pub/sub round-trip on `cell/#` (using `docker exec factory-twin-broker mosquitto_sub/pub` — no host Mosquitto install needed).
- Replayer verified against the broker with a synthetic 3-row CSV (33 messages, correct topics/payloads, paho-mqtt 2.1.0). Real dataset downloaded from UCI (10,000 rows; 339 failures: TWF 46, HDF 115, PWF 95, OSF 98, RNF 19) and verified through both `replay.py` and the C# `Ai4iCsv` parser. CC BY 4.0 — credit line still owed in the Credits panel (M4) and on the web page.
- Ahead of M3 (realvirtual-independent, so buildable now): `TopicPath`, `TopicFilter`, `SignalPayload`, `SignalBus`, `Ai4iCsv`, `DatasetReplayer`, `TwinConfig` (+ `Assets/_Project/Data/TwinConfig.asset`), `FrameRateLock`, in assembly `TwoDude.FactoryTwin.Twin`. 84 EditMode tests — pass in the Editor (Unity Test Runner, 11 s) and under `dotnet test` (NUnit 3).
- realvirtual Starter 6.0.11 (build 8592) imported at its default paths `Assets/realvirtual/` + `Assets/Gizmos/realvirtual/`; compiles with 0 errors under URP 17.3. Both paths are git-ignored; the project settings its importer changed (layers, physics matrix, `REALVIRTUAL` define) are committed.
- Factory Training sample, Fast IK 1.2 and Particle Pack 3.1 imported. The sample imports flat into `Assets/` as a whole project (it also rewrote every ProjectSettings file and added ~24 packages); everything was moved via AssetDatabase (GUIDs kept) to `Assets/ThirdParty/FactoryTraining|FastIK|ParticlePack/`. Restored Active Input Handling = Both and the `REALVIRTUAL` define afterwards. 0 compile errors.
- Render pipeline is now the sample's `ThirdParty/FactoryTraining/Settings/assets/PC_RPAsset_DC` (Deferred+, Decals, SSAO) — the hall's look depends on it, so it stays active. Our `_Project/Settings/PC_RPAsset` is kept as a fallback but unused.

- **M0 exit met.** `DemoRealvirtual.unity` in Play: ConveyorEntry1 `IsRunning`, 200 mm/s; MU `Turbine-1` moved 0.38 m in 4 s; 0 console errors. Verified through UnitySkills (`editor_play` / `gameobject_get_info`).
- Found and fixed on the way: the Factory Training import had wiped realvirtual's 10 `rv*` layers, 3 tags and its physics collision matrix (TransportSurface threw at Start). TagManager merged (sample layers 6–9, realvirtual 13–22), DynamicsManager restored from the realvirtual import commit.

## M1 — Cell loop (exit: infeed → gate → pick station → outfeed runs 10 min without stalling)
- [x] `Assets/_Project/Scenes/Cell.unity` — Save As of the Factory Training hall (keeps its baked lighting; the sample file is untouched). Training content deactivated. Cell built on the hatched keep-clear zone at (−5…0.5, −1…3.9): infeed `ConveyorSmall` (flows −x, z 0.65) → `Gate1` light barrier → pick station at (−2.0, 0, 1.2) → outfeed `ConveyorSmall` (flows −x, z 1.2) → `ExitSensor` → `Sink`. Parts from a realvirtual `Source` every 400 mm. L-shaped so the M2 arm does a 90° swing, pick and place both 0.55 m from its base (inside UR5e reach).
- [x] `TwinSignals` bridge (assembly `TwoDude.FactoryTwin.Twin.Realvirtual`, the only code referencing realvirtual): 11 realvirtual PLC signals under `Cell/Signals`, mirrored to/from `cell/<station>/<signal>` on the bus. Inputs publish on change; outputs are written when their topic is published.
- [x] `CellSequence` (pure virtual PLC, 9 tests) + `CellSequencer` driving it from the bus; `CellTopics` is the topic list; `CellContext` owns the bus. Status mirrored read-only on the `CellSequencer` Inspector.
- [x] Placeholder pick: `PartTransfer` lifts the part out of `Gate1` and sets it on the outfeed over 2.5 s, via `arm/pick_cmd` → `arm/busy`, the same contract the arm will use.
- [x] `ProjectSettingsGuard` (Editor): re-enables Run In Background on every reload and errors if realvirtual's layers go missing.
- [x] **M1 exit met.** 10-minute soak in Play (673.7 s simulated): 143 parts out, 12.8–13.8 parts/min every minute (first minute 9.9 while the line fills), steady 4.58 s cycle ≈ 771 parts/h, 0 pick retries, 0 stalls, parts in the cell never above 9, no console errors from our code, no play-mode resets.
- [x] Build Settings contain only `Cell.unity` (enforced by `ProjectSettingsGuard`).

## M2 — Arm (exit: arm picks every part the gate reports; no misses in a 5-minute run)
- [x] **De-branded UR5e.** `ur_description` @39ad110 expanded from xacro to `Assets/_Project/Robots/ur/cell_arm.urdf`; UR5e meshes only. The `URBlue` material is renamed `CellAccent` and recoloured to machine grey — the UR5e meshes carry no logos or textures, only that brand colour. Provenance and the exact edits are in that folder's README.md; licence row updated.
- [x] Imported as 6 ArticulationBody joints via `Tools > Factory Twin > Import Cell Arm (UR5e)` (the importer's own entry point needs a Project-window selection, which cannot be scripted). Mounted on the 0.6 m realvirtual stand at (−2.0, 0.6, 1.2).
- [x] Waypoints baked from the UR's closed-form kinematics offline (`scratchpad/ur5e_kin.py`) rather than runtime IK, as the build plan recommends: 8 poses, 0.00 mm TCP error, minimum-travel IK branch, shoulder kept in its normal working range.
- [x] `ArmProgram` (pure, 7 tests) plays the poses at 110 °/s and advances only once the measured joints arrive; `ArmController` drives the ArticulationBodies and applies the drive gains the import leaves at zero.
- [x] `ArmSignalBridge` + `PartGripper` keep signals the only 3D↔data bridge: `arm/pick_cmd` in; `arm/busy`, `arm/grip_cmd`, `arm/holding` and `arm/j1_deg`…`j6_deg` out. 19 signals bound in TwinSignals. The M1 `PartTransfer` placeholder is disabled.
- [x] **M2 exit met.** 5.6-minute run: 32 arm cycles, 33 parts out, **0 pick retries, 0 stalls**, 7.0 s cycle (≈516 parts/h), never more than 8 parts in the cell, no console errors from our code, no play-mode resets.

## M3 — Data spine (exit: `mosquitto_sub -t 'cell/#'` shows the whole cell live)
- [x] MQTT client: MQTTnet 4.3.7 vendored as a DLL in `Assets/ThirdParty/MQTTnet/` (replaces the planned M2MqttUnity; reason in `docs/LICENSES.md`).
- [x] `SignalPublisher` (pure, 13 tests): bus → MQTT, rate-limited per signal (10 Hz) and deadbanded. `MqttBridge` on the `Cell` object owns the client; broker, rate and deadband come from `TwinConfig`. Changes made while the broker is unreachable are held (latest value per topic) and the whole cell is re-sent when the link comes up.
- [x] Subscribe side: broker → bus on `cell/#` (MQTT 5 no-local). Messages that arrive from the broker are not sent back to it. Verified: a speed setpoint published from the broker (`cell/infeed/drive_speed` 120) changed the belt, and `cell/infeed/speed_mm_s` reported 120.
- [x] `TwinSignals` now also publishes PLC outputs that a station writes directly, so command signals show up too (`arm/grip_cmd`, `infeed/drive_speed`, `outfeed/drive_speed`).
- [x] Live check, 45 s in Play with `mosquitto_sub -t 'cell/#'` (via `docker exec factory-twin-broker`): 24 cell topics, about 1,500 messages, 0 console errors. That is the production cell complete; the press station is the remaining part of the exit condition.
- [x] Press station in Unity: `PressStation` on `Cell/Press` hosts `DatasetReplayer` (2 rows/s, looping, fault filter on the Inspector), so `cell/press/*` flows without the Python script. Verified in Play: 10,000 rows loaded, 11 press topics on the broker with the dataset's first rows (298.1 K, 308.6 K, 1551 rpm, 42.8 Nm) alongside the 24 cell topics, 0 console errors. Disable the component when `tools/replay/replay.py` feeds the broker instead, or both write the same topics.
- [x] Fault injector: `FaultInjector` on `Cell/Press`. A `FaultMode` value on `cell/press/inject_fault` (1 TWF, 2 HDF, 4 PWF, 8 OSF, 16 RNF; 0 clears) switches the press to dataset rows carrying that failure. It can come from the broker, from `Inject()`/`Clear()` (the M4 button), or from the component's `Fault` field in Play mode. The press runs a healthy baseline until then (`PressStation.healthyBaseline`).
- [x] The line holds on a press fault: `CellSequence` has a `Hold` input and a `Held` state (both conveyors off, no pick). `CellSequencer` asserts it while `press/machine_failure` or any `press/fault_*` is true. A pick already under way finishes first. 117 EditMode tests pass.
- [x] **M3 exit met.** 80 s in Play with `mosquitto_sub -t 'cell/#'`: 36 topics (24 cell, 11 press, the inject command), about 2,500 messages, 0 console errors. Tool wear injected from the broker: 0.5 s later the press reported 208 min with `fault_twf` and `machine_failure` true; the arm finished its pick, then `line/state` went to 3 and both conveyors stopped for the 18 s the fault stayed on. On clear the faults dropped within 0.3 s and parts flowed again (3 parts out before, 6 by the end; 0 pick retries, 0 stalls).

## Manual steps pending
- **Delete the deactivated training objects in Cell.unity** (`ImmersiveTraining`, `Canvas`, `EventSystem`, `DistributionCenterScripts`, `mottoes`, `stopTargets`, `NavMesh_GO`, `NavBlocker`, `cam`, `audio/audioSnapping`, and the hall `Main Camera`). Deletes are blocked for Claude; either delete them in the Hierarchy or add `gameobject_delete_batch` to the UnitySkills allowlist.
- Delete `Assets/Screenshots/` (UnitySkills capture output; git-ignored).
- Play mode restarted itself once during the first soak attempt (~375 s in; no compile, refresh or skill call in the log). Did not recur in the 10-minute rerun. If it happens again, check the realvirtual runtime toolbar's restart/reset controls.
- Restart the Editor once so the Linear colour space and Input System backend switch fully apply (Unity may prompt for this itself).
- Cinemachine is 3.x (namespace `Unity.Cinemachine`, `CinemachineCamera`, spline dolly on Unity Splines) — not the 2.x API most tutorials show.

## Blockers
(none)

## Decisions and risks
- **MQTT messages are not retained.** A subscriber that connects mid-run sees a signal only once it next changes; steady ones (drive speed setpoints, `outfeed/running`) stay silent until then. Start the subscriber before Play, or decide on retained publishes plus a last-will on `cell/link/connected` if a late-joining dashboard ever reads from the broker.
- **The sequencer owns `infeed/drive_fwd`.** It toggles it every cycle, so an external stop on that topic is overwritten within seconds. A fault that has to hold the line needs its own signal the sequencer respects.
- **realvirtual dirties `Cell.unity` on load and after Play** (layout-driven RectTransform values on its UI prefab). UnitySkills refuses to run tests on a dirty scene, so the scene gets re-saved with that noise; it shows up in diffs and is harmless.
- **Arm poses are baked, not solved at runtime.** Change the layout and they must be re-baked (`scratchpad/ur5e_kin.py`; inputs are the measured part rest pose and the arm base). Runtime IK is deliberately not shipped.
- **Measured, not assumed, geometry.** A part rests at the gate at x = −1.71 with its centre at 0.575 m (belt top 0.525). The first bake assumed 0.56 m and drove the flange into the part; with stiff drives PhysX launched parts thousands of metres. Re-measure if the conveyors or part size change.
- **The URDF import leaves the articulation root free-floating and the drives at zero gain**, so the arm fell through the floor on the first run. `ArmImporter` now pins the root and `ArmController` sets the gains, both reproducible on re-import.
- **Raycast sensors never fill `CollidingMus`.** Anything reading parts off a light barrier must also use `LastTriggeredBy` (`PartGripper` does, distance-checked).
- **Cell.unity is a copy of the sample scene** (placement data referencing sample assets, committed to git). The sample's own files stay out of git and ship only inside builds, so licence rule 1 holds; revisit if the repo is ever shared outside the studio.
- **Conveyors are straight realvirtual `ConveyorSmall`s, not spline-built.** Nothing in the M1 layout curves; Splines stay for curved routes, AGV paths and the M6 camera dolly.
- **`PartTransfer` talks to realvirtual `Sensor`/`MU` directly** (in the bridge assembly), a deliberate exception to the TwinSignals-only rule for an actuator stand-in that is deleted at M2.
- **Camera for now:** realvirtual's `Main Camera` (orbit navigation) is active; the hall's camera is off. realvirtual's runtime toolbar (uGUI) is visible in Play and must be hidden before capture. Both are replaced by the Cinemachine camera at M6.
- **Line status as numbers.** `cell/line/state` is 0 feeding, 1 transferring, 2 stalled, 3 held (press fault); the payload scheme has no strings.
- **An injected fault stops the line** (decided 2026-10-12). The demo script used to say the infeed slows to buffer; beat 4 in `docs/demo-script.md` now says the line holds.
- **The hold follows the press's fault signals, wherever they come from.** With `tools/replay/replay.py` feeding the broker, the dataset's own failure rows (about 1 in 30) will each hold the line for one row; run it with `--fault` on purpose, or add a healthy-only switch to it if that gets in the way.
- **Project-style imports overwrite ProjectSettings.** Factory Training did this once (see M0 exit note). If any Asset Store item is re-imported, diff `ProjectSettings/` against git before committing.
- **Packages added by the Factory Training import** (not chosen by us): Timeline, uGUI, Post Processing v2 (unused under URP), ProGrids preview (embedded), AI Navigation, Burst/Collections/Mathematics, Asset Manager, Visual Scripting-free. Left in place because the sample scene may reference them; prune after M1 once the cell is carved out and we know what the scene actually needs.
- QualitySettings "Mobile" level references the URP template's Mobile RP asset, which is not in the project. Harmless on Windows (PC level is active); tidy when pruning.
- **realvirtual location (decided).** Stays at `Assets/realvirtual/` + `Assets/Gizmos/realvirtual/` (its default; Unity only reads gizmo icons from `Assets/Gizmos/`). CLAUDE.md and `.claude/settings.json` treat both as ThirdParty.
- **Compatibility.** realvirtual 6.0.11 warned that Unity 6000.3.19f1 is outside its tested range; import was continued anyway. No compile errors, but watch for runtime oddities in its demo scene.
- **Assemblies (resolved).** realvirtual ships `realvirtual.base.asmdef`, so `TwoDude.FactoryTwin.Twin` can reference it and the `TwinSignals` facade can live in the Twin assembly as planned.
- realvirtual's asmdef references Cinemachine 2.x's assembly name (`Cinemachine`); we have 3.x (`Unity.Cinemachine`). Its optional Cinemachine helpers are therefore inactive — fine for us, our camera work uses 3.x directly.
