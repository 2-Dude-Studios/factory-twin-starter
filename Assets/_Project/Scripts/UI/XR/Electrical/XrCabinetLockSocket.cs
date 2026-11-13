using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Electrical safety (M27, ahead of M20/M21): a hasp on the cabinet (Q1's, or the UPS feed breaker's) and the
    /// padlock that belongs to it. Hanging the padlock in the hasp sends lock_cmd 1 (ups_lock_cmd for the UPS), taking it
    /// out sends 0, as the dashboard and the tablet do. Whenever no hand holds the padlock it follows the cabinet's own
    /// lock count: a lock the rules refuse (the switch still ON: the handle locks in OFF only) goes back to its peg on
    /// the gear bench, and the tablet says why. The pattern of M12's <see cref="XrLockSocket"/>.
    /// </summary>
    public sealed class XrCabinetLockSocket : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private CabinetControl control;
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
                Send(1d);
        }

        private void OnTaken(SelectExitEventArgs args)
        {
            if (!_syncing && Locked)
                Send(0d);
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

        private bool Locked =>
            context.Bus.TryGetLast(control == CabinetControl.UpsBreaker ? CellTopics.CabinetUpsLocked : CellTopics.CabinetLocked, out SignalMessage m)
            && m.Value.AsNumber > 0d;

        private void Send(double value) =>
            context.Bus.Publish(control == CabinetControl.UpsBreaker ? CellTopics.CabinetUpsLockCommand : CellTopics.CabinetLockCommand,
                SignalValue.FromNumber(value), CellContext.NowUnixMs);
    }
}
