# Manufacturing Demo — Factory Digital Twin build plan (Rev 4, 22 Oct 2026)

Source tracker (62 items, licence verdicts, download status): https://claude.ai/artifact/AGRYueGCfv6UwgTbnQcdre
Video SOP to follow once the build exists: `claude/walkthrough-video-playbook.md`

Rev 3 narrowed the prototype to one use case, **predictive maintenance**: the most-asked-for digital-twin use in manufacturing, and the one this build is closest to. Rev 2 planned six use cases (M4–M10, commit `ee852e9`). M0–M6 are done; the PdM video (v02) is approved.

Rev 4 adds a second use case, **virtual commissioning** (M7–M9), for machine builders and system integrators. Chosen after a short research pass on 2026-10-22: machine builders know virtual commissioning but rarely use it, customers increasingly ask them for a virtual factory acceptance test, and competitors' demos are either engineering-tool screen recordings or whole-factory showpieces. None tells the story of one logic bug caught before site. The other four use cases stay under "Later".

## What this is

A Unity prototype for the Manufacturing landing page on 2dudestudios.com and for showing to manufacturing prospects in person. It is 2 Dude Studios' own marketing asset, not client work, so it is built on the current Unity Personal/Pro licence. A Unity Industry seat gets bought the day the first manufacturing project lands — Unity counts the client's revenue, not ours, so any real factory will require it. Price the seat into the first quote.

Its job is to prove that the studio can build a digital twin of an automated line that **sees a machine failure coming, shows where it is and what it will cost the line, and says when to act**. Analytics vendors already sell failure prediction without 3D; the studio's angle is making it visible and tied to the line's output.

Decisions already made: factory digital twin, Unity, URP, Unity 6.3, predictive maintenance as the one use case. Unity Simulation Pro (early access since Sep 2026) needs a Unity Industry licence and targets robot and sensor validation; it is not used here.

## The demo in one paragraph

One production cell in a factory hall. Parts arrive on an infeed conveyor, pass a sensor gate, a six-axis arm picks each one and places it on an outfeed conveyor, parts exit. A press alongside reports machine telemetry (temperatures, speed, torque, tool wear) over MQTT. As the press's tool wears, the twin's failure risk climbs, its remaining tool life counts down, and it recommends a tool change while the line is still running. Ignore it, and the press fails: it turns red in 3D, the line holds and the dashboard counts the parts lost. Act on it, and the line never stops. That contrast is the pitch: the twin is wired to data, and it turns that data into a decision.

Desktop walkthrough first (the website video). A touch/kiosk view and a Quest 3 view are later phases.

## The stack (all free, all verified 22 Sep 2026 — see tracker for links)

