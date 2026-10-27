using System;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the options on the tablet in the left hand (VrMenu.uxml), pressed with the right controller's
    /// ray or a fingertip. Lockout training, Fail the CNC and Safety reset send the same bus commands as the dashboard
    /// and keys; the Go to buttons teleport to set spots facing the right way; Exit VR hands back to the desktop. The
    /// left controller's menu button (or Y) shows and hides the tablet, display and options together.
    /// </summary>
    public sealed class VrMenu : MonoBehaviour
    {
        [Serializable]
        private sealed class Spot
        {
            [SerializeField] private string button;
            [SerializeField] private Vector3 floorPosition;
            [SerializeField] private float yawDegrees;

            public string Button => button;
            public Vector3 FloorPosition => floorPosition;
            public float YawDegrees => yawDegrees;
        }

        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;
        [SerializeField] private VrSwitch vrSwitch;
        [SerializeField] private XROrigin origin;
        [Tooltip("Teleport spots: the button's name in VrMenu.uxml, where to stand and which way to face.")]
        [SerializeField] private Spot[] spots = Array.Empty<Spot>();
        [Tooltip("Shows and hides the panel. Left empty, the left controller's menu button and Y are used.")]
        [SerializeField] private InputActionProperty toggle;

        private VisualElement _root;
        private Action[] _goTo = Array.Empty<Action>();
        private InputAction _ownToggle;
        private bool _shown = true;

        private InputAction Toggle
        {
            get
            {
                if (toggle.action != null && toggle.action.bindings.Count > 0)
                    return toggle.action;
                if (_ownToggle == null)
                {
                    _ownToggle = new InputAction("Toggle VR menu", InputActionType.Button);
                    _ownToggle.AddBinding("<XRController>{LeftHand}/{MenuButton}");
                    _ownToggle.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
                }
                return _ownToggle;
            }
        }

        private void OnEnable()
        {
            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
                return;
            Wire("menu-training", StartTraining, true);
            Wire("menu-fail", FailCnc, true);
            Wire("menu-reset", SafetyReset, true);
            Wire("menu-exit", ExitVr, true);
            _goTo = new Action[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                Spot spot = spots[i];
                _goTo[i] = () => GoTo(spot);
                Wire(spot.Button, _goTo[i], true);
            }
            Show(_shown);
            Toggle.performed += OnToggle;
            Toggle.Enable();
        }

        private void OnDisable()
        {
            if (_root == null)
                return;
            Toggle.performed -= OnToggle;
            if (Toggle == _ownToggle)
                _ownToggle.Disable();
            Wire("menu-training", StartTraining, false);
            Wire("menu-fail", FailCnc, false);
            Wire("menu-reset", SafetyReset, false);
            Wire("menu-exit", ExitVr, false);
            for (int i = 0; i < spots.Length && i < _goTo.Length; i++)
                Wire(spots[i].Button, _goTo[i], false);
        }

        private void OnDestroy()
        {
            _ownToggle?.Dispose();
            _ownToggle = null;
        }

        private void OnToggle(InputAction.CallbackContext _) => Show(!_shown);

        private void Show(bool shown)
        {
            _shown = shown;
            _root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void StartTraining() => TrainingCommands.StartTraining(context.Bus);

        private void FailCnc() => TrainingCommands.FailCnc(context.Bus);

        private void SafetyReset() => TrainingCommands.SafetyReset(context.Bus);

        private void ExitVr()
        {
            if (vrSwitch != null)
                vrSwitch.StopVr();
        }

        // Moves the rig so the headset stands over the spot (keeping its height) and faces the given way.
        private void GoTo(Spot spot)
        {
            if (origin == null)
                return;
            Transform head = origin.Camera.transform;
            origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(head.eulerAngles.y, spot.YawDegrees));
            Vector3 headPosition = head.position;
            origin.transform.position += new Vector3(spot.FloorPosition.x - headPosition.x, 0f, spot.FloorPosition.z - headPosition.z);
        }

        private void Wire(string buttonName, Action handler, bool wire)
        {
            Button button = _root.Q<Button>(buttonName);
            if (button == null)
                return;
            if (wire)
                button.clicked += handler;
            else
                button.clicked -= handler;
        }
    }
}
