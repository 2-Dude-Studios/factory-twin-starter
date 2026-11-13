# Standalone Quest version for SideQuest and the Meta Horizon Store (proposed 2026-11-11, approved 2026-11-12)

**2026-11-13:** decisions 2 to 4 taken (Quest 3 and 3S; free; Factory Twin Training, renamed the same day to Factory VR Safety Training; `com.twodudestudios.factorytwin`), and a milestone added before the store work: **M28 Hub, Meta account and leaderboards** (`docs/quest-hub-and-scores.md`), so store readiness below is now **M29** and listings and release **M30**. **Approved 2026-11-12** (Rev 8). M25 ran on 2026-11-12 (results in STATUS); M26 is under way. Decision 6 (the derived scene) is taken as part of the approval; decisions 1–5 and 7 are still open, and M26 assumes Quest 3 and 3S until decision 2 is made.

**Where it stands (2026-11-13, evening):** M25 done; M26 at 72 fps in every area and the largest fire (the ten-minute thermal mark to show); M27 closed by you (a passed robot level to show); M28 built in code (hub in the cell, game layer, Meta account and leaderboards, studio banners) and waiting for the headset check and your App ID; M29 started (store identity, release signing in code, privacy policy, keyword brief); M30 not started. Decisions 2–7 are taken (see 5); decision 1, what the first release holds, is open. The steps to the store, in order and with who does each, are in **6 Road to release**. Main was pushed to GitHub on 2026-11-13 (`b612d1f`).

You asked (2026-11-11) for a VR-specific version to host on SideQuest and the Meta app store. Today the headset view runs over Link: the PC renders the Windows build, and the MQTT broker and the soft PLC sit on that PC. A store app has to run on the headset alone. This plan says what that takes, in what order, and what only you can decide or do. Nothing here is built yet.

**What it is.** One Android app for Quest 3 and 3S: the start menu, the training hub and the drills (robot cell safety, lockout, fire, electrical as each is ready), with scoring, stars and badges. No PC, no broker, no PLC, no desktop dashboard. The predictive maintenance and virtual commissioning use cases stay in the PC demo: they are dashboard and PLC stories, not headset ones.

**What it is not.** Still a training demo: the store text, like the app, must not say "certified" or "compliant". It does not replace the Link version, which stays as the trainer-led demo with the dashboard.

Contents: 1 What stands in the way · 2 What the stores require · 3 Milestones · 4 What only you can do · 5 Decisions · 6 Road to release · Sources

---

## 1. What stands in the way

| Area | Today | Needed on the headset |
|---|---|---|
| **Rendering budget** | The hall is built for a PC: 316 draw calls plus 116 shadow casters, 1.49 M triangles in view, Deferred+ with SSAO, about 50 realtime point lights (build plan, measured for M12). Link holds 72 fps because the PC renders | A Quest variant of the scene: forward rendering, baked lighting, no SSAO, no or one small realtime shadow, far props culled, ASTC textures, 4x MSAA, fixed foveated rendering, fire and smoke particle counts capped. Meta's Quest 3 guide is 400–600 draw calls and 1.3–1.8 M triangles **in total**, and we are near or over both before the drills' own props |
| **Platform** | Windows build; OpenXR with XR Interaction Toolkit and XR Hands (no Meta SDKs). Unity 6000.3.19f1 has the Android module installed | Android, IL2CPP, ARM64 only, Vulkan, OpenXR with its Meta Quest feature group. The packages we have cover this; controllers and hand tracking stay as they are |
| **Things that live on the PC** | The MQTT bridge connects to a broker at 127.0.0.1; the soft PLC is OpenPLC over Modbus; the trainer starts drills and injects faults from the desktop dashboard; capture tools, Recorder and the Cinemachine rig | A standalone mode: bridge off, the in-Unity sequencer as the logic source (the drills already run on it), no dashboard, no capture rig. **In-headset controls for everything the trainer does today**: pick a drill and a level, end it, reset. The robot drill's levels, for one, can only be started from the dashboard right now |
| **Third-party code under IL2CPP** | realvirtual Starter 6 and MQTTnet are compiled DLLs, only ever built for Windows (Mono) here | realvirtual lists Android and VR among its platforms for Starter (its comparison page), but our scene on IL2CPP is untested: managed stripping and its layers need a real build to prove. MQTTnet is left out of the Android build |
| **Files** | Records and the badge book go to `Builds/Training` in the Editor and to the persistent data path in a build; the AI4I dataset is read from disk | Records and badges already land in the app's own storage. Anything read from StreamingAssets has to go through Android's loader, or be left out with the use case it serves |
| **Identity** | Company "DefaultCompany", product "factory-twin-starter", package id still the Unity template's | A real app name (30 characters at most), a package id that never changes after the first upload, a version scheme, an icon |
| **Licences** | Asset Store content (Factory Training hall, Particle Pack, Fast IK) ships inside builds, which their EULA allows. Rocketbox is MIT. realvirtual is on the register as "compiled-only distribution, single-user commercial", verdict Conditional | **realvirtual's EULA needs reading for a public store release** before anything is uploaded; if it does not allow it, the conveyors and sensors need a Professional licence or our own replacements. The credits and licence texts need a page in the headset (the desktop has one). Unity's own terms apply to a public release too (splash screen on Personal; rule 5 of the register on Unity Industry) |

