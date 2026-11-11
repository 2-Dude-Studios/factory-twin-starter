using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training areas (M24): runs the robot cell safety drill in the scene. cell/training/robot_level starts a level
    /// (0 ends it). It tells the pure <see cref="RobotCellDrill"/> what the cell's signals say (the two zone sensors,
    /// the latched stop, presses of reset, line stop and line start), and carries out what the drill asks: clearing
    /// the part at the gate (cell/gate1/clear) and sending the colleague into the cell and home again
    /// (cell/safety/worker_walk). When a level ends it writes the record, adds the stars, points and badges to the
    /// trainee's badge book and publishes cell/training/robot_stars. The robot area's score board shows the run.
    /// </summary>
    public sealed class RobotCellScorer : MonoBehaviour, IDrillLive
    {
        [SerializeField] private CellContext context;
        [SerializeField] private string traineeName = "Sam Rivera";
        [SerializeField] private string trainerName = "Demo trainer";

        [Header("Status (play mode, read-only)")]
        [SerializeField] private int level;
        [SerializeField] private int stepsDone;
        [SerializeField] private int points;
        [SerializeField] private int stars;
        [SerializeField] private string lastRecord;

        private readonly RobotCellDrill _drill = new RobotCellDrill();
        private readonly StringBuilder _text = new StringBuilder(512);
        private IDisposable[] _subscriptions = Array.Empty<IDisposable>();
        private SignalBus _bus;
        private DateTime _startedUtc;
        private bool _closed = true;
        private double _gateClearOffAt = -1d;
        private int _liveKey = -1;
        private int _publishedSteps = -1;
        private string _liveText = string.Empty;

        public bool Running => _drill.Running;
        public int LiveVersion { get; private set; }
        public string LiveText => _liveText;

        private static double Now => Time.timeAsDouble;

        private void OnEnable()
        {
            if (context == null)
            {
                Debug.LogError("[RobotCellScorer] Assign Context (Cell).", this);
                enabled = false;
                return;
            }
            _bus = context.Bus;
            _subscriptions = new[]
            {
                _bus.Subscribe(CellTopics.TrainingRobotLevel.ToString(), OnLevel),
                _bus.Subscribe(CellTopics.SafetyZoneWarning.ToString(), m => _drill.ZoneWarning(m.Value.AsNumber != 0d, Now)),
                _bus.Subscribe(CellTopics.SafetyArmZoneOccupied.ToString(), m => _drill.ProtectiveZone(m.Value.AsNumber != 0d, Now)),
                _bus.Subscribe(CellTopics.SafetyProtectiveStop.ToString(), m => _drill.ProtectiveStop(m.Value.AsNumber != 0d, Now)),
                _bus.Subscribe(CellTopics.SafetyReset.ToString(), m => { if (m.Value.AsNumber != 0d) _drill.ResetPressed(Now); }),
                _bus.Subscribe(CellTopics.LineStop.ToString(), m => { if (m.Value.AsNumber != 0d) _drill.LineStopPressed(Now); }),
                _bus.Subscribe(CellTopics.LineStart.ToString(), m => { if (m.Value.AsNumber != 0d) _drill.LineStartPressed(Now); }),
            };
        }

        private void OnDisable()
        {
            foreach (IDisposable subscription in _subscriptions)
                subscription?.Dispose();
            _subscriptions = Array.Empty<IDisposable>();
        }

        /// <summary>Starts a level (1 to 4) or ends the one running (0), as cell/training/robot_level does.</summary>
        public void StartLevel(int wanted) => Publish(CellTopics.TrainingRobotLevel, wanted);

        private void OnLevel(SignalMessage message)
        {
            int wanted = (int)message.Value.AsNumber;
            if (wanted <= 0)
            {
                _drill.End(Now);
                return;
            }
            if (_drill.Running)
                _drill.End(Now);
            Close();
            // Where the cell stands now: the sensors' last word, so a level started with someone in a zone knows it.
            _drill.ZoneWarning(Read(CellTopics.SafetyZoneWarning), Now);
            _drill.ProtectiveZone(Read(CellTopics.SafetyArmZoneOccupied), Now);
            _drill.ProtectiveStop(Read(CellTopics.SafetyProtectiveStop), Now);
            _drill.Start(wanted, Now, Environment.TickCount, _drill.LineStopped);
            _startedUtc = DateTime.UtcNow;
            _closed = false;
            level = _drill.Level;
            stars = 0;
            _liveKey = -1;
            Debug.Log($"[RobotCellScorer] Robot cell safety level {level} {DrillLevels.LevelName(level)} started.", this);
        }

        private void Update()
        {
            if (_gateClearOffAt >= 0d && Now >= _gateClearOffAt)
            {
                _gateClearOffAt = -1d;
                Publish(CellTopics.GateClear, 0d);
            }
            if (_closed)
                return;

            _drill.Tick(Now);
            if (_drill.TakeClearGate())
            {
                // A press, as the commissioning suite gives it: true, then false a moment later.
                Publish(CellTopics.GateClear, 1d);
                _gateClearOffAt = Now + 0.5d;
            }
            int walk = _drill.TakeWalkerCommand();
            if (walk >= 0)
                Publish(CellTopics.SafetyWorkerWalk, walk);

            DrillScore score = _drill.Score;
            stepsDone = score.StepsDone;
            points = score.Points;
            if (stepsDone != _publishedSteps)
            {
                _publishedSteps = stepsDone;
                Publish(CellTopics.TrainingRobotStep, stepsDone);
            }
            if (_drill.Running)
                ShowLive(score);
            else
                Close();
        }

        // The run on the board. Rebuilt only when something on it changes (a step, a mistake, the countdown's second).
        private void ShowLive(DrillScore score)
        {
            int left = (int)Math.Ceiling(_drill.ClearSecondsLeft);
            int key = score.Events.Count * 8 + left;
            if (key == _liveKey)
                return;
            _liveKey = key;
            _text.Clear();
            _text.Append("LEVEL ").Append(score.Level).Append("  ").Append(DrillLevels.LevelName(score.Level).ToUpperInvariant()).Append('\n');
            _text.Append(score.Points).Append(" points   ").Append(score.StepsDone).Append(" of ").Append(score.StepsRequired).Append(" steps");
            if (score.Mistakes + score.Criticals > 0)
                _text.Append("   mistakes ").Append(score.Mistakes + score.Criticals);
            _text.Append('\n').Append('\n');
            if (_drill.Guided)
            {
                foreach (DrillStep step in score.Required)
                    _text.Append(score.IsDone(step.Id) ? "[x] " : "[ ] ").Append(step.Text).Append('\n');
            }
            else
            {
                _text.Append("Assessment: no hints.\n");
            }
            if (left > 0)
                _text.Append('\n').Append("Clearing the part: ").Append(left).Append(" s\n");
            if (score.Criticals > 0)
                _text.Append('\n').Append("A critical mistake: this run will not pass.\n");
            _liveText = _text.ToString();
            LiveVersion++;
        }

        // The level is over: the record, the badge book and the stars, once.
        private void Close()
        {
            if (_closed)
                return;
            _closed = true;
            DrillScore score = _drill.Score;
            if (score == null || score.Result == DrillResult.NotStarted)
                return;
            // A level ended by the trainer may have left the colleague in the cell.
            int walk = _drill.TakeWalkerCommand();
            if (walk >= 0)
                Publish(CellTopics.SafetyWorkerWalk, walk);

            stars = score.Stars;
            points = score.Points;
            string title = TrainingAreaDefaults.For(TrainingArea.RobotCell).Title;
            try
            {
                string folder = BadgeBook.Folder();
                Directory.CreateDirectory(folder);
                lastRecord = Path.Combine(folder, DrillRecord.FileName(TrainingArea.RobotCell, traineeName, _startedUtc));
                File.WriteAllText(lastRecord, DrillRecord.ToCsv(traineeName, trainerName, _startedUtc, score, title));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RobotCellScorer] Could not write the record: {e.Message}", this);
            }
            BadgeBook book = BadgeBook.Load(traineeName);
            List<string> fresh = book.RecordLevel(TrainingMode.RobotZone, score.Level, score.Stars, score.Points, score.Badges, DateTime.UtcNow);
            book.Save();
            Publish(CellTopics.TrainingRobotStars, score.Stars);
            Debug.Log($"[RobotCellScorer] {DrillRecord.Summary(score, title)}" +
                      $"{(fresh.Count > 0 ? ". New badges: " + string.Join(", ", fresh) : string.Empty)}. Record: {lastRecord}", this);
        }

        private bool Read(TopicPath topic) => _bus.TryGetLast(topic, out SignalMessage m) && m.Value.AsNumber != 0d;

        private void Publish(TopicPath topic, double value) =>
            _bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);
    }
}
