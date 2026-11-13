using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class TraineeRankTests
    {
        [Test]
        public void RanksClimbWithStars()
        {
            Assert.AreEqual("Trainee", TraineeRank.For(0).Name);
            Assert.AreEqual("Trainee", TraineeRank.For(3).Name);
            Assert.AreEqual("Operator", TraineeRank.For(4).Name);
            Assert.AreEqual("Technician", TraineeRank.For(10).Name);
            Assert.AreEqual("Specialist", TraineeRank.For(31).Name);
            Assert.AreEqual("Safety lead", TraineeRank.For(32).Name);
            Assert.AreEqual("Trainee", TraineeRank.For(-5).Name, "no negative stars");
        }

        [Test]
        public void ProgressToTheNextRank()
        {
            (string _, int nextAt, float progress) = TraineeRank.For(7);
            Assert.AreEqual(10, nextAt);
            Assert.AreEqual(0.5f, progress, 1e-5f);
            (string top, int none, float full) = TraineeRank.For(48);
            Assert.AreEqual("Safety lead", top);
            Assert.AreEqual(0, none);
            Assert.AreEqual(1f, full);
        }
    }
}
