# Demo script — hub video and six use-case clips

Structure follows the walkthrough video playbook: promise / proof / path / ask. Voiceover is rendered before capture; these beats are what the camera must show under each line.

There are seven pieces: one ~90-second hub video for the top of the Manufacturing page, and six short clips, one per use-case section. They are listed below in lifecycle order (plan → commission → run → people), which is the order the hub video and the page tell the story in. The **Built in** line says which milestone in `docs/build-plan.md` makes each clip capturable. Each milestone ends with a rough capture of its clip; final capture is M10.

Signal names are the real topics from `docs/BLUEPRINT.md` section 3, without the `cell/` prefix. A name marked *(Mx)* does not exist yet and is added in that milestone.

---

## Hub video — "A factory you can plan, test, run and train on" (~90 s)

| # | Time | Beat | On screen | Signals in play |
|---|---|---|---|---|
| 1 | 0–8 s | **Promise.** "This is a production cell you can plan, test, run and train on — before it exists." | Cinemachine dolly from hall wide to the cell. Dashboard slides in, all green. | `line/state = 0`, `link/connected` |
| 2 | 8–20 s | Everything is live data. | Part hits the gate, arm picks and places. Hover the infeed: its topic and live value appear. KPIs tick. | `gate1/part_present`, `arm/j1_deg`…`j6_deg`, `line/parts_out`, `line/parts_per_hour` *(M4)* |
| 3 | 20–40 s | **Proof — the twin sees a failure coming.** "The press is wearing out." | Failure-risk gauge climbs; maintenance recommendation in the event log. Left alone, the press turns amber then red; the arm finishes its pick and both conveyors stop. | `press/failure_risk` *(M5)*, `press/tool_wear_min`, `press/fault_twf`, `line/state = 3` |
| 4 | 40–47 s | **Plan.** "Plan it before you build it." | Planning tab: two layouts side by side, the one that meets the target turns green. | `sim/layout` *(M8)* |
| 5 | 47–54 s | **Commission.** "Test the control logic before the machine exists." | PLC code beside the twin; commissioning report all green, then one red line for a seeded bug. | `plc/source` *(M7)* |
| 6 | 54–61 s | **Run.** "Watch it and maintain it while it runs." | Dashboard close-up, health tiles, remaining tool life counting down. | `press/tool_rul_min` *(M5)*, `<station>/health` *(M4)* |
| 7 | 61–70 s | **People.** "Train the people who work beside it." | An operator steps into the arm's zone; the arm freezes; the lockout step list on screen. | `safety/protective_stop` *(M6)*, `training/step` *(M6)* |
| 8 | 70–84 s | **Path.** "Your line: CAD in, signals mapped, deployed on desktop, kiosk or headset." | Three quick cuts: the cell on a touch panel view, on Quest pass-through (or a still if that phase is not built), and the topic catalogue / I/O list. | |
| 9 | 84–90 s | **Ask.** Book a call. 2dudestudios.com/manufacturing. | End card, logo, URL. | |

---

## Clip 1 — Pre-planning (~30 s)

**Built in:** M9 (uses M5 and M8).

| # | Beat | On screen | Signals in play |
|---|---|---|---|
| 1 | "Will this cell hit its target?" | Planning tab with a target of N parts per shift. Pick N so the current layout just misses it; the sweep then has something to find. | |
| 2 | "The twin tries the options for you." | Scenario runner sweeps layouts, belt speeds, arm speed and source rate at the speed-up; rows fill in; the bottleneck column points at the arm. | `sim/time_scale` *(M8)*, `sim/layout` *(M8)* |
| 3 | "Including the maintenance it will need." | Planned tool changes (from remaining tool life) show as downtime in each row. Configurations that meet the target turn green. | `press/tool_rul_min` *(M5)* |
| 4 | "And the prediction holds." | One green configuration re-run in real time; actual vs predicted parts per hour, within 5%. | `line/parts_per_hour` *(M4)* |

## Clip 2 — Factory layout & what-if simulation (~30 s)

**Built in:** M8.

| # | Beat | On screen | Signals in play |
|---|---|---|---|
| 1 | "Move a station. The robot re-plans." | Top-down camera; the outfeed is dragged 0.3 m on the grid; the arm's reach envelope is drawn; the arm re-solves and places the next part at the new spot. | `arm/j1_deg`…`j6_deg` |
| 2 | "It won't let you build what can't work." | Outfeed dragged out of reach: the envelope goes red and the move is refused. | |
| 3 | "Run an hour in minutes. Compare." | Time-scale badge at the measured speed-up; layouts A and B side by side with throughput, cycle time and utilisation. | `sim/time_scale` *(M8)*, `sim/layout` *(M8)*, `line/cycle_time_s` |

