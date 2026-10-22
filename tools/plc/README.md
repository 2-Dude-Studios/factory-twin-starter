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

1. Install the runtime (`OpenPLC_Runtime_4.3.1_Setup.exe`) and start it.
2. In the OpenPLC Editor: new project, paste `cell.st` into a Structured Text POU, connect to the runtime, enable the Modbus TCP slave (port 502), compile and upload, start the PLC.
3. In Unity: `TwinConfig` has the PLC host and port. Play, then publish `cell/plc/source` = 1 to hand the line to the PLC (0 hands it back). `cell/plc/link` shows whether the PLC answers.

If the PLC's heartbeat stops for longer than `TwinConfig`'s watchdog time (0.5 s) while it runs the line, the twin stops both conveyors. If the twin's heartbeat stops for 3 s, the PLC holds the line itself.
