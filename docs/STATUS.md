# Status

**Current milestone:** M0 — Setup
**Last updated:** 2026-09-22

## Done
- Repo scaffold, CLAUDE.md, licence register, broker + replayer tooling.
- Git repo initialised (LFS hooks installed), scaffold committed.
- URP 17.3.0 enabled on the existing project: stock pipeline assets in `Assets/_Project/Settings/`, assigned in Graphics settings, Linear colour space. Verified in Editor 6000.3.19f1 — clean compile.
- Packages added: Splines 2.9.0, Cinemachine 3.1.7, Input System 1.19.0 (Active Input Handling = Both, so legacy-Input code in third-party assets keeps working), Test Framework 1.6.0, URDF-Importer v0.5.2 (compiles on 6000.3 with no fixes).

## Pending (M0 exit: realvirtual demo conveyor moves inside our project)
- [ ] Import Factory Training | Industry Sample from Asset Store → move into `Assets/ThirdParty/FactoryTraining/`
- [ ] Import realvirtual.io Digital Twin Starter 6 → `Assets/ThirdParty/realvirtual/`
- [ ] Import M2MqttUnity → `Assets/ThirdParty/M2MqttUnity/`
- [ ] `docker compose -f tools/broker/docker-compose.yml up -d` and confirm `mosquitto_sub -t 'cell/#'` connects
- [ ] `python tools/replay/replay.py --rate 5` streams AI4I rows
- [ ] Open realvirtual demo scene, press Play, conveyor moves

## Manual steps pending
- Restart the Editor once so the Linear colour space and Input System backend switch fully apply (Unity may prompt for this itself).
- Cinemachine is 3.x (namespace `Unity.Cinemachine`, `CinemachineCamera`, spline dolly on Unity Splines) — not the 2.x API most tutorials show.

## Blockers
(none)
