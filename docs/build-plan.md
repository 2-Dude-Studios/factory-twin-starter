# Manufacturing Demo — Factory Digital Twin build plan (Rev 1, 22 Sep 2026)

Source tracker (62 items, licence verdicts, download status): https://claude.ai/artifact/AGRYueGCfv6UwgTbnQcdre
Video SOP to follow once the build exists: `claude/walkthrough-video-playbook.md`

## What this is

A Unity demo for the Manufacturing landing page on 2dudestudios.com and for showing to manufacturing prospects in person. It is 2 Dude Studios' own marketing asset, not client work, so it is built on the current Unity Personal/Pro licence. A Unity Industry seat gets bought the day the first manufacturing project lands — Unity counts the client's revenue, not ours, so any real factory will require it. Price the seat into the first quote.

Decisions already made: factory digital twin (not VR training), Unity, URP, Unity 6.3.

## The demo in one paragraph

One production cell in a factory hall. Parts arrive on an infeed conveyor, pass a sensor gate, a six-axis arm picks each one and places it on an outfeed conveyor, parts exit. Every drive, sensor and the arm publish state to a live dashboard (OEE, cycle time, machine health, a failure-risk gauge) fed by MQTT. A "what-if" button injects a fault — a jammed sensor, a tool-wear alarm — and the dashboard and the 3D cell react together. That single moment is the pitch: the twin is not a render, it is wired to data.

Three views of the same scene: desktop walkthrough (the website video), touch/kiosk overview, and a Quest 3 pass-through/VR view for the arena (phase 3, only if time allows).

## The stack (all free, all verified 22 Sep 2026 — see tracker for links)

