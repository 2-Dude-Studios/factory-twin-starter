# Voiceover — predictive-maintenance walkthrough (v02, approved 2026-10-21)

The approved video is `Builds/Recordings/2DS_Manufacturing_FactoryTwin_PdM_v02_20261021.mp4` (git-ignored): 111 s, 1920×1080, 60 fps, H.264 + AAC 48 kHz, −14 LUFS integrated, −1 dBTP. The script runs 233 words. It opens by explaining what a digital twin is, then shows predictive maintenance on the cell. Beats and shots match `docs/demo-script.md`; v01 (93 s, no introduction) is superseded.

## Read version

**Bold** marks the 13 emphasised phrases: each is spoken a little slower, about 2.5 dB louder and 3.5 % higher in pitch. `//` is a full pause of about one second. Say the URL as "two dude studios dot com, slash manufacturing", and MQTT and OPC UA letter by letter.

> **[Intro]** A digital twin is a live, virtual copy of a real machine, or a whole production line. // It runs on the same data as the factory floor, so you can watch it, test it, and predict what happens next, **without touching the real thing.**
>
> **[Cell]** This one is a single production cell: an infeed conveyor, a sensor gate, a robot arm, **and a CNC machining centre.** // Every part of it streams live data.
>
> **[Use case]** That data powers one of the most valuable uses of a twin in manufacturing: **predictive maintenance.** // This machining centre is going to fail. **Its twin knows when.**
>
> **[Sees it coming]** The tool is wearing. The twin measures how close every reading is to the machine's failure limits. **Risk climbs. The light turns amber.** // And it tells you to change the tool, while the line is still running.
>
> **[Ignore it]** Ignore it, **and the tool fails.** // The machine stops. The robot finishes its pick. Both conveyors halt. **And the twin counts every part you're losing.**
>
> **[Act on it]** **Act on it instead.** // One tool change, at the recommendation: wear resets, risk drops back to zero, and the line never stops. // **Zero parts lost.**
>
> **[Your line]** Your line works the same way. We connect to your machines over MQTT, or OPC UA, set their limits from your maintenance history, **and build the twin around them.**
>
> **[Ask]** **See your line stop, before it does.** // Book a call at two dude studios dot com, slash manufacturing.

## Cut map

| Time | Beat | Picture | Source | Cue the line lands on |
|---|---|---|---|---|
| 0–6 s | Intro | Start card: logo, "Factory Digital Twin", "Predictive maintenance on a live production cell", over a blurred still of the hall | Take A frame 0 | Dissolves into the dolly at 5–6 s |
| 5–13 s | Intro | Hall-to-cell dolly | Take A 0–8 | — |
| 13–25 s | Cell | Cell, then line close-up | Take A 8–20 | "a robot arm" over the arm |
| 25–39.5 s | Use case | CNC with stack light, healthy, wear starting | Take A 20–34.5 | "going to fail" while still green |
| 39.5–55 s | Sees it coming | CNC, risk climbing to amber | Take A 55–70.5 | Amber at 46.0 s, on "Risk climbs. The light turns amber." |
| 55–72 s | Ignore it | Failure: sparks, flashing red, then cell with the line stopped | Take A 91–108 | Failure at 59.3 s, just after "the tool fails" |
| 72–87 s | Act on it | CNC at amber, tool change, back to green, cell running | Take B 46–61 | Tool change at 78.0 s, on "wear resets" |
| 87–101 s | Your line | Control-panel close-up, then cell | Take C 1–8, 11–18 | — |
| 101–111 s | Ask | End card: logo, "See your line stop — before it does.", yellow "Book a call → 2dudestudios.com/manufacturing" box, "Digital twins for manufacturing · Unity · MQTT · OPC UA", over the cell blurred and running | Take B 61–71 | Fades to black over the last second |

## How v02 was made

- **Voice:** Kokoro-82M TTS, voice `am_michael` (US male), through `kokoro-onnx` 0.6.1 on CPU. Each phrase is rendered separately, trimmed, and placed on the timeline at its cue. Emphasised phrases are rendered slower (speed 0.88–0.95) and get a rubberband pitch lift of 1.035 with formants preserved, plus 2.5 dB. The voice chain is a 90 Hz high-pass, de-esser and gentle compressor (2.5:1), set to −16 LUFS. Licence line in `docs/LICENSES.md`.
- **Game audio:** the takes' own audio (factory ambience, spark crackle), brought to about −30 LUFS and ducked under the voice with a sidechain compressor (5:1). It is lifted 5 dB from 58.5 to 72 s so the sparks read in the failure beat. Two-pass loudnorm takes the mix to −14 LUFS and −1 dBTP.
- **Cards:** built in ffmpeg. The logo is `2 Dude Logo Big.png` from the studio's branding folder (not copied into the repo). Headings are set in Bahnschrift, body text in Segoe UI, and the accent is the logo's yellow.
- Rebuild with `tools/video/` (`tts2.py`, then `build2.py`; see its README). Wording, pace, emphasis and anchors live in `tools/video/script_v2.py`.

## Capture takes

**Automated:** in the Editor, Play `Cell.unity` and tick `runNow` on `CaptureRig` → `TakeSequencer`. It warms up, plays takes A, B and C exactly as below, and records each to `Builds/Recordings/` with Unity Recorder (1920×1080, 60 fps, audio). **By hand:** record the build at 1920×1080, 60 fps (OBS game capture). Before each take, let the line run at least **2.5 minutes** (parts/hour settles near 510), then press **0** for a fresh take (event log and parts lost reset, line keeps running). F10 hides the cursor.

- **Take A — ignore it.** 4 (dolly) → 5 → 6 → press **1** and cut to **7**. "Tool change due" appears at about +28 s, the amber warning at about +42 s, the failure at about +76 s. Stay on 7 for the sparks, then **5** to show both conveyors stop and parts lost climb.
- **Take B — act on it.** **0** fresh take → **1** → wait on **7** for amber (about +42 s) → **2** Change tool → **5** while parts keep flowing. Parts lost stays 0.
- **Take C — inserts.** **8** control-panel close-up and **5** cell, healthy.

The dashboard footnote credits the data ("Telemetry: AI4I 2020 dataset (CC BY 4.0), replayed") and stays readable in the dashboard shots.

## Claims check

Every line has to be true of what is on screen or of what the studio can deliver:

- "A digital twin is a live, virtual copy … runs on the same data as the factory floor": the general definition. In this demo the "live" data is the public AI4I dataset replayed over MQTT, and the dashboard footnote says so.
- "knows when": the warning comes 34 s before the failure in the demo (measured); the demo compresses hours of real tool wear into about a minute.
- "how close every reading is to the machine's failure limits": the risk model is threshold margins to AI4I's published limits (no ML), and the line says exactly that.
- "MQTT, or OPC UA": MQTT is built; OPC UA is a planned client-project option (`OPCFoundation/UA-.NETStandard`, MIT), not in this build. **Kept (decided 2026-10-20):** the studio will deliver OPC UA on the first client project if asked.
- "set their limits from your maintenance history": how a client version would set the margins; not shown in the demo.

## Re-recording with a human voice

USB mic 15–20 cm away, slightly off-axis; a quiet room with soft furnishings; record each phrase group as its own take with a second of room tone before and after; 48 kHz / 24-bit WAV. Land the same cues as the cut map, and stress the bold phrases.
