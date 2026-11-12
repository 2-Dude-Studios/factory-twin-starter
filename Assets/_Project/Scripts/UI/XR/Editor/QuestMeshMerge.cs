using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone Quest (M26): while CellQuest.unity is built into the APK, merges the hall's thousands of small static
    /// pieces into a few meshes per material, lightmap and 10 m square, so the headset draws tens of objects where it
    /// drew thousands (the probe showed object count, not resolution, costs the frame). Done only in the build, so no
    /// large mesh files are kept in the project; the lightmap UVs are carried over. Each merged mesh stays under the
    /// hall part it came from, so the -probe run can still hide walls, ceiling or shelving.
    /// </summary>
    internal sealed class QuestMeshMerge : IProcessSceneWithReport
    {
        private const float CellMetres = 10f;
        private const int MaxVertices = 400000;

        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || scene.name != TrainingLaunch.QuestCellScene)
                return;
            GameObject env = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "env")
                    env = root;
            }
            if (env != null)
                Merge(env);
        }

        private readonly struct Key
        {
            public Key(Transform part, Material material, int lightmap, int cellX, int cellZ)
            {
                Part = part;
                Material = material;
                Lightmap = lightmap;
                CellX = cellX;
                CellZ = cellZ;
            }

            public readonly Transform Part;
            public readonly Material Material;
            public readonly int Lightmap;
            public readonly int CellX;
            public readonly int CellZ;
        }

        private sealed class KeyComparer : IEqualityComparer<Key>
        {
            public bool Equals(Key a, Key b) =>
                a.Part == b.Part && a.Material == b.Material && a.Lightmap == b.Lightmap && a.CellX == b.CellX && a.CellZ == b.CellZ;

            public int GetHashCode(Key k) =>
                ((((k.Part != null ? k.Part.GetInstanceID() : 0) * 397 ^ (k.Material != null ? k.Material.GetInstanceID() : 0)) * 397
                  ^ k.Lightmap) * 397 ^ k.CellX) * 397 ^ k.CellZ;
        }

        private static void Merge(GameObject env)
        {
            var groups = new Dictionary<Key, List<CombineInstance>>(new KeyComparer());
            var merged = new List<MeshRenderer>();
            foreach (Transform part in env.transform)
            {
                foreach (MeshRenderer r in part.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (!r.enabled || r.GetComponentInParent<Animator>() != null || !r.TryGetComponent(out MeshFilter filter))
                        continue;
                    Mesh mesh = filter.sharedMesh;
                    Material[] materials = r.sharedMaterials;
                    if (mesh == null || materials.Length == 0)
                        continue;
                    Vector3 c = r.bounds.center;
                    int cx = Mathf.FloorToInt(c.x / CellMetres), cz = Mathf.FloorToInt(c.z / CellMetres);
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        Material m = materials[Mathf.Min(s, materials.Length - 1)];
                        var key = new Key(part, m, r.lightmapIndex, cx, cz);
                        if (!groups.TryGetValue(key, out List<CombineInstance> list))
                            groups[key] = list = new List<CombineInstance>();
                        list.Add(new CombineInstance
                        {
                            mesh = mesh,
                            subMeshIndex = s,
                            transform = r.transform.localToWorldMatrix,
                            lightmapScaleOffset = r.lightmapScaleOffset,
                        });
                    }
                    merged.Add(r);
                }
            }

            int made = 0;
            foreach (KeyValuePair<Key, List<CombineInstance>> g in groups)
            {
                List<CombineInstance> all = g.Value;
                int start = 0;
                while (start < all.Count)
                {
                    // Split so no merged mesh grows past MaxVertices.
                    int end = start, vertices = 0;
                    while (end < all.Count && (end == start || vertices + all[end].mesh.vertexCount <= MaxVertices))
                        vertices += all[end++].mesh.vertexCount;
                    var mesh = new Mesh { name = "QuestMerged", indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(all.GetRange(start, end - start).ToArray(), true, true, g.Key.Lightmap >= 0);
                    mesh.RecalculateBounds();
                    // Vertices are in world space: the new object sits at the world origin, unrotated and unscaled,
                    // and keeps that when parented under its hall part (so the probe still finds it there).
                    var go = new GameObject($"Merged {(g.Key.Material != null ? g.Key.Material.name : "none")} {g.Key.CellX},{g.Key.CellZ}");
                    SceneManager.MoveGameObjectToScene(go, env.scene);
                    go.transform.SetParent(g.Key.Part, true);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = g.Key.Material;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.lightmapIndex = g.Key.Lightmap;
                    mr.lightmapScaleOffset = new Vector4(1f, 1f, 0f, 0f);
                    made++;
                    start = end;
                }
            }

            foreach (MeshRenderer r in merged)
                Object.DestroyImmediate(r);
            Debug.Log($"[QuestMeshMerge] {merged.Count} hall renderers merged into {made} meshes.");
        }
    }
}
