# Third-party licence register

Rules (from the build plan):
1. Asset Store and Fab content ships only inside built applications; raw files are never handed to a client.
2. realvirtual is distributed compiled only; never share its source, prefabs or scenes.
3. Every CC BY item gets a credit line in the app's Credits panel and on the web page.
4. Vendor branding (UR, KUKA, ABB, FANUC) is removed from any model shown publicly until vendor terms are confirmed.
5. When a client project starts on this codebase: buy the Unity Industry seat, upgrade realvirtual to Professional if the client needs OPC UA/PLC, and re-run this list.

Full tracker with 62 candidates and verdicts: https://claude.ai/artifact/AGRYueGCfv6UwgTbnQcdre

| Item | Version | Source | Licence | Verdict | Where in repo | Credit required |
|---|---|---|---|---|---|---|
| Factory Training \| Industry Sample | 1.0 (9 Feb 2026) | Unity Asset Store | Standard Asset Store EULA (Extension Asset) | OK | Assets/ThirdParty/FactoryTraining (not in git) | no |
| realvirtual.io Digital Twin Starter 6 | 6.0.11 | Unity Asset Store + realvirtual EULA | Compiled-only distribution, single-user commercial | Conditional | Assets/realvirtual + Assets/Gizmos/realvirtual — importer's default paths (not in git, see .gitignore). Prefabs used in Cell.unity: ConveyorSmall, sensors, Source/Sink, RobotStand, and (M4) `3DPrefabs/CNC.prefab` as the press station's machine. It carries only generic safety stickers, no vendor logos. | no |
| Unity Splines | 2.9.0 (minimum bundled with 6000.3.19f1) | Unity registry | Unity Companion License | OK | Packages/manifest.json | no |
| Universal RP | 17.3.0 | Unity registry (built-in) | Unity Companion License | OK | Packages/manifest.json; stock pipeline assets from Unity's URP template in Assets/_Project/Settings | no |
| Cinemachine | 3.1.7 | Unity registry | Unity Companion License | OK | Packages/manifest.json | no |
| Input System | 1.19.0 | Unity registry | Unity Companion License | OK | Packages/manifest.json | no |
| OpenXR Plugin (com.unity.xr.openxr) | 1.18.0 | Unity registry | Unity Companion License | OK | Packages/manifest.json (M12, approved 2026-10-24; Quest 3 over Link) | no |
| XR Plugin Management (com.unity.xr.management) | 4.6.1 | Unity registry | Unity Companion License | OK | Packages/manifest.json (M12); its settings assets in Assets/XR | no |
| XR Interaction Toolkit (com.unity.xr.interaction.toolkit) | 3.6.1 (pulls in com.unity.xr.core-utils, com.unity.xr.legacyinputhelpers) | Unity registry | Unity Companion License | OK | Packages/manifest.json (M12); its project settings in Assets/XRI | no |
| XR Interaction Toolkit Starter Assets sample | 3.6.1 | Unity (sample of the package above) | Unity Companion License | OK | Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets (Unity's fixed sample path; the XR Origin rig, input actions, teleport and turn) | no |
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
| MQTTnet | 4.3.7.1207 (netstandard2.1 build) | github.com/dotnet/MQTTnet, NuGet | MIT | OK | Assets/ThirdParty/MQTTnet (DLL + LICENSE vendored, in git via LFS) | notice in Credits |
| Large Warehouse/Factory Ambience (fimrod) | Freesound #278987, WAV 48 kHz/24-bit, 22.4 s | freesound.org/people/fimrod/sounds/278987/ | CC0 1.0 | OK | Assets/_Project/Audio/FactoryAmbience_fimrod_278987.wav (Git LFS); hall ambience loop | no (CC0) |
| Electric Sparks (kev_durr) | Freesound #396470, WAV 44.1 kHz/16-bit, 11.8 s | freesound.org/people/kev_durr/sounds/396470/ | CC0 1.0 | OK | Assets/_Project/Audio/ElectricSparks_kev_durr_396470.wav (Git LFS); spark crackle during a CNC fault | no (CC0) |
| AI4I 2020 Predictive Maintenance Dataset | 2020 | UCI ML Repository, doi:10.24432/C5HS5C | CC BY 4.0 | Conditional | Assets/_Project/Data/ai4i2020.csv, tools/replay | **yes** — Credits + web page (attribution with changes: `Assets/_Project/Licenses/01-AI4I2020-CC-BY-4.0.txt`) |
| Industrial Workshop Foundry HDRI | — | polyhaven.com/a/industrial_workshop_foundry | CC0 | OK | Assets/_Project/Lighting | no (optional) |
| ambientCG materials | per asset | ambientcg.com | CC0 1.0 | OK | Assets/_Project/Materials | no (optional) |
| Unity Particle Pack | 3.1 | Unity Asset Store | Standard Asset Store EULA | OK | Assets/ThirdParty/ParticlePack (not in git) | no |
| TextMesh Pro essentials | bundled with com.unity.ugui 2.0.0 | Unity | Unity Companion License | OK | Assets/TextMesh Pro (imported by the Factory Training sample; TMP requires this path) | no |
| ProGrids | 3.0.3-preview.6 | Unity (embedded by the Factory Training sample) | Unity Companion License | OK | Packages/com.unity.progrids (editor-only, not shipped) | no |
| Sonniss #GameAudioGDC | per year | gdc.sonniss.com | Sonniss GDC bundle licence | OK (no redistribution, no AI training) | Assets/_Project/Audio | no |
| Pixabay SFX | per clip | pixabay.com | Pixabay Content License | OK | Assets/_Project/Audio | no |
| Sound Design Elements Whoosh SFX 052 (AudioPapkin) | Freesound #812689, WAV 96 kHz/32-bit, 2.4 s | freesound.org/people/AudioPapkin/sounds/812689/ | CC0 1.0 | OK | tools/video/sfx/Whoosh052_AudioPapkin_812689.wav (Git LFS); under the chapter-card reveals in the v03 video only | no (CC0) |
| Whoosh #1 (Kinoton) | Freesound #427823, WAV 48 kHz/24-bit, 0.8 s | freesound.org/people/Kinoton/sounds/427823/ | CC0 1.0 | OK | tools/video/sfx/Whoosh1_Kinoton_427823.wav (Git LFS); under the other masked reveals in the v03 video only | no (CC0) |
| Kokoro-82M TTS, voice `am_michael` | v1.0 (ONNX export, kokoro-onnx model-files-v1.1) | huggingface.co/hexgrad/Kokoro-82M via github.com/thewh1teagle/kokoro-onnx | Apache-2.0 (weights); kokoro-onnx 0.6.1 MIT; phonemizer/espeak-ng GPL-3 used locally for pronunciation only | OK | Not in repo; the generated voiceover is only in the M6 video (Builds/Recordings, git-ignored). Model card allows real-world deployment; the English voices carry no extra licence terms | no |

Replaced 2026-10-04: M2MqttUnity (bundled Eclipse Paho M2Mqtt, last released Dec 2015, EPL-1.0, MQTT 3.1.1 only) was dropped for MQTTnet. MQTTnet 5.x targets net8.0+ only, which Unity 6000.3 (.NET Standard 2.1, Mono/IL2CPP) cannot load, so 4.3.7 is the newest usable release.

Add a row in the same commit as the file. Unknown licence = do not add the file.

Particle Pack 3.1 (Asset Store EULA) is now used in the build (M6): `SparksEffect` and `RisingSteam` instances in `Cell.unity`; compiled-only distribution, like the other Asset Store content.

Notices shipped with the build (M6): `Assets/_Project/Licenses/*.txt`, shown under Credits → Full licence texts and copied to `<build>/Licenses/` after every standalone build by `LicenseFilesPostprocessor`. Keep that folder in step with this register.
