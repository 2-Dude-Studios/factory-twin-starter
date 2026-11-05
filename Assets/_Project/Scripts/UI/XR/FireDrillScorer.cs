using System;
using System.Collections.Generic;
using System.IO;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): scores the fire drill as it happens. Reads the fire's signals from the bus and the
    /// extinguisher in use (pin, hose, discharge, aim, stand-off) into a <see cref="FireDrillScore"/>; raises
    /// <see cref="Scored"/> for each point and badge (the popups and the board show them). Ending the session writes
    /// the training record and adds the score and badges to the trainee's badge book.
    /// </summary>
    public sealed class FireDrillScorer : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private FireExtinguisher extinguisher;
        [Tooltip("The headset's heat warning: stepping inside the fire's danger zone costs points.")]
        [SerializeField] private FireHeatWarning heat;
        [Tooltip("The drill's fires, to know which are flee-only.")]
        [SerializeField] private FireScenario[] scenarios = Array.Empty<FireScenario>();
        [SerializeField] private string traineeName = "Sam Rivera";
        [SerializeField] private string trainerName = "Demo trainer";

        [Header("Status (play mode, read-only)")]
        [SerializeField] private int total;
        [SerializeField] private int runs;
        [SerializeField] private string lastRecord;

        private FireDrillScore _score = new FireDrillScore();
        private IDisposable _subscription;
        private float _startedAt;
        private DateTime _startedUtc;
        private bool _sessionOpen;
        private bool _pin, _hose, _tooClose;
        private FireClass _class;

        public FireDrillScore Score => _score;
        public string TraineeName => traineeName;

        /// <summary>Each point or badge as it is scored.</summary>
        public event Action<ScoreEvent> Scored;

        /// <summary>The session's badges that are new for this trainee, when the session ends.</summary>
        public event Action<IReadOnlyList<string>> SessionEnded;

        /// <summary>Seconds into the session (the score's clock).</summary>
        public double Now => Time.time - _startedAt;

        public bool SessionOpen => _sessionOpen;

        private void OnEnable()
        {
            if (context == null)
            {
                enabled = false;
                return;
            }
            _subscription = context.Bus.Subscribe("cell/fire/+", OnFire);
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
            EndSession();
        }

        /// <summary>A fresh session (the drill page opening).</summary>
        public void BeginSession()
        {
            _score = new FireDrillScore();
            _startedAt = Time.time;
            _startedUtc = DateTime.UtcNow;
            _sessionOpen = true;
            Mirror();
        }

        /// <summary>Writes the record and the badge book once; nothing if no fire was started.</summary>
        public void EndSession()
        {
            if (!_sessionOpen)
                return;
            _sessionOpen = false;
            _score.FinishRun(Now);
            Flush();
            if (_score.Runs == 0)
                return;
            WriteRecord();
            BadgeBook book = BadgeBook.Load(traineeName);
            List<string> fresh = book.Record(TrainingMode.FireSafety, _score.Total, _score.Badges);
            book.Save();
            SessionEnded?.Invoke(fresh);
        }

        private void Update()
        {
            if (!_sessionOpen || !_score.Running)
                return;
            // Each step into the danger zone (not each frame inside it) costs points.
            bool tooClose = heat != null && heat.isActiveAndEnabled && heat.TooClose;
            if (tooClose && !_tooClose)
                _score.TooClose(Now);
            _tooClose = tooClose;
            if (extinguisher == null)
            {
                Flush();
                return;
            }
            if (extinguisher.PinPulled && !_pin)
                _score.PinPulled(Now);
            if (extinguisher.HoseHeld && !_hose)
                _score.HoseTaken(Now);
            _pin = extinguisher.PinPulled;
            _hose = extinguisher.HoseHeld;
            if (extinguisher.Discharging)
                _score.Discharging(extinguisher.Agent, extinguisher.Aim, extinguisher.Standoff, Time.deltaTime, Now);
            Flush();
        }

        private void OnFire(SignalMessage message)
        {
            if (!_sessionOpen)
                return;
            TopicPath topic = message.Topic;
            double v = message.Value.AsNumber;
            if (topic == CellTopics.FireClass)
                _class = (FireClass)(int)v;
            else if (topic == CellTopics.FireLocation)
            {
                var at = (FireLocation)(int)v;
                if (at == FireLocation.None)
                    _score.FinishRun(Now);
                else
                {
                    _score.StartRun(_class, FleeOnly(at), Now);
                    _pin = _hose = false;
                }
            }
            else if (topic == CellTopics.FireStage)
                _score.StageChanged((FireStage)(int)v, Now);
            else if (topic == CellTopics.FireAlarm && v != 0d)
                _score.AlarmRaised(Now);
            else if (topic == CellTopics.FireExtinguished && v != 0d)
                _score.Extinguished(Now);
            else
                return;
            Flush();
        }

        private void Flush()
        {
            foreach (ScoreEvent e in _score.TakeNew())
                Scored?.Invoke(e);
            Mirror();
        }

        private void Mirror()
        {
            total = _score.Total;
            runs = _score.Runs;
        }

        private bool FleeOnly(FireLocation at)
        {
            foreach (FireScenario s in scenarios)
            {
                if (s != null && s.Location == at)
                    return s.FleeOnly;
            }
            return false;
        }

        private void WriteRecord()
        {
            try
            {
                string folder = BadgeBook.Folder();
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, $"fire_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                File.WriteAllText(file, FireDrillRecord.ToCsv(traineeName, trainerName, _startedUtc, _score));
                lastRecord = file;
                Debug.Log($"[FireDrillScorer] Fire drill: {_score.Total} points, {_score.Badges.Count} badges. Record: {file}", this);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FireDrillScorer] Could not write the record: {e.Message}", this);
            }
        }
    }
}
