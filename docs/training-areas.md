# Training areas and drill activities (proposed 2026-11-11)

You asked (2026-11-11) for a dedicated area in the scene for each drill, and then for the drill activities, gamified and tied to the rules each drill trains to. This plan covers the four trainings: robot cell safety (M10), lockout (M11), fire (Rev 6) and electrical (Rev 7). Predictive maintenance and virtual commissioning are not drills and keep the cell as it is.

**What this is not.** These remain training demos. The scores, stars and badges are not certification and do not make anyone "compliant". The clause on each step says which rule the step practises; the employer's own program, its hands-on training and its records still decide who is trained. This is guidance, not legal advice.

Contents: 1 Why areas · 2 Principles · 3 The hall map · 4 The areas · 5 The shared game layer · 6 Activities per drill · 7 What changes in the roadmap · 8 Open decisions

---

## 1. Why areas

Today the cell carries most of the drills on one small footprint (−5…0.5, −1…3.9): the robot zones around the arm, the lockout points at the CNC's east end, the drive cabinet just south-east of the infeed, and three of the fire locations. Their floor markings and boards overlap: the cabinet's arc flash ring reaches the robot drill's start spot, and standing at the CNC door for the fire drill trips the robot zone (seen 2026-11-04). A trainee or a viewer cannot tell which drill a place belongs to. Each drill needs its own space with its own start, kit, briefing and score board.

## 2. Principles

1. **The hazard stays on the real machine.** The pitch of every drill is that it runs on the line's own twin: the arm's zones, the CNC's isolator, the cabinet's bus voltage, the fire's detectors. So an area is built around its machine, not around a mock-up. The one exception is the fire practice pan (section 4), which is a practice ground by design, as on real extinguisher courses.
2. **One area, one drill.** Each area has a floor boundary in its own colour, an entry with a sign, a briefing post, a kit station, a start spot, and a score board. Nothing from another drill sits inside it.
3. **Colours are for finding your way, not for safety.** Each area has an accent colour for its sign, board and floor border. Safety markings keep their usual meanings (red for fire equipment, yellow and black for keep-clear, the boundary rings in their own tints) and never take an area's colour.
4. **Same layout everywhere.** Every area reads the same way: entry and sign → briefing post → kit station → the machine → score board. Learn one, and the others are familiar.
5. **Desktop and headset start at the same spot**, facing the briefing post: the desktop trainee spawns there; the headset rig teleports there (`RigTeleport`).
6. **Built by a re-runnable tool**, like Build Fire Drill: `Tools > Factory Twin > Build Training Areas`. Positions live in one ScriptableObject (`TrainingAreas.asset`) so they can be moved in the Inspector without editing code.

## 3. The hall map

The hall is about 50 × 75 m (x −30.5 to 20, z −37.6 to 38, read from the open Editor 2026-11-11). Top view, north (+z) up. Positions are proposals: **please confirm them in the Scene view** (as you did for the fire training station), because the Editor survey gives the props' positions but not their footprints.

```
 z ▲
   │
  5│            ┌──── LOCKOUT (yellow) ────┐
   │            │ CNC (−0.3, 3.3)          │
   │  ┌ ROBOT (orange) ┐ control station   │
  1│  │ arm (−2.0, 1.2) │ board (1.8, 2.0) │      ┌ ELECTRICAL (blue) ┐
   │  │ zones           └──────────────────┘      │ cabinet DCP-1     │
 −1│  │ start (−2.0, −3.2)                         │ gear station      │
   │  └─────────────────┘                          │ (5…9, −4…1)       │
   │                                               └───────────────────┘
 −9│        HUB (−9, −9): arrivals, four signposts, badge wall
   │
−14│   ┌ FIRE (red) ───────────┐
   │   │ training station       │   fires across the hall stay where they are:
   │   │ (−4.8, −13.9)          │   rack (−4.6, −5.2), column panel (−1.6, −19.4),
   │   │ practice pan (−4.8, −17.5)│ drums (−12.3, −28.9), chargers (11.8, −22.6)
   │   └────────────────────────┘
   └──────────────────────────────────────────────────────────────► x
```

