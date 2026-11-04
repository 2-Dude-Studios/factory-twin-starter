# Fire safety training — use case 4 (Rev 6, approved 2026-11-04)

A fourth use case for the factory twin: fire prevention, extinguisher use, raising the alarm and evacuation, practised in VR on the same cell, crew and dashboard as Rev 5. Quest 3 over Link and desktop, reusing the M12 rig.

**What this is not.** A training demo, not certified or "compliant" training, and not a substitute for the employer's own emergency action plan, fire prevention plan or hands-on training. Section 1 is written as an auditor's guidance. It is not legal advice. The employer and the local authority having jurisdiction (AHJ) decide what applies to a real plant.

Contents: 1 Auditor's view · 2 Scenarios · 3 Training objectives by requirement · 4 3D and audio assets · 5 Trainee phases · 6 Session flow and training record · 7 Build phases and milestones · 8 Capture plan · 9 Open decisions · Sources

---

## 1. Auditor's view: what VR can train, and what it can't

I audit a machining cell like this one against three OSHA rules first, then the NFPA codes they lean on.

- **29 CFR 1910.39, fire prevention plan.** The rule asks you to name the cell's major fire hazards, the ignition sources and how you control them, the equipment for each hazard, and who maintains the heat-producing equipment ((c)(1)–(c)(5)). It also asks you to tell each employee about the fire hazards of their job when they start ((d)) and to review the parts of the plan they need to protect themselves ((e)). **VR fits well here.** A trainee can walk their own cell and find the oily rags, the blocked extinguisher and the overheating cabinet before anything burns.
- **29 CFR 1910.38, emergency action plan.** The plan covers how to report a fire ((c)(1)), how to evacuate and by which routes ((c)(2)), who shuts down critical operations before leaving ((c)(3)), how everyone is accounted for afterwards ((c)(4)), and who does rescue or medical duties ((c)(5)). It also needs an alarm with a distinctive signal ((d), with 1910.165), designated evacuation helpers ((e)), and a review with each employee ((f)). **VR fits very well here too.** Drills in a running plant are disruptive and rare, and a blocked exit or a missing colleague cannot be staged safely.
- **29 CFR 1910.157, portable fire extinguishers.** The plant must choose a policy. With total evacuation, nobody uses an extinguisher ((b)(1)) or none is meant for employees ((b)(2)). If extinguishers are there for employees, everyone gets an educational program on extinguisher use and the hazards of fighting an incipient fire, on hire and **at least annually** ((g)(1)–(g)(2)). Designated firefighters get training on the actual equipment, on assignment and annually ((g)(3)–(g)(4)). OSHA's 1991 interpretation says hands-on training need not be live fire, but should at least include actually discharging the right extinguishers. **VR covers the education in (g)(1) well.** That means classes, selection, PASS, stand-off distance, fight or flee and reflash, repeated as often as needed. **VR does not discharge a real extinguisher.** For designated employees under (g)(3), I would pair the VR session with a physical discharge, using a water trainer or a digital extinguisher trainer, and record it. The training record in section 6 has a field for that.
- **Supporting rules.** 1910.155(c)(26) defines an incipient-stage fire, and that definition is the line between fighting and fleeing. 1910.36–37 cover exit routes: at least two, kept unobstructed, marked "EXIT", and "NOT AN EXIT" where needed. 1910.165 covers the alarm: heard or seen above ambient noise, distinctive, with a known way to report.
- **NFPA 10** (portable extinguishers) is where OSHA's numbers come from. It covers fire classes A/B/C/D/K, travel distance (OSHA: Class A ≤ 75 ft, Class B ≤ 50 ft, 1910.157(d)), mounting heights and the monthly visual inspection: in place, unobstructed, gauge in the green, pin and tamper seal intact, tag current. **NFPA 101** (Life Safety Code) is relevant for keeping the means of egress clear (7.1.10), exit marking and emergency lighting (7.10, 7.9) and drills (4.7). **NFPA 72** applies only to the alarm: audible above the cell's noise, with the temporal-3 evacuation tone. **NFPA 80** says fire doors stay closed or are held open only by a device that releases on alarm.
- **Outside the US**, only if a prospect asks. In the UK, the Regulatory Reform (Fire Safety) Order 2005 art. 21 requires training on hire, on new risks and repeated periodically. BS 5306-8 covers extinguisher selection and siting, and ISO 7010 is the signage (E001/E002 exit, F001 extinguisher, F005 call point, E007 assembly point). We build US signage first and keep ISO 7010 pictograms where they help a global audience.

