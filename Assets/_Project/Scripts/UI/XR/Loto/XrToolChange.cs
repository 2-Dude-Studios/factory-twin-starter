using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the CNC spindle's tool change. Taking the worn tool out of the spindle and putting the other
    /// one in uses the training point (cell/training/action ChangeTool), whether or not the machine is locked out,
    /// so changing a tool before isolating counts as the mistake it would be.
    /// </summary>
    public sealed class XrToolChange : MonoBehaviour
    {
        [SerializeField] private TrainingPoint point;
        [SerializeField] private XRSocketInteractor spindle;

        private IXRSelectInteractable _last;

        private void Awake()
        {
            _last = spindle.startingSelectedInteractable;
        }

        private void OnEnable()
        {
            spindle.selectEntered.AddListener(OnFitted);
        }

        private void OnDisable()
        {
            spindle.selectEntered.RemoveListener(OnFitted);
        }

        private void OnFitted(SelectEnterEventArgs args)
        {
            if (args.interactableObject == _last)
                return;
            _last = args.interactableObject;
            point.Use();
        }
    }
}
