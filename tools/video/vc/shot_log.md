# Part 2 (virtual commissioning) — raw takes, shot log

Captured 2026-10-31 for Part 2 of the series ("the real PLC program drives the twin"). Raw takes only: no voiceover, no edit. Recordings live in `Builds/Recordings/` (git-ignored). Scenario, results and the earlier voiceover draft: `docs/vc_video_script.md`.

## How the takes were made

- **Setup:** Mosquitto broker up (Docker, 1883; the player logged "Connected to 127.0.0.1:1883"). OpenPLC Runtime up (Modbus 502) with the program named per take; the runtime's compiled `program.st` and the PLC heartbeat were checked before each run. Logic source PLC (`TakeSequencer` hands the line to the PLC in every F take).
- **From the Windows build, not the Editor.** Unity Recorder's constant 60 fps sets game time to 1/60 s per rendered frame. The Editor renders soft-PLC takes at about 32 fps, so the plant would run at half speed against the PLC's wall-clock timers and fail the real-time check. The build (`Builds/WindowsCapture/`, built 2026-10-31) holds frame rate easily, so the takes run there:
  - `python tools/video/vc/capture_build_takes.py --exe Builds/WindowsCapture/FactoryTwin.exe --takes F1,F2,… --warmup 150`
  - It starts the player with `-takes`, `-warmup` and `-quitwhendone`, a borderless 1920×1080 window and `-targetfps 120` (see "Frame stalls"). F3 and F8 were recorded that way; F1, F2, F4 and F7 came from the build before the cap, which ran uncapped (230–350 fps) and also dropped no frames in those takes.
  - It follows the player log, where `TakeEventLog` writes "[TakeLog] <time> …" lines.
  - From each pre-roll it records the window with ffmpeg `gfxcapture` (Windows Graphics Capture, no cursor, NVENC).
  - It then cuts each take at its logged start, adds the game audio that `TakeAudioRecorder` wrote for that take, and writes the file.
- **Output (every F take):** 1920×1080, H.264 High 4:2:0, CRF 14, constant 60 fps, AAC 48 kHz stereo (hall ambience, about −32 dB mean). F9 has a silent audio track.
- **Warm-up:** 150 s before F1, F2, F4, F7 and F8. F3 got 30 s: on the bugged program, a long warm-up has parts piled at the gate before the take starts.
- **Panels:** hidden in F1–F4 and F7. F8 shows the dashboard and the commissioning panel.
- **Times below:** seconds from the start of each file. They come from the player log, aligned to the capture to within about 0.1 s.

## Files

| Take | File (`Builds/Recordings/`) | Length | PLC program | Shot | Key events (s from file start) |
|---|---|---|---|---|---|
| F1 Opener | `2DS_FactoryTwin_TakeF1_20261031_012353.mp4` | 15.0 s | `cell.st` | 0 hall dolly | Dolly 0–8 s, lands on the cell, holds 8–15 s. Line running throughout. |
| F2 Overview | `2DS_FactoryTwin_TakeF2_20261031_011503.mp4` | 16.0 s | `cell.st` | 9 `Shot9_Overview` (new, static) | Whole cell in frame; picks at about 3.8 and 10.9 s (line state 0→1 at 5.9 and 12.9 s). Station pixels below. |
| F3 The bug in 3D | `2DS_FactoryTwin_TakeF3_20261031_014610.mp4` | 32.0 s | `cell_buggy.st` | 10 `Shot10_GateClose` (new) | Suite run (panel hidden) sent at 1.0 s; test 1 starts 6.38 s; **interlock violated at about 7.8 s** (1.4 s into the test); test 1 FAIL and "stopped at the first failure" logged 8.14 s. The line keeps running on the bug: the next part rides into the gate under the arm, parts crowd nose to tail at the gate, and one is knocked off the belt onto the floor at about 14 s. Report below. |
| F4 The fix in 3D | `2DS_FactoryTwin_TakeF4_20261031_011531.mp4` | 22.0 s | `cell.st` | 10 `Shot10_GateClose` (same framing as F3) | Parts flow cleanly, spaced; picks at about 4.0, 11.0 and 18.0 s. |
| F5 Proof: the FAIL | Take D, `2DS_FactoryTwin_TakeD_20261030_235059.mp4` (reused) | 46.0 s | `cell_buggy.st` | 0 → 7 → 5 → 7 | Panel and framing unchanged since, so not re-recorded. Code card `cell_buggy.st · line 219` from 12 s; Run tests 18 s; row 1 **FAIL** at about 26 s ("Interlock: infeed stops for a part at the gate: violated from 1.4 s"), "Failed: stopped at the first failure"; queue on the infeed 26–46 s. |
| F6 Proof: all pass | Take E, `2DS_FactoryTwin_TakeE_20261030_233630.mp4` (reused) | 447.2 s | `cell.st` | one shot per test | Code card `cell.st · line 212` 0–4 s; Run 4 s; test starts about 9, 71, 123, 170, 222, 258, 285, 322, 354, 389 s (tests 1–10, ±1 s); CNC glows red about 225 s; test 10 (worker walks into the zone): walk from about 391 s, protective zone about 399 s, reset refused about 403 s, out about 407 s, reset and restart about 417 s; **"All 10 passed"** 440–447 s. |
| F7 Robot zone | `2DS_FactoryTwin_TakeF7_20261031_013212.mp4` | 32.0 s | `cell.st` | 11 `Shot11_ZoneTrack` (new, follows the walker) | Walk to the warning zone sent 2.0 s; **warning zone** 2.6 s (yellow floor, arm speed 0.25); walk on 9.0 s; **protective zone** 9.44 s (orange floor, arm speed 0, line state 4); reset pressed from inside 14.0 s, **refused** (stop stays); walker out 17.5 s (awaiting reset), warning clear 18.3 s; **reset from outside** 24.0 s, line restarts 24.06 s; parts moving again 26 s. |
| F8 Desktop dashboard | `2DS_FactoryTwin_TakeF8_20261031_015642.mp4` | 14.0 s | `cell.st` | 1 cell, high | Whole window: commissioning panel (left) with the source chip **"Soft PLC (OpenPLC)"** in green, "Program: cell.st", "Not run yet"; CNC & line dashboard (right) with **"Broker connected"**, CNC Healthy, line **Running** at 510 parts/hour, cycle time 7.0 s, event log "Soft PLC runs the line". |
| F9a Code, bug | `2DS_FactoryTwin_TakeF9a_20261031_014953.mp4` | 8.0 s | — | — | `tools/plc/cell_buggy.st` lines 219–225 in an editor-style view, line 219 highlighted red: `infeed_run := state = FEEDING OR (state = TRANSFERRING AND ack_seen);` |
| F9b Code, fix | `2DS_FactoryTwin_TakeF9b_20261031_014953.mp4` | 8.0 s | — | — | `tools/plc/cell.st` lines 212–218, line 212 highlighted green: `infeed_run := state = FEEDING;` |

