using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone Quest (M26): what the open scene asks the headset to draw, by part: active renderers, triangles,
    /// material slots, shaders, lights, particle systems, reflection probes and lightmaps. Logged and written to
    /// Builds/Quest/scene-cost.txt so the Quest variant's cuts can be chosen and checked against Meta's guide.
    /// </summary>
    internal static class QuestSceneCost
    {
        public const string ReportPath = "Builds/Quest/scene-cost.txt";

        private sealed class Tally
        {
            public int Renderers;
            public long Triangles;
            public int Slots;
            public int Skinned;
            public int Particles;
            public int Lights;
            public int ShadowCasters;
        }

        [MenuItem("Tools/Factory Twin/Quest/Report Scene Cost")]
        private static void ReportMenu() => Report(SceneManager.GetActiveScene());

        public static string Report(Scene scene, string path = ReportPath)
        {
            var text = new StringBuilder();
            text.Append("[QuestSceneCost] ").Append(scene.path).Append('\n');

            var total = new Tally();
            var parts = new List<(string Name, Tally Tally)>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool split = root.name == "env" || root.name == "Cell";
                if (!split)
                {
                    parts.Add((root.name, Count(root)));
                    continue;
                }
                foreach (Transform child in root.transform)
                    parts.Add((root.name + "/" + child.name, Count(child.gameObject)));
            }
            foreach ((string _, Tally t) in parts)
                Add(total, t);

            text.Append($"TOTAL active: {total.Renderers} renderers, {total.Triangles / 1000} k triangles, {total.Slots} material slots, " +
                        $"{total.ShadowCasters} shadow casters, {total.Skinned} skinned, {total.Particles} particle systems, {total.Lights} lights\n");
            text.Append("By part (largest first, triangles in k):\n");
            foreach ((string name, Tally t) in parts.OrderByDescending(p => p.Tally.Triangles))
            {
                if (t.Renderers == 0 && t.Lights == 0 && t.Particles == 0)
                    continue;
                text.Append($"  {name}: {t.Triangles / 1000} k tris, {t.Renderers} renderers, {t.Slots} slots" +
                            $"{(t.Skinned > 0 ? $", {t.Skinned} skinned" : "")}{(t.Particles > 0 ? $", {t.Particles} particles" : "")}" +
                            $"{(t.Lights > 0 ? $", {t.Lights} lights" : "")}\n");
            }

            // Shaders by how many material slots use them: the expensive ones are what a Quest material swap targets.
            var shaders = new Dictionary<string, int>();
            foreach (Renderer r in Active<Renderer>(scene))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    string key = m == null ? "(none)" : m.shader.name;
                    shaders[key] = shaders.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            }
            text.Append("Shaders by material slots:\n");
            foreach (KeyValuePair<string, int> s in shaders.OrderByDescending(s => s.Value))
                text.Append($"  {s.Value}  {s.Key}\n");

            var lights = Active<Light>(scene).ToList();
            text.Append($"Lights: {lights.Count} active; ");
            foreach (IGrouping<string, Light> g in lights.GroupBy(l => $"{l.type} {l.lightmapBakeType}{(l.shadows != LightShadows.None ? " shadowed" : "")}"))
                text.Append(g.Count()).Append(' ').Append(g.Key).Append("; ");
            text.Append('\n');

            int probes = Active<ReflectionProbe>(scene).Count();
            text.Append($"Reflection probes: {probes} active\n");

            LightmapData[] maps = LightmapSettings.lightmaps;
            long texels = 0;
            foreach (LightmapData map in maps)
            {
                if (map.lightmapColor != null)
                    texels += (long)map.lightmapColor.width * map.lightmapColor.height;
            }
            text.Append($"Lightmaps (active scene): {maps.Length}, {texels / 1000000.0:F1} M texels (colour)\n");

            int lods = Active<LODGroup>(scene).Count();
            text.Append($"LOD groups: {lods}\n");

            string report = text.ToString();
            Debug.Log(report);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? ".");
            System.IO.File.WriteAllText(path, report);
            return report;
        }

        private static Tally Count(GameObject root)
        {
            var t = new Tally();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled)
                    continue;
                t.Renderers++;
                t.Slots += r.sharedMaterials.Length;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                    t.ShadowCasters++;
                Mesh mesh = null;
                if (r is MeshRenderer && r.TryGetComponent(out MeshFilter filter))
                    mesh = filter.sharedMesh;
                else if (r is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                    t.Skinned++;
                }
                if (mesh != null)
                {
                    for (int i = 0; i < mesh.subMeshCount; i++)
                        t.Triangles += (long)mesh.GetIndexCount(i) / 3;
                }
            }
            foreach (ParticleSystem p in root.GetComponentsInChildren<ParticleSystem>(false))
                t.Particles++;
            foreach (Light l in root.GetComponentsInChildren<Light>(false))
            {
                if (l.enabled)
                    t.Lights++;
            }
            return t;
        }

        private static void Add(Tally total, Tally t)
        {
            total.Renderers += t.Renderers;
            total.Triangles += t.Triangles;
            total.Slots += t.Slots;
            total.Skinned += t.Skinned;
            total.Particles += t.Particles;
            total.Lights += t.Lights;
            total.ShadowCasters += t.ShadowCasters;
        }

        private static IEnumerable<T> Active<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T c in root.GetComponentsInChildren<T>(false))
                {
                    if (c is Behaviour b && !b.enabled)
                        continue;
                    if (c is Renderer r && !r.enabled)
                        continue;
                    yield return c;
                }
            }
        }
    }
}
