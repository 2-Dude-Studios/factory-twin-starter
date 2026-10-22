using System.IO;
using UnityEditor;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Soft PLC link (Editor): writes the I/O list from Data/PlcIoMap.asset to tools/plc/io_list.csv.</summary>
    public static class PlcIoListExporter
    {
        private const string MapPath = "Assets/_Project/Data/PlcIoMap.asset";

        [MenuItem("Tools/Factory Twin/Export PLC I-O List")]
        public static void Export()
        {
            var map = AssetDatabase.LoadAssetAtPath<PlcIoMap>(MapPath);
            if (map == null)
            {
                Debug.LogError($"[PlcIoListExporter] No PlcIoMap at {MapPath}.");
                return;
            }

            string project = Path.GetDirectoryName(Application.dataPath) ?? ".";
            string file = Path.Combine(project, "tools", "plc", "io_list.csv");
            File.WriteAllText(file, PlcIoList.ToCsv(map.Points, map.TwinHeartbeatAddress));
            Debug.Log($"[PlcIoListExporter] Wrote {map.Points.Count + 1} rows to {file}.");
        }
    }
}
