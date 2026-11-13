using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Electrical safety (M27, ahead of M20): one item of the PPE kit on the bay's gear bench. Picked up with the grip;
    /// held to the head (or the chest, for the jacket and gloves) it is offered to the <see cref="PpeKitStation"/>,
    /// which puts it on if it is the next one. Let go anywhere else, it goes back to its place on the bench.
    /// </summary>
    public sealed class PpeItem : MonoBehaviour
    {
        [SerializeField] private PpeKitStation station;
        [SerializeField] private XRGrabInteractable grab;
        [Tooltip("Its place in PpeDressing.Kit.")]
        [SerializeField, Min(0)] private int index;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool worn;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private Transform _homeParent;
        private float _nextOffer;

        public int Index => index;
        public bool Worn => worn;
        public bool Held => grab != null && grab.isSelected;

        private void Awake()
        {
            _homeParent = transform.parent;
            _homePosition = transform.localPosition;
            _homeRotation = transform.localRotation;
        }

        private void OnEnable()
        {
            if (grab != null)
                grab.selectExited.AddListener(OnLetGo);
        }

        private void OnDisable()
        {
            if (grab != null)
                grab.selectExited.RemoveListener(OnLetGo);
        }

        private void Update()
        {
            if (worn || !Held || station == null || Time.unscaledTime < _nextOffer)
                return;
            if (!station.AtZone(transform.position, PpeDressing.Kit[index].Zone))
                return;
            // Offered once a second at most, so a refused item does not repeat its refusal every frame.
            _nextOffer = Time.unscaledTime + 1f;
            station.Offer(this);
        }

        /// <summary>The station put it on: out of the hand and out of sight.</summary>
        public void PutOn()
        {
            worn = true;
            if (grab != null && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            gameObject.SetActive(false);
        }

        /// <summary>Back on the bench, not worn (a new gear-up).</summary>
        public void Restore()
        {
            worn = false;
            gameObject.SetActive(true);
            GoHome();
        }

        private void OnLetGo(SelectExitEventArgs args)
        {
            if (!worn)
                GoHome();
        }

        private void GoHome()
        {
            if (transform.parent != _homeParent)
                transform.SetParent(_homeParent, false);
            transform.SetLocalPositionAndRotation(_homePosition, _homeRotation);
        }
    }
}
