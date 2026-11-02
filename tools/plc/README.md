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
| Sensors, press health, twin heartbeat, protective stop (M10), start and stop counts, CNC isolated and locked out (M11) | holding registers 1024–1033 | `%MW0`–`%MW9` | twin → PLC |
| Infeed, outfeed, pick command, stalled | coils 0–3 | `%QX0.0`–`%QX0.3` | PLC → twin |
| Line state, PLC heartbeat, parts out, cycle time (1/100 s), starts refused (M11) | holding registers 0–4 | `%QW0`–`%QW4` | PLC → twin |

The twin writes only holding registers: Modbus cannot write `%IX` inputs.

## Run it

1. Install the runtime (`OpenPLC_Runtime_4.3.1_Setup.exe`) and start it ("Start OpenPLC Runtime"). It listens on 8443 for the Editor.
2. `python sync_project.py` writes `openplc_project/pous/programs/main.st` from `cell.st` (main.st is generated, not committed).
3. In the OpenPLC Editor: Open `openplc_project/`. Device > Configuration is set to OpenPLC Runtime v4 at 127.0.0.1; press Connect. The first time, the runtime asks you to create its user; that account is yours and stays out of the repo. Servers > `twin_modbus` is the Modbus TCP slave on port 502, already enabled. It binds `0.0.0.0` and has no authentication (Modbus TCP has none): run it only on a trusted network, and set the bind to `127.0.0.1` when the PLC and the twin are on the same PC.
4. Build options > Build and upload. The console ends with "Compilation completed successfully", "PLC started", "Upload complete". The left-rail Stop/Start PLC button stops and starts it.
5. In Unity, Play. The PLC shadows the line straight away (it reads the sensors and counts parts) but drives nothing. Publish `cell/plc/source` = 1 to hand it the line, 0 to take it back. `cell/plc/link` shows whether the PLC answers.

A quick check without Unity: holding registers 0–3 should show the state and a heartbeat that counts up about 50 times a second (20 ms task). With Unity stopped, the state is 3 (held), because the twin's heartbeat is missing. States: 0 feeding, 1 transferring, 2 stalled, 3 held, 4 protective stop, 5 stopped.

If the PLC's heartbeat stops for longer than `TwinConfig`'s watchdog time (0.5 s) while it runs the line, the twin stops both conveyors. If the twin's heartbeat stops for 3 s, the PLC holds the line itself.

The safety zone's protective stop (M10) reaches the PLC as `safety_stop` (`%MW5`): the program stops everything at once in whatever state it is in, pauses its timers, and resumes that state when the twin clears the stop on a reset. The arm's own freeze comes from the safety zone in the twin and does not wait for the PLC.

Start and stop (M11) reach the PLC as press counts (`start_count` `%MW6`, `stop_count` `%MW7`): a press is the count going up by exactly one, so it cannot fall between two polls, and the count starting again from 0 when Unity restarts is not a press. Stop is orderly (a pick under way finishes). A start while `press_lockout` (`%MW9`) or `press_isolated` (`%MW8`) is set is refused and counted on `starts_refused` (`%QW4`); an isolated CNC also holds the line.

## Commissioning suite (M8)

In Play, publish `cell/vc/run` = 1 (or press Run tests on the Commissioning panel). The suite runs eight scenarios (two of them for the M10 safety zone) against whichever logic source runs the line and writes `Builds/Commissioning/vc_<source>_<time>.csv`. To show the bug being caught: `python sync_project.py cell_buggy.st <project dir>`, reopen the project in the Editor (it does not always reload `main.st` by itself), Build and upload, run the suite, then put `cell.st` back the same way. The I/O list is `io_list.csv` (Tools > Factory Twin > Export PLC I-O List).

