# Manufacturing Demo — Factory Digital Twin build plan (Rev 7, 4 Nov 2026; Rev 6 in progress; Rev 5 of 24 Oct 2026 complete)

Source tracker (62 items, licence verdicts, download status): kept privately; the shipped items are in `docs/LICENSES.md`.
Video SOP to follow once the build exists: `claude/walkthrough-video-playbook.md`

Rev 3 narrowed the prototype to one use case, **predictive maintenance**: the most-asked-for digital-twin use in manufacturing, and the one this build is closest to. Rev 2 planned six use cases (M4–M10, commit `ee852e9`). M0–M6 are done; the PdM video (v02) is approved.

Rev 4 adds a second use case, **virtual commissioning** (M7–M9), for machine builders and system integrators. Chosen after a short research pass on 2026-10-22: machine builders know virtual commissioning but rarely use it, customers increasingly ask them for a virtual factory acceptance test, and competitors' demos are either engineering-tool screen recordings or whole-factory showpieces. None tells the story of one logic bug caught before site. The other four use cases stay under "Later".

Rev 5 adds a third use case, **workforce training and safety** (M10–M13), the studio's first Quest 3 content. Chosen after a short research pass on 2026-10-24 (summary under Use case 3 below): lockout/tagout is OSHA's #4 most-cited standard in FY2025, and the VR safety-training vendors (Pixaera, Uptale, Interplay, Transfr, PIXO) sell generic modules. None found runs the training on the plant's own live twin, PLC and dashboard, which is what this one does.

Rev 6 (approved 2026-11-04) adds a fourth use case, **fire safety training** (M14–M18), in VR on the same cell, crew and rig. 29 CFR 1910.157(g) makes extinguisher training annual for any plant whose employees may use extinguishers, so it is a recurring need. Generic VR fire modules and digital extinguisher trainers exist; none found runs the drill on the plant's own live twin. Full detail (auditor's view, scenarios, objectives by clause, asset list, phases, capture): `docs/fire-safety-training.md`.

Rev 7 (approved 2026-11-04) adds a fifth use case, **electrical safety and arc flash** (M19–M23), to start after M18. It builds on the M11 lockout and Rev 6's drive cabinet. NFPA 70E-2027 (in effect since 6 May 2026) sets retraining at no more than three years and contact-release training every year, so this is another recurring need. VR arc flash modules exist and already cover boundaries, PPE and the live-dead-live test; none found runs on the plant's own twin, where the dashboard reads 0 V and the tester still finds the backfeed. Full detail: `docs/electrical-safety-training.md`.

## Where it stands (2 Nov 2026)

- **Done:** M0–M13, all of Rev 5. M9, M12 and M13 closed 2 Nov. All three use cases are built and on video as a three-part series: 1 predictive maintenance, 2 virtual commissioning, 3 training and safety in the Quest 3 over Link (`Builds/Recordings/Final Series/`, with YouTube, Shorts and LinkedIn cuts in `v06 kit/`).
- **Next:** Rev 6, fire safety training (M14–M18, about 14 days, below), approved 4 Nov; M14 is current; Rev 7, electrical safety (M19–M23, about 13.5 days, below), approved 4 Nov, starts after M18; the public-repo decisions in `docs/public-release-check.md`; the Later use cases when a prospect asks.

## What this is

A Unity prototype for the Manufacturing landing page on 2dudestudios.com and for showing to manufacturing prospects in person. It is 2 Dude Studios' own marketing asset, not client work, so it is built on the current Unity Personal/Pro licence. A Unity Industry seat gets bought the day the first manufacturing project lands — Unity counts the client's revenue, not ours, so any real factory will require it. Price the seat into the first quote.

Its job is to prove that the studio can build a digital twin of an automated line that **sees a machine failure coming, shows where it is and what it will cost the line, and says when to act**. Analytics vendors already sell failure prediction without 3D; the studio's angle is making it visible and tied to the line's output.

