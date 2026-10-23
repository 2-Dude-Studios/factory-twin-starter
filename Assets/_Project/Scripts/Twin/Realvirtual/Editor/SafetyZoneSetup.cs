using System.Collections.Generic;
using realvirtual;
using TwoDude.FactoryTwin.Robot;
using TwoDude.FactoryTwin.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Safety zone (M10): builds the arm's guarding into Cell.unity, repeatably. Adds the Trainee and SafetyZone
    /// layers (which collide only with each other, so parts never trip a zone and the trainee never trips a part
    /// sensor), the warning and protective zones as realvirtual sensors with their signals bound in TwinSignals,
    /// the arm's speed-scale signal, the SafetyZoneController and the desktop trainee as a capture shot (F5).
    /// Running it again updates what is there instead of adding a second copy.
    /// </summary>
    internal static class SafetyZoneSetup
    {
        public const string TraineeLayer = "Trainee";
        public const string ZoneLayer = "SafetyZone";
        private const int TraineeLayerIndex = 23;
        private const int ZoneLayerIndex = 24;

        // Arm base on its stand; the UR5e reaches 0.85 m. The protective boundary follows the ISO 13855 form
        // S = K·T + C with demo values: reach 0.85 m + 1.6 m/s × 0.2 s stopping and response ≈ 1.2 m. The warning
        // zone adds a metre on every side. A demo sizing, not a safety-rated calculation.
        private static readonly Vector3 ArmBase = new Vector3(-2.0f, 0f, 1.2f);
        private const float ProtectiveHalfSize = 1.2f;
        private const float WarningHalfSize = 2.2f;
        private const float ZoneHeight = 2.2f;

        // Floor outlines: emissive strips on the default layer (the hall renderer does not draw the zone layer).
        private const string StripMaterialPath = "Assets/_Project/Materials/StackLightLamp.mat";
        private const float StripWidth = 0.08f;
        private const float StripThickness = 0.006f;
        private static readonly Color Amber = new Color(1f, 0.68f, 0.05f);
        private static readonly Color Red = new Color(1f, 0.12f, 0.08f);

        // Props from the Factory Training sample (placed, never edited). The conveyors cross the protective zone's
        // east and west edges, so the fence closes the north edge (the CNC side) and the south edge, the trainee's
        // side, stays open with a cone at each end: the opening the zone sensor guards.
        private const string FencePrefabPath = "Assets/ThirdParty/FactoryTraining/Prefabs/env/walkway_Fence_prefab.prefab";
        private const string ConePrefabPath = "Assets/ThirdParty/FactoryTraining/Prefabs/env/koneSmall_prefab.prefab";

        private static readonly Vector3 TraineeStart = new Vector3(-2.0f, 0f, -2.6f);
        private const float EyeHeight = 1.65f;

        [MenuItem("Tools/Factory Twin/Build Safety Zone")]
        private static void Build()
        {
            if (!EnsureLayers())
                return;
            ApplyCollisionMatrix();

            GameObject cell = GameObject.Find("Cell");
            GameObject signals = GameObject.Find("Cell/Signals");
            TwinSignals twinSignals = cell != null ? cell.GetComponent<TwinSignals>() : null;
            CellContext context = cell != null ? cell.GetComponent<CellContext>() : null;
            ArmSignalBridge armBridge = Object.FindAnyObjectByType<ArmSignalBridge>();
            ShotDirector shots = Object.FindAnyObjectByType<ShotDirector>();
            if (cell == null || signals == null || twinSignals == null || context == null || armBridge == null || shots == null)
            {
                Debug.LogError("[SafetyZoneSetup] Open Cell.unity first: Cell, Cell/Signals, TwinSignals, CellContext, ArmSignalBridge and ShotDirector are needed.");
                return;
            }

            Undo.SetCurrentGroupName("Build Safety Zone");
            int undoGroup = Undo.GetCurrentGroup();

            var warningSignal = SignalObject<PLCInputBool>(signals.transform, CellTopics.SafetyZoneWarning);
            var protectiveSignal = SignalObject<PLCInputBool>(signals.transform, CellTopics.SafetyArmZoneOccupied);
            var speedSignal = SignalObject<PLCOutputFloat>(signals.transform, CellTopics.ArmSpeedScale);
            Undo.RecordObject(speedSignal, "Arm speed scale");
            speedSignal.Value = 1f;

            Transform safety = Child(cell.transform, "Safety");
            Zone(safety, "WarningZone", WarningHalfSize, warningSignal);
            Zone(safety, "ProtectiveZone", ProtectiveHalfSize, protectiveSignal);

            var controller = GetOrAdd<SafetyZoneController>(safety.gameObject);
            SetReference(controller, "context", context);

            var stripMaterial = AssetDatabase.LoadAssetAtPath<Material>(StripMaterialPath);
            Marking(safety, "WarningMarking", WarningHalfSize, ZoneRole.Warning, Amber, stripMaterial, context);
            Marking(safety, "ProtectiveMarking", ProtectiveHalfSize, ZoneRole.Protective, Red, stripMaterial, context);
            Props(safety);

            Bind(twinSignals, CellTopics.SafetyZoneWarning, warningSignal);
            Bind(twinSignals, CellTopics.SafetyArmZoneOccupied, protectiveSignal);
            Bind(twinSignals, CellTopics.ArmSpeedScale, speedSignal);
            SetReference(armBridge, "speedScale", speedSignal);

            CinemachineCamera eye = Trainee(shots.transform);
            AddTraineeShot(shots, eye);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(cell.scene);
            Debug.Log("[SafetyZoneSetup] Safety zone built: two zones, three signals bound, controller, trainee shot (F5).", safety);
        }

        private static bool EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (!SetLayer(layers, TraineeLayerIndex, TraineeLayer) || !SetLayer(layers, ZoneLayerIndex, ZoneLayer))
                return false;
            tagManager.ApplyModifiedProperties();
            return true;
        }

        private static bool SetLayer(SerializedProperty layers, int index, string name)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(index);
            if (slot.stringValue == name)
                return true;
            if (!string.IsNullOrEmpty(slot.stringValue))
            {
                Debug.LogError($"[SafetyZoneSetup] Layer {index} is already '{slot.stringValue}'; it is needed for '{name}'.");
                return false;
            }
            slot.stringValue = name;
            return true;
        }

        // The trainee and the zones see only each other.
        private static void ApplyCollisionMatrix()
        {
            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(TraineeLayerIndex, i, i != ZoneLayerIndex);
                Physics.IgnoreLayerCollision(ZoneLayerIndex, i, i != TraineeLayerIndex);
            }
            AssetDatabase.SaveAssets();
        }

        private static T SignalObject<T>(Transform signals, TopicPath topic) where T : Signal
        {
            string name = topic.Station + "." + topic.Signal;
            Transform existing = signals.Find(name);
            GameObject go = existing != null ? existing.gameObject : NewChild(signals, name);
            return GetOrAdd<T>(go);
        }

        private static void Zone(Transform parent, string name, float halfSize, PLCInputBool signal)
        {
            Transform zone = Child(parent, name);
            zone.gameObject.layer = LayerMask.NameToLayer(ZoneLayer);
            zone.position = new Vector3(ArmBase.x, ZoneHeight / 2f, ArmBase.z);
            zone.rotation = Quaternion.identity;

            var box = GetOrAdd<BoxCollider>(zone.gameObject);
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(halfSize * 2f, ZoneHeight, halfSize * 2f);

            var sensor = GetOrAdd<Sensor>(zone.gameObject);
            Undo.RecordObject(sensor, "Configure zone sensor");
            sensor.UseRaycast = false;
            sensor.DisplayStatus = false;
            sensor.LimitSensorToTag = "";
            sensor.SensorOccupied = signal;
        }

        private static void Marking(Transform parent, string name, float halfSize, ZoneRole role, Color color, Material material,
            CellContext context)
        {
            Transform marking = Child(parent, name);
            marking.position = new Vector3(ArmBase.x, StripThickness / 2f + 0.001f, ArmBase.z);
            marking.rotation = Quaternion.identity;

            float length = halfSize * 2f + StripWidth;
            var strips = new[]
            {
                Strip(marking, "North", new Vector3(0f, 0f, halfSize), new Vector3(length, StripThickness, StripWidth), material),
                Strip(marking, "South", new Vector3(0f, 0f, -halfSize), new Vector3(length, StripThickness, StripWidth), material),
                Strip(marking, "East", new Vector3(halfSize, 0f, 0f), new Vector3(StripWidth, StripThickness, length), material),
                Strip(marking, "West", new Vector3(-halfSize, 0f, 0f), new Vector3(StripWidth, StripThickness, length), material),
            };

            var zoneMarking = GetOrAdd<ZoneMarking>(marking.gameObject);
            var so = new SerializedObject(zoneMarking);
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("role").enumValueIndex = (int)role;
            so.FindProperty("color").colorValue = color;
            // Lower than the stack light's lamps: a long strip blooms to white at that level and loses its colour.
            so.FindProperty("litEmission").floatValue = 1.2f;
            SerializedProperty list = so.FindProperty("strips");
            list.arraySize = strips.Length;
            for (int i = 0; i < strips.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = strips[i];
            so.ApplyModifiedProperties();
        }

        private static Renderer Strip(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            Transform existing = parent.Find(name);
            GameObject strip;
            if (existing != null)
            {
                strip = existing.gameObject;
            }
            else
            {
                strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = name;
                Object.DestroyImmediate(strip.GetComponent<BoxCollider>());
                Undo.RegisterCreatedObjectUndo(strip, "Build Safety Zone");
                strip.transform.SetParent(parent, false);
            }
            strip.layer = 0;
            strip.transform.localPosition = localPosition;
            strip.transform.localRotation = Quaternion.identity;
            strip.transform.localScale = size;
            var renderer = strip.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }

        private static void Props(Transform parent)
        {
            Transform props = Child(parent, "Props");
            for (int i = props.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(props.GetChild(i).gameObject);

            var fence = AssetDatabase.LoadAssetAtPath<GameObject>(FencePrefabPath);
            var cone = AssetDatabase.LoadAssetAtPath<GameObject>(ConePrefabPath);
            if (fence == null || cone == null)
            {
                Debug.LogWarning("[SafetyZoneSetup] Factory Training fence or cone prefab not found; props skipped.");
                return;
            }

            // Measure one fence piece, lay its long side along x, and tile it across the north edge.
            GameObject probe = Place(fence, props, Vector3.zero, 0f);
            Bounds size = WorldBounds(probe);
            bool alongZ = size.size.z > size.size.x;
            float pieceLength = Mathf.Max(size.size.x, size.size.z);
            Undo.DestroyObjectImmediate(probe);

            float edge = ProtectiveHalfSize * 2f;
            int pieces = Mathf.Max(1, Mathf.RoundToInt(edge / pieceLength));
            float step = edge / pieces;
            float northZ = ArmBase.z + ProtectiveHalfSize + 0.1f;
            for (int i = 0; i < pieces; i++)
            {
                float x = ArmBase.x - ProtectiveHalfSize + step * (i + 0.5f);
                GameObject piece = Place(fence, props, new Vector3(x, 0f, northZ), alongZ ? 90f : 0f);
                // Stretch or shrink along the rail so the pieces end at the zone's corners.
                Vector3 scale = piece.transform.localScale;
                if (alongZ)
                    scale.z *= step / pieceLength;
                else
                    scale.x *= step / pieceLength;
                piece.transform.localScale = scale;
                // Centre the piece on its slot whatever its pivot.
                Vector3 offset = new Vector3(x, 0f, northZ) - WorldBounds(piece).center;
                piece.transform.position += new Vector3(offset.x, 0f, offset.z);
            }

            float southZ = ArmBase.z - ProtectiveHalfSize - 0.15f;
            Place(cone, props, new Vector3(ArmBase.x - ProtectiveHalfSize - 0.15f, 0f, southZ), 0f);
            Place(cone, props, new Vector3(ArmBase.x + ProtectiveHalfSize + 0.15f, 0f, southZ), 0f);
        }

        private static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(go, "Build Safety Zone");
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static CinemachineCamera Trainee(Transform rig)
        {
            Transform trainee = Child(rig, "Trainee");
            trainee.gameObject.layer = LayerMask.NameToLayer(TraineeLayer);
            trainee.position = TraineeStart;
            trainee.rotation = Quaternion.LookRotation(ArmBase - TraineeStart, Vector3.up);
            trainee.rotation = Quaternion.Euler(0f, trainee.eulerAngles.y, 0f);

            var body = GetOrAdd<Rigidbody>(trainee.gameObject);
            body.isKinematic = true;
            body.useGravity = false;

            var capsule = GetOrAdd<CapsuleCollider>(trainee.gameObject);
            capsule.height = 1.8f;
            capsule.radius = 0.25f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            Transform eyeTransform = Child(trainee, "Eye");
            eyeTransform.localPosition = new Vector3(0f, EyeHeight, 0f);
            eyeTransform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            var eye = GetOrAdd<CinemachineCamera>(eyeTransform.gameObject);
            eye.Priority = 0;
            eye.Lens.FieldOfView = 70f;

            var walker = GetOrAdd<TraineeWalker>(trainee.gameObject);
            SetReference(walker, "eye", eye);
            return eye;
        }

        private static void AddTraineeShot(ShotDirector director, CinemachineCamera eye)
        {
            var so = new SerializedObject(director);
            SerializedProperty shotList = so.FindProperty("shots");
            int index = -1;
            for (int i = 0; i < shotList.arraySize; i++)
            {
                if (shotList.GetArrayElementAtIndex(i).objectReferenceValue == eye)
                    index = i;
            }
            if (index < 0)
            {
                index = shotList.arraySize;
                shotList.InsertArrayElementAtIndex(index);
                shotList.GetArrayElementAtIndex(index).objectReferenceValue = eye;
            }
            so.FindProperty("traineeShot").intValue = index;
            so.ApplyModifiedProperties();
        }

        private static void Bind(TwinSignals twinSignals, TopicPath topic, Signal target)
        {
            var so = new SerializedObject(twinSignals);
            SerializedProperty list = so.FindProperty("bindings");
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("station").stringValue == topic.Station
                    && entry.FindPropertyRelative("signal").stringValue == topic.Signal)
                {
                    entry.FindPropertyRelative("target").objectReferenceValue = target;
                    so.ApplyModifiedProperties();
                    return;
                }
            }

            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            SerializedProperty added = list.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("station").stringValue = topic.Station;
            added.FindPropertyRelative("signal").stringValue = topic.Signal;
            added.FindPropertyRelative("target").objectReferenceValue = target;
            so.ApplyModifiedProperties();
        }

        private static void SetReference(Object component, string field, Object value)
        {
            var so = new SerializedObject(component);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            // GetComponent returns a fake null in the Editor, so ?? cannot be used here.
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            return existing != null ? existing : NewChild(parent, name).transform;
        }

        private static GameObject NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Build Safety Zone");
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
