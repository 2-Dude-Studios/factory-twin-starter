using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12, lockout): the personal padlock's shackle. It springs open (up 14 mm, then a quarter turn about
    /// its long leg) while a hand holds the lock, and snaps shut when the lock hangs in a socket (the hasp, the lock
    /// board's peg) or is let go. Looks only; XrLockSocket decides what a lock in the hasp means.
    /// </summary>
    public sealed class PadlockShackle : MonoBehaviour
    {
        [SerializeField] private XRGrabInteractable grab;
        [SerializeField] private Transform shackle;
        [Tooltip("The long leg's offset from the shackle's pivot: the axis it swivels on.")]
        [SerializeField] private Vector3 legOffset = new Vector3(-0.0125f, 0f, 0f);
        [SerializeField] private float lift = 0.014f;
        [SerializeField] private float swivelDegrees = 90f;
        [SerializeField] private float seconds = 0.25f;

        private Vector3 _closedPosition;
        private Quaternion _closedRotation;
        private float _open;
        private bool _wantOpen;

        private void Awake()
        {
            _closedPosition = shackle.localPosition;
            _closedRotation = shackle.localRotation;
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnSelected);
            grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnSelected);
            grab.selectExited.RemoveListener(OnReleased);
        }

        // Open in a hand, shut in a socket; the release that hands it to a socket is followed by the socket's select.
        private void OnSelected(SelectEnterEventArgs args) => _wantOpen = !(args.interactorObject is XRSocketInteractor);

        private void OnReleased(SelectExitEventArgs args) => _wantOpen = false;

        private void Update()
        {
            float target = _wantOpen ? 1f : 0f;
            if (Mathf.Approximately(_open, target))
                return;
            _open = Mathf.MoveTowards(_open, target, Time.deltaTime / seconds);
            // First half lifts the short leg out of the body, second half swivels it clear about the long leg.
            float up = Mathf.Clamp01(_open * 2f) * lift;
            float turn = Mathf.Clamp01(_open * 2f - 1f) * swivelDegrees;
            Quaternion swivel = Quaternion.Euler(0f, turn, 0f);
            Vector3 leg = _closedPosition + _closedRotation * legOffset;
            shackle.localPosition = leg + swivel * (_closedPosition - leg) + Vector3.up * up;
            shackle.localRotation = swivel * _closedRotation;
        }
    }
}
