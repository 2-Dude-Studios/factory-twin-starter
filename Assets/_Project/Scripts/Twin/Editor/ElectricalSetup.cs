using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Electrical safety (M19): builds the drive cabinet into Cell.unity, repeatably. Writes the scenario assets E1–E4
    /// and the arc flash study (Assets/_Project/Data/Electrical; an existing study is left as it is), adds
    /// Cell/Electrical with the CabinetStation, our own cabinet DCP-1 from Drive_Cabinet.fbx
    /// (tools/blender/drive_cabinet.py) in the electrical training bay (M24; the layout asset says where) with its
    /// door, Q1 handle, DC bus LEDs, 15 probe points and the arc flash label, the three boundary rings, the boundary
    /// monitor on the trainee's head and hands, a cable tray to the infeed drive, and the dashboard's electrical
    /// panel. It removes realvirtual's scaled ControlCabinet box from Cell/Fire and moves the F1 fire and its two
    /// detector heads to the new cabinet. Running it again updates what is there.
    /// </summary>
    internal static class ElectricalSetup
    {
        private const string CellScene = "Assets/_Project/Scenes/Cell.unity";
        private const string DataFolder = "Assets/_Project/Data/Electrical";
        private const string MaterialFolder = "Assets/_Project/Materials/Electrical";
        private const string ModelPath = "Assets/_Project/Art/Electrical/Drive_Cabinet.fbx";
        private const string StudyPath = DataFolder + "/ArcFlashStudy.asset";
        private const string ElectricalPanelType = "TwoDude.FactoryTwin.UI.ElectricalPanel, TwoDude.FactoryTwin.UI";

        public const string CabinetPath = "Cell/Electrical/DriveCabinet";
        public const string AreasPath = "Assets/_Project/Data/Training/TrainingAreas.asset";

        /// <summary>
        /// Where the cabinet stands and which way its door faces: the electrical training bay's machine spot (M24),
        /// from the layout asset when there is one, else the default layout. Until 2026-11-11 it stood on Rev 6's
        /// cabinet footprint at (0.9, −1.08), inside the cell; its pivot is its footprint's centre.
        /// </summary>
        public static AreaSpot CabinetSpot()
        {
            var layout = AssetDatabase.LoadAssetAtPath<TrainingAreas>(AreasPath);
            TrainingAreaSettings bay = layout != null ? layout.Find(TrainingArea.Electrical) : null;
            if (bay == null || !bay.HasMachine)
                bay = TrainingAreaDefaults.For(TrainingArea.Electrical);
            return bay.Machine;
        }

        // The exposed parts behind the door, in the cabinet's space (Drive_Cabinet.fbx): the mounting plate's face to
        // the door opening, the inside of the side walls, floor to roof.
        private static readonly Vector3 ExposedCentre = new Vector3(0f, 1.05f, 0.005f);
        private static readonly Vector3 ExposedSize = new Vector3(0.74f, 1.84f, 0.40f);

        // The label on the door's face, in the cabinet's space: left of Q1's handle as you face it, at eye height.
        private static readonly Vector3 LabelCentre = new Vector3(0.08f, 1.675f, 0.2475f);
        private static readonly Vector2 LabelSize = new Vector2(0.18f, 0.25f);

        [MenuItem("Tools/Factory Twin/Build Electrical Cabinet")]
        private static void Build()
        {
            GameObject cell = GameObject.Find("Cell");
            CellContext context = cell != null ? cell.GetComponent<CellContext>() : null;
            if (cell == null || context == null)
            {
                Debug.LogError("[ElectricalSetup] Open Cell.unity first: Cell with its CellContext is needed.");
                return;
            }
            Dictionary<string, Transform> parts = LoadParts();
            if (!parts.ContainsKey("Cab_Body"))
            {
                Debug.LogError($"[ElectricalSetup] {ModelPath} has no Cab_Body; export it first (tools/blender/drive_cabinet.py).");
                return;
            }

            Undo.SetCurrentGroupName("Build Electrical Cabinet");
            int undoGroup = Undo.GetCurrentGroup();

            List<ElectricalScenario> scenarios = WriteScenarios();
            ArcFlashStudy study = Study();

            Transform root = Child(cell.transform, "Electrical");
            root.localPosition = Vector3.zero;
            var station = GetOrAdd<CabinetStation>(root.gameObject);
            SetObject(station, "context", context);
            SetArray(station, "scenarios", scenarios.ToArray());

            Transform cabinet = Cabinet(root, parts, context, study);
            Transform exposed = Child(cabinet, "ExposedParts");
            exposed.localPosition = ExposedCentre;
            exposed.localRotation = Quaternion.identity;

            Rings(cabinet, context, study);
            Monitor(root, context, study, exposed);
            CableTray(root, cabinet);
            ReplaceFireBox(cabinet);
            DashboardPanel(context);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(cell.scene);
            if (TryBounds(cabinet.gameObject, out Bounds b))
                Debug.Log($"[ElectricalSetup] DriveCabinet DCP-1 at {cabinet.position:F2}, bounds {b.min:F2} to {b.max:F2}.", cabinet);
            Debug.Log($"[ElectricalSetup] Electrical cabinet built: {scenarios.Count} scenarios, {ProbePoints.All.Length} probe points, 3 boundary rings.", root);
        }

        // unity -batchmode -projectPath . -executeMethod TwoDude.FactoryTwin.Twin.ElectricalSetup.BuildFromCommandLine -quit
        public static void BuildFromCommandLine()
        {
            EditorSceneManager.OpenScene(CellScene, OpenSceneMode.Single);
            Build();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        // ---- Assets ----------------------------------------------------------------------------------------------

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static List<ElectricalScenario> WriteScenarios()
        {
            EnsureFolder(DataFolder);
            var list = new List<ElectricalScenario>();
            for (int n = 1; n <= 4; n++)
            {
                string path = $"{DataFolder}/E{n}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<ElectricalScenario>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<ElectricalScenario>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                asset.Configure(ElectricalScenarios.ByNumber(n));
                EditorUtility.SetDirty(asset);
                list.Add(asset);
            }
            AssetDatabase.SaveAssets();
            return list;
        }

        // The study keeps whatever is in it: a client's own values are typed into the asset, not into this tool.
        private static ArcFlashStudy Study()
        {
            var study = AssetDatabase.LoadAssetAtPath<ArcFlashStudy>(StudyPath);
            if (study != null)
                return study;
            study = ScriptableObject.CreateInstance<ArcFlashStudy>();
            AssetDatabase.CreateAsset(study, StudyPath);
            AssetDatabase.SaveAssets();
            return study;
        }

        private static Material Lit(string name, Color color, bool emission)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.35f);
            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                mat.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Dictionary<string, Transform> LoadParts()
        {
            var parts = new Dictionary<string, Transform>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                return parts;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && !parts.ContainsKey(filter.name))
                    parts.Add(filter.name, filter.transform);
            }
            return parts;
        }

        // ---- The cabinet -----------------------------------------------------------------------------------------

        private static Transform Cabinet(Transform root, Dictionary<string, Transform> parts, CellContext context, ArcFlashStudy study)
        {
            Transform cabinet = Child(root, "DriveCabinet");
            AreaSpot spot = CabinetSpot();
            cabinet.SetPositionAndRotation(new Vector3(spot.X, 0f, spot.Z), Quaternion.Euler(0f, spot.YawDegrees, 0f));

            Transform body = Visual(cabinet, "Body", parts["Cab_Body"], Vector3.zero);
            Transform door = Visual(cabinet, "Door", parts["Cab_Door"], Vector3.zero);
            Transform handle = parts.TryGetValue("Cab_Handle", out Transform h)
                ? Visual(door, "Handle", h, door.localPosition)
                : null;
            Material ledMat = Lit("DcLed", new Color(1f, 0.15f, 0.05f), emission: true);
            var leds = new List<Renderer>();
            foreach (string led in new[] { "Cab_Led_U1", "Cab_Led_U2" })
            {
                if (!parts.TryGetValue(led, out Transform source))
                    continue;
                Transform t = Visual(body, led.Substring(4), source, Vector3.zero);
                Renderer r = t.GetComponentInChildren<Renderer>();
                r.sharedMaterial = ledMat;
                leds.Add(r);
            }
            GetOrAdd<BoxCollider>(door.Find("Model").gameObject);

            ProbeMarkers(cabinet, parts);
            Label(door, study);

            var animator = GetOrAdd<CabinetAnimator>(cabinet.gameObject);
            SetObject(animator, "context", context);
            SetObject(animator, "door", door);
            SetObject(animator, "handle", handle);
            SetArray(animator, "dcLeds", leds.ToArray());
            return cabinet;
        }

        // A part at its pivot (relative to its new parent's pivot in the FBX) with a "Model" child holding the mesh.
        private static Transform Visual(Transform parent, string name, Transform source, Vector3 parentPivot)
        {
            Transform t = Child(parent, name);
            t.localPosition = source.localPosition - parentPivot;
            t.localRotation = Quaternion.identity;
            Transform model = Child(t, "Model");
            model.localPosition = Vector3.zero;
            model.localRotation = source.localRotation;
            model.localScale = source.localScale;
            GetOrAdd<MeshFilter>(model.gameObject).sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            GetOrAdd<MeshRenderer>(model.gameObject).sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            return t;
        }

        // One transform per probe point at its screw's face, from the FBX's Probe_* markers (their meshes dropped).
        private static void ProbeMarkers(Transform cabinet, Dictionary<string, Transform> parts)
        {
            Transform probes = Child(cabinet, "ProbePoints");
            probes.localPosition = Vector3.zero;
            foreach (ProbePoint point in ProbePoints.All)
            {
                if (!parts.TryGetValue("Probe_" + point, out Transform source))
                {
                    Debug.LogWarning($"[ElectricalSetup] {ModelPath} has no Probe_{point}.");
                    continue;
                }
                Transform t = Child(probes, point.ToString());
                t.localPosition = source.localPosition;
                t.localRotation = Quaternion.identity;
                GetOrAdd<ProbePointMarker>(t.gameObject).Configure(point, 0.012f);
                EditorUtility.SetDirty(t.GetComponent<ProbePointMarker>());
            }
        }

        // The arc flash label: a white plate, the signal word's band, and two TextMeshPro texts filled from the study.
        private static void Label(Transform door, ArcFlashStudy study)
        {
            Transform label = Child(door, "ArcFlashLabel");
            // Quads and TextMeshPro face -Z; turned 180° they face out of the door (+Z).
            label.localPosition = LabelCentre - door.localPosition;
            label.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Quad(label, "Plate", Vector3.zero, LabelSize, Lit("LabelWhite", new Color(0.95f, 0.95f, 0.92f), false));
            Renderer band = Quad(label, "Band", new Vector3(0f, LabelSize.y / 2f - 0.022f, -0.0006f), new Vector2(LabelSize.x - 0.006f, 0.036f),
                Lit("LabelBand", Color.white, false));

            TMPro.TextMeshPro header = Text(label, "Header", new Vector3(0f, LabelSize.y / 2f - 0.022f, -0.0012f), new Vector2(LabelSize.x - 0.01f, 0.034f), 0.24f, true);
            TMPro.TextMeshPro body = Text(label, "Body", new Vector3(0f, -0.018f, -0.0012f), new Vector2(LabelSize.x - 0.016f, LabelSize.y - 0.05f), 0.07f, false);
            body.alignment = TMPro.TextAlignmentOptions.TopLeft;

            var view = GetOrAdd<ArcFlashLabelView>(label.gameObject);
            SetObject(view, "study", study);
            SetObject(view, "header", header);
            SetObject(view, "body", body);
            SetObject(view, "headerBand", band);
            header.text = ArcFlashLabel.SignalWord(study.Values);
            string text = ArcFlashLabel.Text(study.Values);
            body.text = text.Substring(text.IndexOf('\n') + 1);
        }

        private static Renderer Quad(Transform parent, string name, Vector3 at, Vector2 size, Material mat)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = name;
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, name);
                t = go.transform;
            }
            t.localPosition = at;
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(size.x, size.y, 1f);
            var r = t.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        private static TMPro.TextMeshPro Text(Transform parent, string name, Vector3 at, Vector2 size, float fontSize, bool bold)
        {
            Transform t = Child(parent, name);
            t.localPosition = at;
            t.localRotation = Quaternion.identity;
            var tmp = GetOrAdd<TMPro.TextMeshPro>(t.gameObject);
            tmp.fontSize = fontSize;
            tmp.color = new Color(0.02f, 0.02f, 0.02f);
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontStyle = bold ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            tmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = fontSize * 0.25f;
            tmp.fontSizeMax = fontSize;
            tmp.rectTransform.sizeDelta = size;
            EditorUtility.SetDirty(tmp);
            return tmp;
        }

        // ---- Boundaries ------------------------------------------------------------------------------------------

        private static void Rings(Transform cabinet, CellContext context, ArcFlashStudy study)
        {
            Material ringMat = Lit("BoundaryRing", Color.white, emission: true);
            var defs = new[]
            {
                (ApproachBoundary.ArcFlash, "Ring_ArcFlash", new Color(1f, 0.42f, 0f)),
                (ApproachBoundary.Limited, "Ring_Limited", new Color(0.95f, 0.8f, 0.05f)),
                (ApproachBoundary.Restricted, "Ring_Restricted", new Color(0.85f, 0.05f, 0.04f)),
            };
            Transform rings = Child(cabinet, "BoundaryRings");
            rings.localPosition = Vector3.zero;
            rings.localRotation = Quaternion.identity;
            Vector3 half = ExposedSize * 0.5f;
            foreach ((ApproachBoundary boundary, string name, Color color) in defs)
            {
                Transform t = Child(rings, name);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                GetOrAdd<MeshFilter>(t.gameObject);
                var renderer = GetOrAdd<MeshRenderer>(t.gameObject);
                renderer.sharedMaterial = ringMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var ring = GetOrAdd<BoundaryRing>(t.gameObject);
                var so = new SerializedObject(ring);
                so.FindProperty("context").objectReferenceValue = context;
                so.FindProperty("study").objectReferenceValue = study;
                so.FindProperty("boundary").intValue = (int)boundary;
                so.FindProperty("footprintMin").vector2Value = new Vector2(ExposedCentre.x - half.x, ExposedCentre.z - half.z);
                so.FindProperty("footprintMax").vector2Value = new Vector2(ExposedCentre.x + half.x, ExposedCentre.z + half.z);
                so.FindProperty("color").colorValue = color;
                so.ApplyModifiedProperties();
            }
        }

        // The trainee's head and hands: the desktop trainee's eye, the headset camera and both controllers.
        private static void Monitor(Transform root, CellContext context, ArcFlashStudy study, Transform exposed)
        {
            var tracked = new List<Object>();
            foreach (string path in new[] { "CaptureRig/Trainee/Eye" })
            {
                GameObject go = GameObject.Find(path);
                if (go != null)
                    tracked.Add(go.transform);
            }
            GameObject captureRig = GameObject.Find("CaptureRig");
            Transform rig = captureRig != null ? captureRig.transform.Find("XR Origin (XR Rig)") : null;
            if (rig != null)
            {
                foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Main Camera" || t.name == "Left Controller" || t.name == "Right Controller")
                        tracked.Add(t);
                }
            }
            if (tracked.Count == 0)
                Debug.LogWarning("[ElectricalSetup] No trainee eye or XR rig found; BoundaryMonitor tracks nothing yet.");

            var monitor = GetOrAdd<BoundaryMonitor>(root.gameObject);
            var so = new SerializedObject(monitor);
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("study").objectReferenceValue = study;
            so.FindProperty("exposedParts").objectReferenceValue = exposed;
            so.FindProperty("exposedSize").vector3Value = ExposedSize;
            SerializedProperty list = so.FindProperty("tracked");
            list.arraySize = tracked.Count;
            for (int i = 0; i < tracked.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = tracked[i];
            so.ApplyModifiedProperties();
            Debug.Log($"[ElectricalSetup] BoundaryMonitor tracks {tracked.Count} points.");
        }

        // ---- Rev 6 and the dashboard -----------------------------------------------------------------------------

        // Rev 6's F1 used realvirtual's ControlCabinet box scaled up at the same spot; ours replaces it, and the fire
        // anchor moves to the top of our cabinet (inside, above the VFDs) so F1 burns where it did.
        private static void ReplaceFireBox(Transform cabinet)
        {
            GameObject fire = GameObject.Find("Cell/Fire");
            if (fire == null)
            {
                Debug.Log("[ElectricalSetup] No Cell/Fire yet; Build Fire Drill will use this cabinet.");
                return;
            }
            Transform box = fire.transform.Find("DriveCabinet");
            if (box != null)
            {
                Undo.DestroyObjectImmediate(box.gameObject);
                Debug.Log("[ElectricalSetup] Removed realvirtual's scaled ControlCabinet from Cell/Fire.");
            }
            Transform anchor = fire.transform.Find("Fire_" + FireLocation.DriveCabinet);
            if (anchor != null && TryBounds(cabinet.Find("Body").gameObject, out Bounds b))
            {
                Undo.RecordObject(anchor, "Move F1");
                anchor.position = FireCabinetSpot(b);
                Debug.Log($"[ElectricalSetup] F1 fire anchor moved to {anchor.position:F2}.");
                // Its detector heads hang under the ceiling above it; they keep their height and their spacing.
                Transform smoke = fire.transform.Find("SmokeHead_" + FireLocation.DriveCabinet);
                Transform heat = fire.transform.Find("HeatHead_" + FireLocation.DriveCabinet);
                Vector3 offset = smoke != null && heat != null ? heat.position - smoke.position : new Vector3(0.6f, 0f, 0f);
                if (smoke != null)
                {
                    Undo.RecordObject(smoke, "Move F1 heads");
                    smoke.position = new Vector3(anchor.position.x, smoke.position.y, anchor.position.z);
                }
                if (heat != null)
                {
                    Undo.RecordObject(heat, "Move F1 heads");
                    heat.position = new Vector3(anchor.position.x + offset.x, heat.position.y, anchor.position.z + offset.z);
                }
            }
        }

        // ---- Cable tray ------------------------------------------------------------------------------------------

        private const float TrayHeight = 2.7f;

        // The feed from the cabinet to the infeed drive: up out of the cabinet's roof, west along an overhead tray on
        // posts, across to the south side of the conveyor, down in conduit and into the drive. Dressing only: it shows
        // why a cabinet a few metres from its conveyor still belongs to it. Rebuilt from scratch on each run.
        private static void CableTray(Transform root, Transform cabinet)
        {
            Transform old = root.Find("CableTray");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            GameObject drive = GameObject.Find("Cell/Infeed/Drive");
            if (drive == null || !TryBounds(cabinet.Find("Body").gameObject, out Bounds body))
            {
                Debug.Log("[ElectricalSetup] No Cell/Infeed/Drive (or no cabinet body); no cable tray.");
                return;
            }
            Vector3 d = TryBounds(drive, out Bounds driveBounds) ? driveBounds.center : drive.transform.position;
            Vector3 top = new Vector3(body.center.x, body.max.y, body.center.z);
            float dropX = d.x + 0.15f, dropZ = d.z - 0.5f;
            if (Mathf.Abs(top.x - dropX) < 1.5f)
            {
                Debug.Log("[ElectricalSetup] The cabinet stands by the infeed drive; no cable tray needed.");
                return;
            }

            Transform tray = Child(root, "CableTray");
            tray.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Material steel = Lit("CableTray", new Color(0.62f, 0.64f, 0.66f), emission: false);
            var up = new Vector3(top.x, TrayHeight, top.z);
            var corner = new Vector3(dropX, TrayHeight, top.z);
            var over = new Vector3(dropX, TrayHeight, dropZ);
            var down = new Vector3(dropX, d.y, dropZ);
            var into = new Vector3(dropX, d.y, d.z - 0.12f);
            Run(tray, "Riser", top, up, 0.09f, 0.09f, steel);
            Run(tray, "Tray_Run", up, corner, 0.22f, 0.06f, steel);
            Run(tray, "Tray_Cross", corner, over, 0.22f, 0.06f, steel);
            Run(tray, "Drop", over, down, 0.07f, 0.07f, steel);
            Run(tray, "Stub", down, into, 0.07f, 0.07f, steel);

            // Posts under the long run, clear of both ends, no more than 2.4 m apart.
            float from = Mathf.Min(up.x, corner.x) + 0.9f, to = Mathf.Max(up.x, corner.x) - 1.2f;
            int posts = Mathf.Max(1, Mathf.CeilToInt((to - from) / 2.4f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float x = posts == 1 ? (from + to) * 0.5f : Mathf.Lerp(from, to, i / (float)(posts - 1));
                Run(tray, "Post_" + (i + 1), new Vector3(x, 0f, top.z), new Vector3(x, TrayHeight - 0.03f, top.z), 0.06f, 0.06f, steel);
            }
            Debug.Log($"[ElectricalSetup] Cable tray from the cabinet to the infeed drive at {d:F2}: {Mathf.Abs(up.x - corner.x):F1} m run, {posts} posts.");
        }

        // A straight box between two points, as wide and deep as given (a tray, a conduit, a post).
        private static void Run(Transform parent, string name, Vector3 a, Vector3 b, float width, float depth, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            Vector3 along = b - a;
            go.transform.position = (a + b) * 0.5f;
            bool vertical = Mathf.Abs(along.y) > Mathf.Max(Mathf.Abs(along.x), Mathf.Abs(along.z));
            bool alongX = Mathf.Abs(along.x) >= Mathf.Abs(along.z);
            float length = along.magnitude + (vertical ? 0f : width);
            go.transform.localScale = vertical ? new Vector3(width, length, depth)
                : alongX ? new Vector3(length, depth, width) : new Vector3(width, depth, length);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Where the F1 fire burns on the drive cabinet: the top centre, 0.25 m down (as FireSetup placed it).</summary>
        public static Vector3 FireCabinetSpot(Bounds cabinetBounds) =>
            new Vector3(cabinetBounds.center.x, cabinetBounds.max.y - 0.25f, cabinetBounds.center.z);

        private static void DashboardPanel(CellContext context)
        {
            Type panelType = Type.GetType(ElectricalPanelType);
            GameObject dashboard = GameObject.Find("Dashboard");
            var document = dashboard != null ? dashboard.GetComponent<UnityEngine.UIElements.UIDocument>() : null;
            if (panelType == null || document == null)
            {
                Debug.LogWarning("[ElectricalSetup] No Dashboard with a UIDocument (or no ElectricalPanel type); add ElectricalPanel by hand.");
                return;
            }
            Component panel = dashboard.GetComponent(panelType);
            if (panel == null)
                panel = Undo.AddComponent(dashboard, panelType);
            var so = new SerializedObject(panel);
            so.FindProperty("document").objectReferenceValue = document;
            so.FindProperty("context").objectReferenceValue = context;
            so.ApplyModifiedProperties();
        }

        // ---- Helpers ---------------------------------------------------------------------------------------------

        private static bool TryBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            if (go == null)
                return false;
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return false;
            bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
                bounds.Encapsulate(r.bounds);
            return true;
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

        private static void SetObject(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedProperties();
        }
    }
}
