# factory-twin

A digital twin of one factory production cell, built in Unity 6 by [2 Dude Studios](https://2dudestudios.com). Parts come in on an infeed conveyor, pass a sensor gate, are picked and placed by a six-axis arm, and leave on an outfeed conveyor. A CNC machining centre and a press stand alongside. Every drive, sensor and joint publishes its state over MQTT to a live dashboard.

The cell demonstrates three use cases:

1. **Predictive maintenance.** The press's failure risk climbs, and the twin recommends a tool change before the failure. If the recommendation is ignored, the press fails and the line stops, in 3D and on the dashboard at the same time. CNC telemetry is replayed from the public AI4I 2020 dataset.
2. **Virtual commissioning.** A soft PLC (OpenPLC Runtime v4) runs the cell from Structured Text over Modbus TCP. A commissioning suite of ten scenarios catches a deliberately planted interlock bug, visible in 3D, and passes once the bug is fixed.
3. **Workforce training and safety.** A trainee who walks into the arm's protective zone freezes the arm, and the line stays held until a reset. A CNC fault can be cleared only by following the OSHA 1910.147 lockout/tagout steps: the PLC refuses to start while the lock is on, and every step is logged. It runs on desktop or in a Meta Quest 3 over Link, with hand tracking.

> **Simulated safety function, training demo.** Nothing here is safety-rated or certified. Real machine safeguarding needs rated hardware and a proper risk assessment.

## Requirements

- Windows, **Unity 6000.3.19f1** (URP).
- **Git LFS**: run `git lfs install` before cloning, or the models and sounds arrive as pointer files.
- Docker, for the MQTT broker (Eclipse Mosquitto).
- Python 3, for the dataset replayer.
- Optional: OpenPLC Runtime v4 and OpenPLC Editor v4, for virtual commissioning (see `tools/plc/README.md`).
- Optional: Meta Quest 3 with Link, for VR.

### Assets you must get yourself

These Asset Store packages are not redistributable, so they are not in the repo. Import each one at the path shown, which is already in `.gitignore`:

| Package | Version | Import path |
|---|---|---|
| realvirtual.io Digital Twin Starter 6 | 6.0.11 | its default paths, `Assets/realvirtual` and `Assets/Gizmos/realvirtual` |
| Factory Training \| Industry Sample (Unity) | 1.0 | `Assets/ThirdParty/FactoryTraining` |
| Unity Particle Pack | 3.1 | `Assets/ThirdParty/ParticlePack` |

Without realvirtual the project does not compile. Without Factory Training the scene (`Assets/_Project/Scenes/Cell.unity`) shows missing prefabs, because the scene holds only references to the sample's prefabs, not the sample's content.

## Run it

1. Start the broker: `docker compose -f tools/broker/docker-compose.yml up -d`. It listens on localhost only, with anonymous access, so keep it off public networks.
2. Open the project in Unity, open `Assets/_Project/Scenes/Cell.unity`, and press Play.
3. Optional: replay the dataset with `pip install -r tools/replay/requirements.txt`, then `python tools/replay/replay.py`.
4. Optional: run the cell from the soft PLC by following `tools/plc/README.md`.
5. Optional, for VR: connect the Quest 3 over Link, then start with `-vr` or press F11.

The broker address and port come from `Assets/_Project/Data/TwinConfig.asset`.

## What's where

- `Assets/_Project/`: all project code (`Scripts`, namespace `TwoDude.FactoryTwin.*`), UI Toolkit panels (`UI`), prefabs, the scene and the EditMode tests (`Tests`).
- `tools/`: the broker config, the dataset replayer, the PLC programs (`plc/cell.st`, plus the seeded bug in `plc/cell_buggy.st`), Blender sources and the video build scripts.
- `docs/`: the blueprint, build plan, demo script and licence register. `docs/STATUS.md` is the build log.
- `CLAUDE.md` and `.claude/`: the project guide and commands for Claude Code, which was used to build this.

## Licence and credits

2 Dude Studios' own code and content are under the [MIT licence](LICENSE). Third-party content keeps its own licence. See [LICENSE](LICENSE) and [docs/LICENSES.md](docs/LICENSES.md).

Credits:
- **AI4I 2020 Predictive Maintenance Dataset**: S. Matzka, UCI Machine Learning Repository, 2020, doi:10.24432/C5HS5C, CC BY 4.0. Replayed as the CNC's telemetry.
- **Arm kinematics and meshes**: ros-industrial/universal_robot (`ur_description`), BSD-3-Clause. Used without vendor branding. This project is not affiliated with or endorsed by Universal Robots.
- **Other components**: MQTTnet (MIT), Microsoft Rocketbox avatars (MIT), the Quaternius Animated Human (CC0), Freesound CC0 sounds and Unity's URDF Importer (Apache-2.0).
