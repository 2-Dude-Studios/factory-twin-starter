using System;
using System.Collections.Generic;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub (M28): the Meta leaderboards, one per training (API names robot_cell, lockout, fire, electrical,
    /// created on the Developer Dashboard: higher is better, each user's best kept). A scored run's points go up when it
    /// ends (<see cref="RunResults"/>); the top five of each board are fetched when the account is ready and after
    /// each write, for the hub menu's cards. Nothing happens while the account is offline.
    /// </summary>
    public sealed class MetaLeaderboards : MonoBehaviour
    {
        [SerializeField] private MetaAccount account;
        [SerializeField, Range(1, 10)] private int topCount = 5;

        /// <summary>One line on a board: rank, name and score.</summary>
        public readonly struct Line
        {
            public Line(int rank, string name, long score)
            {
                Rank = rank;
                Name = name;
                Score = score;
            }

            public int Rank { get; }
            public string Name { get; }
            public long Score { get; }
        }

        private readonly Dictionary<TrainingMode, List<Line>> _top = new Dictionary<TrainingMode, List<Line>>();

        /// <summary>Raised when a board's top lines have been fetched.</summary>
        public event Action<TrainingMode> Updated;

        /// <summary>The board's API name for a training, or null for one without a board.</summary>
        public static string BoardFor(TrainingMode mode)
        {
            switch (mode)
            {
                case TrainingMode.RobotZone: return "robot_cell";
                case TrainingMode.Lockout: return "lockout";
                case TrainingMode.FireSafety: return "fire";
                case TrainingMode.Electrical: return "electrical";
                default: return null;
            }
        }

        public IReadOnlyList<Line> Top(TrainingMode mode) =>
            _top.TryGetValue(mode, out List<Line> lines) ? lines : (IReadOnlyList<Line>)Array.Empty<Line>();

        private void OnEnable()
        {
            RunResults.Published += OnRun;
            if (account != null)
                account.SignedIn += OnSignedIn;
        }

        private void OnDisable()
        {
            RunResults.Published -= OnRun;
            if (account != null)
                account.SignedIn -= OnSignedIn;
        }

        private void OnSignedIn(string name)
        {
            foreach (TrainingMode mode in new[] { TrainingMode.RobotZone, TrainingMode.Lockout, TrainingMode.FireSafety, TrainingMode.Electrical })
                Fetch(mode);
        }

        private void OnRun(RunResult result)
        {
            string board = BoardFor(result.Mode);
            if (account == null || !account.Ready || board == null || result.Points <= 0)
                return;
            TrainingMode mode = result.Mode;
            Leaderboards.WriteEntry(board, result.Points).OnComplete(m =>
            {
                if (m.IsError)
                    Debug.LogWarning($"[MetaLeaderboards] Score not posted to {board}: {m.GetError().Message}.", this);
                Fetch(mode);
            });
        }

        private void Fetch(TrainingMode mode)
        {
            string board = BoardFor(mode);
            if (account == null || !account.Ready || board == null)
                return;
            Leaderboards.GetEntries(board, topCount, LeaderboardFilterType.None, LeaderboardStartAt.Top).OnComplete(m =>
            {
                if (m.IsError || m.Data == null)
                    return;
                var lines = new List<Line>();
                foreach (LeaderboardEntry entry in m.Data)
                {
                    string name = entry.User != null ? (string.IsNullOrWhiteSpace(entry.User.DisplayName) ? entry.User.OculusID : entry.User.DisplayName) : "?";
                    lines.Add(new Line(entry.Rank, name, entry.Score));
                }
                _top[mode] = lines;
                Updated?.Invoke(mode);
            });
        }
    }
}
