using System.Linq;
using System.Text;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone mode (M27). Two parts, both re-runnable:
    /// <list type="bullet">
    /// <item>The standalone switch, applied by the Quest variant builder to CellQuest only, so the headset build is
    /// standalone by construction and the desktop and Link builds are untouched: the MQTT bridge and the soft PLC link
    /// off (the in-Unity sequencer runs the line), the desktop dashboard and the capture rig's director and take
    /// sequencer off, the URDF importer's components (missing scripts on Android) left out, and the headset pause on
    /// the rig.</item>
    /// <item>The headset controls, applied to CellQuest too and to the open scene from the menu (for Link): the robot
    /// cell drill's page on the tablet (<see cref="VrRobotMenu"/>) and a teleport floor over the walkways between the
    /// training areas.</item>
    /// </list>
    /// </summary>
    internal static class StandaloneSetup
    {
        private const string WalkwayName = "WalkwayTeleportFloor";

        [MenuItem("Tools/Factory Twin/Quest/Add Headset Controls to Open Scene")]
        private static void ControlsMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            var log = new StringBuilder("[StandaloneSetup] ");
            Controls(scene, log);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.Append("Save the scene to keep them.").ToString());
        }

        /// <summary>Makes the scene run on the headset alone. For CellQuest only.</summary>
        public static void Standalone(Scene scene, StringBuilder log)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            int off = 0;
            off += Disable<MqttBridge>(roots);
            off += Disable<PlcLink>(roots);
            off += Disable<ShotDirector>(roots);
            off += Disable<TakeSequencer>(roots);
            int dashboards = 0;
            foreach (DashboardController dashboard in roots.SelectMany(g => g.GetComponentsInChildren<DashboardController>(true)))
            {
                dashboard.gameObject.SetActive(false);
                dashboards++;
            }

            // The URDF importer's runtime components only describe the arm as imported; on Android their assembly is
            // not built, so each showed as a missing script. Nothing reads them at run time.
            // Removed in passes: a component another one requires goes once that one has gone.
            int urdf = 0;
            for (int pass = 0; pass < 6; pass++)
            {
                int removed = 0;
                foreach (MonoBehaviour b in roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToList())
                {
                    if (b == null || !IsUrdf(b) || RequiredByAnother(b))
                        continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(b))
                        PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(b), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    Object.DestroyImmediate(b);
                    removed++;
                }
                urdf += removed;
                if (removed == 0)
                    break;
            }

            XROrigin origin = roots.SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).FirstOrDefault();
            if (origin != null && origin.GetComponent<HeadsetPause>() == null)
                origin.gameObject.AddComponent<HeadsetPause>();

            log.Append($"Standalone: MQTT bridge, PLC link, shot director and take sequencer off ({off}), dashboard off ({dashboards}), " +
                       $"{urdf} URDF components left out, headset pause on the rig. ");
        }

        /// <summary>The headset's own controls: the robot drill page on the tablet and the walkway teleport floor.</summary>
        public static void Controls(Scene scene, StringBuilder log)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            CellContext context = roots.SelectMany(g => g.GetComponentsInChildren<CellContext>(true)).FirstOrDefault();
            XROrigin origin = roots.SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).FirstOrDefault();
            VrSwitch vrSwitch = roots.SelectMany(g => g.GetComponentsInChildren<VrSwitch>(true)).FirstOrDefault();
            RobotCellScorer scorer = roots.SelectMany(g => g.GetComponentsInChildren<RobotCellScorer>(true)).FirstOrDefault();
            TrainingAreaStart start = roots.SelectMany(g => g.GetComponentsInChildren<TrainingAreaStart>(true)).FirstOrDefault(s => s.Area == TrainingArea.RobotCell);
            VrMenu tablet = roots.SelectMany(g => g.GetComponentsInChildren<VrMenu>(true)).FirstOrDefault();

            if (tablet != null && tablet.TryGetComponent(out UIDocument document))
            {
                VrRobotMenu menu = tablet.GetComponent<VrRobotMenu>() ?? tablet.gameObject.AddComponent<VrRobotMenu>();
                var so = new SerializedObject(menu);
                so.FindProperty("document").objectReferenceValue = document;
                so.FindProperty("context").objectReferenceValue = context;
                so.FindProperty("scorer").objectReferenceValue = scorer;
                so.FindProperty("vrSwitch").objectReferenceValue = vrSwitch;
                so.FindProperty("origin").objectReferenceValue = origin;
                so.FindProperty("start").objectReferenceValue = start;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.Append($"Robot drill page on the tablet (scorer {(scorer != null ? "found" : "MISSING")}, start {(start != null ? "found" : "MISSING")}). ");
            }
            else
            {
                log.Append("No tablet (VrMenu with a UIDocument): robot drill page skipped. ");
            }

            // One floor over the box that holds every training area, under the areas' own floors: the walkways between
            // them. The teleport ray lands on the first collider it meets, so machines and walls still block it.
            Transform training = context != null ? context.transform.Find("Training") : null;
            if (training == null)
            {
                log.Append("No Cell/Training: walkway floor skipped. ");
                return;
            }
            Transform old = training.Find(WalkwayName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            Rect areas = QuestVariantSetup.AreasBox();
            var floor = new GameObject(WalkwayName);
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.SetParent(training, false);
            floor.transform.position = new Vector3(areas.center.x, -0.015f, areas.center.y);
            var box = floor.AddComponent<BoxCollider>();
            box.size = new Vector3(areas.width, 0.02f, areas.height);
            floor.AddComponent<TeleportationArea>();
            log.Append($"Walkway teleport floor {areas.width:F0} x {areas.height:F0} m. ");
        }

        private static bool IsUrdf(Component c) => c.GetType().Namespace?.StartsWith("Unity.Robotics") == true;

        private static bool RequiredByAnother(Component target)
        {
            System.Type type = target.GetType();
            foreach (Component other in target.GetComponents<Component>())
            {
                if (other == null || other == target)
                    continue;
                foreach (RequireComponent rc in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if ((rc.m_Type0 != null && rc.m_Type0.IsAssignableFrom(type)) ||
                        (rc.m_Type1 != null && rc.m_Type1.IsAssignableFrom(type)) ||
                        (rc.m_Type2 != null && rc.m_Type2.IsAssignableFrom(type)))
                        return true;
                }
            }
            return false;
        }

        private static int Disable<T>(GameObject[] roots) where T : Behaviour
        {
            int n = 0;
            foreach (T c in roots.SelectMany(g => g.GetComponentsInChildren<T>(true)))
            {
                c.enabled = false;
                n++;
            }
            return n;
        }
    }
}
