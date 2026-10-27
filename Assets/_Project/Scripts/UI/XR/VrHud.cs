using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the headset HUD (VrHud.uxml), a small world-space panel held at the top left of the view.
    /// It leads with the task at hand, where to do it, and the next one, then safety or line status, then the prompt
    /// for the training point under the controller ray (set by XrTrainingPoint). When an action is refused it says, for
    /// a few seconds, what is due instead. Read from the bus; it changes nothing.
    /// </summary>
    public sealed class VrHud : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;
        [Tooltip("How long the HUD says what is due after an action is refused.")]
        [SerializeField] private float refusalSeconds = 5f;

        private IDisposable _subscription;
        private Label _nowLabel, _current, _where, _refusal, _next, _status, _prompt;
        private float _refusalUntil;
        private VisualElement _nextRow;
        private SafetyState _safety;
        private CellState _line;
        private int _stepNumber;
        private TrainingOutcome _outcome;

        private void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
                return;
            _nowLabel = root.Q<Label>("now-label");
            _current = root.Q<Label>("current");
            _where = root.Q<Label>("where");
            _refusal = root.Q<Label>("refusal");
            _nextRow = root.Q("next-row");
            _next = root.Q<Label>("next");
            _status = root.Q<Label>("status");
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

        /// <summary>The prompt for the training point under a controller, with how to use it; empty clears it.</summary>
        public void SetPrompt(string pointPrompt, string how = "pull the trigger")
        {
            if (_prompt == null)
                return;
            _prompt.text = VrHudText.Prompt(pointPrompt, how);
            _prompt.EnableInClassList("hidden", string.IsNullOrEmpty(pointPrompt));
        }

        private void OnMessage(SignalMessage message)
        {
            // The step due has not moved yet when a refusal arrives, so it names the step still to do.
            if (message.Topic == CellTopics.TrainingMistake && _refusal != null)
            {
                string text = VrHudText.Refusal((LockoutAction)(int)message.Value.AsNumber, _stepNumber, _outcome);
                _refusal.text = text;
                _refusal.EnableInClassList("hidden", string.IsNullOrEmpty(text));
                _refusalUntil = Time.time + refusalSeconds;
                return;
            }
            // The right step done: the hint has served its purpose.
            if (message.Topic == CellTopics.TrainingStep && _refusalUntil > 0f)
                _refusalUntil = Time.time;
            if (Read(message))
                Show();
        }

        private void Update()
        {
            if (_refusalUntil > 0f && Time.time >= _refusalUntil)
            {
                _refusalUntil = 0f;
                _refusal.EnableInClassList("hidden", true);
            }
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
            _nowLabel.text = VrHudText.NowLabel(_stepNumber, _outcome);
            _current.text = VrHudText.Current(_stepNumber, _outcome);
            string where = VrHudText.Where(_stepNumber, _outcome);
            _where.text = where;
            _where.EnableInClassList("hidden", string.IsNullOrEmpty(where));
            string next = VrHudText.Next(_stepNumber, _outcome);
            _next.text = next;
            _nextRow.EnableInClassList("hidden", string.IsNullOrEmpty(next));
            _status.text = VrHudText.Status(_safety, _line);
            _status.EnableInClassList("hud__status--alert", _safety == SafetyState.Stopped || _safety == SafetyState.AwaitingReset);
        }
    }
}
