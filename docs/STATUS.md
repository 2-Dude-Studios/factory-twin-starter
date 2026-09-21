# Status

**Current milestone:** M0 — Setup
**Last updated:** 2026-09-22

## Done
- Repo scaffold, CLAUDE.md, licence register, broker + replayer tooling.

## Pending (M0 exit: realvirtual demo conveyor moves inside our project)
- [ ] Create Unity 6000.3 URP project in this folder (Unity Hub → 3D (URP) → this path)
- [ ] Import Factory Training | Industry Sample from Asset Store → move into `Assets/ThirdParty/FactoryTraining/`
- [ ] Import realvirtual.io Digital Twin Starter 6 → `Assets/ThirdParty/realvirtual/`
- [ ] Add `com.unity.splines`, Cinemachine, Input System via Package Manager
- [ ] Add URDF-Importer by git URL: `https://github.com/Unity-Technologies/URDF-Importer.git?path=/com.unity.robotics.urdf-importer#v0.5.2`
- [ ] Import M2MqttUnity → `Assets/ThirdParty/M2MqttUnity/`
- [ ] `docker compose -f tools/broker/docker-compose.yml up -d` and confirm `mosquitto_sub -t 'cell/#'` connects
- [ ] `python tools/replay/replay.py --rate 5` streams AI4I rows
- [ ] Open realvirtual demo scene, press Play, conveyor moves

## Manual steps pending
(none)

## Blockers
(none)