**Why it sells.** Labels: [R] regulator, [P/A] practitioner/analyst, [V] vendor.
- NFPA: about 37,900 fires a year at US industrial or manufacturing properties (2011–2015 average), about $1.2 B direct damage, 16 civilian deaths and 273 injuries a year. Mechanical failure or malfunction contributed to about a quarter of the structure fires. [P/A] (These figures come from secondary summaries; check the NFPA report before putting any figure on screen.)
- 1910.157(g) training is **annual**, so every plant that lets employees use extinguishers needs the module every year. That makes it a recurring need, unlike a one-time commissioning job.
- Competing products are generic VR fire modules and laser or digital extinguisher trainers. None found runs the drill on the plant's own live twin, where the dashboard sees the overheating cabinet first, holds the line on alarm and counts heads at the assembly point.

**Policy modes the module supports** (set per client in the scenario asset, because the employer chooses):

| Mode | Rule | What the trainee must do |
|---|---|---|
| Evacuate only | 1910.157(b)(1)/(b)(2) | Prevention, raise the alarm, evacuate, roll call. Picking up an extinguisher is logged as a mistake. |
| All employees (default for the demo) | 1910.157(g)(1)–(2) | Above, plus fight an incipient fire with the right extinguisher, or flee when the fight-or-flee test fails. |
| Designated employees | 1910.157(g)(3)–(4) | As "All employees", plus the extinguisher pre-use check, and a reminder that a physical discharge must be recorded. |

---

## 2. Realistic fire scenarios in this cell

The cell has a CNC machining centre (oil-based coolant, hot chips), a six-axis arm, a robot controller and conveyor drive cabinet, two conveyors, cardboard and pallets at the infeed, and the hall's wall chargers for forklifts and AGVs.

| # | Scenario | Class | Correct response | The twin's part |
|---|---|---|---|---|
| F1 | **Electrical cabinet overheats** (drive or controller panel; a loose terminal) | C while energised | Stop the line, raise the alarm, keep the cabinet door shut. Isolate upstream only if it is safe to reach. Use a CO2 or ABC extinguisher from the side, never water. Once the cabinet is de-energised, treat it as A. | The dashboard shows the cabinet temperature climbing **before** any smoke (1910.39(c)(3)). The prevention beat and the fire beat use the same signal. |
| F2 | **CNC coolant-mist fire** (tool breakage ignites oil mist or chips in the enclosure) | B | Press feed hold or E-stop, keep the enclosure closed to starve it of oxygen, raise the alarm. Only a small flare at the door opening gets a B-rated extinguisher (CO2 or ABC). No water on oil. | CNC telemetry (spindle load, temperature) spikes. The press station already reports health. |
| F3 | **Combustible waste fire** (oily rags in an open bin, cardboard at the infeed) | A | Raise the alarm, then use ABC or water with PASS on the base and watch for reflash. Prevention: a self-closing oily-waste can and a clean infeed. | A smoke detector signal and the zone shown on the dashboard. |
| F4 | **Fire beyond incipient** (F2 or F3 grows: smoke banks down, the extinguisher empties, or the fire is between you and the exit) | — | Stop fighting, back out with your face to the fire, close the door, evacuate, report. | Fire stage on the dashboard; the line held. |
| F5 | **Blocked exit route** (a pallet in the primary route, during F4) | — | Take the second route (1910.36(b)), report the obstruction afterwards. | Route state per exit. |
| F6 | **Lithium-ion battery fire at the charger** (advanced, flee only) | — | Do not fight it: thermal runaway, toxic gas, re-ignition. Raise the alarm, evacuate, warn the responders. | The charger zone's smoke detector. |
| H | **Pre-shift hazard hunt** (no fire) | — | Find and report: blocked extinguisher, gauge in the red or missing pin, oily rags in an open bin, pallet in the exit route, wedged fire door, daisy-chained extension leads, solvent outside the flammables cabinet. | Each find is logged against its clause. |

