# factory-twin — Claude Code project guide

Unity 6000.3 · URP · Windows target · marketing demo of a factory digital twin for 2 Dude Studios (2dudestudios.com, Manufacturing page).
Full plan: `docs/build-plan.md`. Demo beats: `docs/demo-script.md`. Third-party licences: `docs/LICENSES.md`.

## What we are building
One production cell: infeed conveyor → sensor gate → six-axis arm pick-and-place → outfeed conveyor. Every drive, sensor and joint publishes state over MQTT to a UI Toolkit dashboard. The prototype proves three use cases and is adding a fourth and a fifth. Predictive maintenance (done, M4–M6): the press's failure risk climbs and the twin recommends a tool change before the failure; ignored, the press fails and the line stops, in 3D and on the dashboard together. Virtual commissioning (Rev 4, M7–M9): a soft PLC (OpenPLC Runtime v4, outside Unity and the repo) runs the cell from Structured Text over Modbus TCP; a commissioning suite catches a seeded interlock bug, visible in 3D, and passes once it is fixed. Workforce training and safety (Rev 5, M10–M13): a trainee entering the arm's protective zone freezes the arm and holds the line until a reset; a CNC fault is cleared only through the OSHA 1910.147 lockout steps, the PLC refuses to start with the lock on, and every step is logged; desktop first, then Quest 3 over Link (M12). It is a simulated safety function and a training demo, never "safety-rated" or "certified". Fire safety training (Rev 6, M14–M18, plan in docs/fire-safety-training.md): hazard hunt, the twin flags an overheating cabinet, alarm holds the line, the right extinguisher with PASS on an incipient fire, evacuation by the alternate route and a roll call, every step logged by clause (OSHA 1910.38/.39/.157); a training demo, never "compliant" or "certified", and the regulatory content is guidance, not legal advice. Electrical safety and arc flash (Rev 7, M19–M23, after M18, plan in docs/electrical-safety-training.md): an infeed drive fault sends the trainee to the drive cabinet; label, PPE and approach boundaries, disconnect locked, the dashboard reads 0 V but the proved tester finds 120 V backfed from the UPS control feed, the second source is locked and tested again, and the PLC refuses to start with the cabinet lock on (OSHA 1910.332–.335, NFPA 70E-2027); tester readings never go on the bus; the same training-demo wording, label values from a made-up study. Each beat is the whole pitch of its clip. Other use cases are parked under "Later" in docs/build-plan.md.

## Hard rules
- Write new code only under `Assets/_Project/`. Never edit anything under `Assets/ThirdParty/` (Factory Training sample, Fast IK, Particle Pack, MQTTnet) or under `Assets/realvirtual/` and `Assets/Gizmos/realvirtual/` (realvirtual's fixed import paths). All are updated by re-import and kept out of git.
- realvirtual `Signal` components are the ONLY bridge between the 3D scene and data. Scripts never read a conveyor's transform to know its state; they read the signal.
- MQTT topic scheme: `cell/<station>/<signal>` — e.g. `cell/infeed/drive_speed`, `cell/gate1/part_present`, `cell/arm/j3_deg`, `cell/press/tool_wear_min`. Payload is plain JSON `{ "v": <value>, "t": <unix_ms> }`.
- No hard-coded broker addresses or ports. Read from `TwinConfig` (ScriptableObject at `Assets/_Project/Data/TwinConfig.asset`).
- One MonoBehaviour per responsibility. No God managers.
- Do not add packages without asking. Approved 2026-10-24 for M12 only: com.unity.xr.openxr, com.unity.xr.management, com.unity.xr.interaction.toolkit (not the Meta XR SDKs); 2026-10-27: com.unity.xr.hands (VR hands, hand tracking over Link). Current set: com.unity.splines, URDF-Importer (git URL), Input System, Cinemachine, Unity Recorder (Editor only, for capture), UI Toolkit (built in). MQTT client is MQTTnet 4.3.7, vendored as a DLL under `Assets/ThirdParty/MQTTnet/` (not a package).
- Any third-party file, model, texture, sound or dataset added to the repo gets a line in `docs/LICENSES.md` in the same commit. If the licence is unknown, do not add the file.
- Keep vendor branding (UR, KUKA, ABB, FANUC) off any model that will be shown publicly.
- Frame-rate lock 60 in builds (`Application.targetFrameRate = 60`) — the video pipeline needs it.

## Conventions
- C#: namespaces `TwoDude.FactoryTwin.<Area>`; `[SerializeField] private` fields; events via C# `event Action<T>`; no `FindObjectOfType` at runtime, wire references in prefabs or a small `CellContext`.
- Scenes: `Menu.unity` (the start menu that picks the training; loads first, decided 2026-11-04) and `Cell.unity` (the cell, everything else). Built by Tools > Factory Twin > Build Menu Scene; `ProjectSettingsGuard` keeps the build settings to Menu then Cell. Capture runs (`-takes`) and `-cell` skip the menu. Sub-areas as prefabs under `Assets/_Project/Prefabs/`.
- UI: UI Toolkit with runtime data binding; UXML/USS under `Assets/_Project/UI/`. No uGUI.
- Tests: EditMode tests under `Assets/_Project/Tests/` for the signal bus, topic parsing and the dataset replayer. Run with `unity -runTests` (see `.claude/commands/test.md`).
- Commit messages: `M<n>: <what>` where n is the milestone from `docs/build-plan.md`.

## Working with the Unity Editor from Claude Code
The `unity` CLI / MCP is available on this machine. Prefer it for scene edits over hand-editing `.unity` YAML. If it is not connected, write the script and a short note of what to wire in the Inspector instead of guessing GUIDs.
Log location on Windows: `%LOCALAPPDATA%\Unity\Editor\Editor.log` — read the tail when a compile fails.

## Milestones (exit conditions in docs/build-plan.md)
M0 setup · M1 cell loop · M2 arm · M3 data spine · M4 dashboard · M5 prediction · M6 polish + capture · M7 soft PLC link · M8 commissioning suite · M9 VC capture · M10 safety zone · M11 lockout procedure · M12 Quest 3 view (Link) · M13 training capture · M14 fire model + effects · M15 extinguisher + hazard hunt · M16 alarm + evacuation · M17 assessment + record · M18 fire capture · M19 electrical model + cabinet · M20 tester + PPE · M21 safe work procedure + hunt · M22 electrical assessment + record · M23 electrical capture.
Current milestone is recorded at the top of `docs/STATUS.md`. Update it when a milestone's exit condition is met.

## When unsure
Ask before: adding a package, changing the topic scheme, touching ThirdParty, importing any asset whose licence is not on the tracker.