## Clip 3 — Virtual commissioning (~40 s)

**Built in:** M7.

| # | Beat | On screen | Signals in play |
|---|---|---|---|
| 1 | "Test the control logic before the machine exists." | Split: Structured Text in the soft-PLC editor on the left, the twin on the right. Logic source switched to external. | `plc/source = 1` *(M7)*, `plc/heartbeat` *(M7)* |
| 2 | "The real program drives the twin." | The line of PLC code that starts the infeed highlights as the belt starts. | `infeed/drive_fwd`, `infeed/running` |
| 3 | "Every scenario, scripted." | Commissioning suite runs: normal run, stuck sensor, missing part, arm not ready, press fault, protective stop. Report fills in, all green. | `gate1/force` *(M7)* |
| 4 | "So bugs show up here, not on the shop floor." | A seeded bug (pick commanded without waiting for `arm/busy` to drop). Rerun: one red line, and the twin shows the stall. | `arm/pick_cmd`, `arm/busy`, `line/stalled` |
| 5 | Close. | I/O list exported from the topic catalogue, one flash. | |

## Clip 4 — Real-time monitoring & visibility (~30 s)

**Built in:** M4.

| # | Beat | On screen | Signals in play |
|---|---|---|---|
| 1 | "Every machine in this cell is live data." | Wide shot, dashboard all green, KPIs ticking with the loop. | `line/parts_out`, `line/cycle_time_s`, `line/parts_per_hour` *(M4)*, `line/oee` *(M4)* |
| 2 | "Point at anything to read it." | Hover the infeed: `infeed/speed_mm_s`. Hover the arm: six joint angles. Hover the gate: its lamp and `part_present` flip together. | `infeed/speed_mm_s`, `arm/j1_deg`…`j6_deg`, `gate1/part_present` |
| 3 | "Same data, any system." | Cut to a second MQTT client (terminal or phone) started mid-run, showing the same values. | all |
| 4 | "When something fails, you see where." | **Inject fault → Tool wear.** Press amber then red in 3D, press health tile red, event log line, line holds. **Clear**: back to green, parts/hour recovers on the sparkline. | `press/inject_fault`, `press/fault_twf`, `press/health` *(M4)*, `line/state` |

## Clip 5 — Predictive maintenance (~35 s)

**Built in:** M5. Beat 4 needs M6's tool change; until M6, end the clip on beat 3.

| # | Beat | On screen | Signals in play |
|---|---|---|---|
| 1 | "This press is wearing out. The twin sees it coming." | Press close-up; tool wear and torque rising; risk gauge still green; line running. | `press/tool_wear_min`, `press/torque_nm`, `press/failure_risk` *(M5)* |
| 2 | "Minutes before it fails, it tells you." | Risk crosses warning: press amber, remaining tool life counting down, event log "Tool change due". The line keeps running. | `press/tool_rul_min` *(M5)*, `press/maint_due` *(M5)* |
| 3 | "Ignore it, and the line stops." | The failure arrives: press red, arm finishes its pick, both conveyors stop. | `press/fault_twf`, `press/machine_failure`, `line/state = 3` |
| 4 | "Act on it, and it doesn't." | Rerun: tool changed at the recommendation, tool wear back to zero, risk back to green, parts keep flowing. | `press/lockout` *(M6)*, `press/tool_wear_min` |

## Clip 6 — Workforce training & safety (~35 s)

**Built in:** M6.

| # | Beat | On screen | Signals in play |
|---|---|---|---|
| 1 | "People work next to this robot." | An operator walks toward the arm; the protective zone is drawn on the floor. | |
| 2 | "Step inside its zone, and it stops." | Operator crosses the line: zone turns red, the arm freezes mid-move, dashboard "Protective stop — arm zone", line holds. | `safety/arm_zone_occupied` *(M6)*, `safety/protective_stop` *(M6)*, `line/state = 3` |
| 3 | "Only a deliberate reset restarts it." | Operator leaves; reset pressed; arm resumes. | |
| 4 | "And the twin trains the procedure." | Press fault; lockout step list on screen. The trainee tries to restart with the lock on: refused, error count +1. They finish the steps, change the tool, remove the lock, restart. | `press/lockout` *(M6)*, `training/step` *(M6)*, `training/errors` *(M6)*, `press/tool_wear_min` |

---

Capture rules: 1920×1080 @ 60 fps, build frame-rate locked, OBS game capture, no cursor except where a beat clicks, hovers or drags. One Cinemachine path per piece (M10).

Fault types available in the injector (map to AI4I failure modes): TWF tool wear, HDF heat dissipation, PWF power, OSF overstrain, RNF random. M7 adds cell faults: gate sensor forced on or off, missing part, arm held busy.
