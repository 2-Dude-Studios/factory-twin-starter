using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Soft PLC link: the I/O list as commissioning engineers work from it, one row per point: PLC tag, IEC
    /// address, Modbus area and address, direction, data type, twin topic and description. Pure.
    /// </summary>
    public static class PlcIoList
    {
        public const string Header = "tag,iec_address,modbus_area,modbus_address,direction,type,topic,description";

        public static string ToCsv(IReadOnlyList<PlcPoint> points, ushort twinHeartbeatAddress)
        {
            var csv = new StringBuilder(Header).Append('\n');
            foreach (PlcPoint point in points)
            {
                Row(csv, point.PlcName, point.IecAddress, Area(point.Area), point.Address,
                    point.Direction == PlcDirection.ToPlc ? "input (twin → PLC)" : "output (PLC → twin)",
                    point.IsBool ? "BOOL" : point.Scale == 1d ? "UINT" : "UINT ×" + point.Scale.ToString("0.###", CultureInfo.InvariantCulture),
                    point.Topic, point.Description);
            }

            var heartbeat = new PlcPoint(string.Empty, PlcArea.HoldingRegister, twinHeartbeatAddress, PlcDirection.ToPlc, false, "twin_heartbeat");
            Row(csv, "twin_heartbeat", heartbeat.IecAddress, Area(PlcArea.HoldingRegister), twinHeartbeatAddress,
                "input (twin → PLC)", "UINT", string.Empty, "Counts up once a second; the PLC holds the line if it stops for 3 s");
            return csv.ToString();
        }

        private static string Area(PlcArea area) => area == PlcArea.Coil ? "coil" : "holding register";

        private static void Row(StringBuilder csv, string tag, string iec, string area, ushort address, string direction,
            string type, string topic, string description)
        {
            csv.Append(Quote(tag)).Append(',')
                .Append(Quote(iec)).Append(',')
                .Append(Quote(area)).Append(',')
                .Append(address.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Quote(direction)).Append(',')
                .Append(Quote(type)).Append(',')
                .Append(Quote(topic)).Append(',')
                .Append(Quote(description)).Append('\n');
        }

        private static string Quote(string cell)
        {
            cell ??= string.Empty;
            return cell.IndexOfAny(QuoteTriggers) < 0 ? cell : "\"" + cell.Replace("\"", "\"\"") + "\"";
        }

        private static readonly char[] QuoteTriggers = { ',', '"', '\n' };
    }
}
