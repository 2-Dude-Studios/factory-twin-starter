using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Electrical safety: how deep a person is inside the cabinet's boundaries, published as the deepest point of the
    /// trainee's head and hands on cell/elec/boundary.
    /// </summary>
    public enum ApproachBoundary
    {
        Outside = 0,
        ArcFlash = 1,
        Limited = 2,
        Restricted = 3,
    }

    /// <summary>
    /// Electrical safety: the shock protection boundaries for exposed fixed circuit parts, from NFPA 70E
    /// Table 130.4(E)(a) (AC). Only the rows this cell uses: 50–150 V (the control circuit) and 151–750 V (480 V).
    /// </summary>
    public readonly struct ShockBoundaries
    {
        private ShockBoundaries(int limitedMm, int limitedInches, int restrictedMm, int restrictedInches)
        {
            LimitedMm = limitedMm;
            LimitedInches = limitedInches;
            RestrictedMm = restrictedMm;
            RestrictedInches = restrictedInches;
        }

        public int LimitedMm { get; }
        public int LimitedInches { get; }

        /// <summary>0 means the table's "avoid contact" (50–150 V).</summary>
        public int RestrictedMm { get; }
        public int RestrictedInches { get; }

        /// <summary>The table row for a nominal phase-to-phase voltage; outside 50–750 V it is not in this demo.</summary>
        public static ShockBoundaries ForVoltage(double nominalVolts)
        {
            if (nominalVolts < 50d)
                throw new ArgumentOutOfRangeException(nameof(nominalVolts), "Below 50 V the table gives no boundaries.");
            if (nominalVolts <= 150d)
                return new ShockBoundaries(1000, 42, 0, 0);
            if (nominalVolts <= 750d)
                return new ShockBoundaries(1000, 42, 300, 12);
            throw new ArgumentOutOfRangeException(nameof(nominalVolts), "Above 750 V is out of scope for this cell.");
        }
    }

    /// <summary>
    /// Electrical safety (M19): the values on the drive cabinet's arc flash label. They come from a made-up study for
    /// the demo, not an engineering one, and the label says so. Plain data so it can be tested outside Unity.
    /// </summary>
    [Serializable]
    public sealed class ArcFlashValues
    {
        [SerializeField] private string equipment = "Drive cabinet DCP-1";
        [SerializeField] private double nominalVolts = 480;
        [SerializeField] private int phases = 3;
        [Tooltip("Incident energy at the working distance, cal/cm² (made-up study).")]
        [SerializeField] private double incidentEnergyCal = 6.1;
        [SerializeField] private int workingDistanceMm = 455;
        [SerializeField] private int arcFlashBoundaryMm = 1370;
        [SerializeField] private string gloveClass = "0";
        [SerializeField] private string studyReference = "Demo study DS-7 rev A";
        [SerializeField] private string studyDate = "2026-10-01";

        public ArcFlashValues()
        {
        }

        public ArcFlashValues(string equipment, double nominalVolts, double incidentEnergyCal, int workingDistanceMm,
            int arcFlashBoundaryMm, string studyDate)
        {
            this.equipment = equipment;
            this.nominalVolts = nominalVolts;
            this.incidentEnergyCal = incidentEnergyCal;
            this.workingDistanceMm = workingDistanceMm;
            this.arcFlashBoundaryMm = arcFlashBoundaryMm;
            this.studyDate = studyDate;
        }

        public string Equipment => equipment;
        public double NominalVolts => nominalVolts;
        public int Phases => phases;
        public double IncidentEnergyCal => incidentEnergyCal;
        public int WorkingDistanceMm => workingDistanceMm;
        public int ArcFlashBoundaryMm => arcFlashBoundaryMm;
        public string GloveClass => gloveClass;
        public string StudyReference => studyReference;
        public string StudyDate => studyDate;

        public ShockBoundaries Shock => ShockBoundaries.ForVoltage(nominalVolts);

        /// <summary>The minimum arc rating of clothing: the incident energy rounded up to the next whole cal/cm², at least 4.</summary>
        public double MinimumArcRatingCal => Math.Max(4d, Math.Ceiling(incidentEnergyCal - 1e-9));

        /// <summary>
        /// The lowest PPE category kit on the board whose minimum arc rating (Table 130.7(C)(15)(c): 4, 8, 25, 40
        /// cal/cm²) covers the incident energy; 0 above 40, where no kit is enough. Used to score the trainee's pick,
        /// not printed (a label shows the incident energy or a PPE category, not both).
        /// </summary>
        public int RequiredPpeCategory => ApproachBoundaries.PpeCategoryFor(incidentEnergyCal);
    }

    /// <summary>Electrical safety: boundary depth and PPE category rules, pure.</summary>
    public static class ApproachBoundaries
    {
        private static readonly double[] CategoryArcRating = { 4d, 8d, 25d, 40d };

        /// <summary>How deep a point <paramref name="distanceMm"/> from the nearest exposed live part is.</summary>
        public static ApproachBoundary Classify(double distanceMm, ArcFlashValues study)
        {
            if (study == null)
                throw new ArgumentNullException(nameof(study));
            ShockBoundaries shock = study.Shock;
            if (shock.RestrictedMm > 0 && distanceMm < shock.RestrictedMm)
                return ApproachBoundary.Restricted;
            if (distanceMm < shock.LimitedMm)
                return ApproachBoundary.Limited;
            if (distanceMm < study.ArcFlashBoundaryMm)
                return ApproachBoundary.ArcFlash;
            return ApproachBoundary.Outside;
        }

        /// <summary>The deeper of two boundary depths.</summary>
        public static ApproachBoundary Deepest(ApproachBoundary a, ApproachBoundary b) => (int)a >= (int)b ? a : b;

        /// <summary>The lowest PPE category (1–4) whose minimum arc rating covers the incident energy; 0 above 40 cal/cm².</summary>
        public static int PpeCategoryFor(double incidentEnergyCal)
        {
            for (int i = 0; i < CategoryArcRating.Length; i++)
            {
                if (incidentEnergyCal <= CategoryArcRating[i])
                    return i + 1;
            }
            return 0;
        }

        /// <summary>
        /// Distance from a point to an axis-aligned box (the exposed parts, in the cabinet's own space), 0 inside.
        /// Each boundary is then this box grown by the boundary's distance.
        /// </summary>
        public static double DistanceToBox(double x, double y, double z, double minX, double minY, double minZ,
            double maxX, double maxY, double maxZ)
        {
            double dx = Math.Max(Math.Max(minX - x, 0d), x - maxX);
            double dy = Math.Max(Math.Max(minY - y, 0d), y - maxY);
            double dz = Math.Max(Math.Max(minZ - z, 0d), z - maxZ);
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Distance from a point to the cabinet's exposed parts (an axis-aligned box in the cabinet's space, door on
        /// its +Z face), or infinity for a point behind the cabinet's back (z below the box), which the closed back
        /// and the line keep apart from them.
        /// </summary>
        public static double DistanceToExposedParts(double x, double y, double z, double minX, double minY, double minZ,
            double maxX, double maxY, double maxZ) =>
            z < minZ ? double.PositiveInfinity : DistanceToBox(x, y, z, minX, minY, minZ, maxX, maxY, maxZ);

        /// <summary>
        /// The floor outline of one boundary: the box's footprint grown by <paramref name="offset"/>, as a U open at
        /// the back (matching <see cref="DistanceToExposedParts"/>). (x, z) pairs from the back left, round the front,
        /// to the back right; each front corner is a quarter circle of <paramref name="arcSteps"/> segments.
        /// </summary>
        public static double[] Outline(double minX, double maxX, double backZ, double frontZ, double offset, int arcSteps)
        {
            if (arcSteps < 1)
                throw new ArgumentOutOfRangeException(nameof(arcSteps), "At least 1.");
            int count = 2 + 2 * (arcSteps + 1);
            var points = new double[count * 2];
            int i = 0;
            void Add(double px, double pz)
            {
                points[i++] = px;
                points[i++] = pz;
            }
            Add(minX - offset, backZ);
            // Front left corner: from facing -X (180°) to facing +Z (90°), round (minX, frontZ).
            for (int s = 0; s <= arcSteps; s++)
            {
                double a = Math.PI - (Math.PI / 2d) * s / arcSteps;
                Add(minX + offset * Math.Cos(a), frontZ + offset * Math.Sin(a));
            }
            // Front right corner: from +Z (90°) to +X (0°), round (maxX, frontZ).
            for (int s = 0; s <= arcSteps; s++)
            {
                double a = (Math.PI / 2d) - (Math.PI / 2d) * s / arcSteps;
                Add(maxX + offset * Math.Cos(a), frontZ + offset * Math.Sin(a));
            }
            Add(maxX + offset, backZ);
            return points;
        }
    }

    /// <summary>
    /// Electrical safety (M19): the arc flash label's text, built from the study so the label is always the study's
    /// numbers. ANSI Z535 style: WARNING up to 40 cal/cm², DANGER above. NFPA 70E 130.5(H) content: nominal voltage,
    /// arc flash boundary, incident energy with its working distance, minimum arc rating, and the study's date.
    /// </summary>
    public static class ArcFlashLabel
    {
        public const string DemoNotice = "DEMO VALUES - NOT FROM AN ENGINEERING STUDY";

        public static string SignalWord(ArcFlashValues study) => study.IncidentEnergyCal > 40d ? "DANGER" : "WARNING";

        /// <summary>A metric length followed by feet and inches, the way US labels print it: "1.37 m (4 ft 6 in)".</summary>
        public static string Length(int millimetres) =>
            Length(millimetres, (int)Math.Round(millimetres / 25.4d, MidpointRounding.AwayFromZero));

        /// <summary>
        /// A metric length with its own imperial value, for table values whose two columns are not exact conversions
        /// (Table 130.4(E)(a) prints 1.0 m as 3 ft 6 in).
        /// </summary>
        public static string Length(int millimetres, int inches)
        {
            string metric = millimetres >= 1000
                ? (millimetres / 1000d).ToString("0.00", CultureInfo.InvariantCulture) + " m"
                : millimetres.ToString(CultureInfo.InvariantCulture) + " mm";
            string imperial = inches >= 12 ? $"{inches / 12} ft {inches % 12} in" : $"{inches} in";
            return $"{metric} ({imperial})";
        }

        /// <summary>A working distance, printed in inches only, as labels give it: "455 mm (18 in)".</summary>
        public static string WorkingDistance(int millimetres) =>
            $"{millimetres.ToString(CultureInfo.InvariantCulture)} mm ({(int)Math.Round(millimetres / 25.4d, MidpointRounding.AwayFromZero)} in)";

        public static string Text(ArcFlashValues study)
        {
            if (study == null)
                throw new ArgumentNullException(nameof(study));
            ShockBoundaries shock = study.Shock;
            CultureInfo inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(SignalWord(study)).Append('\n');
            sb.Append("Arc flash and shock hazard").Append('\n');
            sb.Append(study.Equipment).Append('\n');
            sb.Append("ARC FLASH").Append('\n');
            sb.Append("Arc flash boundary: ").Append(Length(study.ArcFlashBoundaryMm)).Append('\n');
            sb.Append("Incident energy: ").Append(study.IncidentEnergyCal.ToString("0.0", inv)).Append(" cal/cm² at ")
                .Append(WorkingDistance(study.WorkingDistanceMm)).Append('\n');
            sb.Append("Minimum arc rating of clothing: ").Append(study.MinimumArcRatingCal.ToString("0", inv)).Append(" cal/cm²").Append('\n');
            sb.Append("SHOCK").Append('\n');
            sb.Append("Nominal voltage: ").Append(study.NominalVolts.ToString("0", inv)).Append(" V AC, ")
                .Append(study.Phases.ToString(inv)).Append(" phase").Append('\n');
            sb.Append("Limited approach: ").Append(Length(shock.LimitedMm, shock.LimitedInches)).Append('\n');
            sb.Append("Restricted approach: ").Append(shock.RestrictedMm > 0 ? Length(shock.RestrictedMm, shock.RestrictedInches) : "avoid contact").Append('\n');
            sb.Append("Rubber insulating gloves: Class ").Append(study.GloveClass).Append('\n');
            sb.Append(study.StudyReference).Append(", ").Append(study.StudyDate).Append('\n');
            sb.Append(DemoNotice);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Electrical safety (M19): the drive cabinet's arc flash study as an asset (demo values), from which the label and
    /// the boundary zones are made. One per cabinet; a client's own study values would go here.
    /// </summary>
    [CreateAssetMenu(menuName = "Factory Twin/Arc Flash Study", fileName = "ArcFlashStudy")]
    public sealed class ArcFlashStudy : ScriptableObject
    {
        [SerializeField] private ArcFlashValues values = new ArcFlashValues();

        public ArcFlashValues Values => values;
        public string LabelText => ArcFlashLabel.Text(values);
    }
}
