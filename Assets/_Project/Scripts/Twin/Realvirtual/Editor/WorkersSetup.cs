using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training (M12): places four simulated co-workers beside the cell from the Quaternius "Animated Human" model
    /// (CC0, Assets/ThirdParty/Quaternius). Two operators, the affected employees of a lockout: one tends the infeed,
    /// east of the warning zone, standing and working by turns, one works the outfeed end, west of it, and walks to the
    /// aisle and back; when notified of a lockout both stand clear until told the lock is off. A maintenance colleague
    /// comes into the CNC's work area after the tool change, so the trainee's area check finds them (WorkArea). The
    /// zone walker waits south of the robot zone on the Trainee layer until the commissioning suite sends them in, so
    /// the real zone sensors see them; a NavMesh baked over the cell's floor (Cell/Training/WalkArea) takes them round
    /// the conveyors and props, and each spot is snapped to it and checked against the zone it is meant to be in.
    /// Their clothes are our own palette textures, so nothing in ThirdParty is edited beyond its import settings.
    /// Run Build Training Points first (the colleague joins its guide). Running it again rebuilds them in place.
    /// </summary>
    internal static class WorkersSetup
    {
        private const string ModelPath = "Assets/ThirdParty/Quaternius/AnimatedHuman/AnimatedHuman.fbx";
        private const string Folder = "Assets/_Project/Art/Workers";
        private const string ControllerPath = Folder + "/SimulatedWorker.controller";
        // Standing height to the top of the head. The cell is built a little under real scale (belts about 0.5 m
        // high), and the model's large head and shoulders read big beside it, so the workers are slightly short.
        private const float Height = 1.6f;
        private const string HeadTopBone = "HeadTop_End_end";
        private const string TraineeLayer = "Trainee";
        private const string ZoneLayer = "SafetyZone";
        private const string NavMeshPath = Folder + "/WalkArea.asset";
        // The walk area: the cell's floor, as the teleport floor covers it. Agents are the workers: 0.3 m radius.
        private static readonly Vector3 WalkCenter = new Vector3(-1.5f, 1f, 1f);
        private static readonly Vector3 WalkSize = new Vector3(13f, 3f, 12f);
        private const float AgentRadius = 0.3f;
        private const float BodyRadius = 0.25f;
        private static readonly string[] Clips = { "Idle", "Walk", "Working", "Run" };

        // The model's 32 px palette texture, rows from the top: skin 0–5, eyes 6–10, hair 11–16, shirt 17–22,
        // pants 23–31 (read from the pack's ClothedLightSkin.png).
        private static readonly (int firstRow, int part)[] Bands = { (0, 0), (6, 1), (11, 2), (17, 3), (23, 4) };
        private static readonly Color Eyes = new Color32(41, 41, 41, 255);

        [MenuItem("Tools/Factory Twin/Build Simulated Workers")]
        private static void Build()
        {
            GameObject training = GameObject.Find("Cell/Training");
            CellContext context = Object.FindAnyObjectByType<CellContext>();
            if (training == null || context == null || LayerMask.NameToLayer(TraineeLayer) < 0)
            {
                Debug.LogError("[WorkersSetup] Open Cell.unity first: Cell/Training, CellContext and the Trainee layer (Build Safety Zone) are needed.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
            {
                Debug.LogError($"[WorkersSetup] Copy the Quaternius Animated Human FBX to {ModelPath} first.");
                return;
            }

            ConfigureModel();
            EnsureFolder();
            AnimatorController controller = Controller();
            Material loader = Look("WorkerA", new Color32(255, 209, 159, 255), new Color32(124, 88, 35, 255), new Color32(255, 112, 16, 255), new Color32(32, 42, 64, 255));
            Material carrier = Look("WorkerB", new Color32(141, 85, 54, 255), new Color32(30, 26, 24, 255), new Color32(214, 232, 40, 255), new Color32(64, 66, 72, 255));
            Material technician = Look("WorkerC", new Color32(198, 134, 66, 255), new Color32(20, 18, 16, 255), new Color32(36, 104, 196, 255), new Color32(40, 44, 52, 255));
            Material maintenance = Look("WorkerD", new Color32(232, 180, 140, 255), new Color32(90, 60, 40, 255), new Color32(120, 120, 128, 255), new Color32(36, 52, 90, 255));

            Undo.SetCurrentGroupName("Build Simulated Workers");
            int undoGroup = Undo.GetCurrentGroup();
            Transform old = training.transform.Find("Workers");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var workers = new GameObject("Workers");
            Undo.RegisterCreatedObjectUndo(workers, "Build Simulated Workers");
            workers.transform.SetParent(training.transform, false);

            // Warning zone: x -4.2 to 0.2, z -1.0 to 3.4 (SafetyZoneSetup). The infeed starts at x 0.25, the outfeed
            // ends at x -4.85, both on the cell's long axis.
            // Each operator is an affected employee: notified of a lockout, they stand clear at a spot facing the cell.
            var infeed = new Vector3(0.95f, 0f, 0.35f);
            Transform infeedWorker = Worker(workers.transform, "Worker_Infeed", loader, controller, context,
                (new Vector3(2.4f, 0f, -0.5f), -120f, 0f, "Idle"),
                (infeed, -90f, 7f, "Idle"),
                (infeed, -90f, 9f, "Working"));
            Transform outfeedWorker = Worker(workers.transform, "Worker_Outfeed", carrier, controller, context,
                (new Vector3(-6.1f, 0f, -0.6f), 70f, 0f, "Idle"),
                (new Vector3(-5.4f, 0f, 1.2f), 90f, 10f, "Working"),
                (new Vector3(-5.4f, 0f, -1.7f), 200f, 6f, "Idle"));

            // The maintenance colleague waits north-east of the CNC, comes into its work area (east of the machine, by the
            // isolator and the inspection plate) after the tool change, and stands clear when asked.
            Transform colleague = Colleague(workers.transform, maintenance, controller, context,
                (new Vector3(3.1f, 0f, 5.0f), -110f, "Idle"),
                (new Vector3(1.45f, 0f, 4.3f), -90f, "Idle"),
                (new Vector3(3.0f, 0f, 3.0f), -100f, "Idle"));
            Area(workers.transform, context, new Bounds(new Vector3(1.5f, 1f, 3.9f), new Vector3(1.4f, 2f, 3.0f)),
                infeedWorker, outfeedWorker, colleague);

            // The zone walker comes in through the zone's open south side (protective zone z 0 to 2.4, x -3.2 to
            // -0.8), clear of the trainee's start at (-2, -2.6): home outside both zones, a spot in the warning zone
            // short of the protective one, a spot just inside the protective zone south of the infeed.
            if (!WalkArea(training.transform))
                return;
            ZoneWalkerWorker(workers.transform, technician, controller, context,
                (new Vector3(-1.0f, 0f, -1.8f), 0f),
                (new Vector3(-1.0f, 0f, -0.55f), 0f),
                (new Vector3(-1.5f, 0f, 0.2f), 0f));

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(training.scene);
            Debug.Log("[WorkersSetup] Four simulated workers built under Cell/Training/Workers: two operators who stand clear during a lockout, the maintenance colleague for the area check, and the zone walker for the suite.");
        }

        // Generic rig, our materials, the four clips named plainly and looping; the pack's two unnamed actions dropped.
        private static void ConfigureModel()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation clip in importer.defaultClipAnimations)
            {
                string name = clip.takeName.Substring(clip.takeName.LastIndexOf('|') + 1);
                if (name.StartsWith("ArmatureAction"))
                    continue;
                clip.name = name;
                clip.loopTime = Clips.Contains(name);
                clips.Add(clip);
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Workers");
        }

        private static AnimatorController Controller()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            Dictionary<string, AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (string name in new[] { "Working", "Walk", "Idle" })
            {
                AnimatorState state = machine.AddState(name);
                if (clips.TryGetValue(name, out AnimationClip clip))
                    state.motion = clip;
                else
                    Debug.LogWarning($"[WorkersSetup] No '{name}' clip in the model.");
                if (name == "Working")
                    machine.defaultState = state;
            }
            AssetDatabase.SaveAssets();
            return controller;
        }

        // A palette texture in the model's layout and a URP Lit material on it.
        private static Material Look(string name, Color skin, Color hair, Color shirt, Color pants)
        {
            Color[] parts = { skin, Eyes, hair, shirt, pants };
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int row = 0; row < 32; row++)
            {
                int part = Bands.Last(b => b.firstRow <= row).part;
                for (int x = 0; x < 32; x++)
                    texture.SetPixel(x, 31 - row, parts[part]);
            }
            string texturePath = $"{Folder}/{name}_Palette.png";
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.mipmapEnabled = false;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.SaveAndReimport();

            string materialPath = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        // An operator: the first entry is where they stand clear during a lockout, the rest their working stops.
        private static Transform Worker(Transform parent, string name, Material material, AnimatorController controller, CellContext context,
            (Vector3 position, float yaw, float workSeconds, string state) standClear,
            params (Vector3 position, float yaw, float workSeconds, string state)[] stops)
        {
            GameObject root = Root(parent, name, stops[0].position, stops[0].yaw);
            Animator animator = Model(root, material, controller);

            var worker = root.AddComponent<SimulatedWorker>();
            var so = new SerializedObject(worker);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("context").objectReferenceValue = context;
            SerializedProperty list = so.FindProperty("stops");
            list.arraySize = stops.Length;
            for (int i = 0; i < stops.Length; i++)
                SetStop(list.GetArrayElementAtIndex(i), stops[i]);
            SetStop(so.FindProperty("standClear"), standClear);
            so.ApplyModifiedProperties();
            return root.transform;
        }

        private static void SetStop(SerializedProperty entry, (Vector3 position, float yaw, float workSeconds, string state) stop)
        {
            entry.FindPropertyRelative("position").vector3Value = stop.position;
            entry.FindPropertyRelative("yawDegrees").floatValue = stop.yaw;
            entry.FindPropertyRelative("workSeconds").floatValue = stop.workSeconds;
            entry.FindPropertyRelative("state").stringValue = stop.state;
        }

        // The colleague is a training point too (Colleague): a click, or a controller ray and trigger, asks them to
        // stand clear. The guide's beacon points at them while they block the area check.
        private static Transform Colleague(Transform parent, Material material, AnimatorController controller, CellContext context,
            (Vector3 position, float yaw, string state) home, (Vector3 position, float yaw, string state) workArea,
            (Vector3 position, float yaw, string state) clear)
        {
            GameObject root = Root(parent, "Worker_Colleague", home.position, home.yaw);
            Animator animator = Model(root, material, controller);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.height = Height;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, Height / 2f, 0f);

            var point = root.AddComponent<TrainingPoint>();
            var pointSo = new SerializedObject(point);
            pointSo.FindProperty("context").objectReferenceValue = context;
            pointSo.FindProperty("kind").enumValueIndex = (int)TrainingPointKind.Colleague;
            pointSo.FindProperty("highlight").arraySize = 0;
            pointSo.ApplyModifiedProperties();

            var npc = root.AddComponent<NpcColleague>();
            var so = new SerializedObject(npc);
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("animator").objectReferenceValue = animator;
            foreach (var (field, spot) in new[] { ("home", home), ("workArea", workArea), ("clear", clear) })
            {
                SerializedProperty entry = so.FindProperty(field);
                entry.FindPropertyRelative("position").vector3Value = spot.position;
                entry.FindPropertyRelative("yawDegrees").floatValue = spot.yaw;
                entry.FindPropertyRelative("state").stringValue = spot.state;
            }
            so.ApplyModifiedProperties();

            TrainingGuide guide = Object.FindAnyObjectByType<TrainingGuide>();
            if (guide != null)
            {
                var guideSo = new SerializedObject(guide);
                guideSo.FindProperty("colleague").objectReferenceValue = point;
                guideSo.ApplyModifiedProperties();
            }
            else
                Debug.LogWarning("[WorkersSetup] No TrainingGuide; run Build Training Points first so the beacon can point at the colleague.");
            return root.transform;
        }

        private static void Area(Transform parent, CellContext context, Bounds bounds, params Transform[] people)
        {
            var go = new GameObject("WorkArea");
            go.transform.SetParent(parent, false);
            var area = go.AddComponent<WorkArea>();
            var so = new SerializedObject(area);
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("bounds").boundsValue = bounds;
            SerializedProperty list = so.FindProperty("people");
            list.arraySize = people.Length;
            for (int i = 0; i < people.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = people[i];
            so.ApplyModifiedProperties();
        }

        // The body the zone sensors see is the root: a kinematic capsule on the Trainee layer, like the trainee's.
        // The model under it stays on Default, so cameras draw it as before.
        private static void ZoneWalkerWorker(Transform parent, Material material, AnimatorController controller, CellContext context,
            (Vector3 position, float yaw) home, (Vector3 position, float yaw) warning, (Vector3 position, float yaw) protective)
        {
            home.position = Snap(home.position);
            warning.position = Snap(warning.position);
            protective.position = Snap(protective.position);
            Check(home.position, warning.position, protective.position);

            GameObject root = Root(parent, "Worker_ZoneWalker", home.position, home.yaw);
            root.layer = LayerMask.NameToLayer(TraineeLayer);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = Height;
            capsule.radius = BodyRadius;
            capsule.center = new Vector3(0f, Height / 2f, 0f);
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = AgentRadius;
            agent.height = Height;
            agent.speed = 1.2f;
            agent.angularSpeed = 360f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.05f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            Animator animator = Model(root, material, controller);

            var walker = root.AddComponent<ZoneWalker>();
            var so = new SerializedObject(walker);
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("agent").objectReferenceValue = agent;
            so.FindProperty("animator").objectReferenceValue = animator;
            foreach (var (field, spot) in new[] { ("home", home), ("warningZone", warning), ("protectiveZone", protective) })
            {
                SerializedProperty entry = so.FindProperty(field);
                entry.FindPropertyRelative("position").vector3Value = spot.position;
                entry.FindPropertyRelative("yawDegrees").floatValue = spot.yaw;
            }
            so.ApplyModifiedProperties();
        }

        // Bakes the walk area: the cell's floor from render meshes, the conveyors, CNC, station and props cut out of
        // it, the trainee, zone and UI layers left out. The NavMesh data is saved as an asset beside the workers.
        private static bool WalkArea(Transform training)
        {
            SetHumanoidAgent();
            Transform old = training.Find("WalkArea");
            GameObject go = old != null ? old.gameObject : new GameObject("WalkArea");
            if (old == null)
            {
                Undo.RegisterCreatedObjectUndo(go, "Build Simulated Workers");
                go.transform.SetParent(training, false);
            }
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            NavMeshSurface surface = go.GetComponent<NavMeshSurface>();
            if (surface == null)
                surface = go.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Volume;
            surface.center = WalkCenter;
            surface.size = WalkSize;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            int excluded = 0;
            foreach (string layer in new[] { TraineeLayer, ZoneLayer, "UI" })
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0)
                    excluded |= 1 << index;
            }
            surface.layerMask = ~excluded;
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                Debug.LogError("[WorkersSetup] The walk area baked nothing: no floor in the volume.");
                return false;
            }
            NavMeshData data = surface.navMeshData;
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(data, NavMeshPath);
            surface.navMeshData = data;
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();
            return true;
        }

        // The project's one agent type, Humanoid, sized for the workers.
        private static void SetHumanoidAgent()
        {
            var areas = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            SerializedProperty agent = areas.FindProperty("m_Settings").GetArrayElementAtIndex(0);
            agent.FindPropertyRelative("agentRadius").floatValue = AgentRadius;
            agent.FindPropertyRelative("agentHeight").floatValue = Height;
            agent.FindPropertyRelative("agentClimb").floatValue = 0.2f;
            agent.FindPropertyRelative("agentSlope").floatValue = 30f;
            areas.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector3 Snap(Vector3 spot)
        {
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                return hit.position;
            Debug.LogWarning($"[WorkersSetup] {spot} is not near the walk area.");
            return spot;
        }

        // Each spot must be where the scenario needs it, measured with the body's radius against the zone volumes,
        // and reachable from home.
        private static void Check(Vector3 home, Vector3 warning, Vector3 protective)
        {
            Bounds warningZone = ZoneBounds("Cell/Safety/WarningZone");
            Bounds protectiveZone = ZoneBounds("Cell/Safety/ProtectiveZone");
            bool Touches(Bounds zone, Vector3 at) => zone.SqrDistance(at + Vector3.up * 0.5f) <= BodyRadius * BodyRadius;
            string Words(bool ok) => ok ? "ok" : "WRONG";
            Debug.Log("[WorkersSetup] Zone walker spots: " +
                $"home {home} outside both zones {Words(!Touches(warningZone, home))}; " +
                $"warning {warning} in the warning zone only {Words(Touches(warningZone, warning) && !Touches(protectiveZone, warning))}; " +
                $"protective {protective} in the protective zone {Words(Touches(protectiveZone, protective))}; " +
                $"paths {PathWords(home, warning)}, {PathWords(warning, protective)}, {PathWords(protective, home)}.");
        }

        private static Bounds ZoneBounds(string path)
        {
            GameObject zone = GameObject.Find(path);
            BoxCollider box = zone != null ? zone.GetComponent<BoxCollider>() : null;
            return box != null ? box.bounds : new Bounds(Vector3.one * 1000f, Vector3.zero);
        }

        private static string PathWords(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                return "NO PATH";
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return $"{length:0.00} m";
        }

        private static GameObject Root(Transform parent, string name, Vector3 position, float yaw)
        {
            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Build Simulated Workers");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return root;
        }

        // The model under a worker's root, in the given look, scaled to Height, with the worker animator.
        private static Animator Model(GameObject root, Material material, AnimatorController controller)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
                r.sharedMaterial = material;
            Transform headTop = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == HeadTopBone);
            float height = (headTop != null ? headTop.position.y : renderers.Select(r => r.bounds.max.y).DefaultIfEmpty(Height).Max())
                - root.transform.position.y;
            if (height > 0.01f)
                model.transform.localScale *= Height / height;

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            return animator;
        }
    }
}
