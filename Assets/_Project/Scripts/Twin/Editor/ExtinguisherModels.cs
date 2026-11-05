using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Fire safety (M15), editor tooling: assembles an extinguisher from the parts in Fire_Extinguishers.fbx
    /// (tools/blender/fire_extinguishers.py). Each part gets a transform at its pivot holding the model's mesh, the
    /// group's origin is the carry-handle grip. Stowed: hose on, nozzle clipped at the hose end pointing down. Held:
    /// hose hidden, nozzle forward of the hand, its transform's +Z the way the stream goes.
    /// </summary>
    public static class ExtinguisherModels
    {
        public const string ModelPath = "Assets/_Project/Art/Fire/Fire_Extinguishers.fbx";
        public static readonly string[] Kinds = { "Ext_Water", "Ext_CO2", "Ext_ABC" };
        private static readonly string[] Parts = { "Body", "Hose", "Lever", "Pin", "Nozzle" };

        public static Dictionary<string, Transform> LoadParts()
        {
            var parts = new Dictionary<string, Transform>();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (root == null)
                return parts;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && filter.GetComponent<MeshRenderer>() != null && !parts.ContainsKey(filter.name))
                    parts.Add(filter.name, filter.transform);
            }
            return parts;
        }

        /// <summary>Builds (or rebuilds) the group named <paramref name="name"/> under the parent. Returns its nozzle.</summary>
        public static Transform Build(string kind, string name, Transform parent, bool held, out GameObject group)
        {
            Dictionary<string, Transform> parts = LoadParts();
            Transform old = parent.Find(name);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            group = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(group, name);
            group.transform.SetParent(parent, false);
            if (!parts.TryGetValue(kind + "_Body", out Transform body))
            {
                Debug.LogWarning($"[ExtinguisherModels] {ModelPath} has no {kind}_Body; build the models first (tools/blender/fire_extinguishers.py).");
                return null;
            }

            Transform nozzle = null, hose = null;
            foreach (string part in Parts)
            {
                if (!parts.TryGetValue($"{kind}_{part}", out Transform source))
                    continue;
                var go = new GameObject(part);
                go.transform.SetParent(group.transform, false);
                // Part pivots sit apart in the FBX as they do round the grip; the body's pivot is the grip.
                go.transform.localPosition = source.localPosition - body.localPosition;
                var visual = new GameObject("Model");
                visual.transform.SetParent(go.transform, false);
                visual.transform.localRotation = source.localRotation;
                visual.transform.localScale = source.localScale;
                visual.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                visual.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                if (part == "Nozzle")
                    nozzle = go.transform;
                if (part == "Hose")
                    hose = go.transform;
            }

            if (held)
            {
                if (hose != null)
                    hose.gameObject.SetActive(false);
            }
            else if (nozzle != null && hose != null)
            {
                Stow(nozzle, hose);
            }
            return nozzle;
        }

        // Nozzle pointing down, its back end at the lowest point of the hose (the clip on the body's side).
        private static void Stow(Transform nozzle, Transform hose)
        {
            nozzle.rotation = Quaternion.LookRotation(Vector3.down, nozzle.parent.forward);
            Bounds hoseBounds = WorldBounds(hose);
            Bounds nozzleBounds = WorldBounds(nozzle);
            var hoseEnd = new Vector3(hoseBounds.center.x, hoseBounds.min.y, hoseBounds.center.z);
            var nozzleBack = new Vector3(nozzleBounds.center.x, nozzleBounds.max.y, nozzleBounds.center.z);
            nozzle.position += hoseEnd - nozzleBack;
            // Shift sideways so the nozzle sits on the hose line, not the bounds' centre.
            Vector3 lowest = LowestVertex(hose);
            nozzle.position += new Vector3(lowest.x - hoseEnd.x, 0f, lowest.z - hoseEnd.z);
        }

        private static Bounds WorldBounds(Transform t)
        {
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(t.position, Vector3.zero);
            foreach (Renderer r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }

        private static Vector3 LowestVertex(Transform t)
        {
            MeshFilter filter = t.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null)
                return t.position;
            Vector3 lowest = new Vector3(0f, float.PositiveInfinity, 0f);
            foreach (Vector3 v in filter.sharedMesh.vertices)
            {
                Vector3 w = filter.transform.TransformPoint(v);
                if (w.y < lowest.y)
                    lowest = w;
            }
            return lowest;
        }
    }
}
