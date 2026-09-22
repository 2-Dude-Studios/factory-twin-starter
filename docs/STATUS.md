# Status

**Current milestone:** M2 — Arm (M1 closed 2026-09-22)
**Last updated:** 2026-09-22

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

## Pending (M0 leftovers, non-blocking)
- [ ] Import M2MqttUnity → `Assets/ThirdParty/M2MqttUnity/` (needed at M3)

## M1 — Cell loop (exit: infeed → gate → pick station → outfeed runs 10 min without stalling)
- [x] `Assets/_Project/Scenes/Cell.unity` — Save As of the Factory Training hall (keeps its baked lighting; the sample file is untouched). Training content deactivated. Cell built on the hatched keep-clear zone at (−5…0.5, −1…3.9): infeed `ConveyorSmall` (flows −x, z 0.65) → `Gate1` light barrier → pick station at (−2.0, 0, 1.2) → outfeed `ConveyorSmall` (flows −x, z 1.2) → `ExitSensor` → `Sink`. Parts from a realvirtual `Source` every 400 mm. L-shaped so the M2 arm does a 90° swing, pick and place both 0.55 m from its base (inside UR5e reach).
- [x] `TwinSignals` bridge (assembly `TwoDude.FactoryTwin.Twin.Realvirtual`, the only code referencing realvirtual): 11 realvirtual PLC signals under `Cell/Signals`, mirrored to/from `cell/<station>/<signal>` on the bus. Inputs publish on change; outputs are written when their topic is published.
- [x] `CellSequence` (pure virtual PLC, 9 tests) + `CellSequencer` driving it from the bus; `CellTopics` is the topic list; `CellContext` owns the bus. Status mirrored read-only on the `CellSequencer` Inspector.
- [x] Placeholder pick: `PartTransfer` lifts the part out of `Gate1` and sets it on the outfeed over 2.5 s, via `arm/pick_cmd` → `arm/busy`, the same contract the arm will use.
- [x] `ProjectSettingsGuard` (Editor): re-enables Run In Background on every reload and errors if realvirtual's layers go missing.
- [x] **M1 exit met.** 10-minute soak in Play (673.7 s simulated): 143 parts out, 12.8–13.8 parts/min every minute (first minute 9.9 while the line fills), steady 4.58 s cycle ≈ 771 parts/h, 0 pick retries, 0 stalls, parts in the cell never above 9, no console errors from our code, no play-mode resets.
- [x] Build Settings contain only `Cell.unity` (enforced by `ProjectSettingsGuard`).

## Manual steps pending
- **Delete the deactivated training objects in Cell.unity** (`ImmersiveTraining`, `Canvas`, `EventSystem`, `DistributionCenterScripts`, `mottoes`, `stopTargets`, `NavMesh_GO`, `NavBlocker`, `cam`, `audio/audioSnapping`, and the hall `Main Camera`). Deletes are blocked for Claude; either delete them in the Hierarchy or add `gameobject_delete_batch` to the UnitySkills allowlist.
- Delete `Assets/Screenshots/` (UnitySkills capture output; git-ignored).
- Play mode restarted itself once during the first soak attempt (~375 s in; no compile, refresh or skill call in the log). Did not recur in the 10-minute rerun. If it happens again, check the realvirtual runtime toolbar's restart/reset controls.
- Restart the Editor once so the Linear colour space and Input System backend switch fully apply (Unity may prompt for this itself).
- Cinemachine is 3.x (namespace `Unity.Cinemachine`, `CinemachineCamera`, spline dolly on Unity Splines) — not the 2.x API most tutorials show.

## Blockers
(none)

## Decisions and risks
- **Cell.unity is a copy of the sample scene** (placement data referencing sample assets, committed to git). The sample's own files stay out of git and ship only inside builds, so licence rule 1 holds; revisit if the repo is ever shared outside the studio.
- **Conveyors are straight realvirtual `ConveyorSmall`s, not spline-built.** Nothing in the M1 layout curves; Splines stay for curved routes, AGV paths and the M5 camera dolly.
- **`PartTransfer` talks to realvirtual `Sensor`/`MU` directly** (in the bridge assembly), a deliberate exception to the TwinSignals-only rule for an actuator stand-in that is deleted at M2.
- **Camera for now:** realvirtual's `Main Camera` (orbit navigation) is active; the hall's camera is off. realvirtual's runtime toolbar (uGUI) is visible in Play and must be hidden before capture. Both are replaced by the Cinemachine camera at M5.
- **Line status as numbers.** `cell/line/state` is 0 feeding, 1 transferring, 2 stalled; the payload scheme has no strings.
- **Project-style imports overwrite ProjectSettings.** Factory Training did this once (see M0 exit note). If any Asset Store item is re-imported, diff `ProjectSettings/` against git before committing.
- **Packages added by the Factory Training import** (not chosen by us): Timeline, uGUI, Post Processing v2 (unused under URP), ProGrids preview (embedded), AI Navigation, Burst/Collections/Mathematics, Asset Manager, Visual Scripting-free. Left in place because the sample scene may reference them; prune after M1 once the cell is carved out and we know what the scene actually needs.
- QualitySettings "Mobile" level references the URP template's Mobile RP asset, which is not in the project. Harmless on Windows (PC level is active); tidy when pruning.
- **realvirtual location (decided).** Stays at `Assets/realvirtual/` + `Assets/Gizmos/realvirtual/` (its default; Unity only reads gizmo icons from `Assets/Gizmos/`). CLAUDE.md and `.claude/settings.json` treat both as ThirdParty.
- **Compatibility.** realvirtual 6.0.11 warned that Unity 6000.3.19f1 is outside its tested range; import was continued anyway. No compile errors, but watch for runtime oddities in its demo scene.
- **Assemblies (resolved).** realvirtual ships `realvirtual.base.asmdef`, so `TwoDude.FactoryTwin.Twin` can reference it and the `TwinSignals` facade can live in the Twin assembly as planned.
- realvirtual's asmdef references Cinemachine 2.x's assembly name (`Cinemachine`); we have 3.x (`Unity.Cinemachine`). Its optional Cinemachine helpers are therefore inactive — fine for us, our camera work uses 3.x directly.
