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
- Replayer verified against the broker with a synthetic 3-row CSV (33 messages, correct topics/payloads, paho-mqtt 2.1.0). Real dataset downloaded from UCI (10,000 rows; 339 failures: TWF 46, HDF 115, PWF 95, OSF 98, RNF 19) and verified through both `replay.py` and the C# `Ai4iCsv` parser. CC BY 4.0 — credit line still owed in the Credits panel (M4) and on the web page.
- Ahead of M3 (realvirtual-independent, so buildable now): `TopicPath`, `TopicFilter`, `SignalPayload`, `SignalBus`, `Ai4iCsv`, `DatasetReplayer`, `TwinConfig` (+ `Assets/_Project/Data/TwinConfig.asset`), `FrameRateLock`, in assembly `TwoDude.FactoryTwin.Twin`. 84 EditMode tests; compiles clean in the Editor; tests pass under `dotnet test` (NUnit 3). In-Editor run pending the allowlist step below.
- realvirtual Starter 6.0.11 (build 8592) imported at its default paths `Assets/realvirtual/` + `Assets/Gizmos/realvirtual/`; compiles with 0 errors under URP 17.3. Both paths are git-ignored; the project settings its importer changed (layers, physics matrix, `REALVIRTUAL` define) are committed.

## Pending (M0 exit: realvirtual demo conveyor moves inside our project)
- [ ] Import Factory Training | Industry Sample from Asset Store → move into `Assets/ThirdParty/FactoryTraining/`
- [ ] Import M2MqttUnity → `Assets/ThirdParty/M2MqttUnity/`
- [ ] Open `Assets/realvirtual/Scenes/DemoRealvirtual.unity`, press Play, conveyor moves (M0 exit)

## Manual steps pending
- UnitySkills panel (Window > UnitySkills → Settings → Allowlist): add `scene_create`, `scene_save`, `scene_load`, `test_run`, `test_run_by_name`. The plugin blocks these in Auto mode by design; until then Claude cannot create `Cell.unity` or run tests in the Editor.
- Restart the Editor once so the Linear colour space and Input System backend switch fully apply (Unity may prompt for this itself).
- Cinemachine is 3.x (namespace `Unity.Cinemachine`, `CinemachineCamera`, spline dolly on Unity Splines) — not the 2.x API most tutorials show.

## Blockers
(none)

## Decisions and risks
- **realvirtual location.** It imported to `Assets/realvirtual/`, not `Assets/ThirdParty/realvirtual/`. Recommendation: leave it there (Unity only reads gizmo icons from `Assets/Gizmos/`, and realvirtual's updater expects its default path) and extend the "never edit" rule in CLAUDE.md and `.claude/settings.json` to `Assets/realvirtual/**`. Awaiting confirmation.
- **Compatibility.** realvirtual 6.0.11 warned that Unity 6000.3.19f1 is outside its tested range; import was continued anyway. No compile errors, but watch for runtime oddities in its demo scene.
- **Assemblies (resolved).** realvirtual ships `realvirtual.base.asmdef`, so `TwoDude.FactoryTwin.Twin` can reference it and the `TwinSignals` facade can live in the Twin assembly as planned.
- realvirtual's asmdef references Cinemachine 2.x's assembly name (`Cinemachine`); we have 3.x (`Unity.Cinemachine`). Its optional Cinemachine helpers are therefore inactive — fine for us, our camera work uses 3.x directly.
