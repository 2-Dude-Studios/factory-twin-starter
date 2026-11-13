using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Electrical safety (M27, ahead of M20/M21): which of the cabinet's controls a hand works.</summary>
    public enum CabinetControl
    {
        /// <summary>Q1's through-door handle: grip and twist the wrist, ON up, OFF to the side.</summary>
        Q1 = 0,

        /// <summary>The UPS feed breaker CB-U1's lever: grip it and move the hand down to OFF, up to ON.</summary>
        UpsBreaker = 1,

        /// <summary>The door: grip its latch and swing it open on the hinge, or push it shut.</summary>
        Door = 2,
    }

    /// <summary>
    /// Electrical safety (M27, ahead of M20/M21): one of the drive cabinet's controls in the hand, sending the same
    /// commands as the dashboard's electrical panel and the tablet (cell/cabinet/disconnect_cmd, ups_cmd, door_cmd).
    /// While gripped the part follows the hand (a HandGrip anchor keeps the hand on it): Q1's handle by the wrist's
    /// twist, the breaker's lever and the door by where the hand goes round their hinge. Let go, the move is sent if
    /// it ended nearer the other state, and the part settles where the cabinet's own state says, so a move the rules
    /// refuse (Q1 on with a lock in its hasp, the door open with Q1 on) swings back and the tablet says why. A lock in
    /// its hasp stops the handle or lever moving at all, as the real one does.
    /// </summary>
    public sealed class XrCabinetSwitch : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private CabinetControl control;
        [Tooltip("The part that moves: Q1's handle, the breaker's lever, the door (pivot on its hinge).")]
        [SerializeField] private Transform part;
        [Tooltip("The part's hinge axis, in its parent's space.")]
        [SerializeField] private Vector3 axis = Vector3.forward;
        [Tooltip("Angle in the ON (or shut) state, about the axis.")]
        [SerializeField] private float onAngle;
        [Tooltip("Angle in the OFF (or open) state, about the axis.")]
        [SerializeField] private float offAngle = -90f;
        [Tooltip("For the door: the share of the way open past which a let-go opens it.")]
        [SerializeField, Range(0.05f, 0.95f)] private float openAt = 0.3f;
        [SerializeField] private float returnDegreesPerSecond = 240f;

        private Quaternion _rest;
        private float _angle;
        private float _grabStartAngle;
        private Vector3 _startUp;
        private Vector3 _startArm;
        private IXRSelectInteractor _hand;

        private void Awake()
        {
            if (part != null)
                _rest = part.localRotation;
            _angle = onAngle;
        }

        private void OnEnable()
        {
            interactable.selectEntered.AddListener(OnGripped);
            interactable.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnGripped);
            interactable.selectExited.RemoveListener(OnReleased);
            _hand = null;
        }

        private void Update()
        {
            bool locked = control == CabinetControl.Q1 ? Number(CellTopics.CabinetLocked) > 0d
                : control == CabinetControl.UpsBreaker && Number(CellTopics.CabinetUpsLocked) > 0d;
            if (interactable.enabled == locked)
                interactable.enabled = !locked;
            if (part == null)
                return;
            if (_hand != null)
            {
                Transform attach = _hand.GetAttachTransform(interactable);
                Vector3 worldAxis = part.parent.TransformDirection(axis);
                float moved = control == CabinetControl.Q1
                    ? TurnMath.Twist(worldAxis, _startUp, attach.up)
                    : Vector3.SignedAngle(Arm(attach.position, worldAxis), _startArm, -worldAxis);
                _angle = TurnMath.Clamp(_grabStartAngle + moved, onAngle, offAngle);
            }
            else
            {
                _angle = Mathf.MoveTowards(_angle, Off() ? offAngle : onAngle, returnDegreesPerSecond * Time.deltaTime);
            }
            part.localRotation = Quaternion.AngleAxis(_angle, axis) * _rest;
        }

        // From the hinge to the hand, flat in the plane the part turns in.
        private Vector3 Arm(Vector3 hand, Vector3 worldAxis) => Vector3.ProjectOnPlane(hand - part.position, worldAxis);

        private void OnGripped(SelectEnterEventArgs args)
        {
            _hand = args.interactorObject;
            Transform attach = _hand.GetAttachTransform(interactable);
            _startUp = attach.up;
            _startArm = part != null ? Arm(attach.position, part.parent.TransformDirection(axis)) : Vector3.zero;
            _grabStartAngle = _angle;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (args.interactorObject != _hand)
                return;
            _hand = null;
            bool off = control == CabinetControl.Door
                ? Mathf.Abs(_angle - onAngle) >= openAt * Mathf.Abs(offAngle - onAngle)
                : TurnMath.NearerTurned(_angle, onAngle, offAngle);
            if (off == Off())
                return;
            TopicPath command = control == CabinetControl.Q1 ? CellTopics.CabinetDisconnectCommand
                : control == CabinetControl.UpsBreaker ? CellTopics.CabinetUpsCommand
                : CellTopics.CabinetDoorCommand;
            Send(command, off ? 1 : 0);
        }

        // OFF for Q1 and the breaker (cell/cabinet/disconnect and ups_feed are 1 when open), open for the door.
        private bool Off() => Number(control == CabinetControl.Q1 ? CellTopics.CabinetDisconnect
            : control == CabinetControl.UpsBreaker ? CellTopics.CabinetUpsFeed
            : CellTopics.CabinetDoorOpen) != 0d;

        private double Number(TopicPath topic) =>
            context.Bus.TryGetLast(topic, out SignalMessage m) ? m.Value.AsNumber : 0d;

        private void Send(TopicPath topic, double value) =>
            context.Bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);
    }
}
