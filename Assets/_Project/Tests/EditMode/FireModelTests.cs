using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class FireModelTests
    {
        private const double Dt = 0.1;

        // Defaults: 40 s precursor, 12 s smoke, ignition at 0.05, incipient 45 s to 0.3, growing 25 s to 0.6.
        private static FireModel Fire(FireClass fireClass = FireClass.A, bool precursor = false) =>
            new FireModel(new FireModelSettings(), fireClass, precursor);

        private static void RunUntil(FireModel fire, double elapsed, FireAgent agent = FireAgent.None, double aim = 1d, double standoff = 1d)
        {
            bool first = true;
            while (fire.ElapsedSeconds < elapsed - 1e-9)
            {
                fire.Tick(Dt, agent, aim, standoff, first && agent != FireAgent.None);
                first = false;
            }
        }

        private static void RunFor(FireModel fire, double seconds, FireAgent agent = FireAgent.None, double aim = 1d, double standoff = 1d) =>
            RunUntil(fire, fire.ElapsedSeconds + seconds, agent, aim, standoff);

        // Runs a fire with no precursor to just after ignition (12 s of smoke).
        private static FireModel Ignited(FireClass fireClass = FireClass.A)
        {
            FireModel fire = Fire(fireClass);
            RunUntil(fire, 12.5);
            Assume.That(fire.Stage, Is.EqualTo(FireStage.Incipient));
            return fire;
        }

        [Test]
        public void WithPrecursor_StagesFollowTheSchedule()
        {
            FireModel fire = Fire(FireClass.C, precursor: true);

            RunUntil(fire, 15);
            Assert.AreEqual(FireStage.None, fire.Stage);
            Assert.That(fire.TemperatureC, Is.GreaterThan(32).And.LessThan(60));
            Assert.IsFalse(fire.Overheating);

            RunUntil(fire, 20);
            Assert.IsTrue(fire.Overheating, "60 °C is passed about 18 s in");

            RunUntil(fire, 39.5);
            Assert.AreEqual(FireStage.None, fire.Stage);
            RunUntil(fire, 41);
            Assert.AreEqual(FireStage.Smoke, fire.Stage);
            Assert.AreEqual(95d, fire.TemperatureC, 1e-6);

            RunUntil(fire, 51.5);
            Assert.AreEqual(FireStage.Smoke, fire.Stage);
            RunUntil(fire, 53);
            Assert.AreEqual(FireStage.Incipient, fire.Stage);

            RunUntil(fire, 96);
            Assert.AreEqual(FireStage.Incipient, fire.Stage);
            RunUntil(fire, 98.5);
            Assert.AreEqual(FireStage.Growing, fire.Stage);

            RunUntil(fire, 121);
            Assert.AreEqual(FireStage.Growing, fire.Stage);
            RunUntil(fire, 123.5);
            Assert.AreEqual(FireStage.BeyondIncipient, fire.Stage);
        }

        [Test]
        public void WithoutPrecursor_SmokesAtOnce_AtAmbient()
        {
            FireModel fire = Fire();
            fire.Tick(Dt);

            Assert.AreEqual(FireStage.Smoke, fire.Stage);
            Assert.AreEqual(32d, fire.TemperatureC, 1e-9);
            Assert.IsFalse(fire.Overheating);
        }

        [Test]
        public void Smoke_RisesThroughTheSmokeStage_ThenFollowsTheFlame()
        {
            FireModel fire = Fire();
            RunUntil(fire, 6);
            Assert.That(fire.Smoke, Is.GreaterThan(0.1).And.LessThan(0.35));

            RunUntil(fire, 100);
            Assert.That(fire.Smoke, Is.GreaterThan(0.35 + 0.65 * 0.3 - 1e-6));
        }

        [Test]
        public void RightAgent_KnocksDownAnIncipientFire_AndSoakingStopsReflash()
        {
            FireModel fire = Ignited(FireClass.A);

            RunFor(fire, 1.5, FireAgent.Water);
            Assert.AreEqual(FireStage.Out, fire.Stage);
            Assert.IsFalse(fire.Extinguished, "knocked down, not yet soaked");

            RunFor(fire, 2.5, FireAgent.Water);
            Assert.IsTrue(fire.Extinguished);

            RunFor(fire, 30);
            Assert.AreEqual(FireStage.Out, fire.Stage);
            Assert.AreEqual(0, fire.Reflashes);
            Assert.AreEqual(0, fire.WrongAgentUses);
        }

        [Test]
        public void StoppingAsSoonAsItIsDown_LetsItReflash()
        {
            FireModel fire = Ignited(FireClass.A);
            RunFor(fire, 1.5, FireAgent.DryChemicalAbc);
            Assume.That(fire.Stage, Is.EqualTo(FireStage.Out));

            RunFor(fire, 5);
            Assert.AreEqual(FireStage.Out, fire.Stage);
            RunFor(fire, 1.5);
            Assert.AreEqual(FireStage.Incipient, fire.Stage, "reflashes 6 s after knock-down");
            Assert.AreEqual(1, fire.Reflashes);
        }

        [Test]
        public void WaterOnALiveElectricalFire_SpreadsIt_AndCountsOnce()
        {
            FireModel wrong = Ignited(FireClass.C);
            FireModel left = Ignited(FireClass.C);

            RunFor(wrong, 5, FireAgent.Water);
            RunFor(left, 5);

            Assert.That(wrong.Size, Is.GreaterThan(left.Size + 0.1));
            Assert.AreEqual(1, wrong.WrongAgentUses);
        }

        [Test]
        public void PastIncipient_AnExtinguisherCannotWin()
        {
            FireModel fire = Ignited(FireClass.A);
            RunFor(fire, 72);
            Assume.That(fire.Stage, Is.EqualTo(FireStage.BeyondIncipient));
            double before = fire.Size;

            RunFor(fire, 10, FireAgent.DryChemicalAbc);

            Assert.AreEqual(FireStage.BeyondIncipient, fire.Stage);
            Assert.That(fire.Size, Is.GreaterThanOrEqualTo(before));
        }

        [Test]
        public void AimingAway_DoesNothing()
        {
            FireModel aimed = Ignited();
            FireModel left = Ignited();

            RunFor(aimed, 5, FireAgent.Water, aim: 0d);
            RunFor(left, 5);

            Assert.AreEqual(left.Size, aimed.Size, 1e-9);
            Assert.AreEqual(FireStage.Incipient, aimed.Stage);
        }

        [Test]
        public void PoorStandoff_SlowsTheKnockdown()
        {
            FireModel close = Ignited();
            FireModel far = Ignited();

            RunFor(close, 0.5, FireAgent.Water, standoff: 1d);
            RunFor(far, 0.5, FireAgent.Water, standoff: 0.3d);

            Assert.That(far.Size, Is.GreaterThan(close.Size));
        }

        [Test]
        public void DeEnergising_TurnsClassCIntoClassA_SoWaterWorks()
        {
            FireModel fire = Ignited(FireClass.C);
            Assert.IsTrue(fire.Energised);

            fire.DeEnergise();
            RunFor(fire, 4, FireAgent.Water);

            Assert.AreEqual(FireClass.A, fire.Class);
            Assert.IsFalse(fire.Energised);
            Assert.IsTrue(fire.Extinguished);
            Assert.AreEqual(0, fire.WrongAgentUses);
        }

        [TestCase(FireAgent.Water, FireClass.A, 1d)]
        [TestCase(FireAgent.Water, FireClass.B, -1d)]
        [TestCase(FireAgent.Water, FireClass.C, -1d)]
        [TestCase(FireAgent.Co2, FireClass.B, 1d)]
        [TestCase(FireAgent.Co2, FireClass.C, 1d)]
        [TestCase(FireAgent.Co2, FireClass.A, 0.3d)]
        [TestCase(FireAgent.DryChemicalAbc, FireClass.A, 1d)]
        [TestCase(FireAgent.DryChemicalAbc, FireClass.B, 1d)]
        [TestCase(FireAgent.DryChemicalAbc, FireClass.C, 1d)]
        [TestCase(FireAgent.None, FireClass.A, 0d)]
        public void Effectiveness_FollowsTheExtinguisherClasses(FireAgent agent, FireClass fireClass, double expected)
        {
            Assert.AreEqual(expected, FireModel.Effectiveness(agent, fireClass), 1e-9);
        }

        [Test]
        public void SettingsConstructor_KeepsDefaultsForWhatItIsNotGiven()
        {
            var settings = new FireModelSettings(incipientSeconds: 10);

            Assert.AreEqual(10d, settings.IncipientSeconds);
            Assert.AreEqual(12d, settings.SmokeSeconds);
            Assert.AreEqual(25d, settings.GrowingSeconds);
        }

        [Test]
        public void ShortIncipient_GrowsPastIncipientSooner()
        {
            var fire = new FireModel(new FireModelSettings(smokeSeconds: 5, incipientSeconds: 15, growingSeconds: 10), FireClass.B, false);
            RunUntil(fire, 5 + 15 + 10 + 1.5);

            Assert.AreEqual(FireStage.BeyondIncipient, fire.Stage);
        }
    }
}
