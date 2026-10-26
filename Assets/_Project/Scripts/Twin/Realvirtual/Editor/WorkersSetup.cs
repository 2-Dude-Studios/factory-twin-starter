using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training (M12): places two simulated co-workers beside the cell from the Quaternius "Animated Human" model
    /// (CC0, Assets/ThirdParty/Quaternius). One tends the infeed, east of the warning zone, standing and working by
    /// turns; the other works the outfeed end, west of it, and walks to the aisle and back. Both stay outside the robot zone. Their clothes are
    /// our own palette textures (hi-vis shirts), so nothing in ThirdParty is edited beyond its import settings.
    /// Running it again rebuilds them in place.
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
        private static readonly string[] Clips = { "Idle", "Walk", "Working", "Run" };

        // The model's 32 px palette texture, rows from the top: skin 0–5, eyes 6–10, hair 11–16, shirt 17–22,
        // pants 23–31 (read from the pack's ClothedLightSkin.png).
        private static readonly (int firstRow, int part)[] Bands = { (0, 0), (6, 1), (11, 2), (17, 3), (23, 4) };
        private static readonly Color Eyes = new Color32(41, 41, 41, 255);

        [MenuItem("Tools/Factory Twin/Build Simulated Workers")]
        private static void Build()
        {
            GameObject training = GameObject.Find("Cell/Training");
            if (training == null)
            {
                Debug.LogError("[WorkersSetup] Open Cell.unity first: Cell/Training is needed.");
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
            var infeed = new Vector3(0.95f, 0f, 0.35f);
            Worker(workers.transform, "Worker_Infeed", loader, controller,
                (infeed, -90f, 7f, "Idle"),
                (infeed, -90f, 9f, "Working"));
            Worker(workers.transform, "Worker_Outfeed", carrier, controller,
                (new Vector3(-5.4f, 0f, 1.2f), 90f, 10f, "Working"),
                (new Vector3(-5.4f, 0f, -1.7f), 200f, 6f, "Idle"));

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(training.scene);
            Debug.Log("[WorkersSetup] Two simulated workers built under Cell/Training/Workers, outside the robot zone.");
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

        private static void Worker(Transform parent, string name, Material material, AnimatorController controller,
            params (Vector3 position, float yaw, float workSeconds, string state)[] stops)
        {
            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Build Simulated Workers");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(stops[0].position, Quaternion.Euler(0f, stops[0].yaw, 0f));

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

            var worker = root.AddComponent<SimulatedWorker>();
            var so = new SerializedObject(worker);
            so.FindProperty("animator").objectReferenceValue = animator;
            SerializedProperty list = so.FindProperty("stops");
            list.arraySize = stops.Length;
            for (int i = 0; i < stops.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("position").vector3Value = stops[i].position;
                entry.FindPropertyRelative("yawDegrees").floatValue = stops[i].yaw;
                entry.FindPropertyRelative("workSeconds").floatValue = stops[i].workSeconds;
                entry.FindPropertyRelative("state").stringValue = stops[i].state;
            }
            so.ApplyModifiedProperties();
        }
    }
}
