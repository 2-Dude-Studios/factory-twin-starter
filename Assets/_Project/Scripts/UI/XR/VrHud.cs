using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the headset HUD (VrHud.uxml), a small world-space panel that lazily follows the view. Three
    /// lines: safety or line status first, the lockout step due, and the prompt for the training point under the
    /// controller ray (set by XrTrainingPoint). Read from the bus; it changes nothing.
    /// </summary>
    public sealed class VrHud : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;

        private IDisposable _subscription;
        private Label _status, _step, _prompt;
        private SafetyState _safety;
        private CellState _line;
        private int _stepNumber;
        private TrainingOutcome _outcome;

        private void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
                return;
            _status = root.Q<Label>("status");
            _step = root.Q<Label>("step");
            _prompt = root.Q<Label>("prompt");

            SignalBus bus = context.Bus;
            foreach (TopicPath topic in new[] { CellTopics.SafetyState, CellTopics.LineState, CellTopics.TrainingStep, CellTopics.TrainingResult })
            {
                if (bus.TryGetLast(topic, out SignalMessage last))
                    Read(last);
            }
            _subscription = bus.Subscribe(TopicFilter.All, OnMessage);
            SetPrompt(string.Empty);
            Show();
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        /// <summary>The prompt for the training point under the ray; empty clears it.</summary>
        public void SetPrompt(string pointPrompt)
        {
            if (_prompt == null)
                return;
            _prompt.text = VrHudText.Prompt(pointPrompt);
            _prompt.EnableInClassList("hidden", string.IsNullOrEmpty(pointPrompt));
        }

        private void OnMessage(SignalMessage message)
        {
            if (Read(message))
                Show();
        }

        private bool Read(SignalMessage message)
        {
            TopicPath topic = message.Topic;
            int v = (int)message.Value.AsNumber;
            if (topic == CellTopics.SafetyState) _safety = (SafetyState)v;
            else if (topic == CellTopics.LineState) _line = (CellState)v;
            else if (topic == CellTopics.TrainingStep) _stepNumber = v;
            else if (topic == CellTopics.TrainingResult) _outcome = (TrainingOutcome)v;
            else return false;
            return true;
        }

        private void Show()
        {
            if (_status == null)
                return;
            _status.text = VrHudText.Status(_safety, _line);
            _status.EnableInClassList("hud__status--alert", _safety == SafetyState.Stopped || _safety == SafetyState.AwaitingReset);
            string step = VrHudText.Step(_stepNumber, _outcome);
            _step.text = step;
            _step.EnableInClassList("hidden", string.IsNullOrEmpty(step));
        }
    }
}
