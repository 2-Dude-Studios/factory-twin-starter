using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Training areas (M24): what a line in a drill's score is.</summary>
    public enum DrillEventKind
    {
        Done = 0,
        Mistake = 1,

        /// <summary>A mistake that fails the level whatever the points.</summary>
        Critical = 2,
        Metric = 3,
        Badge = 4,
    }

    /// <summary>Training areas (M24): how a level run stands.</summary>
    public enum DrillResult
    {
        NotStarted = 0,
        InProgress = 1,
        Passed = 2,
        Failed = 3,
    }

    /// <summary>Training areas (M24): one thing the trainee did in a level, with the rule it follows.</summary>
    public readonly struct DrillEvent
    {
        public DrillEvent(double atSeconds, DrillEventKind kind, int points, string text, string regulation)
        {
            AtSeconds = atSeconds;
            Kind = kind;
            Points = points;
            Text = text;
            Regulation = regulation;
        }

        public double AtSeconds { get; }
        public DrillEventKind Kind { get; }
        public int Points { get; }
        public string Text { get; }
        public string Regulation { get; }
    }

    /// <summary>Training areas (M24): a step a level requires, with the rule it practises and what it is worth.</summary>
    public sealed class DrillStep
    {
        public DrillStep(string id, string text, string regulation, int points = 50)
        {
            Id = id;
            Text = text;
            Regulation = regulation;
            Points = points;
        }

        public string Id { get; }
        public string Text { get; }
        public string Regulation { get; }
        public int Points { get; }
    }

    /// <summary>
    /// Training areas (M24): the stars a level run earns, the same for every drill. One star for every required step
    /// with no critical mistake; two with no minor mistakes either; three when that is also done within the par time.
    /// A critical mistake or a missing step earns none: points never buy back a safety rule.
    /// </summary>
    public static class DrillStars
    {
        public const int Max = 3;

        public static int For(bool allStepsDone, int criticals, int mistakes, double seconds, double parSeconds)
        {
            if (!allStepsDone || criticals > 0)
                return 0;
            if (mistakes > 0)
                return 1;
            return parSeconds <= 0d || seconds <= parSeconds ? 3 : 2;
        }

        /// <summary>Stars as text that any font can show: "**-" is two of three.</summary>
        public static string Text(int stars)
        {
            stars = Math.Min(Math.Max(stars, 0), Max);
            return new string('*', stars) + new string('-', Max - stars);
        }
    }

    /// <summary>
    /// Training areas (M24): scores one run of one level of a drill, pure and tested. The level has required steps;
    /// each is worth its points once. Minor mistakes take points off; a critical mistake fails the run whatever the
    /// points. Finishing works out the result and the stars (<see cref="DrillStars"/>). Every line carries its rule,
    /// so the debrief and the record read by clause. The same for all four drills.
    /// </summary>
    public sealed class DrillScore
    {
        public const int CriticalPoints = -100;

        private readonly List<DrillStep> _required = new List<DrillStep>();
        private readonly HashSet<string> _done = new HashSet<string>();
        private readonly List<DrillEvent> _events = new List<DrillEvent>();
        private readonly List<string> _badges = new List<string>();
        private double _startedAt;
        private double _finishedAt;
        private int _pendingFrom;

        public DrillScore(TrainingArea area, int level, IEnumerable<DrillStep> required, double parSeconds)
        {
            Area = area;
            Level = level;
            ParSeconds = parSeconds;
            if (required != null)
                _required.AddRange(required);
        }

        public TrainingArea Area { get; }
        public int Level { get; }
        public double ParSeconds { get; }
        public DrillResult Result { get; private set; }
        public int Points { get; private set; }
        public int Mistakes { get; private set; }
        public int Criticals { get; private set; }
        public int Stars { get; private set; }
        public int StepsDone => _done.Count;
        public int StepsRequired => _required.Count;
        public IReadOnlyList<DrillStep> Required => _required;
        public IReadOnlyList<DrillEvent> Events => _events;
        public IReadOnlyList<string> Badges => _badges;

        /// <summary>Seconds from the start to the finish, or to <paramref name="now"/> while it runs.</summary>
        public double Seconds(double now) =>
            Result == DrillResult.NotStarted ? 0d : (Result == DrillResult.InProgress ? now : _finishedAt) - _startedAt;

        /// <summary>Seconds a finished run took; 0 until it is finished.</summary>
        public double Duration => _finishedAt - _startedAt;

        public bool IsDone(string stepId) => _done.Contains(stepId);

        /// <summary>The required steps not done yet, in order: what is still to do, and why a run failed.</summary>
        public List<DrillStep> Missing()
        {
            var missing = new List<DrillStep>();
            foreach (DrillStep step in _required)
            {
                if (!_done.Contains(step.Id))
                    missing.Add(step);
            }
            return missing;
        }

        public void Start(double t)
        {
            _done.Clear();
            _events.Clear();
            _badges.Clear();
            _pendingFrom = 0;
            Points = Mistakes = Criticals = Stars = 0;
            _startedAt = _finishedAt = t;
            Result = DrillResult.InProgress;
        }

        /// <summary>A required step done: its points, once. False for a step the level does not have or that is done.</summary>
        public bool Done(string stepId, double t)
        {
            if (Result != DrillResult.InProgress)
                return false;
            DrillStep step = _required.Find(s => s.Id == stepId);
            if (step == null || !_done.Add(stepId))
                return false;
            Add(t, DrillEventKind.Done, step.Points, step.Text, step.Regulation);
            return true;
        }

        /// <summary>Something right beyond the required steps (a fast response, a clean technique).</summary>
        public void Bonus(string text, string regulation, int points, double t)
        {
            if (Result == DrillResult.InProgress)
                Add(t, DrillEventKind.Done, Math.Abs(points), text, regulation);
        }

        /// <summary>A minor mistake: points off, and no more than one star.</summary>
        public void Mistake(string text, string regulation, int pointsOff, double t)
        {
            if (Result != DrillResult.InProgress)
                return;
            Mistakes++;
            Add(t, DrillEventKind.Mistake, -Math.Abs(pointsOff), text, regulation);
        }

        /// <summary>A critical mistake: the run fails when it finishes, whatever the points.</summary>
        public void Critical(string text, string regulation, double t)
        {
            if (Result != DrillResult.InProgress)
                return;
            Criticals++;
            Add(t, DrillEventKind.Critical, CriticalPoints, text, regulation);
        }

        /// <summary>A badge, once a run. False when it was already awarded.</summary>
        public bool Badge(string name, double t)
        {
            if (Result != DrillResult.InProgress || string.IsNullOrEmpty(name) || _badges.Contains(name))
                return false;
            _badges.Add(name);
            Add(t, DrillEventKind.Badge, 0, name, string.Empty);
            return true;
        }

        /// <summary>A number for the record (time to safe, tests done) that scores nothing.</summary>
        public void Metric(string name, string value, double t)
        {
            if (Result == DrillResult.InProgress)
                Add(t, DrillEventKind.Metric, 0, name + "=" + value, string.Empty);
        }

        /// <summary>Ends the run: passed with its stars, or failed. A failed run keeps its points but earns no stars.</summary>
        public void Finish(double t)
        {
            if (Result != DrillResult.InProgress)
                return;
            _finishedAt = t;
            Stars = DrillStars.For(_done.Count == _required.Count, Criticals, Mistakes, t - _startedAt, ParSeconds);
            Result = Stars > 0 ? DrillResult.Passed : DrillResult.Failed;
        }

        /// <summary>Why a finished run failed, in words; empty when it passed or is not finished.</summary>
        public string FailReason()
        {
            if (Result != DrillResult.Failed)
                return string.Empty;
            if (Criticals > 0)
            {
                foreach (DrillEvent e in _events)
                {
                    if (e.Kind == DrillEventKind.Critical)
                        return "Critical mistake: " + e.Text;
                }
            }
            List<DrillStep> missing = Missing();
            return missing.Count == 0 ? string.Empty : "Not done: " + missing[0].Text + (missing.Count > 1 ? $" and {missing.Count - 1} more" : string.Empty);
        }

        /// <summary>Lines since the last call, for the popups.</summary>
        public List<DrillEvent> TakeNew()
        {
            List<DrillEvent> list = _events.GetRange(_pendingFrom, _events.Count - _pendingFrom);
            _pendingFrom = _events.Count;
            return list;
        }

        private void Add(double t, DrillEventKind kind, int points, string text, string regulation)
        {
            _events.Add(new DrillEvent(t - _startedAt, kind, points, text, regulation));
            Points += points;
        }
    }

    /// <summary>
    /// Training areas (M24): what a trainee has reached in a drill, and what follows from it. A level opens once the
    /// one before it is passed. The assessment (level 4) starts the clock for the refresher: yearly for fire
    /// (1910.157(g)(2)), yearly as a reminder for lockout (the procedure is inspected yearly, 1910.147(c)(6)) and the
    /// robot cell (the demo's default), within three years for electrical (NFPA 70E 110.4). A reminder, not a record
    /// of compliance.
    /// </summary>
    public static class DrillProgress
    {
        /// <summary>Level 1 is always open; each later level opens when the one before it has a star.</summary>
        public static bool Unlocked(IReadOnlyList<int> starsByLevel, int level)
        {
            if (level <= 1)
                return level == 1;
            if (level > DrillLevels.Count || starsByLevel == null || starsByLevel.Count < level - 1)
                return false;
            return starsByLevel[level - 2] > 0;
        }

        public static int TotalStars(IReadOnlyList<int> starsByLevel)
        {
            int total = 0;
            if (starsByLevel != null)
            {
                foreach (int stars in starsByLevel)
                    total += Math.Min(Math.Max(stars, 0), DrillStars.Max);
            }
            return total;
        }

        public static int RefresherMonths(TrainingArea area) => area == TrainingArea.Electrical ? 36 : 12;

        /// <summary>When the drill is due again, from the day its assessment was last passed; null before that.</summary>
        public static DateTime? DueAgain(DateTime? assessedUtc, TrainingArea area) =>
            assessedUtc.HasValue ? assessedUtc.Value.AddMonths(RefresherMonths(area)) : (DateTime?)null;
    }

    /// <summary>
    /// Training areas (M24): a level run as CSV, the same columns as the lockout and fire records
    /// (trainee, trainer, started_utc, at_s, step, regulation, kind, detail), so one viewer reads them all. Kinds:
    /// done, mistake, critical, metric, badge, and the result. A demo record, not a certification.
    /// </summary>
    public static class DrillRecord
    {
        public const string Header = "trainee,trainer,started_utc,at_s,step,regulation,kind,detail";

        public static string ToCsv(string trainee, string trainer, DateTime startedUtc, DrillScore score, string drillTitle)
        {
            if (score == null)
                throw new ArgumentNullException(nameof(score));
            string started = startedUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csv = new StringBuilder();
            csv.Append(Header).Append('\n');
            int step = 0;
            foreach (DrillEvent e in score.Events)
            {
                string at = e.AtSeconds.ToString("0.0", CultureInfo.InvariantCulture);
                bool scored = e.Kind == DrillEventKind.Done || e.Kind == DrillEventKind.Mistake || e.Kind == DrillEventKind.Critical;
                string number = scored ? (++step).ToString(CultureInfo.InvariantCulture) : string.Empty;
                string detail = scored && e.Points != 0
                    ? $"{e.Text} ({(e.Points > 0 ? "+" : string.Empty)}{e.Points.ToString(CultureInfo.InvariantCulture)})"
                    : e.Text;
                Row(csv, trainee, trainer, started, at, number, e.Regulation, KindWord(e.Kind), detail);
            }
            Row(csv, trainee, trainer, started, string.Empty, string.Empty, string.Empty, "result", Summary(score, drillTitle));
            return csv.ToString();
        }

        /// <summary>The run in one line: the drill and level, passed or failed and why, stars, points, mistakes.</summary>
        public static string Summary(DrillScore score, string drillTitle)
        {
            string level = $"{drillTitle} level {score.Level} {DrillLevels.LevelName(score.Level)}";
            switch (score.Result)
            {
                case DrillResult.Passed:
                    return $"{level}: passed, {score.Stars} of {DrillStars.Max} stars, {score.Points} points, " +
                           $"{score.Mistakes} mistake{(score.Mistakes == 1 ? "" : "s")}, {Math.Round(score.Duration)} s";
                case DrillResult.Failed:
                    return $"{level}: failed ({score.FailReason()}), {score.Points} points";
                case DrillResult.InProgress:
                    return $"{level}: not finished, {score.StepsDone} of {score.StepsRequired} steps";
                default:
                    return $"{level}: not started";
            }
        }

        public static string KindWord(DrillEventKind kind)
        {
            switch (kind)
            {
                case DrillEventKind.Mistake: return "mistake";
                case DrillEventKind.Critical: return "critical";
                case DrillEventKind.Metric: return "metric";
                case DrillEventKind.Badge: return "badge";
                default: return "done";
            }
        }

        /// <summary>The record's file name: the drill's short name, the trainee and the UTC start.</summary>
        public static string FileName(TrainingArea area, string trainee, DateTime startedUtc)
        {
            string who = string.IsNullOrWhiteSpace(trainee) ? "trainee" : trainee.Trim().Replace(' ', '_');
            var safe = new StringBuilder(who.Length);
            foreach (char c in who)
                safe.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return $"{Prefix(area)}_{safe}_{startedUtc.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.csv";
        }

        public static string Prefix(TrainingArea area)
        {
            switch (area)
            {
                case TrainingArea.RobotCell: return "robot";
                case TrainingArea.Lockout: return "lockout";
                case TrainingArea.Fire: return "fire";
                case TrainingArea.Electrical: return "elec";
                default: return "training";
            }
        }

        private static readonly char[] QuoteTriggers = { ',', '"', '\n' };

        private static void Row(StringBuilder csv, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0)
                    csv.Append(',');
                string cell = cells[i] ?? string.Empty;
                csv.Append(cell.IndexOfAny(QuoteTriggers) < 0 ? cell : "\"" + cell.Replace("\"", "\"\"") + "\"");
            }
            csv.Append('\n');
        }
    }
}
