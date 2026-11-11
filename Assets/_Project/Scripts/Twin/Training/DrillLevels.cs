using System;
using System.Collections.Generic;
using System.Text;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Training areas (M24): one level of a drill: what the trainee does, the rule it practises, its badge.</summary>
    public sealed class DrillLevel
    {
        public DrillLevel(int number, string name, string activity, string clauses, string badge)
        {
            Number = number;
            Name = name;
            Activity = activity;
            Clauses = clauses;
            Badge = badge;
        }

        public int Number { get; }
        public string Name { get; }
        public string Activity { get; }
        public string Clauses { get; }
        public string Badge { get; }
    }

    /// <summary>
    /// Training areas (M24): the four levels every drill has (docs/training-areas.md, section 6), as data: awareness,
    /// gear and skills, procedure, assessment. Each names the rule it practises. A training demo: the clause says what
    /// the level practises, not that passing it makes anyone compliant.
    /// </summary>
    public static class DrillLevels
    {
        public const int Count = 4;
        public const string DemoNotice = "Training demo - not certified training";

        private static readonly string[] LevelNames = { "Awareness", "Gear and skills", "Procedure", "Assessment" };

        public static string LevelName(int number) =>
            number >= 1 && number <= Count ? LevelNames[number - 1] : string.Empty;

        /// <summary>The rules a drill trains to, for its sign and its record.</summary>
        public static string Standard(TrainingArea area)
        {
            switch (area)
            {
                case TrainingArea.RobotCell: return "ISO 10218-1/-2:2025, ISO 13855";
                case TrainingArea.Lockout: return "OSHA 29 CFR 1910.147";
                case TrainingArea.Fire: return "OSHA 29 CFR 1910.38, .39, .157";
                case TrainingArea.Electrical: return "OSHA 29 CFR 1910.332-.335, NFPA 70E-2027";
                default: return string.Empty;
            }
        }

        public static IReadOnlyList<DrillLevel> For(TrainingArea area)
        {
            switch (area)
            {
                case TrainingArea.RobotCell: return Robot;
                case TrainingArea.Lockout: return Lockout;
                case TrainingArea.Fire: return Fire;
                case TrainingArea.Electrical: return Electrical;
                default: return Array.Empty<DrillLevel>();
            }
        }

        private static readonly DrillLevel[] Robot =
        {
            new DrillLevel(1, LevelNames[0], "Step into each zone and see what the arm does; reset from outside the zone.", "ISO 10218-2; ISO 13855", "Zone aware"),
            new DrillLevel(2, LevelNames[1], "Stop the line, go in, come out, reset from outside the zone, restart.", "IEC 60204-1; ISO 10218-2", "Reset right"),
            new DrillLevel(3, LevelNames[2], "Clear a jam at the gate under the protective stop; stop a colleague walking in.", "ISO 10218-2; 1910.147(a)(2)(ii)", "Clean clear"),
            new DrillLevel(4, LevelNames[3], "A jam, a walker, or both, with no hints.", "All of the above", "Cell keeper"),
        };

        private static readonly DrillLevel[] Lockout =
        {
            new DrillLevel(1, LevelNames[0], "Read the energy control procedure and name the CNC's three energies.", "1910.147(c)(4), (c)(7)(i)", "Know your energies"),
            new DrillLevel(2, LevelNames[1], "Take your own lock and tag from the lock board, not a shared one.", "1910.147(c)(5), (d)(4)", "Own lock"),
            new DrillLevel(3, LevelNames[2], "The lockout steps on the CNC with the try-start; stop a colleague removing your lock.", "1910.147(c)(9), (d)(1)-(d)(6), (e)(1)-(e)(3)", "Try-start"),
            new DrillLevel(4, LevelNames[3], "A random fault, with no hints.", "All of the above", "Perfect lockout"),
        };

        private static readonly DrillLevel[] Fire =
        {
            new DrillLevel(1, LevelNames[0], "Hazard hunt; find both exits, the call point, the extinguishers and the assembly point.", "1910.39(c); 1910.38(c)(2); 1910.37", "Sharp eye"),
            new DrillLevel(2, LevelNames[1], "Check the extinguisher before use; PASS on the practice pan from the right distance.", "1910.157(g)(1); NFPA 10", "Sharp aim"),
            new DrillLevel(3, LevelNames[2], "A fire in the hall: alarm first, the right agent, watch for reflash; leave when it grows.", "1910.38(c)(1)-(c)(5); 1910.157(g)", "Clean knockdown"),
            new DrillLevel(4, LevelNames[3], "A random fire, with no hints.", "All of the above", "Fire marshal"),
        };

        private static readonly DrillLevel[] Electrical =
        {
            new DrillLevel(1, LevelNames[0], "Hazard hunt; read the arc flash label; walk the three boundaries.", "1910.303(g)(1); 1910.335(b); 70E 130.4, 130.5(H)", "Label reader"),
            new DrillLevel(2, LevelNames[1], "Pick the PPE the label calls for; inspect the gloves; prove the tester before and after.", "1910.335(a)(1); 1910.137(c)(2); 70E 130.7, 120.6(7)", "Live-dead-live"),
            new DrillLevel(3, LevelNames[2], "Isolate, lock, wait, test every phase and the DC bus; find the backfeed and lock it.", "1910.333(b)(2); 70E 120.6, 120.5(B)(6)", "Backfeed hunter"),
            new DrillLevel(4, LevelNames[3], "A random fault, with no hints.", "All of the above", "Qualified hands"),
        };
    }

    /// <summary>Training areas (M24): the words on an area's sign, briefing post and score board, and the hub's hall map.</summary>
    public static class TrainingAreaText
    {
        /// <summary>The entry sign: the drill's name in capitals.</summary>
        public static string Sign(TrainingAreaSettings area) => area == null ? string.Empty : area.Title.ToUpperInvariant();

        /// <summary>The briefing post: the rules the drill trains to, its four levels, and that it is a demo.</summary>
        public static string Briefing(TrainingArea area)
        {
            IReadOnlyList<DrillLevel> levels = DrillLevels.For(area);
            if (levels.Count == 0)
                return string.Empty;
            var text = new StringBuilder();
            text.Append("Trains to: ").Append(DrillLevels.Standard(area)).Append('\n');
            foreach (DrillLevel level in levels)
            {
                text.Append('\n').Append("LEVEL ").Append(level.Number).Append("  ").Append(level.Name.ToUpperInvariant()).Append('\n');
                text.Append(level.Activity).Append('\n');
                text.Append(level.Clauses).Append("   Badge: ").Append(level.Badge).Append('\n');
            }
            text.Append('\n').Append(DrillLevels.DemoNotice);
            return text.ToString();
        }

        /// <summary>How the stars are earned, the same in every area, for the score boards.</summary>
        public static string Stars() =>
            "ONE STAR  every required step, no critical mistake\n" +
            "TWO STARS  and no minor mistakes\n" +
            "THREE STARS  and within the par time\n\n" +
            "A critical mistake fails the level, whatever the points.";

        /// <summary>The hub's map: each drill's area with its distance and direction from where the reader stands.</summary>
        public static string HallMap(IReadOnlyList<TrainingAreaSettings> areas, double fromX, double fromZ)
        {
            var text = new StringBuilder();
            if (areas == null)
                return string.Empty;
            foreach (TrainingAreaSettings area in areas)
            {
                if (area == null || area.Area == TrainingArea.Hub)
                    continue;
                if (text.Length > 0)
                    text.Append('\n');
                int metres = (int)Math.Round(TrainingAreaRules.Distance(fromX, fromZ, area.Start.X, area.Start.Z));
                text.Append(area.Title).Append(": ").Append(metres).Append(" m ")
                    .Append(TrainingAreaRules.Compass(fromX, fromZ, area.Start.X, area.Start.Z));
            }
            return text.ToString();
        }
    }
}
