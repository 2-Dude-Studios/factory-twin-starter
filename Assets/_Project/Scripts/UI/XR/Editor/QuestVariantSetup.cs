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
        /// Shelving, pallets and boxes are kept only this close to an area: they are thousands of small pieces, and the
        /// probe on the Quest (2026-11-12) showed them costing about 14 ms of a 13.9 ms frame.
        /// </summary>
        private const float ShelvingMarginMetres = 2f;

        private static readonly string[] Shelving = { "shelf", "pallet", "trailer", "truck" };

        /// <summary>The hall's shell: kept whole, so no area looks out through a hole. What it costs is measured first.</summary>
        private static readonly string[] Shell = { "walls", "ceiling", "floor", "wallTopSpacer" };

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
                    Trim(env, AreasBox(), log);
                Lights(scene, log);
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
                so.ApplyModifiedPropertiesWithoutUndo();
                log.Append("Quality level Mobile (Android) uses the Quest pipeline. ");
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
                Rect keep = Grow(areas, IsShelving(part.name) ? ShelvingMarginMetres : KeepMarginMetres);
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
            log.Append($"Hall trimmed to the areas + {KeepMarginMetres:F0} m (shelving and pallets + {ShelvingMarginMetres:F0} m): " +
                       $"{removed} props removed ({trianglesRemoved / 1000} k triangles), {kept} kept; shell kept whole. ");
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
