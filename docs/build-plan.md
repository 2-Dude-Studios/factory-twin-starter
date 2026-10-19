# Manufacturing Demo — Factory Digital Twin build plan (Rev 2, 19 Oct 2026)

Source tracker (62 items, licence verdicts, download status): https://claude.ai/artifact/AGRYueGCfv6UwgTbnQcdre
Video SOP to follow once the build exists: `claude/walkthrough-video-playbook.md`

Rev 2 widens the goal from one fault-injection beat to six use cases (M4–M10 below). M0–M3 are done and unchanged.

## What this is

A Unity prototype for the Manufacturing landing page on 2dudestudios.com and for showing to manufacturing prospects in person. It is 2 Dude Studios' own marketing asset, not client work, so it is built on the current Unity Personal/Pro licence. A Unity Industry seat gets bought the day the first manufacturing project lands — Unity counts the client's revenue, not ours, so any real factory will require it. Price the seat into the first quote.

Its job is to prove that the studio can build a robotic or otherwise automated factory as a digital twin that serves six uses:

| # | Use case | What the prototype has to show | Milestone |
|---|---|---|---|
| 1 | **Real-time monitoring & visibility** | Every drive, sensor and joint live on a dashboard and on the broker; hover any machine to read its signal. | M4 |
| 2 | **Predictive maintenance** | The press's failure risk and remaining tool life climb *before* the failure, and the twin recommends maintenance. | M5 |
| 3 | **Workforce training & safety** | A person entering the arm's zone stops it; a guided lockout procedure clears a press fault and catches wrong steps. | M6 |
| 4 | **Virtual commissioning** | The cell runs from controller logic written in a PLC language, outside Unity, and a scripted test suite finds a seeded logic bug. | M7 |
| 5 | **Factory layout & what-if simulation** | Move a station, the arm re-solves its motion, the cell runs faster than real time and two layouts are compared. | M8 |
| 6 | **Pre-planning** | Given a production target, the twin sweeps configurations and reports which meet it, the bottleneck and the maintenance windows. | M9 |

All six run on the same cell and the same signal contract. Each milestone adds one capability on top of what is already there, so it builds on the signal bus rather than being bolted on.

Decisions already made: factory digital twin, Unity, URP, Unity 6.3. Training is in scope as a desktop feature of the twin (M6); a VR training product is not. Unity Simulation Pro (early access since Sep 2026) needs a Unity Industry licence and targets robot and sensor validation (LiDAR, ROS 2, headless Linux); it is not used here. On a client robotics job it is the likely replacement for URDF-Importer.

## The demo in one paragraph

One production cell in a factory hall. Parts arrive on an infeed conveyor, pass a sensor gate, a six-axis arm picks each one and places it on an outfeed conveyor, parts exit. A press alongside reports machine telemetry. Every drive, sensor and the arm publish state to a live dashboard (OEE, cycle time, machine health, failure risk) fed by MQTT. Around that one cell, the prototype shows the twin's whole life: planned before it is built (pre-planning, layout what-if), its control logic tested before the machine exists (virtual commissioning), watched and maintained while it runs (monitoring, predictive maintenance), and used to train the people who work beside it (training and safety). The fault-injection beat stays the centre of the pitch: the twin is not a render, it is wired to data.

Three views of the same scene: desktop walkthrough (the website videos), touch/kiosk overview, and a Quest 3 pass-through/VR view for the arena (later phase, only if time allows).

## The stack (all free, all verified 22 Sep 2026 — see tracker for links)