| Layer | Choice | Licence | Notes |
|---|---|---|---|
| Base scene | Factory Training \| Industry Sample (Unity, Asset Store) | Standard Asset Store EULA | Free, 2.8 GB, URP, needs Unity 6000.3.0. No Industry seat needed. |
| Twin logic | realvirtual.io Digital Twin Starter 6 | Asset Store EULA + realvirtual EULA | Free, commercial use permitted (single-user), distribute compiled only. Includes drives, sensors, transport/conveyors, signal model, S7 TCP/IP. **No** OPC UA/MQTT, **no** robot IK, **no** CAD import in Starter. |
| Paths | com.unity.splines 2.8.4 | Unity Companion | Camera dolly; conveyor and AGV routes if a layout ever curves. |
| Robot model | ros-industrial/universal_robot → UR5e or UR10e | Apache-2.0 + BSD-3 | Do **not** use UR8LONG/15/18/20/30 meshes (UR's own terms). Keep branding off the model for the public page. |
| Robot import | Unity-Technologies/URDF-Importer v0.5.2 | Apache-2.0 | Unity 2020.2+, no Unity 6 statement; expect small fixes. Install via Package Manager git URL. |
| Robot motion | Baked joint angles | Our code | Starter has no IK. Poses baked offline from the UR's closed-form kinematics (M2). |
| Live data | MQTTnet 4.3.7, vendored DLL | MIT | Local Mosquitto broker on the studio PC. Map topics → realvirtual Signal components. |
| Telemetry stand-in | AI4I 2020 Predictive Maintenance Dataset | CC BY 4.0 | 10,000 rows: air/process temp, rpm, torque, tool wear, five failure modes. Replay it through the broker. Cite: UCI ML Repository, doi:10.24432/C5HS5C. |
| OPC UA (later) | OPCFoundation/UA-.NETStandard | MIT | The 1.5.378.x line targets .NET Standard 2.1 (2.0.x drops it). Not in this build, but the video promises it (decided 2026-10-20), so the first client project that asks gets it. |
| Soft PLC (M7) | OpenPLC Runtime v4.3.1 (Autonomy-Logic/openplc-runtime) | MIT | External tool on the studio PC (Windows installer, MSYS2 inside), not in the repo. Runs the cell's Structured Text; Modbus TCP slave. The OpenPLC Editor v4 (GPL-3.0) may be needed to build the program: a tool only, never bundled. |
| PLC link (M7) | Our own Modbus TCP client | Our code | A few function codes over a socket; no package. NModbus (MIT) is the fallback, and only after asking. |
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
      Scripts/Twin/      # signal bus, MQTT bridge, dataset replayer, fault injector, risk model (M5)
      Scripts/Robot/     # joint controller, pick-place program
      Scripts/UI/        # dashboard (UI Toolkit, runtime data binding)
      Data/ai4i2020.csv
      Prefabs/
    ThirdParty/          # Asset Store imports, untouched
  Packages/manifest.json # splines, urdf-importer (git url), input system
  tools/
    broker/              # mosquitto.conf, docker-compose
    replay/              # python: csv → MQTT at N rows/sec with fault flags
    plc/                 # Structured Text for the soft PLC (M7), I/O list
  docs/
    LICENSES.md          # one line per third-party item, copied from tracker
    demo-script.md       # the 90-second walkthrough beats
```

Git: LFS for anything over 10 MB. The Factory Training sample and realvirtual go through Asset Store import, not git — record package versions in `docs/LICENSES.md`.

## Milestones

Days are rough estimates of focused time.

**M0 — Setup (day 1). Done.** Unity 6000.3 project, URP, Factory Training sample imported, realvirtual Starter imported and its demo scene running. Mosquitto up. `LICENSES.md` started. Exit: realvirtual demo conveyor moves in our project.

**M1 — Cell (days 2–4). Done.** Carve one cell out of the Factory Training hall. Infeed conveyor → sensor gate → pick station → outfeed conveyor, built from realvirtual transport/drive/sensor components. Parts spawn, travel, get detected, exit. Exit: a loop that runs for ten minutes without stalling.

**M2 — Arm (days 5–7). Done.** UR5e imported via URDF-Importer, branding removed, ArticulationBody joints. Baked pick-and-place synced to the sensor gate. Exit: arm picks every part the sensor reports; no misses in a 5-minute run.

**M3 — Data spine (days 8–10). Done.** Signal bus: every drive, sensor and arm joint writes to a topic. AI4I rows replayed as press telemetry. Fault injector switches the press to failure rows on demand. Exit: `mosquitto_sub -t 'cell/#'` shows the whole cell live.

**M4 — Dashboard (≈3 days).** Only what the predictive-maintenance story needs. A UI Toolkit overlay with runtime binding:
- Press panel: health, live telemetry (air and process temperature, speed, torque, tool wear) as sparklines, and a failure-risk gauge (empty until M5 feeds it).
- Line panel: line state, parts out, parts/hour, and parts lost while the line is held.
- Event log, Inject/Clear fault buttons, broker link indicator, credits panel.

The press gets geometry and turns amber/red in 3D from its health signal. realvirtual's runtime toolbar is hidden. New signals: `cell/press/health` (0 ok, 1 warning, 2 fault), `cell/line/parts_per_hour`, `cell/line/parts_lost` (held time × the running rate).
Exit: the "inject fault" beat looks good on camera. The press goes red, the line holds, and the dashboard's press health, line state and parts lost move together.

**M5 — Prediction (≈3 days).** Today the injector jumps straight to a failure row, which shows detection, not prediction. M5 adds:
- **Degradation mode:** tool wear and torque climb through a warning band to failure over about a minute.
- **Risk model:** a pure, tested class that turns press telemetry into failure risk, remaining tool life and a maintenance recommendation. Parameters live in a ScriptableObject; no ML package.
- **Tool change:** a command that resets tool wear, so acting on the recommendation can be shown.

New signals: `cell/press/failure_risk` (0–1), `cell/press/tool_rul_min`, `cell/press/maint_due`, `cell/press/tool_change` (in). `cell/press/health` is then derived from risk as well as faults.
Exit:
- In a degradation run, risk passes its warning level at least 30 s before `press/machine_failure`, and the recommendation appears in the event log.
- A tool change at the recommendation avoids the failure, and the line never holds.
- The score's hit rate on held-out AI4I rows is recorded in `docs/STATUS.md`.

Decide at start: the risk definition (see open questions).

**M6 — Polish and capture (≈4 days). Done.** Foundry HDRI, ambientCG floors, sparks/steam, ambience, a Cinemachine 3 camera path for the video, 60-fps lock, Windows build. Then hand over to the video playbook: voiceover first, capture against it.
Exit: the walkthrough video is captured from the project and approved. Reworded 2026-10-21 from "captured from the Windows build": the takes are Editor recordings through Unity Recorder, with the same scene and settings and no dropped frames. The Windows build is the live-demo deliverable. **Met 2026-10-21** (v02, 111 s; see `docs/voiceover.md`).

M4–M6 came to roughly 10 days of focused time, which fits the 2–3 weeks of unpaid build time originally budgeted for a sellable demo.

### Use case 2 — Virtual commissioning (Rev 4)

The beat that is the whole pitch: a soft PLC runs the cell from Structured Text. The commissioning test suite runs and one test fails; in 3D, parts pile up at the gate because the program is missing an interlock. One line of the program is fixed, the suite reruns and every test passes. "Found in the twin on day 1, not on site in commissioning week."

Soft PLC: OpenPLC Runtime v4 (MIT), outside Unity and outside the repo. Link: Modbus TCP, with our own client. realvirtual Professional is not needed, because Unity writes the Signals itself. CODESYS (2-hour demo mode), TwinCAT and PLCSIM Advanced (trial licences, and realvirtual Professional's interfaces) were ruled out. Commissioning savings figures (25–70%) are all vendor-reported; any on screen carry that label.

**M7 — Soft PLC link (≈2 days).** OpenPLC Runtime v4 running on the studio PC. `CellSequence` ported to Structured Text (`tools/plc/cell.st`), the same four states, handshake and hold. `PlcLink`: our own Modbus TCP client (a pure, tested frame codec plus a socket loop) with an I/O map ScriptableObject that maps coils and registers to bus topics; the PLC host and port live in `TwinConfig`. A logic-source switch hands the line to the PLC (the in-Unity `CellSequencer` stops writing its outputs), and a heartbeat watchdog holds the line if the PLC goes silent. New signals: `cell/plc/source` (0 Unity, 1 PLC), `cell/plc/heartbeat`, `cell/plc/link`.
Exit: the cell runs 10 minutes from the soft PLC with `CellSequencer` off. Parts per hour stay within 10% of the M2 rate, with 0 stalls. Stopping the PLC holds the line within 1 s.

**M8 — Commissioning suite (≈2.5 days).** Fault forcing extends to the cell: force the gate sensor on or off, drop a part, keep the arm busy. Six scenarios (normal run, stuck sensor, missing part, arm not ready, press fault, PLC loss) run against either logic source and write a pass/fail report, shown on a dashboard panel and saved as CSV. An I/O list is exported from the I/O map as CSV, because that is what commissioning engineers work from. A copy of the program with a seeded bug (`tools/plc/cell_buggy.st`, the missing interlock). New signals: `cell/gate1/force` (0 off, 1 forced false, 2 forced true), `cell/vc/test`, `cell/vc/result`.
Exit: the suite passes against both logic sources. The bugged program fails a named test, and the failure is visible in 3D.

**M9 — VC capture (≈1.5 days).** Demo-script beats for VC, voiceover, takes through `TakeSequencer`, cut with `tools/video`. Naming `2DS_Manufacturing_FactoryTwin_VC_v01_<date>`.
Exit: the clip is captured from the project and approved.

M7–M9 come to roughly 6 days of focused time.

## Later (not scheduled)

Planned in Rev 2 (commit `ee852e9` has the full exit conditions). Virtual commissioning moved into Rev 4 as M7–M9. Each builds on the same signal contract and can be picked up when a prospect asks for it:

- **Real-time monitoring, full version** — hover any machine to read its signal, health for every station, OEE, retained MQTT state for clients that join mid-run.
- **Workforce training & safety** — a protective zone that freezes the arm, and a guided lockout procedure for clearing a press fault (natural first Quest 3 content).
- **Factory layout & what-if** — runtime UR5e IK in C# so stations can move, layout variants, faster-than-real-time comparison. Commit the arm bake script first (blueprint gap 4).
- **Pre-planning** — a scenario runner that sweeps layouts and speeds against a production target, using M5's maintenance windows.
- **Views** — touch/kiosk view, Quest 3 view.

## Claude Code kickoff prompt (paste into the project)

> This is a Unity 6000.3 URP project for a factory digital-twin marketing demo focused on predictive maintenance. Third-party code: realvirtual.io Starter 6 (do not modify files under ThirdParty/ or Assets/realvirtual/), Unity Splines, URDF-Importer, MQTTnet. Read CLAUDE.md, docs/STATUS.md and docs/demo-script.md first. Write all new code under Assets/_Project. Conventions: one MonoBehaviour per responsibility, realvirtual Signal components are the only bridge between 3D and data, MQTT topic scheme `cell/<station>/<signal>`, no hard-coded broker addresses (read from TwinConfig). The current milestone is at the top of docs/STATUS.md.

## Licence rules for this repo (copy into LICENSES.md header)

1. Asset Store and Fab content ships only inside built applications; raw files are never handed to a client.
2. realvirtual is distributed compiled only; never share its source, prefabs or scenes.
3. Every CC BY item gets a credit line in the app's About panel and on the web page.
4. Vendor branding (UR, KUKA, ABB, FANUC) is removed from any model shown publicly until vendor terms are confirmed.
5. When a client project starts on this codebase: buy the Unity Industry seat, upgrade realvirtual to Professional if the client needs OPC UA/PLC, and re-run this list.

## Video handoff

The demo feeds one ~90-second walkthrough per the playbook:
- **Promise:** a twin that sees a failure coming.
- **Proof:** risk climbs, then two outcomes. Ignored, the line stops; acted on, it doesn't.
- **Path:** your machines' sensors over MQTT or OPC UA, your failure history, the same twin.
- **Ask:** book a call.

The VC clip (M9) follows the same shape:
- **Promise:** find the bug before the machine ships.
- **Proof:** the suite fails on a missing interlock, parts pile up in 3D; one line fixed, every test passes.
- **Path:** your PLC program, your I/O list, the same twin.
- **Ask:** book a call.

Voiceover rendered before capture. Naming `2DS_Manufacturing_FactoryTwin_PdM_v01_<date>`.
As built (2026-10-21): the takes were captured first and the voice (Kokoro TTS) was cut to them; the approved video is v02, 111 s, with a digital-twin introduction ahead of the promise.

## Open questions

- Failure-risk definition (M5): margins to the AI4I failure thresholds (explainable, and the dataset's failure rules are published) or a small classifier trained offline (reads as more "predictive")? Either ships without an ML package. Suggested: margins first, classifier only if a prospect asks for ML.
- How the video shows "ignored vs acted on": two takes cut together, or a split screen of two runs with a parts-lost counter on each? Decide before the voiceover.
- Arm choice for the public page: decided at M2, de-branded UR5e.
- VC (M7): ~~does OpenPLC Runtime v4 accept a plain `.st` upload?~~ Answered 2026-10-22: no; the OpenPLC Editor v4 (GPL-3.0, tool only) compiles and uploads the program. Fallback if the v4 Windows runtime does not hold up: Beremiz, as an external tool.
- VC (M9): ~~one VC clip, or a VC segment added to the PdM video?~~ Decided 2026-10-23: a separate clip of about 76 s; the approved 111 s PdM video stays as it is.
- Quest 3 view (later): pass-through tabletop twin or room-scale? Only decide after M6 ships.
