using Unity.XR.CoreUtils;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Training (M12): moves the XR rig so the headset stands over a floor spot, facing a given way.</summary>
    public static class RigTeleport
    {
        /// <summary>Keeps the headset's height; turns the rig about the headset, then moves it over the spot.</summary>
        public static void GoTo(XROrigin origin, Vector3 floorPosition, float yawDegrees)
        {
            if (origin == null)
                return;
            Transform head = origin.Camera.transform;
            origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(head.eulerAngles.y, yawDegrees));
            Vector3 headPosition = head.position;
            origin.transform.position += new Vector3(floorPosition.x - headPosition.x, 0f, floorPosition.z - headPosition.z);
        }

        /// <summary>A spot <paramref name="metres"/> back from a target, on the side towards <paramref name="from"/>, facing it.</summary>
        public static (Vector3 floor, float yaw) StandOff(Vector3 target, Vector3 from, float metres)
        {
            Vector3 away = from - target;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f)
                away = Vector3.back;
            away.Normalize();
            Vector3 floor = new Vector3(target.x, 0f, target.z) + away * metres;
            float yaw = Mathf.Atan2(-away.x, -away.z) * Mathf.Rad2Deg;
            return (floor, yaw);
        }
    }
}
