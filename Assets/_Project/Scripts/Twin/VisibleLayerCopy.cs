using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Cell visuals: draws this object's mesh a second time on a layer the camera's URP renderer includes. The
    /// Factory Training renderer (PC_Renderer_DC) only draws layers 0–7 and 9, so anything on realvirtual's layers
    /// (rvMU for parts, rvTransport for belt surfaces) shows in the Scene view but not in the Game view. The copy has
    /// no collider and follows the original's enabled state and material (belt texture scrolling included), so
    /// realvirtual's sensors, transport and physics see exactly what they saw before.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VisibleLayerCopy : MonoBehaviour
    {
        private const string CopyName = "VisibleCopy";

        [Tooltip("Layer the copy is drawn on. Must be in the URP renderer's opaque layer mask (0–7 or 9); 0 is Default.")]
        [SerializeField, Range(0, 31)] private int visibleLayer;

        private MeshRenderer _source;
        private MeshRenderer _copy;

        private void Awake()
        {
            _source = GetComponent<MeshRenderer>();

            // A part made by realvirtual's Source is a clone of the template, so it may already carry a copy.
            Transform existing = transform.Find(CopyName);
            GameObject copyObject = existing != null ? existing.gameObject : CreateCopy();
            copyObject.layer = visibleLayer;
            _copy = copyObject.GetComponent<MeshRenderer>();
        }

        private GameObject CreateCopy()
        {
            var copyObject = new GameObject(CopyName);
            copyObject.transform.SetParent(transform, false);
            copyObject.AddComponent<MeshFilter>().sharedMesh = GetComponent<MeshFilter>().sharedMesh;
            var copy = copyObject.AddComponent<MeshRenderer>();
            copy.sharedMaterials = _source.sharedMaterials;
            copy.shadowCastingMode = _source.shadowCastingMode;
            copy.receiveShadows = _source.receiveShadows;
            return copyObject;
        }

        private void LateUpdate()
        {
            bool show = _source.enabled;
            if (_copy.enabled != show)
                _copy.enabled = show;

            Material material = _source.sharedMaterial;
            if (_copy.sharedMaterial != material)
                _copy.sharedMaterial = material;

            GameObject copyObject = _copy.gameObject;
            if (copyObject.layer != visibleLayer)
                copyObject.layer = visibleLayer;
        }
    }
}
