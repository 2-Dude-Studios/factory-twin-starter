using System.Collections.Generic;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Soft PLC link: the I/O list (Assets/_Project/Data/PlcIoMap.asset). Each row binds an OpenPLC Modbus address
    /// to a bus topic; tools/plc/cell.st declares the same addresses as located variables. Addresses follow
    /// OpenPLC's Modbus slave: coils are %QX, holding registers 0–1023 are %QW, 1024 onwards are %MW.
    /// </summary>
    [CreateAssetMenu(menuName = "Factory Twin/PLC I/O Map", fileName = "PlcIoMap")]
    public sealed class PlcIoMap : ScriptableObject
    {
        [Tooltip("Holding register the twin counts up once a second, so the PLC program can hold the line if the twin stops (%MW4).")]
        [SerializeField] private int twinHeartbeatAddress = 1028;
        [SerializeField] private List<PlcPoint> points = new List<PlcPoint>(DefaultPoints());

        public ushort TwinHeartbeatAddress => (ushort)twinHeartbeatAddress;
        public IReadOnlyList<PlcPoint> Points => points;

        public PlcImage BuildImage() => new PlcImage(points, TwinHeartbeatAddress);

        private void Reset()
        {
            twinHeartbeatAddress = 1028;
            points = new List<PlcPoint>(DefaultPoints());
        }

        /// <summary>The cell's I/O list, matching tools/plc/cell.st.</summary>
        public static IEnumerable<PlcPoint> DefaultPoints()
        {
            const PlcDirection toPlc = PlcDirection.ToPlc;
            const PlcDirection fromPlc = PlcDirection.FromPlc;
            const PlcArea register = PlcArea.HoldingRegister;
            const PlcArea coil = PlcArea.Coil;

            // Sensors and feedback the program reads (%MW0–%MW3, %MW5)
            yield return new PlcPoint("cell/gate1/part_present", register, 1024, toPlc, true, "part_at_gate", "Light barrier in front of the pick station");
            yield return new PlcPoint("cell/arm/busy", register, 1025, toPlc, true, "arm_busy", "Arm acknowledges the pick and is moving");
            yield return new PlcPoint("cell/outfeed/part_exit", register, 1026, toPlc, true, "part_at_exit", "Exit sensor at the end of the outfeed");
            yield return new PlcPoint("cell/press/health", register, 1027, toPlc, false, "press_health", "Press health: 0 ok, 1 warning, 2 fault (holds the line)");
            // The safety zone's latched protective stop (M10, %MW5, after the twin heartbeat at %MW4)
            yield return new PlcPoint("cell/safety/protective_stop", register, 1029, toPlc, true, "safety_stop", "Protective stop from the safety zone: the line stops at once and resumes on reset");

            // Commands and status the program writes (%QX0.0–%QX0.3, %QW0–%QW3)
            yield return new PlcPoint("cell/infeed/drive_fwd", coil, 0, fromPlc, true, "infeed_run", "Infeed conveyor runs forward");
            yield return new PlcPoint("cell/outfeed/drive_fwd", coil, 1, fromPlc, true, "outfeed_run", "Outfeed conveyor runs forward");
            yield return new PlcPoint("cell/arm/pick_cmd", coil, 2, fromPlc, true, "pick_cmd", "Pick the part at the gate (rising edge)");
            yield return new PlcPoint("cell/line/stalled", coil, 3, fromPlc, true, "line_stalled", "A transfer did not finish in time");
            yield return new PlcPoint("cell/line/state", register, 0, fromPlc, false, "line_state", "0 feeding, 1 transferring, 2 stalled, 3 held, 4 protective stop");
            yield return new PlcPoint("cell/plc/heartbeat", register, 1, fromPlc, false, "plc_heartbeat", "Scan counter; the twin holds the line if it stops");
            yield return new PlcPoint("cell/line/parts_out", register, 2, fromPlc, false, "parts_out", "Parts that left the cell");
            yield return new PlcPoint("cell/line/cycle_time_s", register, 3, fromPlc, false, "cycle_time_cs", "Seconds between the last two parts out (register in 1/100 s)", 0.01d);
        }
    }
}
