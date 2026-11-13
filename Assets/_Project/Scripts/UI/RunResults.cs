using System;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>The headset hub's game layer (M28): one finished run, as the results card shows it.</summary>
    public sealed class RunResult
    {
        public TrainingMode Mode;
        /// <summary>The level (1 to 4), or 0 for a training without levels.</summary>
        public int Level;
        public string Title = string.Empty;
        /// <summary>Stars earned (0 to 3), or -1 for a training scored without stars.</summary>
        public int Stars = -1;
        public int Points;
        public bool Passed;
        public bool NewBest;
        public IReadOnlyList<string> NewBadges = Array.Empty<string>();
        public XpGain Gain;
        public int XpBefore;
        public int XpAfter;
        /// <summary>What to look at again: critical mistakes, missed steps, mistakes (<see cref="RunDebrief"/>).</summary>
        public IReadOnlyList<string> Debrief = Array.Empty<string>();
    }

    /// <summary>
    /// The headset hub's game layer (M28): every scorer (robot cell, lockout, fire) announces a finished run here once
    /// it has written the record and the badge book; the results card listens. Cleared on entering Play, so a
    /// listener from an earlier session never lingers.
    /// </summary>
    public static class RunResults
    {
        public static event Action<RunResult> Published;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Published = null;

        public static void Publish(RunResult result)
        {
            if (result != null)
                Published?.Invoke(result);
        }
    }
}
