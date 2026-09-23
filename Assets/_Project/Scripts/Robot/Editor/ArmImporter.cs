using System.Collections.Generic;
using Unity.Robotics.UrdfImporter;
using UnityEditor;
using UnityEngine;

namespace TwoDude.FactoryTwin.Robot
{
    /// <summary>
    /// Pick station: imports the de-branded UR5e (cell_arm.urdf) as ArticulationBody joints.
    /// The importer's own entry point needs an asset selected in the Project window, which cannot be
    /// scripted; this menu item drives the same API so the import is repeatable.
    /// </summary>
    internal static class ArmImporter
    {
        private const string UrdfPath = "Assets/_Project/Robots/ur/cell_arm.urdf";

        [MenuItem("Tools/Factory Twin/Import Cell Arm (UR5e)")]
        private static void Import()
        {
            string absolute = System.IO.Path.GetFullPath(UrdfPath);
            if (!System.IO.File.Exists(absolute))
            {
                Debug.LogError($"[ArmImporter] {UrdfPath} not found.");
                return;
            }

            var settings = new ImportSettings
            {
                chosenAxis = ImportSettings.axisType.yAxis,          // URDF is z-up, Unity is y-up
                convexMethod = ImportSettings.convexDecomposer.unity, // vHACD is slow and not needed for a pick-place arm
            };

            IEnumerator<GameObject> import = UrdfRobotExtensions.Create(absolute, settings, loadStatus: false);
            GameObject robot = null;
            while (import.MoveNext())
                robot = import.Current;

            if (robot == null)
            {
                Debug.LogError("[ArmImporter] Import produced no robot.");
                return;
            }

            robot.name = "Arm";

            // The importer leaves the articulation root free-floating, so the whole arm falls through the
            // floor the moment play starts. A bolted-down robot needs an immovable root.
            ArticulationBody root = robot.GetComponentInChildren<ArticulationBody>();
            if (root != null)
            {
                root.immovable = true;
                Debug.Log($"[ArmImporter] Pinned the articulation root '{root.name}' (immovable).", root);
            }
            Undo.RegisterCreatedObjectUndo(robot, "Import Cell Arm");
            Selection.activeGameObject = robot;
            Debug.Log($"[ArmImporter] Imported {robot.name} with {robot.GetComponentsInChildren<ArticulationBody>().Length} articulation bodies.");
        }
    }
}
