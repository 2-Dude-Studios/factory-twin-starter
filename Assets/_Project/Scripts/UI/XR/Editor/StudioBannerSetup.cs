using System.Collections.Generic;
using System.Linq;
using System.Text;
using TwoDude.FactoryTwin.Twin;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub (M28): 2 Dude Studios banners round the hall, on the open scene (Cell.unity; the Quest variant
    /// inherits them). A two-sided banner hangs over each training area facing its start, one over the hub menu, and
    /// four over the walkways round the areas. With the logo at <see cref="StandaloneSetup.StudioLogoPath"/> the banners
    /// carry it; until then they carry the name in the studio's colours. Re-runnable: run it again once the logo is in.
    /// Our own brand (CLAUDE.md's rule on vendor branding is about other companies' marks).
    /// </summary>
    internal static class StudioBannerSetup
    {
        private const string RootName = "StudioBanners";
        private const string AreasPath = "Assets/_Project/Data/Training/TrainingAreas.asset";
        private const string MaterialFolder = "Assets/_Project/Materials/Brand";
        private static readonly Color Ink = new Color(0.03f, 0.03f, 0.035f);
        private static readonly Color Gold = new Color(0.98f, 0.76f, 0.1f);

        // Banner sizes in metres: over an area, and the larger walkway ones; hanging this high (their middle).
        private static readonly Vector2 AreaSize = new Vector2(2.6f, 1.3f);
        private static readonly Vector2 WalkwaySize = new Vector2(3.6f, 1.8f);
        private const float AreaHeight = 3.7f, WalkwayHeight = 4.4f, HubHeight = 3.3f, CeilingRods = 3.5f;

        [MenuItem("Tools/Factory Twin/Build Studio Banners")]
        private static void BuildMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            var log = new StringBuilder("[StudioBannerSetup] ");
            Build(scene, log);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.Append("Save the scene to keep them.").ToString());
        }

        public static void Build(Scene scene, StringBuilder log)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            CellContext context = roots.SelectMany(g => g.GetComponentsInChildren<CellContext>(true)).FirstOrDefault();
            if (context == null)
            {
                log.Append("No cell in the open scene. ");
                return;
            }
            Transform old = context.transform.Find(RootName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RootName).transform;
            root.SetParent(context.transform, false);

            var layout = AssetDatabase.LoadAssetAtPath<TrainingAreas>(AreasPath);
            IReadOnlyList<TrainingAreaSettings> areas = layout != null && layout.Areas.Count > 0 ? layout.Areas : TrainingAreaDefaults.All();
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(StandaloneSetup.StudioLogoPath);
            Material backing = Unlit("Banner_Backing", Ink, null);
            Material face = logo != null ? Unlit("Banner_Logo", Color.white, logo) : null;
            Material rod = Unlit("Banner_Rod", new Color(0.25f, 0.26f, 0.27f), null);

            int count = 0;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (TrainingAreaSettings area in areas)
            {
                minX = Mathf.Min(minX, area.MinX); maxX = Mathf.Max(maxX, area.MaxX);
                minZ = Mathf.Min(minZ, area.MinZ); maxZ = Mathf.Max(maxZ, area.MaxZ);
                var centre = new Vector3(area.CentreX, 0f, area.CentreZ);
                var start = new Vector3(area.Start.X, 0f, area.Start.Z);
                if (area.Area == TrainingArea.Hub)
                {
                    // Over the hub menu, facing the trainee at the hub's start.
                    Quaternion yaw = Quaternion.Euler(0f, area.Start.YawDegrees, 0f);
                    Banner(root, "Hub", start + yaw * Vector3.forward * 2.4f + Vector3.up * HubHeight, yaw, AreaSize, logo, backing, face, rod);
                }
                else
                {
                    Vector3 toStart = start - centre;
                    toStart.y = 0f;
                    Quaternion facing = toStart.sqrMagnitude > 0.01f ? Quaternion.LookRotation(-toStart.normalized) : Quaternion.identity;
                    Banner(root, area.Area.ToString(), centre + Vector3.up * AreaHeight, facing, AreaSize, logo, backing, face, rod);
                }
                count++;
            }
            // Over the walkways: the middle of each side of the box the areas fill, facing in.
            var mid = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            var sides = new[]
            {
                (new Vector3(mid.x, 0f, maxZ + 1f), Quaternion.LookRotation(Vector3.forward)),
                (new Vector3(mid.x, 0f, minZ - 1f), Quaternion.LookRotation(Vector3.back)),
                (new Vector3(maxX + 1f, 0f, mid.z), Quaternion.LookRotation(Vector3.right)),
                (new Vector3(minX - 1f, 0f, mid.z), Quaternion.LookRotation(Vector3.left)),
            };
            int s = 0;
            foreach ((Vector3 at, Quaternion facing) in sides)
            {
                Banner(root, "Walkway_" + (++s), at + Vector3.up * WalkwayHeight, facing, WalkwaySize, logo, backing, face, rod);
                count++;
            }
            log.Append($"{count} banners ({(logo != null ? "the logo" : "the studio's name; the logo file is not there yet")}). ");
        }

        // A two-sided banner: a black board, the logo (or the name) on both faces, two rods up to the roof trusses.
        // facing: the way its front looks (the trainee sees the front from the side it faces).
        private static void Banner(Transform root, string name, Vector3 at, Quaternion facing, Vector2 size, Texture2D logo,
            Material backing, Material face, Material rod)
        {
            var banner = new GameObject("Banner_" + name).transform;
            banner.SetParent(root, false);
            banner.SetPositionAndRotation(at, facing * Quaternion.Euler(0f, 180f, 0f));
            Box(banner, "Board", Vector3.zero, new Vector3(size.x, size.y, 0.04f), backing);
            for (int side = 0; side < 2; side++)
            {
                // Side 0 is read from the front (the banner's -Z), side 1 from behind.
                float z = side == 0 ? -0.022f : 0.022f;
                Quaternion turn = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
                if (logo != null)
                {
                    float aspect = logo.width / (float)Mathf.Max(1, logo.height);
                    float h = size.y * 0.86f, w = Mathf.Min(size.x * 0.92f, h * aspect);
                    h = w / aspect;
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(quad.GetComponent<Collider>());
                    quad.name = side == 0 ? "Logo_Front" : "Logo_Back";
                    quad.transform.SetParent(banner, false);
                    quad.transform.localPosition = new Vector3(0f, 0f, z);
                    quad.transform.localRotation = turn;
                    quad.transform.localScale = new Vector3(w, h, 1f);
                    var r = quad.GetComponent<MeshRenderer>();
                    r.sharedMaterial = face;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                else
                {
                    Text(banner, side == 0 ? "Name_Front" : "Name_Back", "2 DUDE STUDIOS", new Vector3(0f, size.y * 0.12f, z), turn,
                        new Vector2(size.x * 0.9f, size.y * 0.5f), 4f, Gold, true);
                    Text(banner, side == 0 ? "Site_Front" : "Site_Back", "VR TRAINING  ·  DIGITAL TWINS  ·  2dudestudios.com", new Vector3(0f, -size.y * 0.3f, z), turn,
                        new Vector2(size.x * 0.9f, size.y * 0.2f), 1.2f, new Color(0.85f, 0.86f, 0.88f), false);
                }
            }
            for (int i = -1; i <= 1; i += 2)
                Box(banner, i < 0 ? "Rod_L" : "Rod_R", new Vector3(i * size.x * 0.4f, size.y * 0.5f + CeilingRods * 0.5f, 0f), new Vector3(0.025f, CeilingRods, 0.025f), rod);
        }

        private static void Box(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Text(Transform parent, string name, string text, Vector3 at, Quaternion rotation, Vector2 size, float fontSize, Color colour, bool bold)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rotation;
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text = text;
            tmp.color = colour;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontStyle = bold ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.1f;
            tmp.fontSizeMax = fontSize;
            tmp.fontSize = fontSize;
            tmp.rectTransform.sizeDelta = size;
        }

        // An unlit material, saved with the project (Materials/Brand), so the banners cost no lighting on the headset.
        private static Material Unlit(string name, Color colour, Texture2D texture)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Brand");
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.SetTexture("_BaseMap", texture);
            if (texture != null)
            {
                // The logo's transparent background shows the black board behind it.
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.4f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