Left out on purpose: hot work and fire watch (1910.252, NFPA 51B), combustible dust (NFPA 652), and Class D metal fires. They don't fit this cell. They are listed under Later in the build plan.

---

## 3. Training objectives, each tied to its requirement

| # | The trainee can… | Requirement | Phase |
|---|---|---|---|
| O1 | Name the cell's major fire hazards and ignition sources, and how each is controlled | 1910.39(c)(1), (d) | 1 |
| O2 | Spot and report housekeeping hazards (oily waste, combustibles, flammables out of their cabinet) | 1910.39(c)(2); 1910.106 for flammable liquids storage | 1 |
| O3 | Recognise an overheating machine or cabinet as a fire precursor and report it | 1910.39(c)(3)–(c)(4) | 1 |
| O4 | Locate both exit routes, the nearest extinguishers, the alarm call point and the assembly point | 1910.38(c)(2), (f); 1910.37(a)(3), (b); NFPA 101 7.10 | 1 |
| O5 | Explain fire classes and pick the right extinguisher; never water on C or B | 1910.157(g)(1); NFPA 10 classification | 2 |
| O6 | Carry out the pre-use check (gauge, pin, seal, tag, hose) and reject a faulty unit | 1910.157(c)(4), (e)(2); NFPA 10 monthly inspection | 2 |
| O7 | Use PASS from a safe stand-off with the exit at their back, sweep the base, back away and watch for reflash | 1910.157(g)(1), (g)(3) | 2 |
| O8 | Apply the fight-or-flee test and stop fighting a fire past the incipient stage | 1910.155(c)(26); 1910.157(g)(1); OSHA extinguisher eTool | 2–3 |
| O9 | Raise the alarm first, by the plant's preferred means | 1910.38(c)(1); 1910.165(b)(4) | 3 |
| O10 | Make the cell safe before leaving if that is their assigned duty (stop the line, feed hold) | 1910.38(c)(3) | 3 |
| O11 | Evacuate by the assigned route, take the alternate when it is blocked, stay below smoke, close doors behind them | 1910.38(c)(2); 1910.36(b); NFPA 80 | 3 |
| O12 | Report to the assembly point, be counted, report a missing colleague, never re-enter | 1910.38(c)(4), (c)(5) | 3 |
| O13 | Do all of the above unguided in a random scenario, with a record of every step and mistake | 1910.157(g)(2), (g)(4) (annual); 1910.38(f) | 4 |

---

## 4. 3D and audio assets

Register key: **Have** = already in the project. **Make** = we model or author it in Blender or in code, like the LOTO props (`tools/blender/`). Our own work needs no LICENSES.md row. **Source** = a third-party candidate, imported only after you approve it, with a LICENSES.md row in the same commit.

### Already in the project

