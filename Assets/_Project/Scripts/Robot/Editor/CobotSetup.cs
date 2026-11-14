using System.Collections.Generic;
using System.Text;
using Unity.Robotics.UrdfImporter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwoDude.FactoryTwin.Robot
{
    /// <summary>
    /// Pick station (M29): dresses the imported arm in our own cobot (Art/Robot/Cobot.fbx, built by
    /// tools/blender/cobot.py) in place of the UR5e visual and collision meshes, so none of UR's geometry ships.
    /// Only what you see and what collides changes: the joints, their limits, the baked poses and the zones stay.
    /// </summary>
    internal static class CobotSetup
    {
        private const string ModelPath = "Assets/_Project/Art/Robot/Cobot.fbx";
        private const string MaterialFolder = "Assets/_Project/Materials/Robot";
        private const string ArmPath = "Cell/PickStation/Arm";
        private const string ChildName = "Cobot";
        private const string MeshPrefix = "Cobot_";

        // The FBX's material names, made into our own URP Lit materials (the Quest build turns them Simple Lit).
        private static readonly (string name, Color color, float smoothness, float metallic)[] Materials =
        {
            ("Cobot_Shell", new Color(0.86f, 0.87f, 0.88f), 0.55f, 0f),
            ("Cobot_Joint", new Color(0.10f, 0.11f, 0.12f), 0.45f, 0f),
            ("Cobot_Accent", new Color(0.95f, 0.42f, 0.06f), 0.5f, 0f),
            ("Cobot_Flange", new Color(0.55f, 0.56f, 0.58f), 0.65f, 0.8f),
        };

        [MenuItem("Tools/Factory Twin/Robot/Use Our Cobot on the Arm")]
        private static void BuildMenu()
        {
            var log = new StringBuilder("[CobotSetup] ");
            if (Build(log))
                EditorSceneManager.MarkAllScenesDirty();
            Debug.Log(log.ToString());
        }

        /// <summary>Swaps the arm's link meshes for the cobot's; false with the reason in log when it cannot.</summary>
        public static bool Build(StringBuilder log)
        {
            GameObject arm = GameObject.Find(ArmPath);
            if (arm == null)
            {
                log.Append($"{ArmPath} not found; open Cell.unity.");
                return false;
            }
            Dictionary<string, Mesh> meshes = Meshes();
            if (meshes.Count == 0)
            {
                log.Append($"No Cobot_ meshes in {ModelPath}.");
                return false;
            }
            Dictionary<string, Material> materials = MaterialsByName();

            int links = 0;
            foreach (UrdfLink link in arm.GetComponentsInChildren<UrdfLink>(true))
            {
                if (!meshes.TryGetValue(MeshPrefix + link.name, out Mesh mesh))
                    continue;
                Bounds before = WorldBounds(link.transform, out bool hadOld);

                // Off with the UR5e meshes, visual and collision, and any cobot from an earlier run.
                foreach (UrdfVisual visual in link.GetComponentsInChildren<UrdfVisual>(true))
                    if (OwnLink(visual.transform, link))
                        Undo.DestroyObjectImmediate(visual.gameObject);
                foreach (UrdfCollision collision in link.GetComponentsInChildren<UrdfCollision>(true))
                    if (OwnLink(collision.transform, link))
                        Undo.DestroyObjectImmediate(collision.gameObject);
                Transform old = link.transform.Find(ChildName);
                if (old != null)
                    Undo.DestroyObjectImmediate(old.gameObject);

                // The mesh is already in the link's frame (cobot.py maps ROS to Unity as the URDF importer does).
                var go = new GameObject(ChildName);
                Undo.RegisterCreatedObjectUndo(go, "Cobot link");
                go.layer = link.gameObject.layer;
                go.transform.SetParent(link.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = SlotMaterials(mesh, materials);
                // The moving links collide as their convex hulls. A fixed link (the base) gets none: it never moves,
                // and its mounting flange is wider than the UR base, so the upper arm's hull would rest on it and
                // the arm could not reach home.
                var body = link.GetComponent<ArticulationBody>();
                if (body == null || body.jointType != ArticulationJointType.FixedJoint)
                {
                    var collider = go.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    collider.convex = true;
                }

                Bounds after = renderer.bounds;
                log.Append(hadOld
                    ? $"{link.name}: UR {Fmt(before)} -> ours {Fmt(after)}; "
                    : $"{link.name}: ours {Fmt(after)}; ");
                links++;
            }
            log.Insert(0, $"{links} links dressed. ");
            return links > 0;
        }

        /// <summary>In Play: lists every collider the arm's colliders are in contact with at the current pose.</summary>
        [MenuItem("Tools/Factory Twin/Robot/Report Arm Contacts")]
        private static void ReportContacts()
        {
            GameObject arm = GameObject.Find(ArmPath);
            if (arm == null)
                return;
            var log = new StringBuilder("[CobotSetup] contacts: ");
            Collider[] own = arm.GetComponentsInChildren<Collider>();
            var buffer = new Collider[64];
            foreach (Collider c in own)
            {
                Bounds b = c.bounds;
                int n = Physics.OverlapBoxNonAlloc(b.center, b.extents, buffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    Collider other = buffer[i];
                    if (other == c)
                        continue;
                    if (Physics.ComputePenetration(c, c.transform.position, c.transform.rotation, other, other.transform.position,
                            other.transform.rotation, out _, out float depth))
                        log.Append($"{c.transform.parent.name} x {other.transform.parent?.name}/{other.name} {depth * 1000f:F1} mm; ");
                }
            }
            Debug.Log(log.ToString());
        }

        // Only the visuals and collisions of this link, not of the links below it.
        private static bool OwnLink(Transform t, UrdfLink link) => t.GetComponentInParent<UrdfLink>(true) == link;

        private static Bounds WorldBounds(Transform link, out bool found)
        {
            found = false;
            var bounds = new Bounds();
            foreach (UrdfVisual visual in link.GetComponentsInChildren<UrdfVisual>(true))
            {
                if (visual.GetComponentInParent<UrdfLink>(true).transform != link)
                    continue;
                foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (!found)
                        bounds = r.bounds;
                    else
                        bounds.Encapsulate(r.bounds);
                    found = true;
                }
            }
            return bounds;
        }

        private static string Fmt(Bounds b) => $"c({b.center.x:F3},{b.center.y:F3},{b.center.z:F3}) s({b.size.x:F3},{b.size.y:F3},{b.size.z:F3})";

        private static Dictionary<string, Mesh> Meshes()
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (asset is Mesh mesh && mesh.name.StartsWith(MeshPrefix) && !meshes.ContainsKey(mesh.name))
                    meshes.Add(mesh.name, mesh);
            return meshes;
        }

        // The FBX's own material on each submesh tells which of ours it gets.
        private static Material[] SlotMaterials(Mesh mesh, Dictionary<string, Material> materials)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != mesh)
                    continue;
                Material[] source = filter.GetComponent<MeshRenderer>().sharedMaterials;
                var result = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                    result[i] = source[i] != null && materials.TryGetValue(source[i].name, out Material m) ? m : source[i];
                return result;
            }
            return new Material[0];
        }

        private static Dictionary<string, Material> MaterialsByName()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Robot");
            var result = new Dictionary<string, Material>();
            foreach ((string name, Color color, float smoothness, float metallic) in Materials)
            {
                string path = $"{MaterialFolder}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Metallic", metallic);
                EditorUtility.SetDirty(mat);
                result[name] = mat;
            }
            AssetDatabase.SaveAssets();
            return result;
        }
    }
}
