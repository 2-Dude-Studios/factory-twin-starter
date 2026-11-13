using System;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class TraineeRankTests
    {
        [Test]
        public void RanksClimbWithXp()
        {
            Assert.AreEqual("Trainee", TraineeRank.For(0).Name);
            Assert.AreEqual("Trainee", TraineeRank.For(499).Name);
            Assert.AreEqual("Operator", TraineeRank.For(500).Name);
            Assert.AreEqual("Technician", TraineeRank.For(1500).Name);
            Assert.AreEqual("Specialist", TraineeRank.For(6999).Name);
            Assert.AreEqual("Safety lead", TraineeRank.For(7000).Name);
            Assert.AreEqual("Trainee", TraineeRank.For(-5).Name, "no negative XP");
        }

        [Test]
        public void ProgressToTheNextRank()
        {
            (string _, int nextAt, float progress) = TraineeRank.For(1000);
            Assert.AreEqual(1500, nextAt);
            Assert.AreEqual(0.5f, progress, 1e-5f);
            (string top, int none, float full) = TraineeRank.For(20000);
            Assert.AreEqual("Safety lead", top);
            Assert.AreEqual(0, none);
            Assert.AreEqual(1f, full);
        }

        [Test]
        public void DailyChallengeIsTheSameAllDayAndChanges()
        {
            var morning = new DateTime(2026, 11, 13, 1, 0, 0, DateTimeKind.Utc);
            var evening = new DateTime(2026, 11, 13, 23, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(TraineeProgress.Daily(morning), TraineeProgress.Daily(evening));
            bool changes = false;
            for (int d = 1; d < 8; d++)
                changes |= !TraineeProgress.Daily(morning.AddDays(d)).Equals(TraineeProgress.Daily(morning));
            Assert.IsTrue(changes, "the challenge moves on over a week");
        }

        [Test]
        public void DailyPaysDoubleOnceAndOtherRunsPayPoints()
        {
            var day = new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
            (TrainingArea area, int level) = TraineeProgress.Daily(day);
            var state = new ProgressState();
            XpGain first = TraineeProgress.Gain(state, area, level, 300, day);
            Assert.IsTrue(first.Daily);
            Assert.AreEqual(600, first.Xp);
            XpGain again = TraineeProgress.Gain(state, area, level, 300, day.AddHours(1));
            Assert.IsFalse(again.Daily, "once a day");
            Assert.AreEqual(300, again.Xp);
            TrainingArea other = area == TrainingArea.Fire ? TrainingArea.Lockout : TrainingArea.Fire;
            Assert.AreEqual(100, TraineeProgress.Gain(state, other, 0, 100, day).Xp);
            Assert.AreEqual(1000, state.xp);
            Assert.IsFalse(TraineeProgress.Gain(new ProgressState(), area, level, 0, day).Daily, "a run with no points is not the challenge");
        }

        [Test]
        public void StreakCountsConsecutiveDays()
        {
            var state = new ProgressState();
            var day = new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(1, TraineeProgress.Gain(state, TrainingArea.Fire, 0, 10, day).Streak);
            Assert.AreEqual(1, TraineeProgress.Gain(state, TrainingArea.Fire, 0, 10, day.AddHours(3)).Streak, "same day");
            Assert.AreEqual(2, TraineeProgress.Gain(state, TrainingArea.Fire, 0, 10, day.AddDays(1)).Streak);
            Assert.AreEqual(1, TraineeProgress.Gain(state, TrainingArea.Fire, 0, 10, day.AddDays(3)).Streak, "a day missed");
        }
    }
}