| Asset | Where | Use | Register |
|---|---|---|---|
| Hall, walls, doors, loading gate, truck trailer | Factory Training (`wall_door_GRP`, `wall_gate_GRP`, `truck_trailer`) | Exits, the route out to the yard | Row exists |
| Pallets, boxes, barrels, plastic container, shelves | Factory Training `Prefabs/env` | Class A fuel at the infeed, exit blocker (F5), oil drum | Row exists |
| Wall chargers | Factory Training `wall_chargers_GRP` | F6 battery fire location | Row exists |
| Barrier, steel barrier, cones, walkway fence | Factory Training | Cordoning, route marking | Row exists |
| Control cabinet (standing and hanging), 3D emergency button | realvirtual `3DPrefabs/ControlCabinet` | F1 cabinet; E-stop | Row exists (compiled only) |
| Emergency assembly point decal | realvirtual `3DPrefabs/Decals/EmergencyAssemblyPoint` | Assembly point floor marking | Row exists |
| Fence door | realvirtual `Fences/Fence-1m-Door` | Gate on the walkway | Row exists |
| CNC machining centre, stack light | realvirtual `CNC.prefab` | F2 | Row exists |
| Flames: Tiny, Medium, Large, WildFire; SmokeEffect, HeatDistortion, ElectricalSparks; Steam, PressurisedSteam; Shower, WaterLeak | Particle Pack 3.1 | Fire stages, smoke, cabinet sparks. Extinguisher streams are built as our own particle systems from these textures (dry-chem cloud from SmokeEffect, CO2 from PressurisedSteam, water from Shower) | Row exists. Update its usage note when added |
| Electric sparks sound, factory ambience | `Assets/_Project/Audio` (Freesound CC0) | Cabinet arcing, bed | Rows exist |
| NPC crew (4 Rocketbox, 2 Quaternius) and their walk and idle clips | `Assets/ThirdParty/Rocketbox`, `Quaternius` | They evacuate. One becomes the fire warden, one goes missing | Rows exist |
| VR rig, XR hands, tablet, training board, training station, trainee avatar | `_Project/Art/Hands`, `Training`, M12 | The same rig and panels | Ours / rows exist |
| NavMesh walk area | `Cell/Training/WalkArea` (AI Navigation) | NPC evacuation paths | Row exists |

### To make (no register row)

