using NUnit.Framework;
using TwoDude.FactoryTwin.UI;
using UnityEngine;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class ProceduralAudioTests
    {
        [Test]
        public void Hiss_IsTheRightLength_WithinLevel_AndLoopsWithoutAJump()
        {
            float[] s = ProceduralAudio.HissSamples(1f, 0.45f, 0.5f, 7);

            Assert.AreEqual(ProceduralAudio.SampleRate, s.Length);
            foreach (float v in s)
                Assert.That(Mathf.Abs(v), Is.LessThanOrEqualTo(0.5f + 1e-5f));
            // The cross-fade makes the wrap from the last sample to the first no bigger than a normal step.
            float typicalStep = 0f;
            for (int i = 1; i < s.Length; i++)
                typicalStep = Mathf.Max(typicalStep, Mathf.Abs(s[i] - s[i - 1]));
            Assert.That(Mathf.Abs(s[0] - s[s.Length - 1]), Is.LessThanOrEqualTo(typicalStep * 1.5f));
        }

        [Test]
        public void Hiss_IsRepeatableForASeed()
        {
            CollectionAssert.AreEqual(ProceduralAudio.HissSamples(0.1f, 0.3f, 0.4f, 3), ProceduralAudio.HissSamples(0.1f, 0.3f, 0.4f, 3));
        }

        [Test]
        public void Click_RingsDown()
        {
            float[] s = ProceduralAudio.ClickSamples(2600f, 0.12f, 0.6f);
            float early = 0f, late = 0f;
            for (int i = 0; i < s.Length / 10; i++)
                early = Mathf.Max(early, Mathf.Abs(s[i]));
            for (int i = s.Length * 9 / 10; i < s.Length; i++)
                late = Mathf.Max(late, Mathf.Abs(s[i]));
            Assert.That(early, Is.GreaterThan(0.3f));
            Assert.That(late, Is.LessThan(early * 0.05f));
        }
    }
}
