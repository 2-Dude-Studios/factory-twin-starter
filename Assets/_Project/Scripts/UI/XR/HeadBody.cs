using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the body of the person in the headset, for the safety zones. A kinematic capsule on the
    /// Trainee layer that follows the headset across the floor every physics step, so walking or teleporting into
    /// the robot zone trips the same zone sensors the desktop trainee does.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class HeadBody : MonoBehaviour
    {
        [Tooltip("The headset camera the body follows.")]
        [SerializeField] private Transform head;
        [Tooltip("The XR Origin: the body stands on its floor height.")]
        [SerializeField] private Transform origin;

        private Rigidbody _body;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
        }

        private void FixedUpdate()
        {
            if (head == null)
                return;
            Vector3 headPosition = head.position;
            float floor = origin != null ? origin.position.y : 0f;
            _body.MovePosition(new Vector3(headPosition.x, floor, headPosition.z));
        }
    }
}
