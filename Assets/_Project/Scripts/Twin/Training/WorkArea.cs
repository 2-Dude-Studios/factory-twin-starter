using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training (M12): the CNC's work area for the lockout's area check (1910.147(e)(2)(i)). Publishes
    /// cell/training/area_occupied, true while any of the simulated people it watches stands inside the box. The
    /// trainee is not watched: they do the check.
    /// </summary>
    public sealed class WorkArea : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("The work area in world space.")]
        [SerializeField] private Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
        [Tooltip("The simulated people whose position counts.")]
        [SerializeField] private Transform[] people = Array.Empty<Transform>();

        private bool _occupied;
        private bool _published;

        public Bounds Area => bounds;

        private void Update()
        {
            bool occupied = false;
            foreach (Transform person in people)
            {
                if (person != null && Inside(bounds, person.position))
                {
                    occupied = true;
                    break;
                }
            }
            if (_published && occupied == _occupied)
                return;
            _occupied = occupied;
            _published = true;
            context.Bus.Publish(CellTopics.TrainingAreaOccupied, SignalValue.FromBool(occupied), CellContext.NowUnixMs);
        }

        /// <summary>Inside the area on the floor plan; height does not matter.</summary>
        public static bool Inside(Bounds area, Vector3 position) =>
            position.x >= area.min.x && position.x <= area.max.x && position.z >= area.min.z && position.z <= area.max.z;
    }
}
