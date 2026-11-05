# Third-party licence register

Rules (from the build plan):
1. Asset Store and Fab content ships only inside built applications; raw files are never handed to a client.
2. realvirtual is distributed compiled only; never share its source, prefabs or scenes.
3. Every CC BY item gets a credit line in the app's Credits panel and on the web page.
4. Vendor branding (UR, KUKA, ABB, FANUC) is removed from any model shown publicly until vendor terms are confirmed.
5. When a client project starts on this codebase: buy the Unity Industry seat, upgrade realvirtual to Professional if the client needs OPC UA/PLC, and re-run this list.

The full tracker (62 candidates and verdicts) is kept privately. Rows marked "not in repo" are cleared candidates that the project does not use.

| Item | Version | Source | Licence | Verdict | Where in repo | Credit required |
|---|---|---|---|---|---|---|
| Factory Training \| Industry Sample | 1.0 (9 Feb 2026) | Unity Asset Store | Standard Asset Store EULA (Extension Asset) | OK | Assets/ThirdParty/FactoryTraining (not in git) | no |
| realvirtual.io Digital Twin Starter 6 | 6.0.11 | Unity Asset Store + realvirtual EULA | Compiled-only distribution, single-user commercial | Conditional | Assets/realvirtual + Assets/Gizmos/realvirtual — importer's default paths (not in git, see .gitignore). Prefabs used in Cell.unity: ConveyorSmall, sensors, Source/Sink, RobotStand, and (M4) `3DPrefabs/CNC.prefab` as the press station's machine. It carries only generic safety stickers, no vendor logos. From M14, `3DPrefabs/ControlCabinet/ControlCabinet.prefab` (mesh only, no realvirtual components) scaled up as the fire drill's drive cabinet, a placeholder until our own model. | no |
| Unity Splines | 2.9.0 (minimum bundled with 6000.3.19f1) | Unity registry | Unity Companion License | OK | Packages/manifest.json | no |
| Universal RP | 17.3.0 | Unity registry (built-in) | Unity Companion License | OK | Packages/manifest.json; stock pipeline assets from Unity's URP template in Assets/_Project/Settings | no |
| Cinemachine | 3.1.7 | Unity registry | Unity Companion License | OK | Packages/manifest.json | no |
| Input System | 1.19.0 | Unity registry | Unity Companion License | OK | Packages/manifest.json | no |
| OpenXR Plugin (com.unity.xr.openxr) | 1.18.0 | Unity registry | Unity Companion License | OK | Packages/manifest.json (M12, approved 2026-10-24; Quest 3 over Link) | no |
| XR Plugin Management (com.unity.xr.management) | 4.6.1 | Unity registry | Unity Companion License | OK | Packages/manifest.json (M12); its settings assets in Assets/XR | no |
| XR Interaction Toolkit (com.unity.xr.interaction.toolkit) | 3.6.1 (pulls in com.unity.xr.core-utils, com.unity.xr.legacyinputhelpers) | Unity registry | Unity Companion License | OK | Packages/manifest.json (M12); its project settings in Assets/XRI | no |
| XR Interaction Toolkit Starter Assets sample | 3.6.1 | Unity (sample of the package above) | Unity Companion License | OK | Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets (Unity's fixed sample path; the XR Origin rig, input actions, teleport and turn) | no |
| XR Hands (com.unity.xr.hands) | 1.10.0 | Unity registry | Unity Package Distribution License (the package's LICENSE.md) | OK | Packages/manifest.json (M12, approved 2026-10-27; hand joints for hand tracking over Link) | no |
| XR Hands HandVisualizer sample | 1.10.0 | Unity (sample of the package above) | Unity Package Distribution License | OK | Assets/Samples/XR Hands/1.10.0/HandVisualizer (Unity's fixed sample path; its LeftHand/RightHand models are the controller hands and the tracked hands) | no |
| AI Navigation | 2.0.13 | Unity registry (com.unity.ai.navigation) | Unity Companion License | OK | Packages/manifest.json (already in the manifest; used from M12 for the zone walker's NavMesh, `Cell/Training/WalkArea`) | no |
| Unity Recorder | 5.1.7 (pulls in com.unity.bindings.openimageio 1.0.2) | Unity registry | Unity Companion License | OK | Packages/manifest.json (Editor only, not in the build; used to record the capture takes) | no |
| UnitySkills (Editor automation for Claude Code) | v2.8.4 | github.com/Besty0728/Unity-Skills | MIT | OK | Packages/manifest.json (git URL; dev tooling, not part of the demo) | no |
| Unity Test Framework | 1.6.0 | Unity registry (built-in) | Unity Companion License | OK | Packages/manifest.json (editor-only, not shipped) | no |
| URDF-Importer | v0.5.2 | github.com/Unity-Technologies/URDF-Importer | Apache-2.0 | OK | Packages/manifest.json (git URL) | notice in Credits |
| universal_robot — UR5e URDF + meshes only | commit 39ad110d (20 Jul 2026) | github.com/ros-industrial/universal_robot | BSD-3-Clause (`ur_description/LICENSE`), repo Apache-2.0 | OK — UR5e only; UR15/18/20/30/8LONG meshes not copied | Assets/_Project/Robots/ur (de-branded: URBlue material renamed and recoloured; see its README.md) | **yes** — notice in Credits |
| xacro (Python package, build-time only) | 2.x | pypi.org/project/xacro | BSD-3-Clause | OK — used once to expand the UR5e xacro; not in the repo or the build | not shipped | no |
| Fast IK | 1.2 | Unity Asset Store | Standard Asset Store EULA | OK | Assets/ThirdParty/FastIK (not in git) | no |
| Microsoft Rocketbox avatars and animations | master (2026-10-27): Construction_Male_01, _02, _03, Construction_Female_01; m_/f_ idle_neutral_01, walk_neutral_01, work_mid | github.com/microsoft/Microsoft-Rocketbox | MIT (LICENSE.md copied with the files) | OK | Assets/ThirdParty/Rocketbox (Git LFS). Changed by us: textures cut to the colour and normal maps (and the opacity colour map) and resized from 2048/4096 px TGA to 1024 px PNG; FBX unchanged. Used for the four NPC workers; our URP materials and controllers in Assets/_Project/Art/Workers/Rocketbox | yes — MIT notice (copyright Microsoft) in Credits |
| Animated Human Low Poly (Quaternius) | 2017-11-03 release, `Animated Human by @Quaternius_0.zip` (sha256 dcd72162…24db3); FBX only, plus its License.txt | opengameart.org/content/animated-human-low-poly | CC0 1.0 (License.txt in the pack) | OK | Assets/ThirdParty/Quaternius/AnimatedHuman (FBX via Git LFS; import settings only, clips renamed); the two simulated workers in Cell.unity. Their palette textures and materials are ours, in Assets/_Project/Art/Workers | no (CC0) |
| Material Icons (Google), 11 icons | material-design-icons master (2026-11-04): notifications_active, fire_extinguisher, local_fire_department, my_location, directions_run, workspace_premium, military_tech, emoji_events, timer, check_circle, radio_button_unchecked; 48dp 2x PNG (96 px) | github.com/google/material-design-icons | Apache-2.0 (repo LICENSE) | OK | Assets/_Project/Art/Icons/ms_*.png (recoloured black to white, alpha kept, by us); fire drill badges and HUD (M15) | yes, notice in Credits + 07-Material-Icons-Apache-2.0.txt |
| MQTTnet | 4.3.7.1207 (netstandard2.1 build) | github.com/dotnet/MQTTnet, NuGet | MIT | OK | Assets/ThirdParty/MQTTnet (DLL + LICENSE vendored, in git via LFS) | notice in Credits |
| Large Warehouse/Factory Ambience (fimrod) | Freesound #278987, WAV 48 kHz/24-bit, 22.4 s | freesound.org/people/fimrod/sounds/278987/ | CC0 1.0 | OK | Assets/_Project/Audio/FactoryAmbience_fimrod_278987.wav (Git LFS); hall ambience loop | no (CC0) |
| Electric Sparks (kev_durr) | Freesound #396470, WAV 44.1 kHz/16-bit, 11.8 s | freesound.org/people/kev_durr/sounds/396470/ | CC0 1.0 | OK | Assets/_Project/Audio/ElectricSparks_kev_durr_396470.wav (Git LFS); spark crackle during a CNC fault | no (CC0) |
| AI4I 2020 Predictive Maintenance Dataset | 2020 | UCI ML Repository, doi:10.24432/C5HS5C | CC BY 4.0 | Conditional | Assets/_Project/Data/ai4i2020.csv, tools/replay | **yes** — Credits + web page (attribution with changes: `Assets/_Project/Licenses/01-AI4I2020-CC-BY-4.0.txt`) |
| Industrial Workshop Foundry HDRI | — | polyhaven.com/a/industrial_workshop_foundry | CC0 | OK | not in repo (candidate, unused) | no (optional) |
| ambientCG materials | per asset | ambientcg.com | CC0 1.0 | OK | not in repo (candidate, unused) | no (optional) |
| Unity Particle Pack | 3.1 | Unity Asset Store | Standard Asset Store EULA | OK | Assets/ThirdParty/ParticlePack (not in git) | no |
| TextMesh Pro essentials | bundled with com.unity.ugui 2.0.0 | Unity | Unity Companion License | OK | Assets/TextMesh Pro (imported by the Factory Training sample; TMP requires this path) | no |
| ProGrids | 3.0.3-preview.6 | Unity package com.unity.progrids, embedded (came in with the Factory Training sample) | Unity Companion License (its LICENSE.md) | OK | Packages/com.unity.progrids (editor-only, not shipped) | no |
| Sonniss #GameAudioGDC | per year | gdc.sonniss.com | Sonniss GDC bundle licence | OK (no redistribution, no AI training) | not in repo (candidate, unused; may never be committed) | no |
| Pixabay SFX | per clip | pixabay.com | Pixabay Content License | OK | not in repo (candidate, unused) | no |
| Sound Design Elements Whoosh SFX 052 (AudioPapkin) | Freesound #812689, WAV 96 kHz/32-bit, 2.4 s | freesound.org/people/AudioPapkin/sounds/812689/ | CC0 1.0 | OK | tools/video/sfx/Whoosh052_AudioPapkin_812689.wav (Git LFS); under the chapter-card reveals in the v03 video only | no (CC0) |
| Whoosh #1 (Kinoton) | Freesound #427823, WAV 48 kHz/24-bit, 0.8 s | freesound.org/people/Kinoton/sounds/427823/ | CC0 1.0 | OK | tools/video/sfx/Whoosh1_Kinoton_427823.wav (Git LFS); under the other masked reveals in the v03 video only | no (CC0) |
| Kokoro-82M TTS, voice `am_michael` | v1.0 (ONNX export, kokoro-onnx model-files-v1.1) | huggingface.co/hexgrad/Kokoro-82M via github.com/thewh1teagle/kokoro-onnx | Apache-2.0 (weights); kokoro-onnx 0.6.1 MIT; phonemizer/espeak-ng GPL-3 used locally for pronunciation only | OK | Not in repo; the generated voiceover is only in the M6 video (Builds/Recordings, git-ignored). Model card allows real-world deployment; the English voices carry no extra licence terms | no |

| OpenPLC Runtime | v4.3.1 | github.com/Autonomy-Logic/openplc-runtime | MIT | OK | not in repo (external tool on the studio PC); our programs `tools/plc/*.st` and `tools/plc/openplc_project/` | no |
| OpenPLC Editor | v4 | github.com/Autonomy-Logic/openplc-editor | GPL-3.0 | OK, a tool only | not in repo, never bundled; compiles and uploads `tools/plc/openplc_project/` | no |

Replaced 2026-10-04: M2MqttUnity (bundled Eclipse Paho M2Mqtt, last released Dec 2015, EPL-1.0, MQTT 3.1.1 only) was dropped for MQTTnet. MQTTnet 5.x targets net8.0+ only, which Unity 6000.3 (.NET Standard 2.1, Mono/IL2CPP) cannot load, so 4.3.7 is the newest usable release.

Add a row in the same commit as the file. Unknown licence = do not add the file.

Particle Pack 3.1 (Asset Store EULA) is now used in the build (M6): `SparksEffect` and `RisingSteam` instances in `Cell.unity`; compiled-only distribution, like the other Asset Store content. From M14 the fire drill also places `TinyFlames`, `MediumFlames`, `LargeFlames`, `SmokeEffect`, `ElectricalSparks` and (M15, the extinguisher spray) `PressurisedSteam` instances under `Cell/Fire` (scene overrides only: play on awake off, smoke darker and upright; the pack's files are unchanged). Four material copies, `Assets/_Project/Materials/Fire/*_NoSoft.mat`, are the pack's flame and smoke materials with soft particles off (the VR pipeline has no depth texture). They hold settings and GUID references to the pack's textures only, no texture data, and ship only inside the build like the pack itself.

Lettering on our own fire models (M15: the extinguisher labels, inspection tags, stand placards and floor marking, built by `tools/blender/fire_extinguishers.py` into `Assets/_Project/Art/Fire/Fire_Extinguishers.fbx`) is mesh geometry made from Inter, the font bundled with Blender (`datafiles/fonts/Inter.woff2`, SIL Open Font License 1.1). The OFL places no restriction on works made with the font; no font file is in the repo or the build.

`Assets/_Project/Scenes/Cell.unity` was started as a Save As of the Factory Training sample hall. It holds no sample meshes, textures or materials: only GUID references to Factory Training, realvirtual and Particle Pack prefabs, with their transforms and overrides, plus our own objects. It opens properly only once those packages are imported at the paths above.

Notices shipped with the build (M6): `Assets/_Project/Licenses/*.txt`, shown under Credits → Full licence texts and copied to `<build>/Licenses/` after every standalone build by `LicenseFilesPostprocessor`. Keep that folder in step with this register.
