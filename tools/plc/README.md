# Soft PLC (M7)

The cell can run from a soft PLC instead of the in-Unity `CellSequencer`. The PLC program is `cell.st`, the same sequence as `CellSequence.cs`. The twin talks to it over Modbus TCP through `PlcLink`.

## What runs where

| Piece | Where | Licence |
|---|---|---|
| OpenPLC Runtime v4.3.1 | Studio PC, outside the repo (Windows installer, MSYS2 inside) | MIT |
| OpenPLC Editor v4 | Studio PC, outside the repo. Runtime v4 has no web upload; the Editor compiles the program and uploads it | GPL-3.0, a tool only, never bundled |
| `cell.st` | This folder | Our code |
| `PlcLink`, `ModbusClient`, `PlcIoMap` | `Assets/_Project/Scripts/Twin/Plc/` | Our code, no package |

## Addresses

OpenPLC's Modbus slave layout, mirrored in `Assets/_Project/Data/PlcIoMap.asset`:

| Area | Modbus | IEC | Direction |
|---|---|---|---|
| Sensors, press health, twin heartbeat | holding registers 1024–1028 | `%MW0`–`%MW4` | twin → PLC |
| Infeed, outfeed, pick command, stalled | coils 0–3 | `%QX0.0`–`%QX0.3` | PLC → twin |
| Line state, PLC heartbeat, parts out, cycle time (1/100 s) | holding registers 0–3 | `%QW0`–`%QW3` | PLC → twin |

The twin writes only holding registers: Modbus cannot write `%IX` inputs.

## Run it

1. Install the runtime (`OpenPLC_Runtime_4.3.1_Setup.exe`) and start it ("Start OpenPLC Runtime"). It listens on 8443 for the Editor.
2. `python sync_project.py` writes `openplc_project/pous/programs/main.st` from `cell.st` (main.st is generated, not committed).
3. In the OpenPLC Editor: Open `openplc_project/`. Device > Configuration is set to OpenPLC Runtime v4 at 127.0.0.1; press Connect. The first time, the runtime asks you to create its user; that account is yours and stays out of the repo. Servers > `twin_modbus` is the Modbus TCP slave on port 502, already enabled.
4. Build options > Build and upload. The console ends with "Compilation completed successfully", "PLC started", "Upload complete". The left-rail Stop/Start PLC button stops and starts it.
5. In Unity, Play. The PLC shadows the line straight away (it reads the sensors and counts parts) but drives nothing. Publish `cell/plc/source` = 1 to hand it the line, 0 to take it back. `cell/plc/link` shows whether the PLC answers.

A quick check without Unity: holding registers 0–3 should show the state and a heartbeat that counts up about 50 times a second (20 ms task). With Unity stopped, the state is 3 (held), because the twin's heartbeat is missing.

If the PLC's heartbeat stops for longer than `TwinConfig`'s watchdog time (0.5 s) while it runs the line, the twin stops both conveyors. If the twin's heartbeat stops for 3 s, the PLC holds the line itself.

## Commissioning suite (M8)

In Play, publish `cell/vc/run` = 1 (or press Run tests on the Commissioning panel). The suite runs six scenarios against whichever logic source runs the line and writes `Builds/Commissioning/vc_<source>_<time>.csv`. To show the bug being caught: `python sync_project.py cell_buggy.st <project dir>`, reopen the project in the Editor (it does not always reload `main.st` by itself), Build and upload, run the suite, then put `cell.st` back the same way. The I/O list is `io_list.csv` (Tools > Factory Twin > Export PLC I-O List).

