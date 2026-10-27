using NUnit.Framework;
using TwoDude.FactoryTwin.UI;
using UnityEngine;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class TurnMathTests
    {
        [Test]
        public void Twist_IsTheRollAboutTheAxis()
        {
            // Rolling the controller's up from world up toward -Z about +X is -90 degrees.
            Assert.AreEqual(-90f, TurnMath.Twist(Vector3.right, Vector3.up, Vector3.back), 1e-3f);
            Assert.AreEqual(90f, TurnMath.Twist(Vector3.right, Vector3.up, Vector3.forward), 1e-3f);
        }

        [Test]
        public void Twist_IgnoresMovementAlongTheAxis()
        {
            Vector3 tilted = (Vector3.up + Vector3.right).normalized;
            Assert.AreEqual(0f, TurnMath.Twist(Vector3.right, Vector3.up, tilted), 1e-3f);
            Assert.AreEqual(0f, TurnMath.Twist(Vector3.right, Vector3.right, Vector3.up), 1e-3f);
        }

        [Test]
        public void Clamp_HoldsTheHandleBetweenItsEnds_EitherWayRound()
        {
            Assert.AreEqual(-90f, TurnMath.Clamp(-130f, 0f, -90f));
            Assert.AreEqual(0f, TurnMath.Clamp(25f, 0f, -90f));
            Assert.AreEqual(-40f, TurnMath.Clamp(-40f, 0f, -90f));
            Assert.AreEqual(90f, TurnMath.Clamp(120f, 0f, 90f));
        }

        [Test]
        public void Release_SnapsToTheNearerEnd()
        {
            Assert.IsTrue(TurnMath.NearerTurned(-60f, 0f, -90f));
            Assert.IsFalse(TurnMath.NearerTurned(-30f, 0f, -90f));
            Assert.IsTrue(TurnMath.NearerTurned(50f, 0f, 90f));
        }
    }
}
