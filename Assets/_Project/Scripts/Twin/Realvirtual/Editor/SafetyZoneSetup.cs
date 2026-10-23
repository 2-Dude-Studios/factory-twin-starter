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
