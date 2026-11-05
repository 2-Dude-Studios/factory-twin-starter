using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class ArcFlashStudyTests
    {
        [Test]
        public void ShockBoundaries_FollowTable130_4Ea()
        {
            ShockBoundaries v480 = ShockBoundaries.ForVoltage(480);
            Assert.AreEqual(1000, v480.LimitedMm);
            Assert.AreEqual(42, v480.LimitedInches);
            Assert.AreEqual(300, v480.RestrictedMm);
            Assert.AreEqual(12, v480.RestrictedInches);

            ShockBoundaries v120 = ShockBoundaries.ForVoltage(120);
            Assert.AreEqual(1000, v120.LimitedMm);
            Assert.AreEqual(0, v120.RestrictedMm, "avoid contact");

            Assert.Throws<System.ArgumentOutOfRangeException>(() => ShockBoundaries.ForVoltage(24));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ShockBoundaries.ForVoltage(4160));
        }

        [Test]
        public void Classify_GivesTheDeepestBoundary()
        {
            var study = new ArcFlashValues(); // AFB 1.37 m, limited 1.0 m, restricted 0.3 m
            Assert.AreEqual(ApproachBoundary.Outside, ApproachBoundaries.Classify(1500, study));
            Assert.AreEqual(ApproachBoundary.Outside, ApproachBoundaries.Classify(1370, study));
            Assert.AreEqual(ApproachBoundary.ArcFlash, ApproachBoundaries.Classify(1369, study));
            Assert.AreEqual(ApproachBoundary.ArcFlash, ApproachBoundaries.Classify(1000, study));
            Assert.AreEqual(ApproachBoundary.Limited, ApproachBoundaries.Classify(999, study));
            Assert.AreEqual(ApproachBoundary.Limited, ApproachBoundaries.Classify(300, study));
            Assert.AreEqual(ApproachBoundary.Restricted, ApproachBoundaries.Classify(299, study));
            Assert.AreEqual(ApproachBoundary.Restricted, ApproachBoundaries.Classify(0, study));

            Assert.AreEqual(ApproachBoundary.Limited, ApproachBoundaries.Deepest(ApproachBoundary.ArcFlash, ApproachBoundary.Limited));
            Assert.AreEqual(ApproachBoundary.Restricted, ApproachBoundaries.Deepest(ApproachBoundary.Restricted, ApproachBoundary.Outside));
        }

        [Test]
        public void DistanceToBox_IsZeroInside_AndEuclideanOutside()
        {
            Assert.AreEqual(0d, ApproachBoundaries.DistanceToBox(0.1, 0.5, 0.1, 0, 0, 0, 1, 2, 0.4), 1e-12);
            Assert.AreEqual(0.3d, ApproachBoundaries.DistanceToBox(0.5, 1, 0.7, 0, 0, 0, 1, 2, 0.4), 1e-12, "in front of the face");
            Assert.AreEqual(0.5d, ApproachBoundaries.DistanceToBox(1.3, 2.4, 0.2, 0, 0, 0, 1, 2, 0.4), 1e-12, "off a corner edge: 3-4-5");
        }

        [Test]
        public void DistanceToExposedParts_IgnoresPointsBehindTheCabinet()
        {
            // Box x ±0.37, y 0..1.8, z −0.2..0.2 (door on +Z).
            Assert.AreEqual(0.5d, ApproachBoundaries.DistanceToExposedParts(0, 1, 0.7, -0.37, 0, -0.2, 0.37, 1.8, 0.2), 1e-9);
            Assert.AreEqual(0.13d, ApproachBoundaries.DistanceToExposedParts(0.5, 1, 0, -0.37, 0, -0.2, 0.37, 1.8, 0.2), 1e-9, "beside it");
            Assert.IsTrue(double.IsPositiveInfinity(ApproachBoundaries.DistanceToExposedParts(0, 1, -0.5, -0.37, 0, -0.2, 0.37, 1.8, 0.2)), "behind its back");
        }

        [Test]
        public void Outline_IsAUOpenAtTheBack_AtTheBoundaryDistance()
        {
            double[] p = ApproachBoundaries.Outline(-0.37, 0.37, -0.2, 0.2, 1.0, 4);
            int n = p.Length / 2;
            Assert.AreEqual(2 + 2 * 5, n);
            Assert.AreEqual(-1.37, p[0], 1e-9);
            Assert.AreEqual(-0.2, p[1], 1e-9, "starts at the back left");
            Assert.AreEqual(1.37, p[(n - 1) * 2], 1e-9);
            Assert.AreEqual(-0.2, p[(n - 1) * 2 + 1], 1e-9, "ends at the back right");
            // Every point in front of the back line is the boundary distance from the box's footprint.
            for (int i = 0; i < n; i++)
            {
                double d = ApproachBoundaries.DistanceToBox(p[i * 2], 0.5, p[i * 2 + 1], -0.37, 0, -0.2, 0.37, 1, 0.2);
                Assert.AreEqual(1.0, d, 1e-9, $"point {i}");
            }
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ApproachBoundaries.Outline(0, 1, 0, 1, 1, 0));
        }

        [Test]
        public void PpeCategory_IsTheLowestKitThatCoversTheIncidentEnergy()
        {
            Assert.AreEqual(1, ApproachBoundaries.PpeCategoryFor(1.2));
            Assert.AreEqual(1, ApproachBoundaries.PpeCategoryFor(4));
            Assert.AreEqual(2, ApproachBoundaries.PpeCategoryFor(6.1));
            Assert.AreEqual(3, ApproachBoundaries.PpeCategoryFor(8.1));
            Assert.AreEqual(4, ApproachBoundaries.PpeCategoryFor(40));
            Assert.AreEqual(0, ApproachBoundaries.PpeCategoryFor(41), "no kit is enough");

            var study = new ArcFlashValues();
            Assert.AreEqual(2, study.RequiredPpeCategory);
            Assert.AreEqual(7d, study.MinimumArcRatingCal);
        }

        [Test]
        public void Length_PrintsMetricThenFeetAndInches()
        {
            Assert.AreEqual("1.37 m (4 ft 6 in)", ArcFlashLabel.Length(1370));
            Assert.AreEqual("455 mm (18 in)", ArcFlashLabel.WorkingDistance(455));
            Assert.AreEqual("610 mm (24 in)", ArcFlashLabel.WorkingDistance(610));
            Assert.AreEqual("1.00 m (3 ft 6 in)", ArcFlashLabel.Length(1000, 42), "the table's own inches");
            Assert.AreEqual("300 mm (1 ft 0 in)", ArcFlashLabel.Length(300, 12));
        }

        [Test]
        public void Label_IsMadeFromTheStudy_AndSaysItIsDemoData()
        {
            string text = ArcFlashLabel.Text(new ArcFlashValues());
            string[] lines = text.Split('\n');

            Assert.AreEqual("WARNING", lines[0]);
            StringAssert.Contains("Drive cabinet DCP-1", text);
            StringAssert.Contains("Arc flash boundary: 1.37 m (4 ft 6 in)", text);
            StringAssert.Contains("Incident energy: 6.1 cal/cm² at 455 mm (18 in)", text);
            StringAssert.Contains("Minimum arc rating of clothing: 7 cal/cm²", text);
            StringAssert.Contains("Nominal voltage: 480 V AC, 3 phase", text);
            StringAssert.Contains("Limited approach: 1.00 m (3 ft 6 in)", text);
            StringAssert.Contains("Restricted approach: 300 mm (1 ft 0 in)", text);
            StringAssert.Contains("Class 0", text);
            StringAssert.Contains("2026-10-01", text);
            Assert.AreEqual(ArcFlashLabel.DemoNotice, lines[lines.Length - 1]);
            StringAssert.DoesNotContain("PPE category", text, "a label shows incident energy or a category, not both");
        }

        [Test]
        public void Label_FollowsTheStudy_AndSaysDangerAbove40Cal()
        {
            var study = new ArcFlashValues("Test panel", 480, 52.3, 610, 6100, "2026-01-01");
            string text = ArcFlashLabel.Text(study);
            Assert.AreEqual("DANGER", text.Split('\n')[0]);
            StringAssert.Contains("Test panel", text);
            StringAssert.Contains("Incident energy: 52.3 cal/cm² at 610 mm (24 in)", text);
            StringAssert.Contains("Arc flash boundary: 6.10 m (20 ft 0 in)", text);
        }
    }
}
