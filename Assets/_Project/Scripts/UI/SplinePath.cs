using UnityEngine;
using UnityEngine.Splines;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Capture tools: keeps a SplineContainer's first spline equal to a list of points (local space) with
    /// auto-smoothed tangents, so a camera path is edited as a few positions in the Inspector instead of tangent
    /// handles. Rebuilds whenever the points change in the Editor.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public sealed class SplinePath : MonoBehaviour
    {
        [SerializeField] private Vector3[] points =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
        };

        public int PointCount => points.Length;

        private void OnValidate()
        {
            Rebuild();
        }

        /// <summary>Replaces the spline's knots with the points, auto-smoothed.</summary>
        public void Rebuild()
        {
            var container = GetComponent<SplineContainer>();
            if (container == null || points == null || points.Length < 2)
                return;

            Spline spline = container.Spline;
            spline.Clear();
            foreach (Vector3 point in points)
                spline.Add(point, TangentMode.AutoSmooth);
            spline.Closed = false;
        }
    }
}
