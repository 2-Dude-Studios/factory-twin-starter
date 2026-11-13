using System.Collections.Generic;
using System.Linq;
using System.Text;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Electrical safety (M27, ahead of M20/M21): makes the electrical bay hands-on in the headset. Re-runnable, on the
    /// open scene (Cell.unity; the Quest variant inherits it). Q1's through-door handle turns in the hand, the door's
    /// latch opens and shuts the door, a UPS feed panel beside the cabinet has a breaker lever and a hasp, two padlocks
    /// with tags hang on the gear bench's back board for Q1's hasp and the UPS hasp, and the arc flash PPE kit
    /// (Electrical_PPE.fbx, our own Blender models) lies on the bench to put on item by item before the training
    /// starts. The cabinet model's rules still decide every move (CabinetStation).
    /// </summary>
    internal static class ElectricalXrSetup
    {
        private const string PpeModelPath = "Assets/_Project/Art/Electrical/Electrical_PPE.fbx";
        private const string RootName = "HandsOn";

        // In the cabinet's space (Drive_Cabinet.fbx, it faces +Z): Q1's hasp ring under the handle, the door's latch,
        // and where the UPS feed panel stands, to your right as you face the cabinet (-X).
        private static readonly Vector3 Q1Hasp = new Vector3(-0.20f, 1.206f, 0.258f);
        private static readonly Vector3 Latch = new Vector3(-0.36f, 0.62f, 0.25f);
        private static readonly Vector3 UpsPanelSpot = new Vector3(-1.15f, 0f, 0.05f);

        // In the UPS panel's space (Electrical_PPE.fbx, UPS_Panel faces +Z): the breaker and its lockout tab.
        private static readonly Vector3 UpsBreaker = new Vector3(0f, 1.32f, 0.135f);
        private static readonly Vector3 UpsHasp = new Vector3(0.075f, 1.27f, 0.137f);

        // On the gear bench (the electrical bay's KitStation; the trainee stands at -Z facing +Z, the back board at
        // +Z, the top at 0.9 m): each PPE item's place, in PpeDressing's order, and the two padlock pegs.
        private static readonly (string Mesh, Vector3 At)[] KitSpots =
        {
            ("PPE_Balaclava", new Vector3(-0.62f, 1.025f, -0.05f)),
            ("PPE_SafetyGlasses", new Vector3(-0.38f, 0.92f, -0.12f)),
            ("PPE_HearingProtection", new Vector3(-0.15f, 1.035f, -0.05f)),
            ("PPE_HardHatShield", new Vector3(0.12f, 1.14f, -0.02f)),
            ("PPE_ArcJacket", new Vector3(0.45f, 1.75f, 0.24f)),
            ("PPE_Gloves", new Vector3(0.70f, 1.035f, -0.05f)),
        };
        private static readonly Vector3 PegQ1 = new Vector3(-0.62f, 1.55f, 0.29f);
        private static readonly Vector3 PegUps = new Vector3(-0.42f, 1.55f, 0.29f);

        [MenuItem("Tools/Factory Twin/Build Electrical Hands-On")]
        public static void BuildMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            var log = new StringBuilder("[ElectricalXrSetup] ");
            Build(scene, log);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.Append("Save the scene to keep it.").ToString());
        }

        public static void Build(Scene scene, StringBuilder log)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            CellContext context = roots.SelectMany(g => g.GetComponentsInChildren<CellContext>(true)).FirstOrDefault();
            XROrigin origin = roots.SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).FirstOrDefault();
            Transform cabinet = context != null ? context.transform.root.Find(ElectricalSetup.CabinetPath.Substring(ElectricalSetup.CabinetPath.IndexOf('/') + 1)) : null;
            cabinet ??= roots.Select(g => g.transform.Find(ElectricalSetup.CabinetPath.Substring(ElectricalSetup.CabinetPath.IndexOf('/') + 1))).FirstOrDefault(t => t != null);
            if (context == null || cabinet == null)
            {
                log.Append("No cell context or no drive cabinet (run Build Electrical Cabinet first). ");
                return;
            }
            Transform door = cabinet.Find("Door");
            Transform handle = door != null ? door.Find("Handle") : null;
            Transform kit = Kit(context.transform);
            Dictionary<string, Transform> models = Models();
            if (door == null || handle == null || kit == null)
            {
                log.Append($"Missing: {(door == null ? "door " : "")}{(handle == null ? "Q1 handle " : "")}{(kit == null ? "electrical gear bench " : "")}. ");
                return;
            }

            // Again from scratch: the root and everything this builder put on the cabinet.
            Transform electrical = cabinet.parent;
            Transform old = electrical.Find(RootName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            foreach (string name in new[] { "Latch", "Q1_HaspSocket" })
            {
                Transform t = door.Find(name);
                if (t != null)
                    Object.DestroyImmediate(t.gameObject);
            }
            foreach (Component c in handle.GetComponents<Component>().Where(c => c is XrCabinetSwitch || c is XRBaseInteractable).ToList())
                Object.DestroyImmediate(c);
            foreach (Transform t in kit.Cast<Transform>().Where(t => t.name.StartsWith("PPE_") || t.name.StartsWith("Peg_") || t.name.StartsWith("Padlock_")).ToList())
                Object.DestroyImmediate(t.gameObject);

            var root = new GameObject(RootName).transform;
            root.SetParent(electrical, false);

            // Q1: the handle turns in the hand from here on, not by the animator.
            var animator = cabinet.GetComponent<CabinetAnimator>();
            if (animator != null)
                Set(animator, ("handle", null));
            ColliderOn(handle);
            Switch(handle.gameObject, context, CabinetControl.Q1, handle, Vector3.forward, 0f, -90f);

            // The door's latch: a small grip box on the door, moving with it.
            var latch = new GameObject("Latch").transform;
            latch.SetParent(door, false);
            latch.position = cabinet.TransformPoint(Latch);
            latch.rotation = cabinet.rotation;
            latch.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.07f, 0.09f, 0.05f);
            Switch(latch.gameObject, context, CabinetControl.Door, null, Vector3.forward, 0f, 0f);

            KeyedSocket q1Hasp = LotoProps.PadlockSocket(door, "Q1_HaspSocket", door.InverseTransformPoint(cabinet.TransformPoint(Q1Hasp)),
                Quaternion.Inverse(door.rotation) * cabinet.rotation);

            // The UPS feed panel: CB-U1 and its lockout tab.
            Transform panel = new GameObject("UPS_FeedPanel").transform;
            panel.SetParent(root, false);
            panel.SetPositionAndRotation(cabinet.TransformPoint(UpsPanelSpot), cabinet.rotation);
            Mesh(panel, "Body", models, "UPS_Panel", Vector3.zero, true);
            Transform lever = Mesh(panel, "Breaker", models, "UPS_Breaker", UpsBreaker, true);
            Mesh(panel, "HaspTab", models, "UPS_HaspTab", UpsHasp, false);
            Switch(lever.gameObject, context, CabinetControl.UpsBreaker, lever, Vector3.right, 0f, 150f);
            KeyedSocket upsHasp = LotoProps.PadlockSocket(panel, "UPS_HaspSocket", UpsHasp + new Vector3(0f, -0.006f, 0.004f), Quaternion.identity);

            // Two padlocks with tags on the gear bench's back board.
            KeyedSocket pegQ1 = LotoProps.PadlockSocket(kit, "Peg_Q1", PegQ1, Quaternion.identity);
            KeyedSocket pegUps = LotoProps.PadlockSocket(kit, "Peg_UPS", PegUps, Quaternion.identity);
            var lockQ1 = LotoProps.Padlock(kit, "Padlock_Q1", pegQ1);
            var lockUps = LotoProps.Padlock(kit, "Padlock_UPS", pegUps);
            LockSocket(root, "Q1_Lock", context, CabinetControl.Q1, q1Hasp, pegQ1, lockQ1);
            LockSocket(root, "UPS_Lock", context, CabinetControl.UpsBreaker, upsHasp, pegUps, lockUps);

            // The PPE kit on the bench, and the station that puts it on.
            var station = root.gameObject.AddComponent<PpeKitStation>();
            var items = new List<PpeItem>();
            for (int i = 0; i < KitSpots.Length && i < PpeDressing.Kit.Count; i++)
            {
                (string mesh, Vector3 at) = KitSpots[i];
                Transform item = new GameObject(mesh).transform;
                item.SetParent(kit, false);
                item.SetLocalPositionAndRotation(at, Quaternion.Euler(0f, 180f, 0f));
                Mesh(item, "Model", models, mesh, Vector3.zero, true, keepParent: true);
                var body = item.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var grab = item.gameObject.AddComponent<XRGrabInteractable>();
                grab.colliders.Clear();
                grab.colliders.AddRange(item.GetComponentsInChildren<Collider>(true));
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                grab.throwOnDetach = false;
                grab.useDynamicAttach = true;
                var ppe = item.gameObject.AddComponent<PpeItem>();
                var so = new SerializedObject(ppe);
                so.FindProperty("station").objectReferenceValue = station;
                so.FindProperty("grab").objectReferenceValue = grab;
                so.FindProperty("index").intValue = i;
                so.ApplyModifiedPropertiesWithoutUndo();
                items.Add(ppe);
            }
            Renderer[] hands = origin != null
                ? origin.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.ToLowerInvariant().Contains("hand")).Cast<Renderer>().ToArray()
                : new Renderer[0];
            var st = new SerializedObject(station);
            st.FindProperty("context").objectReferenceValue = context;
            st.FindProperty("head").objectReferenceValue = origin != null && origin.Camera != null ? origin.Camera.transform : null;
            SerializedProperty list = st.FindProperty("items");
            list.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            SerializedProperty handList = st.FindProperty("hands");
            handList.arraySize = hands.Length;
            for (int i = 0; i < hands.Length; i++)
                handList.GetArrayElementAtIndex(i).objectReferenceValue = hands[i];
            st.ApplyModifiedPropertiesWithoutUndo();

            log.Append($"Hands-on cabinet: Q1 handle, door latch, Q1 hasp; UPS feed panel at {panel.position.x:F2}, {panel.position.z:F2} with its breaker and hasp; " +
                       $"2 padlocks on the gear bench; {items.Count} PPE items ({models.Count(m => m.Key.StartsWith("PPE_"))} models), {hands.Length} hand renderers to tint. ");
        }

        // The electrical bay's gear bench (TrainingAreasSetup): the KitStation nearest the layout's kit spot.
        private static Transform Kit(Transform cell)
        {
            var layout = AssetDatabase.LoadAssetAtPath<TrainingAreas>(ElectricalSetup.AreasPath);
            TrainingAreaSettings bay = layout != null ? layout.Find(TrainingArea.Electrical) : null;
            bay ??= TrainingAreaDefaults.For(TrainingArea.Electrical);
            var spot = new Vector3(bay.Kit.X, 0f, bay.Kit.Z);
            return cell.GetComponentsInChildren<Transform>(true).Where(t => t.name == "KitStation")
                .OrderBy(t => Vector3.Distance(new Vector3(t.position.x, 0f, t.position.z), spot)).FirstOrDefault();
        }

        private static Dictionary<string, Transform> Models()
        {
            var models = new Dictionary<string, Transform>();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PpeModelPath);
            if (root == null)
                return models;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && !models.ContainsKey(filter.name))
                    models.Add(filter.name, filter.transform);
            }
            return models;
        }

        // A part at a pivot with the model's mesh under it (its own rotation and scale, so Blender's axes come out
        // right). keepParent: the mesh goes straight under the parent (the parent is the part).
        private static Transform Mesh(Transform parent, string name, Dictionary<string, Transform> models, string mesh, Vector3 pivot, bool collider, bool keepParent = false)
        {
            Transform part = parent;
            if (!keepParent)
            {
                part = new GameObject(name).transform;
                part.SetParent(parent, false);
                part.localPosition = pivot;
            }
            if (!models.TryGetValue(mesh, out Transform source))
                return part;
            var visual = new GameObject(keepParent ? name : "Model");
            visual.transform.SetParent(part, false);
            visual.transform.localRotation = source.localRotation;
            visual.transform.localScale = source.localScale;
            visual.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            visual.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            if (collider)
                visual.AddComponent<BoxCollider>();
            return part;
        }

        private static void ColliderOn(Transform part)
        {
            Transform model = part.Find("Model");
            GameObject target = model != null ? model.gameObject : part.gameObject;
            if (target.GetComponent<Collider>() == null)
                target.AddComponent<BoxCollider>();
        }

        private static void Switch(GameObject go, CellContext context, CabinetControl control, Transform part, Vector3 axis, float onAngle, float offAngle)
        {
            var interactable = go.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Clear();
            interactable.colliders.AddRange(go.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger));
            var sw = go.AddComponent<XrCabinetSwitch>();
            var so = new SerializedObject(sw);
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("interactable").objectReferenceValue = interactable;
            so.FindProperty("control").enumValueIndex = (int)control;
            so.FindProperty("part").objectReferenceValue = part;
            so.FindProperty("axis").vector3Value = axis;
            so.FindProperty("onAngle").floatValue = onAngle;
            so.FindProperty("offAngle").floatValue = offAngle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void LockSocket(Transform root, string name, CellContext context, CabinetControl control, KeyedSocket hasp, KeyedSocket home, XRGrabInteractable padlock)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var socket = go.AddComponent<XrCabinetLockSocket>();
            var so = new SerializedObject(socket);
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("control").enumValueIndex = (int)control;
            so.FindProperty("lockSocket").objectReferenceValue = hasp;
            so.FindProperty("homeSocket").objectReferenceValue = home;
            so.FindProperty("padlock").objectReferenceValue = padlock;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, params (string field, Object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in values)
                so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
