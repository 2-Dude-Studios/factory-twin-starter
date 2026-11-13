# Standalone Quest version for SideQuest and the Meta Horizon Store (proposed 2026-11-11, approved 2026-11-12)

**2026-11-13:** decisions 2 to 4 taken (Quest 3 and 3S; free; Factory Twin Training, `com.twodudestudios.factorytwin`), and a milestone added before the store work: **M28 Hub, Meta account and leaderboards** (`docs/quest-hub-and-scores.md`), so store readiness below is now **M29** and listings and release **M30**. **Approved 2026-11-12** (Rev 8). M25 ran on 2026-11-12 (results in STATUS); M26 is under way. Decision 6 (the derived scene) is taken as part of the approval; decisions 1–5 and 7 are still open, and M26 assumes Quest 3 and 3S until decision 2 is made.

You asked (2026-11-11) for a VR-specific version to host on SideQuest and the Meta app store. Today the headset view runs over Link: the PC renders the Windows build, and the MQTT broker and the soft PLC sit on that PC. A store app has to run on the headset alone. This plan says what that takes, in what order, and what only you can decide or do. Nothing here is built yet.

**What it is.** One Android app for Quest 3 and 3S: the start menu, the training hub and the drills (robot cell safety, lockout, fire, electrical as each is ready), with scoring, stars and badges. No PC, no broker, no PLC, no desktop dashboard. The predictive maintenance and virtual commissioning use cases stay in the PC demo: they are dashboard and PLC stories, not headset ones.

**What it is not.** Still a training demo: the store text, like the app, must not say "certified" or "compliant". It does not replace the Link version, which stays as the trainer-led demo with the dashboard.

Contents: 1 What stands in the way · 2 What the stores require · 3 Milestones · 4 What only you can do · 5 Decisions · Sources

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
- **Entitlement:** a paid app must verify the user owns it within 10 seconds of launch. That check comes from Meta's Platform SDK, which is a package we do not have (CLAUDE.md allows OpenXR, XR Management, XRI and XR Hands, "not the Meta XR SDKs"). A free app can do without it; see decision 3.
- **Listing:** icon 1024 × 1024, hero art 2560 × 1440 with text inside the middle 80 %, at least three screenshots at 2560 × 1440 **captured in the headset** (not Editor views), a short description (150 characters), a long one (4,000), a category, the IARC age rating questionnaire, a comfort rating that matches the locomotion (teleport: Comfortable), **a privacy policy URL** and a support contact. A trailer is optional.
- **Review:** about one to two weeks, longer round holidays or after a rejection.

**SideQuest:** a developer account, then a listing with a banner (about 2048 × 370), an icon (about 580 × 470), screenshots, a summary and description, the package name, version code and name, a comfort level, and **a direct download URL for the APK** (it can follow a GitHub release, which suits this repo). SideQuest reviews the listing for content and safety. It can also link to the Meta store page once that exists. The same signed APK serves both.

**Order.** SideQuest first, as early access: quick to list and it gets the app on real headsets. The Meta store second, once the performance and the checklist hold.

## 3. Milestones (Rev 8, about 13 days of focused time, plus the store's review)

| Milestone | Scope | Exit | Days |
|---|---|---|---|
| **M25 Quest spike** | An Android build of the menu and the cell as they are, on a Quest 3: IL2CPP, ARM64, Vulkan, OpenXR's Meta Quest features, hand tracking. Find what breaks (realvirtual, MQTTnet, shaders, layers) and measure frame rate, draw calls and triangles in each training area. Read realvirtual's EULA for a public release | The app starts on the headset and reaches the hub; the numbers and the list of what breaks are in STATUS; the licence question is answered | 2 |
| **M26 Quest scene variant** | `Tools > Factory Twin > Build Quest Variant`: derives the headset scene from `Cell.unity`, re-runnable, so the drills stay in one place: forward renderer asset, baked lighting, lights and shadows cut, the hall trimmed to what the areas can see, static batching, texture compression, particle caps for the fires | 72 fps or better standing in each area and during the largest fire, within Meta's draw-call and triangle guide, no thermal warning in ten minutes | 4 |
| **M27 Standalone mode** | A standalone switch in `TwinConfig` (set by the Android build): bridge off, Unity sequencer, no dashboard or capture rig. In-headset controls at the hub and each area for drill, level, end and reset; teleport floors everywhere the trainee may go; pause when the headset comes off; recentre | Each drill that ships can be started, played, ended and scored with the headset alone; the records and the badge book survive a restart | 2.5 |
| **M28 Store readiness** | App name, package id, version scheme, icon and splash; release keystore and signing; manifest and permission audit; start-up time; credits and licence texts in the headset; a privacy policy page; Meta's checklist run on the device with the `metavr` tools; the entitlement check if the app is paid | The release APK passes every item of the checklist on a Quest 3 and a Quest 3S | 2.5 |
| **M29 Listings and release** | Icon, hero art, banner, five screenshots and a short trailer captured in the headset; the store texts (with the training-demo wording); the SideQuest listing on a GitHub release; the Meta dashboard entry, an ALPHA channel upload, then the submission | Live on SideQuest; submitted to the Meta Horizon Store; review feedback answered | 2, plus review |

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

## 5. Decisions

1. **What the first release contains.** (a) Early access now-ish with robot cell safety and lockout, the other two marked "coming soon", updated as M18 and M23 land (recommended: it gets the app out and proves the pipeline while the drills are finished); or (b) wait until all four drills are done.
2. **Devices.** Quest 3 and 3S (recommended), or Quest 2 as well: Quest 2 is much weaker and would set the budget for everything.
3. **Free or paid.** Free (recommended for a studio showcase; no entitlement check, no Meta Platform SDK), or paid, which needs your approval for that package.
4. **Name and package id.** A suggestion: "Factory Twin Training" and `com.twodudestudios.factorytwin`. The package id cannot change after the first upload.
5. **When.** Rev 8 next, ahead of M20 (the spike at least, since it tells us early whether the hall fits); or after Rev 7.
6. **A second scene.** The Quest variant is a derived scene beside `Cell.unity`. CLAUDE.md says `Cell.unity` is the only scene (with `Menu.unity` added by your decision on 2026-11-04); this needs the same kind of yes.
7. **Trainee name.** Fixed "Sam Rivera" as now, or typed by the user. A typed name is personal data kept on the device, and the privacy policy has to say so.

## Sources

- Meta Horizon Store publishing requirements and the store submission checklist (developers.meta.com/horizon; the Meta VR tooling's `hz-store-submit` guide, read 2026-11-11).
- SideQuest listing fields and APK hosting: spectrexr.io/blog/projects/how-to-publish-your-vr-apps-on-sidequest (read 2026-11-11); to be confirmed against SideQuest's own dashboard when the account exists.
- realvirtual product comparison (Starter: "All Unity Platforms (Windows, Linux, macOS, iOS, Android, WebGL, VR/AR)"): realvirtual.io/en/products/compare (read 2026-11-11). Its EULA is still to be read for public distribution.
- The hall's rendering numbers and Meta's Quest 3 guide: `docs/build-plan.md`, Rev 5 platform note.
