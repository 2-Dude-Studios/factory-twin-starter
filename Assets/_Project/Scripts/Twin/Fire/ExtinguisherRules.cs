using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Fire safety (M15): how a portable extinguisher behaves in the drill, pure and tested. Discharge time, the
    /// stand-off range that works, and how well the stream is aimed at the base of the fire. Typical figures for
    /// small extinguishers (5 lb ABC and CO2, 2.5 gal water); a real unit's label is what counts.
    /// </summary>
    public static class ExtinguisherRules
    {
        // The stream counts fully within this distance of the base of the fire, and not at all beyond MissMetres.
        public const double OnTargetMetres = 0.35;
        public const double MissMetres = 1.2;

        /// <summary>Seconds of continuous discharge from full.</summary>
        public static double CapacitySeconds(FireAgent agent)
        {
            switch (agent)
            {
                case FireAgent.Water: return 40d;
                case FireAgent.DryChemicalAbc: return 14d;
                case FireAgent.Co2: return 10d;
                default: return 0d;
            }
        }

        /// <summary>The stand-off band that works best, in metres (inner and outer edge), and where it stops working.</summary>
        public static (double tooClose, double bestFrom, double bestTo, double outOfRange) Range(FireAgent agent)
        {
            switch (agent)
            {
                case FireAgent.Water: return (1.5d, 3d, 6d, 9d);
                case FireAgent.Co2: return (0.5d, 1d, 2.5d, 3.5d);
                case FireAgent.DryChemicalAbc: return (0.8d, 1.8d, 3.5d, 5d);
                default: return (0d, 0d, 0d, 0d);
            }
        }

        /// <summary>
        /// How well the distance suits the agent, 0–1: 1 inside the best band, down to 0 at the edge of its reach.
        /// Too close still knocks fire down but scatters it, so it falls only to 0.4 at the nozzle.
        /// </summary>
        public static double Standoff(FireAgent agent, double metres)
        {
            if (agent == FireAgent.None || double.IsNaN(metres))
                return 0d;
            (double tooClose, double bestFrom, double bestTo, double outOfRange) = Range(agent);
            if (metres >= bestFrom && metres <= bestTo)
                return 1d;
            if (metres > bestTo)
                return metres >= outOfRange ? 0d : 1d - (metres - bestTo) / (outOfRange - bestTo);
            if (metres <= tooClose)
                return 0.4d;
            return 0.4d + 0.6d * (metres - tooClose) / (bestFrom - tooClose);
        }

        /// <summary>How well the stream is aimed, 0–1, from how far it passes from the base of the fire.</summary>
        public static double Aim(double missMetres)
        {
            if (double.IsNaN(missMetres) || missMetres >= MissMetres)
                return 0d;
            if (missMetres <= OnTargetMetres)
                return 1d;
            return 1d - (missMetres - OnTargetMetres) / (MissMetres - OnTargetMetres);
        }

        /// <summary>
        /// How far a ray from <paramref name="origin"/> along <paramref name="direction"/> passes from
        /// <paramref name="target"/>. A target behind the nozzle counts as missed.
        /// </summary>
        public static double MissDistance(double ox, double oy, double oz, double dx, double dy, double dz,
            double tx, double ty, double tz)
        {
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length <= 0d)
                return double.PositiveInfinity;
            dx /= length;
            dy /= length;
            dz /= length;
            double vx = tx - ox, vy = ty - oy, vz = tz - oz;
            double along = vx * dx + vy * dy + vz * dz;
            if (along <= 0d)
                return double.PositiveInfinity;
            double px = vx - along * dx, py = vy - along * dy, pz = vz - along * dz;
            return Math.Sqrt(px * px + py * py + pz * pz);
        }

        public static string Name(FireAgent agent)
        {
            switch (agent)
            {
                case FireAgent.Water: return "Water";
                case FireAgent.Co2: return "CO2";
                case FireAgent.DryChemicalAbc: return "Dry chemical ABC";
                default: return string.Empty;
            }
        }

        /// <summary>The classes on the label, as the trainee reads them.</summary>
        public static string Rating(FireAgent agent)
        {
            switch (agent)
            {
                case FireAgent.Water: return "Class A only";
                case FireAgent.Co2: return "Class B and C";
                case FireAgent.DryChemicalAbc: return "Class A, B and C";
                default: return string.Empty;
            }
        }
    }
}
