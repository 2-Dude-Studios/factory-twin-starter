using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the arithmetic of a handle turned by a twist of the wrist (the isolator, the air-dump valve).
    /// A handle rests at one of two ends; while gripped it follows the controller's roll about the handle's axis,
    /// held between the ends, and on release it snaps to the nearer end.
    /// </summary>
    public static class TurnMath
    {
        /// <summary>
        /// Degrees <paramref name="up"/> has rolled from <paramref name="startUp"/> about <paramref name="axis"/>,
        /// both measured across the axis; 0 when either lies along it.
        /// </summary>
        public static float Twist(Vector3 axis, Vector3 startUp, Vector3 up)
        {
            Vector3 from = Vector3.ProjectOnPlane(startUp, axis);
            Vector3 to = Vector3.ProjectOnPlane(up, axis);
            if (from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f)
                return 0f;
            return Vector3.SignedAngle(from, to, axis);
        }

        /// <summary>The angle held between the two ends.</summary>
        public static float Clamp(float angle, float normal, float turned) =>
            Mathf.Clamp(angle, Mathf.Min(normal, turned), Mathf.Max(normal, turned));

        /// <summary>True if the handle is nearer its turned end than its normal one.</summary>
        public static bool NearerTurned(float angle, float normal, float turned) =>
            Mathf.Abs(angle - turned) < Mathf.Abs(angle - normal);
    }
}
