using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the lockout board above the line control station, a world-space UI Toolkit document
    /// (TrainingBoard.uxml). It shows in the headset what the screen-only dashboard cannot: who is training, the step
    /// due with its paragraph of 1910.147, a bar of the 12 steps, mistakes and the result. Its buttons, pressed with
    /// a controller ray, start a run, fail the CNC and reset the safety zone through the same bus commands as the
    /// dashboard (TrainingCommands).
    /// </summary>
    public sealed class TrainingBoard : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;
        [SerializeField] private TrainingController training;

        private IDisposable _subscription;
        private VisualElement _root;
        private Label _who, _result, _step, _next, _mistakes, _status;
        private VisualElement[] _segments = Array.Empty<VisualElement>();
        private Action _startTraining, _failCnc, _safetyReset;
        private int _stepNumber;
        private int _errors;
        private TrainingOutcome _outcome;
        private SafetyState _safety;
        private CellState _line;

        private void OnEnable()
        {
            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
                return;
            VisualElement root = _root;
            _who = root.Q<Label>("who");
            _result = root.Q<Label>("result");
            _step = root.Q<Label>("step");
            _next = root.Q<Label>("next");
            _mistakes = root.Q<Label>("mistakes");
            _status = root.Q<Label>("status");
            BuildProgress(root.Q("progress"));
            if (_who != null && training != null)
                _who.text = $"Trainee {training.TraineeName} · Trainer {training.TrainerName}";

            SignalBus bus = context.Bus;
            foreach (TopicPath topic in new[] { CellTopics.TrainingStep, CellTopics.TrainingErrors, CellTopics.TrainingResult, CellTopics.SafetyState, CellTopics.LineState })
            {
                if (bus.TryGetLast(topic, out SignalMessage last))
                    Read(last);
            }
            _subscription = bus.Subscribe(TopicFilter.All, OnMessage);

            _startTraining ??= () => TrainingCommands.StartTraining(context.Bus);
            _failCnc ??= () => TrainingCommands.FailCnc(context.Bus);
            _safetyReset ??= () => TrainingCommands.SafetyReset(context.Bus);
            Wire("board-training", _startTraining, true);
            Wire("board-fail", _failCnc, true);
            Wire("board-reset", _safetyReset, true);
            Show();
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
            if (_root == null)
                return;
            Wire("board-training", _startTraining, false);
            Wire("board-fail", _failCnc, false);
            Wire("board-reset", _safetyReset, false);
        }

        // One segment per lockout step.
        private void BuildProgress(VisualElement progress)
        {
            if (progress == null)
                return;
            progress.Clear();
            _segments = new VisualElement[LockoutProcedure.Steps.Count];
            for (int i = 0; i < _segments.Length; i++)
            {
                var segment = new VisualElement { pickingMode = PickingMode.Ignore };
                segment.AddToClassList("board__seg");
                progress.Add(segment);
                _segments[i] = segment;
            }
        }

        private void Wire(string buttonName, Action handler, bool wire)
        {
            Button button = _root.Q<Button>(buttonName);
            if (button == null)
                return;
            if (wire)
                button.clicked += handler;
            else
                button.clicked -= handler;
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

            bool finished = _outcome == TrainingOutcome.Passed || _outcome == TrainingOutcome.PassedWithErrors;
            for (int i = 0; i < _segments.Length; i++)
            {
                int number = i + 1;
                bool running = _outcome == TrainingOutcome.InProgress;
                _segments[i].EnableInClassList("board__seg--done", finished || (running && number < _stepNumber));
                _segments[i].EnableInClassList("board__seg--now", running && number == _stepNumber);
            }
        }
    }
}
