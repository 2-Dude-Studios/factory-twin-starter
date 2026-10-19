#!/usr/bin/env python3
"""Replay the AI4I 2020 dataset as live machine telemetry over MQTT.

Each CSV row becomes one tick of the 'press' station. Fault columns (TWF, HDF,
PWF, OSF, RNF) are published as booleans so the Unity fault injector and the
dashboard can react. Values keep the dataset's units (K, rpm, Nm, min).

Usage:
  python tools/replay/replay.py                    # 2 rows/sec, loop forever
  python tools/replay/replay.py --rate 10 --once
  python tools/replay/replay.py --fault TWF        # only rows where TWF == 1
  python tools/replay/replay.py --host 192.168.1.20

Dataset: AI4I 2020 Predictive Maintenance Dataset, UCI ML Repository,
doi:10.24432/C5HS5C, CC BY 4.0. Place ai4i2020.csv at Assets/_Project/Data/.
"""
import argparse, csv, json, sys, time
from pathlib import Path

try:
    import paho.mqtt.client as mqtt
except ImportError:
    sys.exit("pip install paho-mqtt")

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CSV = ROOT / "Assets" / "_Project" / "Data" / "ai4i2020.csv"

FIELDS = {
    "Air temperature [K]": "air_temp_k",
    "Process temperature [K]": "process_temp_k",
    "Rotational speed [rpm]": "speed_rpm",
    "Torque [Nm]": "torque_nm",
    "Tool wear [min]": "tool_wear_min",
}
FAULTS = ["TWF", "HDF", "PWF", "OSF", "RNF"]
# AI4I product type -> ProductQuality value in Unity (sets the overstrain limit). Published first in each row.
QUALITY = {"L": 0, "M": 1, "H": 2}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv", type=Path, default=DEFAULT_CSV)
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=1883)
    ap.add_argument("--station", default="press")
    ap.add_argument("--rate", type=float, default=2.0, help="rows per second")
    ap.add_argument("--fault", choices=FAULTS, help="only replay rows with this fault set")
    ap.add_argument("--once", action="store_true", help="stop at end of file instead of looping")
    a = ap.parse_args()

    if not a.csv.exists():
        sys.exit(f"CSV not found: {a.csv}")

    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id="ai4i-replayer")
    client.connect(a.host, a.port, keepalive=30)
    client.loop_start()
    base = f"cell/{a.station}"
    period = 1.0 / a.rate

    while True:
        with a.csv.open(newline="", encoding="utf-8-sig") as f:
            for row in csv.DictReader(f):
                if a.fault and row.get(a.fault) != "1":
                    continue
                now = int(time.time() * 1000)
                client.publish(f"{base}/product_quality", json.dumps({"v": QUALITY[row["Type"]], "t": now}))
                for col, sig in FIELDS.items():
                    client.publish(f"{base}/{sig}", json.dumps({"v": float(row[col]), "t": now}))
                for fcode in FAULTS:
                    client.publish(f"{base}/fault_{fcode.lower()}", json.dumps({"v": row[fcode] == "1", "t": now}))
                client.publish(f"{base}/machine_failure", json.dumps({"v": row["Machine failure"] == "1", "t": now}))
                time.sleep(period)
        if a.once:
            break

    client.loop_stop()
    client.disconnect()


if __name__ == "__main__":
    main()
