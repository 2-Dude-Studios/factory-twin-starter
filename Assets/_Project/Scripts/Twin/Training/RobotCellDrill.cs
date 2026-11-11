using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training areas (M24): the robot cell safety drill, pure and tested: four levels on M10's zone signals, scored
    /// with the shared <see cref="DrillScore"/>. It is told what the cell's signals say (someone in the warning or the
    /// protective zone, the latched stop, presses of reset, line stop and line start) and works out the steps.
    /// Level 1, awareness: step into each zone and see what the arm does; reset from outside. Level 2, skills: stop
    /// the line, go in, come out, reset, restart. Level 3, procedure: clear a part at the gate under the protective
    /// stop (minor servicing with effective alternative protection, 1910.147(a)(2)(ii); decided 2026-11-11), then
    /// stop a colleague who walks into the cell. Level 4, assessment: a jam, a walker or both, with no hints.
    /// A simulated safety function, not safety-rated, and a training demo.
    /// </summary>
    public sealed class RobotCellDrill
    {
        public const string StepWarnIn = "warn_in";
        public const string StepWarnOut = "warn_out";
        public const string StepStopIn = "stop_in";
        public const string StepStopOut = "stop_out";
        public const string StepLineStop = "line_stop";
        public const string StepEnter = "enter";
        public const string StepClear = "clear";
        public const string StepExit = "exit";
        public const string StepReset = "reset";
        public const string StepRestart = "restart";
        public const string StepColleague = "colleague";
        public const string StepRecover = "recover";

        public const string BadgeZoneAware = "Zone aware";
        public const string BadgeResetRight = "Reset right";
        public const string BadgeCleanClear = "Clean clear";
        public const string BadgeEagleEye = "Eagle eye";
        public const string BadgeCellKeeper = "Cell keeper";

        public const string EnteredMoving = "Entered the cell while the arm was moving";
        public const string ResetInside = "Reset pressed from inside the zone: it is refused";
        public const string ResetWithSomeoneInside = "Reset pressed with someone in the zone: it is refused";
        public const string StartBeforeReset = "Start pressed before the reset";
        public const string ColleagueReachedArm = "Your colleague reached the arm: the zone stopped it, not you";
        public const string RestartedTooSoon = "Restarted before your colleague was clear";

        /// <summary>Seconds the trainee stands at the gate to clear the part.</summary>
        public const double ClearSeconds = 3d;

        /// <summary>Seconds after the drill (or its jam part) before the colleague sets off.</summary>
        public const double ColleagueDelaySeconds = 2d;

        private const string Zone = "ISO 10218-2";
        private const string Stop = "IEC 60204-1";
        private const string Servicing = "ISO 10218-2; 1910.147(a)(2)(ii)";

        private bool _warning, _protective, _latched, _lineStopped;
        private bool _jam, _walker;
        private bool _walkerSent, _walkerInProtective, _colleagueStoppedInTime, _resetInside, _jamMistake;
        private int _mistakesAtJamEnd = -1;
        private double _dwellFrom = -1d, _lastTick, _walkerAt = -1d;
        private bool _clearGate;
        private int _walkerCommand = -1;

        public DrillScore Score { get; private set; }
        public int Level { get; private set; }
        public bool Running => Score != null && Score.Result == DrillResult.InProgress;

        /// <summary>Levels 1–3 say what is due; the assessment does not.</summary>
        public bool Guided => Level < DrillLevels.Count;

        /// <summary>Whether the line stands stopped by a line stop press (as far as the drill has been told).</summary>
        public bool LineStopped => _lineStopped;

        /// <summary>The level has a part to clear at the gate.</summary>
        public bool HasJam => _jam;

        /// <summary>The level has a colleague who walks into the cell.</summary>
        public bool HasWalker => _walker;

        /// <summary>The step due, in words; empty in the assessment and when nothing runs.</summary>
        public string Prompt
        {
            get
            {
                if (!Running || !Guided)
                    return string.Empty;
                List<DrillStep> missing = Score.Missing();
                return missing.Count > 0 ? missing[0].Text : string.Empty;
            }
        }

        /// <summary>True once when the part at the gate is to be cleared (the scene publishes gate1/clear).</summary>
        public bool TakeClearGate()
        {
            bool clear = _clearGate;
            _clearGate = false;
            return clear;
        }

        /// <summary>A WorkerWalk value to send the colleague (2 into the cell, 0 home), once; −1 when there is none.</summary>
        public int TakeWalkerCommand()
        {
            int command = _walkerCommand;
            _walkerCommand = -1;
            return command;
        }

        /// <summary>Par times for three stars, in seconds.</summary>
        public static double ParSeconds(int level)
        {
            switch (level)
            {
                case 1: return 120d;
                case 2: return 120d;
                case 3: return 240d;
                default: return 240d;
            }
        }

        /// <summary>What the assessment asks, from its seed: a jam, a walker, or both.</summary>
        public static void AssessmentTasks(int seed, out bool jam, out bool walker)
        {
            int pick = ((seed % 3) + 3) % 3;
            jam = pick != 1;
            walker = pick != 0;
        }

        public static List<DrillStep> Steps(int level, bool jam, bool walker)
        {
            var steps = new List<DrillStep>();
            if (level == 1)
            {
                steps.Add(new DrillStep(StepWarnIn, "Step into the warning zone: the arm slows", "ISO 10218-2; ISO 13855"));
                steps.Add(new DrillStep(StepWarnOut, "Step back out: full speed returns by itself", Zone));
                steps.Add(new DrillStep(StepStopIn, "Step into the protective zone: the arm stops and the line holds", "ISO 10218-2; ISO 13855"));
                steps.Add(new DrillStep(StepStopOut, "Step out: nothing restarts by itself", Zone));
                steps.Add(new DrillStep(StepReset, "Reset from outside the zone: the line runs again", Zone));
                return steps;
            }
            if (level == 2 || jam)
            {
                steps.Add(new DrillStep(StepLineStop, "Stop the line at the control station", Stop));
                steps.Add(new DrillStep(StepEnter, "Go into the cell with the line stopped", Servicing));
                if (level != 2)
                    steps.Add(new DrillStep(StepClear, "Clear the part at the gate: stand at it for 3 seconds", Servicing, 100));
                steps.Add(new DrillStep(StepExit, "Come out of the cell", Zone));
                steps.Add(new DrillStep(StepReset, "Reset from outside the zone", Zone));
                steps.Add(new DrillStep(StepRestart, "Restart the line", Stop));
            }
            if (level != 2 && walker)
            {
                steps.Add(new DrillStep(StepColleague, "A colleague is walking into the cell: stop the line", Zone, 100));
                steps.Add(new DrillStep(StepRecover, "Colleague clear: reset if needed, then restart the line", Zone));
            }
            return steps;
        }

        /// <summary>
        /// Starts a level (1–4). Tell it how the cell stands right now: whether the line was already stopped by a
        /// line stop press. The assessment's seed picks its tasks.
        /// </summary>
        public void Start(int level, double t, int seed = 0, bool lineStopped = false)
        {
            Level = System.Math.Min(System.Math.Max(level, 1), DrillLevels.Count);
            _jam = Level == 3;
            _walker = Level == 3;
            if (Level == DrillLevels.Count)
                AssessmentTasks(seed, out _jam, out _walker);
            Score = new DrillScore(TrainingArea.RobotCell, Level, Steps(Level, _jam, _walker), ParSeconds(Level));
            Score.Start(t);
            _lineStopped = lineStopped;
            _walkerSent = _walkerInProtective = _colleagueStoppedInTime = _resetInside = _jamMistake = false;
            _mistakesAtJamEnd = -1;
            _dwellFrom = -1d;
            _lastTick = t;
            _clearGate = false;
            _walkerCommand = -1;
            _walkerAt = _walker && !_jam ? t + ColleagueDelaySeconds : -1d;
        }

        /// <summary>The trainer ends the level: finished as it stands (a level with steps missing fails).</summary>
        public void End(double t)
        {
            if (!Running)
                return;
            if (_walkerSent)
                _walkerCommand = 0;
            Score.Finish(t);
        }

        private bool WalkerPhase => _walker && _walkerSent && !Score.IsDone(StepRecover);

        public void ZoneWarning(bool occupied, double t)
        {
            bool was = _warning;
            _warning = occupied;
            if (!Running || occupied == was || Level != 1)
                return;
            if (occupied && !_protective)
                Score.Done(StepWarnIn, t);
            else if (!occupied && Score.IsDone(StepWarnIn) && !_latched)
                Done(StepWarnOut, t);
        }

        public void ProtectiveZone(bool occupied, double t)
        {
            bool was = _protective;
            _protective = occupied;
            if (!Running || occupied == was)
                return;
            // The wait at the gate counts from stepping in, and starts again after stepping out.
            _dwellFrom = occupied ? t : -1d;

            if (Level == 1)
            {
                if (occupied)
                    Score.Done(StepStopIn, t);
                else if (Score.IsDone(StepStopIn))
                    Done(StepStopOut, t);
                return;
            }
            if (WalkerPhase)
            {
                // The colleague, not the trainee: they got to the arm before the line was stopped.
                if (occupied && !Score.IsDone(StepColleague) && !_walkerInProtective)
                {
                    _walkerInProtective = true;
                    Score.Mistake(ColleagueReachedArm, Zone, 50, t);
                }
                return;
            }
            if (occupied)
            {
                if (_lineStopped)
                {
                    Score.Done(StepEnter, t);
                }
                else if (Level == 2)
                {
                    Score.Mistake("Entered while the arm was moving: stop the line first", Stop, 50, t);
                }
                else
                {
                    _jamMistake = true;
                    Score.Critical(EnteredMoving, Servicing, t);
                }
            }
            else if (Score.IsDone(StepEnter) && (Level == 2 || !_jam || Score.IsDone(StepClear)))
            {
                Score.Done(StepExit, t);
            }
        }

        public void ProtectiveStop(bool latched, double t)
        {
            _latched = latched;
        }

        public void ResetPressed(double t)
        {
            if (!Running)
                return;
            if (_protective)
            {
                // Refused by the zone either way: the trainee is inside, or the colleague is.
                _resetInside = true;
                Score.Mistake(WalkerPhase ? ResetWithSomeoneInside : ResetInside, Zone, 25, t);
                return;
            }
            if (Level == 1)
            {
                if (Score.IsDone(StepStopOut))
                    Done(StepReset, t);
                return;
            }
            if (!WalkerPhase && Score.IsDone(StepExit))
                Score.Done(StepReset, t);
        }

        public void LineStopPressed(double t)
        {
            _lineStopped = true;
            if (!Running || Level == 1)
                return;
            if (WalkerPhase)
            {
                if (Score.Done(StepColleague, t))
                {
                    _colleagueStoppedInTime = !_walkerInProtective;
                    _walkerCommand = 0;
                }
                return;
            }
            Score.Done(StepLineStop, t);
        }

        public void LineStartPressed(double t)
        {
            bool wasStopped = _lineStopped;
            _lineStopped = false;
            if (!Running || Level == 1)
                return;
            if (WalkerPhase)
            {
                if (!Score.IsDone(StepColleague))
                    return;
                if (_warning || _protective || _latched)
                {
                    Score.Mistake(_latched && !_protective && !_warning ? StartBeforeReset : RestartedTooSoon, Zone, 25, t);
                    _lineStopped = wasStopped;
                    return;
                }
                Done(StepRecover, t);
                return;
            }
            if (Score.IsDone(StepReset))
            {
                if (Score.Done(StepRestart, t))
                {
                    _mistakesAtJamEnd = Score.Mistakes;
                    if (_walker)
                        _walkerAt = t + ColleagueDelaySeconds;
                    FinishIfComplete(t);
                }
            }
            else if (_latched || Score.IsDone(StepEnter))
            {
                Score.Mistake(StartBeforeReset, Stop, 25, t);
                _lineStopped = wasStopped;
            }
        }

        /// <summary>Time passes: the part at the gate clears after the dwell, and the colleague sets off when due.</summary>
        public void Tick(double t)
        {
            _lastTick = t;
            if (!Running)
                return;
            if (Clearing && t - _dwellFrom >= ClearSeconds && Score.Done(StepClear, t))
                _clearGate = true;
            if (_walker && !_walkerSent && _walkerAt >= 0d && t >= _walkerAt)
            {
                _walkerSent = true;
                _walkerCommand = 2;
            }
        }

        /// <summary>Seconds left of the dwell at the gate; 0 when it is not running.</summary>
        public double ClearSecondsLeft => Clearing ? System.Math.Max(0d, ClearSeconds - (_lastTick - _dwellFrom)) : 0d;

        // Standing at the gate with the line stopped, the part not yet cleared.
        private bool Clearing =>
            Running && _jam && _dwellFrom >= 0d && _protective && _lineStopped && Score.IsDone(StepEnter) && !Score.IsDone(StepClear);

        private void Done(string step, double t)
        {
            if (Score.Done(step, t))
                FinishIfComplete(t);
        }

        private void FinishIfComplete(double t)
        {
            if (Score.StepsDone < Score.StepsRequired)
                return;
            if (Level == 1 && Score.Mistakes == 0 && Score.Criticals == 0)
                Score.Badge(BadgeZoneAware, t);
            if (Level == 2 && !_resetInside)
                Score.Badge(BadgeResetRight, t);
            if (Level >= 3)
            {
                if (_jam && !_jamMistake && _mistakesAtJamEnd == 0)
                    Score.Badge(BadgeCleanClear, t);
                if (_walker && _colleagueStoppedInTime)
                    Score.Badge(BadgeEagleEye, t);
            }
            // The assessment's own badge needs all three stars, worked out here so it is in the score before it closes.
            if (Level == DrillLevels.Count &&
                DrillStars.For(true, Score.Criticals, Score.Mistakes, Score.Seconds(t), Score.ParSeconds) == DrillStars.Max)
                Score.Badge(BadgeCellKeeper, t);
            Score.Finish(t);
        }

        /// <summary>The badges the run earned.</summary>
        public IReadOnlyList<string> Badges() => Score != null ? Score.Badges : (IReadOnlyList<string>)System.Array.Empty<string>();
    }
}
