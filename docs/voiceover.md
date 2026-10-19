# Voiceover — 90-second predictive-maintenance walkthrough

Record this first; the capture is cut to it. 171 words at a calm 140–145 words per minute is about 72 seconds of speech, which leaves room for the sparks, the stop and the end card to breathe. Beats and shots match `docs/demo-script.md`.

## Read version

Read it as written. `/` is a short breath, `//` a full pause (about one second). Say the URL as "two dude studios dot com, slash manufacturing".

> **[1]** This machining centre is going to fail. // Its digital twin knows when.
>
> **[2]** Every part of this cell is live data / — the conveyors, the gate, the robot, / and the machine itself: / spindle speed, torque, temperature and tool wear.
>
> **[3]** The tool is wearing. / The twin measures how close every reading is to the machine's failure limits. // Risk climbs, / the light turns amber, / and it tells you when to change the tool — / while the line is still running.
>
> **[4]** Ignore it, / and the tool fails. // The machine stops, / the robot finishes its pick, / both conveyors halt — / and the twin counts every part you're losing.
>
> **[5]** Act on it instead. // One tool change at the recommendation: / wear resets, / risk drops back to zero, / and the line never stops. // Zero parts lost.
>
> **[6]** Your line works the same way. / We connect to your machines over MQTT or OPC UA, / set their limits from your maintenance history, / and build the twin around them.
>
> **[7]** See your line stop — before it does. // Book a call: two dude studios dot com, slash manufacturing.

## Timing sheet

| # | Time | Words | Line | On screen | Camera key | Take key |
|---|---|---|---|---|---|---|
| 1 | 0–8 s | 12 | "This machining centre is going to fail. Its digital twin knows when." | Hall-to-cell dolly; dashboard all green; stack light green. | **4** (8 s dolly) | — |
| 2 | 8–20 s | 25 | "Every part of this cell is live data — the conveyors, the gate, the robot, and the machine itself: spindle speed, torque, temperature and tool wear." | Parts flow through gate, arm, outfeed; sparklines moving. | **5**, cut to **6** | — |
| 3 | 20–38 s | 38 | "The tool is wearing. The twin measures how close every reading is to the machine's failure limits. Risk climbs, the light turns amber, and it tells you when to change the tool — while the line is still running." | Tool wear rising, risk bar filling, amber stack light, "Tool change due" in the log. | **7** | **1** (Wear tool out) |
| 4 | 38–55 s | 25 | "Ignore it, and the tool fails. The machine stops, the robot finishes its pick, both conveyors halt — and the twin counts every part you're losing." | Sparks, red glow, flashing red light, "Stopped — CNC fault", parts lost climbing. | **7**, then **5** | (failure arrives by itself at +76 s) |
| 5 | 55–70 s | 25 | "Act on it instead. One tool change at the recommendation: wear resets, risk drops back to zero, and the line never stops. Zero parts lost." | Second take: tool changed at amber; wear back to fresh, risk 0 %, green, parts lost 0. | **7**, then **5** | **2** (Change tool) at amber |
| 6 | 70–84 s | 29 | "Your line works the same way. We connect to your machines over MQTT or OPC UA, set their limits from your maintenance history, and build the twin around them." | Control-panel close-up, then the dashboard; optional cut to the broker's live topic list. | **8**, then **5** | — |
| 7 | 84–90 s | 17 | "See your line stop — before it does. Book a call: two dude studios dot com, slash manufacturing." | End card: logo, 2dudestudios.com/manufacturing (made in the edit). | — | — |

## Capture takes

Record the build at 1920×1080, 60 fps (OBS game capture). Before each take, let the line run at least **2.5 minutes** (parts/hour settles near 510), then press **0** for a fresh take (event log and parts lost reset, line keeps running). F10 hides the cursor.

- **Take A — beats 1–4 (ignore it).** 4 (dolly) → 5 → 6 → press **1** and cut to **7**. "Tool change due" appears at about +28 s, the amber warning at about +42 s, the failure at about +76 s. Stay on 7 for the sparks, then **5** to show both conveyors stop and parts lost climb.
- **Take B — beat 5 (act on it).** **0** fresh take → **1** → wait on **7** for amber (about +42 s) → **2** Change tool → **5** while parts keep flowing. Parts lost stays 0.
- **Take C — beat 6 inserts.** **8** control-panel close-up and **5** cell, healthy. Optional screen capture of a terminal running `mosquitto_sub -v -t 'cell/#'` for the "live data" cut (start the broker first).

In the edit, beat 3 compresses the 42-second build-up to 18 seconds: cut from the early climb to the amber warning; don't speed-ramp the dashboard numbers. The dashboard footnote already credits the data ("Telemetry: AI4I 2020 dataset (CC BY 4.0), replayed"); keep it readable in at least one shot, or add it to the end card.

## Claims check

Every line has to be true of what is on screen or of what the studio can deliver:

- "knows when": the warning comes 34 s before the failure in the demo (measured); the demo compresses hours of real tool wear into about a minute.
- "how close every reading is to the machine's failure limits": the risk model is threshold margins to AI4I's published limits (no ML), and the line says exactly that.
- "MQTT or OPC UA": MQTT is built; OPC UA is a planned client-project option (`OPCFoundation/UA-.NETStandard`, MIT), not in this build. **Kept (decided 2026-10-20):** the studio will deliver OPC UA on the first client project if asked.
- "set their limits from your maintenance history": how a client version would set the margins; not shown in the demo.

## Recording tips

USB mic 15–20 cm away, slightly off-axis; a quiet room with soft furnishings; record each beat as its own take with a second of room tone before and after; 48 kHz / 24-bit WAV. Leave the factory ambience and music to the edit, ducked under the voice.
