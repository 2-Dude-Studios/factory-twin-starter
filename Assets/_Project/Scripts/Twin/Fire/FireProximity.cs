using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Fire safety (M15): how close is too close to a burning fire, measured on the floor from the base of the fire.
    /// The danger zone grows with the fire (0.9 m round an incipient flame, 1.5 m round one at full size); the heat
    /// is felt from 0.8 m further out. Pure, so it is tested; the headset's red screen and the score read it.
    /// </summary>
    public static class FireProximity
    {
        public const double BaseTooCloseMetres = 0.9;
        public const double GrowthMetres = 0.6;
        public const double WarnBandMetres = 0.8;

        /// <summary>Inside this distance the trainee is too close (the fire's size 0–1).</summary>
        public static double TooCloseMetres(double size) => BaseTooCloseMetres + GrowthMetres * Clamp01(size);

        /// <summary>The heat is felt from this distance in.</summary>
        public static double WarnMetres(double size) => TooCloseMetres(size) + WarnBandMetres;

        /// <summary>0 outside the warning distance, rising to 1 at the too-close line and staying 1 inside it.</summary>
        public static double Heat(double metres, double size)
        {
            if (double.IsNaN(metres))
                return 0d;
            double near = TooCloseMetres(size);
            double far = WarnMetres(size);
            if (metres <= near)
                return 1d;
            if (metres >= far)
                return 0d;
            return 1d - (metres - near) / (far - near);
        }

        public static bool TooClose(double metres, double size) => !double.IsNaN(metres) && metres <= TooCloseMetres(size);

        /// <summary>Whether a fire at this stage gives off flame heat (smoke alone does not).</summary>
        public static bool Burning(FireStage stage) =>
            stage == FireStage.Incipient || stage == FireStage.Growing || stage == FireStage.BeyondIncipient;

        private static double Clamp01(double v) => Math.Max(0d, Math.Min(1d, double.IsNaN(v) ? 0d : v));
    }
}