## 2. What the stores require

**Meta Horizon Store** (from Meta's publishing checklist; the numbers to re-check on the day of submission):

- **Packaging:** release-signed APK (signature scheme v2 or later), ARM64 only, `minSdkVersion` 29 or higher, `targetSdkVersion` 32 or higher, the `com.oculus.intent.category.VR` category on the launch activity, a version code that only ever goes up. **The signing key is permanent**: lose it and the app cannot be updated.
- **Performance:** 72 Hz sustained at the least (90 Hz is what Quest 3 buyers expect), no thermal throttling in the first five minutes, interactive within about 15 seconds of launch, no crashes or hangs in any flow.
- **Permissions:** only what the app uses, checked on the merged manifest. With the MQTT bridge out, the app should need no network permission at all.
- **Entitlement:** a paid app must verify the user owns it within 10 seconds of launch. That check comes from Meta's Platform SDK, which is a package we do not have (CLAUDE.md allows OpenXR, XR Management, XRI and XR Hands, "not the Meta XR SDKs"). A free app can do without it; see decision 3. *(2026-11-13: the Platform SDK is now approved for M28; the app runs the entitlement check at start-up although it is free.)*
- **Data Use Checkup (DUC):** any app using Platform SDK user data (here: User ID and User Profile, for the global leaderboards) needs a DUC on the dashboard, and the privacy policy is reviewed with it. Requests are approved automatically during development, and reviewed by hand once the app is submitted. Steps and ready-to-paste descriptions: 6 Road to release, step 3.
- **Age group:** every app self-certifies Teens and Adults (13+), Mixed Ages or Children. This app: 13+ (apps for children cannot use DUC features).
- **Listing:** icon 1024 × 1024, hero art 2560 × 1440 with text inside the middle 80 %, at least three screenshots at 2560 × 1440 **captured in the headset** (not Editor views), a short description (150 characters), a long one (4,000), a category, the IARC age rating questionnaire, a comfort rating that matches the locomotion (teleport: Comfortable), **a privacy policy URL** and a support contact. A trailer is optional.
- **Review:** about one to two weeks, longer round holidays or after a rejection.

**SideQuest:** a developer account, then a listing with a banner (about 2048 × 370), an icon (about 580 × 470), screenshots, a summary and description, the package name, version code and name, a comfort level, and **a direct download URL for the APK** (it can follow a GitHub release, which suits this repo). SideQuest reviews the listing for content and safety. It can also link to the Meta store page once that exists. The same signed APK serves both.

**Order.** SideQuest first, as early access: quick to list and it gets the app on real headsets. The Meta store second, once the performance and the checklist hold.

## 3. Milestones (Rev 8, about 15.5 days of focused time with M28, plus the store's review)

| Milestone | Scope | Exit | Days |
|---|---|---|---|
| **M25 Quest spike** | An Android build of the menu and the cell as they are, on a Quest 3: IL2CPP, ARM64, Vulkan, OpenXR's Meta Quest features, hand tracking. Find what breaks (realvirtual, MQTTnet, shaders, layers) and measure frame rate, draw calls and triangles in each training area. Read realvirtual's EULA for a public release | The app starts on the headset and reaches the hub; the numbers and the list of what breaks are in STATUS; the licence question is answered | 2 |
| **M26 Quest scene variant** | `Tools > Factory Twin > Build Quest Variant`: derives the headset scene from `Cell.unity`, re-runnable, so the drills stay in one place: forward renderer asset, baked lighting, lights and shadows cut, the hall trimmed to what the areas can see, static batching, texture compression, particle caps for the fires | 72 fps or better standing in each area and during the largest fire, within Meta's draw-call and triangle guide, no thermal warning in ten minutes | 4 |
| **M27 Standalone mode** | A standalone switch in `TwinConfig` (set by the Android build): bridge off, Unity sequencer, no dashboard or capture rig. In-headset controls at the hub and each area for drill, level, end and reset; teleport floors everywhere the trainee may go; pause when the headset comes off; recentre | Each drill that ships can be started, played, ended and scored with the headset alone; the records and the badge book survive a restart | 2.5 |
| **M28 Hub, Meta account and leaderboards** (added 2026-11-13) | The menu inside the cell, the game layer (XP, rank, results card, daily challenge, streak), the Meta account and leaderboards, the studio banners. Plan and exit in `docs/quest-hub-and-scores.md` | The headset app opens at the hub with the Meta name and rank; every drill starts from the hub and returns to it with no scene load; results cards; scores reach the leaderboards; 72 fps at the hub | 2.5 |
| **M29 Store readiness** (was M28) | App name, package id, version scheme, icon and splash; release keystore and signing; manifest and permission audit; start-up time; credits and licence texts in the headset; a privacy policy page; Meta's checklist run on the device with the `metavr` tools; the entitlement check if the app is paid | The release APK passes every item of the checklist on a Quest 3 and a Quest 3S | 2.5 |
| **M30 Listings and release** (was M29) | Icon, hero art, banner, five screenshots and a short trailer captured in the headset; the store texts (with the training-demo wording); the SideQuest listing on a GitHub release; the Meta dashboard entry, an ALPHA channel upload, then the submission | Live on SideQuest; submitted to the Meta Horizon Store; review feedback answered | 2, plus review |

**What ships depends on the drills.** Robot cell safety (four levels) and lockout (level 3; levels 1, 2 and 4 to come) are playable now. Fire is part-built (M15 open, M16 to M18 to come). Electrical has its cabinet and model but no tester, PPE or procedure yet (M20 to M22). See decision 1.

**Risks.**
- The hall may not fit even trimmed; the fallback is a smaller hall built from the same pieces, which adds days to M26.
- realvirtual on IL2CPP, or its licence for a public release, may force us to replace the conveyors and sensors with our own (they are simple), which adds about two days.
- The store may reject on performance or polish; a rejection costs a resubmission and another review.
- Fire particles are the heaviest thing we draw; the fire drill may ship with smaller fires on the headset than on the PC.

## 4. What only you can do

- A Meta developer account with a verified organisation for 2 Dude Studios, and the store's data-use and age-rating questionnaires.
- A SideQuest developer account.
- Keep the release keystore and its passwords safe and backed up; I can create it, but it must never go into the repo.
- Host the privacy policy and give a support address (2dudestudios.com is the natural place).
- Pricing and payout details, if the app is paid.
- Wear the headset: store screenshots, the trailer and the performance checks all come from a real device.
- Added 2026-11-13 with M28: create the app on the Developer Dashboard and send the App ID; create the four leaderboards; submit the Data Use Checkup; self-certify the age group; answer the IARC questionnaire; put the privacy policy page live on 2dudestudios.com (prompt in `docs/website-privacy-page-prompt.md`) with a support inbox that is read; the studio logo as a large PNG.

## 5. Decisions

**Outcomes (you, 2026-11-12 and 2026-11-13):** 2 Quest 3 and 3S. 3 Free, with the Meta Platform SDK approved anyway for the Meta account and leaderboards (M28). 4 **Factory VR Safety Training** (renamed from Factory Twin Training for search, `docs/keyword-brief-vr-training.md`), `com.twodudestudios.factorytwin`. 5 Rev 8 now, ahead of M20. 6 Taken with the approval (`CellQuest.unity`). 7 The trainee is the Meta account's display name on the headset; "Sam Rivera" stays for the PC and the videos. **Still open: 1**, now with all four drills playable on the headset in some form (see 6, step 5).

1. **What the first release contains.** (a) Early access now-ish with robot cell safety and lockout, the other two marked "coming soon", updated as M18 and M23 land (recommended: it gets the app out and proves the pipeline while the drills are finished); or (b) wait until all four drills are done.
2. **Devices.** Quest 3 and 3S (recommended), or Quest 2 as well: Quest 2 is much weaker and would set the budget for everything.
3. **Free or paid.** Free (recommended for a studio showcase; no entitlement check, no Meta Platform SDK), or paid, which needs your approval for that package.
4. **Name and package id.** A suggestion: "Factory Twin Training" and `com.twodudestudios.factorytwin`. The package id cannot change after the first upload.
5. **When.** Rev 8 next, ahead of M20 (the spike at least, since it tells us early whether the hall fits); or after Rev 7.
6. **A second scene.** The Quest variant is a derived scene beside `Cell.unity`. CLAUDE.md says `Cell.unity` is the only scene (with `Menu.unity` added by your decision on 2026-11-04); this needs the same kind of yes.
7. **Trainee name.** Fixed "Sam Rivera" as now, or typed by the user. A typed name is personal data kept on the device, and the privacy policy has to say so.

## 6. Road to release (written 2026-11-13)

Everything left between today's build and the app being live, in order. **You** marks what only you can do; the rest is mine unless it needs the headset on your head. Steps in the same group can run side by side.

**Step 1. Prove M28 on the headset** (me, with the Quest on and you for the walk-through; half a day)
- Save the scene through UnitySkills, Build Quest Variant, build and install the APK (`Builds/Quest/FactoryVRSafetyTraining.apk`).
- Check the hub title, cards and level buttons, the banners (name until the logo comes), the results card after a run (stars, points counting up, badges, a new best), the step and mistake popups and the buzz, the daily challenge and the streak, XP and rank, and the return to the hub with the cell put back.
- `-probe-records` for a results card without playing, then delete the probe records from the headset.
- 72 fps at the hub with the menu up (part of M28's exit).

**Step 2. Meta account live** (you first, then me; a day plus Meta's set-up time)
- **You:** create the app on the Developer Dashboard (Meta Horizon Store, Quest 3 and 3S); add your account and any testers to the organisation; create Meta test users for screenshots; send the App ID.
- **You:** create the leaderboards `robot_cell`, `lockout`, `fire`, `electrical` (higher is better, each user's best kept).
- **Me:** the App ID into `TwinConfig.asset`, a build, then on the headset: the entitlement check passes, the hub shows your Meta name, a scored run posts to its leaderboard and the hub card shows the top five. That closes M28's exit.

**Step 3. Data Use Checkup and the privacy page** (you; an hour, then automatic approval while in development)
- Put the privacy page live (prompt in `docs/website-privacy-page-prompt.md`; the text is `docs/privacy-policy.md`, filled with the address, support inbox and date) and its URL in App Submission, Additional Details, Privacy Policy.
- Self-certify the age group: Teens and Adults (13+).
- DUC: add **User ID** (usage: identify users across sessions) and **User Profile** (show the username), with the descriptions below and screenshots of the hub name and a leaderboard card taken with a test user; certify; Submit Requests; Submit for Review.
  - User ID: "Factory VR Safety Training is a single-player VR safety training demo. It reads the signed-in user's ID only through Meta's platform, to post the user's score from each scored training (robot cell safety, lockout/tagout, fire safety, electrical safety) to that training's global Meta leaderboard and to show the top entries. We do not store the ID, send it to our own servers or share it. The app has no analytics or advertising."
  - User Profile: "The app reads the user's display name and shows it on the training hub as the trainee's name. It uses the name to label the user's training progress, which is saved on the headset only. Display names appear on the global leaderboards for other players of this app. We do not use the profile photo."
- Request everything before the app is submitted: after submission, new DUC requests wait for a manual review.

**Step 4. Close what M26 and M27 left open** (you in the headset, me on the logs; one session)
- A ten-minute training session with no thermal warning (M26 exit).
- A passed robot cell level (M27).
- A Quest 3S run if you can borrow one: the 3S has the same chip as the Quest 3, so it should hold, but the store checks both.

**Step 5. Decision 1: what the first release holds** (you)
- Today on the headset: robot cell safety (four levels), lockout/tagout (level 3), fire safety (the tablet's drill), electrical safety (E1–E4 with the PPE kit). My recommendation: ship all four as they are, marked "early access" in the store text, and add the rest (lockout levels 1, 2 and 4; fire M16–M18; electrical M20–M22) as updates.

**Step 6. M29 store readiness** (me, then you for the keystore; about two days)
- **You:** make the release keystore (outside the repo, backed up twice; losing it means the app can never be updated); set `FT_KEYSTORE`, `FT_KEYSTORE_PASS`, `FT_KEY_ALIAS` and `FT_KEY_PASS` before starting Unity.
- **Me:** `4 Build Release APK`, then check the APK: signature scheme v2 or later, ARM64 only, `minSdkVersion` 29+ and `targetSdkVersion` 32+, the VR launcher category, the version code, **only the permissions it uses** (the merged manifest, read with `aapt`), and Meta's own store validator.
- Start-up to the hub under 15 seconds; no crash or hang in any flow (start, each drill, back to hub, pause and resume, headset off and on).
- Credits and licence texts in the headset (a page on the tablet or the hub): Asset Store content, Rocketbox (MIT), realvirtual (compiled component), MQTTnet is not in the APK, the Platform SDK, our own models. And you read realvirtual's terms 1.5 to 1.7 yourself once, as M25 asked.
- Exit: the release APK passes every item on a Quest 3 (and a 3S if you have one).

**Step 7. M30 listings** (me for the files and texts, you for the captures and accounts; about two days)
- Texts from the keyword brief: the name (Factory VR Safety Training), a short description of 150 characters with "VR safety training for manufacturing", a long description (4,000 at most) with one paragraph per drill, category Education or Productivity, comfort rating Comfortable (teleport), the training-demo wording (never "certified" or "compliant"), and a line pointing businesses to 2dudestudios.com for custom training.
- Art: icon 1024 x 1024, hero art 2560 x 1440 (text inside the middle 80 %), at least three screenshots of 2560 x 1440 **captured in the headset** (one per drill and the hub), a 30–60 s trailer from headset captures. The studio logo is needed for the icon and the hero art.
- **You:** the IARC questionnaire; the support email and website fields; a SideQuest developer account.

**Step 8. Release** (me for the uploads you approve, you for the submission; then one to two weeks of review)
- A GitHub release with the signed APK (the repo is public, so the APK is too); the SideQuest listing pointing at it: early access, live in days.
- The Meta store: upload to the ALPHA channel (`metavr store dist upload`, or the dashboard), test it from the store on your headset (platform features answer for store copies), then **you** press Submit for Review on the dashboard. Answer the review's feedback and resubmit if needed.
- After approval: you choose release now or on a date.

**After launch (v0.2 onwards, unscheduled)**
- Content updates through the same pipeline, bumping the version code each time: lockout levels 1, 2 and 4; fire M15–M18 (hazard hunt, alarm, evacuation, assessment); electrical M20–M22 (proved tester, the second source, the full safe work procedure); Meta achievements for the badges if you want them on the Meta profile.
- 90 Hz on Quest 3 (what buyers expect; we hold 72 with about 3 ms spare).
- Leads: the hub's studio banner and a "Bring this training to your site" panel with the website address (text only: the app has no internet permission), and a product page on 2dudestudios.com per `docs/keyword-brief-vr-training.md`.
- Recertify the DUC every year, or the app leaves the store; update the privacy policy and the DUC before any new data use.
- For a client: a branded build of their own line, sideloaded or through a device manager, outside the public store.

## Sources

- Meta Horizon Store publishing requirements and the store submission checklist (developers.meta.com/horizon; the Meta VR tooling's `hz-store-submit` guide, read 2026-11-11).
- SideQuest listing fields and APK hosting: spectrexr.io/blog/projects/how-to-publish-your-vr-apps-on-sidequest (read 2026-11-11); to be confirmed against SideQuest's own dashboard when the account exists.
- realvirtual product comparison (Starter: "All Unity Platforms (Windows, Linux, macOS, iOS, Android, WebGL, VR/AR)"): realvirtual.io/en/products/compare (read 2026-11-11). Its EULA is still to be read for public distribution.
- The hall's rendering numbers and Meta's Quest 3 guide: `docs/build-plan.md`, Rev 5 platform note.
