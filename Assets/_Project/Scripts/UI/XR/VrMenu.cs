using System;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the selection panel on the left controller (VrMenu.uxml), pressed with the right controller's
    /// ray. Lockout training, Fail the CNC and Safety reset send the same bus commands as the dashboard and keys;
    /// the Go to buttons teleport to set spots facing the right way; Exit VR hands back to the desktop.
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

        private VisualElement _root;
        private Action[] _goTo = Array.Empty<Action>();

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
        }

        private void OnDisable()
        {
            if (_root == null)
                return;
            Wire("menu-training", StartTraining, false);
            Wire("menu-fail", FailCnc, false);
            Wire("menu-reset", SafetyReset, false);
            Wire("menu-exit", ExitVr, false);
            for (int i = 0; i < spots.Length && i < _goTo.Length; i++)
                Wire(spots[i].Button, _goTo[i], false);
        }

        private void StartTraining() => Send(CellTopics.TrainingRun, 1d);

        // The scenario the lockout clears: a tool-wear failure on the CNC, as the dashboard's Tool wear button injects.
        private void FailCnc() => Send(CellTopics.PressInjectFault, (int)FaultMode.ToolWear);

        private void SafetyReset() => Send(CellTopics.SafetyReset, 1d);

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

        private void Send(TopicPath topic, double value)
        {
            context.Bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);
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
