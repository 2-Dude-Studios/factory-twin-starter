# Electrical safety training — use case 5 (Rev 7, approved 2026-11-04)

A fifth use case for the factory twin: electrical safety and arc flash, practised in VR on the same cell, crew and dashboard as Revs 5 and 6. Quest 3 over Link and desktop, reusing the M12 rig. It builds on two things the cell already has: the M11 lockout on the CNC, and Rev 6's drive cabinet, whose temperature the dashboard already shows (`cell/cabinet/temp_c`).

**What this is not.** A training demo, not certified or "compliant" training, and not a substitute for the employer's electrical safety program, its arc flash risk assessment or hands-on training. It does not qualify anyone: under NFPA 70E the employer decides who is qualified, task by task. The arc flash label values in the demo come from a made-up study, not an engineering one. Section 1 is written as an auditor's guidance. It is not legal advice. The employer and the authority having jurisdiction decide what applies to a real plant.

Contents: 1 Auditor's view · 2 Scenarios · 3 Training objectives by requirement · 4 3D and audio assets · 5 Trainee phases · 6 Session flow and training record · 7 Build phases and milestones · 8 Capture plan · 9 Open decisions · Sources

---

## 1. Auditor's view: what VR can train, and what it can't

I audit electrical work in a cell like this one against OSHA Subpart S first, then NFPA 70E, which OSHA does not adopt by reference but cites as the recognised practice (general duty clause, and as evidence of what a reasonable employer knows).

