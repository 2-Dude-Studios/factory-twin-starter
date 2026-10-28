using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): keeps input alive while VR runs and the desktop window is in the background. The trainee is in
    /// the headset, so another window on the PC (Meta Quest Developer Hub, to record, for example) can take the focus.
    /// UI Toolkit then drops every pointer event, and the controller ray no longer pressed the tablet or the board;
    /// XRI works round that in the Editor only. On the XR rig, so it applies only while VR runs.
    /// </summary>
    public sealed class VrBackgroundInput : MonoBehaviour
    {
        private static readonly Func<bool> Always = () => true;

        private FieldInfo _focusHook;
        private object _focusHookBefore;
        private InputSettings.BackgroundBehavior _backgroundBefore;

        private void OnEnable()
        {
            _backgroundBefore = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            // UI Toolkit's DefaultEventSystem skips its update while the app is unfocused unless this internal hook
            // returns true; XRI sets the same hook in the Editor.
            _focusHook = typeof(UIDocument).Assembly.GetType("UnityEngine.UIElements.DefaultEventSystem")
                ?.GetField("IsEditorRemoteConnected", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (_focusHook != null && _focusHook.FieldType == typeof(Func<bool>))
            {
                _focusHookBefore = _focusHook.GetValue(null);
                _focusHook.SetValue(null, Always);
                Debug.Log("[VrBackgroundInput] Panels and controllers keep taking input while the window is in the background.");
            }
            else
            {
                _focusHook = null;
                Debug.LogWarning("[VrBackgroundInput] UI Toolkit's focus hook was not found: keep the Factory Twin window focused while in VR.");
            }
        }

        private void OnDisable()
        {
            InputSystem.settings.backgroundBehavior = _backgroundBefore;
            if (_focusHook != null)
                _focusHook.SetValue(null, _focusHookBefore);
        }

        private void OnApplicationFocus(bool focused)
        {
            Debug.Log($"[VrBackgroundInput] Window {(focused ? "focused" : "in the background")}.");
        }
    }
}
