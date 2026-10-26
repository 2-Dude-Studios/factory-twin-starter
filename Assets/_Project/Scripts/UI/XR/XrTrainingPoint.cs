using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): lets a controller use a training point. Hovering the point with a controller ray lights it,
    /// selecting it (the trigger) uses it: the same cell/training/action a desktop click sends.
    /// </summary>
    [RequireComponent(typeof(TrainingPoint), typeof(XRSimpleInteractable))]
    public sealed class XrTrainingPoint : MonoBehaviour
    {
        private TrainingPoint _point;
        private XRSimpleInteractable _interactable;

        private void Awake()
        {
            _point = GetComponent<TrainingPoint>();
            _interactable = GetComponent<XRSimpleInteractable>();
        }

        private void OnEnable()
        {
            _interactable.hoverEntered.AddListener(OnHoverEntered);
            _interactable.hoverExited.AddListener(OnHoverExited);
            _interactable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            _interactable.hoverEntered.RemoveListener(OnHoverEntered);
            _interactable.hoverExited.RemoveListener(OnHoverExited);
            _interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void OnHoverEntered(HoverEnterEventArgs args) => _point.SetHovered(true);

        private void OnHoverExited(HoverExitEventArgs args) => _point.SetHovered(_interactable.isHovered);

        private void OnSelectEntered(SelectEnterEventArgs args) => _point.Use();
    }
}