- **29 CFR 1910.332, training.** Anyone facing a risk of electric shock that the installation's own safeguards don't reduce to a safe level must be trained ((a)). Unqualified persons are trained in the safety practices their job needs ((b)(2)). Qualified persons must also be able to tell exposed live parts from other parts, determine nominal voltage, and know the approach distances in 1910.333(c) ((b)(3)). The training may be classroom or on-the-job ((c)). **VR fits the classroom part well.** A trainee can read the plant's own label, find the boundaries on the floor and pick PPE as many times as needed.
- **29 CFR 1910.333, work practices.** Live parts are de-energised before anyone works on or near them, unless that adds hazards or is infeasible ((a)(1)). Electrical lockout is (b)(2). Note 2 is the key line for this cell: **a 1910.147 lockout procedure, like our M11 one, also satisfies (b)(2), but only if it covers the electrical hazards and adds two steps.** (b)(2)(iii)(D): release stored electric energy (capacitors discharged, high-capacitance parts shorted and grounded). (b)(2)(iv)(B): a qualified person tests the parts with test equipment and verifies they are de-energised, including against induced voltage and backfeed from an unrelated source; above 600 V the tester is also checked before and after the test. Only qualified persons work on parts that are not de-energised ((c)(2)). Also note that 1910.147 itself excludes work on conductors ((a)(1)(ii)(B)): opening the drive cabinet is Subpart S work, not CNC servicing. **This is where VR fits best.** The trainee meets the backfeed, the charged DC bus and the dead tester, which can't be staged on a live panel.
- **29 CFR 1910.335, PPE and alerting.** Employees use electrical PPE suited to the parts they work on ((a)(1)), insulated tools near live parts ((a)(2)), and barricades, signs or an attendant to keep others out ((b)). **29 CFR 1910.137** covers rubber insulating gloves: inspected before each day's use, with an air test ((c)(2)). **VR covers the inspection routine**, but not the feel of a real glove.
- **Supporting rules.** 1910.303(g)(1): clear working space in front of panels, never used for storage. 1910.305(b)(1): unused openings in cabinets closed. 1910.305(g)(1)(iv)(A): flexible cords are not a substitute for fixed wiring. 1910.334(a)(2): portable cord-and-plug equipment inspected before use each shift. These feed the hazard hunt.
- **NFPA 70E, 2027 edition** (in effect since 6 May 2026; it replaces the 2024 edition). It covers training with retraining at intervals of no more than three years, and documented training (110.4, and 105.3 as reported for 2027). Contact-release training is refreshed every year (110.4(C)). It covers establishing an electrically safe work condition (Article 120, the eight-step process in 120.6 of the 2024 edition: sources, disconnect, visual check, stored energy, lockout, absence-of-voltage test with the tester proved on a known source before and after, grounding where needed). The energised electrical work permit is 130.2, and 2027 adds a second person, trained in contact release, whenever the permit calls for shock or arc flash PPE (130.2(A)(2)). The shock risk assessment and the limited and restricted approach boundaries are 130.4, with Table 130.4(E)(a) for AC. The arc flash risk assessment, boundary and equipment label are 130.5 and 130.5(H). PPE categories are Table 130.7(C)(15)(c), new thermal hand protection is 130.7(C)(7)(e), and the condition of maintenance is 205.4. Approach boundaries and PPE categories did not change in 2027. **Section numbers here follow the 2024 text plus the 2027 changes reported by secondary sources. Check every number against the 2027 text (NFPA's free read-only access) before anything goes on screen. That check is an M19 start task.**
- **Outside the US**, only if a prospect asks. UK: Electricity at Work Regulations 1989 (reg. 12 isolation, reg. 13 precautions on dead equipment, reg. 14 live work, reg. 16 competence) and HSE guidance HSG85 and GS38 (test equipment). EU: EN 50110-1 and its five safety rules. Parked under Later.

**Why it sells.** Labels: [R] regulator, [P/A] practitioner/analyst, [V] vendor.
- BLS census data via NFPA: 147 US workers died from exposure to electricity or electric arcs in 2023, matching the 2014–2023 average of about 147 a year. The trend has been flat for a decade after falling in the decade before. [P/A]
- ESFI from BLS and OSHA data: 2,070 workplace electrical deaths from 2011 to 2024, about 5.6 % of all workplace deaths. In 2011–2023, 74 % of them were in non-electrical occupations, people who may never have had electrical safety training. [P/A] That is the case for phase 1 (unqualified awareness) as much as for the qualified phases. Check both sources before putting a figure on screen.
- 70E retraining is due at least every three years and contact-release training every year, so this is a recurring need, like the annual extinguisher training in Rev 6. The 2027 edition's documented-training and second-person rules give plants a reason to revisit their programs now.
- **Competitors are real and cover the basics.** Several VR vendors already sell arc flash and NFPA 70E modules with approach boundaries, PPE selection, lockout and the live-dead-live test. We do not compete on those steps alone. **The difference is the twin.** The fault that sends the trainee to the cabinet comes from the line's own signals. The dashboard shows the DC bus voltage falling, and the trainee still has to test, because a dashboard is not a test instrument. The backfeed comes from the cell's own control-power feed. The soft PLC refuses to start the line while the cabinet lock is on. The label is generated from the plant's own study data. The pitch line: **"The twin tells you where to look. Only your tester tells you it's dead."**

**Role modes the module supports** (set per client in the scenario asset):

| Mode | Rule | What the trainee must do |
|---|---|---|
| Unqualified (affected employee) | 1910.332(b)(2); 70E 110.4(A)(2) | Hazard hunt, read the label, stay outside the limited approach boundary, report, keep others out. Opening the cabinet is logged as a mistake. |
| Qualified (default for the demo) | 1910.332(b)(3); 1910.333(b)(2); 70E 110.4(A)(1), Article 120 | Above, plus establish an electrically safe work condition and do the task. |
| Qualified with energised diagnostics | 70E 130.2 exemptions for testing and troubleshooting; 130.4, 130.7 | As qualified, plus a voltage measurement on a live circuit in full PPE, with the second person present (2027 rule). |

---

## 2. Realistic electrical scenarios in this cell

The cell's drive cabinet (Rev 6's F1 location) is a US 480 V three-phase panel. It holds a main disconnect, the two conveyor VFDs (each with a DC bus of about 650 V), the robot controller feed and a 120 V control transformer. The cabinet's control circuit also takes a separate 120 V feed from a hall panel through a UPS, so the PLC and HMI ride through a power cut. That feed is the backfeed in E2, and it is a common real-world trap. The CNC keeps its own isolator from M11.

