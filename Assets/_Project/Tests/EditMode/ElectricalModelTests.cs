using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class ElectricalModelTests
    {
        private const double Tol = 0.05;
        private static readonly double PhaseToGround = 480d / Math.Sqrt(3d); // 277.1 V

        private static readonly ProbePoint[] LineSide = { ProbePoint.LineL1, ProbePoint.LineL2, ProbePoint.LineL3 };
        private static readonly ProbePoint[] Phases = { ProbePoint.L1, ProbePoint.L2, ProbePoint.L3 };

        // The points tied to ground: PE and the two grounded neutrals.
        private static readonly ProbePoint[] Grounded = { ProbePoint.Ground, ProbePoint.ControlNeutral, ProbePoint.UpsNeutral };

        private static ElectricalModel Energised(bool ups = false) => new ElectricalModel(new ElectricalSettings(), ups);

        // Q1 open and the DC buses discharged well past the 20 s label time.
        private static ElectricalModel Isolated(bool ups = false)
        {
            ElectricalModel m = Energised(ups);
            Assert.AreEqual(SwitchResult.Done, m.SetDisconnect(false));
            RunFor(m, 120);
            return m;
        }

        private static void RunFor(ElectricalModel m, double seconds, double dt = 0.1)
        {
            for (double t = 0; t < seconds - 1e-9; t += dt)
                m.Tick(dt);
        }

        private static void AssertAc(ElectricalModel m, ProbePoint a, ProbePoint b, double volts)
        {
            ProbeReading r = m.Read(a, b);
            Assert.AreEqual(volts, r.AcVolts, Tol, $"{ProbePoints.Label(a)}–{ProbePoints.Label(b)} AC");
            Assert.AreEqual(0d, r.DcVolts, Tol, $"{ProbePoints.Label(a)}–{ProbePoints.Label(b)} DC");
        }

        private static IEnumerable<(ProbePoint a, ProbePoint b)> AllPairs()
        {
            ProbePoint[] all = ProbePoints.All;
            for (int i = 0; i < all.Length; i++)
                for (int j = i + 1; j < all.Length; j++)
                    yield return (all[i], all[j]);
        }

        private static bool In(ProbePoint p, ProbePoint[] set) => Array.IndexOf(set, p) >= 0;

        // ---- Energised ---------------------------------------------------------------------------------------------

        [Test]
        public void Energised_EveryPhasePair_Reads480_AndEveryPhaseToGround277()
        {
            ElectricalModel m = Energised();
            foreach (ProbePoint[] side in new[] { LineSide, Phases })
            {
                AssertAc(m, side[0], side[1], 480);
                AssertAc(m, side[1], side[2], 480);
                AssertAc(m, side[0], side[2], 480);
                foreach (ProbePoint phase in side)
                    foreach (ProbePoint g in Grounded)
                        AssertAc(m, phase, g, PhaseToGround);
            }
            Assert.AreEqual(480d, m.BusVolts, Tol);
        }

        [Test]
        public void Energised_LineAndLoadSideOfTheSamePhase_ReadZero_AcrossTheClosedDisconnect()
        {
            ElectricalModel m = Energised();
            for (int i = 0; i < 3; i++)
                AssertAc(m, LineSide[i], Phases[i], 0);
            AssertAc(m, ProbePoint.LineL1, ProbePoint.L2, 480);
        }

        [Test]
        public void Energised_Control_Reads120_FromTheTransformer()
        {
            ElectricalModel m = Energised();
            Assert.AreEqual(ControlSource.Transformer, m.ControlSource);
            AssertAc(m, ProbePoint.ControlHot, ProbePoint.ControlNeutral, 120);
            AssertAc(m, ProbePoint.ControlHot, ProbePoint.Ground, 120);
            AssertAc(m, ProbePoint.ControlNeutral, ProbePoint.Ground, 0);
            Assert.AreEqual(120d, m.ControlVolts, Tol);
            AssertAc(m, ProbePoint.UpsHot, ProbePoint.UpsNeutral, 0); // no UPS feed in E1
        }

        [Test]
        public void Energised_EachVfdDcBus_Reads650_SplitAboutGround()
        {
            ElectricalModel m = Energised();
            foreach ((ProbePoint plus, ProbePoint minus) in new[] { (ProbePoint.Vfd1DcPlus, ProbePoint.Vfd1DcMinus), (ProbePoint.Vfd2DcPlus, ProbePoint.Vfd2DcMinus) })
            {
                ProbeReading bus = m.Read(plus, minus);
                Assert.IsTrue(bus.IsDc);
                Assert.AreEqual(650d, bus.DcVolts, Tol);
                Assert.AreEqual(0d, bus.AcVolts, Tol);
                Assert.AreEqual(325d, m.Read(plus, ProbePoint.Ground).DcVolts, Tol);
                Assert.AreEqual(-325d, m.Read(minus, ProbePoint.Ground).DcVolts, Tol);
                Assert.AreEqual(-650d, m.Read(minus, plus).DcVolts, Tol, "reversed probes read negative");
            }
            Assert.AreEqual(650d, m.DcBusVolts(Vfd.Infeed), Tol);
        }

        // ---- Every pair ------------------------------------------------------------------------------------------

        [Test]
        public void EveryPair_IsSymmetric_AndAPointAgainstItselfReadsZero()
        {
            foreach (ElectricalModel m in new[] { Energised(), Energised(ups: true), Isolated(), Isolated(ups: true) })
            {
                foreach (ProbePoint p in ProbePoints.All)
                    Assert.AreEqual(0d, m.Read(p, p).Volts, 1e-9);
                foreach ((ProbePoint a, ProbePoint b) in AllPairs())
                {
                    ProbeReading ab = m.Read(a, b);
                    ProbeReading ba = m.Read(b, a);
                    Assert.AreEqual(ab.AcVolts, ba.AcVolts, 1e-9);
                    Assert.AreEqual(ab.DcVolts, -ba.DcVolts, 1e-9);
                }
            }
        }

        [Test]
        public void Isolated_EveryPair_ReadsZero_ExceptTheLineSideOfQ1()
        {
            ElectricalModel m = Isolated();
            int checkedPairs = 0;
            foreach ((ProbePoint a, ProbePoint b) in AllPairs())
            {
                bool aLine = In(a, LineSide), bLine = In(b, LineSide);
                double expected = aLine && bLine ? 480d : aLine || bLine ? PhaseToGround : 0d;
                Assert.AreEqual(expected, m.Read(a, b).Volts, 0.5, $"{ProbePoints.Label(a)}–{ProbePoints.Label(b)}");
                checkedPairs++;
            }
            Assert.AreEqual(105, checkedPairs, "15 points, every pair");
            Assert.AreEqual(0d, m.BusVolts, Tol);
            Assert.AreEqual(0d, m.ControlVolts, Tol);
            Assert.AreEqual(ControlSource.None, m.ControlSource);
        }

        // ---- E2 backfeed -----------------------------------------------------------------------------------------

        [Test]
        public void E2_Isolated_BusReadsZero_ButTheControlTerminalsStillRead120()
        {
            ElectricalModel m = Isolated(ups: true);

            Assert.AreEqual(0d, m.BusVolts, Tol, "the dashboard's bus reading");
            foreach (ProbePoint p in Phases)
                AssertAc(m, p, ProbePoint.Ground, 0);
            Assert.AreEqual(ControlSource.Ups, m.ControlSource);
            AssertAc(m, ProbePoint.ControlHot, ProbePoint.ControlNeutral, 120);
            AssertAc(m, ProbePoint.ControlHot, ProbePoint.Ground, 120);
            AssertAc(m, ProbePoint.UpsHot, ProbePoint.UpsNeutral, 120);
            AssertAc(m, ProbePoint.ControlHot, ProbePoint.UpsHot, 0); // same source, through the transfer relay
            Assert.AreEqual(120d, m.ControlVolts, Tol);
        }

        [Test]
        public void E2_Isolated_EveryPair_ReadsZero_ExceptLineSideAndTheUpsHotTerminals()
        {
            ElectricalModel m = Isolated(ups: true);
            ProbePoint[] upsHot = { ProbePoint.ControlHot, ProbePoint.UpsHot };
            foreach ((ProbePoint a, ProbePoint b) in AllPairs())
            {
                double expected;
                bool aLine = In(a, LineSide), bLine = In(b, LineSide), aUps = In(a, upsHot), bUps = In(b, upsHot);
                if (aLine && bLine) expected = 480d;
                else if (aUps && bUps) expected = 0d;
                else if ((aLine && bUps) || (aUps && bLine)) expected = Mixed(LineSide, aLine ? a : b);
                else if (aLine || bLine) expected = PhaseToGround;
                else if (aUps || bUps) expected = 120d;
                else expected = 0d;
                Assert.AreEqual(expected, m.Read(a, b).Volts, 0.5, $"{ProbePoints.Label(a)}–{ProbePoints.Label(b)}");
            }
        }

        // A line-side phase against the unsynchronised UPS (120 V at 0°): |277∠θ − 120∠0|.
        private static double Mixed(ProbePoint[] lineSide, ProbePoint phase)
        {
            double angle = (Array.IndexOf(lineSide, phase) switch { 0 => 0d, 1 => -120d, _ => 120d }) * Math.PI / 180d;
            double re = PhaseToGround * Math.Cos(angle) - 120d;
            double im = PhaseToGround * Math.Sin(angle);
            return Math.Sqrt(re * re + im * im);
        }

        [Test]
        public void E2_OpeningAndLockingTheUpsFeed_KillsTheControlCircuit()
        {
            ElectricalModel m = Isolated(ups: true);
            Assert.AreEqual(SwitchResult.RefusedNotOpen, m.AddUpsLock(), "a lock fits only with the breaker open");
            Assert.AreEqual(SwitchResult.Done, m.SetUpsBreaker(false));
            Assert.AreEqual(SwitchResult.Done, m.AddUpsLock());

            Assert.AreEqual(1, m.UpsLocks);
            Assert.AreEqual(0d, m.ControlVolts, Tol);
            AssertAc(m, ProbePoint.UpsHot, ProbePoint.Ground, 0);
            Assert.AreEqual(SwitchResult.RefusedLocked, m.SetUpsBreaker(true));
        }

        [Test]
        public void E2_WithTheMainLive_ControlStaysOnTheTransformer()
        {
            ElectricalModel m = Energised(ups: true);
            Assert.AreEqual(ControlSource.Transformer, m.ControlSource);
            AssertAc(m, ProbePoint.ControlHot, ProbePoint.ControlNeutral, 120);
        }

        // ---- DC bus decay (E3) -----------------------------------------------------------------------------------

        [Test]
        public void DcBus_DecaysAfterIsolation_AndIsSafeOnSchedule()
        {
            ElectricalModel m = Energised();
            m.SetDisconnect(false);

            RunFor(m, 1);
            Assert.That(m.DcBusVolts(Vfd.Infeed), Is.GreaterThan(500), "hundreds of volts right after isolation");
            Assert.AreEqual(m.DcBusVolts(Vfd.Infeed), m.Read(ProbePoint.Vfd1DcPlus, ProbePoint.Vfd1DcMinus).DcVolts, 1e-9);

            RunFor(m, 18.5); // 19.5 s
            Assert.That(m.DcBusVolts(Vfd.Infeed), Is.GreaterThan(50), "still above the safe level just before 20 s");
            RunFor(m, 1); // 20.5 s
            Assert.That(m.DcBusVolts(Vfd.Infeed), Is.LessThan(50), "below the safe level just after 20 s");
            Assert.AreEqual(m.DcBusVolts(Vfd.Infeed), m.DcBusVolts(Vfd.Outfeed), 1e-9);
        }

        [Test]
        public void DcBus_DecayDoesNotDependOnTheTimeStep()
        {
            ElectricalModel coarse = Energised(), fine = Energised();
            coarse.SetDisconnect(false);
            fine.SetDisconnect(false);
            RunFor(coarse, 10, dt: 1);
            RunFor(fine, 10, dt: 0.01);
            Assert.AreEqual(coarse.DcBusVolts(Vfd.Infeed), fine.DcBusVolts(Vfd.Infeed), 0.5);
        }

        [Test]
        public void DcBus_E3TakesSixtySeconds_AndSecondsToSafeAgrees()
        {
            ElectricalModel m = ElectricalScenarios.E3().CreateModel();
            Assert.AreEqual(60d, m.SecondsToSafe(650), 1e-6);
            m.SetDisconnect(false);
            RunFor(m, 30);
            Assert.That(m.DcBusVolts(Vfd.Infeed), Is.GreaterThan(150));
            Assert.AreEqual(30d, m.SecondsToSafe(m.DcBusVolts(Vfd.Infeed)), 0.2);
            RunFor(m, 31);
            Assert.That(m.DcBusVolts(Vfd.Infeed), Is.LessThan(50));
        }

        [Test]
        public void DcBus_ChargesAgain_WhenTheDisconnectCloses()
        {
            ElectricalModel m = Energised();
            m.SetDisconnect(false);
            RunFor(m, 30);
            m.SetDisconnect(true);
            m.Tick(0.1);
            Assert.AreEqual(650d, m.DcBusVolts(Vfd.Infeed), Tol);
        }

        [Test]
        public void DcBus_WithoutATick_StaysChargedAtTheMomentOfIsolation()
        {
            ElectricalModel m = Energised();
            m.SetDisconnect(false);
            Assert.AreEqual(650d, m.Read(ProbePoint.Vfd1DcPlus, ProbePoint.Vfd1DcMinus).DcVolts, Tol);
            AssertAc(m, ProbePoint.L1, ProbePoint.L2, 0);
        }

        // ---- Disconnect, locks and door ----------------------------------------------------------------------------

        [Test]
        public void Disconnect_LocksOnlyWhenOpen_CountsLocks_AndRefusesToCloseUntilAllAreOff()
        {
            ElectricalModel m = Energised();
            Assert.AreEqual(SwitchResult.RefusedNotOpen, m.AddDisconnectLock());
            Assert.AreEqual(SwitchResult.Done, m.SetDisconnect(false));
            Assert.AreEqual(SwitchResult.NoChange, m.SetDisconnect(false));
            Assert.AreEqual(SwitchResult.Done, m.AddDisconnectLock());
            Assert.AreEqual(SwitchResult.Done, m.AddDisconnectLock()); // a second worker's lock
            Assert.AreEqual(2, m.DisconnectLocks);

            Assert.AreEqual(SwitchResult.RefusedLocked, m.SetDisconnect(true));
            m.RemoveDisconnectLock();
            Assert.AreEqual(SwitchResult.RefusedLocked, m.SetDisconnect(true), "one lock still on");
            m.RemoveDisconnectLock();
            Assert.AreEqual(SwitchResult.NoChange, m.RemoveDisconnectLock());
            Assert.AreEqual(SwitchResult.Done, m.SetDisconnect(true));
            Assert.IsTrue(m.MainBusLive);
        }

        [Test]
        public void Door_IsInterlockedWithTheDisconnect_UnlessDefeated()
        {
            ElectricalModel m = Energised();
            Assert.AreEqual(SwitchResult.RefusedInterlock, m.SetDoor(true));
            Assert.IsFalse(m.DoorOpen);

            Assert.AreEqual(SwitchResult.Done, m.SetDoor(true, useDefeater: true), "energised diagnostics");
            Assert.AreEqual(SwitchResult.Done, m.SetDoor(false));

            m.SetDisconnect(false);
            Assert.AreEqual(SwitchResult.Done, m.SetDoor(true));
            Assert.AreEqual(SwitchResult.RefusedInterlock, m.SetDisconnect(true), "Q1 does not close with the door open");
            m.SetDoor(false);
            Assert.AreEqual(SwitchResult.Done, m.SetDisconnect(true));
        }

        [Test]
        public void DriveFault_IsSetAndClearedByTheScenario_AndLeavesTheDcBusCharged()
        {
            ElectricalModel m = Energised();
            m.SetInfeedDriveFault(true);
            Assert.IsTrue(m.InfeedDriveFault);
            Assert.AreEqual(650d, m.DcBusVolts(Vfd.Infeed), Tol, "a tripped drive still holds its charge");
            m.SetInfeedDriveFault(false);
            Assert.IsFalse(m.InfeedDriveFault);
        }

        // ---- Tester (E4) -----------------------------------------------------------------------------------------

        [Test]
        public void Tester_ShowsTheReading_ToWholeVolts_AndOverRangeAsOL()
        {
            var tester = new VoltageTester();
            ElectricalModel m = Energised();
            TesterDisplay shown = tester.Measure(m.Read(ProbePoint.L1, ProbePoint.Ground));
            Assert.AreEqual(277d, shown.Volts);
            Assert.IsFalse(shown.Dc);
            Assert.AreEqual("650 V DC", tester.Measure(m.Read(ProbePoint.Vfd1DcPlus, ProbePoint.Vfd1DcMinus)).ToString());
            Assert.IsTrue(new VoltageTester(ratedVolts: 300).Measure(m.Read(ProbePoint.L1, ProbePoint.L2)).OverRange);
        }

        [Test]
        public void Tester_Healthy_PassesTheProofBeforeAndAfter()
        {
            VoltageTester tester = ElectricalScenarios.E1().CreateTester();
            ElectricalModel m = Isolated();
            Assert.IsTrue(tester.Prove());
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(0d, tester.Measure(m.Read(ProbePoint.L1, ProbePoint.L2)).Volts);
            Assert.IsTrue(tester.Prove());
            Assert.IsFalse(tester.BatteryFlat);
        }

        [Test]
        public void E4_TheFlatTester_PassesTheFirstProof_GoesBlank_AndFailsTheAfterProof()
        {
            VoltageTester tester = ElectricalScenarios.E4().CreateTester();
            ElectricalModel m = Energised(); // live, so a blank display is a false "dead"

            Assert.IsTrue(tester.Prove(), "proved before the test");
            for (int i = 0; i < ElectricalScenarios.E4FailsAfter - 1; i++)
                Assert.AreEqual(480d, tester.Measure(m.Read(ProbePoint.L1, ProbePoint.L2)).Volts);

            TesterDisplay blank = tester.Measure(m.Read(ProbePoint.L1, ProbePoint.L2));
            Assert.IsTrue(blank.Blank, "battery flat mid-test");
            Assert.AreEqual(0d, blank.Volts);
            Assert.AreEqual("", blank.ToString());
            Assert.IsFalse(tester.Prove(), "the after-proof catches it");

            VoltageTester spare = ElectricalScenarioSettings.CreateSpareTester();
            Assert.IsTrue(spare.Prove());
            Assert.AreEqual(480d, spare.Measure(m.Read(ProbePoint.L1, ProbePoint.L2)).Volts);
        }

        // ---- Scenarios -------------------------------------------------------------------------------------------

        [Test]
        public void Scenarios_E1ToE4_SeedOneLessonEach()
        {
            ElectricalScenarioSettings e1 = ElectricalScenarios.E1(), e2 = ElectricalScenarios.E2(),
                e3 = ElectricalScenarios.E3(), e4 = ElectricalScenarios.E4();

            Assert.AreEqual(new[] { 1, 2, 3, 4 }, new[] { e1.Number, e2.Number, e3.Number, e4.Number });
            Assert.IsFalse(e1.UpsBackfeed);
            Assert.IsTrue(e2.UpsBackfeed);
            Assert.That(e3.Settings.DcDischargeSeconds, Is.GreaterThan(e1.Settings.DcDischargeSeconds));
            Assert.AreEqual(-1, e1.TesterFailsAfter);
            Assert.That(e4.TesterFailsAfter, Is.GreaterThan(0));
            foreach (ElectricalScenarioSettings e in new[] { e1, e2, e3, e4 })
            {
                Assert.That(e.DriveFaultAfterSeconds, Is.GreaterThan(0));
                Assert.AreEqual(e.Number, ElectricalScenarios.ByNumber(e.Number).Number);
            }
            Assert.IsNull(ElectricalScenarios.ByNumber(5));
            Assert.IsTrue(e2.CreateModel().UpsFeedLive);
        }

        // ---- Signals ---------------------------------------------------------------------------------------------

        [Test]
        public void Topics_CabinetAndElecStations_HaveExactlyThePlannedSignals_AndNoTesterReading()
        {
            var names = new SortedSet<string>();
            foreach (FieldInfo field in typeof(CellTopics).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is TopicPath topic)
                {
                    string s = topic.ToString();
                    if (s.StartsWith("cell/cabinet/") || s.StartsWith("cell/elec/") || s == "cell/infeed/drive_fault")
                        names.Add(s);
                }
            }
            var expected = new SortedSet<string>
            {
                "cell/cabinet/temp_c", "cell/cabinet/overheat", // Rev 6
                "cell/cabinet/door_open", "cell/cabinet/disconnect", "cell/cabinet/ups_feed", "cell/cabinet/locked",
                "cell/cabinet/ups_locked", "cell/cabinet/bus_v", "cell/cabinet/dc_bus_v", "cell/cabinet/control_v",
                "cell/cabinet/door_cmd", "cell/cabinet/disconnect_cmd", "cell/cabinet/ups_cmd", "cell/cabinet/lock_cmd",
                "cell/cabinet/ups_lock_cmd", "cell/infeed/drive_fault",
                "cell/elec/scenario", "cell/elec/boundary", "cell/elec/ppe_cat", "cell/elec/tester_ok",
                "cell/cabinet/refused", "cell/elec/gear_up", "cell/elec/ppe_worn", "cell/elec/ppe_refused", // M27: refusals, the PPE stage
            };
            CollectionAssert.AreEqual(expected, names);
        }
    }
}
