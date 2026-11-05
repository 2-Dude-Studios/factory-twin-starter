using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M15): a trainee's badges and best score per training, kept between sessions as a small JSON file
    /// (persistent data, or Builds/Training in the Editor). The start menu shows them on each training's card.
    /// </summary>
    public sealed class BadgeBook
    {
        [Serializable]
        private sealed class Entry
        {
            public TrainingMode mode;
            public int best;
            public List<string> badges = new List<string>();
        }

        [Serializable]
        private sealed class Data
        {
            public string trainee;
            public List<Entry> entries = new List<Entry>();
        }

        private readonly Data _data;
        private readonly string _path;

        private BadgeBook(Data data, string path)
        {
            _data = data;
            _path = path;
        }

        public string Trainee => _data.trainee;

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

        public IReadOnlyList<string> Earned(TrainingMode mode) => (IReadOnlyList<string>)Find(mode)?.badges ?? Array.Empty<string>();

        /// <summary>Adds a session's score and badges. Returns the badges that are new to this trainee.</summary>
        public List<string> Record(TrainingMode mode, int score, IEnumerable<string> badges)
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

        public void Save()
        {
            if (_path == null)
                return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
                File.WriteAllText(_path, JsonUtility.ToJson(_data, true));
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