| Asset | Notes |
|---|---|
| **Portable extinguishers**: 5 lb ABC dry chemical, 5 lb CO2 (horn), 2.5 gal water | Interactive parts: pin with tamper seal, carry handle and lever, hose or horn, pressure gauge (needle in the green, or in the red for faulty units), inspection tag, label with class pictograms. Highest priority; model in Blender like `LOTO_LockTag`. No vendor branding. |
| Extinguisher wall bracket, cabinet and column marker | Mounting heights per NFPA 10; red location marker and sign |
| Manual fire alarm box (pull station) | US red pull station. ISO 7010 F005 call-point variant if needed |
| Horn/strobe, smoke detector, heat detector | Simple ceiling and wall appliances; the strobe is an emissive flash |
| Exit signs (lit "EXIT", arrow variants), "NOT AN EXIT", assembly point sign, extinguisher sign | We draw the graphics ourselves. "EXIT" lettering per 1910.37(b)(7); pictograms in ISO 7010 style |
| "You are here" evacuation map | Generated from our own layout |
| Emergency lighting unit (twin spot) | Lights come on when the hall lights dim in F4 |
| Panic bar for the exit door | Added onto the Factory Training door |
| Self-closing oily-waste can, open bin, oily rags, flammables cabinet, red safety can, daisy-chained extension leads, door wedge | Hazard hunt props |
| Smoke layer | Our own few-quad ceiling layer that banks down. Cheap for VR (no volumetrics) |
| Scorch decal, charred cardboard | URP decal (Deferred+ decals are already on) |
| Fire warden hi-vis vest and clipboard | A texture variant on one Rocketbox worker (MIT allows changes; note it in that row's "Changed by us") |
| **Alarm tone** | The temporal-3 evacuation pattern (NFPA 72), synthesised in code, so no file and no licence |

### To source (approval needed; register row when added)

| Asset | Candidate | Licence | Note |
|---|---|---|---|
| Fire crackle loop, extinguisher discharge hiss (dry chem, CO2), water spray | Freesound, CC0 clips only (like the fimrod and kev_durr rows) | CC0 1.0 | Pick clips and confirm the licence per clip before import. Row per clip |
| Yard ground for the outside assembly point | ambientCG asphalt or concrete | CC0 1.0 | Already on the register as "not in repo". Update the row when it is used |
| ISO 7010 pictogram source art (only if we don't draw our own) | Wikimedia Commons ISO 7010 set | Per file; many are marked public domain | Check each file's licence. If any is unclear, draw our own instead (the default) |
| Extinguisher model (fallback only) | A CC0 library model, if one with interactive parts exists | Check | Not confirmed. Making our own is the plan |

---

## 5. Learning phases for the trainee

Each phase is one board selection. Phases 1–3 are guided, with the step due on the tablet and hints on. Phase 4 is unguided. Every run writes a record.

| Phase | Name | Content | Objectives | Pass mark (demo) |
|---|---|---|---|---|
| 1 | **Awareness and prevention** | Briefing at the training board: hazards, classes, policy mode. Hazard hunt H: 5 of 7 seeded hazards in 3 minutes. Locate both exits, the call point, the nearest extinguisher of each class and the assembly point. The F1 precursor appears on the dashboard and gets reported. | O1–O4 | 5/7 hazards, all four locations, precursor reported |
| 2 | **Extinguisher use** | Pre-use check (one unit is faulty). Choose by class. PASS on F3 (A), then F1 (C). Stand-off, sweep, back away, reflash. Wrong-agent and too-close outcomes are shown, not just scored. | O5–O8 | Fire out, no critical mistake, ≤ 1 minor |
| 3 | **Alarm and evacuation** | Alarm first. Line stop if assigned. F4: the fire grows and the trainee flees. F5: the primary route is blocked. Smoke layer (stay low). Assembly point, roll call, one colleague missing. | O8–O12 | At assembly within the target time, alarm before anything else, missing person reported, no re-entry |
| 4 | **Assessment** | One seeded random scenario (F1, F2 or F3, with a chance of F4, F5 or F6), unguided, no hints, timed. Debrief on the board: steps, mistakes by clause, metrics. | O13 | All required steps, no critical mistakes |

**Critical mistakes** (fail the run): water on a C or B fire; fighting before raising the alarm; fighting past the incipient stage; turning your back on the fire; re-entering after evacuating; in evacuate-only mode, picking up an extinguisher. **Minor mistakes**: wrong order, stand-off out of range, aiming at the flames rather than the base, not watching for reflash, door left open, hazard reported late.

Annual refresher (1910.157(g)(2)): phase 4 alone, with phases 2–3 offered only if it fails.

---

## 6. Session flow and training record (built for recording)

A complete session is one continuous take of about 8–12 minutes. Every beat has a clear start event, which `TakeEventLog` marks so that the cut can find it.

| Beat | What happens | Start marker |
|---|---|---|
| 0 Briefing | Training board: trainee "Sam Rivera", policy mode, scenario, seed. The record opens | `session_start` |
| 1 Walk-down | Hazard hunt and locating exits, call point, extinguishers, assembly point | `hunt_start` |
| 2 Precursor | Cabinet temperature climbs on the dashboard; the trainee reports it (or misses it) | `precursor` |
| 3 Ignition | Smoke, then detector, then a small flame (stage 1 → 2) | `ignition` |
| 4 Alarm | Call point pulled; horn and strobe; the line holds; the NPCs start to leave | `alarm` |
| 5 Fight or flee | Pre-use check, PASS and sweep, or abandon when the fire passes incipient | `fight` / `flee` |
| 6 Evacuate | Route taken, blocked route, doors, smoke layer | `evacuate` |
| 7 Roll call | The warden counts heads; one missing; reported; no re-entry | `roll_call` |
| 8 Debrief | The board shows the record: steps, mistakes by clause, metrics | `debrief` |

**Training record.** Same pattern and folder as Rev 5: `Builds/Training/fire_<trainee>_<utc>.csv` (git-ignored), written by the same writer, with the same header as the lockout record so one viewer reads both:

```
trainee,trainer,started_utc,at_s,step,regulation,kind,detail
```

`kind` is `done`, `mistake`, `critical`, `metric` or `result`. Metric rows: time to alarm, time to first discharge, agent used, discharge used (%), stand-off held (%), time to assembly, route taken, hazards found, precursor reported. The last row is the result plus `hands_on_discharge=yes|no|n/a`, which the trainer sets for designated employees (section 1).

**Signals.** New station `fire` under the existing scheme `cell/<station>/<signal>`, so the topic scheme itself is unchanged. Numbers only, as with all our payloads.

| Topic | Values |
|---|---|
| `cell/fire/scenario` (in) | scenario id |
| `cell/fire/stage` | 0 none, 1 smoke, 2 incipient, 3 growing, 4 beyond incipient, 5 out |
| `cell/fire/size` | 0–1 |
| `cell/fire/class` | 1 A, 2 B, 3 C |
| `cell/fire/location` | zone id |
| `cell/fire/smoke_detected` | 0/1 |
| `cell/fire/alarm` (in) | 0/1, from the call point or the detector |
| `cell/fire/agent`, `cell/fire/discharge`, `cell/fire/charge` | 1 water, 2 ABC, 3 CO2 · 0/1 · 0–1 |
| `cell/fire/evacuated`, `cell/fire/missing` | head counts |
| `cell/cabinet/temp_c` | the F1 precursor |
| `cell/training/step`, `errors`, `result` | reused from M11 |

The line holds on `cell/fire/alarm`, the way it holds on a CNC fault. The fire drills run on the in-Unity sequencer. Nothing here involves the soft PLC.

---

## 7. Build phases and milestones (Rev 6)

Every milestone runs on desktop and in the Quest 3 over Link from the start, because the M12 rig exists. Pure models get EditMode tests, as `LockoutProcedure` does. Days are rough estimates of focused time.

| Milestone | Scope | Exit | Days |
|---|---|---|---|
| **M14 Fire model and effects** | Pure, tested `FireModel` (stage growth, suppression by agent × class × aim × stand-off, reflash) with parameters in a ScriptableObject. Scenario assets F1–F6. `FireEffects` drives the Particle Pack flames, smoke and sparks from signals. Smoke and heat detectors; the alarm holds the line; a fire panel on the dashboard. | Each scenario ignites and grows through its stages on schedule; a detector raises the alarm and the line holds; the dashboard shows stage and location. The largest fire plus smoke holds 72 fps over Link. Tests pass. | 3 |
| **M15 Extinguisher and hazard hunt** | The three extinguishers modelled and rigged. Pre-use check. PASS by hand (pull pin, aim, squeeze, sweep) with XR Hands and controllers, and mouse on desktop. Hazard spots for H. | A faulty unit is caught in the check. The right class with PASS puts an incipient fire out within one charge. The wrong agent makes it worse and is logged as critical. Stopping early leads to reflash. The hunt logs each find with its clause. Desktop and headset both pass. | 3.5 |
| **M16 Alarm and evacuation** | Pull station, temporal-3 horn and strobe, exit signs, emergency lighting, two routes plus a blocker, smoke layer with a stay-low check (head height), NPC evacuation on the NavMesh, warden and roll call at the assembly point, re-entry detection. | The alarm sounds and flashes; the NPCs reach the assembly point; the blocked route forces the alternate; the roll call reports one missing; re-entry is logged as critical. | 3 |
| **M17 Assessment and record** | `FireDrillProcedure` (pure, tested, like `LockoutProcedure`) for phases 1–4, the policy modes, the scoring, the board pages for phase select and debrief, and the CSV record. | A fight session and a flee session run end to end unguided in the headset and on desktop. Each writes a record by clause, and the two records match. A seeded wrong-order run records its mistakes. Switching the policy mode changes the required steps. | 2.5 |
| **M18 Fire capture** | See section 8. | The clip is captured from the project and approved. | 2 |

About 14 days. **Decide at start of M14:** the default policy mode (suggested: all employees), US-only or US + ISO 7010 signs, and where the outside assembly point sits (the yard beyond the loading gate).

**VR budget risk.** Particle overdraw is the main cost on the VR pipeline. Cap particle counts, turn soft particles off in VR, and keep the smoke layer to a few large quads. Check 72 fps at every exit, not only at M17.

---

## 8. Capture plan (M18, in the style of M9 and M13)

**Part 4 of the series.** Final naming `2DS_Manufacturing_FactoryTwin_Fire_v01_<date>`, in `Builds/Recordings/` (git-ignored), with YouTube, Shorts and LinkedIn cuts like the `v06 kit/`.

- **Promise:** practise the fire you hope never happens, on the line you actually run.
- **Proof:** the twin flags the overheating cabinet before the smoke. The alarm holds the line, and the right extinguisher with PASS puts the fire out. In a second run the fire grows: the trainee backs out, finds the route blocked, takes the other one, and the roll call finds one missing. Every step lands in the record by clause.
- **Path:** your cell's twin, your emergency action plan and fire prevention plan, Quest 3 or desktop.
- **Ask:** book a call.

**Takes.** Each is a complete recorded session, not a stitched beat:

| Take | Content | Source |
|---|---|---|
| A | Full session, phase 4, F1 fight → passed | Headset (mirror view) |
| B | Full session, F2 → F4 flee → F5 blocked route → roll call | Headset (mirror view) |
| C | Same seed as A on desktop (record comparison) | Desktop trainee |
| D | Dashboard close-ups: precursor, fire panel, line held, head count | Cinemachine shots via `TakeSequencer` |
| E | Debrief board and the CSV record | Desktop |

**Recording rules** (from M9 and M13):
- Recorded through `TakeSequencer`. `TakeEventLog` writes the beat markers from section 6, and `TakeAudioRecorder` captures game audio, so alarm, crackle and spray sync for the cut.
- Desktop holds the 60 fps lock, and VR runs at 72 fps over Link.
- No UnitySkills calls during a real-time take (they stall Play for about 0.5 s).
- The record for each take is kept next to the video, named with the same session id.
- The alarm is levelled down in the mix. The horn is mastered, not clipped, and ducked under the voice.
- Trainee name "Sam Rivera" only.
- Voiceover script in `docs/voiceover_fire.md`; cut script in `tools/video/fire/` (same tooling as `tools/video/training/`). The narration voice is the series explainer voice; audio renders and music stay out of git.
- On screen and in the voice: "a training demo, not certified training", and "hands-on discharge still required for designated employees".

---

## 9. Open decisions

1. ~~Approve Rev 6 (M14–M18, about 14 days) as scoped here.~~ Approved 2026-11-04 (you).
2. ~~Default policy mode~~ **All employees** (2026-11-04).
3. ~~Signage~~ **US + ISO 7010**: lit "EXIT" lettering plus ISO 7010-style pictograms, drawn by us (2026-11-04).
4. Sourcing: the Freesound CC0 clips (per clip) and the ambientCG yard ground. Each still gets your approval at import.
5. ~~F6~~ **Kept in Rev 6**. Assembly point: **the yard past the loading gate** (2026-11-04).

## Sources

- 29 CFR 1910.38, 1910.39, 1910.155, 1910.157, 1910.165, 1910.36–37 (eCFR / osha.gov).
- OSHA standard interpretation, 2 Jul 1991, hands-on extinguisher training need not be live fire: https://www.osha.gov/laws-regs/standardinterpretations/1991-07-02-0
- OSHA Evacuation Plans and Procedures eTool, portable extinguishers, "fight or flee".
- NFPA 10, NFPA 101, NFPA 72, NFPA 80 (current editions; section numbers as cited above, check against the edition the client's AHJ uses).
- NFPA, Fires in U.S. Industrial and Manufacturing Facilities (2011–2015 figures via https://www.powderbulksolids.com/industrial-fires-explosions/nfpa-releases-new-data-on-manufacturing-property-fires and https://www.jm.com/en/blog/2017/february/fires-in-industrial-or-manufacturing-properties/; verify against the report).
- UK: Regulatory Reform (Fire Safety) Order 2005; BS 5306-8; ISO 7010.
