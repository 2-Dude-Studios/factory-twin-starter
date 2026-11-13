using System;
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
        /// <summary>Q1's through-door handle: grip and twist, ON up, OFF to the side.</summary>
        Q1 = 0,

        /// <summary>The UPS feed breaker CB-U1's lever: a grip flips it, ON (up) to OFF (down) and back.</summary>
        UpsBreaker = 1,

        /// <summary>The door's latch: grip it to open the door, again to shut it.</summary>
        Door = 2,
    }

    /// <summary>
    /// Electrical safety (M27, ahead of M20/M21): one of the drive cabinet's controls in the hand, sending the same
    /// commands as the dashboard's electrical panel and the tablet (cell/cabinet/disconnect_cmd, ups_cmd, door_cmd).
    /// A handle or lever follows the hand while gripped and, let go, settles where the cabinet's own state says,
    /// so a move the rules refuse (Q1 on with a lock in its hasp, the door open with Q1 on) springs back and the
    /// tablet says why. A lock in its hasp stops the handle turning at all, as the real one does.
    /// </summary>
    public sealed class XrCabinetSwitch : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private CabinetControl control;
        [Tooltip("The part that turns (Q1's handle, the breaker's lever); empty for the door's latch.")]
        [SerializeField] private Transform part;
        [SerializeField] private Vector3 axis = Vector3.forward;
        [Tooltip("Angle in the ON (or shut) state, about the axis.")]
        [SerializeField] private float onAngle;
        [Tooltip("Angle in the OFF state, about the axis.")]
        [SerializeField] private float offAngle = -90f;
        [SerializeField] private float returnDegreesPerSecond = 360f;

        private Quaternion _rest;
        private float _angle;
        private float _grabStartAngle;
        private Vector3 _startUp;
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
                _angle = TurnMath.Clamp(_grabStartAngle + TurnMath.Twist(worldAxis, _startUp, attach.up), onAngle, offAngle);
            }
            else
            {
                _angle = Mathf.MoveTowards(_angle, Off() ? offAngle : onAngle, returnDegreesPerSecond * Time.deltaTime);
            }
            part.localRotation = Quaternion.AngleAxis(_angle, axis) * _rest;
        }

        private void OnGripped(SelectEnterEventArgs args)
        {
            if (control == CabinetControl.Door)
            {
                // The latch: a grip opens a shut door and shuts an open one.
                Send(CellTopics.CabinetDoorCommand, Number(CellTopics.CabinetDoorOpen) != 0d ? 0 : 1);
                return;
            }
            if (control == CabinetControl.UpsBreaker)
            {
                // The breaker's lever flips with a grip; it then settles where the cabinet's state says.
                Send(CellTopics.CabinetUpsCommand, Off() ? 0 : 1);
                return;
            }
            _hand = args.interactorObject;
            _startUp = _hand.GetAttachTransform(interactable).up;
            _grabStartAngle = _angle;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (control != CabinetControl.Q1 || args.interactorObject != _hand)
                return;
            _hand = null;
            bool off = TurnMath.NearerTurned(_angle, onAngle, offAngle);
            if (off == Off())
                return;
            Send(control == CabinetControl.Q1 ? CellTopics.CabinetDisconnectCommand : CellTopics.CabinetUpsCommand, off ? 1 : 0);
        }

        private bool Off() => Number(control == CabinetControl.Q1 ? CellTopics.CabinetDisconnect : CellTopics.CabinetUpsFeed) != 0d;

        private double Number(TopicPath topic) =>
            context.Bus.TryGetLast(topic, out SignalMessage m) ? m.Value.AsNumber : 0d;

        private void Send(TopicPath topic, double value) =>
            context.Bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);
    }
}
