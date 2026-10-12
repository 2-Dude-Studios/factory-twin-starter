# Demo script — 90-second walkthrough beats

Structure follows the walkthrough video playbook: promise / proof / path / ask. Voiceover is rendered before capture; these beats are what the camera must show under each line.

| # | Time | Beat | On screen | Signals in play |
|---|---|---|---|---|
| 1 | 0–8 s | **Promise.** "This is a production cell you can see, query and stress-test before it exists." | Cinemachine dolly from hall wide to the cell. Dashboard slides in, all green. | `cell/*/state = running` |
| 2 | 8–22 s | Parts flow. | Close on infeed conveyor; part hits gate sensor; sensor lamp flips; arm picks, places on outfeed. Loop twice. | `gate1/part_present`, `arm/j*_deg`, `outfeed/count` |
| 3 | 22–38 s | Every part of it is live data. | Split: 3D on left, dashboard right. OEE, cycle time, parts/hour tick with the loop. Hover a conveyor → its topic and value appear. | all |
| 4 | 38–58 s | **Proof — fault injection.** "Now the press reports tool wear past its limit." | Click **Inject fault → Tool wear**. Press turns amber then red in 3D, dashboard health drops, failure-risk gauge climbs, event log line appears, the arm finishes its pick and both conveyors stop. | `press/tool_wear_min`, `press/fault_twf`, `press/health`, `line/state` |
| 5 | 58–70 s | Reset and recovery. | Click **Clear**. Cell returns to green; parts/hour recovers on the sparkline. | |
| 6 | 70–84 s | **Path.** "Your line: CAD in, signals mapped, deployed on desktop, kiosk or headset." | Three quick cuts: same cell on a touch panel view, on Quest pass-through (or a still if phase 3 is not built), and the S7/OPC UA config screen. | |
| 7 | 84–90 s | **Ask.** Book a call. 2dudestudios.com/manufacturing. | End card, logo, URL. | |

Capture rules: 1920×1080 @ 60 fps, build frame-rate locked, OBS game capture, no cursor except in beats 3–5.

Fault types available in the injector (map to AI4I failure modes): TWF tool wear, HDF heat dissipation, PWF power, OSF overstrain, RNF random.
