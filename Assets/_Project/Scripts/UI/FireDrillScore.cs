using System;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Fire safety (M15): one thing the trainee earned or lost points for, with the rule it follows.</summary>
    public readonly struct ScoreEvent
    {
        public ScoreEvent(double atSeconds, int points, string text, string regulation, bool mistake, string badge = null)
        {
            AtSeconds = atSeconds;
            Points = points;
            Text = text;
            Regulation = regulation;
            Mistake = mistake;
            Badge = badge;
        }

        public double AtSeconds { get; }
        public int Points { get; }
        public string Text { get; }
        public string Regulation { get; }
        public bool Mistake { get; }

        /// <summary>A badge this event awards, or null.</summary>
        public string Badge { get; }
    }

    /// <summary>
    /// Fire safety (M15): scores a fire drill, pure and tested. Points for each right step (alarm first, the right
    /// extinguisher for the class, pin, hose, aim at the base, the right distance, the knockdown, no reflash, knowing
    /// when to leave), points off for the mistakes, badges for the runs that earn them. One run per fire; the
    /// session adds them up and awards the badges that need several runs (all three classes put out).
    /// </summary>
    public sealed class FireDrillScore
    {
        public const string BadgeAlarmFirst = "Alarm first";
        public const string BadgeRightClass = "Right class";
        public const string BadgeCleanKnockdown = "Clean knockdown";
        public const string BadgeSharpAim = "Sharp aim";
        public const string BadgeKnewWhenToLeave = "Knew when to leave";
        public const string BadgePerfectDrill = "Perfect drill";
        public const string BadgeFireMarshal = "Fire marshal";

        public const int PerfectRunPoints = 500;
        public const int TooClosePoints = -30;
        public const int TooCloseMaxPerRun = 3;
        public const double FastResponseSeconds = 25d;

        private readonly List<ScoreEvent> _events = new List<ScoreEvent>();
        private readonly HashSet<string> _badges = new HashSet<string>();
        private readonly HashSet<FireClass> _classesPutOut = new HashSet<FireClass>();
        private int _pendingFrom;

        // The run in progress.
        private bool _running;
        private FireClass _class;
        private bool _fleeOnly;
        private FireStage _stage;
        private double _ignitedAt = -1d;
        private bool _alarm, _discharged, _pin, _hose, _knockedDown, _foughtBeyond, _judgedAgent, _mistake, _reflashed;
        private bool _rightAgent, _out;
        private int _tooClose;
        private double _dischargeSeconds, _aimSum, _standoffSum;
        private int _runStartIndex;

        public int Total { get; private set; }
        public int RunPoints { get; private set; }
        public int Runs { get; private set; }
        public IReadOnlyList<ScoreEvent> Events => _events;
        public IReadOnlyCollection<string> Badges => _badges;
        public bool Running => _running;

        // The run's checklist, for the HUD.
        public bool AlarmDone => _alarm;
        public bool RightAgentDone => _rightAgent;
        public bool PinDone => _pin;
        public bool HoseDone => _hose;
        public bool DischargeDone => _discharged;
        public bool KnockedDownDone => _knockedDown;
        public bool OutDone => _out;
        public bool FleeOnly => _fleeOnly;

        /// <summary>Times this run the trainee stepped inside the fire's danger zone.</summary>
        public int TooCloseCount => _tooClose;

        /// <summary>Seconds since the flame took hold in this run, or a negative number before it.</summary>
        public double SinceIgnition(double t) => _ignitedAt < 0d ? -1d : t - _ignitedAt;

        /// <summary>Events since the last call, for the popups.</summary>
        public List<ScoreEvent> TakeNew()
        {
            var list = _events.GetRange(_pendingFrom, _events.Count - _pendingFrom);
            _pendingFrom = _events.Count;
            return list;
        }

        public void StartRun(FireClass fireClass, bool fleeOnly, double t)
        {
            if (_running)
                FinishRun(t);
            _running = true;
            _class = fireClass;
            _fleeOnly = fleeOnly;
            _stage = FireStage.None;
            _ignitedAt = -1d;
            _alarm = _discharged = _pin = _hose = _knockedDown = _foughtBeyond = _judgedAgent = _mistake = _reflashed = false;
            _rightAgent = _out = false;
            _tooClose = 0;
            _dischargeSeconds = _aimSum = _standoffSum = 0d;
            RunPoints = 0;
            _runStartIndex = _events.Count;
        }

        public void AlarmRaised(double t)
        {
            if (!_running || _alarm)
                return;
            _alarm = true;
            if (!_discharged)
                Add(t, 100, "Alarm raised first", "1910.38(c)(1)", false, BadgeAlarmFirst);
        }

        public void PinPulled(double t)
        {
            if (!_running || _pin)
                return;
            _pin = true;
            Add(t, 25, "Pin pulled", "1910.157(g)(1)", false);
        }

        public void HoseTaken(double t)
        {
            if (!_running || _hose)
                return;
            _hose = true;
            Add(t, 25, "Hose in hand", "1910.157(g)(1)", false);
        }

        public void StageChanged(FireStage stage, double t)
        {
            if (!_running)
                return;
            FireStage from = _stage;
            _stage = stage;
            if (stage == FireStage.Incipient && _ignitedAt < 0d)
                _ignitedAt = t;
            if (stage == FireStage.Out && from != FireStage.Out && !_knockedDown)
                KnockedDown(t);
            if (stage == FireStage.Incipient && from == FireStage.Out)
            {
                _reflashed = true;
                _mistake = true;
                Add(t, -50, "It reflashed: keep on it after the knockdown", "1910.157(g)(1)", true);
            }
        }

        /// <summary>
        /// The trainee stepped inside the fire's danger zone (FireProximity): points off each time, up to
        /// <see cref="TooCloseMaxPerRun"/> times a run, and no clean knockdown or perfect drill.
        /// </summary>
        public void TooClose(double t)
        {
            if (!_running || _tooClose >= TooCloseMaxPerRun)
                return;
            _tooClose++;
            _mistake = true;
            Add(t, TooClosePoints, "Too close to the fire: keep your distance", "1910.157(g)(1)", true);
        }

        /// <summary>One frame of discharge with this agent, aim and stand-off (0–1).</summary>
        public void Discharging(FireAgent agent, double aim, double standoff, double dt, double t)
        {
            if (!_running || agent == FireAgent.None)
                return;
            if (!_discharged)
            {
                _discharged = true;
                if (!_alarm)
                {
                    _mistake = true;
                    Add(t, -50, "Fought it before raising the alarm", "1910.38(c)(1)", true);
                }
            }
            if (!_judgedAgent)
            {
                _judgedAgent = true;
                JudgeAgent(agent, t);
            }
            if (_stage == FireStage.BeyondIncipient && !_foughtBeyond)
            {
                _foughtBeyond = true;
                _mistake = true;
                Add(t, -50, "Past the incipient stage: leave it", "1910.155(c)(26)", true);
            }
            if (_fleeOnly && !_foughtBeyond)
            {
                _foughtBeyond = true;
                _mistake = true;
                Add(t, -100, "Lithium-ion: do not fight it", "1910.38(c)(2)", true);
            }
            if (FireModel.Effectiveness(agent, _class) > 0d)
            {
                _dischargeSeconds += dt;
                _aimSum += aim * dt;
                _standoffSum += standoff * dt;
            }
        }

        public void Extinguished(double t)
        {
            if (!_running)
                return;
            _out = true;
            Add(t, 100, "Out, and no reflash", "1910.157(g)(1)", false);
            _classesPutOut.Add(_class);
            if (!_mistake)
                Add(t, 0, "Clean knockdown", string.Empty, false, BadgeCleanKnockdown);
            FinishRun(t);
        }

        /// <summary>The run ends (drill ended, another fire started): leaving a fire you should leave scores here.</summary>
        public void FinishRun(double t)
        {
            if (!_running)
                return;
            bool shouldLeave = _fleeOnly || _stage == FireStage.BeyondIncipient;
            if (shouldLeave && _alarm && !_foughtBeyond)
                Add(t, 150, "Knew when to leave", "1910.155(c)(26)", false, BadgeKnewWhenToLeave);
            if (!_mistake && RunPoints >= PerfectRunPoints)
                Add(t, 0, "Perfect drill", string.Empty, false, BadgePerfectDrill);
            if (_classesPutOut.Count >= 3)
                Add(t, 0, "Classes A, B and C put out", string.Empty, false, BadgeFireMarshal);
            _running = false;
            Runs++;
        }

        public double AverageAim => _dischargeSeconds > 0d ? _aimSum / _dischargeSeconds : 0d;
        public double AverageStandoff => _dischargeSeconds > 0d ? _standoffSum / _dischargeSeconds : 0d;

        private void JudgeAgent(FireAgent agent, double t)
        {
            double effect = FireModel.Effectiveness(agent, _class);
            string cls = FireDrillText.ClassLetter(_class);
            if (effect < 0d)
            {
                _mistake = true;
                Add(t, -150, $"{ExtinguisherRules.Name(agent)} on a Class {cls} fire spreads it", "1910.157(g)(1)", true);
            }
            else if (effect < 0.5d)
            {
                _mistake = true;
                Add(t, -25, $"{ExtinguisherRules.Name(agent)} barely touches Class {cls}", "1910.157(g)(1)", true);
            }
            else
            {
                _rightAgent = true;
                Add(t, 100, $"Right extinguisher for Class {cls}", "1910.157(g)(1)", false, BadgeRightClass);
            }
        }

        private void KnockedDown(double t)
        {
            _knockedDown = true;
            Add(t, 150, "Fire knocked down", "1910.157(g)(1)", false);
            if (_ignitedAt >= 0d && t - _ignitedAt <= FastResponseSeconds)
                Add(t, 50, "Fast response", string.Empty, false);
            if (AverageAim >= 0.75d)
                Add(t, 50, "Aimed at the base", "1910.157(g)(1)", false, AverageAim >= 0.85d ? BadgeSharpAim : null);
            if (AverageStandoff >= 0.8d)
                Add(t, 50, "Right distance", "1910.157(g)(1)", false);
        }

        private void Add(double t, int points, string text, string regulation, bool mistake, string badge = null)
        {
            // A badge only counts the first time this session.
            if (badge != null && !_badges.Add(badge))
                badge = null;
            if (points == 0 && badge == null)
                return;
            _events.Add(new ScoreEvent(t, points, text, regulation, mistake, badge));
            Total += points;
            RunPoints += points;
        }
    }
}
