using System.Collections.Generic;
using TMPro;
using TwoDude.FactoryTwin.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training (M11): builds the lockout training points around the CNC, repeatably, from the machine's measured
    /// bounds. A line control station east of the CNC (Start, Stop, intercom Notify and All clear), the isolator, its
    /// lock hasp, the air dump valve and the inspection plate on the CNC's east end, the tool change on its front.
    /// Also gives the trainee the interactor and the HUD. Running it again rebuilds the points in place. Parts with
    /// a model of the same name in Assets/_Project/Art/Training (tools/blender/training_details.py) use its mesh;
    /// the rest are placeholder primitives. The buttons stay primitive caps, so their lit colours are unchanged.
    /// </summary>
    internal static class TrainingPointsSetup
    {
        private const string LampMaterialPath = "Assets/_Project/Materials/StackLightLamp.mat";
        private const string PoleMaterialPath = "Assets/_Project/Materials/StackLightPole.mat";
        private const string HudUxmlPath = "Assets/_Project/UI/TraineeHud.uxml";
        private const string PanelSettingsPath = "Assets/_Project/UI/DashboardPanelSettings.asset";
        private const string ModelFolder = "Assets/_Project/Art/Training";
        // The station's legends and header: about 11 mm capitals, read from an arm's length.
        private const float LegendSize = 0.15f;

        private static Dictionary<string, Transform> _models = new Dictionary<string, Transform>();

        private static readonly Color Green = new Color(0.15f, 0.85f, 0.35f);
        private static readonly Color Red = new Color(1f, 0.12f, 0.08f);
        private static readonly Color Blue = new Color(0.2f, 0.5f, 1f);
        private static readonly Color White = new Color(0.92f, 0.94f, 0.96f);
        private static readonly Color Amber = new Color(1f, 0.68f, 0.05f);
        private static readonly Color Cyan = new Color(0.1f, 0.8f, 0.85f);
        private static readonly Color Yellow = new Color(1f, 0.82f, 0.1f);

        // The CNC's written energy control procedure (1910.147(c)(4)), what the trainee reads to prepare (d)(1).
        private const string ProcedureText =
            "ENERGY CONTROL PROCEDURE\nCNC machining centre 1\n\n" +
            "1 Electrical, 400 V AC:\n   main isolator, yellow box\n" +
            "2 Pneumatic, 6 bar:\n   air-dump valve, below it\n" +
            "3 Stored: spindle inertia,\n   wait until it is at rest\n\n" +
            "Lock: hasp under the isolator\nVerify: press Start,\n   nothing may move";

        [MenuItem("Tools/Factory Twin/Build Training Points")]
        private static void Build()
        {
            GameObject cell = GameObject.Find("Cell");
            GameObject cnc = GameObject.Find("Cell/Press/PressMachine");
            GameObject training = GameObject.Find("Cell/Training");
            CellContext context = cell != null ? cell.GetComponent<CellContext>() : null;
            GameObject trainee = GameObject.Find("CaptureRig/Trainee");
            if (cell == null || cnc == null || training == null || context == null || trainee == null)
            {
                Debug.LogError("[TrainingPointsSetup] Open Cell.unity first: Cell, Cell/Press/PressMachine, Cell/Training and CaptureRig/Trainee are needed (run Build Safety Zone first).");
                return;
            }

            _models = LoadModels();
            var lamp = AssetDatabase.LoadAssetAtPath<Material>(LampMaterialPath);
            var pole = AssetDatabase.LoadAssetAtPath<Material>(PoleMaterialPath);
            Bounds b = MachineBounds(cnc);

            Undo.SetCurrentGroupName("Build Training Points");
            int undoGroup = Undo.GetCurrentGroup();

            Transform points = Child(training.transform, "Points");
            for (int i = points.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(points.GetChild(i).gameObject);

            // Line control station east of the CNC, its panel facing the cell (south), clear of both safety zones: a
            // 440 x 300 mm enclosure on a post, four buttons in chrome bezels, each with its legend beside it.
            var stationBase = new Vector3(b.max.x + 0.9f, 0f, b.min.z - 0.3f);
            Part(points, "ControlStation_Post", PrimitiveType.Cube, stationBase + new Vector3(0f, 0.5f, 0f), new Vector3(0.1f, 1f, 0.1f), pole);
            Part(points, "ControlStation_Panel", PrimitiveType.Cube, stationBase + new Vector3(0f, 1.1f, 0f), new Vector3(0.44f, 0.3f, 0.07f), pole);
            Vector3 face = stationBase + new Vector3(0f, 1.1f, -0.035f);
            var buttons = new (string name, TrainingPointKind kind, Vector2 at, Color color, string legend)[]
            {
                ("StartButton", TrainingPointKind.StartButton, new Vector2(-0.14f, 0.065f), Green, "START"),
                ("StopButton", TrainingPointKind.StopButton, new Vector2(-0.14f, -0.065f), Red, "STOP"),
                ("NotifyButton", TrainingPointKind.NotifyButton, new Vector2(0.07f, 0.065f), Blue, "NOTIFY"),
                ("AllClearButton", TrainingPointKind.AllClearButton, new Vector2(0.07f, -0.065f), White, "ALL CLEAR"),
            };
            foreach (var button in buttons)
            {
                Vector3 at = face + new Vector3(button.at.x, button.at.y, 0f);
                Button(points, button.name, button.kind, at, button.color, lamp, context);
                if (_models.ContainsKey("PushButton_Bezel"))
                    Part(points, button.name + "_Bezel", PrimitiveType.Cylinder, at, Vector3.one, pole, "PushButton_Bezel");
                Label(points, button.name + "_Legend", button.legend, at + new Vector3(0.054f, 0.0095f, -0.0006f), Quaternion.identity,
                    new Vector2(0.09f, 0.02f), Color.white, LegendSize);
            }
            Label(points, "ControlStation_Header", "LINE CONTROL  -  CELL 1", face + new Vector3(-0.17f, 0.128f, -0.0006f), Quaternion.identity,
                new Vector2(0.34f, 0.022f), Color.white, LegendSize);

            // On the CNC's east end, outside both safety zones: isolator with the lock hasp under it, the air dump
            // valve, and the inspection plate at the back.
            float east = b.max.x + 0.06f;
            Point(points, "Isolator", TrainingPointKind.Isolator, PrimitiveType.Cube, new Vector3(east, 1.25f, b.center.z - 0.5f), new Vector3(0.12f, 0.24f, 0.18f), Amber, lamp, context);
            Point(points, "LockHasp", TrainingPointKind.LockHasp, PrimitiveType.Cube, new Vector3(east + 0.01f, 1.06f, b.center.z - 0.5f), new Vector3(0.05f, 0.07f, 0.07f), Red, lamp, context);
            Point(points, "AirDumpValve", TrainingPointKind.AirDumpValve, PrimitiveType.Cylinder, new Vector3(east, 0.7f, b.center.z + 0.1f), new Vector3(0.1f, 0.03f, 0.1f), Blue, lamp, context);
            // The procedure card above the isolator, read first.
            Vector3 card = new Vector3(east, 1.62f, b.center.z - 0.5f);
            Point(points, "ProcedureCard", TrainingPointKind.ProcedureCard, PrimitiveType.Cube, card, new Vector3(0.02f, 0.36f, 0.30f), Yellow, lamp, context);
            Label(points, "ProcedureCard_Text", ProcedureText, card + new Vector3(0.0105f, 0.165f, -0.14f), Quaternion.Euler(0f, -90f, 0f),
                new Vector2(0.28f, 0.33f), Color.black);
            // The inspection access door, a window onto the tool area with the guard interlock and its checklist,
            // flush on the east end above the air valve: further north the machine's side steps in and it floated.
            var inspection = new Vector3(b.max.x + 0.011f, 1.2f, b.center.z + 0.1f);
            Point(points, "Inspection", TrainingPointKind.Inspection, PrimitiveType.Cube, inspection, new Vector3(0.02f, 0.2f, 0.2f), White, lamp, context);
            if (_models.ContainsKey("Inspection"))
                Label(points, "Inspection_Checklist", "INSPECT BEFORE RESTART\n- tools removed\n- guards closed\n- area clear",
                    inspection + new Vector3(0.0032f, -0.027f, -0.078f), Quaternion.Euler(0f, -90f, 0f), new Vector2(0.126f, 0.056f), Color.black);

            // The tool change at the east corner of the CNC's front: most of the front is behind the fence, and standing
            // further west would put the trainee in the protective zone.
            Point(points, "ToolChange", TrainingPointKind.ToolChange, PrimitiveType.Cube, new Vector3(b.max.x - 0.2f, 1.15f, b.min.z - 0.02f), new Vector3(0.24f, 0.24f, 0.02f), Cyan, lamp, context);

            WireTrainee(trainee);
            Guide(training.transform, points, lamp, context);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(cell.scene);
            Debug.Log($"[TrainingPointsSetup] Built {points.childCount} objects around the CNC (bounds {b.min:F2} to {b.max:F2}); trainee interactor and HUD wired.", points);
        }

        // The guide (M12): a beacon, a ball on a short stem pointing down, that bobs above the point the step due needs.
        // No colliders, so it never takes a click or a ray.
        private static void Guide(Transform training, Transform points, Material lamp, CellContext context)
        {
            Transform old = training.Find("Guide");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var guide = new GameObject("Guide");
            Undo.RegisterCreatedObjectUndo(guide, "Build Training Points");
            guide.transform.SetParent(training, false);
            var beacon = new GameObject("Beacon");
            beacon.transform.SetParent(guide.transform, false);
            GameObject ball = Part(beacon.transform, "Ball", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.07f, lamp);
            GameObject stem = Part(beacon.transform, "Stem", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.015f, 0.06f, 0.015f), lamp);
            ball.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            stem.transform.localPosition = new Vector3(0f, 0.03f, 0f);

            var component = Undo.AddComponent<TrainingGuide>(guide);
            var so = new SerializedObject(component);
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("beacon").objectReferenceValue = beacon.transform;
            TrainingPoint[] all = points.GetComponentsInChildren<TrainingPoint>(true);
            SerializedProperty list = so.FindProperty("points");
            list.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            SerializedProperty renderers = so.FindProperty("beaconRenderers");
            renderers.arraySize = 2;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = ball.GetComponent<Renderer>();
            renderers.GetArrayElementAtIndex(1).objectReferenceValue = stem.GetComponent<Renderer>();
            so.ApplyModifiedProperties();
        }

        // Every renderer of the machine: the case alone is smaller than the machine body, which would hide the points.
        private static Bounds MachineBounds(GameObject machine)
        {
            Renderer[] renderers = machine.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void Button(Transform parent, string name, TrainingPointKind kind, Vector3 position, Color color, Material material, CellContext context)
        {
            GameObject button = Point(parent, name, kind, PrimitiveType.Cylinder, position, new Vector3(0.06f, 0.015f, 0.06f), color, material, context);
            button.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static GameObject Point(Transform parent, string name, TrainingPointKind kind, PrimitiveType shape, Vector3 position,
            Vector3 size, Color color, Material material, CellContext context)
        {
            GameObject go = Part(parent, name, shape, position, size, material);
            // A trigger box a little larger than the part, so it is easy to aim at; never in the way of anything. A
            // primitive is scaled to the size; a model is at its real size.
            var box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            box.size = _models.ContainsKey(name) ? size * 1.6f : Vector3.one * 1.6f;

            var point = Undo.AddComponent<TrainingPoint>(go);
            var so = new SerializedObject(point);
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("color").colorValue = color;
            SerializedProperty highlight = so.FindProperty("highlight");
            highlight.arraySize = 1;
            highlight.GetArrayElementAtIndex(0).objectReferenceValue = go.GetComponent<Renderer>();
            so.ApplyModifiedProperties();
            return go;
        }

        // Text on a part's face, its upper-left corner at the position, sized to fit the box (metres). The rotation
        // turns it to face its reader: identity reads from the south, (0, -90, 0) from the east. TextMeshPro, not
        // TextMesh: the legacy font material ignores depth and showed through the card from behind. A plain transform
        // carries the place, since a RectTransform under a non-UI parent keeps only its anchored position.
        private static void Label(Transform parent, string name, string text, Vector3 position, Quaternion rotation, Vector2 box, Color color,
            float fontSize = 0f)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Build Training Points");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var words = new GameObject("Text");
            words.transform.SetParent(go.transform, false);
            var tmp = words.AddComponent<TextMeshPro>();
            tmp.rectTransform.pivot = new Vector2(0f, 1f);
            tmp.rectTransform.sizeDelta = box;
            tmp.rectTransform.anchoredPosition = Vector2.zero;
            tmp.text = text;
            // A fixed size keeps a set of labels alike; without one the text fits its box.
            tmp.enableAutoSizing = fontSize <= 0f;
            tmp.fontSize = fontSize > 0f ? fontSize : 0.1f;
            tmp.fontSizeMin = 0.01f;
            tmp.fontSizeMax = 0.3f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = color;
        }

        // A part: the model of that name (or of model) at its real size when there is one, else a placeholder
        // primitive scaled to the size.
        private static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 size, Material material,
            string model = null)
        {
            GameObject go;
            if (_models.TryGetValue(model ?? name, out Transform source))
            {
                go = new GameObject(name);
                go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                go.transform.localScale = source.localScale;
                go.transform.rotation = source.localRotation;
            }
            else
            {
                go = GameObject.CreatePrimitive(shape);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.localScale = size;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            Undo.RegisterCreatedObjectUndo(go, "Build Training Points");
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.layer = 0;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // Every object in the models under Assets/_Project/Art/Training, by name, that carries a mesh.
        private static Dictionary<string, Transform> LoadModels()
        {
            var models = new Dictionary<string, Transform>();
            if (!AssetDatabase.IsValidFolder(ModelFolder))
                return models;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (root == null)
                    continue;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh != null && filter.GetComponent<MeshRenderer>() != null && !models.ContainsKey(filter.name))
                        models.Add(filter.name, filter.transform);
                }
            }
            return models;
        }

        private static void WireTrainee(GameObject trainee)
        {
            Transform rig = trainee.transform.parent;
            Transform hudTransform = Child(rig, "TraineeHud");
            var document = GetOrAdd<UIDocument>(hudTransform.gameObject);
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
            document.sortingOrder = 5;
            var hud = GetOrAdd<TraineeHud>(hudTransform.gameObject);
            Set(hud, "document", document);

            var interactor = GetOrAdd<TraineeInteractor>(trainee);
            CinemachineCamera eye = trainee.GetComponentInChildren<CinemachineCamera>();
            Set(interactor, "eye", eye);
            Set(interactor, "hud", hud);
        }

        private static void Set(Object component, string field, Object value)
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
            if (existing != null)
                return existing;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Build Training Points");
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
