using System;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub (M29): the comfort options on the left-hand tablet, so they can be changed mid-drill as well as at
    /// the hub board. A Comfort button on every tablet page opens the comfort page (teleport or smooth movement, snap or
    /// smooth turning, the vignette, the trainer's voice); Back returns to the page it was opened over. The same
    /// <see cref="ComfortSettings"/> as the hub's comfort page, so both always agree.
    /// </summary>
    public sealed class VrComfortMenu : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private ComfortSettings comfort;
        [SerializeField] private CellContext context;

        private static readonly string[] OpenButtons = { "main-comfort", "robot-comfort", "elec-comfort", "fire-comfort" };
        private VisualElement _root;
        private readonly List<(Button button, Action action)> _wired = new List<(Button, Action)>();
        private IDisposable _hubSubscription;

        private void OnEnable()
        {
            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
                return;
            foreach (string name in OpenButtons)
                Wire(name, () => TabletPages.OpenComfort(_root));
            Wire("tablet-comfort-back", () => TabletPages.CloseComfort(_root));
            if (comfort != null)
            {
                Wire("tablet-comfort-move", comfort.ToggleSmoothMove);
                Wire("tablet-comfort-turn", comfort.ToggleSmoothTurn);
                Wire("tablet-comfort-vignette", comfort.ToggleVignette);
                Wire("tablet-comfort-voice", comfort.ToggleVoice);
                comfort.Changed += Refresh;
            }
            if (context != null)
                _hubSubscription = context.Bus.Subscribe(CellTopics.TrainingHub.ToString(), _ => TabletPages.CloseComfort(_root));
            Refresh();
            // A rebuilt tablet with the comfort page open: show it again.
            if (TabletPages.ComfortOver != null)
                TabletPages.ShowOnly(_root, TabletPages.ComfortOver, "hud");
        }

        private void OnDisable()
        {
            foreach ((Button button, Action action) in _wired)
                button.clicked -= action;
            _wired.Clear();
            if (comfort != null)
                comfort.Changed -= Refresh;
            _hubSubscription?.Dispose();
            _hubSubscription = null;
        }

        private void Update()
        {
            // The tablet's UI was rebuilt (the panel woke again): wire the new one.
            if (document != null && document.rootVisualElement != _root)
            {
                OnDisable();
                OnEnable();
            }
        }

        private void Wire(string name, Action action)
        {
            Button button = _root.Q<Button>(name);
            if (button == null)
                return;
            button.clicked += action;
            _wired.Add((button, action));
        }

        private void Refresh()
        {
            if (_root == null || comfort == null)
                return;
            SetText("tablet-comfort-move", comfort.SmoothMove ? "Movement: smooth + teleport" : "Movement: teleport only");
            SetText("tablet-comfort-turn", comfort.SmoothTurn ? "Turning: smooth" : "Turning: snap 45°");
            SetText("tablet-comfort-vignette", comfort.Vignette ? "Vignette: on" : "Vignette: off");
            SetText("tablet-comfort-voice", ComfortSettings.VoiceOn ? "Trainer voice: on" : "Trainer voice: off");
        }

        private void SetText(string name, string text)
        {
            Button button = _root.Q<Button>(name);
            if (button != null)
                button.text = text;
        }
    }
}
