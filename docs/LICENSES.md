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
| realvirtual.io Digital Twin Starter 6 | 6.0.11 | Unity Asset Store + realvirtual EULA | Compiled-only distribution, single-user commercial | Conditional | Assets/ThirdParty/realvirtual (not in git) | no |
| Unity Splines | 2.8.4 | Unity registry | Unity Companion License | OK | Packages/manifest.json | no |
| URDF-Importer | v0.5.2 | github.com/Unity-Technologies/URDF-Importer | Apache-2.0 | OK | Packages/manifest.json (git URL) | notice in Credits |
| universal_robot (UR5e/UR10e URDF + meshes) | noetic-devel | github.com/ros-industrial/universal_robot | Apache-2.0 + BSD-3-Clause | OK (not UR15/18/20/30/8LONG) | Assets/_Project/Robots/ur (de-branded) | notice in Credits |
| Fast IK | 1.2 | Unity Asset Store | Standard Asset Store EULA | OK | Assets/ThirdParty/FastIK (not in git) | no |
| M2MqttUnity | main | github.com/gpvigano/M2MqttUnity | MIT (bundled M2Mqtt: EPL-1.0) | Conditional (do not modify the EPL lib) | Assets/ThirdParty/M2MqttUnity | notice in Credits |
| AI4I 2020 Predictive Maintenance Dataset | 2020 | UCI ML Repository, doi:10.24432/C5HS5C | CC BY 4.0 | Conditional | Assets/_Project/Data/ai4i2020.csv, tools/replay | **yes** — Credits + web page |
| Industrial Workshop Foundry HDRI | — | polyhaven.com/a/industrial_workshop_foundry | CC0 | OK | Assets/_Project/Lighting | no (optional) |
| ambientCG materials | per asset | ambientcg.com | CC0 1.0 | OK | Assets/_Project/Materials | no (optional) |
| Unity Particle Pack | 3.1 | Unity Asset Store | Standard Asset Store EULA | OK | Assets/ThirdParty/ParticlePack (not in git) | no |
| Sonniss #GameAudioGDC | per year | gdc.sonniss.com | Sonniss GDC bundle licence | OK (no redistribution, no AI training) | Assets/_Project/Audio | no |
| Pixabay SFX | per clip | pixabay.com | Pixabay Content License | OK | Assets/_Project/Audio | no |

Add a row in the same commit as the file. Unknown licence = do not add the file.
