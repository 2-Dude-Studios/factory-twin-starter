using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Electrical safety (M19): marks one named probe point on the drive cabinet model (a terminal screw). M20's
    /// probe tips touch the nearest marker within reach; the reading itself comes from <see cref="CabinetStation.Read"/>.
    /// </summary>
    public sealed class ProbePointMarker : MonoBehaviour
    {
        [SerializeField] private ProbePoint point;
        [Tooltip("How close a probe tip must be to touch this point, in metres.")]
        [SerializeField, Min(0.001f)] private float reach = 0.012f;

        public ProbePoint Point => point;
        public float Reach => reach;

        /// <summary>Editor setup: names the point.</summary>
        public void Configure(ProbePoint probePoint, float reachMetres)
        {
            point = probePoint;
            reach = reachMetres;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