| # | Scenario | Correct response | The twin's part |
|---|---|---|---|
| E1 | **Replace a failed infeed VFD** (the main task) | Plan (drawings, label, PPE). Stop the line, open the disconnect, check visually that it is open, apply lock and tag, wait out the VFD discharge time, prove the tester, test all three phases phase-to-phase and phase-to-ground plus the DC bus, prove the tester again, then work. Restore by the M11 release steps. | `cell/infeed/drive_fault` stops the line and the dashboard points to the cabinet. The disconnect and lock are signals, and the PLC refuses to start with the lock on. |
| E2 | **Backfeed** (seeded; the main lesson) | The disconnect is open and the dashboard says 0 V, but the absence-of-voltage test finds 120 V on the control terminals. Stop, check the drawings, find the UPS feed, open it and lock it, then test again. | The control feed is a second source in the model. The dashboard's "0 V" is the trap: it reads only the main bus. |
| E3 | **Stored energy** | Right after isolation the DC bus test reads hundreds of volts. Wait the label's discharge time (shortened for the demo), test again, and never short the bus with a tool. | `cell/cabinet/dc_bus_v` decays on the dashboard in real time. |
| E4 | **Dead tester** | The proving unit fails the tester on the after-check (flat battery). The test before it is void: replace the tester, prove it, test again, prove it again. | None: this one is in the trainee's hands. That is the point. |
| E5 | **Unqualified colleague** | An NPC walks toward the open cabinet. Stop them at the limited approach boundary, set barrier tape or an attendant, and escort or turn them away. | Boundary zones on the floor, like M10's robot zones; crossings are logged. |
| E6 | **Contact release** (advanced; see the open decisions) | A colleague is in contact with a live part. Don't touch them. Open the source if it is in reach (the disconnect, not the E-stop), or use the insulated rescue hook. Raise the alarm, start CPR, fetch the AED. | Alarm and line hold reuse Rev 6's panel. |
| H | **Pre-shift electrical hazard hunt** | Find and report: pallet in the panel's working space, cabinet door unlatched, open knockout, faded or missing arc flash label, extension cord as permanent wiring, damaged cord on a hand tool, daisy-chained power strips, gloves past their test date. | Each find is logged against its clause. |

Left out on purpose: batteries, PV and DC systems (2027 Chapter 3), overhead lines, and medium voltage above 1,000 V. They don't fit this cell, so they go under Later in the build plan.

---

## 3. Training objectives, each tied to its requirement

| # | The trainee can… | Requirement | Phase |
|---|---|---|---|
| O1 | Spot and report electrical housekeeping hazards (working space, openings, cords, labels) | 1910.303(g)(1); 1910.305(b)(1), (g)(1)(iv)(A); 1910.334(a)(2) | 1 |
| O2 | Read an arc flash label: nominal voltage, arc flash boundary, incident energy or PPE category, limited and restricted approach boundaries | 70E 130.5(H), 130.4 | 1 |
| O3 | Stay outside the limited approach boundary when unqualified, and keep others out with tape, signs or an attendant | 1910.335(b); 70E 130.4, 130.8 | 1 |
| O4 | Explain why work is done de-energised, and when energised work is justified | 1910.333(a)(1); 70E 130.2 | 2 |
| O5 | Pick the PPE the label calls for, inspect and air-test rubber gloves, reject a faulty glove | 1910.335(a)(1); 1910.137(c)(2); 70E 130.7 | 2 |
| O6 | Pick a tester rated for the circuit and prove it on a known source before and after each test | 1910.333(b)(2)(iv)(B); 70E 120.6(7) (2024 numbering) | 2 |
| O7 | Establish an electrically safe work condition in order: sources, disconnect, visual check, stored energy, lock and tag, test, ground if needed | 1910.333(b)(2); 70E 120.6 | 3 |
| O8 | Test every phase phase-to-phase and phase-to-ground, plus the DC bus, and treat a reading as live until proved otherwise | 1910.333(b)(2)(iv)(B); 70E 120.6(7) | 3 |
| O9 | Recognise backfeed from a second source, isolate it and test again | 1910.333(b)(2)(iv)(B) | 3 |
| O10 | Wait out stored energy and confirm it by test | 1910.333(b)(2)(iii)(D) | 3 |
| O11 | Restore power by the release steps: tools out, guards on, people clear, own lock off | 1910.333(b)(2)(v); 1910.147(e), reusing M11 | 3 |
| O12 | Respond to a person in contact with a live part without becoming a second casualty | 70E 110.4(C) (annual contact-release refresher) | 3–4 |
| O13 | Do all of the above unguided in a random scenario, with a record of every step and mistake | 70E 110.4 (retraining ≤ 3 years, documented) | 4 |

---

## 4. 3D and audio assets

