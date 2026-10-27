using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): lets a controller use a training point. Hovering the point with a controller ray lights it;
    /// the trigger uses it, the same cell/training/action a desktop click sends. The trigger is XRI's activate input,
    /// which XRI only passes to things already selected (select is the grip), so the point reads it from the
    /// hovering controller itself; a grip press uses the point too.
    /// </summary>
    [RequireComponent(typeof(TrainingPoint), typeof(XRSimpleInteractable))]
    public sealed class XrTrainingPoint : MonoBehaviour
    {
        [Tooltip("The headset HUD that shows this point's prompt while a ray is on it.")]
        [SerializeField] private VrHud hud;

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

        private void Update()
        {
            if (!_interactable.isHovered)
                return;
            List<IXRHoverInteractor> hovering = _interactable.interactorsHovering;
            for (int i = 0; i < hovering.Count; i++)
            {
                if (hovering[i] is XRBaseInputInteractor controller && controller.activateInput.ReadWasPerformedThisFrame())
                {
                    Use();
                    return;
                }
            }
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            _point.SetHovered(true);
            if (hud != null)
                hud.SetPrompt(_point.Prompt, VrHudText.ShortHow(_point.Kind));
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            _point.SetHovered(_interactable.isHovered);
            if (hud != null && !_interactable.isHovered)
                hud.SetPrompt(string.Empty);
        }

        private void OnSelectEntered(SelectEnterEventArgs args) => Use();

        // The prompt changes with the point's state (isolator off, then on), so it is refreshed after each use.
        private void Use()
        {
            _point.Use();
            if (hud != null)
                hud.SetPrompt(_point.Prompt, VrHudText.ShortHow(_point.Kind));
        }
    }
}
