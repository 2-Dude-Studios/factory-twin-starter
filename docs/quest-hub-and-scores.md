# Headset hub, Meta account and leaderboards (Rev 8, M28; asked and decided 2026-11-13)

You asked (2026-11-13), before the store build: a better menu, a better gamified experience, a Meta login and a score keeper, and no scene switching on the headset ("from menu can be on same scene as cell ... scene switching is not smooth"). This adds one milestone to Rev 8 ahead of the store work, which moves one number on: **M28 Hub, Meta account and leaderboards**, **M29 Store readiness** (was M28), **M30 Listings and release** (was M29).

## Decisions (yours, 2026-11-13)

1. **Meta XR Platform SDK approved** (`com.meta.xr.sdk.platform`, from Meta's npm registry, version 207.0.0: older than two weeks, and v207 fixes the `await Core.AsyncInitialize()` hang of v203–v205). It brings no Meta core SDK; its dependencies are Unity's own (uGUI, Newtonsoft JSON, Input System). Used for the Meta account (user name), the entitlement check the store asks for, and leaderboards. Our UI stays UI Toolkit.
2. **Scores: Meta leaderboards only.** One leaderboard per drill; the trainee's stars, badges and best scores stay in the badge book on the device, as now (that is progress, not a leaderboard).
3. **The menu inside the cell, one scene on the headset.** The app opens at the hub; picking a drill takes the trainee there and starts it; "Main menu" brings them back; no scene loads after start-up. The PC keeps Menu.unity and Cell.unity as they are.
4. Earlier the same day: name **Factory Twin Training** (renamed the same day to **Factory VR Safety Training**), package id **com.twodudestudios.factorytwin**, free, Quest 3 and 3S.

## What gets built

**Hub menu in the cell (headset).** A large world-space menu at the hub start, facing the trainee as the app opens: the trainee's Meta name and rank, then one card per drill (Robot cell safety, Lockout / tagout, Fire safety, Electrical safety, Free roam) with its levels and stars, best score and badges, and its leaderboard's top entries. Picking a card (or a level) fades out, takes the trainee to the area's start, starts the drill and fades back in. The tablet's "Main menu" ends the drill, puts the cell back (fire out, alarm reset, lockout and robot drill ended, cabinet back to normal and the PPE back on the bench) and returns to the hub the same way. A **session** component owns this start and end, so no drill has to know about the menu. The Quest build ships CellQuest only (Menu.unity stays for the PC); a short launch image shows while it loads (the Meta Quest feature's system splash).

**Game layer** (you asked twice for a better gamified experience).
- **XP and rank.** Every point earned is XP; the rank climbs with it: Trainee, Operator, Technician, Specialist, Safety lead (thresholds as data). A rank-up has its own moment (banner, sound). Shown on the hub menu and the tablet.
- **Results card** at the end of every level or drill, in front of the trainee: stars revealed one at a time with a sound and a burst of sparks, points counting up, new badges, a new best, the leaderboard position, and buttons for Next level, Again and Hub.
- **Live feedback in every drill**: a "+25 step" popup and a controller buzz on each step done, a red flash and a buzz on a mistake (the fire drill's popups, made common to all drills).
- **Daily challenge** on the hub: one level a day, picked from the date, worth double XP; a **streak** counts the days in a row trained.
- **Level select on the hub menu** for every drill with levels (locked ones shown locked), not only on the tablet; each card shows its stars, best, badges and the top of its leaderboard.
- **Meta achievements** for the badges, if you want them on the Meta profile too (each needs creating on the dashboard; a later step, not in the exit).

**2 Dude Studios branding in the hall** (you, 2026-11-13). The studio's logo on banners round the hall: over the hub, at each training area's gateway, on the long walls in view of the areas, and on the start of the hub menu and the results card. Built by a re-runnable Editor step from one logo texture. It needs the logo at a good size: the only copy in the project is 153 x 103 pixels (the ads' kit), which would be blurred on a banner; a PNG of about 2048 px wide with a transparent background is wanted. Our own brand, not a vendor's, so CLAUDE.md's rule on vendor branding does not apply; a line in docs/LICENSES.md records it as ours.

**Meta account.** At start-up `Core.Initialize` and the entitlement check, then `Users.GetLoggedInUser`: the trainee is the Meta account's display name (badge book and records under that name; "Trainee" when the platform is not reachable, for example an unsigned sideload). The fixed "Sam Rivera" stays for the PC and the videos.

**Leaderboards.** On the Developer Dashboard (yours): one leaderboard per drill with these API names, sort descending (higher is better), entry per user keeps the best: `robot_cell`, `lockout`, `fire`, `electrical` (scores from M22). At the end of a scored run the score goes up with `Leaderboards.WriteEntry`; the hub card shows the top 5 and the trainee's own rank (`Leaderboards.GetEntries`). Platform features work only for copies from the store or, sideloaded, for accounts in your developer organisation (Meta's rule).

## What only you can do

- Create the app on the Developer Dashboard (Meta Horizon Store) and send me its **App ID**; add your account (and testers) to the organisation.
- Create the four leaderboards above.
- The 2 Dude Studios logo as a large PNG (about 2048 px wide, transparent background).
- The **Data Use Checkup** for User ID, User Profile and Leaderboards; the privacy policy must say the app shows the Meta user name and posts scores to Meta leaderboards.

## Exit (M28)

The headset app opens at the hub with the Meta name and rank, under the studio's banners; every drill starts from the hub menu and returns to it with no scene load; a level's results card shows stars, points and badges; scores reach the drill's leaderboard and the hub shows the top entries; 72 fps holds at the hub with the menu up.