Register key as in Rev 6: **Have** = already in the project. **Make** = we model or author it in Blender or in code (`tools/blender/`), so it needs no LICENSES.md row. **Source** = a third-party candidate, imported only after you approve it, with a LICENSES.md row in the same commit.

### Already in the project

| Asset | Where | Use | Register |
|---|---|---|---|
| Lock, tag, lock socket, turn handle | M11 props (`LOTO_LockTag`, `XrLockSocket`, `XrTurnHandle`) | Cabinet disconnect lockout, UPS feed lockout | Ours |
| Two-handed grab | `HandedGrabInteractable` (Rev 6 extinguishers) | Tester in one hand, probe in the other | Ours |
| Hazard hunt, scorer, badges, HUD, score popups | Rev 6 (`FireDrillScorer`, `BadgeBook`, `FireHud`, `ScorePopups`) | Generalised for the electrical hunt and scoring | Ours |
| Safety zones and floor marking | M10 (`SafetyZone`, `ZoneMarking`) | Arc flash, limited and restricted approach boundaries | Ours |
| Barrier, cones, walkway fence | Factory Training | Barricading (O3) | Row exists |
| Electric sparks sound, factory ambience | `Assets/_Project/Audio` (Freesound CC0) | Arcing and background bed | Rows exist |
| NPC crew, training board, tablet, start menu | Rocketbox / Quaternius, M12, M15 | Colleague in E5 and E6; a fifth menu card | Rows exist / ours |
| Alarm panel and line hold | Rev 6 `FireAlarmPanel` | E6 raises the alarm | Ours |

realvirtual's ControlCabinet is a closed box scaled up by `FireSetup`, with no door or interior, and we can't change it. Rev 7 needs a cabinet that opens, so we make our own.

### To make (no register row)

| Asset | Notes |
|---|---|
| **Drive cabinet** with a hinged door, through-door rotary disconnect (lockable), two VFDs with DC bus LEDs, terminal strips (L1 L2 L3, control 120 V), control transformer, a UPS feed terminal, a drawing pocket | Highest priority. Probe-able points are named transforms the model reads, so a probe tip on L1 and L2 gives the L1–L2 reading. No vendor branding. Replaces the scaled realvirtual box at the same spot, so Rev 6's F1 still works. |
| **Voltage tester** (two-probe, CAT III 600 V marking, display) and **proving unit** | The tester shows readings from `ElectricalModel`. One unit has a flat battery for E4. |
| Rubber insulating gloves (Class 0) with leather protectors, arc-rated face shield and balaclava, arc-rated jacket, a PPE board or locker | Wearing PPE in VR swaps the hand material to the glove and tints the view edge for the face shield. Cheap, and it reads well on video. |
| Arc flash label and danger signs | Our own graphics in the ANSI Z535 style, filled from a ScriptableObject (`ArcFlashStudy`), with demo values. |
| Boundary floor rings | Decals at the arc flash and limited approach boundaries (URP decals are already on). |
| Insulated rescue hook, AED wall cabinet | For E6. |
| Hazard hunt props | Damaged cord, daisy-chained power strips, extension cord stapled along a wall, open knockout, glove bag with an expired date. |
| **Hum and arc sounds** | Transformer hum synthesised in code, like Rev 6's alarm tone, so it needs no file. |

### To source (approval needed; register row when added)

| Asset | Candidate | Licence | Note |
|---|---|---|---|
| Breaker or disconnect clunk, tester beep, arc flash bang | Freesound, CC0 clips only | CC0 1.0 | Pick and confirm per clip before import, one row per clip. The bang is used only in the debrief replay, never as a jump scare. |

---

## 5. Learning phases for the trainee

Each phase is one board selection. Phases 1–3 are guided, with the step due on the tablet and hints on. Phase 4 is unguided. Every run writes a record.

