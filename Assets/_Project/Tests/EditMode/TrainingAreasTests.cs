using System.Collections.Generic;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;
using TwoDude.FactoryTwin.UI;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class TrainingAreasTests
    {
        // The drive cabinet's exposed parts in its own space (ElectricalSetup: centre (0, 1.05, 0.005), size 0.74 × 1.84 × 0.40).
        private const double ExposedMinX = -0.37, ExposedMaxX = 0.37, ExposedBackZ = -0.195, ExposedFrontZ = 0.205;

        [Test]
        public void Defaults_AreTheHubAndOneAreaPerDrill_AndSound()
        {
            TrainingAreaSettings[] areas = TrainingAreaDefaults.All();
            Assert.AreEqual(5, areas.Length);
            foreach (TrainingArea area in System.Enum.GetValues(typeof(TrainingArea)))
                Assert.IsNotNull(TrainingAreaRules.Find(areas, area), area.ToString());
            CollectionAssert.IsEmpty(TrainingAreaRules.Problems(areas));
        }

        [Test]
        public void NoTwoAreasShareFloor_SoNothingFromAnotherDrillSitsInOne()
        {
            TrainingAreaSettings[] areas = TrainingAreaDefaults.All();
            for (int i = 0; i < areas.Length; i++)
            {
                for (int j = i + 1; j < areas.Length; j++)
                    Assert.IsFalse(TrainingAreaRules.Overlap(areas[i], areas[j]), $"{areas[i].Title} / {areas[j].Title}");
                // Each area's own machine and start are in it and in no other.
                Assert.AreSame(areas[i], TrainingAreaRules.At(areas, areas[i].Start.X, areas[i].Start.Z), areas[i].Title);
                if (areas[i].HasMachine)
                    Assert.AreSame(areas[i], TrainingAreaRules.At(areas, areas[i].Machine.X, areas[i].Machine.Z), areas[i].Title);
            }
        }

        [Test]
        public void Problems_SayWhatIsWrong()
        {
            var a = new TrainingAreaSettings(TrainingArea.Fire, "A", TrainingAreaDefaults.FireColour, 0f, 4f, 0f, 4f, new AreaSpot(2f, 2f));
            var b = new TrainingAreaSettings(TrainingArea.Lockout, "B", TrainingAreaDefaults.LockoutColour, 3f, 7f, 3f, 7f, new AreaSpot(3.2f, 5f))
                .WithBoard(9f, 5f);
            List<string> problems = TrainingAreaRules.Problems(new[] { a, b });
            CollectionAssert.Contains(problems, "B and A share floor.");
            CollectionAssert.Contains(problems, "B: the start spot is not at least 0.5 m inside its border.");
            CollectionAssert.Contains(problems, "B: the score board is outside its border.");
            Assert.AreEqual(3, problems.Count);

            // Touching borders are fine.
            var c = new TrainingAreaSettings(TrainingArea.Hub, "C", TrainingAreaDefaults.HubColour, 4f, 8f, 0f, 3f, new AreaSpot(6f, 1.5f));
            CollectionAssert.IsEmpty(TrainingAreaRules.Problems(new[] { a, c }));
            CollectionAssert.Contains(TrainingAreaRules.Problems(new TrainingAreaSettings[0]), "No areas.");
        }

        [Test]
        public void RobotArea_StartsAndPostsAreOutsideTheWarningZone()
        {
            // SafetyZoneSetup: the warning zone is ±2.2 m round the arm's base at (−2.0, 1.2).
            TrainingAreaSettings robot = TrainingAreaDefaults.For(TrainingArea.RobotCell);
            foreach (AreaSpot spot in new[] { robot.Start, robot.Briefing, robot.Board })
            {
                bool inside = spot.X > -4.2f && spot.X < 0.2f && spot.Z > -1.0f && spot.Z < 3.4f;
                Assert.IsFalse(inside, $"({spot.X}, {spot.Z})");
            }
            Assert.IsTrue(robot.Machine.X == -2.0f && robot.Machine.Z == 1.2f, "the arm");
        }

        [Test]
        public void ToHall_TurnsTheCabinetsOwnSpaceIntoTheHalls()
        {
            // The cabinet faces south (yaw 180): its right is the hall's west, its front the hall's south.
            AreaSpot cabinet = TrainingAreaDefaults.For(TrainingArea.Electrical).Machine;
            TrainingAreaRules.ToHall(cabinet, ExposedMaxX, ExposedFrontZ, out double x, out double z);
            Assert.AreEqual(7.0 - 0.37, x, 1e-6);
            Assert.AreEqual(0.55 - 0.205, z, 1e-6);
            TrainingAreaRules.ToHall(new AreaSpot(1f, 2f, 90f), 0, 1, out x, out z);
            Assert.AreEqual(2.0, x, 1e-6, "front of a machine facing east");
            Assert.AreEqual(2.0, z, 1e-6);
        }

        [Test]
        public void ElectricalBay_HoldsAllThreeBoundaryRings_AndItsStationsAreOutsideThem()
        {
            TrainingAreaSettings[] areas = TrainingAreaDefaults.All();
            TrainingAreaSettings bay = TrainingAreaRules.Find(areas, TrainingArea.Electrical);
            var study = new ArcFlashValues();
            double arcFlashM = study.ArcFlashBoundaryMm / 1000d;

            double[] ring = ApproachBoundaries.Outline(ExposedMinX, ExposedMaxX, ExposedBackZ, ExposedFrontZ, arcFlashM, 8);
            for (int i = 0; i < ring.Length / 2; i++)
            {
                TrainingAreaRules.ToHall(bay.Machine, ring[i * 2], ring[i * 2 + 1], out double x, out double z);
                Assert.IsTrue(TrainingAreaRules.Contains(bay, x, z), $"ring point {i} at ({x:F2}, {z:F2})");
                Assert.AreSame(bay, TrainingAreaRules.At(areas, x, z));
            }

            // The exposed parts as a box in the hall, and everything the trainee stands at before gearing up.
            TrainingAreaRules.ToHall(bay.Machine, ExposedMinX, ExposedBackZ, out double ax, out double az);
            TrainingAreaRules.ToHall(bay.Machine, ExposedMaxX, ExposedFrontZ, out double bx, out double bz);
            double minX = System.Math.Min(ax, bx), maxX = System.Math.Max(ax, bx), minZ = System.Math.Min(az, bz), maxZ = System.Math.Max(az, bz);
            foreach (AreaSpot spot in new[] { bay.Start, bay.Briefing, bay.Kit, bay.Board })
            {
                double d = ApproachBoundaries.DistanceToBox(spot.X, 1.0, spot.Z, minX, 0, minZ, maxX, 2, maxZ);
                Assert.Greater(d, arcFlashM + 0.4, $"({spot.X}, {spot.Z}) is {d:F2} m from the exposed parts");
            }
        }

        [Test]
        public void Facing_Compass_AndTheNearestEdge()
        {
            Assert.AreEqual(0d, TrainingAreaRules.YawTowards(0, 0, 0, 5), 1e-9);
            Assert.AreEqual(90d, TrainingAreaRules.YawTowards(0, 0, 5, 0), 1e-9);
            Assert.AreEqual(180d, System.Math.Abs(TrainingAreaRules.YawTowards(0, 0, 0, -5)), 1e-9);
            Assert.AreEqual(-90d, TrainingAreaRules.YawTowards(0, 0, -5, 0), 1e-9);
            Assert.AreEqual(0d, TrainingAreaRules.YawTowards(1, 1, 1, 1), 1e-9);

            Assert.AreEqual("north", TrainingAreaRules.Compass(0, 0, 0.1, 5));
            Assert.AreEqual("south-east", TrainingAreaRules.Compass(0, 0, 3, -3));
            Assert.AreEqual("west", TrainingAreaRules.Compass(0, 0, -4, 0.2));
            Assert.AreEqual("north-west", TrainingAreaRules.Compass(0, 0, -3, 3));

            TrainingAreaSettings bay = TrainingAreaDefaults.For(TrainingArea.Electrical);
            Assert.AreEqual(AreaEdge.South, TrainingAreaRules.NearestEdge(bay, bay.Start.X, bay.Start.Z));
            TrainingAreaSettings fire = TrainingAreaDefaults.For(TrainingArea.Fire);
            Assert.AreEqual(AreaEdge.North, TrainingAreaRules.NearestEdge(fire, fire.Start.X, fire.Start.Z));
            Assert.AreEqual(AreaEdge.East, TrainingAreaRules.NearestEdge(bay, 8.9, -1.5));
            Assert.AreEqual(AreaEdge.West, TrainingAreaRules.NearestEdge(bay, 5.1, -1.5));
        }

        [Test]
        public void EveryDrill_HasFourLevels_EachWithItsRuleAndBadge()
        {
            foreach (TrainingArea area in new[] { TrainingArea.RobotCell, TrainingArea.Lockout, TrainingArea.Fire, TrainingArea.Electrical })
            {
                IReadOnlyList<DrillLevel> levels = DrillLevels.For(area);
                Assert.AreEqual(DrillLevels.Count, levels.Count, area.ToString());
                for (int i = 0; i < levels.Count; i++)
                {
                    Assert.AreEqual(i + 1, levels[i].Number);
                    Assert.AreEqual(DrillLevels.LevelName(i + 1), levels[i].Name);
                    Assert.IsNotEmpty(levels[i].Activity);
                    Assert.IsNotEmpty(levels[i].Clauses);
                    Assert.IsNotEmpty(levels[i].Badge);
                }
                Assert.IsNotEmpty(DrillLevels.Standard(area));
            }
            Assert.AreEqual(0, DrillLevels.For(TrainingArea.Hub).Count);
            Assert.AreEqual("Assessment", DrillLevels.LevelName(4));
            Assert.AreEqual(string.Empty, DrillLevels.LevelName(5));
        }

        [Test]
        public void Briefing_NamesTheRulesAndTheLevels_AndSaysItIsADemo()
        {
            string text = TrainingAreaText.Briefing(TrainingArea.Electrical);
            StringAssert.StartsWith("Trains to: OSHA 29 CFR 1910.332-.335, NFPA 70E-2027", text);
            StringAssert.Contains("LEVEL 1  AWARENESS", text);
            StringAssert.Contains("LEVEL 4  ASSESSMENT", text);
            StringAssert.Contains("Badge: Backfeed hunter", text);
            StringAssert.EndsWith(DrillLevels.DemoNotice, text);
            StringAssert.DoesNotContain("compliant", text.ToLowerInvariant());
            StringAssert.Contains("1910.147", TrainingAreaText.Briefing(TrainingArea.Lockout));
            Assert.AreEqual(string.Empty, TrainingAreaText.Briefing(TrainingArea.Hub));
            StringAssert.Contains("A critical mistake fails the level, whatever the points.", TrainingAreaText.Stars());
            Assert.AreEqual("ELECTRICAL SAFETY", TrainingAreaText.Sign(TrainingAreaDefaults.For(TrainingArea.Electrical)));
        }

        [Test]
        public void HallMap_GivesEachDrillsDistanceAndDirectionFromTheHub()
        {
            TrainingAreaSettings[] areas = TrainingAreaDefaults.All();
            TrainingAreaSettings hub = TrainingAreaRules.Find(areas, TrainingArea.Hub);
            string[] lines = TrainingAreaText.HallMap(areas, hub.Start.X, hub.Start.Z).Split('\n');
            Assert.AreEqual(4, lines.Length);
            CollectionAssert.Contains(lines, "Fire safety: 7 m east");
            CollectionAssert.Contains(lines, "Lockout / tagout: 18 m north-east");
            CollectionAssert.Contains(lines, "Robot cell safety: 12 m north-east");
            CollectionAssert.Contains(lines, "Electrical safety: 19 m east");
        }

        [Test]
        public void EachTraining_StartsInItsOwnArea()
        {
            Assert.AreEqual(TrainingArea.Fire, TrainingLaunch.AreaFor(TrainingMode.FireSafety));
            Assert.AreEqual(TrainingArea.Lockout, TrainingLaunch.AreaFor(TrainingMode.Lockout));
            Assert.AreEqual(TrainingArea.RobotCell, TrainingLaunch.AreaFor(TrainingMode.RobotZone));
            Assert.AreEqual(TrainingArea.Electrical, TrainingLaunch.AreaFor(TrainingMode.Electrical));
            Assert.AreEqual(TrainingArea.Hub, TrainingLaunch.AreaFor(TrainingMode.FreeRoam));
            Assert.AreEqual("Electrical safety", TrainingLaunch.Title(TrainingMode.Electrical));
        }
    }
}
