# Demo script — 90-second walkthrough beats (predictive maintenance)

Structure follows the walkthrough video playbook: promise / proof / path / ask. Voiceover is rendered before capture; these beats are what the camera must show under each line.

The machine on screen is a **CNC machining centre**, because the AI4I 2020 telemetry models a milling process (spindle speed, torque, tool wear). The signal names keep their station name `press`; only the words on screen and in the voiceover say CNC.

Signal names are the real topics from `docs/BLUEPRINT.md` section 3, without the `cell/` prefix. A name marked *(M5)* does not exist yet and is added in that milestone.

| # | Time | Beat | On screen | Signals in play |
|---|---|---|---|---|
| 1 | 0–8 s | **Promise.** "This machining centre is going to fail. Its digital twin knows when." | Cinemachine dolly from hall wide to the cell. Dashboard slides in, all green; the CNC's stack light is green. | `line/state = 0`, `press/health = 0` |
| 2 | 8–20 s | Every sensor is live data. | Parts flow: gate, arm, outfeed. Cut to the CNC panel: temperatures, spindle speed and torque sparklines moving with the replay. | `press/air_temp_k`, `press/process_temp_k`, `press/speed_rpm`, `press/torque_nm`, `line/parts_per_hour` |
| 3 | 20–38 s | **The twin sees it coming.** "The tool is wearing. Risk is climbing." | Tool wear and torque rise; the risk gauge climbs and crosses warning. The stack light turns amber, remaining tool life counts down, the event log says "Tool change due". The line is still running. | `press/tool_wear_min`, `press/failure_risk` *(M5)*, `press/tool_rul_min` *(M5)*, `press/maint_due` *(M5)* |
| 4 | 38–55 s | **Proof 1 — ignore it.** "Ignore it, and the line stops." | The failure arrives: the stack light flashes red and the CNC glows red, the arm finishes its pick, both conveyors stop, the dashboard reads "Stopped — CNC fault". The parts-lost counter climbs. | `press/fault_twf`, `press/machine_failure`, `press/health = 2`, `line/state = 3`, `line/parts_lost` |
| 5 | 55–70 s | **Proof 2 — act on it.** "Change the tool when the twin says, and it never does." | Same cell, second run: tool change at the recommendation. Tool wear back to zero, risk back to green, parts keep flowing, parts lost stays at 0. | `press/tool_change` *(M5)*, `press/tool_wear_min`, `press/failure_risk` *(M5)*, `line/parts_lost` |
| 6 | 70–84 s | **Path.** "Your machines: their sensors over MQTT or OPC UA, your failure history, the same twin." | Three quick cuts: the live topic list on the broker, the CNC panel close-up, the dashboard on a touch panel (or a still if the kiosk view is not built). | |
| 7 | 84–90 s | **Ask.** Book a call. 2dudestudios.com/manufacturing. | End card, logo, URL. | |

Capture rules: 1920×1080 @ 60 fps, build frame-rate locked, OBS game capture, no cursor except in beats 4–5 (Inject, Tool change). Take keys: **1** Wear tool out, **2** Change tool, **3** Clear, **0** fresh take (log and parts lost reset, line keeps running), **F9** FPS readout, **F10** cursor. Camera keys: **4** hall dolly (8 s), **5** cell, **6** line close, **7** CNC with stack light, **8** CNC control panel, **9** operator camera.

Suggested shots per beat: 1 → **4** (the dolly lands on the cell shot). 2 → **5**, cut to **6** for the gate and arm. 3 → **7** while the risk climbs and the stack light turns amber. 4 → **7** for the red, then **5** to show both conveyors stop. 5 → **5**. 6 → **8** for the panel close-up, then **5**. 7 → end card in the edit. Let the line run at least 2.5 minutes before beat 4, so parts/hour has settled (about 510) and parts lost has a running rate to count against. How beats 4 and 5 are shown (two takes cut together or a split screen of two runs) is an open question in `docs/build-plan.md`.

Fault types available in the injector (map to AI4I failure modes): TWF tool wear, HDF heat dissipation, PWF power, OSF overstrain, RNF random. The video uses tool wear (TWF), because it builds up gradually and a tool change fixes it, so it is the failure a twin can most convincingly predict.
