using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Training areas (M24): the places in the hall, one per drill, plus the hub.</summary>
    public enum TrainingArea
    {
        Hub = 0,
        RobotCell = 1,
        Lockout = 2,
        Fire = 3,
        Electrical = 4,
    }

    /// <summary>Training areas (M24): which side of an area's floor border something is on.</summary>
    public enum AreaEdge
    {
        South = 0,
        North = 1,
        West = 2,
        East = 3,
    }

    /// <summary>Training areas (M24): a spot on the hall floor and the way something there faces (yaw, 0 = north, +Z).</summary>
    [Serializable]
    public sealed class AreaSpot
    {
        [SerializeField] private float x;
        [SerializeField] private float z;
        [SerializeField] private float yawDegrees;

        public AreaSpot() { }

        public AreaSpot(float x, float z, float yawDegrees = 0f)
        {
            this.x = x;
            this.z = z;
            this.yawDegrees = yawDegrees;
        }

        public float X => x;
        public float Z => z;
        public float YawDegrees => yawDegrees;
    }

    /// <summary>
    /// Training areas (M24): one area's place in the hall. A floor border in the area's colour, the spot where the
    /// trainee starts (desktop and headset alike), a briefing post, a kit station and a score board, and where its
    /// machine stands. What already exists in the scene (the lockout's board, the fire stand) is not built again.
    /// </summary>
    [Serializable]
    public sealed class TrainingAreaSettings
    {
        [SerializeField] private TrainingArea area;
        [SerializeField] private string title = string.Empty;
        [Tooltip("For finding your way only: the sign, the border and the boards. Safety markings keep their own colours.")]
        [SerializeField] private Color colour = Color.white;
        [SerializeField] private float minX;
        [SerializeField] private float maxX;
        [SerializeField] private float minZ;
        [SerializeField] private float maxZ;
        [SerializeField] private AreaSpot start = new AreaSpot();
        [SerializeField] private bool buildBriefing = true;
        [SerializeField] private AreaSpot briefing = new AreaSpot();
        [SerializeField] private bool buildKit;
        [SerializeField] private string kitTitle = string.Empty;
        [SerializeField] private AreaSpot kit = new AreaSpot();
        [SerializeField] private bool buildBoard = true;
        [SerializeField] private AreaSpot board = new AreaSpot();
        [Tooltip("Where the area's machine stands (the arm, the CNC, the drive cabinet, the extinguisher stand).")]
        [SerializeField] private bool hasMachine;
        [SerializeField] private AreaSpot machine = new AreaSpot();

        public TrainingAreaSettings() { }

        public TrainingAreaSettings(TrainingArea area, string title, Color colour, float minX, float maxX, float minZ, float maxZ, AreaSpot start)
        {
            this.area = area;
            this.title = title;
            this.colour = colour;
            this.minX = minX;
            this.maxX = maxX;
            this.minZ = minZ;
            this.maxZ = maxZ;
            this.start = start;
            buildBriefing = false;
            buildBoard = false;
        }

        public TrainingArea Area => area;
        public string Title => title;
        public Color Colour => colour;
        public float MinX => minX;
        public float MaxX => maxX;
        public float MinZ => minZ;
        public float MaxZ => maxZ;
        public float CentreX => (minX + maxX) * 0.5f;
        public float CentreZ => (minZ + maxZ) * 0.5f;
        public float SizeX => maxX - minX;
        public float SizeZ => maxZ - minZ;
        public AreaSpot Start => start;
        public bool BuildBriefing => buildBriefing;
        public AreaSpot Briefing => briefing;
        public bool BuildKit => buildKit;
        public string KitTitle => kitTitle;
        public AreaSpot Kit => kit;
        public bool BuildBoard => buildBoard;
        public AreaSpot Board => board;
        public bool HasMachine => hasMachine;
        public AreaSpot Machine => machine;

        public TrainingAreaSettings WithBriefing(float x, float z)
        {
            buildBriefing = true;
            briefing = new AreaSpot(x, z);
            return this;
        }

        public TrainingAreaSettings WithKit(string kitTitle, float x, float z)
        {
            buildKit = true;
            this.kitTitle = kitTitle;
            kit = new AreaSpot(x, z);
            return this;
        }

        public TrainingAreaSettings WithBoard(float x, float z)
        {
            buildBoard = true;
            board = new AreaSpot(x, z);
            return this;
        }

        public TrainingAreaSettings WithMachine(float x, float z, float yawDegrees = 0f)
        {
            hasMachine = true;
            machine = new AreaSpot(x, z, yawDegrees);
            return this;
        }
    }

    /// <summary>
    /// Training areas (M24): the default layout, proposed 2026-11-11 from the hall as read from the open Editor
    /// (hall x −30.5…20, z −37.6…38; the cell at (−5…0.5, −1…3.9); the arm's warning zone ±2.2 m round (−2.0, 1.2);
    /// the line control station at (1.84, 1.97); the fire stand at (−4.8, −13.9)). The asset holds the layout in use.
    /// </summary>
    public static class TrainingAreaDefaults
    {
        public static readonly Color HubColour = new Color(0.92f, 0.92f, 0.92f);
        public static readonly Color RobotColour = new Color(1f, 0.45f, 0.05f);
        public static readonly Color LockoutColour = new Color(1f, 0.82f, 0.05f);
        public static readonly Color FireColour = new Color(0.85f, 0.08f, 0.05f);
        public static readonly Color ElectricalColour = new Color(0.1f, 0.45f, 0.95f);

        public static TrainingAreaSettings[] All() => new[]
        {
            // Open floor south-west of the cell, between it and the fire area.
            new TrainingAreaSettings(TrainingArea.Hub, "Training hub", HubColour, -13.6f, -8.4f, -11.5f, -5.5f, new AreaSpot(-11f, -10.6f))
                .WithBriefing(-11f, -6.2f).WithBoard(-9.0f, -8.4f),
            // The cell's own footprint with an apron to the south, the trainee's side. The start and both posts are
            // outside the warning zone (its south edge is z −1.0).
            new TrainingAreaSettings(TrainingArea.RobotCell, "Robot cell safety", RobotColour, -5.2f, 0.5f, -4.2f, 4.2f, new AreaSpot(-2.0f, -3.4f))
                .WithBriefing(-3.7f, -1.9f).WithBoard(0.0f, -3.0f).WithMachine(-2.0f, 1.2f),
            // The CNC's east end and the line control station. Its training board and lock board exist (M11, M12).
            // The CNC's body stands in the cell; what the lockout works on is its isolator, on the east face.
            // The south border stays north of the waste bin at (2.4, 0.4), which is a fire location.
            new TrainingAreaSettings(TrainingArea.Lockout, "Lockout / tagout", LockoutColour, 0.7f, 4.4f, 0.85f, 5.6f, new AreaSpot(1.84f, 1.35f))
                .WithBoard(3.6f, 2.0f).WithMachine(1.0f, 3.23f),
            // Round the fire training station. Its stand and score board exist (M15); the fires stay across the hall.
            new TrainingAreaSettings(TrainingArea.Fire, "Fire safety", FireColour, -8.0f, -1.6f, -18.8f, -11.4f, new AreaSpot(-4.8f, -12.6f, 180f))
                .WithBriefing(-6.7f, -13.6f).WithMachine(-4.8f, -13.9f),
            // Its own bay east of the cell: the drive cabinet against the north edge, door to the south, with room
            // for all three boundary rings inside the bay and the gear station outside them.
            new TrainingAreaSettings(TrainingArea.Electrical, "Electrical safety", ElectricalColour, 5.0f, 9.0f, -4.0f, 1.0f, new AreaSpot(7.0f, -3.4f))
                .WithBriefing(8.3f, -2.0f).WithKit("PPE AND TEST EQUIPMENT", 5.7f, -2.0f).WithBoard(8.5f, -3.3f).WithMachine(7.0f, 0.55f, 180f),
        };

        public static TrainingAreaSettings For(TrainingArea area) => TrainingAreaRules.Find(All(), area);
    }

    /// <summary>Training areas (M24): the geometry of the layout: what is inside what, which way to face, what is wrong.</summary>
    public static class TrainingAreaRules
    {
        public static TrainingAreaSettings Find(IReadOnlyList<TrainingAreaSettings> areas, TrainingArea area)
        {
            if (areas == null)
                return null;
            for (int i = 0; i < areas.Count; i++)
            {
                if (areas[i] != null && areas[i].Area == area)
                    return areas[i];
            }
            return null;
        }

        /// <summary>Inside the floor border, at least <paramref name="margin"/> metres from it.</summary>
        public static bool Contains(TrainingAreaSettings area, double x, double z, double margin = 0d) =>
            area != null && x >= area.MinX + margin && x <= area.MaxX - margin && z >= area.MinZ + margin && z <= area.MaxZ - margin;

        /// <summary>Two areas share floor (touching borders do not count).</summary>
        public static bool Overlap(TrainingAreaSettings a, TrainingAreaSettings b) =>
            a != null && b != null && a.MinX < b.MaxX && b.MinX < a.MaxX && a.MinZ < b.MaxZ && b.MinZ < a.MaxZ;

        /// <summary>The area a point on the floor is in, or null: where a trainee is standing.</summary>
        public static TrainingAreaSettings At(IReadOnlyList<TrainingAreaSettings> areas, double x, double z)
        {
            if (areas == null)
                return null;
            for (int i = 0; i < areas.Count; i++)
            {
                if (Contains(areas[i], x, z))
                    return areas[i];
            }
            return null;
        }

        /// <summary>The yaw (0 = north, 90 = east) that looks from one floor point to another.</summary>
        public static double YawTowards(double fromX, double fromZ, double toX, double toZ)
        {
            double dx = toX - fromX, dz = toZ - fromZ;
            if (Math.Abs(dx) < 1e-9 && Math.Abs(dz) < 1e-9)
                return 0d;
            return Math.Atan2(dx, dz) * 180d / Math.PI;
        }

        /// <summary>The border edge nearest a point: where the entry sign goes, by the start spot.</summary>
        public static AreaEdge NearestEdge(TrainingAreaSettings area, double x, double z)
        {
            double south = Math.Abs(z - area.MinZ), north = Math.Abs(area.MaxZ - z);
            double west = Math.Abs(x - area.MinX), east = Math.Abs(area.MaxX - x);
            double best = Math.Min(Math.Min(south, north), Math.Min(west, east));
            if (best == south)
                return AreaEdge.South;
            if (best == north)
                return AreaEdge.North;
            return best == west ? AreaEdge.West : AreaEdge.East;
        }

        public static double Distance(double fromX, double fromZ, double toX, double toZ)
        {
            double dx = toX - fromX, dz = toZ - fromZ;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The eight-point compass word for the way from one floor point to another.</summary>
        public static string Compass(double fromX, double fromZ, double toX, double toZ)
        {
            double yaw = YawTowards(fromX, fromZ, toX, toZ);
            int index = (int)Math.Round(((yaw % 360d) + 360d) % 360d / 45d) % 8;
            return CompassWords[index];
        }

        private static readonly string[] CompassWords = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

        /// <summary>
        /// What is wrong with a layout, in words; empty when it is sound. Areas must not share floor, and each area's
        /// start, posts and machine must stand inside its own border (the start half a metre in, so the trainee does
        /// not begin on the line).
        /// </summary>
        public static List<string> Problems(IReadOnlyList<TrainingAreaSettings> areas)
        {
            var problems = new List<string>();
            if (areas == null || areas.Count == 0)
            {
                problems.Add("No areas.");
                return problems;
            }
            for (int i = 0; i < areas.Count; i++)
            {
                TrainingAreaSettings a = areas[i];
                if (a == null)
                {
                    problems.Add($"Area {i} is empty.");
                    continue;
                }
                if (a.SizeX <= 0f || a.SizeZ <= 0f)
                    problems.Add($"{a.Title}: the border has no size.");
                for (int j = 0; j < i; j++)
                {
                    if (areas[j] != null && areas[j].Area == a.Area)
                        problems.Add($"{a.Title}: listed twice.");
                    if (Overlap(a, areas[j]))
                        problems.Add($"{a.Title} and {areas[j].Title} share floor.");
                }
                if (!Contains(a, a.Start.X, a.Start.Z, 0.5d))
                    problems.Add($"{a.Title}: the start spot is not at least 0.5 m inside its border.");
                if (a.BuildBriefing && !Contains(a, a.Briefing.X, a.Briefing.Z))
                    problems.Add($"{a.Title}: the briefing post is outside its border.");
                if (a.BuildKit && !Contains(a, a.Kit.X, a.Kit.Z))
                    problems.Add($"{a.Title}: the kit station is outside its border.");
                if (a.BuildBoard && !Contains(a, a.Board.X, a.Board.Z))
                    problems.Add($"{a.Title}: the score board is outside its border.");
                if (a.HasMachine && !Contains(a, a.Machine.X, a.Machine.Z))
                    problems.Add($"{a.Title}: its machine is outside its border.");
            }
            return problems;
        }

        /// <summary>
        /// A point given in a machine's own space (x to its right, z out of its front) as a hall floor point, for a
        /// machine standing at <paramref name="machine"/>: where the drive cabinet's boundary rings reach.
        /// </summary>
        public static void ToHall(AreaSpot machine, double localX, double localZ, out double x, out double z)
        {
            double yaw = machine.YawDegrees * Math.PI / 180d;
            double cos = Math.Cos(yaw), sin = Math.Sin(yaw);
            x = machine.X + localX * cos + localZ * sin;
            z = machine.Z - localX * sin + localZ * cos;
        }
    }

    /// <summary>
    /// Training areas (M24): the layout in use, as an asset (Assets/_Project/Data/Training/TrainingAreas.asset), so an
    /// area can be moved in the Inspector and rebuilt (Tools > Factory Twin > Build Training Areas) without code.
    /// </summary>
    [CreateAssetMenu(menuName = "Factory Twin/Training Areas", fileName = "TrainingAreas")]
    public sealed class TrainingAreas : ScriptableObject
    {
        [SerializeField] private TrainingAreaSettings[] areas = Array.Empty<TrainingAreaSettings>();

        public IReadOnlyList<TrainingAreaSettings> Areas => areas;

        public TrainingAreaSettings Find(TrainingArea area) => TrainingAreaRules.Find(areas, area);

        /// <summary>Fills the asset (the builder does this once, with the defaults).</summary>
        public void Configure(TrainingAreaSettings[] settings) => areas = settings ?? Array.Empty<TrainingAreaSettings>();
    }
}