| Phase | Name | Content | Objectives | Pass mark (demo) |
|---|---|---|---|---|
| 1 | **Awareness** (unqualified) | Briefing at the board: shock and arc flash, who is qualified, the label. Hazard hunt H: 6 of 8 seeded hazards in 3 minutes. Read the cabinet's label and walk its boundaries. E5 as the bystander: stop a colleague, set the tape. | O1–O3 | 6/8 hazards, label read, no boundary crossed |
| 2 | **Gear and tester** | Why de-energise (O4). Pick PPE from the label. Glove inspection and air test, with one faulty glove. Choose a tester, prove it on the proving unit, measure the known source. E4: the after-check fails. | O4–O6 | Right PPE category, faulty glove rejected, live-dead-live done and the failed tester caught |
| 3 | **Electrically safe work condition** | E1 end to end, with E2 (backfeed) and E3 (stored energy) seeded in. Restore and restart, and the PLC refuses to start with the lock on. E6 contact release, if kept. | O7–O12 | Every test done in order, backfeed found and locked, no work before a proved test, no critical mistake |
| 4 | **Assessment** | One seeded random run (E1 with a chance of E2, E3, E4 or E5), unguided, no hints, timed. Debrief on the board: steps, mistakes by clause, metrics. | O13 | All required steps, no critical mistakes |

**Critical mistakes** (fail the run): crossing the restricted approach boundary near exposed live parts; opening the cabinet without the label's PPE; touching a conductor before a proved absence-of-voltage test; trusting the dashboard or HMI reading instead of testing; skipping the before or after tester check; carrying on after a reading that isn't zero; removing another person's lock; touching a person in contact with a live part. **Minor mistakes**: wrong order, label not read before approaching, no barricade, glove air test skipped (critical if the glove was the faulty one), phase-to-ground tests missed on one phase, drawings not checked.

Retraining (70E, at least every three years): phase 4 alone, with phases 2–3 offered only if it fails. The contact-release refresher (E6) is offered every year.

---

## 6. Session flow and training record (built for recording)

A complete session is one continuous take of about 8–12 minutes. Every beat has a clear start event, which `TakeEventLog` marks so that the cut can find it.

| Beat | What happens | Start marker |
|---|---|---|
| 0 Briefing | Training board: trainee "Sam Rivera", role mode, scenario, seed. The record opens | `session_start` |
| 1 Walk-down | Hazard hunt, label, boundaries | `hunt_start` |
| 2 Fault | The infeed drive faults; the line stops; the dashboard points to the cabinet | `drive_fault` |
| 3 Plan and gear | Drawings, label, PPE, glove check, tester proved | `gear_up` |
| 4 Isolate | Disconnect open, visual check, lock and tag | `isolate` |
| 5 Test | DC bus wait, live-dead-live, every phase; backfeed found, second source locked, test again | `test` / `backfeed` |
| 6 Work | VFD swap | `work` |
| 7 Restore | Release steps; the PLC refuses to start with the lock on, then the line restarts | `restore` |
| 8 Debrief | The board shows the record: steps, mistakes by clause, metrics | `debrief` |

**Training record.** Same pattern, folder and header as the lockout and fire records, so one viewer reads all three: `Builds/Training/elec_<trainee>_<utc>.csv` (git-ignored).

```
trainee,trainer,started_utc,at_s,step,regulation,kind,detail
```

`kind` is `done`, `mistake`, `critical`, `metric`, `badge` or `result`. Metric rows: time to an electrically safe work condition, tests done (of 8: three phase-to-phase, three phase-to-ground, and the DC bus of each VFD), tester proved before and after, boundary crossings by type, PPE category chosen against the label, discharge wait respected, backfeed found. The last row is the result plus `hands_on_verified=yes|no|n/a`, which the trainer sets after a real tester exercise on a training board (section 1: VR does not replace it).

**Signals.** The existing `cabinet` station gets more signals, and a new `elec` station covers the person. Both fit the existing scheme `cell/<station>/<signal>`, so the topic scheme itself is unchanged. Numbers only, as with all our payloads.

