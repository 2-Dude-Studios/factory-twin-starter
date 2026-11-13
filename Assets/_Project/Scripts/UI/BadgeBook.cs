using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M15): a trainee's badges and best score per training, kept between sessions as a small JSON file
    /// (persistent data, or Builds/Training in the Editor). The start menu shows them on each training's card.
    /// Since M24 it also keeps the best stars per level and the day the assessment was last passed, which the hub's
    /// badge wall and each area's score board show.
    /// </summary>
    public sealed class BadgeBook
    {
        [Serializable]
        private sealed class Entry
        {
            public TrainingMode mode;
            public int best;
            public List<string> badges = new List<string>();
            public List<int> stars = new List<int>();
            public string assessedUtc = string.Empty;
        }

        [Serializable]
        private sealed class Data
        {
            public string trainee;
            public List<Entry> entries = new List<Entry>();
            // M28: XP, the day streak and the daily challenge (books from before load with none).
            public ProgressState progress = new ProgressState();
        }

        private readonly Data _data;
        private readonly string _path;

        private BadgeBook(Data data, string path)
        {
            _data = data;
            _path = path;
        }

        public string Trainee => _data.trainee;

        /// <summary>Raised after a book is written to disk, with the trainee's name: the boards read it again.</summary>
        public static event Action<string> Saved;

        public static BadgeBook Load(string trainee)
        {
            string path = PathFor(trainee);
            Data data = null;
            try
            {
                if (File.Exists(path))
                    data = JsonUtility.FromJson<Data>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BadgeBook] Could not read {path}: {e.Message}");
            }
            return new BadgeBook(data ?? new Data { trainee = trainee }, path);
        }

        /// <summary>A book that is not saved anywhere (tests).</summary>
        public static BadgeBook InMemory(string trainee) => new BadgeBook(new Data { trainee = trainee }, null);

        public int BestScore(TrainingMode mode) => Find(mode)?.best ?? 0;

        /// <summary>The trainee's XP: every point of every run, the daily challenge doubled (M28).</summary>
        public int Xp => Progress.xp;

        /// <summary>Days in a row trained, counting the last day trained.</summary>
        public int Streak => Progress.streak;

        public bool DailyDone(DateTime utc) => TraineeProgress.DailyDone(Progress, utc);

        /// <summary>What the last run recorded here earned.</summary>
        public XpGain LastGain { get; private set; }

        private ProgressState Progress => _data.progress ??= new ProgressState();

        public IReadOnlyList<string> Earned(TrainingMode mode) => (IReadOnlyList<string>)Find(mode)?.badges ?? Array.Empty<string>();

        /// <summary>Adds a session's score and badges, and its XP. Returns the badges that are new to this trainee.</summary>
        public List<string> Record(TrainingMode mode, int score, IEnumerable<string> badges)
        {
            List<string> fresh = RecordScore(mode, score, badges);
            LastGain = TraineeProgress.Gain(Progress, TrainingLaunch.AreaFor(mode), 0, score, DateTime.UtcNow);
            return fresh;
        }

        private List<string> RecordScore(TrainingMode mode, int score, IEnumerable<string> badges)
        {
            Entry entry = Find(mode);
            if (entry == null)
            {
                entry = new Entry { mode = mode };
                _data.entries.Add(entry);
            }
            entry.best = Math.Max(entry.best, score);
            var fresh = new List<string>();
            foreach (string badge in badges)
            {
                if (!entry.badges.Contains(badge))
                {
                    entry.badges.Add(badge);
                    fresh.Add(badge);
                }
            }
            return fresh;
        }

        /// <summary>The best stars reached on each of a training's four levels (0 where none yet).</summary>
        public IReadOnlyList<int> Stars(TrainingMode mode)
        {
            var stars = new int[DrillLevels.Count];
            Entry entry = Find(mode);
            for (int i = 0; entry != null && entry.stars != null && i < entry.stars.Count && i < stars.Length; i++)
                stars[i] = entry.stars[i];
            return stars;
        }

        /// <summary>The day the training's assessment (level 4) was last passed, or null.</summary>
        public DateTime? AssessedUtc(TrainingMode mode)
        {
            Entry entry = Find(mode);
            if (entry == null || string.IsNullOrEmpty(entry.assessedUtc))
                return null;
            return DateTime.TryParseExact(entry.assessedUtc, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime day) ? day : (DateTime?)null;
        }

        /// <summary>
        /// Adds one level run: its points and badges as <see cref="Record"/> does, the level's best stars, and the
        /// day when it was the assessment and it passed. Returns the badges that are new to this trainee.
        /// </summary>
        public List<string> RecordLevel(TrainingMode mode, int level, int stars, int points, IEnumerable<string> badges, DateTime utc)
        {
            List<string> fresh = RecordScore(mode, points, badges);
            LastGain = TraineeProgress.Gain(Progress, TrainingLaunch.AreaFor(mode), level, points, utc);
            if (level < 1 || level > DrillLevels.Count)
                return fresh;
            Entry entry = Find(mode);
            if (entry.stars == null)
                entry.stars = new List<int>();
            while (entry.stars.Count < DrillLevels.Count)
                entry.stars.Add(0);
            entry.stars[level - 1] = Math.Max(entry.stars[level - 1], Math.Min(Math.Max(stars, 0), DrillStars.Max));
            if (level == DrillLevels.Count && stars > 0)
                entry.assessedUtc = utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return fresh;
        }

        public void Save()
        {
            if (_path == null)
                return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
                File.WriteAllText(_path, JsonUtility.ToJson(_data, true));
                Saved?.Invoke(_data.trainee);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BadgeBook] Could not save {_path}: {e.Message}");
            }
        }

        public static string Folder() => Application.isEditor
            ? Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Builds", "Training")
            : Path.Combine(Application.persistentDataPath, "Training");

        private static string PathFor(string trainee)
        {
            string safe = string.IsNullOrWhiteSpace(trainee) ? "trainee" : trainee.Trim().Replace(' ', '_');
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');
            return Path.Combine(Folder(), $"badges_{safe}.json");
        }

        private Entry Find(TrainingMode mode)
        {
            foreach (Entry e in _data.entries)
            {
                if (e.mode == mode)
                    return e;
            }
            return null;
        }
    }
}