| Layer | Choice | Licence | Notes |
|---|---|---|---|
| Base scene | Factory Training \| Industry Sample (Unity, Asset Store) | Standard Asset Store EULA | Free, 2.8 GB, URP, needs Unity 6000.3.0. No Industry seat needed. |
| Twin logic | realvirtual.io Digital Twin Starter 6 | Asset Store EULA + realvirtual EULA | Free, commercial use permitted (single-user), distribute compiled only. Includes drives, sensors, transport/conveyors, signal model, S7 TCP/IP. **No** OPC UA/MQTT, **no** robot IK, **no** CAD import in Starter. |
| Paths | com.unity.splines 2.8.4 | Unity Companion | Conveyor routes, AGV routes. |
| Robot model | ros-industrial/universal_robot → UR5e or UR10e | Apache-2.0 + BSD-3 | Do **not** use UR8LONG/15/18/20/30 meshes (UR's own terms). Keep branding off the model for the public page. |
| Robot import | Unity-Technologies/URDF-Importer v0.5.2 | Apache-2.0 | Unity 2020.2+, no Unity 6 statement; expect small fixes. Install via Package Manager git URL. |
| Robot motion | Fast IK (Asset Store) or keyframed joint angles | Asset Store EULA | Starter has no IK. For a fixed pick-and-place loop, keyframing joint angles is the more reliable path. |
| Live data | M2MqttUnity (fast path) or MQTTnet 4.x netstandard build | MIT (+EPL-1.0 lib) / MIT | Local Mosquitto broker on the studio PC. Map topics → realvirtual Signal components. |
| Telemetry stand-in | AI4I 2020 Predictive Maintenance Dataset | CC BY 4.0 | 10,000 rows: air/process temp, rpm, torque, tool wear, five failure modes. Replay it through the broker. Cite: UCI ML Repository, doi:10.24432/C5HS5C. |
| OPC UA (phase 2) | OPCFoundation/UA-.NETStandard | MIT | Now targets .NET Standard 2.1. Only when a prospect asks. |
| Lighting | Poly Haven — Industrial Workshop Foundry HDRI | CC0 | |
| Materials | ambientCG metals / concrete | CC0 | Raw files may live in the repo. |
| VFX | Unity Particle Pack 3.1 | Asset Store EULA | URP, sparks/steam. |
| Audio | Sonniss GDC bundles, Pixabay CC0 | Sonniss GDC / Pixabay | No standalone redistribution. |

Do not use: Industry Fundamentals template (Industry seat/trial only, no post-trial grant), AWSIM assets (CC BY-NC), realvirtual-WEB (AGPL) inside the build, the MARS-era `template-training` repo, any unlicensed snippet repo.

## Repo layout

```
factory-twin/
  Assets/
    _Project/            # everything we write
      Scenes/Cell.unity
      Scripts/Twin/      # signal bus, MQTT bridge, dataset replayer, fault injector
      Scripts/Robot/     # joint controller, pick-place sequencer
      Scripts/UI/        # dashboard (UI Toolkit, runtime data binding)
      Data/ai4i2020.csv
      Prefabs/
    ThirdParty/          # Asset Store imports, untouched
  Packages/manifest.json # splines, urdf-importer (git url), input system, xr (phase 3)
  tools/
    broker/              # mosquitto.conf, docker-compose
    replay/              # python: csv → MQTT at N rows/sec with fault flags
  docs/
    LICENSES.md          # one line per third-party item, copied from tracker
    demo-script.md       # the 90-second walkthrough beats
```

Git: LFS for anything over 10 MB. The Factory Training sample and realvirtual go through Asset Store import, not git — record package versions in `docs/LICENSES.md`.

## Milestones

**M0 — Setup (day 1).** Unity 6000.3 project, URP, Factory Training sample imported, realvirtual Starter imported and its demo scene running. Mosquitto up. `LICENSES.md` started. Exit: realvirtual demo conveyor moves in our project.

**M1 — Cell (days 2–4).** Carve one cell out of the Factory Training hall. Infeed conveyor → sensor gate → pick station → outfeed conveyor, built from realvirtual transport/drive/sensor components on Unity splines. Parts spawn, travel, get detected, exit. Exit: a loop that runs for ten minutes without stalling.

**M2 — Arm (days 5–7).** UR5e imported via URDF-Importer, branding removed, ArticulationBody joints. Keyframed or Fast-IK pick-and-place synced to the sensor gate. Exit: arm picks every part the sensor reports; no misses in a 5-minute run.

**M3 — Data spine (days 8–10).** Signal bus: every drive, sensor and arm joint writes to a topic. Python replayer streams AI4I rows as machine telemetry. Fault injector flips failure flags on demand (tool wear, heat dissipation, power, overstrain). Exit: `mosquitto_sub -t 'cell/#'` shows the whole cell live.

**M4 — Dashboard (days 11–13).** UI Toolkit overlay with runtime binding: OEE, cycle time, parts/hour, per-machine health, failure-risk gauge, event log. Fault injection lights the right machine red in 3D and in the panel simultaneously. Exit: the "inject fault" beat looks good on camera.

**M5 — Polish and capture (days 14–16).** Foundry HDRI, ambientCG floors, sparks/steam, ambience, cinemachine camera path for the video, 60-fps lock. Windows build. Then hand over to the video playbook: voiceover first, capture against it.

Roughly 3 weeks of focused time, which is within the 2–3 weeks of unpaid build time already budgeted for a sellable demo.

## Claude Code kickoff prompt (paste into the project)

> This is a Unity 6000.3 URP project for a factory digital-twin marketing demo. Third-party packages: realvirtual.io Starter 6 (do not modify files under ThirdParty/), Unity Splines, URDF-Importer, M2MqttUnity. Read docs/demo-script.md and docs/LICENSES.md first. Write all new code under Assets/_Project. Conventions: one MonoBehaviour per responsibility, realvirtual Signal components are the only bridge between 3D and data, MQTT topic scheme `cell/<station>/<signal>`, no hard-coded broker addresses (read from a ScriptableObject config). Current milestone: M1 — build the infeed→sensor→pick→outfeed loop.

## Licence rules for this repo (copy into LICENSES.md header)

1. Asset Store and Fab content ships only inside built applications; raw files are never handed to a client.
2. realvirtual is distributed compiled only; never share its source, prefabs or scenes.
3. Every CC BY item gets a credit line in the app's About panel and on the web page.
4. Vendor branding (UR, KUKA, ABB, FANUC) is removed from any model shown publicly until vendor terms are confirmed.
5. When a client project starts on this codebase: buy the Unity Industry seat, upgrade realvirtual to Professional if the client needs OPC UA/PLC, and re-run this list.

## Video handoff

The demo feeds a ~90-second walkthrough per the playbook: promise (a factory you can see and query), proof (fault injection beat, dashboard reacting), path (how a client's own line gets modelled — CAD in, signals mapped, deployed on desktop/kiosk/Quest), ask (book a call). Voiceover rendered before capture. Naming `2DS_Manufacturing_FactoryTwin_v01_<date>`.

## Open questions

- Arm choice for the public page: de-branded UR5e or a fully generic arm modelled in-house? Decide at M2.
- Quest 3 view (phase 3): pass-through tabletop twin or room-scale? Only decide after M5 ships.
- Whether to show the S7 interface at all — no Siemens PLC in the studio; a PLCSIM Advanced trial could make it real for one screenshot.