| Topic | Values |
|---|---|
| `cell/elec/scenario` (in) | scenario id |
| `cell/cabinet/door_open` | 0/1 |
| `cell/cabinet/disconnect` | 0 closed (on), 1 open (off) |
| `cell/cabinet/locked`, `cell/cabinet/ups_locked` | lock counts on each source |
| `cell/cabinet/bus_v` | main AC bus, what the dashboard sees |
| `cell/cabinet/dc_bus_v` | VFD DC bus, decaying after isolation |
| `cell/cabinet/control_v` | 120 V control, live from the UPS in E2 |
| `cell/infeed/drive_fault` | 0/1, the fault that starts E1 |
| `cell/elec/boundary` | 0 outside, 1 arc flash, 2 limited, 3 restricted (the trainee's deepest point) |
| `cell/elec/ppe_cat` | PPE category worn, 0–4 |
| `cell/elec/tester_ok` | 0/1, as last proved |
| `cell/training/step`, `errors`, `result` | reused from M11 |

The trainee's tester readings are **not** signals. They come from `ElectricalModel` straight to the tester display, so the dashboard can never show the trainee the answer. The cabinet lock is an input to the soft PLC and to the in-Unity sequencer, like M11's CNC lock, so the line refuses to start with it on under either logic source.

---

## 7. Build phases and milestones (Rev 7)

Every milestone runs on desktop and in the Quest 3 over Link from the start. Pure models get EditMode tests, as `LockoutProcedure` and `FireModel` do. Days are rough estimates of focused time. Rev 7 starts after M18.

| Milestone | Scope | Exit | Days |
|---|---|---|---|
| **M19 Electrical model and cabinet** | Pure, tested `ElectricalModel`: sources (main, UPS), disconnect, lock counts, AC legs, DC bus decay, readings between any two named points, tester health. `ArcFlashStudy` ScriptableObject (demo values) and the label made from it. Our own drive cabinet in place of the scaled box, keeping F1. Boundary zones from the M10 pattern. New signals, and an electrical panel on the dashboard. Check the 70E-2027 section numbers. | Opening the disconnect drops the AC legs to 0 except the E2 control circuit. The DC bus decays on schedule. Every probe pair reads right in tests. Head and hand entry into each boundary is detected. The dashboard shows bus, DC bus and lock state. 72 fps over Link with the cabinet open. Tests pass. | 3 |
| **M20 Tester, PPE and gloves** | Two-handed tester with probes (XR Hands and controllers, mouse on desktop), proving unit, flat-battery tester. PPE board, wearing PPE (glove material, face-shield tint), glove inspection and air test with one faulty glove. | Probing L1–L2 on a live bus reads 480 V and on a dead one reads 0. The proving unit catches the flat tester. The wrong PPE category is logged against the label. A faulty glove is caught. Desktop and headset both pass. | 3 |
| **M21 Safe work procedure and hunt** | Pure, tested `ElectricalSafeWorkProcedure`: the Article 120 sequence plus 1910.333's two added steps, with the M11 release steps reused. Scenarios E1–E5. Electrical hazard hunt H. Cabinet lock into the soft PLC and the sequencer. | E1 runs end to end. E2's backfeed is found only by testing and cleared by locking the UPS feed. E3 forces the wait. E4 voids the test. E5's colleague is stopped at the limited approach boundary. The PLC refuses to start with the lock on. Each hunt find is logged with its clause. | 3 |
| **M22 Assessment, rescue and record** | Phase 4, the role modes, scoring by clause, board pages for phase select and debrief, the CSV record, a fifth card on the start menu, and E6 contact release if kept. | A qualified session and an unqualified session run end to end unguided, in the headset and on desktop. Each writes a record by clause, and the two records match. A seeded wrong-order run records its mistakes. Switching the role mode changes the required steps. | 2.5 |
| **M23 Electrical capture** | See section 8. | The clip is captured from the project and approved. | 2 |

About 13.5 days. **Decided 2026-11-04:** qualified by default, our own cabinet, E6 in training only, 480 V with demo label values, the cabinet lock into the soft PLC (section 9). **At start of M19:** check the 70E-2027 section numbers.

**VR budget risk.** The cabinet interior is many small parts seen close up. Keep it to one material atlas and few draw calls, bake its lighting, and check 72 fps with the door open and the trainee's head inside the arc flash boundary at every exit, not only at M22.

---

## 8. Capture plan (M23, in the style of M9, M13 and M18)

**Part 5 of the series.** Final naming `2DS_Manufacturing_FactoryTwin_Electrical_v01_<date>`, in `Builds/Recordings/` (git-ignored), with YouTube, Shorts and LinkedIn cuts like the `v06 kit/`.

- **Promise:** the twin tells you where to look. Only your tester tells you it's dead.
- **Proof:** the infeed drive faults and the dashboard sends the trainee to the cabinet. The label sets the PPE and the boundaries. The disconnect is opened and locked, and the dashboard reads 0 V. The trainee proves the tester and tests anyway, and finds 120 V from the UPS feed. They lock the second source, test again, prove the tester again, and only then touch it. On restore, the PLC refuses to start with the lock on. In a second take an unqualified colleague is stopped at the boundary. Every step lands in the record by clause.
- **Path:** your cell's twin, your one-line drawings and arc flash study, your PLC, your procedure; Quest 3 or desktop.
- **Ask:** book a call.

**Takes.** Each is a complete recorded session, not a stitched beat:

| Take | Content | Source |
|---|---|---|
| A | Full session, phase 4, E1 with E2 backfeed → passed | Headset (mirror view) |
| B | Phase 1, E5: the bystander stops a colleague and sets the tape | Headset (mirror view) |
| C | Same seed as A on desktop (record comparison) | Desktop trainee |
| D | Dashboard close-ups: drive fault, bus at 0 V, DC bus decaying, lock on, PLC refusal | Cinemachine shots via `TakeSequencer` |
| E | Debrief board and the CSV record | Desktop |

**Recording rules** (from M9, M13 and M18):
- Recorded through `TakeSequencer`. `TakeEventLog` writes the beat markers from section 6, and `TakeAudioRecorder` captures game audio.
- Desktop holds the 60 fps lock, and VR runs at 72 fps over Link.
- No UnitySkills calls during a real-time take (they stall Play for about 0.5 s).
- The record for each take is kept next to the video, named with the same session id.
- Trainee name "Sam Rivera" only.
- Voiceover script in `docs/voiceover_electrical.md`; cut script in `tools/video/electrical/`. The narration voice is the series explainer voice. Audio renders and music stay out of git.
- On screen and in the voice: "a training demo, not certified training", "label values are for the demo", and "hands-on tester practice still required".
- No injury shown. E6, if kept, stays out of the clip unless you decide otherwise.

---

## 9. Open decisions

1. ~~Approve Rev 7 (M19–M23, about 13.5 days) as scoped here, to start after M18.~~ Approved 2026-11-04 (you), with the suggested defaults below.
2. ~~Default role mode~~ **Qualified**, with phase 1 covering the unqualified level (2026-11-04).
3. ~~The cabinet~~ **Our own Blender cabinet** in place of realvirtual's scaled box (2026-11-04).
4. ~~E6 contact release~~ **In training only**, kept out of the clip (2026-11-04).
5. ~~Electrical system~~ **US 480 V three-phase with 120 V control**, label values from a made-up study and marked as such (2026-11-04).
6. ~~The cabinet lock as a soft PLC input~~ **Yes**, the same as M11's CNC lock: the Structured Text program and the I/O list change, outside Unity (2026-11-04).
7. Sourcing: the Freesound CC0 clips (clunk, beep, bang). Each still gets your approval at import.
8. At M19 start: check every NFPA 70E section number against the 2027 text.

## Sources

- 29 CFR 1910.137, 1910.147(a)(1)(ii)(B), 1910.303(g), 1910.305, 1910.332, 1910.333 (including Note 2 to (b)(2)), 1910.334, 1910.335 (eCFR / osha.gov): https://www.osha.gov/laws-regs/regulations/standardnumber/1910/1910.333
- NFPA 70E, 2027 edition (in effect 6 May 2026), with the 2024 text for section numbers not reported as changed. 2027 changes via https://www.herzigengineering.com/nfpa-70e-2027 and https://resources.duralabel.com/articles/nfpa-70e-arc-flash; check against the 2027 text.
- NFPA Research, Fatal Occupational Injuries in 2023 Caused by Exposure to Electricity or Arc Flash (1 Aug 2025): https://www.nfpa.org/education-and-research/research/nfpa-research/fire-statistical-reports/fatal-occupational-injuries-caused-by-exposure-to-electricity-or-arc-flash
- ESFI, Workplace Injury & Fatality Statistics 2011–2024: https://www.esfi.org/workplace-safety/workplace-injury-fatality-statistics/
- Competitor scan (2026-11-04): VR arc flash and 70E modules from several vendors, for example https://dronavr.com/us/arc-flash-training-simulator/ and https://www.culenz.com/safety-training/electrical-arc-flash-safety-vr. All found cover boundaries, PPE and live-dead-live. None found runs on the plant's own twin, PLC and dashboard.
- UK, if asked: Electricity at Work Regulations 1989; HSE HSG85, GS38. EU: EN 50110-1.
