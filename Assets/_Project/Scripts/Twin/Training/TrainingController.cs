using System;
using System.IO;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training: runs the lockout procedure (M11). cell/training/run = 1 starts a run for the trainee. Each
    /// cell/training/action is checked by <see cref="LockoutProcedure"/>; the step due is carried out on the cell
    /// through the same commands everything else uses, and so is Start, always, so the logic source's own
    /// interlock is what refuses a start with the lock on. Any other out-of-order action is held back and counted.
    /// Publishes cell/training/step, errors and result, and writes the training record when a run completes.
    /// A training demo, not certified training.
    /// </summary>
    public sealed class TrainingController : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("Trainee written into the training record (set 2026-10-25).")]
        [SerializeField] private string traineeName = "Apoorv Garg";
        [Tooltip("Trainer who supervises the run, written into the training record (set 2026-10-25).")]
        [SerializeField] private string trainerName = "Akaash Garg";

        [Header("Status (play mode, read-only)")]
        [SerializeField] private TrainingOutcome result;
        [SerializeField] private int step;
        [SerializeField] private int errors;
        [SerializeField] private string expected;
        [SerializeField] private string lastRecord;

        private readonly LockoutProcedure _run = new LockoutProcedure();
        private IDisposable _runSubscription;
        private IDisposable _actionSubscription;
        private double _startedAt;

        public LockoutProcedure Run => _run;
        public string TraineeName => traineeName;
        public string TrainerName => trainerName;

        /// <summary>Raised with the record's path when a run completes.</summary>
        public event Action<string> RecordWritten;

        private void OnEnable()
        {
            SignalBus bus = context.Bus;
            _runSubscription = bus.Subscribe(CellTopics.TrainingRun.ToString(), OnRun);
            _actionSubscription = bus.Subscribe(CellTopics.TrainingAction.ToString(), OnAction);
        }

        private void OnDisable()
        {
            _runSubscription?.Dispose();
            _runSubscription = null;
            _actionSubscription?.Dispose();
            _actionSubscription = null;
        }

        private void Start()
        {
            PublishProgress();
        }

        /// <summary>Starts a new run, as cell/training/run = 1 does.</summary>
        public void Begin()
        {
            _run.Begin(traineeName, CellContext.NowUnixMs, trainerName);
            _startedAt = Time.timeAsDouble;
            PublishProgress();
        }

        private void OnRun(SignalMessage message)
        {
            if (message.Value.AsNumber != 0d)
                Begin();
        }

        private void OnAction(SignalMessage message)
        {
            var action = (LockoutAction)(int)message.Value.AsNumber;
            if (!_run.IsRunning || action == LockoutAction.None)
                return;

            bool due = _run.Attempt(action, Time.timeAsDouble - _startedAt);
            if (!due)
                Command(CellTopics.TrainingMistake, (int)action);
            if (due || LockoutProcedure.AlwaysReachesThePlant(action))
                CarryOut(action);

            PublishProgress();
            if (_run.IsComplete)
                WriteRecord();
        }

        // Each action becomes the cell command it stands for. Notify, release energy, inspect and clear-and-notify
        // are things the trainee does with people and hands: the twin records them and moves nothing.
        private void CarryOut(LockoutAction action)
        {
            switch (action)
            {
                case LockoutAction.Stop: Command(CellTopics.LineStop, 1d); break;
                case LockoutAction.Start: Command(CellTopics.LineStart, 1d); break;
                case LockoutAction.IsolatorOff: Command(CellTopics.PressIsolatorCommand, 1d); break;
                case LockoutAction.IsolatorOn: Command(CellTopics.PressIsolatorCommand, 0d); break;
                case LockoutAction.ApplyLock: Command(CellTopics.PressLockCommand, 1d); break;
                case LockoutAction.RemoveLock: Command(CellTopics.PressLockCommand, 0d); break;
                case LockoutAction.ChangeTool: Command(CellTopics.PressToolChange, 1d); break;
            }
        }

        private void Command(TopicPath topic, double value)
        {
            context.Bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);
        }

        private void PublishProgress()
        {
            result = _run.Result;
            step = _run.StepNumber;
            errors = _run.Errors;
            expected = _run.Expected?.Title ?? string.Empty;

            long now = CellContext.NowUnixMs;
            SignalBus bus = context.Bus;
            bus.Publish(CellTopics.TrainingStep, SignalValue.FromNumber(step), now);
            bus.Publish(CellTopics.TrainingErrors, SignalValue.FromNumber(errors), now);
            bus.Publish(CellTopics.TrainingResult, SignalValue.FromNumber((int)result), now);
        }

        private void WriteRecord()
        {
            string folder = Application.isEditor
                ? Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Builds", "Training")
                : Path.Combine(Application.persistentDataPath, "Training");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, $"lockout_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            File.WriteAllText(file, TrainingRecord.ToCsv(_run));
            lastRecord = file;
            Debug.Log($"[TrainingController] Lockout {TrainingRecord.ResultWord(_run.Result)}: {_run.Errors} mistake(s). Record: {file}", this);
            RecordWritten?.Invoke(file);
        }
    }
}
