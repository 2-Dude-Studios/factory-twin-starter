using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training: the desktop trainee, a first-person walker the safety zone sensors can see. Arrow keys walk, Shift
    /// walks faster, the right mouse button held turns the view (so the cursor stays free for the dashboard). It
    /// moves only while its eye camera is the live shot (ShotDirector, F5). The body is a kinematic capsule on the
    /// Trainee layer, which only the safety zone sensors collide with: the trainee walks through machines and
    /// parts, and is never seen by the cell's part sensors. Height stays fixed, so no floor colliders are needed.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class TraineeWalker : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera eye;
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.4f;
        [SerializeField, Min(1f)] private float fastMultiplier = 2.5f;
        [SerializeField, Min(0.01f)] private float lookDegreesPerPixel = 0.15f;
        [SerializeField, Range(0f, 89f)] private float pitchLimit = 70f;

        private Rigidbody _body;
        private float _yaw;
        private float _pitch;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _yaw = transform.eulerAngles.y;
            if (eye != null)
                _pitch = NormalizedPitch(eye.transform.localEulerAngles.x);
        }

        /// <summary>
        /// Puts the trainee at (<paramref name="x"/>, <paramref name="z"/>) on the floor, through the Rigidbody so the
        /// interpolated body does not pull it back. For scripted takes and checks; the zones see it on the next step.
        /// </summary>
        public void Teleport(float x, float z)
        {
            var position = new Vector3(x, transform.position.y, z);
            _body.position = position;
            transform.position = position;
        }

        /// <summary>
        /// Puts the trainee at (<paramref name="x"/>, <paramref name="z"/>) looking along <paramref name="yawDegrees"/>
        /// (0 is +z) and <paramref name="pitchDegrees"/> down. For scripted takes and checks.
        /// </summary>
        public void Teleport(float x, float z, float yawDegrees, float pitchDegrees)
        {
            Teleport(x, z);
            _yaw = yawDegrees;
            _pitch = Mathf.Clamp(pitchDegrees, -pitchLimit, pitchLimit);
            Quaternion facing = Quaternion.Euler(0f, _yaw, 0f);
            _body.rotation = facing;
            transform.rotation = facing;
            if (eye != null)
                eye.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Update()
        {
            if (eye == null || !eye.IsLive)
                return;
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed)
                return;

            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * lookDegreesPerPixel;
            _pitch = Mathf.Clamp(_pitch - delta.y * lookDegreesPerPixel, -pitchLimit, pitchLimit);
            eye.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        // The body moves in FixedUpdate through the kinematic Rigidbody, so the zone triggers see every step.
        private void FixedUpdate()
        {
            Quaternion facing = Quaternion.Euler(0f, _yaw, 0f);
            _body.MoveRotation(facing);

            Keyboard keyboard = Keyboard.current;
            if (eye == null || !eye.IsLive || keyboard == null)
                return;

            Vector2 input = Vector2.zero;
            if (keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (input == Vector2.zero)
                return;

            float speed = walkSpeed * (keyboard.shiftKey.isPressed ? fastMultiplier : 1f);
            Vector3 step = facing * new Vector3(input.x, 0f, input.y).normalized * (speed * Time.fixedDeltaTime);
            _body.MovePosition(_body.position + step);
        }

        private static float NormalizedPitch(float degrees) => degrees > 180f ? degrees - 360f : degrees;
    }
}