## 4. The areas

| Area | Colour | Footprint (proposed) | Start spot, facing | Briefing post | Kit station | The machine | Score board |
|---|---|---|---|---|---|---|---|
| **Hub** | white | about 6 × 6 m round (−9, −9) | (−9, −9), north | Map of the hall with the four areas | — | — | Badge wall: each trainee's stars and badges for all four drills, refresher due dates |
| **Robot cell safety** | orange | south and west of the arm, x −5…−0.5, z −4…−1, outside the warning zone | (−2.0, −3.2), north | South of the cell at (−3.2, −3.4) | E-stop and reset station (exists), a jam-clearing hook | The arm and its two zones (M10) | Beside the briefing post |
| **Lockout** | yellow | the CNC's east end and the line control station, x 0.5…4, z 1.5…5 | (2.6, 2.6), west | The existing training board at (1.8, 2.0) | A lock board: personal lock, tag, hasp, try-start card (M11 props) | The CNC (M11) | On the board's second page |
| **Electrical** | blue | its own bay east of the cell, x 5…9, z −4…1 | (7.0, −3.5), north | Arc flash label, drawings and the PPE chart | PPE locker, glove inspection table with an inflator, tester rack (two testers, one CAT II decoy, one flat), proving unit, barrier tape stand | Drive cabinet DCP-1, **moved here** from (0.9, −1.08), with a cable tray to the infeed drive | Beside the gear station |
| **Fire** | red | round the training station, x −8…−1.5, z −19…−12 | (−4.8, −12.6), south (today's station floor spot) | Fire classes, extinguisher types, PASS, exit routes | The extinguisher stand (exists), a pre-use inspection rack | The fires across the hall, plus a **practice pan** at (−4.8, −17.5) | The station's score board (exists) |

**Moving the drive cabinet.** Its own bay solves both overlaps: its arc flash ring no longer reaches the robot drill's start, and the gear station has room in front of it. A drive cabinet a few metres from its conveyor, fed by a tray, is how most plants have it. Rev 6's F1 fire follows the cabinet (its anchor is placed from the cabinet's top), and `CabinetAt` in `ElectricalSetup` is the only number to change.

**The practice pan** is a steel tray fire, like the one on real extinguisher courses: the trainee practises PASS on it before meeting a real scenario in the hall. It is a new fire location for `FireModel` (Class B, short incipient stage), so it uses the same suppression rules and scores the same way.

## 5. The shared game layer

Rev 6 already has the pieces: `FireDrillScore` (points, mistakes by clause, badges), popups, a HUD, a station board, a badge book and a CSV record. This plan turns them into one layer that all four drills use, so the trainee earns stars the same way everywhere and the record reads the same.

**Levels.** Each drill has the same four levels, matching the trainee phases in the Rev 6 and Rev 7 plans: 1 Awareness · 2 Gear and skills · 3 Procedure · 4 Assessment. Passing a level unlocks the next. Level 4 is unguided, seeded and timed.

**Stars per level.**
- ★ Passed: every required step done and no critical mistake.
- ★★ Also no minor mistakes.
- ★★★ Also within the level's par time.

**The compliance gate.** A critical mistake fails the level whatever the points. Points never buy back a safety rule. Points rank trainees on the hub's badge wall, and stars show progress; only "passed" goes in the record as the result.

**Every step and every mistake carries its clause**, as Rev 6 does today, so the debrief and the CSV record read by clause. One record format for all drills: `Builds/Training/<drill>_<trainee>_<utc>.csv` with the existing header `trainee,trainer,started_utc,at_s,step,regulation,kind,detail`.

**Activity types**, reused across the drills:

| Type | What the trainee does | Scored on |
|---|---|---|
| Hazard hunt | Find a set number of seeded hazards in the area within a time limit | Finds, time, false reports |
| Quiz card | Two or three questions at the briefing post (read the label, name the energies) | Right answers first time |
| Kit check | Pick and inspect the right kit, reject the faulty one | Right choice, faulty item caught |
| Procedure run | The ordered steps on the real machine | Order, completeness, critical rules |
| Spot the mistake | Watch a colleague (NPC) do a step wrong and stop them | Caught, how fast |
| Assessment | One seeded random run, no hints | All of the above, unguided |

**Feedback**, the same in every area: points and badge popups, the HUD checklist, a buzz in the hand, short sounds made in code, and the area's score board. Desktop and headset score alike.

**Refreshers.** The badge wall shows when each drill is due again: fire yearly (1910.157(g)(2), at least annually); lockout yearly as a reminder, since the procedure is inspected at least yearly (1910.147(c)(6)) and retraining is due when jobs, machines or procedures change or an inspection finds gaps ((c)(7)(iii)); electrical within three years (70E 110.4) with the contact-release refresher yearly; robot cell safety yearly as the demo's default (the employer sets it). "Due again" is a reminder, not a compliance record.

## 6. Activities per drill

### Robot cell safety (orange): ISO 10218-1/-2:2025, ISO 13855, IEC 60204-1

| Level | Activity | Clause it practises | Badge |
|---|---|---|---|
| 1 Awareness | Walk the fence line: find the warning zone, the protective zone, the E-stops and the reset station; quiz card (what slows the arm, what stops it, who may reset) | ISO 10218-2 (safeguarding, restart), ISO 13855 (zone size) | Zone aware |
| 2 Skills | E-stop and reset: stop the arm from the nearest E-stop, then reset from outside the zone; a reset tried from inside is refused and counts as a mistake | IEC 60204-1 (stop category, reset); ISO 10218-2 (restart only from outside) | Reset right |
| 3 Procedure | Clear a jam at the gate: stop the line, enter (protective stop), clear it, leave, check the zone is clear, reset; entering while the arm moves is critical | ISO 10218-2; 1910.147(a)(2)(ii) (minor servicing needs effective alternative protection) | Clean clear |
| 3 Spot the mistake | A colleague walks into the running cell (the existing `ZoneWalker`): stop them at the warning zone or hit the E-stop | ISO 10218-2 (protection of others) | Eagle eye |
| 4 Assessment | Random: a jam, a walker, or both | All | Cell keeper (all three stars) |

### Lockout (yellow): OSHA 29 CFR 1910.147

| Level | Activity | Clause | Badge |
|---|---|---|---|
| 1 Awareness | Read the CNC's energy control procedure card; quiz card: name its three energies (electrical, pneumatic, stored) and who is authorised | (c)(4), (c)(7)(i) | Know your energies |
| 2 Skills | Kit check at the lock board: your own lock and tag, not a shared one; a tag without a lock is a mistake | (c)(5), (d)(4) | Own lock |
| 3 Procedure | The 14 M11 steps on the CNC with the try-start; the PLC refuses to start with the lock on | (c)(9), (d)(1)–(d)(6), (e)(1)–(e)(3) | Try-start, Clean release |
| 3 Spot the mistake | A colleague goes to remove your lock to restart: stop them; only the person who put it on removes it | (e)(3) | Hands off |
| 4 Assessment | Random fault, unguided | All | Perfect lockout |

### Fire (red): OSHA 29 CFR 1910.38, .39, .157 (details in `docs/fire-safety-training.md`)

| Level | Activity | Clause | Badge (existing ones kept) |
|---|---|---|---|
| 1 Awareness | Hazard hunt (5 of 7 in 3 minutes); find both exit routes, the call point, the extinguishers and the assembly point | 1910.39(c), 1910.38(c)(2), 1910.37 | Sharp eye (new) |
| 2 Skills | Pre-use check at the inspection rack (one faulty unit); PASS on the practice pan from the right stand-off | 1910.157(g)(1), (e)(2); NFPA 10 | Right class, Sharp aim |
| 3 Procedure | A real scenario in the hall (F1, F2 or F3): alarm first, the right agent, knock it down, watch for reflash; F4 and F5: back out, take the other route, roll call | 1910.38(c)(1)–(c)(5), 1910.157(g) | Alarm first, Clean knockdown, Knew when to leave |
| 4 Assessment | Seeded random scenario, unguided | All | Perfect drill, Fire marshal |

### Electrical (blue): OSHA 29 CFR 1910.332–.335, .137; NFPA 70E-2027 (details in `docs/electrical-safety-training.md`)

| Level | Activity | Clause | Badge |
|---|---|---|---|
| 1 Awareness | Hazard hunt H (6 of 8 in 3 minutes); quiz card on the label (boundaries, incident energy); walk the three rings; stop an unqualified colleague at the limited approach boundary (E5) | 1910.303(g)(1), 1910.305, 1910.335(b); 70E 130.4, 130.5(H), 130.7(E) | Label reader |
| 2 Gear and skills (**M20**) | Pick the PPE the label calls for; inspect and air-test the gloves (one has a pinhole); pick the tester rated for 480 V, not the CAT II decoy; prove it on the proving unit; measure a known source; E4's flat tester is caught on the after-check | 1910.335(a)(1), 1910.137(c)(2); 70E 130.7, 120.6(7), 110.6(D) | Right kit, Glove guardian, Live-dead-live |
| 3 Procedure | E1 with E2 (backfeed) and E3 (stored energy) seeded: isolate, lock, wait, test every phase and the DC bus, find the UPS feed, lock it, test again, restore; the PLC refuses to start with the cabinet lock on | 1910.333(b)(2); 70E 120.6, 120.5(B)(6) | Backfeed hunter, Patience, Zero energy |
| 4 Assessment | Seeded random run, unguided | All | Qualified hands |

## 7. What changes in the roadmap

A new milestone, **M24 Training areas and game layer** (about 4 days), proposed to run **next, before the rest of M20**, because M20's gear station needs the electrical bay to stand in:

| Part | Work | Days |
|---|---|---|
| Areas | `TrainingAreas` asset and `Build Training Areas`: five areas with floor borders, entry signs, briefing posts and score boards; start spots for desktop and headset; the cabinet moved to its bay with a cable tray; the hub's badge wall | 1.5 |
| Game layer | `DrillScore` generalised from `FireDrillScore` (levels, stars, the compliance gate, clauses, badges) with EditMode tests; one record format; the badge book across drills | 1.5 |
| Rev 5 drills | Levels, badges and the spot-the-mistake activities for robot cell safety and lockout, on the existing M10 and M11 models | 1 |

Exit: each drill starts at its own area on desktop and in the headset, and nothing from another drill sits in it; the hub shows every trainee's stars and badges; a critical mistake fails a level whatever the points; the robot and lockout drills score by level and clause; 72 fps over Link standing in each area.

The fire and electrical activities stay in their own milestones: the fire practice pan and inspection rack in M15, levels and stars in M17; the electrical gear in M20, the procedure in M21, levels in M22. The start menu's cards (M15) start a drill at its area.

**M15 is open in the main checkout.** The fire area only adds around the existing station; nothing moves there. Build Training Areas leaves `Cell/Fire` alone except for the practice pan, which comes with M15's next fire work.

## 8. Open decisions

1. Approve the areas and M24 as above, to run before the rest of M20.
2. Move the drive cabinet to its own bay east of the cell (recommended), or keep it at (0.9, −1.08) and fit the electrical bay round it.
3. The hub: build it (recommended; it is where the badge wall and the hall map live), or start each drill straight at its area from the start menu.
4. Positions: confirm or mark the five spots in the Scene view (hub, robot start, lockout start, electrical bay, fire practice pan).
5. Robot level 3 (jam clearing under the protective stop): keep it, framed as minor servicing with alternative protection under 1910.147(a)(2)(ii), or replace it with a full lockout of the cell.
