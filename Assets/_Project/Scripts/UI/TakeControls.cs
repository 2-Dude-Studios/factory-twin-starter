using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Capture tools: keyboard shortcuts for recording takes, so the demo can be driven without the cursor on screen.
    /// 1 Wear tool out · 2 Change tool · 3 Clear · 0 fresh take · F6 safety reset · F7 line start · F8 line stop ·
    /// F9 FPS readout · F10 show or hide the cursor.
    /// Number and function keys only: realvirtual's own hotkeys use letters, Tab, Esc, Home/End/Insert, F1, F12 and
    /// the keypad. Every action goes out as the same bus command the dashboard buttons send.
    /// </summary>
    public sealed class TakeControls : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private FaultInjector faultInjector;
        [SerializeField] private FpsReadout fpsReadout;
        [Tooltip("Hide the cursor when Play starts (F10 shows it again).")]
        [SerializeField] private bool hideCursorOnStart;

        private void Start()
        {
            if (hideCursorOnStart)
                Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.visible = true;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                Send(CellTopics.PressDegrade);
            if (keyboard.digit2Key.wasPressedThisFrame)
                Send(CellTopics.PressToolChange);
            if (keyboard.digit3Key.wasPressedThisFrame)
                faultInjector.Clear();
            if (keyboard.digit0Key.wasPressedThisFrame)
                FreshTake();
            if (keyboard.f9Key.wasPressedThisFrame && fpsReadout != null)
                fpsReadout.Visible = !fpsReadout.Visible;
            if (keyboard.f10Key.wasPressedThisFrame)
                Cursor.visible = !Cursor.visible;
            if (keyboard.f6Key.wasPressedThisFrame)
                Send(CellTopics.SafetyReset);
            if (keyboard.f7Key.wasPressedThisFrame)
                Send(CellTopics.LineStart);
            if (keyboard.f8Key.wasPressedThisFrame)
                Send(CellTopics.LineStop);
        }

        /// <summary>
        /// Starts a new take without restarting Play: faults cleared, degradation stopped, a fresh tool, then the
        /// fresh-take command, on which parts lost goes back to 0 and the event log empties. The line keeps running,
        /// so parts/hour stays at its running rate.
        /// </summary>
        public void FreshTake()
        {
            faultInjector.Clear();
            Send(CellTopics.PressToolChange);
            Send(CellTopics.DemoFreshTake);
        }

        private void Send(TopicPath command)
        {
            context.Bus.Publish(command, SignalValue.FromBool(true), CellContext.NowUnixMs);
        }
    }
}
