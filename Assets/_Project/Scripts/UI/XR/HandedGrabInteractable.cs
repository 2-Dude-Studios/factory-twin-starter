using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M15): a grab that sits in the palm differently for each hand. The right hand uses the attach
    /// transform; the left hand uses its own, mirrored, so a part with a top and a bottom (an extinguisher's carry
    /// handle) hangs the right way up in either hand. Both attaches are grip frames turned into controller attaches
    /// (HandPoseMath.ControllerAttach).
    /// </summary>
    public sealed class HandedGrabInteractable : XRGrabInteractable
    {
        [Tooltip("The attach for the left hand. Empty: both hands use the attach transform.")]
        [SerializeField] private Transform leftAttachTransform;

        public Transform LeftAttachTransform => leftAttachTransform;

        public override Transform GetAttachTransform(IXRInteractor interactor)
        {
            if (leftAttachTransform != null && interactor != null && interactor.handedness == InteractorHandedness.Left)
                return leftAttachTransform;
            return base.GetAttachTransform(interactor);
        }
    }
}
