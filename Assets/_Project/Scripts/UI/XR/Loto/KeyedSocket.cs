using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): a socket that only takes props with a matching <see cref="SocketKey"/>: the hasp and the
    /// lock board take the padlock, the spindle and the tool cart take tools.
    /// </summary>
    public sealed class KeyedSocket : XRSocketInteractor
    {
        [SerializeField] private string key = string.Empty;

        public override bool CanHover(IXRHoverInteractable interactable) => base.CanHover(interactable) && Fits(interactable);

        public override bool CanSelect(IXRSelectInteractable interactable) => base.CanSelect(interactable) && Fits(interactable);

        private bool Fits(IXRInteractable interactable)
        {
            if (!(interactable is Component component) || component == null)
                return false;
            SocketKey prop = component.GetComponent<SocketKey>();
            return prop != null && prop.Key == key;
        }
    }
}
