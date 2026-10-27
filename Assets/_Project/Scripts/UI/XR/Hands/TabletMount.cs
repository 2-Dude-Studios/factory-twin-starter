using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): keeps the training tablet in the left hand whichever way the trainee is tracked. With the
    /// controllers it rides on the left controller; when the headset switches to hand tracking (the controllers are
    /// put down and switched off) it moves onto the tracked left hand's wrist, and back again.
    /// </summary>
    public sealed class TabletMount : MonoBehaviour
    {
        [SerializeField] private XRInputModalityManager modality;
        [SerializeField] private Transform tablet;
        [Tooltip("The tablet's place on the left controller.")]
        [SerializeField] private Transform controllerMount;
        [Tooltip("The tablet's place on the tracked left hand (a child of its wrist joint).")]
        [SerializeField] private Transform handMount;

        private void OnEnable()
        {
            if (modality == null)
                return;
            modality.trackedHandModeStarted.AddListener(OnHands);
            modality.motionControllerModeStarted.AddListener(OnControllers);
            if (XRInputModalityManager.currentInputMode.Value == XRInputModalityManager.InputMode.TrackedHand)
                OnHands();
        }

        private void OnDisable()
        {
            if (modality == null)
                return;
            modality.trackedHandModeStarted.RemoveListener(OnHands);
            modality.motionControllerModeStarted.RemoveListener(OnControllers);
        }

        private void OnHands() => Mount(handMount);

        private void OnControllers() => Mount(controllerMount);

        private void Mount(Transform mount)
        {
            if (tablet == null || mount == null)
                return;
            tablet.SetParent(mount, false);
            tablet.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }
}
