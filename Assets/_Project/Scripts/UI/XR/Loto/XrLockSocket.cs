using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the hasp under the CNC isolator. Hanging the padlock (with its tag) in the hasp's socket applies
    /// the lock; taking it out removes it, each the same cell/training/action a click sends. The padlock follows
    /// cell/press/lockout whenever no hand holds it: a lock the rules refuse (isolator still on) goes back to its
    /// board, and a lock applied by a click or a reset appears in, or leaves, the hasp.
    /// </summary>
    public sealed class XrLockSocket : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private TrainingPoint point;
        [SerializeField] private XRSocketInteractor lockSocket;
        [SerializeField] private XRSocketInteractor homeSocket;
        [SerializeField] private XRGrabInteractable padlock;

        private bool _syncing;

        private void OnEnable()
        {
            lockSocket.selectEntered.AddListener(OnHung);
            lockSocket.selectExited.AddListener(OnTaken);
        }

        private void OnDisable()
        {
            lockSocket.selectEntered.RemoveListener(OnHung);
            lockSocket.selectExited.RemoveListener(OnTaken);
        }

        private void OnHung(SelectEnterEventArgs args)
        {
            if (!_syncing && !Locked)
                point.Use();
        }

        private void OnTaken(SelectExitEventArgs args)
        {
            if (!_syncing && Locked)
                point.Use();
        }

        private void Update()
        {
            if (padlock.interactionManager == null)
                return;
            bool held = padlock.isSelected && !(padlock.firstInteractorSelecting is XRSocketInteractor);
            if (held)
                return;
            bool inHasp = lockSocket.hasSelection;
            if (Locked && !inHasp)
                Move(lockSocket);
            else if (!Locked && inHasp)
                Move(homeSocket);
        }

        private void Move(XRSocketInteractor socket)
        {
            _syncing = true;
            XRInteractionManager manager = padlock.interactionManager;
            if (padlock.isSelected)
                manager.SelectExit(padlock.firstInteractorSelecting, (IXRSelectInteractable)padlock);
            if (!socket.hasSelection)
                manager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)padlock);
            _syncing = false;
        }

        private bool Locked => context.Bus.TryGetLast(CellTopics.PressLockout, out SignalMessage m) && m.Value.AsNumber != 0d;
    }
}