F9 is rendered rather than screen-captured. `tools/video/vc/render_code_takes.py` reads the real file text: dark theme, 38 px monospace, line numbers, file name in a tab. It starts at the `infeed_run` line, so `cell_buggy.st`'s "SEEDED BUG" comment above it stays out of view.

## F2 station positions (pixels in the 1920×1080 frame)

These are computed from the overview camera's settled pose: position (−1.2, 5.6, −5.4), pitch 31.73°, yaw 354.77°, vertical FOV 33°. They were checked against the F2 frame and land on each station. The shot is static, so they hold for the whole take.

| Station | x, y |
|---|---|
| Infeed start (part source) | 1436, 815 |
| Infeed (middle) | 1196, 801 |
| Gate light barrier | 961, 787 |
| Arm (base, on its stand) | 917, 707 |
| Outfeed (middle) | 583, 688 |
| Exit sensor | 403, 678 |
| Sink (outfeed end) | 310, 673 |
| CNC machining centre (body) | 1280, 394 |
| CNC stack light | 1295, 286 |
| Protective zone (centre, floor) | 919, 809 |

## Suite reports

- F3: `Builds/Commissioning/vc_plc_20261031_014619.csv`, copied from the player's `LocalLow/DefaultCompany/factory-twin-starter/Commissioning/`. Program `cell_buggy.st`. "At least 6 parts out" FAIL (0 of 6, run cut short), both interlocks FAIL "violated from 1.4 s", real time pass.
- F5 (take D): `Builds/Commissioning/vc_plc_20261030_235126.csv`.
- F6 (take E): `Builds/Commissioning/vc_plc_20261030_234349.csv` (10 of 10 tests, 68 of 68 checks).

## Frame stalls

- **Every F take: no stalls.** No frame gap over 25 ms in the capture of any F take, so constant 60 fps with no dropped frames. The player's own log (`-logfps`) shows no frame over 24 ms during any take. F9 is rendered at a constant 60 fps.
- **F5 and F6 (takes D and E, reused):** Editor recordings at a variable rate of about 31–33 fps, with no gaps (checked on 2026-10-30). Convert them to 60 fps in the edit.
- **What it took to get there:**
  - The build's normal 60 fps cap drifts against this ~60 Hz monitor, so the compositor skipped a frame now and then.
  - With the cap lifted, the player ran near 300 fps and starved the compositor whenever anything else drew.
  - Fix: capture takes start the player with `-targetfps 120` (new in `FrameRateLock`; the default stays 60).
  - The script also keeps the player window on top.
  - Superseded attempts are in `Builds/Recordings/superseded_capture/`. They are an F1 that hitched on its first pass through the hall, F7s shot from the wrong side or with dropped frames, F3s with dropped frames, the first F9 render, an Editor probe, and a first F8 whose parts/hour read about 1050.
- **Why that F8 read about 1050 parts/hour:**
  - Handing the line to the PLC switches `line/parts_out` to the PLC's own count, a jump from 13 to 33 here.
  - The 120 s parts/hour window counted that jump as exits.
  - `TakeSequencer` now hands over before the warm-up, so the jump is out of the window before the first take.

## Notes for the edit

- **Callouts:** F2's static framing suits leader-line callouts, using the pixel table above.
- **F3 → F4:** a straight before/after on the same framing.
- **F7:** the yellow-then-orange floor lighting carries the zone beat without text.
- **Background content:** workers stand and work in the background of several shots. The in-world "Lockout training" board (an M11 prop) is visible right of the CNC in F2.
- **F8 legibility:** the commissioning panel's text is small at 1080p, so use a punch-in on the source chip and the first rows.
- **Raw captures:** each take's uncut capture and its audio WAV are in `Builds/Recordings/capture_work/<run>/`. The logged events for every run are in `Builds/Recordings/capture_<run>_events.json`.
