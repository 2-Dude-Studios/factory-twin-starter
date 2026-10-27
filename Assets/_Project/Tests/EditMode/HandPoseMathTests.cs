using NUnit.Framework;
using TwoDude.FactoryTwin.UI;
using UnityEngine;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class HandPoseMathTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void FromInput_ReleasedIsRelaxed()
        {
            FingerCurls rest = FingerCurls.Relaxed;
            FingerCurls curls = HandPoseMath.FromInput(0f, 0f);
            for (int f = 0; f < HandPoseMath.Fingers; f++)
                Assert.AreEqual(rest[f], curls[f], Tolerance);
        }

        [Test]
        public void FromInput_TriggerBendsTheIndex_GripTheOtherThree()
        {
            FingerCurls trigger = HandPoseMath.FromInput(0f, 1f);
            Assert.Greater(trigger.index, 0.8f);
            Assert.AreEqual(FingerCurls.Relaxed.middle, trigger.middle, Tolerance);

            // Grip alone: the pointing hand, index out, the rest in a fist.
            FingerCurls point = HandPoseMath.FromInput(1f, 0f);
            Assert.AreEqual(FingerCurls.Relaxed.index, point.index, Tolerance);
            Assert.Greater(point.middle, 0.9f);
            Assert.Greater(point.little, 0.9f);
            Assert.Greater(point.thumb, 0.5f);
        }

        [Test]
        public void FromInput_ClampsOutOfRangeInput()
        {
            FingerCurls over = HandPoseMath.FromInput(3f, -2f);
            FingerCurls full = HandPoseMath.FromInput(1f, 0f);
            Assert.AreEqual(full.middle, over.middle, Tolerance);
            Assert.AreEqual(full.index, over.index, Tolerance);
        }

        [Test]
        public void MoveTowards_StepsEachFingerAtMostTheDelta()
        {
            var from = new FingerCurls(0f, 0f, 0f, 0f, 0f);
            var to = new FingerCurls(1f, 0.05f, 1f, 1f, 1f);
            FingerCurls step = HandPoseMath.MoveTowards(from, to, 0.1f);
            Assert.AreEqual(0.1f, step.thumb, Tolerance);
            Assert.AreEqual(0.05f, step.index, Tolerance);
            Assert.AreEqual(0.1f, step.little, Tolerance);
        }

        [Test]
        public void GripInPalm_IsInFrontOfThePalm_PalmNormalOnX()
        {
            foreach (bool left in new[] { true, false })
            {
                Pose grip = HandPoseMath.GripInPalm(left);
                // In front of the palm: the palm faces -Y in the joint's frame (the back of the hand is +Y).
                Assert.Less(grip.position.y, 0f);
                Vector3 x = grip.rotation * Vector3.right;
                Vector3 palmFacing = Vector3.down;
                // OpenXR's grip: +X away from the palm on the left hand, into it on the right.
                Assert.AreEqual(left ? 1f : -1f, Vector3.Dot(x, palmFacing), Tolerance);
            }
        }

        [Test]
        public void GripInPalm_ZPointsTowardTheThumb()
        {
            // With the XR Hands axes the thumb is on +X for the left hand and on -X for the right.
            Assert.AreEqual(1f, Vector3.Dot(HandPoseMath.GripInPalm(true).rotation * Vector3.forward, Vector3.right), Tolerance);
            Assert.AreEqual(1f, Vector3.Dot(HandPoseMath.GripInPalm(false).rotation * Vector3.forward, Vector3.left), Tolerance);
        }

        [Test]
        public void AnchorFor_TurnsTheLeftHandHalfAboutTheFist()
        {
            Quaternion right = Quaternion.LookRotation(Vector3.up, Vector3.back);
            Quaternion left = HandPoseMath.AnchorFor(true, right);
            Assert.AreEqual(1f, Vector3.Dot(left * Vector3.forward, right * Vector3.forward), Tolerance);
            Assert.AreEqual(-1f, Vector3.Dot(left * Vector3.right, right * Vector3.right), Tolerance);
            Assert.AreEqual(right, HandPoseMath.AnchorFor(false, right));
        }

        [Test]
        public void InverseAndMultiply_RoundTrip()
        {
            var pose = new Pose(new Vector3(0.1f, -0.2f, 0.3f), Quaternion.Euler(10f, 70f, -30f));
            Pose identity = HandPoseMath.Multiply(pose, HandPoseMath.Inverse(pose));
            Assert.AreEqual(0f, identity.position.magnitude, Tolerance);
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(identity.rotation, Quaternion.identity)), Tolerance);
        }

        [Test]
        public void MaxBend_FingersBendMoreThanTheThumb()
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.Greater(HandPoseMath.MaxBend(1, j), 0f);
                Assert.LessOrEqual(HandPoseMath.MaxBend(0, j), HandPoseMath.MaxBend(1, j));
            }
        }
    }
}
