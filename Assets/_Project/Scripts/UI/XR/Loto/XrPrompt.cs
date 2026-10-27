using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): on a gripped lockout prop (isolator handle, padlock, valve lever, tool), lights its training
    /// point and shows the point's prompt with how to use it on the headset HUD while a controller is on it.
    /// </summary>
    public sealed class XrPrompt : MonoBehaviour
    {
        [SerializeField] private XRBaseInteractable interactable;
        [SerializeField] private TrainingPoint point;
        [SerializeField] private VrHud hud;

        private void OnEnable()
        {
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }

        private void OnDisable()
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            point.SetHovered(true);
            if (hud != null)
                hud.SetPrompt(point.Prompt, VrHudText.ShortHow(point.Kind));
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (interactable.isHovered)
                return;
            point.SetHovered(false);
            if (hud != null)
                hud.SetPrompt(string.Empty);
        }
    }
}