Decisions already made: factory digital twin, Unity, URP, Unity 6.3, predictive maintenance as the one use case. Unity Simulation Pro (early access since Sep 2026) needs a Unity Industry licence and targets robot and sensor validation; it is not used here.

## The demo in one paragraph

One production cell in a factory hall. Parts arrive on an infeed conveyor, pass a sensor gate, a six-axis arm picks each one and places it on an outfeed conveyor, parts exit. A press alongside reports machine telemetry (temperatures, speed, torque, tool wear) over MQTT. As the press's tool wears, the twin's failure risk climbs, its remaining tool life counts down, and it recommends a tool change while the line is still running. Ignore it, and the press fails: it turns red in 3D, the line holds and the dashboard counts the parts lost. Act on it, and the line never stops. That contrast is the pitch: the twin is wired to data, and it turns that data into a decision.

Desktop walkthrough first (the website video). A Quest 3 view over Link comes with the training use case (M12); standalone Quest and a touch/kiosk view stay later.

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
| XR (M12) | com.unity.xr.openxr 1.18.0, com.unity.xr.management 4.6.1, com.unity.xr.interaction.toolkit 3.6.1 | Unity Companion License | Approved 2026-10-24, added at M12 only. Quest 3 over Link / Air Link: the Windows build renders on the PC. XRI pulls in com.unity.ugui as a dependency; our UI stays UI Toolkit. Not the Meta XR Core SDK (Meta's own SDK licence). |
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

**M4 — Dashboard (≈3 days). Done 2026-10-20.** Only what the predictive-maintenance story needs. A UI Toolkit overlay with runtime binding:
- Press panel: health, live telemetry (air and process temperature, speed, torque, tool wear) as sparklines, and a failure-risk gauge (empty until M5 feeds it).
- Line panel: line state, parts out, parts/hour, and parts lost while the line is held.
- Event log, Inject/Clear fault buttons, broker link indicator, credits panel.

The press gets geometry and turns amber/red in 3D from its health signal. realvirtual's runtime toolbar is hidden. New signals: `cell/press/health` (0 ok, 1 warning, 2 fault), `cell/line/parts_per_hour`, `cell/line/parts_lost` (held time × the running rate).
Exit: the "inject fault" beat looks good on camera. The press goes red, the line holds, and the dashboard's press health, line state and parts lost move together.

**M5 — Prediction (≈3 days). Done 2026-10-20.** Today the injector jumps straight to a failure row, which shows detection, not prediction. M5 adds:
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

**M7 — Soft PLC link (≈2 days). Done 2026-10-23.** OpenPLC Runtime v4 running on the studio PC. `CellSequence` ported to Structured Text (`tools/plc/cell.st`), the same four states, handshake and hold. `PlcLink`: our own Modbus TCP client (a pure, tested frame codec plus a socket loop) with an I/O map ScriptableObject that maps coils and registers to bus topics; the PLC host and port live in `TwinConfig`. A logic-source switch hands the line to the PLC (the in-Unity `CellSequencer` stops writing its outputs), and a heartbeat watchdog holds the line if the PLC goes silent. New signals: `cell/plc/source` (0 Unity, 1 PLC), `cell/plc/heartbeat`, `cell/plc/link`.
Exit: the cell runs 10 minutes from the soft PLC with `CellSequencer` off. Parts per hour stay within 10% of the M2 rate, with 0 stalls. Stopping the PLC holds the line within 1 s.

**M8 — Commissioning suite (≈2.5 days). Done 2026-10-23.** Fault forcing extends to the cell: force the gate sensor on or off, drop a part, keep the arm busy. Six scenarios (normal run, stuck sensor, missing part, arm not ready, press fault, PLC loss) run against either logic source and write a pass/fail report, shown on a dashboard panel and saved as CSV. An I/O list is exported from the I/O map as CSV, because that is what commissioning engineers work from. A copy of the program with a seeded bug (`tools/plc/cell_buggy.st`, the missing interlock). New signals: `cell/gate1/force` (0 off, 1 forced false, 2 forced true), `cell/vc/test`, `cell/vc/result`.
Exit: the suite passes against both logic sources. The bugged program fails a named test, and the failure is visible in 3D.

**M9 — VC capture (≈1.5 days). Done 2026-11-02: Part 2 of the series.** Demo-script beats for VC, voiceover, takes through `TakeSequencer`, cut with `tools/video`. Naming `2DS_Manufacturing_FactoryTwin_VC_v01_<date>`.
Exit: the clip is captured from the project and approved.

M7–M9 come to roughly 6 days of focused time.

### Use case 3 — Workforce training & safety (Rev 5)

The beat that is the whole pitch: a trainee walks toward the running cell. In the warning zone the arm slows; in the protective zone it freezes mid-move and the line holds. Then the CNC faults, and the trainee clears it through the lockout steps. Starting the line with the lock still on is refused by the same soft PLC that runs production, and the twin logs every step and every error. "Train on the line you actually run."

Why it sells (sources labelled: [R] regulator, [P/A] practitioner/academic/analyst, [V] vendor):
- Lockout/tagout (1910.147) is OSHA's #4 most-cited standard in FY2025, 2,562 citations, up from #5; plastics makers and machine shops are cited most. [R]
- A 2024 *Safety Science* meta-analysis of 52 studies: VR beats conventional training on knowledge and retention, though only about a third measured long-term retention. A 2021 chemical-plant study found VR equal to video or slides for complex safety tasks. [P/A]
- Deloitte and the Manufacturing Institute: up to 1.9 M US manufacturing jobs could go unfilled by 2033, so onboarding speed matters. [P/A]
- Onboarding and ROI figures (Forrester for Meta: about 25% shorter onboarding; PwC/Talespin "4x faster") are vendor-reported and carry that label. [V]

Standards the demo follows, and how far:
- **Lockout/tagout, OSHA 29 CFR 1910.147.** Notify (c)(9); prepare (d)(1), shut down (d)(2), isolate (d)(3), apply lock and tag (d)(4), release stored energy (d)(5), verify isolation with a try-start (d)(6). Release: inspect (e)(1), people clear and notified (e)(2), remove the device (e)(3). The training record follows (c)(7): who, when, steps, errors.
- **Robot zone, ISO 10218-1/-2:2025** (which absorbs most of ISO/TS 15066), with ISO 13855 for the zone size. The cell is a guarded, non-collaborative application, so a warning zone slows the arm, the protective zone stops it (IEC 60204-1 category 2: controlled stop, drives held), and restarting needs a deliberate reset from outside the zone. A collaborative "monitored standstill" may resume on its own; that is not what this cell shows.
- On screen and in the voiceover it is a **simulated safety function, not safety-rated**, and a demo of training, not certified training.

Platform: Quest 3 over Link or Air Link first. The PC renders the existing Windows build, and the broker and the soft PLC stay on localhost. Standalone Quest does not fit the hall as it stands (316 draw calls plus 116 shadow casters, 1.49 M triangles in view, Deferred+ with SSAO and about 50 realtime point lights, against Meta's Quest 3 guide of 400–600 draw calls and 1.3–1.8 M triangles); it needs a forward, fully baked scene variant and an Android build, about 3–4 more days, and stays under "Later". Meta stopped selling Quest for Business in Feb 2026; business buyers now run consumer Quest 3 headsets under ArborXR or ManageXR.

**M10 — Safety zone (≈2.5 days). Done 2026-10-24.** A warning zone and a protective zone around the arm (sensor volumes, like a safety laser scanner), dressed with the Factory Training fence and cone props. The warning zone slows the arm; the protective zone freezes it where it is, on the tick the signal changes (not after the current pick, unlike the CNC-fault hold), and holds the line. Leaving the zone does not restart anything; a reset from outside the zone does. `cell.st` gets a safety input and the reset logic, mirrored in `CellSequence`. A desktop first-person trainee (Input System) walks the hall. Two commissioning scenarios check it: zone entry stops the arm and holds the line; leaving without a reset does not restart it. New signals: `cell/safety/zone_warning`, `cell/safety/arm_zone_occupied`, `cell/safety/protective_stop`, `cell/safety/reset` (in).
Exit: entering the zone stops the arm within one physics tick of the signal, and the dashboard shows why. Leaving the zone does not restart the line; the reset does. The suite passes against both logic sources.

**M11 — Lockout procedure (≈3 days). Done 2026-10-26; 14 steps since 2026-10-27** (preparation (d)(1) added, and the area check (e)(2)(i) and the notification (e)(2)(ii) split as the rule has them). The 1910.147 steps on the CNC as a pure, tested procedure model: notify, stop the line, isolate (the CNC's main isolator), apply lock and tag, verify with a try-start, change the tool (resets tool wear), clear and notify, remove the lock, restart. Interactable points on the CNC drive the steps through signals only. Out-of-order steps are flagged and counted. A step panel on the dashboard; each run writes a training record (CSV: trainee, start, steps with times, errors, result). The PLC refuses to start the line while the lock is on, and the refusal is logged. New signals: `cell/press/isolated`, `cell/press/lockout`, `cell/training/step`, `cell/training/errors`, `cell/training/result`.
Exit: the procedure runs end to end and the line restarts. Starting the line with the lock on is refused by the PLC and logged. A wrong-order run records its errors in the training record.
Decide at start: the trainee's name in the record (fixed demo name or typed in).

**M12 — Quest 3 view over Link (≈3 days). Done 2026-11-02.** The three approved XR packages. An XR rig beside the desktop trainee (one or the other is active), teleport and snap turn, ray and grab on the same lockout points, the dashboard's step panel as a world-space panel. Nothing new on the signal side: the headset drives the same signals. Desktop stays the default; a launch switch turns VR on.
Exit: both beats (zone stop, lockout) run in the Quest 3 over Link at 72 fps or better, with no new signals, and the training record matches a desktop run.
Decided 2026-10-26: world-space UI Toolkit with XRI.
Built (26–29 Oct; details in `docs/STATUS.md`): `-vr` or F11 switches to the XR rig with a lighter VR render pipeline (72 fps steady in the Windows build over Link); controller hands and tracked hands (com.unity.xr.hands, approved 27 Oct) with grip poses; hands-on props on the lockout points (isolator twist, padlock and tag into the hasp, air-valve twist, tool swap from a cart, poke buttons), modelled in Blender from `tools/blender/`; a tablet in the left hand with the step due and the options; a pressable training board; NPC operators and a maintenance colleague who react to the procedure; the walkway fence round the press. Checked in the headset on 29 Oct.
Left: the exit runs in the headset and the training-record comparison with a desktop run.

**M13 — Training capture (≈1.5 days). Done 2026-11-02: Part 3 of the series.** Demo-script beats for the training clip, voiceover, takes from the desktop trainee and from the headset (mirror view), cut with `tools/video`. Naming `2DS_Manufacturing_FactoryTwin_Safety_v01_<date>`.
Exit: the clip is captured from the project and approved.

M10–M13 come to roughly 10 days of focused time.

### Use case 4 — Fire safety training (Rev 6, approved 2026-11-04)

The beat that is the whole pitch: the dashboard sees the drive cabinet overheating before there is any smoke. Then the smoke comes, the trainee raises the alarm, the line holds, and the right extinguisher used with PASS puts the fire out. In a second run the fire grows past the incipient stage. The trainee backs out, finds the exit route blocked, takes the other one, and the roll call at the assembly point finds a colleague missing. The twin logs every step against its clause. "Practise the fire you hope never happens, on the line you actually run."

Requirements it trains to (detail and the objective-by-clause table in `docs/fire-safety-training.md`):
- **Fire prevention plan, 29 CFR 1910.39**: hazards, ignition sources, housekeeping, maintenance of heat-producing equipment ((c)(1)–(c)(5)), informing employees ((d), (e)).
- **Emergency action plan, 1910.38**: report, evacuate by route, shut down critical operations, account for everyone, alarm ((c)(1)–(c)(5), (d), (f)), with 1910.165 (alarm) and 1910.36–37 (exit routes).
- **Portable fire extinguishers, 1910.157**: the employer's policy ((b)(1)/(b)(2) evacuate only, or (g) employee use). An educational program on hire and at least annually ((g)(1)–(g)(2)), and equipment training for designated employees ((g)(3)–(g)(4)). VR covers the education. A physical discharge for designated employees is still recorded separately (OSHA interpretation, 2 Jul 1991). Incipient stage per 1910.155(c)(26).
- **NFPA 10** (classes, siting, monthly inspection), **NFPA 101** (egress, exit marking, drills), **NFPA 72** (temporal-3 alarm), **NFPA 80** (fire doors). UK RRO 2005 / BS 5306 / ISO 7010 only if a prospect asks.
- On screen and in the voiceover it is a **training demo, not certified training**, and the auditor content is guidance, not legal advice.

Scenarios in this cell: F1 electrical cabinet (C), F2 CNC coolant-mist fire (B), F3 oily waste or cardboard (A), F4 fire past incipient (flee), F5 blocked exit route, F6 lithium-ion charger (flee only), plus a pre-shift hazard hunt. Policy modes: evacuate only, all employees (the demo default), designated employees. Trainee phases: 1 awareness and prevention, 2 extinguisher use, 3 alarm and evacuation, 4 unguided assessment (the annual refresher).

Reuses: the M12 XR rig and hands, the training board and tablet, the NPC crew (one becomes the fire warden), the CSV training-record writer and folder (`Builds/Training/`, same header as the lockout record), Particle Pack flames and smoke, and realvirtual's control cabinet and assembly-point decal. New signals under the existing scheme: `cell/fire/*` and `cell/cabinet/temp_c`. The line holds on `cell/fire/alarm`, like a CNC fault. The fire drills run on the in-Unity sequencer and need no PLC work. Assets to make (extinguishers with interactive pin, gauge and tag; pull station; horn and strobe; detectors; exit signs; hazard props) are listed in the doc. Third-party audio and textures are imported only after approval, each with its LICENSES.md row.

**M14 — Fire model and effects (≈3 days).** A pure, tested `FireModel` (stage growth; suppression by agent, class, aim and stand-off; reflash), with parameters in a ScriptableObject. Scenario assets F1–F6. Effects driven from signals. Smoke and heat detectors. The alarm holds the line, and the dashboard gets a fire panel. New signals: `cell/fire/scenario` (in), `stage`, `size`, `class`, `location`, `smoke_detected`, `alarm` (in), `cell/cabinet/temp_c`.
Exit: each scenario ignites and grows through its stages on schedule. A detector raises the alarm and the line holds; the dashboard shows stage and location. The largest fire with smoke holds 72 fps in the Quest 3 over Link.
Decided 2026-11-04: all employees by default, US + ISO 7010 signs, F6 kept, assembly point in the yard past the loading gate.

**M15 — Extinguisher and hazard hunt (≈3.5 days).** ABC, CO2 and water extinguishers, modelled in Blender and rigged. A pre-use check (gauge, pin, seal, tag). PASS by hand in VR (XR Hands and controllers) and by mouse on desktop. Hazard spots for the hunt. New signals: `cell/fire/agent`, `discharge`, `charge`.
Exit: the check catches a faulty unit. The right class used with PASS puts out an incipient fire within one charge. The wrong agent makes the fire worse and is logged as critical. Stopping early leads to reflash. Each hazard found is logged with its clause, on desktop and in the headset.

**M16 — Alarm and evacuation (≈3 days).** Pull station, temporal-3 horn (synthesised) and strobe, exit signs and emergency lighting, two routes and a blocker, a smoke layer with a stay-low check, NPC evacuation on the NavMesh, and a warden with a roll call at the assembly point. New signals: `cell/fire/evacuated`, `missing`.
Exit: the alarm sounds and flashes, and the NPCs reach the assembly point. The blocked route forces the alternate one. The roll call reports one missing. Re-entry is logged as critical.

**M17 — Assessment and record (≈2.5 days).** A pure, tested `FireDrillProcedure` for phases 1–4, with the policy modes, scoring by clause, board pages for phase select and debrief, and the CSV record with metric rows and a hands-on discharge field.
Exit: a fight session and a flee session run end to end unguided, in the headset and on desktop. Each writes a record by clause, and the headset and desktop records match. A seeded wrong-order run records its mistakes. Switching the policy mode changes the required steps.

**M18 — Fire capture (≈2 days).** Part 4 of the series. Complete sessions recorded as single takes through `TakeSequencer`, with beat markers from `TakeEventLog` and game audio from `TakeAudioRecorder`. Takes from the headset (mirror view) and the desktop, plus dashboard shots. Voiceover in `docs/voiceover_fire.md`, cut with `tools/video/fire/`. Naming `2DS_Manufacturing_FactoryTwin_Fire_v01_<date>`.
Exit: the clip is captured from the project and approved.

M14–M18 come to roughly 14 days of focused time.

### Use case 5 — Electrical safety and arc flash (Rev 7, approved 2026-11-04)

The beat that is the whole pitch: the infeed drive faults and the dashboard sends the trainee to the drive cabinet. The arc flash label sets the PPE and the boundaries. The trainee opens and locks the disconnect, and the dashboard reads 0 V. They prove the tester and test anyway, and find 120 V backfed from the control circuit's UPS feed. They lock the second source, test again and prove the tester again before touching anything. On restore, the PLC refuses to start with the lock on. "The twin tells you where to look. Only your tester tells you it's dead."

Requirements it trains to (detail and the objective-by-clause table in `docs/electrical-safety-training.md`):
- **Training, 29 CFR 1910.332**: unqualified ((b)(2)) and qualified ((b)(3)) persons.
- **Work practices, 1910.333**: de-energise first ((a)(1)). Electrical lockout ((b)(2)): a 1910.147 procedure like M11's also satisfies it only with two added steps, release of stored electric energy ((b)(2)(iii)(D)) and a qualified person's test for absence of voltage, including backfeed ((b)(2)(iv)(B)). Energised work by qualified persons only ((c)(2)).
- **PPE and alerting, 1910.335**, with rubber insulating gloves under **1910.137**. Working space, openings and cords (1910.303(g), 1910.305, 1910.334) feed the hazard hunt.
- **NFPA 70E-2027**: training and retraining (110.4), the electrically safe work condition (Article 120), the energised work permit and the new second-person rule (130.2), approach boundaries (130.4), arc flash assessment and label (130.5), PPE (130.7). Section numbers are checked against the 2027 text at M19 start.
- On screen and in the voiceover it is a **training demo, not certified training**; the label values come from a made-up study; and hands-on tester practice is still required. The auditor content is guidance, not legal advice.

Scenarios in this cell: E1 replace a failed VFD (the main task), E2 backfeed from the UPS feed, E3 charged DC bus, E4 dead tester caught by the proving unit, E5 unqualified colleague at the boundary, E6 contact release (training only unless decided otherwise), plus an electrical hazard hunt. Role modes: unqualified, qualified (the demo default), qualified with energised diagnostics. Trainee phases: 1 awareness, 2 gear and tester, 3 electrically safe work condition, 4 unguided assessment (the retraining run).

Reuses: the M11 lock, tag and release steps; Rev 6's hazard hunt, scorer, badges, HUD, two-handed grab and alarm panel; the M10 zones for the boundaries; the CSV record writer and header; the start menu (a fifth card). New signals under the existing scheme: more `cell/cabinet/*` (door, disconnect, locks, bus, DC bus, control voltage), `cell/infeed/drive_fault` and `cell/elec/*` (scenario, boundary, PPE, tester). Tester readings stay off the bus, so the dashboard never shows the trainee the answer. The cabinet lock goes into both logic sources, like M11's. Assets to make: our own drive cabinet with a door, disconnect and VFDs (realvirtual's box has no interior), tester and proving unit, gloves and arc-rated PPE, labels and boundary decals, rescue hook and AED. Third-party sound clips only after approval, each with its LICENSES.md row.

**M19 — Electrical model and cabinet (≈3 days).** A pure, tested `ElectricalModel` (sources, disconnect, locks, AC legs, DC bus decay, readings between named points, tester health), an `ArcFlashStudy` asset and the label made from it, our own cabinet in place of the scaled box (F1 still works), boundary zones, new signals and a dashboard electrical panel.
Exit: opening the disconnect drops the AC legs to 0 except the E2 control circuit; the DC bus decays on schedule; every probe pair reads right in tests; boundary entry is detected; the dashboard shows bus, DC bus and locks; 72 fps over Link with the cabinet open.
Decided 2026-11-04: qualified by default, our own cabinet, E6 in training only, US 480 V with demo label values, the cabinet lock into the soft PLC. At start: check the 70E-2027 section numbers.

**M20 — Tester, PPE and gloves (≈3 days).** Two-handed tester and probes in VR, mouse on desktop; proving unit and a flat-battery tester; PPE board and wearing PPE; glove inspection and air test.
Exit: live and dead buses read right; the proving unit catches the flat tester; the wrong PPE category is logged against the label; a faulty glove is caught, on desktop and in the headset.

**M21 — Safe work procedure and hunt (≈3 days).** A pure, tested `ElectricalSafeWorkProcedure` (Article 120 plus 1910.333's two steps, M11's release steps reused), scenarios E1–E5, the electrical hazard hunt, and the cabinet lock into the soft PLC and the sequencer.
Exit: E1 runs end to end; E2's backfeed is found only by testing and cleared by locking the UPS feed; E3 forces the wait; E4 voids the test; E5's colleague is stopped at the limited approach boundary; the PLC refuses to start with the lock on; each hunt find is logged with its clause.

**M22 — Assessment, rescue and record (≈2.5 days).** Phase 4, the role modes, scoring by clause, board pages, the CSV record with a hands-on field, the menu card, and E6 if kept.
Exit: a qualified session and an unqualified session run end to end unguided, in the headset and on desktop; the records match by clause; a seeded wrong-order run records its mistakes; switching the role mode changes the required steps.

**M23 — Electrical capture (≈2 days).** Part 5 of the series, recorded as in M18. Voiceover in `docs/voiceover_electrical.md`, cut with `tools/video/electrical/`. Naming `2DS_Manufacturing_FactoryTwin_Electrical_v01_<date>`.
Exit: the clip is captured from the project and approved.

M19–M23 come to roughly 13.5 days of focused time.

## Later (not scheduled)

Planned in Rev 2 (commit `ee852e9` has the full exit conditions). Virtual commissioning moved into Rev 4 as M7–M9, workforce training and safety into Rev 5 as M10–M13. Each builds on the same signal contract and can be picked up when a prospect asks for it:

- **Real-time monitoring, full version** — hover any machine to read its signal, health for every station, OEE, retained MQTT state for clients that join mid-run.
- **Factory layout & what-if** — runtime UR5e IK in C# so stations can move, layout variants, faster-than-real-time comparison. Commit the arm bake script first (blueprint gap 4).
- **Pre-planning** — a scenario runner that sweeps layouts and speeds against a production target, using M5's maintenance windows.
- **Views** — touch/kiosk view, standalone Quest 3 (a forward, fully baked scene variant and an Android build; ≈3–4 days).
- **Fire safety extensions** (after Rev 6): hot work permit and fire watch (1910.252, NFPA 51B), combustible dust (NFPA 652), Class D metal fires, and a UK variant (RRO 2005, BS 5306, ISO 7010 signage).
- **Electrical safety extensions** (after Rev 7): batteries, PV and DC systems (NFPA 70E-2027 Chapter 3), work above 1,000 V, and a UK/EU variant (Electricity at Work Regulations 1989, HSE GS38; EN 50110-1).

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

The training clip (M13):
- **Promise:** train on the line you actually run.
- **Proof:** step into the zone and the arm freezes; clear a CNC fault with the lockout steps, and the line refuses to start while the lock is on.
- **Path:** your cell's twin, your PLC, your procedure; Quest 3 or desktop.
- **Ask:** book a call.

The fire safety clip (M18):
- **Promise:** practise the fire you hope never happens, on the line you actually run.
- **Proof:** the twin flags the overheating cabinet before the smoke; alarm, line held, PASS puts it out. A second run grows past incipient, the route is blocked, and the roll call finds one missing.
- **Path:** your cell's twin, your emergency action plan and fire prevention plan; Quest 3 or desktop.
- **Ask:** book a call.

The electrical safety clip (M23):
- **Promise:** the twin tells you where to look. Only your tester tells you it's dead.
- **Proof:** the drive faults; label, PPE, disconnect locked; the dashboard reads 0 V, and the tester finds the backfeed anyway. Second source locked, tested again, and the PLC refuses to start with the lock on.
- **Path:** your cell's twin, your one-line drawings and arc flash study, your PLC; Quest 3 or desktop.
- **Ask:** book a call.

Voiceover rendered before capture. Naming `2DS_Manufacturing_FactoryTwin_PdM_v01_<date>`.
As built (2026-10-21): the takes were captured first and the voice (Kokoro TTS) was cut to them; the approved video is v02, 111 s, with a digital-twin introduction ahead of the promise.

## Open questions

- Failure-risk definition (M5): margins to the AI4I failure thresholds (explainable, and the dataset's failure rules are published) or a small classifier trained offline (reads as more "predictive")? Either ships without an ML package. Suggested: margins first, classifier only if a prospect asks for ML.
- How the video shows "ignored vs acted on": two takes cut together, or a split screen of two runs with a parts-lost counter on each? Decide before the voiceover.
- Arm choice for the public page: decided at M2, de-branded UR5e.
- VC (M7): ~~does OpenPLC Runtime v4 accept a plain `.st` upload?~~ Answered 2026-10-22: no; the OpenPLC Editor v4 (GPL-3.0, tool only) compiles and uploads the program. Fallback if the v4 Windows runtime does not hold up: Beremiz, as an external tool.
- VC (M9): ~~one VC clip, or a VC segment added to the PdM video?~~ Decided 2026-10-23: a separate clip of about 76 s; the approved 111 s PdM video stays as it is.
- Quest 3 view: ~~pass-through tabletop twin or room-scale?~~ Decided 2026-10-24: room-scale in the hall, over Link first (M12); standalone later.
- Fire safety (Rev 6): ~~policy mode, signage, F6, assembly point~~ Decided 2026-11-04: all employees; US + ISO 7010 signs; F6 kept; assembly point in the yard past the loading gate.
- Electrical safety (Rev 7): ~~approval, role mode, cabinet, E6, voltage, PLC lock input~~ Decided 2026-11-04: approved; qualified by default; our own cabinet; E6 in training only; US 480 V with demo label values; the cabinet lock into the soft PLC.
