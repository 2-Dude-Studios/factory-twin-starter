# Virtual commissioning clip — voiceover, takes and cut (M9)

A separate clip of about 76 s for machine builders and system integrators (decided 2026-10-23). The PdM video (v02, 111 s) is unchanged. Beats follow `docs/demo-script.md` (VC section). Voice: recorded by you (decided 2026-10-23), not Kokoro TTS. The reading script is `docs/voiceover_vc_script.md`; the table below is the same words with their picture. Naming `2DS_Manufacturing_FactoryTwin_VC_v01_<date>.mp4`.

## Script (about 145 words)

| # | Video time | Words | Picture |
|---|---|---|---|
| 1 | 0–8 s | "Picture this cell before it's built. Its PLC program already exists, and it has a bug. Let's find it before the machine ships." | Take D 0–8: hall dolly lands on the cell. Commissioning panel on the left; CNC dashboard hidden. |
| 2 | 8–20 s | "This is the real control program, in Structured Text, running on a soft PLC. It drives the digital twin through the same I/O list the machine will use." | Take D 8–20: cell shot; the code card appears with `cell_buggy.st` and its `infeed_run` line; source chip "Soft PLC (OpenPLC)". |
| 3 | 20–40 s | "We run the commissioning tests. The first one fails in under two seconds: the infeed restarts while the robot is still at the gate. Parts back up, and nothing leaves the cell." | Take D 17–37: pick-station close-up; Run at 18 s; row 1 turns FAIL with the interlock named; parts queue nose-to-tail; cut back to the cell with "Failed: stopped at the first failure". |
| 4 | 40–55 s | "One line of logic. Fix it, upload, and run every test again: a stuck sensor, a dropped part, a robot that won't answer, a machine fault, a lost PLC link. All six pass." | Take E 0–4: code card with `cell.st` and the corrected line. Then take E's whole run (about 290 s) time-lapsed into about 11 s, rows turning green one by one, caption "4 min 50 s, sped up"; ends on "All 6 passed". |
| 5 | 55–68 s | "Bring us your PLC program and your I/O list. We build the twin, and you find these bugs at your desk, not on site." | I/O list card (from `tools/plc/io_list.csv`) over the blurred cell, then take E close-up with parts flowing. |
| 6 | 68–76 s | "2 Dude Studios. Book a call." | End card: logo, "Find the bug before the machine ships.", 2dudestudios.com/manufacturing. |

## Claims check

| Claim | Evidence |
|---|---|
| "running on a soft PLC" | OpenPLC Runtime v4.3.1 runs `cell_buggy.st` / `cell.st` (M7). |
| "the same I/O list" | The twin's I/O list is the one exported to `tools/plc/io_list.csv`; on a real job it would be the machine's. |
| "fails in under two seconds" | M8 run: "violated from 1.5 s". Re-check on take D's frames. |
| "nothing leaves the cell" | M8 run: 0 of 6 parts in 60 s. Take D stops at the first failure, so the picture shows the queue, not a count. |
| "All six pass" | M8 run on the PLC with `cell.st`: 6 of 6. Must hold again in take E, or the line changes. |
| No savings figures | Vendor-reported numbers (25–70 %) stay off this clip. |
| "Picture this cell before it's built" | Framing: the cell is a twin; nothing claims a real machine exists. |

## Takes

| Take | PLC program | What it records | Length |
|---|---|---|---|
| D | `cell_buggy.st` | Dolly, cell, code card, close-up, suite run stopping at the first failure | 46 s |
| E | `cell.st` | Code card, the whole suite on the PLC, shots alternating cell / close-up / line | 330 s |

Both are VC takes: `TakeSequencer` hands the line to the PLC, hides the CNC dashboard, shows the commissioning panel, and the Recorder records at a variable rate (up to 60 fps) so the plant runs in real time. Set `warmUpSeconds` to about 20 s for VC takes; parts/hour does not matter here. Load the take's program into the PLC first (`tools/plc/README.md`), and check `cell/sim/realtime_ratio` stayed within ±10 % (the suite fails the run otherwise).

## Build

New scripts in `tools/video/vc/` (separate from the PdM scripts): `build_vc.py` cuts the takes, time-lapses take E, adds the I/O list card and the end card, and places your recorded voice blocks (one WAV per numbered block) on the picture, mixed at −14 LUFS. No TTS step.
