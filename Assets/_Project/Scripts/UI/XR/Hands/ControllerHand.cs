using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): a hand drawn round a Quest controller in place of the controller model. The trigger curls the
    /// index finger and the grip the other three; while the controller's interactor holds a prop with a HandGrip, the
    /// fingers take its grip, and on a fixed part (a handle) the hand moves onto the part's anchor until it lets go.
    /// The hand model is the XR Hands skeleton (L_/R_ joint names), the same one the tracked hands use.
    /// </summary>
    public sealed class ControllerHand : MonoBehaviour
    {
        private static readonly string[][] JointNames =
        {
            new[] { "ThumbMetacarpal", "ThumbProximal", "ThumbDistal" },
            new[] { "IndexProximal", "IndexIntermediate", "IndexDistal" },
            new[] { "MiddleProximal", "MiddleIntermediate", "MiddleDistal" },
            new[] { "RingProximal", "RingIntermediate", "RingDistal" },
            new[] { "LittleProximal", "LittleIntermediate", "LittleDistal" },
        };

        [SerializeField] private bool leftHand;
        [Tooltip("The controller's interactor whose grip and trigger drive the fingers and whose held prop sets the grip.")]
        [SerializeField] private XRBaseInputInteractor interactor;
        [Tooltip("The controller: its pose is the grip pose the hand closes round.")]
        [SerializeField] private Transform controller;
        [SerializeField] private float curlPerSecond = 7f;
        [SerializeField] private float settleSeconds = 0.08f;

        private readonly Transform[,] _joints = new Transform[HandPoseMath.Fingers, 3];
        private readonly Quaternion[,] _bind = new Quaternion[HandPoseMath.Fingers, 3];
        private Pose _rootInGrip;
        private Pose _rootInController;
        private FingerCurls _curls = FingerCurls.Relaxed;
        private HandGrip _held;
        private Transform _anchor;
        private float _onAnchor;
        private bool _ready;

        private void Awake()
        {
            string prefix = leftHand ? "L_" : "R_";
            Transform palm = FindDeep(transform, prefix + "Palm");
            for (int f = 0; f < HandPoseMath.Fingers; f++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Transform joint = FindDeep(transform, prefix + JointNames[f][j]);
                    _joints[f, j] = joint;
                    _bind[f, j] = joint != null ? joint.localRotation : Quaternion.identity;
                }
            }
            if (palm == null || controller == null || transform.parent != controller)
            {
                Debug.LogWarning($"[ControllerHand] {name}: no {prefix}Palm joint, or not a child of its controller; the hand stays where it is.", this);
                return;
            }
            // Where the root must sit so the palm closes round a frame: frame * inverse(frame in palm) * inverse(palm in root).
            var root = new Pose(transform.position, transform.rotation);
            var palmPose = new Pose(palm.position, palm.rotation);
            Pose palmInRoot = HandPoseMath.Multiply(HandPoseMath.Inverse(root), palmPose);
            // On a handle the anchor is a grip frame; on the controller the hand goes where the controller's own frame
            // sits in the palm (HandPoseMath.ControllerInPalm).
            _rootInGrip = HandPoseMath.Multiply(HandPoseMath.Inverse(HandPoseMath.GripInPalm(leftHand)), HandPoseMath.Inverse(palmInRoot));
            _rootInController = HandPoseMath.Multiply(HandPoseMath.Inverse(HandPoseMath.ControllerInPalm(leftHand)), HandPoseMath.Inverse(palmInRoot));
            transform.SetLocalPositionAndRotation(_rootInController.position, _rootInController.rotation);
            _ready = true;
        }

        private void OnEnable()
        {
            // After the tracked poses: the Input System moves the controller before render, then this puts the hand
            // back on a held handle, so it does not trail the controller by a frame.
            Application.onBeforeRender += PlaceHand;
            if (interactor == null)
                return;
            interactor.selectEntered.AddListener(OnSelectEntered);
            interactor.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= PlaceHand;
            if (interactor != null)
            {
                interactor.selectEntered.RemoveListener(OnSelectEntered);
                interactor.selectExited.RemoveListener(OnSelectExited);
            }
            _held = null;
            _anchor = null;
            _onAnchor = 0f;
            if (_ready)
                transform.SetLocalPositionAndRotation(_rootInController.position, _rootInController.rotation);
        }

        // A fixed part (a handle, a lever): the hand leaves the controller and closes on the part, turning with it.
        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            _held = args.interactableObject.transform.GetComponentInParent<HandGrip>();
            if (_held != null && _held.Anchor != null)
                _anchor = _held.Anchor;
        }

        private void OnSelectExited(SelectExitEventArgs args) => _held = null;

        private void LateUpdate()
        {
            FingerCurls target = _held != null
                ? _held.Curls
                : HandPoseMath.FromInput(interactor != null ? interactor.selectInput.ReadValue() : 0f,
                    interactor != null ? interactor.activateInput.ReadValue() : 0f);
            _curls = HandPoseMath.MoveTowards(_curls, target, curlPerSecond * Time.deltaTime);
            for (int f = 0; f < HandPoseMath.Fingers; f++)
            {
                float curl = _curls[f];
                for (int j = 0; j < 3; j++)
                {
                    if (_joints[f, j] != null)
                        _joints[f, j].localRotation = _bind[f, j] * Quaternion.Euler(curl * HandPoseMath.MaxBend(f, j), 0f, 0f);
                }
            }

            // Settle onto the anchor, or back onto the controller, over a few frames rather than jumping there.
            bool holdingAnchor = _held != null && _anchor != null && _held.Anchor == _anchor;
            _onAnchor = Mathf.MoveTowards(_onAnchor, holdingAnchor ? 1f : 0f, Time.deltaTime / settleSeconds);
            if (_onAnchor <= 0f)
                _anchor = null;
            PlaceHand();
        }

        // The hand stays a child of its controller (so it switches off with it); on a handle its world pose is set from
        // the anchor, blended with its place on the controller while it settles.
        private void PlaceHand()
        {
            if (!_ready)
                return;
            if (_anchor == null)
            {
                transform.SetLocalPositionAndRotation(_rootInController.position, _rootInController.rotation);
                return;
            }
            Pose onController = HandPoseMath.Multiply(new Pose(controller.position, controller.rotation), _rootInController);
            var anchorPose = new Pose(_anchor.position, HandPoseMath.AnchorFor(leftHand, _anchor.rotation));
            Pose onAnchor = HandPoseMath.Multiply(anchorPose, _rootInGrip);
            float t = Mathf.SmoothStep(0f, 1f, _onAnchor);
            transform.SetPositionAndRotation(Vector3.Lerp(onController.position, onAnchor.position, t),
                Quaternion.Slerp(onController.rotation, onAnchor.rotation, t));
        }

        private static Transform FindDeep(Transform parent, string jointName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == jointName)
                    return child;
                Transform found = FindDeep(child, jointName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
