using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training areas (M24): the spot in an area where its drill starts. The desktop trainee and the headset rig are
    /// both put here, facing the way this transform faces. Placed by Build Training Areas from the layout asset.
    /// </summary>
    public sealed class TrainingAreaStart : MonoBehaviour
    {
        [SerializeField] private TrainingArea area;

        public TrainingArea Area => area;
        public Vector3 FloorPosition => new Vector3(transform.position.x, 0f, transform.position.z);
        public float YawDegrees => transform.eulerAngles.y;
    }
}
