using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Electrical safety (M27, ahead of M20): where on the body a PPE item goes on.</summary>
    public enum PpeZone
    {
        Head = 0,
        Body = 1,
    }

    /// <summary>Electrical safety (M27, ahead of M20): one item of the arc flash PPE kit and how it goes on.</summary>
    public sealed class PpeKitItem
    {
        public PpeKitItem(string name, PpeZone zone, string howTo)
        {
            Name = name;
            Zone = zone;
            HowTo = howTo;
        }

        public string Name { get; }
        public PpeZone Zone { get; }
        public string HowTo { get; }
    }

    /// <summary>
    /// Electrical safety (M27, ahead of M20): the arc flash PPE kit for the demo label (DS-7, 6.1 cal/cm², PPE
    /// category 2, NFPA 70E Table 130.7(C)(15)(c)), in the order it goes on: the layers next to the skin first, the
    /// gloves last so the hands work for the rest. Pure, so the order and its refusals are tested outside Unity.
    /// A training demo: the order is good practice for the drill, not a rule quoted from the standard.
    /// </summary>
    public sealed class PpeDressing
    {
        public const int Category = 2;

        public static readonly IReadOnlyList<PpeKitItem> Kit = new[]
        {
            new PpeKitItem("Arc-rated balaclava", PpeZone.Head, "pull it over your head: hold it to your head"),
            new PpeKitItem("Safety glasses", PpeZone.Head, "hold them to your eyes"),
            new PpeKitItem("Hearing protection", PpeZone.Head, "hold the ear muffs to your head"),
            new PpeKitItem("Hard hat with arc-rated face shield", PpeZone.Head, "hold it to your head"),
            new PpeKitItem("Arc-rated jacket", PpeZone.Body, "hold it against your chest"),
            new PpeKitItem("Rubber insulating gloves, Class 0, with leather protectors", PpeZone.Body, "hold them against your chest"),
        };

        /// <summary>How many items are on, in order.</summary>
        public int Worn { get; private set; }

        public bool Complete => Worn >= Kit.Count;

        /// <summary>The item to put on next, or null when the kit is complete.</summary>
        public PpeKitItem Next => Complete ? null : Kit[Worn];

        /// <summary>Puts item <paramref name="index"/> on if it is the next one; false (refused) otherwise.</summary>
        public bool TryWear(int index)
        {
            if (Complete || index != Worn)
                return false;
            Worn++;
            return true;
        }

        public void Reset() => Worn = 0;
    }
}
