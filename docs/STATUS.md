# Status

**Current milestone:** M0 — Setup
**Last updated:** 2026-09-22

## Done
- Repo scaffold, CLAUDE.md, licence register, broker + replayer tooling.
- Git repo initialised (LFS hooks installed), scaffold committed.
- URP 17.3.0 enabled on the existing project: stock pipeline assets in `Assets/_Project/Settings/`, assigned in Graphics settings, Linear colour space. Verified in Editor 6000.3.19f1 — clean compile.
- Packages added: Splines 2.9.0, Cinemachine 3.1.7, Input System 1.19.0 (Active Input Handling = Both, so legacy-Input code in third-party assets keeps working), Test Framework 1.6.0, URDF-Importer v0.5.2 (compiles on 6000.3 with no fixes).
- UnitySkills v2.8.4 installed; REST server on localhost:8090 (mode: auto). Lets Claude Code drive the Editor.
- Broker verified: `docker compose up -d`, pub/sub round-trip on `cell/#` (using `docker exec factory-twin-broker mosquitto_sub/pub` — no host Mosquitto install needed).
- Replayer verified against the broker with a synthetic 3-row CSV (33 messages, correct topics/payloads, paho-mqtt 2.1.0). Real dataset still to download.
- Ahead of M3 (realvirtual-independent, so buildable now): `TopicPath`, `TopicFilter`, `SignalPayload`, `SignalBus`, `Ai4iCsv`, `DatasetReplayer`, `TwinConfig` (+ `Assets/_Project/Data/TwinConfig.asset`), `FrameRateLock`, in assembly `TwoDude.FactoryTwin.Twin`. 84 EditMode tests; compiles clean in the Editor; tests pass under `dotnet test` (NUnit 3). In-Editor run pending the allowlist step below.

## Pending (M0 exit: realvirtual demo conveyor moves inside our project)
- [ ] Import Factory Training | Industry Sample from Asset Store → move into `Assets/ThirdParty/FactoryTraining/`
- [ ] Import realvirtual.io Digital Twin Starter 6 → `Assets/ThirdParty/realvirtual/`
- [ ] Import M2MqttUnity → `Assets/ThirdParty/M2MqttUnity/`
- [ ] Download `ai4i2020.csv` (UCI, doi:10.24432/C5HS5C) → `Assets/_Project/Data/`, then `python tools/replay/replay.py --rate 5`
- [ ] Open realvirtual demo scene, press Play, conveyor moves

## Manual steps pending
- UnitySkills panel (Window > UnitySkills → Settings → Allowlist): add `scene_create`, `scene_save`, `scene_load`, `test_run`, `test_run_by_name`. The plugin blocks these in Auto mode by design; until then Claude cannot create `Cell.unity` or run tests in the Editor.
- Restart the Editor once so the Linear colour space and Input System backend switch fully apply (Unity may prompt for this itself).
- Cinemachine is 3.x (namespace `Unity.Cinemachine`, `CinemachineCamera`, spline dolly on Unity Splines) — not the 2.x API most tutorials show.

## Blockers
(none)

## Open design note
- `TwoDude.FactoryTwin.Twin` is an asmdef (tests require one). If realvirtual Starter ships without asmdefs, the `TwinSignals` facade cannot live in this assembly and will need its own home in Assembly-CSharp. Decide when realvirtual is imported.
