using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the lockout board above the line control station, a world-space UI Toolkit document
    /// (TrainingBoard.uxml). It shows in the headset what the screen-only dashboard cannot: who is training, the step
    /// due with its paragraph of 1910.147, mistakes and the result. Read from the bus; it changes nothing.
    /// </summary>
    public sealed class TrainingBoard : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;
        [SerializeField] private TrainingController training;

        private IDisposable _subscription;
        private Label _who, _result, _step, _next, _mistakes, _status;
        private int _stepNumber;
        private int _errors;
        private TrainingOutcome _outcome;
        private SafetyState _safety;
        private CellState _line;

        private void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
                return;
            _who = root.Q<Label>("who");
            _result = root.Q<Label>("result");
            _step = root.Q<Label>("step");
            _next = root.Q<Label>("next");
            _mistakes = root.Q<Label>("mistakes");
            _status = root.Q<Label>("status");
            if (_who != null && training != null)
                _who.text = $"Trainee {training.TraineeName} · Trainer {training.TrainerName}";

            SignalBus bus = context.Bus;
            foreach (TopicPath topic in new[] { CellTopics.TrainingStep, CellTopics.TrainingErrors, CellTopics.TrainingResult, CellTopics.SafetyState, CellTopics.LineState })
            {
                if (bus.TryGetLast(topic, out SignalMessage last))
                    Read(last);
            }
            _subscription = bus.Subscribe(TopicFilter.All, OnMessage);
            Show();
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
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
            if (topic == CellTopics.TrainingStep) _stepNumber = v;
            else if (topic == CellTopics.TrainingErrors) _errors = v;
            else if (topic == CellTopics.TrainingResult) _outcome = (TrainingOutcome)v;
            else if (topic == CellTopics.SafetyState) _safety = (SafetyState)v;
            else if (topic == CellTopics.LineState) _line = (CellState)v;
            else return false;
            return true;
        }

        private void Show()
        {
            if (_result == null)
                return;
            _result.text = DashboardViewModel.TrainingResultWords(_outcome);
            _result.EnableInClassList("board__chip--ok", _outcome == TrainingOutcome.Passed);
            _result.EnableInClassList("board__chip--warn", _outcome == TrainingOutcome.PassedWithErrors);
            _step.text = _outcome == TrainingOutcome.Idle ? "Press Lockout training to start a run" : DashboardViewModel.TrainingStepWords(_stepNumber, _outcome);
            _next.text = DashboardViewModel.TrainingNextWords(_stepNumber, _outcome);
            _mistakes.text = _outcome == TrainingOutcome.Idle ? string.Empty : DashboardViewModel.MistakeWords(_errors);
            _mistakes.EnableInClassList("board__mistakes--some", _errors > 0);
            _status.text = VrHudText.Status(_safety, _line);
            _status.EnableInClassList("board__status--alert", _safety == SafetyState.Stopped || _safety == SafetyState.AwaitingReset);
        }
    }
}
