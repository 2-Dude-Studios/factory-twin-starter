using System.Collections.Generic;
using System.Linq;
using System.Text;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone Quest (M26): derives CellQuest.unity, the headset's copy of the cell, from Cell.unity. Re-runnable:
    /// the variant is rebuilt from the saved Cell.unity every time, so the drills stay in one place and the variant is
    /// never edited by hand. It trims the hall to what the training areas can see, turns off realtime lights the
    /// headset's pipeline cannot use, gives the headset its own render pipeline asset (Quest_RPAsset, a copy of the
    /// Link one to tune separately), and adds the headset's foveation and the -probe measuring run.
    /// </summary>
    internal static class QuestVariantSetup
    {
        public const string SourceScene = "Assets/_Project/Scenes/Cell.unity";
        public const string QuestScene = "Assets/_Project/Scenes/CellQuest.unity";
        public const string ReportPath = "Builds/Quest/scene-cost-quest.txt";

        private const string VrPipelinePath = "Assets/_Project/Settings/VR_RPAsset.asset";
        private const string VrRendererPath = "Assets/_Project/Settings/VR_Renderer.asset";
        public const string QuestPipelinePath = "Assets/_Project/Settings/Quest_RPAsset.asset";
        private const string QuestRendererPath = "Assets/_Project/Settings/Quest_Renderer.asset";
        private const string AreasPath = "Assets/_Project/Data/Training/TrainingAreas.asset";

        /// <summary>How far past the training areas the hall is kept, in metres: what the trainee sees from inside them.</summary>
        private const float KeepMarginMetres = 8f;

        /// <summary>
        /// Storage racks, boxes, pallets and the trailer are left out of the headset's hall altogether: no drill uses
        /// them, and on the Quest (2026-11-12) they were the biggest single cost (thousands of boxes, and two rack frames
        /// of 190 k triangles each that no trim or merge can split).
        /// </summary>
        private static readonly string[] Shelving = { "shelf", "pallet", "trailer", "truck" };

        /// <summary>
        /// The hall's materials on the headset: copies on URP's Baked Lit shader (one colour texture and the baked
        /// lightmap; no realtime light, specular or reflections), generated here and kept out of git. The hall's own
        /// shader graph samples eight textures and computes noise, hue and contrast for every pixel.
        /// </summary>
        public const string QuestMaterialsFolder = "Assets/_Project/Generated/QuestMaterials";
        private const string BakedLit = "Universal Render Pipeline/Baked Lit";

        /// <summary>Textures on Android drop this many mip levels: 1 is half the width and height.</summary>
        private const int AndroidTextureMipLimit = 1;

        /// <summary>The headset camera's reach, with fog fading the far hall out before it is cut off.</summary>
        private const float FarClipMetres = 30f;
        private const float FogStartMetres = 18f;
        private static readonly Color HallFog = new Color(0.72f, 0.74f, 0.76f);

        /// <summary>The hall's shell: never trimmed by distance, so no area looks out through a hole.</summary>
        private static readonly string[] Shell = { "walls", "ceiling", "floor", "wallTopSpacer" };

        /// <summary>
        /// The shell's detail, left out on the headset (cost report's shell list, 2026-11-13: walls and ceiling still
        /// cost about 6 and 5 ms of GPU each). The ceiling keeps its roof panels and lights and loses the trusses,
        /// rings, hangers, ducts, pipes and railings; the walls keep their panels, doors and barriers and lose the pipe
        /// holders, the charging station, clips, wiring and pipes, and every piece smaller than <see cref="ShellDetailMetres"/>.
        /// </summary>
        private static readonly string[] ShellParts = { "walls", "ceiling" };
        private static readonly string[] ShellDetail =
        {
            "rings_", "scaffolding", "hangers_A", "hangers_B", "ventilation", "pipes_", "railing_A", "railing_B",
            "pipe_holder", "wall_chargers", "wiring_case", "water_pipes", "gate_rollers", "window_locks", "small_wire_box",
            "clip", "greyDarkMetal", "greyMetal",
        };
        private const float ShellDetailMetres = 1f;

        /// <summary>
        /// Large, light pieces always stay, whatever their name: a wall section is named after what hangs on it
        /// (wall_chargers_01 is 9 m of wall in 424 triangles), and leaving it out opens a hole into the fog.
        /// </summary>
        private const float PanelMetres = 5f;
        private const long PanelTriangles = 1000;

        [MenuItem("Tools/Factory Twin/Quest/Build Quest Variant")]
        public static void Build()
        {
            var log = new StringBuilder("[QuestVariant] ");
            RenderPipelineAsset questPipeline = QuestPipeline(log);
            SetAndroidQuality(questPipeline, log);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(QuestScene) != null)
                AssetDatabase.DeleteAsset(QuestScene);
            if (!AssetDatabase.CopyAsset(SourceScene, QuestScene))
            {
                Debug.LogError("[QuestVariant] Could not copy " + SourceScene);
                return;
            }

            // Additive, so whatever is open in the Editor (and unsaved) is left alone.
            Scene scene = EditorSceneManager.OpenScene(QuestScene, OpenSceneMode.Additive);
            try
            {
                GameObject env = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "env");
                if (env != null)
                {
                    Trim(env, AreasBox(), log);
                    StripShellDetail(env, log);
                }
                Lights(scene, log);
                if (env != null)
                    LightMaterials(env, log);
                Distance(scene, log);
                Wire(scene, env, questPipeline, log);
                EditorSceneManager.SaveScene(scene);
                QuestSceneCost.Report(scene, ReportPath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            log.Append("Saved ").Append(QuestScene).Append('.');
            Debug.Log(log.ToString());
        }

        // unity -batchmode -projectPath . -executeMethod TwoDude.FactoryTwin.UI.QuestVariantSetup.BuildFromCommandLine -quit
        public static void BuildFromCommandLine() => Build();

        private static RenderPipelineAsset QuestPipeline(StringBuilder log)
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(QuestRendererPath) == null)
                AssetDatabase.CopyAsset(VrRendererPath, QuestRendererPath);
            bool created = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(QuestPipelinePath) == null;
            if (created)
                AssetDatabase.CopyAsset(VrPipelinePath, QuestPipelinePath);
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(QuestPipelinePath);
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(QuestRendererPath);
            var so = new SerializedObject(pipeline);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            if (list != null && list.arraySize > 0)
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            log.Append(created ? "Created " : "Kept ").Append(QuestPipelinePath).Append(" (renderer ").Append(QuestRendererPath).Append("). ");
            return pipeline;
        }

        /// <summary>The quality level Android starts on (named "Mobile") uses the Quest pipeline, so the menu does too.</summary>
        private static void SetAndroidQuality(RenderPipelineAsset pipeline, StringBuilder log)
        {
            Object settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            if (settings == null)
                return;
            var so = new SerializedObject(settings);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");
            for (int i = 0; levels != null && i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                if (level.FindPropertyRelative("name").stringValue != "Mobile")
                    continue;
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
                SerializedProperty mips = level.FindPropertyRelative("globalTextureMipmapLimit");
                if (mips != null)
                    mips.intValue = AndroidTextureMipLimit;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.Append($"Quality level Mobile (Android) uses the Quest pipeline, textures {AndroidTextureMipLimit} mip down (half size). ");
                return;
            }
        }

        private static Rect AreasBox()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TrainingAreas>(AreasPath);
            IReadOnlyList<TrainingAreaSettings> areas = asset != null && asset.Areas.Count > 0 ? asset.Areas : TrainingAreaDefaults.All();
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (TrainingAreaSettings a in areas)
            {
                minX = Mathf.Min(minX, (float)a.MinX);
                minZ = Mathf.Min(minZ, (float)a.MinZ);
                maxX = Mathf.Max(maxX, (float)a.MaxX);
                maxZ = Mathf.Max(maxZ, (float)a.MaxZ);
            }
            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        /// <summary>Removes the hall's props that lie wholly outside the box; the shell stays.</summary>
        private static Rect Grow(Rect r, float m) => Rect.MinMaxRect(r.xMin - m, r.yMin - m, r.xMax + m, r.yMax + m);

        private static bool IsShelving(string name)
        {
            foreach (string word in Shelving)
            {
                if (name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static void Trim(GameObject env, Rect areas, StringBuilder log)
        {
            foreach (Transform t in env.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                    PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            int removed = 0, kept = 0;
            long trianglesRemoved = 0;
            foreach (Transform part in env.transform.Cast<Transform>().ToList())
            {
                if (Shell.Contains(part.name))
                    continue;
                if (IsShelving(part.name))
                {
                    foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true))
                    {
                        trianglesRemoved += Triangles(r);
                        removed++;
                    }
                    Object.DestroyImmediate(part.gameObject);
                    continue;
                }
                Rect keep = Grow(areas, KeepMarginMetres);
                foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true))
                {
                    Bounds b = r.bounds;
                    bool outside = b.max.x < keep.xMin || b.min.x > keep.xMax || b.max.z < keep.yMin || b.min.z > keep.yMax;
                    if (!outside)
                    {
                        kept++;
                        continue;
                    }
                    trianglesRemoved += Triangles(r);
                    removed++;
                    GameObject go = r.gameObject;
                    if (go.transform.childCount == 0)
                    {
                        Object.DestroyImmediate(go);
                    }
                    else
                    {
                        if (go.TryGetComponent(out MeshFilter filter))
                            Object.DestroyImmediate(filter);
                        Object.DestroyImmediate(r);
                    }
                }
                if (part != null && part.GetComponentsInChildren<Renderer>(true).Length == 0 && part.GetComponentsInChildren<Light>(true).Length == 0)
                    Object.DestroyImmediate(part.gameObject);
            }
            log.Append($"Hall trimmed to the areas + {KeepMarginMetres:F0} m, racks, boxes, pallets and trailer left out: " +
                       $"{removed} props removed ({trianglesRemoved / 1000} k triangles), {kept} kept. ");
        }

        private static void StripShellDetail(GameObject env, StringBuilder log)
        {
            int removed = 0, kept = 0;
            long trianglesRemoved = 0, trianglesKept = 0;
            foreach (string partName in ShellParts)
            {
                Transform part = env.transform.Find(partName);
                if (part == null)
                    continue;
                foreach (MeshRenderer r in part.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                        continue;
                    Vector3 size = r.bounds.size;
                    float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                    bool panel = largest >= PanelMetres && Triangles(r) <= PanelTriangles;
                    bool small = largest < ShellDetailMetres;
                    if (panel || (!small && !IsShellDetail(filter.sharedMesh.name)))
                    {
                        kept++;
                        trianglesKept += Triangles(r);
                        continue;
                    }
                    removed++;
                    trianglesRemoved += Triangles(r);
                    GameObject go = r.gameObject;
                    if (go.transform.childCount == 0 && go.GetComponents<Component>().Length <= 3)
                    {
                        Object.DestroyImmediate(go);
                    }
                    else
                    {
                        Object.DestroyImmediate(r);
                        Object.DestroyImmediate(filter);
                    }
                }
            }
            log.Append($"Shell detail left out: {removed} pieces ({trianglesRemoved / 1000} k triangles); " +
                       $"{kept} kept ({trianglesKept / 1000} k): panels, doors, roof and lights. ");
        }

        private static bool IsShellDetail(string mesh)
        {
            foreach (string word in ShellDetail)
            {
                if (mesh.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>Gives the hall's renderers Baked Lit copies of their materials (decals and glass keep their own).</summary>
        private static void LightMaterials(GameObject env, StringBuilder log)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Generated"))
                AssetDatabase.CreateFolder("Assets/_Project", "Generated");
            if (AssetDatabase.IsValidFolder(QuestMaterialsFolder))
                AssetDatabase.DeleteAsset(QuestMaterialsFolder);
            AssetDatabase.CreateFolder("Assets/_Project/Generated", "QuestMaterials");
            Shader bakedLit = Shader.Find(BakedLit);
            if (bakedLit == null)
            {
                log.Append("Baked Lit shader not found; hall materials left as they are. ");
                return;
            }

            var copies = new Dictionary<Material, Material>();
            int slots = 0;
            foreach (Renderer r in env.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material m = materials[i];
                    if (m == null || !Lightable(m))
                        continue;
                    if (!copies.TryGetValue(m, out Material copy))
                    {
                        copy = BakedLitCopy(m, bakedLit);
                        AssetDatabase.CreateAsset(copy, $"{QuestMaterialsFolder}/{copies.Count:D4} {Safe(m.name)}.mat");
                        copies[m] = copy;
                    }
                    materials[i] = copy;
                    changed = true;
                    slots++;
                }
                if (changed)
                    r.sharedMaterials = materials;
            }
            log.Append($"Hall on Baked Lit: {copies.Count} materials copied, {slots} slots. ");
        }

        /// <summary>Opaque hall materials: the hall's basic shader graph and URP Lit. Decals, glass and the rest keep theirs.</summary>
        private static bool Lightable(Material m)
        {
            string shader = m.shader.name;
            if (shader.IndexOf("decal", System.StringComparison.OrdinalIgnoreCase) >= 0 || shader.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (m.renderQueue >= (int)RenderQueue.AlphaTest)
                return false;
            return shader == "Shader Graphs/shdr_basic_01" || shader == "Universal Render Pipeline/Lit" || shader == "Universal Render Pipeline/Simple Lit";
        }

        private static Material BakedLitCopy(Material source, Shader bakedLit)
        {
            var copy = new Material(bakedLit) { name = source.name + " (Quest)", enableInstancing = true };
            Texture baseMap = null;
            Vector2 tiling = Vector2.one, offset = Vector2.zero;
            Color colour = Color.white;
            if (source.HasProperty("_baseMap"))
            {
                // The hall's shader graph: colour map with its own tiling and offset, tinted by _Color_Map.
                baseMap = source.GetTexture("_baseMap");
                if (source.HasProperty("_BASE_Tiling"))
                    tiling = source.GetVector("_BASE_Tiling");
                if (source.HasProperty("_BASE_Offset"))
                    offset = source.GetVector("_BASE_Offset");
                if (source.HasProperty("_Color_Map"))
                    colour = source.GetColor("_Color_Map");
            }
            else if (source.HasProperty("_BaseMap"))
            {
                baseMap = source.GetTexture("_BaseMap");
                tiling = source.GetTextureScale("_BaseMap");
                offset = source.GetTextureOffset("_BaseMap");
                if (source.HasProperty("_BaseColor"))
                    colour = source.GetColor("_BaseColor");
            }
            if (tiling == Vector2.zero)
                tiling = Vector2.one;
            copy.SetTexture("_BaseMap", baseMap);
            copy.SetTextureScale("_BaseMap", tiling);
            copy.SetTextureOffset("_BaseMap", offset);
            copy.SetColor("_BaseColor", colour);
            return copy;
        }

        private static string Safe(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 60 ? name.Substring(0, 60) : name;
        }

        /// <summary>The headset camera draws to FarClipMetres; linear fog in the hall's grey hides the edge.</summary>
        private static void Distance(Scene scene, StringBuilder log)
        {
            Scene before = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = HallFog;
            RenderSettings.fogStartDistance = FogStartMetres;
            RenderSettings.fogEndDistance = FarClipMetres;
            SceneManager.SetActiveScene(before);

            int cameras = 0;
            foreach (XROrigin origin in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)))
            {
                Camera eye = origin.Camera;
                if (eye == null)
                    continue;
                eye.farClipPlane = FarClipMetres;
                eye.clearFlags = CameraClearFlags.SolidColor;
                eye.backgroundColor = HallFog;
                cameras++;
            }
            log.Append($"Headset camera draws to {FarClipMetres:F0} m with fog from {FogStartMetres:F0} m ({cameras} camera). ");
        }

        private static long Triangles(Renderer r)
        {
            Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.TryGetComponent(out MeshFilter f) ? f.sharedMesh : null;
            long n = 0;
            for (int i = 0; mesh != null && i < mesh.subMeshCount; i++)
                n += mesh.GetIndexCount(i) / 3;
            return n;
        }

        /// <summary>The Quest pipeline draws no additional lights and no shadows: realtime point lights are switched off.</summary>
        private static void Lights(Scene scene, StringBuilder log)
        {
            int off = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light l in root.GetComponentsInChildren<Light>(true))
                {
                    if (l.type == LightType.Directional)
                    {
                        l.shadows = LightShadows.None;
                        continue;
                    }
                    if (l.lightmapBakeType == LightmapBakeType.Realtime && l.enabled)
                    {
                        l.enabled = false;
                        off++;
                    }
                }
            }
            log.Append($"{off} realtime lights off, sun without shadows. ");
        }

        private static void Wire(Scene scene, GameObject env, RenderPipelineAsset pipeline, StringBuilder log)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (VrSwitch vr in roots.SelectMany(g => g.GetComponentsInChildren<VrSwitch>(true)))
            {
                var so = new SerializedObject(vr);
                so.FindProperty("vrPipeline").objectReferenceValue = pipeline;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject cell = roots.FirstOrDefault(g => g.name == "Cell");
            var quest = new GameObject("Quest");
            SceneManager.MoveGameObjectToScene(quest, scene);
            if (cell != null)
                quest.transform.SetParent(cell.transform, false);
            quest.AddComponent<QuestDisplay>();
            QuestCostProbe probe = quest.AddComponent<QuestCostProbe>();
            var p = new SerializedObject(probe);
            p.FindProperty("env").objectReferenceValue = env != null ? env.transform : null;
            p.FindProperty("sun").objectReferenceValue = roots.SelectMany(g => g.GetComponentsInChildren<Light>(true)).FirstOrDefault(l => l.type == LightType.Directional);
            p.FindProperty("origin").objectReferenceValue = roots.SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).FirstOrDefault();
            TrainingAreaStart[] starts = roots.SelectMany(g => g.GetComponentsInChildren<TrainingAreaStart>(true)).ToArray();
            SerializedProperty list = p.FindProperty("starts");
            list.arraySize = starts.Length;
            for (int i = 0; i < starts.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = starts[i];
            p.ApplyModifiedPropertiesWithoutUndo();
            log.Append($"VR uses Quest_RPAsset; foveation and the -probe run added ({starts.Length} area starts). ");
        }
    }
}
