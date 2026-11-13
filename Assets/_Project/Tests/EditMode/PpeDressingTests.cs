using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class PpeDressingTests
    {
        [Test]
        public void KitIsCategoryTwoWithGlovesLast()
        {
            Assert.AreEqual(2, PpeDressing.Category);
            Assert.AreEqual(6, PpeDressing.Kit.Count);
            StringAssert.Contains("balaclava", PpeDressing.Kit[0].Name.ToLowerInvariant());
            StringAssert.Contains("gloves", PpeDressing.Kit[PpeDressing.Kit.Count - 1].Name.ToLowerInvariant());
            Assert.AreEqual(PpeZone.Body, PpeDressing.Kit[PpeDressing.Kit.Count - 1].Zone);
        }

        [Test]
        public void ItemsGoOnInOrderOnly()
        {
            var dressing = new PpeDressing();
            Assert.IsFalse(dressing.TryWear(1), "the glasses before the balaclava");
            Assert.AreEqual(0, dressing.Worn);
            Assert.IsTrue(dressing.TryWear(0));
            Assert.IsFalse(dressing.TryWear(0), "the same item twice");
            Assert.IsFalse(dressing.TryWear(5), "the gloves out of turn");
            Assert.AreEqual(1, dressing.Worn);
            Assert.AreSame(PpeDressing.Kit[1], dressing.Next);
        }

        [Test]
        public void CompleteKitThenReset()
        {
            var dressing = new PpeDressing();
            for (int i = 0; i < PpeDressing.Kit.Count; i++)
                Assert.IsTrue(dressing.TryWear(i));
            Assert.IsTrue(dressing.Complete);
            Assert.IsNull(dressing.Next);
            Assert.IsFalse(dressing.TryWear(PpeDressing.Kit.Count), "nothing after the kit");
            dressing.Reset();
            Assert.AreEqual(0, dressing.Worn);
            Assert.IsFalse(dressing.Complete);
        }
    }
}