| Layer | Choice | Licence | Notes |
|---|---|---|---|
| Base scene | Factory Training \| Industry Sample (Unity, Asset Store) | Standard Asset Store EULA | Free, 2.8 GB, URP, needs Unity 6000.3.0. No Industry seat needed. |
| Twin logic | realvirtual.io Digital Twin Starter 6 | Asset Store EULA + realvirtual EULA | Free, commercial use permitted (single-user), distribute compiled only. Includes drives, sensors, transport/conveyors, signal model, S7 TCP/IP. **No** OPC UA/MQTT, **no** robot IK, **no** CAD import in Starter. |
| Paths | com.unity.splines 2.8.4 | Unity Companion | Conveyor routes, AGV routes. |
| Robot model | ros-industrial/universal_robot → UR5e or UR10e | Apache-2.0 + BSD-3 | Do **not** use UR8LONG/15/18/20/30 meshes (UR's own terms). Keep branding off the model for the public page. |
| Robot import | Unity-Technologies/URDF-Importer v0.5.2 | Apache-2.0 | Unity 2020.2+, no Unity 6 statement; expect small fixes. Install via Package Manager git URL. |
| Robot motion | Baked joint angles (M2), analytic UR inverse kinematics in C# (M8) | Our code | Starter has no IK. M2 bakes poses offline; M8 ports the same closed-form solution to C# so stations can move. |
| Live data | MQTTnet 4.3.7, vendored DLL | MIT | Local Mosquitto broker on the studio PC. Map topics → realvirtual Signal components. |
| Telemetry stand-in | AI4I 2020 Predictive Maintenance Dataset | CC BY 4.0 | 10,000 rows: air/process temp, rpm, torque, tool wear, five failure modes. Replay it through the broker. Cite: UCI ML Repository, doi:10.24432/C5HS5C. |
| OPC UA (M7 candidate) | OPCFoundation/UA-.NETStandard | MIT | Now targets .NET Standard 2.1. Comes in at M7 if the soft PLC chosen there speaks OPC UA rather than MQTT. Ask before adding. |
| Soft PLC (M7, outside the build) | To be chosen at M7 | Check before use | Runs as a separate process, never shipped inside the build. See open questions. |
| Lighting | Poly Haven — Industrial Workshop Foundry HDRI | CC0 | |
| Materials | ambientCG metals / concrete | CC0 | Raw files may live in the repo. |
| VFX | Unity Particle Pack 3.1 | Asset Store EULA | URP, sparks/steam. |
| Audio | Sonniss GDC bundles, Pixabay CC0 | Sonniss GDC / Pixabay | No standalone redistribution. |

Do not use: Industry Fundamentals template (Industry seat/trial only, no post-trial grant), Unity Simulation Pro (Industry licence only), AWSIM assets (CC BY-NC), realvirtual-WEB (AGPL) inside the build, the MARS-era `template-training` repo, any unlicensed snippet repo.

## Repo layout

```
factory-twin/
  Assets/
    _Project/            # everything we write
      Scenes/Cell.unity
      Scripts/Twin/      # signal bus, MQTT bridge, dataset replayer, fault injector
      Scripts/Robot/     # joint controller, pick-place program, IK (M8)
      Scripts/UI/        # dashboard (UI Toolkit, runtime data binding)
      Data/ai4i2020.csv
      Prefabs/
    ThirdParty/          # Asset Store imports, untouched
  Packages/manifest.json # splines, urdf-importer (git url), input system, xr (later phase)
  tools/
    broker/              # mosquitto.conf, docker-compose
    replay/              # python: csv → MQTT at N rows/sec with fault flags
    kinematics/          # UR5e closed-form IK reference + pose bake (M8)
    plc/                 # soft-PLC project for virtual commissioning (M7)
  docs/
    LICENSES.md          # one line per third-party item, copied from tracker
    demo-script.md       # the hub video and the six use-case clips
```

Git: LFS for anything over 10 MB. The Factory Training sample and realvirtual go through Asset Store import, not git — record package versions in `docs/LICENSES.md`.

## Milestones

Days are rough estimates of focused time. Each milestone from M4 on ends with a rough capture of its clip (see `docs/demo-script.md`), so the project can stop after any milestone and still have something to show. Final lighting and capture are in M10.

**M0 — Setup (day 1). Done.** Unity 6000.3 project, URP, Factory Training sample imported, realvirtual Starter imported and its demo scene running. Mosquitto up. `LICENSES.md` started. Exit: realvirtual demo conveyor moves in our project.

**M1 — Cell (days 2–4). Done.** Carve one cell out of the Factory Training hall. Infeed conveyor → sensor gate → pick station → outfeed conveyor, built from realvirtual transport/drive/sensor components. Parts spawn, travel, get detected, exit. Exit: a loop that runs for ten minutes without stalling.

**M2 — Arm (days 5–7). Done.** UR5e imported via URDF-Importer, branding removed, ArticulationBody joints. Baked pick-and-place synced to the sensor gate. Exit: arm picks every part the sensor reports; no misses in a 5-minute run.

**M3 — Data spine (days 8–10). Done.** Signal bus: every drive, sensor and arm joint writes to a topic. AI4I rows replayed as press telemetry. Fault injector switches the press to failure rows on demand. Exit: `mosquitto_sub -t 'cell/#'` shows the whole cell live.

**M4 — Monitoring & visibility (≈3 days).** Use case 1. The dashboard from the blueprint's M4 list: UI Toolkit overlay with runtime binding, KPI tiles (parts out, cycle time, parts/hour, OEE), per-station health, event log, fault Inject/Clear buttons, broker link indicator, credits panel. The press gets geometry and turns amber/red in 3D from its fault topics. Hover any conveyor, sensor, the arm or the press to see its topic and live value. Decide retained publishes plus a last will on `cell/link/connected`, so a client joining mid-run sees the whole cell at once. New signals: `cell/line/parts_per_hour`, `cell/line/oee`, `cell/<station>/health` (0 ok, 1 warning, 2 fault).
Exit: the "inject fault" beat looks good on camera, and a second MQTT client started mid-run shows the same values as the dashboard.
Decide at start: what OEE's quality factor comes from. This cell has no reject data. Either show availability × performance, labelled as such, or add an inspection result at the gate.

**M5 — Predictive maintenance (≈3 days).** Use case 2. Today the injector jumps straight to a failure row, which shows detection, not prediction. M5 adds a degradation mode: tool wear and torque climb through a warning band to failure over about a minute. A failure-risk score is computed from the press telemetry, plus remaining tool life and a maintenance recommendation in the event log. The risk definition is documented and tested. No ML package: either margins to the AI4I failure thresholds, or a small classifier trained offline in `tools/` whose coefficients live in a ScriptableObject. New signals: `cell/press/failure_risk` (0–1), `cell/press/tool_rul_min`, `cell/press/maint_due`. M4's planned `cell/press/health` is derived from these.
Exit: in a degradation run, the risk gauge passes its warning level at least 30 s before `press/machine_failure`, and the recommendation appears in the event log. The score's hit rate on held-out AI4I rows is recorded in `docs/STATUS.md`.

**M6 — Workforce training & safety (≈4 days).** Use case 3. Safety: a protective zone around the arm (a sensor volume, like a laser scanner). A person entering it stops the arm where it is and holds the line, and restarting needs an explicit reset. The person is an operator figure walking a path, or the viewer in first person. Training: a guided lockout procedure for clearing a press fault. Acknowledge the alarm, stop the line, lock out the press, change the tool (which resets tool wear), remove the lock, restart. Each step is a signal, and out-of-order steps are flagged and counted. Desktop first; the same procedure is what a later Quest view would run. New signals: `cell/safety/arm_zone_occupied`, `cell/safety/protective_stop`, `cell/press/lockout`, `cell/training/step`, `cell/training/errors`.
Exit: walking into the zone stops the arm within one physics tick of the signal, and the dashboard shows why. The lockout procedure can be completed end to end, and starting the line with the lock on is refused and logged.
Decide at start: the operator figure. Check whether the Factory Training sample has a usable worker model; otherwise use a licensed one, with its line in `docs/LICENSES.md` in the same commit.

**M7 — Virtual commissioning (≈5 days).** Use case 4. A logic-source switch: the in-Unity `CellSequencer` (today) or an external controller. The external controller runs the same sequence written in IEC 61131-3 (Structured Text) in a soft PLC, outside Unity. It talks to the twin through the existing signal contract (MQTT), or through OPC UA if that is what the PLC speaks. A heartbeat watchdog holds the line if the controller goes silent. An I/O list is exported from the topic catalogue (CSV), because that is what commissioning engineers work from. Sensor fault injection extends to the cell: force a gate sensor on or off, drop a part, keep the arm busy. A scripted commissioning suite (normal run, stuck sensor, missing part, arm not ready, press fault, protective stop) runs against either logic source and writes a pass/fail report. New signals: `cell/plc/source`, `cell/plc/heartbeat`, `cell/gate1/force`.
Exit: the cell runs 10 minutes from the external controller with `CellSequencer` off. The suite passes against both logic sources, and a deliberately seeded logic bug in the PLC program fails it.
Decide at start: which soft PLC (see open questions). Adding OPC UA or any new DLL needs asking first.

**M8 — Factory layout & what-if simulation (≈4 days).** Use case 5. Port the UR5e closed-form inverse kinematics from the M2 bake script to C#. It is pure, tested to zero TCP error against the baked poses, and replaces them at runtime. This reverses the M2 "no runtime IK" decision on purpose: a layout that moves needs it. The bake script itself goes into `tools/kinematics/` (blueprint gap 4). Layouts are data, as ScriptableObject variants: station positions, belt speeds, source rate, arm speed. Stations can be moved on a grid in Play, with the arm's reach envelope drawn and an unreachable placement refused. Faster-than-real-time running through `Time.timeScale`; measure the highest speed-up that keeps physics stable. New signals: `cell/sim/time_scale`, `cell/sim/layout`.
Exit: switching to a second layout variant re-solves the arm with no re-bake. One simulated hour runs at the measured speed-up, and both layouts' throughput, cycle time and utilisation are shown side by side.

**M9 — Pre-planning (≈3 days).** Use case 6. A scenario runner on top of M8 sweeps layout variants, speeds and source rate. It runs each at the M8 speed-up (or in a batch-mode build without graphics, if that proves stable), and reports throughput, utilisation per station, the bottleneck, and whether a production target (parts per shift) is met. Planned maintenance windows from M5's remaining tool life go into the shift plan. Results appear in a planning tab on the dashboard and as a CSV.
Exit: given a target of N parts per shift, the runner reports which configurations meet it, with the bottleneck and maintenance downtime for each. One configuration it predicts is re-run in real time and lands within 5% of the prediction.

**M10 — Polish and capture (≈4 days).** Foundry HDRI, ambientCG floors, sparks/steam, ambience, Cinemachine 3 camera paths (one per clip), realvirtual's runtime toolbar hidden, 60-fps lock, Windows build. Then hand over to the video playbook: voiceover first, capture the hub video and the six clips against it.

M4–M10 come to roughly 26 days of focused time, about five more weeks after M3. That is beyond the 2–3 weeks originally budgeted for a sellable demo. The order puts the cheapest and most convincing proofs first: M4 and M5 together are the original pitch, M6 is cheap and very visible, and M7–M9 are the deepest. If time runs short, stop after M6 and go straight to M10 with four clips.

## Claude Code kickoff prompt (paste into the project)

> This is a Unity 6000.3 URP project for a factory digital-twin marketing demo. Third-party packages: realvirtual.io Starter 6 (do not modify files under ThirdParty/), Unity Splines, URDF-Importer, M2MqttUnity. Read docs/demo-script.md and docs/LICENSES.md first. Write all new code under Assets/_Project. Conventions: one MonoBehaviour per responsibility, realvirtual Signal components are the only bridge between 3D and data, MQTT topic scheme `cell/<station>/<signal>`, no hard-coded broker addresses (read from a ScriptableObject config). Current milestone: M1 — build the infeed→sensor→pick→outfeed loop.

## Licence rules for this repo (copy into LICENSES.md header)

1. Asset Store and Fab content ships only inside built applications; raw files are never handed to a client.
2. realvirtual is distributed compiled only; never share its source, prefabs or scenes.
3. Every CC BY item gets a credit line in the app's About panel and on the web page.
4. Vendor branding (UR, KUKA, ABB, FANUC) is removed from any model shown publicly until vendor terms are confirmed.
5. When a client project starts on this codebase: buy the Unity Industry seat, upgrade realvirtual to Professional if the client needs OPC UA/PLC, consider Unity Simulation Pro (Industry only) in place of URDF-Importer for robot work, and re-run this list.

## Video handoff

The prototype feeds one ~90-second hub video and six short clips, one per use case. Beats are in `docs/demo-script.md`.

- **Hub video:** promise (a factory you can plan, test, run and train on before it exists), proof (the fault-injection beat), then the lifecycle in quick cuts (plan → commission → run → people), path (how a client's own line gets modelled — CAD in, signals mapped, deployed on desktop/kiosk/Quest), ask (book a call).
- **Six clips:** 20–40 s each, one for each use-case section of the Manufacturing page.

Voiceover is rendered before capture. Naming: `2DS_Manufacturing_FactoryTwin_<hub|monitoring|pdm|safety|vc|layout|planning>_v01_<date>`.

## Open questions

- Arm choice for the public page: de-branded UR5e or a fully generic arm modelled in-house? Decided at M2: de-branded UR5e.
- OEE quality factor (M4): show availability × performance only, or add an inspection result at the gate?
- Failure-risk definition (M5): margins to the AI4I failure thresholds (explainable) or an offline-trained classifier (sounds more "predictive")? Either can be tested and shipped without an ML package.
- Soft PLC for virtual commissioning (M7). Candidates: CODESYS Control Win (trial runtime), OpenPLC, Siemens PLCSIM Advanced (trial; would also make the S7 story real). Pick on three checks: it runs Structured Text, it can reach us over MQTT or OPC UA (directly or through a bridge), and its licence allows use in a marketing video. It never ships inside the build.
- Quest 3 view (later phase): pass-through tabletop twin or room-scale? Only decide after M10 ships. M6's lockout procedure is the natural first VR content.
- Whether to show the S7 interface at all. Folded into the M7 soft-PLC choice.
