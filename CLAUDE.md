# factory-twin — Claude Code project guide

Unity 6000.3 · URP · Windows target · marketing demo of a factory digital twin for 2 Dude Studios (2dudestudios.com, Manufacturing page).
Full plan: `docs/build-plan.md`. Demo beats: `docs/demo-script.md`. Third-party licences: `docs/LICENSES.md`.

## What we are building
One production cell: infeed conveyor → sensor gate → six-axis arm pick-and-place → outfeed conveyor. Every drive, sensor and joint publishes state over MQTT to a UI Toolkit dashboard. The prototype proves one use case, predictive maintenance: the press's failure risk climbs and the twin recommends a tool change before the failure; ignored, the press fails and the line stops, in 3D and on the dashboard together. That beat is the whole pitch. Other use cases are parked under "Later" in docs/build-plan.md.

## Hard rules
- Write new code only under `Assets/_Project/`. Never edit anything under `Assets/ThirdParty/` (Factory Training sample, Fast IK, Particle Pack, MQTTnet) or under `Assets/realvirtual/` and `Assets/Gizmos/realvirtual/` (realvirtual's fixed import paths). All are updated by re-import and kept out of git.
- realvirtual `Signal` components are the ONLY bridge between the 3D scene and data. Scripts never read a conveyor's transform to know its state; they read the signal.
- MQTT topic scheme: `cell/<station>/<signal>` — e.g. `cell/infeed/drive_speed`, `cell/gate1/part_present`, `cell/arm/j3_deg`, `cell/press/tool_wear_min`. Payload is plain JSON `{ "v": <value>, "t": <unix_ms> }`.
- No hard-coded broker addresses or ports. Read from `TwinConfig` (ScriptableObject at `Assets/_Project/Data/TwinConfig.asset`).
- One MonoBehaviour per responsibility. No God managers.
- Do not add packages without asking. Current set: com.unity.splines, URDF-Importer (git URL), Input System, Cinemachine, UI Toolkit (built in). MQTT client is MQTTnet 4.3.7, vendored as a DLL under `Assets/ThirdParty/MQTTnet/` (not a package).
- Any third-party file, model, texture, sound or dataset added to the repo gets a line in `docs/LICENSES.md` in the same commit. If the licence is unknown, do not add the file.
- Keep vendor branding (UR, KUKA, ABB, FANUC) off any model that will be shown publicly.
- Frame-rate lock 60 in builds (`Application.targetFrameRate = 60`) — the video pipeline needs it.

## Conventions
- C#: namespaces `TwoDude.FactoryTwin.<Area>`; `[SerializeField] private` fields; events via C# `event Action<T>`; no `FindObjectOfType` at runtime, wire references in prefabs or a small `CellContext`.
- Scenes: `Cell.unity` is the only scene. Sub-areas as prefabs under `Assets/_Project/Prefabs/`.
- UI: UI Toolkit with runtime data binding; UXML/USS under `Assets/_Project/UI/`. No uGUI.
- Tests: EditMode tests under `Assets/_Project/Tests/` for the signal bus, topic parsing and the dataset replayer. Run with `unity -runTests` (see `.claude/commands/test.md`).
- Commit messages: `M<n>: <what>` where n is the milestone from `docs/build-plan.md`.

## Working with the Unity Editor from Claude Code
The `unity` CLI / MCP is available on this machine. Prefer it for scene edits over hand-editing `.unity` YAML. If it is not connected, write the script and a short note of what to wire in the Inspector instead of guessing GUIDs.
Log location on Windows: `%LOCALAPPDATA%\Unity\Editor\Editor.log` — read the tail when a compile fails.

## Milestones (exit conditions in docs/build-plan.md)
M0 setup · M1 cell loop · M2 arm · M3 data spine · M4 dashboard · M5 prediction · M6 polish + capture.
Current milestone is recorded at the top of `docs/STATUS.md`. Update it when a milestone's exit condition is met.

## When unsure
Ask before: adding a package, changing the topic scheme, touching ThirdParty, importing any asset whose licence is not on the tracker.
