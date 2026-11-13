using System;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training areas (M24): when a lockout run ends (the TrainingController has written its record), scores it as
    /// level 3 of the lockout drill with the shared stars (<see cref="LockoutLevel"/>) and adds the stars, points and
    /// badges to the trainee's badge book, which the lockout area's score board and the hub's badge wall show.
    /// </summary>
    public sealed class LockoutScorer : MonoBehaviour
    {
        [SerializeField] private TrainingController controller;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private int stars;
        [SerializeField] private int points;
        [SerializeField] private string summary;

        private void OnEnable()
        {
            if (controller != null)
                controller.RecordWritten += OnRecordWritten;
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.RecordWritten -= OnRecordWritten;
        }

        private void OnRecordWritten(string file)
        {
            DrillScore score = LockoutLevel.Score(controller.Run);
            stars = score.Stars;
            points = score.Points;
            summary = DrillRecord.Summary(score, TrainingLaunch.Title(TrainingMode.Lockout));
            BadgeBook book = BadgeBook.Load(controller.TraineeName);
            int bestBefore = book.BestScore(TrainingMode.Lockout), xpBefore = book.Xp;
            List<string> fresh = book.RecordLevel(TrainingMode.Lockout, score.Level, score.Stars, score.Points, score.Badges, DateTime.UtcNow);
            book.Save();
            RunResults.Publish(new RunResult
            {
                Mode = TrainingMode.Lockout, Level = score.Level, Title = $"{TrainingLaunch.Title(TrainingMode.Lockout)}: level {score.Level} {DrillLevels.LevelName(score.Level)}",
                Stars = score.Stars, Points = score.Points, Passed = score.Stars > 0, NewBest = score.Points > bestBefore,
                NewBadges = fresh, Gain = book.LastGain, XpBefore = xpBefore, XpAfter = book.Xp,
            });
            Debug.Log($"[LockoutScorer] {summary}{(fresh.Count > 0 ? ". New badges: " + string.Join(", ", fresh) : string.Empty)}", this);
        }
    }
}
