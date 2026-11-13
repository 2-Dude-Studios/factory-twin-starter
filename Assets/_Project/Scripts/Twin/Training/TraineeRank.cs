using System;
using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// The headset hub's game layer (M28): the trainee's rank from the stars earned across every drill's levels.
    /// Pure, so the thresholds are tested outside Unity. A training demo's game rank, not a qualification.
    /// </summary>
    public static class TraineeRank
    {
        /// <summary>The ranks in order and the total stars each starts at.</summary>
        public static readonly IReadOnlyList<(string Name, int Stars)> Ranks = new[]
        {
            ("Trainee", 0), ("Operator", 4), ("Technician", 10), ("Specialist", 20), ("Safety lead", 32),
        };

        /// <summary>The rank reached with <paramref name="stars"/>, the stars the next one needs (0 at the top) and
        /// how far along to it (0 to 1; 1 at the top).</summary>
        public static (string Name, int NextAt, float Progress) For(int stars)
        {
            stars = Math.Max(stars, 0);
            int index = 0;
            for (int i = 0; i < Ranks.Count; i++)
            {
                if (stars >= Ranks[i].Stars)
                    index = i;
            }
            if (index == Ranks.Count - 1)
                return (Ranks[index].Name, 0, 1f);
            int from = Ranks[index].Stars, to = Ranks[index + 1].Stars;
            return (Ranks[index].Name, to, (stars - from) / (float)(to - from));
        }
    }
}
