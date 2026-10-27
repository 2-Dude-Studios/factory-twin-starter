using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Training (M12): what a turn handle is wired to.</summary>
    public enum TurnBinding
    {
        /// <summary>The CNC's main isolator: off when turned, follows cell/press/isolated, locked by cell/press/lockout.</summary>
        Isolator = 0,

        /// <summary>The air-dump valve: turning it releases the stored air once per lockout run.</summary>
        AirValve = 1,
    }

    /// <summary>
    /// Training (M12): a handle the trainee grips (grip button) and turns a quarter turn with the wrist, the isolator
    /// or the air-dump valve. On release it snaps to the nearer end; reaching the other end uses the training point,
    /// the same cell/training/action a click sends. At rest it shows the plant's state, so a desktop click or a
    /// refused command moves it too. The isolator handle cannot be gripped while a lock hangs on it.
    /// </summary>
    public sealed class XrTurnHandle : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private TrainingPoint point;
        [SerializeField] private XRSimpleInteractable interactable;
        [Tooltip("The part that turns, its origin on the turning axis (Isolator_Handle, Valve_Lever).")]
        [SerializeField] private Transform handle;
        [Tooltip("The turning axis in the handle's parent space.")]
        [SerializeField] private Vector3 axis = Vector3.right;
        [SerializeField] private float normalAngle;
        [SerializeField] private float turnedAngle = -90f;
        [SerializeField] private TurnBinding binding;
        [SerializeField] private float returnDegreesPerSecond = 360f;

        private Quaternion _rest;
        private float _angle;
        private float _grabStartAngle;
        private Vector3 _startUp;
        private IXRSelectInteractor _hand;
        private bool _valveTurned;
        private IDisposable _runSubscription;

        private void Awake()
        {
            _rest = handle.localRotation;
            _angle = normalAngle;
        }

        private void OnEnable()
        {
            interactable.selectEntered.AddListener(OnGripped);
            interactable.selectExited.AddListener(OnReleased);
            if (binding == TurnBinding.AirValve)
                _runSubscription = context.Bus.Subscribe(CellTopics.TrainingRun.ToString(), _ => _valveTurned = false);
        }

        private void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnGripped);
            interactable.selectExited.RemoveListener(OnReleased);
            _runSubscription?.Dispose();
            _runSubscription = null;
            _hand = null;
        }

        private void Update()
        {
            bool canTurn = binding != TurnBinding.Isolator || !Read(CellTopics.PressLockout);
            if (interactable.enabled != canTurn)
                interactable.enabled = canTurn;

            if (_hand != null)
            {
                Transform attach = _hand.GetAttachTransform(interactable);
                Vector3 worldAxis = handle.parent.TransformDirection(axis);
                _angle = TurnMath.Clamp(_grabStartAngle + TurnMath.Twist(worldAxis, _startUp, attach.up), normalAngle, turnedAngle);
            }
            else
            {
                _angle = Mathf.MoveTowards(_angle, RestAngle(), returnDegreesPerSecond * Time.deltaTime);
            }
            handle.localRotation = Quaternion.AngleAxis(_angle, axis) * _rest;
        }

        private float RestAngle()
        {
            bool turned = binding == TurnBinding.Isolator ? Read(CellTopics.PressIsolated) : _valveTurned;
            return turned ? turnedAngle : normalAngle;
        }

        private void OnGripped(SelectEnterEventArgs args)
        {
            _hand = args.interactorObject;
            _startUp = _hand.GetAttachTransform(interactable).up;
            _grabStartAngle = _angle;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (args.interactorObject != _hand)
                return;
            _hand = null;
            bool turned = TurnMath.NearerTurned(_angle, normalAngle, turnedAngle);
            if (binding == TurnBinding.Isolator)
            {
                if (turned != Read(CellTopics.PressIsolated))
                    point.Use();
                return;
            }
            if (turned && !_valveTurned)
            {
                _valveTurned = true;
                point.Use();
            }
            else if (!turned)
            {
                _valveTurned = false;
            }
        }

        private bool Read(TopicPath topic) =>
            context.Bus.TryGetLast(topic, out SignalMessage m) && m.Value.AsNumber != 0d;
    }
}
