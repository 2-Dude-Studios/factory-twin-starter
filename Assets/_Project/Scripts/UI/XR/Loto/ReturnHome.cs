using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): a lockout prop let go anywhere but a socket floats for a moment, then goes back to its home
    /// socket (the padlock to its board, a tool to its slot on the cart), so nothing is ever lost on the floor.
    /// </summary>
    public sealed class ReturnHome : MonoBehaviour
    {
        [SerializeField] private XRGrabInteractable grab;
        [SerializeField] private XRSocketInteractor home;
        [SerializeField] private float delaySeconds = 0.6f;

        private float _due;

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnSelected);
            grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnSelected);
            grab.selectExited.RemoveListener(OnReleased);
            _due = 0f;
        }

        private void OnSelected(SelectEnterEventArgs args) => _due = 0f;

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!(args.interactorObject is XRSocketInteractor))
                _due = Time.time + delaySeconds;
        }

        private void Update()
        {
            if (_due <= 0f || Time.time < _due)
                return;
            _due = 0f;
            GoHome();
        }

        /// <summary>Puts the prop in its home socket now, unless something holds it or the socket is taken.</summary>
        public void GoHome()
        {
            if (grab.isSelected || home.hasSelection || grab.interactionManager == null)
                return;
            grab.interactionManager.SelectEnter((IXRSelectInteractor)home, (IXRSelectInteractable)grab);
        }
    }
}
