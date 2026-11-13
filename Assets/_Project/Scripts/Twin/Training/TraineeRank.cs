using System;
using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// The headset hub's game layer (M28): the trainee's rank from their XP (every point a run earns; see
    /// <see cref="TraineeProgress"/>). Pure, so the thresholds are tested outside Unity. A training demo's game rank,
    /// not a qualification.
    /// </summary>
    public static class TraineeRank
    {
        /// <summary>The ranks in order and the XP each starts at.</summary>
        public static readonly IReadOnlyList<(string Name, int Xp)> Ranks = new[]
        {
            ("Trainee", 0), ("Operator", 500), ("Technician", 1500), ("Specialist", 3500), ("Safety lead", 7000),
        };

        /// <summary>The rank reached with <paramref name="xp"/>, the XP the next one needs (0 at the top) and how far
        /// along to it (0 to 1; 1 at the top).</summary>
        public static (string Name, int NextAt, float Progress) For(int xp)
        {
            xp = Math.Max(xp, 0);
            int index = 0;
            for (int i = 0; i < Ranks.Count; i++)
            {
                if (xp >= Ranks[i].Xp)
                    index = i;
            }
            if (index == Ranks.Count - 1)
                return (Ranks[index].Name, 0, 1f);
            int from = Ranks[index].Xp, to = Ranks[index + 1].Xp;
            return (Ranks[index].Name, to, (xp - from) / (float)(to - from));
        }
    }
}
