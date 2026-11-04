using System.Collections.Generic;
using System.Linq;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15), editor tooling: makes the extinguishers on the fire training station's stand work with two
    /// hands, and wires the tablet's fire page. Each one gets a grab on its carry handle, a pin to pull, a hose to take
    /// by its nozzle (with a hose line that follows the hand) and an ExtinguisherUnit; FireExtinguisher on the station
    /// follows the one in use; VrFireMenu goes on the tablet. Run Build Fire Drill first (it rebuilds the stand), then
    /// this. Running it again replaces what it built.
    /// </summary>
    internal static class FireXrSetup
    {
        private const string ScenarioFolder = "Assets/_Project/Data/Fire";
        // The tablet starts one fire per spot; F4 and F5 are timing variants of the CNC fire for the dashboard.
        private static readonly int[] TabletScenarios = { 1, 2, 3, 7, 8, 9, 6 };
        private static readonly Vector3 StationFloor = new Vector3(-4.8f, 0f, -12.6f);
        private const float StationYaw = 180f;
        private const float HoseWidth = 0.018f;
        private const int HosePoints = 12;

        private static readonly Dictionary<string, (FireAgent agent, Color spray)> Kinds = new Dictionary<string, (FireAgent, Color)>
        {
            { "Ext_Water", (FireAgent.Water, new Color(0.75f, 0.88f, 1f, 0.7f)) },
            { "Ext_CO2", (FireAgent.Co2, new Color(1f, 1f, 1f, 0.85f)) },
            { "Ext_ABC", (FireAgent.DryChemicalAbc, new Color(1f, 0.96f, 0.84f, 0.95f)) },
        };

        [MenuItem("Tools/Factory Twin/Build Fire Drill (VR)")]
        private static void Build()
        {
            GameObject captureRig = GameObject.Find("CaptureRig");
            GameObject cell = GameObject.Find("Cell");
            Transform rigTransform = captureRig != null ? captureRig.transform.Find("XR Origin (XR Rig)") : null;
            Transform fire = cell != null ? cell.transform.Find("Fire") : null;
            Transform station = fire != null ? fire.Find("TrainingStation") : null;
            CellContext context = cell != null ? cell.GetComponent<CellContext>() : null;
            if (rigTransform == null || station == null || context == null)
            {
                Debug.LogError("[FireXrSetup] Open Cell.unity with the VR rig built (Build VR Rig) and the fire drill built (Build Fire Drill).");
                return;
            }
            XROrigin origin = rigTransform.GetComponent<XROrigin>();
            Transform tablet = rigTransform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "VrTablet");
            if (tablet == null)
            {
                Debug.LogError("[FireXrSetup] The rig has no VrTablet; rebuild the VR rig first.");
                return;
            }

            Undo.SetCurrentGroupName("Build Fire Drill (VR)");
            int undoGroup = Undo.GetCurrentGroup();
            RemoveOldHandSetup(rigTransform);

            // The extinguishers on the stand, each worked with two hands.
            var units = new List<ExtinguisherUnit>();
            foreach (KeyValuePair<string, (FireAgent agent, Color spray)> kind in Kinds)
            {
                Transform group = station.Find("Stowed_" + kind.Key);
                if (group == null)
                {
                    Debug.LogWarning($"[FireXrSetup] No Stowed_{kind.Key} on the stand; run Build Fire Drill first.");
                    continue;
                }
                units.Add(Unit(group, kind.Value.agent));
            }

            // The extinguisher in use: on the station, reading whichever unit is in a hand.
            var extinguisher = GetOrAdd<FireExtinguisher>(station.gameObject);
            var so = new SerializedObject(extinguisher);
            so.FindProperty("context").objectReferenceValue = context;
            SetList(so.FindProperty("units"), units.Cast<Object>().ToList());
            FireEffects[] spots = fire.GetComponentsInChildren<FireEffects>(true);
            SerializedProperty fires = so.FindProperty("fires");
            fires.arraySize = spots.Length;
            for (int i = 0; i < spots.Length; i++)
            {
                SerializedProperty entry = fires.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("location").intValue = new SerializedObject(spots[i]).FindProperty("location").intValue;
                entry.FindPropertyRelative("baseOfFire").objectReferenceValue = spots[i].transform;
            }
            Transform spray = fire.Find("ExtinguisherSpray");
            so.FindProperty("spray").objectReferenceValue = spray != null ? spray.GetComponent<ParticleSystem>() : null;
            SerializedProperty colours = so.FindProperty("sprayColours");
            colours.arraySize = Kinds.Count;
            int c = 0;
            foreach (KeyValuePair<string, (FireAgent agent, Color spray)> kind in Kinds)
            {
                SerializedProperty entry = colours.GetArrayElementAtIndex(c++);
                entry.FindPropertyRelative("agent").intValue = (int)kind.Value.agent;
                entry.FindPropertyRelative("colour").colorValue = kind.Value.spray;
            }
            so.ApplyModifiedProperties();

            // The tablet's fire page.
            var menu = GetOrAdd<VrFireMenu>(tablet.gameObject);
            var menuSo = new SerializedObject(menu);
            menuSo.FindProperty("document").objectReferenceValue = tablet.GetComponent<UIDocument>();
            menuSo.FindProperty("context").objectReferenceValue = context;
            menuSo.FindProperty("extinguisher").objectReferenceValue = extinguisher;
            menuSo.FindProperty("origin").objectReferenceValue = origin;
            menuSo.FindProperty("stationFloor").vector3Value = StationFloor;
            menuSo.FindProperty("stationYaw").floatValue = StationYaw;
            List<Object> scenarios = TabletScenarios
                .Select(n => (Object)AssetDatabase.LoadAssetAtPath<FireScenario>($"{ScenarioFolder}/F{n}.asset"))
                .Where(s => s != null).ToList();
            SetList(menuSo.FindProperty("scenarios"), scenarios);
            menuSo.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(cell.scene);
            Debug.Log($"[FireXrSetup] {units.Count} two-handed extinguishers on the stand, {spots.Length} fire spots, {scenarios.Count} fires on the tablet.");
        }

        // The first version put three extinguishers in the right hand; take those out.
        private static void RemoveOldHandSetup(Transform rig)
        {
            Transform right = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Right Controller");
            if (right == null)
                return;
            foreach (Transform child in right.Cast<Transform>().Where(t => t.name.StartsWith("Held_")).ToList())
                Undo.DestroyObjectImmediate(child.gameObject);
            var old = right.GetComponent<FireExtinguisher>();
            if (old != null)
                Undo.DestroyObjectImmediate(old);
        }

        private static ExtinguisherUnit Unit(Transform group, FireAgent agent)
        {
            Transform body = group.Find("Body");
            Transform pin = group.Find("Pin");
            Transform nozzle = group.Find("Nozzle");
            Transform lever = group.Find("Lever");
            Transform hose = group.Find("Hose");

            // Carry handle: the whole unit, held at the grip (the group's origin).
            Collider bodyCollider = BoxOn(body);
            XRGrabInteractable bodyGrab = Grab(group.gameObject, Attach(group, "Grip", Vector3.zero), bodyCollider);

            // Pin: grabbed by its ring and pulled out.
            var pinCollider = GetOrAdd<SphereCollider>(pin.gameObject);
            pinCollider.center = new Vector3(-0.012f, 0f, 0f);
            pinCollider.radius = 0.03f;
            var pinSimple = GetOrAdd<XRSimpleInteractable>(pin.gameObject);
            pinSimple.colliders.Clear();
            pinSimple.colliders.Add(pinCollider);

            // Nozzle: its own grab, held a little behind the tip; length from its mesh.
            Collider nozzleCollider = BoxOn(nozzle);
            float length = MeshLength(nozzle);
            XRGrabInteractable nozzleGrab = Grab(nozzle.gameObject, Attach(nozzle, "Grip", new Vector3(0f, 0f, -length * 0.6f)), nozzleCollider);

            // The hose line from the valve, used while the nozzle is in a hand.
            Transform start = Attach(group, "HoseStart", group.InverseTransformPoint(HighestVertex(hose)));
            Transform lineGo = group.Find("HoseLine") ?? new GameObject("HoseLine").transform;
            lineGo.SetParent(group, false);
            var line = GetOrAdd<LineRenderer>(lineGo.gameObject);
            line.useWorldSpace = true;
            line.positionCount = HosePoints;
            line.widthMultiplier = HoseWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer hoseRenderer = hose.GetComponentInChildren<Renderer>(true);
            line.sharedMaterial = hoseRenderer != null ? hoseRenderer.sharedMaterial : null;
            line.enabled = false;

            // How the hands close on each part (the rig's hands take a held part's HandGrip).
            Curls(group.gameObject, FingerCurls.Handle);                                        // carry handle and lever
            Curls(nozzle.gameObject, new FingerCurls(0.6f, 0.8f, 0.85f, 0.88f, 0.9f));        // round the hose nozzle
            Curls(pin.gameObject, new FingerCurls(0.72f, 0.78f, 0.45f, 0.5f, 0.55f));          // a pinch on the ring

            // The discharge hiss and the pin's click, from the unit.
            var sound = GetOrAdd<AudioSource>(group.gameObject);
            sound.playOnAwake = false;
            sound.loop = true;
            sound.spatialBlend = 1f;
            sound.minDistance = 0.6f;
            sound.maxDistance = 15f;
            sound.rolloffMode = AudioRolloffMode.Linear;
            sound.volume = agent == FireAgent.Water ? 0.45f : 0.6f;

            var unit = GetOrAdd<ExtinguisherUnit>(group.gameObject);
            var so = new SerializedObject(unit);
            so.FindProperty("agent").intValue = (int)agent;
            so.FindProperty("body").objectReferenceValue = bodyGrab;
            so.FindProperty("pin").objectReferenceValue = pinSimple;
            so.FindProperty("nozzle").objectReferenceValue = nozzleGrab;
            so.FindProperty("lever").objectReferenceValue = lever;
            so.FindProperty("hoseMesh").objectReferenceValue = hose.gameObject;
            so.FindProperty("hoseLine").objectReferenceValue = line;
            so.FindProperty("hoseStart").objectReferenceValue = start;
            so.FindProperty("nozzleLength").floatValue = length;
            so.FindProperty("sound").objectReferenceValue = sound;
            so.FindProperty("hissBrightness").floatValue = agent == FireAgent.Co2 ? 0.8f : agent == FireAgent.Water ? 0.2f : 0.45f;
            so.ApplyModifiedProperties();
            return unit;
        }

        private static void Curls(GameObject go, FingerCurls curls)
        {
            var grip = GetOrAdd<HandGrip>(go);
            var so = new SerializedObject(grip);
            SerializedProperty c = so.FindProperty("curls");
            c.FindPropertyRelative("thumb").floatValue = curls.thumb;
            c.FindPropertyRelative("index").floatValue = curls.index;
            c.FindPropertyRelative("middle").floatValue = curls.middle;
            c.FindPropertyRelative("ring").floatValue = curls.ring;
            c.FindPropertyRelative("little").floatValue = curls.little;
            so.ApplyModifiedProperties();
        }

        // Moved straight with the hand, never thrown, on a kinematic body; only its own collider counts for it.
        private static XRGrabInteractable Grab(GameObject go, Transform attach, Collider collider)
        {
            var body = GetOrAdd<Rigidbody>(go);
            body.isKinematic = true;
            body.useGravity = false;
            var grab = GetOrAdd<XRGrabInteractable>(go);
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = false;
            grab.retainTransformParent = true;
            grab.attachTransform = attach;
            grab.colliders.Clear();
            grab.colliders.Add(collider);
            return grab;
        }

        private static Collider BoxOn(Transform part)
        {
            Transform model = part.Find("Model") ?? part;
            return GetOrAdd<BoxCollider>(model.gameObject);
        }

        private static Transform Attach(Transform parent, string name, Vector3 localPosition)
        {
            Transform t = parent.Find(name) ?? new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            return t;
        }

        private static float MeshLength(Transform part)
        {
            Renderer r = part.GetComponentInChildren<Renderer>(true);
            if (r == null)
                return 0.07f;
            Vector3 s = r.bounds.size;
            return Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        }

        private static Vector3 HighestVertex(Transform part)
        {
            MeshFilter filter = part.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null)
                return part.position;
            Vector3 best = new Vector3(0f, float.NegativeInfinity, 0f);
            foreach (Vector3 v in filter.sharedMesh.vertices)
            {
                Vector3 w = filter.transform.TransformPoint(v);
                if (w.y > best.y)
                    best = w;
            }
            return best;
        }

        private static void SetList(SerializedProperty list, List<Object> values)
        {
            list.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }
    }
}
