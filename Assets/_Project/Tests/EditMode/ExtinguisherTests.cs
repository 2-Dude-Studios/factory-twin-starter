using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class ExtinguisherTests
    {
        [TestCase(FireAgent.DryChemicalAbc, 2.5, 1d)]
        [TestCase(FireAgent.DryChemicalAbc, 5.0, 0d)]
        [TestCase(FireAgent.DryChemicalAbc, 0.5, 0.4d)]
        [TestCase(FireAgent.Co2, 1.5, 1d)]
        [TestCase(FireAgent.Co2, 3.0, 0.5)]
        [TestCase(FireAgent.Water, 4.0, 1d)]
        [TestCase(FireAgent.Water, 9.5, 0d)]
        [TestCase(FireAgent.None, 2.0, 0d)]
        public void Standoff_IsBestInTheAgentsBand(FireAgent agent, double metres, double expected)
        {
            Assert.AreEqual(expected, ExtinguisherRules.Standoff(agent, metres), 0.01);
        }

        [Test]
        public void Standoff_RisesFromTooCloseToTheBand()
        {
            double near = ExtinguisherRules.Standoff(FireAgent.DryChemicalAbc, 1.0);
            double nearer = ExtinguisherRules.Standoff(FireAgent.DryChemicalAbc, 0.9);
            Assert.That(near, Is.GreaterThan(nearer).And.LessThan(1d));
        }

        [Test]
        public void Aim_FullOnTheBase_NoneWhenWellOff()
        {
            Assert.AreEqual(1d, ExtinguisherRules.Aim(0.2), 1e-9);
            Assert.AreEqual(0d, ExtinguisherRules.Aim(1.5), 1e-9);
            Assert.That(ExtinguisherRules.Aim(0.8), Is.GreaterThan(0d).And.LessThan(1d));
            Assert.AreEqual(0d, ExtinguisherRules.Aim(double.PositiveInfinity), 1e-9);
        }

        [Test]
        public void MissDistance_MeasuresFromTheRay_AndMissesBehind()
        {
            // Ray from the origin along +Z; target 3 m ahead, 0.4 m to the side.
            Assert.AreEqual(0.4, ExtinguisherRules.MissDistance(0, 0, 0, 0, 0, 1, 0.4, 0, 3), 1e-9);
            Assert.AreEqual(0.0, ExtinguisherRules.MissDistance(0, 1, 0, 0, -1, 2, 0, 0, 2), 1e-9);
            Assert.IsTrue(double.IsPositiveInfinity(ExtinguisherRules.MissDistance(0, 0, 0, 0, 0, 1, 0, 0, -3)));
        }

        [Test]
        public void Capacity_FollowsTheSize()
        {
            Assert.That(ExtinguisherRules.CapacitySeconds(FireAgent.Water), Is.GreaterThan(ExtinguisherRules.CapacitySeconds(FireAgent.DryChemicalAbc)));
            Assert.That(ExtinguisherRules.CapacitySeconds(FireAgent.DryChemicalAbc), Is.GreaterThan(ExtinguisherRules.CapacitySeconds(FireAgent.Co2)));
            Assert.AreEqual(0d, ExtinguisherRules.CapacitySeconds(FireAgent.None));
        }

        private static string Advice(FireStage stage, FireClass cls, FireAgent agent, bool alarm = true, bool discharging = false,
            float aim = 1f, float standoff = 1f, float distance = 2.5f, bool flee = false, bool out_ = false, float charge = 1f) =>
            FireDrillText.Advice(stage, cls, flee, out_, alarm, agent, charge, discharging, aim, standoff, distance);

        [Test]
        public void Advice_MostUrgentFirst()
        {
            Assert.AreEqual("Raise the alarm before you fight it.", Advice(FireStage.Incipient, FireClass.B, FireAgent.Co2, alarm: false));
            Assert.AreEqual("Take an extinguisher for a Class B fire from the stand: grip its handle.", Advice(FireStage.Incipient, FireClass.B, FireAgent.None));
            Assert.AreEqual("Water on a Class C fire spreads it. Stop and change extinguisher.", Advice(FireStage.Incipient, FireClass.C, FireAgent.Water));
            Assert.AreEqual("CO2 barely touches a Class A fire. Change extinguisher.", Advice(FireStage.Incipient, FireClass.A, FireAgent.Co2));
            Assert.AreEqual("Aim at the base, then squeeze the lever (trigger on the hand holding it) and sweep.", Advice(FireStage.Incipient, FireClass.A, FireAgent.Water));
            Assert.AreEqual("Too close: step back to 1.8–3.5 m.",
                Advice(FireStage.Incipient, FireClass.B, FireAgent.DryChemicalAbc, discharging: true, standoff: 0.4f, distance: 0.7f));
            Assert.AreEqual("Too far: move in to 1–2.5 m.",
                Advice(FireStage.Incipient, FireClass.C, FireAgent.Co2, discharging: true, standoff: 0.2f, distance: 3.3f));
            Assert.AreEqual("Aim at the base of the fire, not the flames, and sweep.",
                Advice(FireStage.Incipient, FireClass.C, FireAgent.Co2, discharging: true, aim: 0.2f));
            Assert.AreEqual("Good: keep sweeping the base.", Advice(FireStage.Incipient, FireClass.C, FireAgent.Co2, discharging: true));
        }

        [Test]
        public void Advice_PinThenHoseThenSqueeze()
        {
            Assert.AreEqual("Pull the pin: grab its ring with your other hand and pull.",
                FireDrillText.Advice(FireStage.Incipient, FireClass.A, false, false, true, FireAgent.Water, 1f, false, 0f, 0f, 0f, pinPulled: false, hoseHeld: false));
            Assert.AreEqual("Take the hose in your other hand and point it at the base of the fire.",
                FireDrillText.Advice(FireStage.Incipient, FireClass.A, false, false, true, FireAgent.Water, 1f, false, 0f, 0f, 0f, pinPulled: true, hoseHeld: false));
            StringAssert.StartsWith("Water on a Class C fire spreads it",
                FireDrillText.Advice(FireStage.Incipient, FireClass.C, false, false, true, FireAgent.Water, 1f, false, 0f, 0f, 0f, pinPulled: false, hoseHeld: false),
                "the wrong extinguisher is named before the pin");
        }

        [Test]
        public void Advice_FleeAndBeyondIncipient_SayLeave()
        {
            StringAssert.StartsWith("Lithium-ion battery: do not fight it", Advice(FireStage.Incipient, FireClass.C, FireAgent.Co2, flee: true));
            StringAssert.StartsWith("Beyond the incipient stage: stop", Advice(FireStage.BeyondIncipient, FireClass.A, FireAgent.Water));
            StringAssert.StartsWith("Extinguisher empty", Advice(FireStage.Growing, FireClass.A, FireAgent.Water, charge: 0f));
        }

        [Test]
        public void Advice_Out_DependsOnTheSoak()
        {
            Assert.AreEqual("Knocked down: keep the stream on it a moment longer.", Advice(FireStage.Out, FireClass.A, FireAgent.Water));
            Assert.AreEqual("Fire out. Watch it for reflash, then report it.", Advice(FireStage.Out, FireClass.A, FireAgent.Water, out_: true));
        }

        [Test]
        public void FireAndExtinguisherLines()
        {
            Assert.AreEqual("Solvent drums · Class B, flammable liquids · Incipient",
                FireDrillText.Fire(FireLocation.SolventDrums, FireClass.B, FireStage.Incipient, false));
            Assert.AreEqual("Charging station · Lithium-ion battery · Smoke",
                FireDrillText.Fire(FireLocation.Chargers, FireClass.C, FireStage.Smoke, true));
            Assert.AreEqual("No fire", FireDrillText.Fire(FireLocation.None, FireClass.None, FireStage.None, false));
            Assert.AreEqual("CO2 (Class B and C) · 64 %", FireDrillText.Extinguisher(FireAgent.Co2, 0.64f));
            Assert.AreEqual("No extinguisher in hand", FireDrillText.Extinguisher(FireAgent.None, 0f));
        }
    }
}
