# Factory Digital Twin: one twin, three proofs

A working digital twin of one factory production cell, built in Unity 6 by [2 Dude Studios](https://2dudestudios.com/manufacturing). The same cell shows three use cases: predictive maintenance, virtual commissioning against a real soft PLC, and lockout/tagout safety training in VR.

**Watch first:** [the playlist](https://www.youtube.com/playlist?list=PLRKxyYfU9K7U), or one part at a time:

| Part | Video | What it shows |
|---|---|---|
| 1 | [Predictive maintenance](https://youtu.be/elzFxAVOdHU) | The warning comes 34 seconds before the press fails; if it is ignored, the line stops |
| 2 | [Virtual commissioning](https://youtu.be/KSvkD8MWyJQ) | A soft PLC runs the cell, and a ten-test suite catches a planted interlock bug in 1.4 s |
| 3 | [Training and safety in VR](https://youtu.be/Tp4IIKecFys) | A protective zone that freezes the arm, and the OSHA 1910.147 lockout steps in a Quest 3 |

> **Simulated safety function, training demo.** Nothing here is safety-rated or certified. Guarding a real machine needs rated hardware and a proper risk assessment.

---

## What the cell is

Parts arrive on an infeed conveyor and pass a sensor gate. A six-axis arm picks each one and places it on an outfeed conveyor. A CNC machining centre (the "press") stands alongside, with telemetry replayed from the public AI4I 2020 dataset.

Every drive, sensor and joint publishes its state as a signal, and a live dashboard reads those signals:

```
 3D simulation (realvirtual drives and sensors, PhysX, the arm)
        │  realvirtual Signals are the only bridge between the scene and the data
        ▼
 Signal bus in the twin ──► MQTT  cell/<station>/<signal>  {"v": <value>, "t": <unix ms>}
        │                        e.g. cell/infeed/drive_speed, cell/arm/j3_deg, cell/press/tool_wear_min
        ├──► Dashboard (UI Toolkit): line state, parts per hour, failure risk, training progress
        └──► Modbus TCP ◄──► OpenPLC Runtime (optional): the PLC can run the line instead of the twin's own logic
```

### The three use cases

1. **Predictive maintenance.** A risk model watches the CNC's tool wear, torque and temperatures. When the failure risk crosses a threshold, the dashboard recommends a tool change before the failure happens. If you change the tool, the line keeps running. If you ignore the warning, the tool fails: the stack light turns red, sparks fly and the line stops, in 3D and on the dashboard at the same moment.
2. **Virtual commissioning.** The cell's control logic also exists as Structured Text (`tools/plc/cell.st`), which runs in OpenPLC Runtime v4 and talks to the twin over Modbus TCP. A commissioning suite plays ten scenarios against whichever logic runs the line (the twin's own or the PLC's) and grades each check. A deliberately buggy program (`cell_buggy.st`) fails the suite in the 3D view; the fixed one passes all ten.
3. **Workforce training and safety.** A trainee who walks into the arm's protective zone freezes the arm and holds the line until someone outside the zone presses reset. A CNC fault can be cleared only by working through the lockout/tagout steps in order: isolate, lock and tag, bleed the air, verify, then change the tool. The PLC refuses to start while the lock is on, and every step and mistake goes into a training record. It runs on desktop or in a Meta Quest 3 over Link, with controllers or tracked hands.

---

## What you need

| | Required? | Notes |
|---|---|---|
| Windows 10/11 | yes | The only platform it is built and tested on |
| [Unity 6000.3.19f1](https://unity.com/releases/editor/archive) with URP | yes | Install through Unity Hub. Other 6000.3 versions may work |
| [Git LFS](https://git-lfs.com) | yes | Models, textures and sounds are stored in LFS |
| Three Asset Store packages | yes | See below. They are not redistributable, so they are not in this repo |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | optional | Runs the MQTT broker, which external tools use to see the twin's data. The twin and the dashboard run without it |
| Python 3.10+ | optional | For the dataset replayer and the PLC project sync |
| [OpenPLC Runtime v4 and Editor v4](https://autonomylogic.com) | optional | Only for running the cell from the soft PLC (use case 2) |
| Meta Quest 3 with Link | optional | Only for the VR version of use case 3 |

### Asset Store packages

Get these from the Unity Asset Store and import each one at the path shown. The paths are already in `.gitignore`, so the files stay out of git.

| Package | Version | Import into |
|---|---|---|
| realvirtual.io Digital Twin Starter 6 | 6.0.11 | its default paths (`Assets/realvirtual`, `Assets/Gizmos/realvirtual`) |
| Factory Training \| Industry Sample (Unity) | 1.0 | `Assets/ThirdParty/FactoryTraining` |
| Unity Particle Pack | 3.1 | `Assets/ThirdParty/ParticlePack` |

Without realvirtual the project does not compile. The scene holds only references to the Factory Training prefabs, not their content, so without that package the hall shows missing prefabs.

---

## Set it up

1. **Clone with LFS:**
   ```bash
   git lfs install
   ```
   ```bash
   git clone https://github.com/2-Dude-Studios/factory-twin-starter.git
   ```
2. **Open the folder in Unity Hub** (Add → the cloned folder) with 6000.3.19f1. The first import takes a while. Expect compile errors until step 3 is done; if Unity offers Safe Mode, choose **Ignore**.
3. **Import the three Asset Store packages** at the paths in the table above. Window → Package Manager → My Assets → Download → Import.
4. **Open the scene** `Assets/_Project/Scenes/Cell.unity`.
5. Optional: **start the broker**:
   ```bash
   docker compose -f tools/broker/docker-compose.yml up -d
   ```
   It listens on localhost only and allows anonymous connections, so don't expose it to a network you don't trust. The address and port the twin uses are set in `Assets/_Project/Data/TwinConfig.asset`.

---

## Try it

### 1. Predictive maintenance (about 5 minutes)

1. Press **Play**. Let the line run for two or three minutes, until parts per hour settles near 510.
2. Press **1** to wear the tool out. The failure risk on the dashboard climbs, and a tool-change recommendation appears before the failure.
3. Press **2** to follow the recommendation and change the tool, or do nothing and watch the press fail and the line stop.
4. **3** clears faults and **0** starts a fresh run.

### 2. Virtual commissioning

**Without a PLC:** in Play mode, press **Run tests** on the Commissioning panel. The suite runs against the twin's own logic and writes its report to `Builds/Commissioning/`. The "PLC link lost" test applies only when the PLC runs the line.

**With the soft PLC:**
1. Install and start OpenPLC Runtime v4.
2. Write the PLC program into the Editor project:
   ```bash
   python tools/plc/sync_project.py
   ```
3. In OpenPLC Editor v4, open `tools/plc/openplc_project/`, press **Connect** (127.0.0.1), then **Build and upload**. The first time, the runtime asks you to create its user.
4. Press Play in Unity. The PLC shadows the line straight away. To hand it control, set **Start Source** on `Cell > PlcLink`, or publish:
   ```bash
   docker exec factory-twin-broker mosquitto_pub -t cell/plc/source -m '{"v":1,"t":0}'
   ```
5. Press **Run tests**. To see the bug being caught, load the buggy program (`python tools/plc/sync_project.py cell_buggy.st`), reopen the project in the Editor, build and upload, and run the suite again. Then put `cell.st` back the same way.

`tools/plc/README.md` has the Modbus address map, the watchdogs and more detail.

### 3. Training and safety

**Desktop:**
1. In Play mode, press **F5** to become the trainee. Use the arrow keys to walk (Shift to go faster), and hold the right mouse button to look around.
2. Walk into the arm's zone: the arm freezes and the line holds. Walk out, then press **F6** (or Safety reset) to restart it.
3. Press **Start lockout training** on the dashboard or the training board, and follow the steps. Records are saved to `Builds/Training/lockout_*.csv`.

**Quest 3 over Link:**
1. Put the headset on and start Link.
2. Press **F11** in Play mode. In a Windows build, start `FactoryTwin.exe -vr` instead; add `-logfps` to write frame times to `Player.log`.
3. Grab the props (isolator, padlock and tag, air valve, tool) with the controllers or your hands.

If the log says "OpenXR did not start", the headset was not in Link yet. Put it on and try again.

### Keys

| Key | Action |
|---|---|
| 1 / 2 / 3 / 0 | Wear the tool out / change the tool / clear faults / fresh run |
| 4–9 | Camera shots |
| F5 | Trainee view (arrow keys walk, Shift is faster, hold right mouse to look) |
| F6 / F7 / F8 | Safety reset / line start / line stop |
| F9 / F10 | FPS readout / show or hide the cursor |
| F11 | Switch to VR (Quest 3 over Link) |

### Watch the data

With the broker running, see every topic live:

```bash
docker exec factory-twin-broker mosquitto_sub -t 'cell/#' -v
```

Or replay the dataset over MQTT from outside Unity:

```bash
pip install -r tools/replay/requirements.txt
```
```bash
python tools/replay/replay.py --rate 10
```

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| Hundreds of compile errors, or the Safe Mode prompt | realvirtual is missing or at the wrong path |
| Pink or missing objects all over the hall | Factory Training is missing, or not in `Assets/ThirdParty/FactoryTraining` |
| Models or sounds fail to import; tiny text files where `.fbx` or `.wav` files should be | Git LFS was not installed before cloning. Run `git lfs install`, then `git lfs pull` |
| The PLC never takes the line (`cell/plc/link` stays 0) | OpenPLC Runtime isn't running, or no program has been uploaded |
| VR doesn't start | Link wasn't active when you pressed F11; see the training section |

---

## How it is built

- **One rule holds it together:** scripts never read a conveyor's transform to find out its state. They read the realvirtual signal. Every value goes through one signal bus and is published as `cell/<station>/<signal>`.
- **Code:** everything is under `Assets/_Project/`. The namespaces are `TwoDude.FactoryTwin.*`, and only one assembly references realvirtual. Each MonoBehaviour has one responsibility, and the UI is UI Toolkit with runtime data binding.
- **Tests:** the EditMode tests in `Assets/_Project/Tests/` cover the signal bus, topic parsing, the dataset replayer, the cell sequence, the risk model, the PLC link, the commissioning checks, the safety zone and the lockout procedure. Run them from Window → General → Test Runner, or headless:
  ```bash
  "C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults tools/test-results.xml
  ```
- **Docs:**
  - [`docs/BLUEPRINT.md`](docs/BLUEPRINT.md): the full design, including the topic catalogue, the architecture and the control logic.
  - [`docs/demo-script.md`](docs/demo-script.md): the demo beats.
  - [`docs/LICENSES.md`](docs/LICENSES.md): the licence register.
  - [`docs/build-plan.md`](docs/build-plan.md) and [`docs/STATUS.md`](docs/STATUS.md): the build plan and the build log.
- **Built with Claude Code:** `CLAUDE.md` and `.claude/` are the project guide and commands it used.

| Folder | What's in it |
|---|---|
| `Assets/_Project/Scripts` | Twin, robot, UI and XR code |
| `Assets/_Project/UI` | UXML and USS for the dashboard, the commissioning panel and the VR panels |
| `Assets/_Project/Robots` | The cell arm (URDF and meshes) |
| `Assets/_Project/Data` | `TwinConfig`, the PLC I/O map and the AI4I 2020 dataset |
| `tools/broker` | Mosquitto config and Docker Compose |
| `tools/plc` | `cell.st`, `cell_buggy.st`, the OpenPLC Editor project and the I/O list |
| `tools/replay` | The MQTT dataset replayer |
| `tools/blender` | Blender sources for the lockout props |

---

## Licence and credits

2 Dude Studios' own code and content are under the [MIT licence](LICENSE). Third-party content keeps its own licence: see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and [docs/LICENSES.md](docs/LICENSES.md).

- **AI4I 2020 Predictive Maintenance Dataset**: S. Matzka, UCI Machine Learning Repository, 2020, doi:10.24432/C5HS5C, CC BY 4.0. Replayed as the CNC's telemetry.
- **Arm kinematics and meshes**: ros-industrial/universal_robot (`ur_description`), BSD-3-Clause. Used without vendor branding. This project is not affiliated with or endorsed by Universal Robots.
- **Other components**: MQTTnet (MIT), Microsoft Rocketbox avatars (MIT), the Quaternius Animated Human (CC0), Freesound CC0 sounds and Unity's URDF Importer (Apache-2.0).

## Want one of your own line?

2 Dude Studios builds digital twins, virtual commissioning and VR training for manufacturers. See [2dudestudios.com/manufacturing](https://2dudestudios.com/manufacturing), or [book a 30-minute demo call](https://cal.com/akaash-garg-xvijkt/hi) and we'll run the twin live.
