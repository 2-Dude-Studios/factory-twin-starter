using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training areas (M24): builds the hub and one area per drill into Cell.unity, repeatably, from the layout asset
    /// (Assets/_Project/Data/Training/TrainingAreas.asset, written with the defaults the first time and left alone
    /// after that, so it can be edited in the Inspector). Each area gets a floor border in its colour, an entry sign,
    /// its start spot, a briefing post, a kit station and a score board where the scene has none yet, and a teleport
    /// floor where the cell's own does not reach. The start spots are handed to the TrainingLauncher, so each training
    /// starts in its area on desktop and in the headset. Run Build Electrical Cabinet afterwards: it stands the drive
    /// cabinet in the electrical bay.
    /// </summary>
    internal static class TrainingAreasSetup
    {
        private const string CellScene = "Assets/_Project/Scenes/Cell.unity";
        private const string DataFolder = "Assets/_Project/Data/Training";
        private const string AreasPath = DataFolder + "/TrainingAreas.asset";
        private const string MaterialFolder = "Assets/_Project/Materials/TrainingAreas";

        private const float StripWidth = 0.08f;
        private const float StripThickness = 0.006f;
        private const float SignHalfWidth = 1.3f;
        private const float SignHeight = 2.5f;

        private static readonly Color BoardColour = new Color(0.05f, 0.06f, 0.07f);
        private static readonly Color SteelColour = new Color(0.32f, 0.34f, 0.36f);
        private static readonly Color Ink = new Color(0.03f, 0.03f, 0.03f);

        [MenuItem("Tools/Factory Twin/Build Training Areas")]
        private static void Build()
        {
            GameObject cell = GameObject.Find("Cell");
            if (cell == null || cell.GetComponent<CellContext>() == null)
            {
                Debug.LogError("[TrainingAreasSetup] Open Cell.unity first: Cell with its CellContext is needed.");
                return;
            }
            TrainingAreas layout = Layout();
            List<string> problems = TrainingAreaRules.Problems(layout.Areas);
            if (problems.Count > 0)
            {
                Debug.LogError("[TrainingAreasSetup] The layout in " + AreasPath + " is not sound; nothing built:\n" + string.Join("\n", problems));
                return;
            }

            Undo.SetCurrentGroupName("Build Training Areas");
            int undoGroup = Undo.GetCurrentGroup();

            Transform training = Child(cell.transform, "Training");
            Transform old = training.Find("Areas");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            Transform root = Child(training, "Areas");
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Material board = Lit("Area_Board", BoardColour, Color.black);
            Material steel = Lit("Area_Steel", SteelColour, Color.black);
            Bounds? cellTeleport = CellTeleportFloor(training);
            var starts = new List<Object>();
            int teleportFloors = 0;
            foreach (TrainingAreaSettings area in layout.Areas)
            {
                Material accent = Lit("Area_" + area.Area, area.Colour, area.Colour * 0.45f);
                Transform node = Child(root, "Area_" + area.Area);
                node.position = new Vector3(area.CentreX, 0f, area.CentreZ);
                Border(node, area, accent);
                starts.Add(StartSpot(node, area, accent));
                Sign(node, area, accent, steel);
                if (area.BuildBriefing)
                {
                    string body = area.Area == TrainingArea.Hub
                        ? "YOU ARE AT THE HUB\n\n" + TrainingAreaText.HallMap(layout.Areas, area.Start.X, area.Start.Z) + "\n\nFollow the floor border in each drill's colour."
                        : TrainingAreaText.Briefing(area.Area);
                    Post(node, "BriefingPost", area, area.Briefing, TrainingAreaText.Sign(area), body, accent, board, steel, 1.1f, 0.95f);
                }
                if (area.BuildKit)
                    Kit(node, area, accent, board, steel);
                if (area.BuildBoard)
                {
                    string header = area.Area == TrainingArea.Hub ? "BADGE WALL" : "SCORES";
                    string body = area.Area == TrainingArea.Hub
                        ? "Stars and badges for every drill.\n\n" + TrainingAreaText.Stars()
                        : TrainingAreaText.Stars();
                    Post(node, "ScoreBoard", area, area.Board, header, body, accent, board, steel, 0.9f, 0.7f);
                }
                if (TeleportFloor(node, area, cellTeleport))
                    teleportFloors++;
            }

            WireLauncher(starts);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(cell.scene);
            Debug.Log($"[TrainingAreasSetup] {layout.Areas.Count} training areas built under Cell/Training/Areas ({teleportFloors} with their own teleport floor). " +
                      "Run Build Electrical Cabinet to stand the drive cabinet in the electrical bay.", root);
        }

        // unity -batchmode -projectPath . -executeMethod TwoDude.FactoryTwin.UI.TrainingAreasSetup.BuildFromCommandLine -quit
        public static void BuildFromCommandLine()
        {
            EditorSceneManager.OpenScene(CellScene, OpenSceneMode.Single);
            Build();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        // ---- Assets ----------------------------------------------------------------------------------------------

        // The layout keeps whatever is in it: areas are moved in the asset, not in this tool.
        private static TrainingAreas Layout()
        {
            EnsureFolder(DataFolder);
            var layout = AssetDatabase.LoadAssetAtPath<TrainingAreas>(AreasPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<TrainingAreas>();
                layout.Configure(TrainingAreaDefaults.All());
                AssetDatabase.CreateAsset(layout, AreasPath);
                AssetDatabase.SaveAssets();
            }
            return layout;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static Material Lit(string name, Color colour, Color emission)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", colour);
            mat.SetFloat("_Smoothness", 0.2f);
            if (emission.maxColorComponent > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                mat.SetColor("_EmissionColor", emission);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---- One area --------------------------------------------------------------------------------------------

        // Four strips on the floor along the border, a hair above it.
        private static void Border(Transform node, TrainingAreaSettings area, Material accent)
        {
            Transform border = Child(node, "Border");
            float y = StripThickness * 0.5f + 0.002f;
            float halfX = area.SizeX * 0.5f, halfZ = area.SizeZ * 0.5f;
            Box(border, "South", new Vector3(0f, y, -halfZ), new Vector3(area.SizeX + StripWidth, StripThickness, StripWidth), accent, false);
            Box(border, "North", new Vector3(0f, y, halfZ), new Vector3(area.SizeX + StripWidth, StripThickness, StripWidth), accent, false);
            Box(border, "West", new Vector3(-halfX, y, 0f), new Vector3(StripWidth, StripThickness, area.SizeZ + StripWidth), accent, false);
            Box(border, "East", new Vector3(halfX, y, 0f), new Vector3(StripWidth, StripThickness, area.SizeZ + StripWidth), accent, false);
        }

        // Where the drill starts: a disc on the floor with a bar pointing the way the trainee faces.
        private static TrainingAreaStart StartSpot(Transform node, TrainingAreaSettings area, Material accent)
        {
            Transform start = Child(node, "Start");
            start.SetPositionAndRotation(new Vector3(area.Start.X, 0f, area.Start.Z), Quaternion.Euler(0f, area.Start.YawDegrees, 0f));
            var spot = GetOrAdd<TrainingAreaStart>(start.gameObject);
            var so = new SerializedObject(spot);
            so.FindProperty("area").intValue = (int)area.Area;
            so.ApplyModifiedProperties();

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.name = "Disc";
            disc.transform.SetParent(start, false);
            disc.transform.localPosition = new Vector3(0f, 0.004f, 0f);
            disc.transform.localScale = new Vector3(0.7f, 0.002f, 0.7f);
            Dress(disc, accent);
            Box(start, "Facing", new Vector3(0f, 0.005f, 0.52f), new Vector3(0.08f, StripThickness, 0.3f), accent, false);
            return spot;
        }

        // A gateway on the area's sign edge (by the start, unless the layout says otherwise): two posts and a banner
        // with the drill's name on both sides.
        private static void Sign(Transform node, TrainingAreaSettings area, Material accent, Material steel)
        {
            AreaEdge edge = TrainingAreaRules.SignEdge(area);
            bool alongX = edge == AreaEdge.South || edge == AreaEdge.North;
            float min = (alongX ? area.MinX : area.MinZ) + SignHalfWidth + 0.1f;
            float max = (alongX ? area.MaxX : area.MaxZ) - SignHalfWidth - 0.1f;
            float along = min <= max ? Mathf.Clamp(alongX ? area.Start.X : area.Start.Z, min, max) : (alongX ? area.CentreX : area.CentreZ);
            Vector3 at;
            float yaw;
            switch (edge)
            {
                case AreaEdge.South: at = new Vector3(along, 0f, area.MinZ); yaw = 0f; break;
                case AreaEdge.North: at = new Vector3(along, 0f, area.MaxZ); yaw = 180f; break;
                case AreaEdge.West: at = new Vector3(area.MinX, 0f, along); yaw = 90f; break;
                default: at = new Vector3(area.MaxX, 0f, along); yaw = 270f; break;
            }
            // Local frame: x along the edge, −z out of the area. TextMeshPro reads from the side its −z faces.
            Transform sign = Child(node, "Sign");
            sign.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            float top = SignHeight + 0.21f;
            Box(sign, "Post_L", new Vector3(-SignHalfWidth, top * 0.5f, 0f), new Vector3(0.07f, top, 0.07f), steel, false);
            Box(sign, "Post_R", new Vector3(SignHalfWidth, top * 0.5f, 0f), new Vector3(0.07f, top, 0.07f), steel, false);
            Box(sign, "Banner", new Vector3(0f, SignHeight, 0f), new Vector3(SignHalfWidth * 2f + 0.07f, 0.42f, 0.05f), accent, false);
            Color ink = InkOn(area.Colour);
            var size = new Vector2(SignHalfWidth * 2f - 0.15f, 0.3f);
            Text(sign, "Text_Out", TrainingAreaText.Sign(area), new Vector3(0f, SignHeight, -0.027f), 0f, size, 2.4f, true, ink, TMPro.TextAlignmentOptions.Center);
            Text(sign, "Text_In", TrainingAreaText.Sign(area), new Vector3(0f, SignHeight, 0.027f), 180f, size, 2.4f, true, ink, TMPro.TextAlignmentOptions.Center);
        }

        // A board on a post, turned to face the start spot: a header band in the area's colour and a body text.
        private static void Post(Transform node, string name, TrainingAreaSettings area, AreaSpot spot, string header, string body,
            Material accent, Material board, Material steel, float width, float height)
        {
            Transform post = Child(node, name);
            // Its +z points away from the reader, so its text (which reads from −z) faces the start.
            float yaw = (float)TrainingAreaRules.YawTowards(area.Start.X, area.Start.Z, spot.X, spot.Z);
            post.SetPositionAndRotation(new Vector3(spot.X, 0f, spot.Z), Quaternion.Euler(0f, yaw, 0f));
            float centre = 1.45f, bandHeight = 0.14f;
            Box(post, "Leg", new Vector3(0f, (centre - height * 0.5f) * 0.5f, 0.03f), new Vector3(0.07f, centre - height * 0.5f, 0.07f), steel, false);
            Box(post, "Foot", new Vector3(0f, 0.015f, 0.03f), new Vector3(0.5f, 0.03f, 0.4f), steel, false);
            Box(post, "Panel", new Vector3(0f, centre, 0f), new Vector3(width, height, 0.04f), board, true);
            float bandY = centre + height * 0.5f - bandHeight * 0.5f;
            Box(post, "Band", new Vector3(0f, bandY, -0.004f), new Vector3(width, bandHeight, 0.04f), accent, false);
            Text(post, "Header", header, new Vector3(0f, bandY, -0.026f), 0f, new Vector2(width - 0.06f, bandHeight - 0.03f), 1.0f, true,
                InkOn(area.Colour), TMPro.TextAlignmentOptions.Center);
            Text(post, "Body", body, new Vector3(0f, centre - bandHeight * 0.5f, -0.022f), 0f, new Vector2(width - 0.1f, height - bandHeight - 0.08f), 0.5f, false,
                Color.white, TMPro.TextAlignmentOptions.TopLeft);
        }

        // The kit station: a bench with a back panel and its name. What lies on it comes with the drill's own milestone.
        private static void Kit(Transform node, TrainingAreaSettings area, Material accent, Material board, Material steel)
        {
            Transform kit = Child(node, "KitStation");
            float yaw = (float)TrainingAreaRules.YawTowards(area.Start.X, area.Start.Z, area.Kit.X, area.Kit.Z);
            kit.SetPositionAndRotation(new Vector3(area.Kit.X, 0f, area.Kit.Z), Quaternion.Euler(0f, yaw, 0f));
            const float width = 1.6f, depth = 0.7f, top = 0.9f;
            Box(kit, "Top", new Vector3(0f, top - 0.025f, 0f), new Vector3(width, 0.05f, depth), steel, true);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * (width * 0.5f - 0.06f), z = (i < 2 ? -1f : 1f) * (depth * 0.5f - 0.06f);
                Box(kit, "Leg_" + (i + 1), new Vector3(x, (top - 0.05f) * 0.5f, z), new Vector3(0.05f, top - 0.05f, 0.05f), steel, false);
            }
            Box(kit, "Back", new Vector3(0f, top + 0.45f, depth * 0.5f - 0.02f), new Vector3(width, 0.9f, 0.04f), board, false);
            Box(kit, "Band", new Vector3(0f, top + 0.83f, depth * 0.5f - 0.024f), new Vector3(width, 0.14f, 0.04f), accent, false);
            Text(kit, "Header", area.KitTitle, new Vector3(0f, top + 0.83f, depth * 0.5f - 0.046f), 0f, new Vector2(width - 0.08f, 0.11f), 1.0f, true,
                InkOn(area.Colour), TMPro.TextAlignmentOptions.Center);
        }

        // A teleport floor over the area, unless the cell's own (Build VR Rig) already covers it.
        private static bool TeleportFloor(Transform node, TrainingAreaSettings area, Bounds? cellTeleport)
        {
            if (cellTeleport.HasValue)
            {
                Bounds b = cellTeleport.Value;
                if (area.MinX >= b.min.x && area.MaxX <= b.max.x && area.MinZ >= b.min.z && area.MaxZ <= b.max.z)
                    return false;
            }
            Transform floor = Child(node, "TeleportFloor");
            floor.localPosition = new Vector3(0f, -0.01f, 0f);
            var box = GetOrAdd<BoxCollider>(floor.gameObject);
            box.size = new Vector3(area.SizeX, 0.02f, area.SizeZ);
            GetOrAdd<TeleportationArea>(floor.gameObject);
            return true;
        }

        private static Bounds? CellTeleportFloor(Transform training)
        {
            Transform floor = training.Find("TeleportFloor");
            BoxCollider box = floor != null ? floor.GetComponent<BoxCollider>() : null;
            if (box == null)
                return null;
            return new Bounds(floor.TransformPoint(box.center), Vector3.Scale(box.size, floor.lossyScale));
        }

        // ---- The launcher ----------------------------------------------------------------------------------------

        private static void WireLauncher(List<Object> starts)
        {
            var launcher = Object.FindAnyObjectByType<TrainingLauncher>(FindObjectsInactive.Include);
            if (launcher == null)
            {
                Debug.LogWarning("[TrainingAreasSetup] No TrainingLauncher in the scene (Build Fire Drill (VR) adds it); the trainings will not start at their areas yet.");
                return;
            }
            var so = new SerializedObject(launcher);
            SerializedProperty array = so.FindProperty("starts");
            array.arraySize = starts.Count;
            for (int i = 0; i < starts.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = starts[i];
            so.FindProperty("trainee").objectReferenceValue = Object.FindAnyObjectByType<TraineeWalker>(FindObjectsInactive.Include);
            so.FindProperty("origin").objectReferenceValue = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            so.ApplyModifiedProperties();
            Debug.Log($"[TrainingAreasSetup] TrainingLauncher has {starts.Count} start spots.", launcher);
        }

        // ---- Helpers ---------------------------------------------------------------------------------------------

        // Dark ink on a light colour, white on a dark one.
        private static Color InkOn(Color colour) =>
            0.2126f * colour.r + 0.7152f * colour.g + 0.0722f * colour.b > 0.55f ? Ink : Color.white;

        private static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Material mat, bool solid)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!solid)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            Dress(go, mat);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go;
        }

        private static void Dress(GameObject go, Material mat)
        {
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void Text(Transform parent, string name, string text, Vector3 localPosition, float localYaw, Vector2 size, float fontSize,
            bool bold, Color colour, TMPro.TextAlignmentOptions alignment)
        {
            Transform t = Child(parent, name);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.Euler(0f, localYaw, 0f);
            var tmp = GetOrAdd<TMPro.TextMeshPro>(t.gameObject);
            tmp.text = text;
            tmp.color = colour;
            tmp.alignment = alignment;
            tmp.fontStyle = bold ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            tmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.1f;
            tmp.fontSizeMax = fontSize;
            tmp.fontSize = fontSize;
            tmp.rectTransform.sizeDelta = size;
            EditorUtility.SetDirty(tmp);
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                return child;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go.transform;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }
    }
}
