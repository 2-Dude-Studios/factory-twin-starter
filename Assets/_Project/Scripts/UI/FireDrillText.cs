using System.Globalization;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): what the tablet says during a fire drill. Where the fire is and how far along, the
    /// extinguisher in hand, and one line of advice at a time, most urgent first. Pure, so it is tested.
    /// </summary>
    public static class FireDrillText
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Fire(FireLocation location, FireClass fireClass, FireStage stage, bool fleeOnly)
        {
            if (stage == FireStage.None || location == FireLocation.None)
                return "No fire";
            string where = Capitalise(FireWords.Location(location));
            string what = fleeOnly ? "Lithium-ion battery" : FireWords.Class(fireClass);
            return $"{where} · {what} · {FireWords.Stage(stage)}";
        }

        public static string Extinguisher(FireAgent agent, float charge)
        {
            if (agent == FireAgent.None)
                return "No extinguisher in hand";
            int percent = (int)System.Math.Round(charge * 100f);
            return $"{ExtinguisherRules.Name(agent)} ({ExtinguisherRules.Rating(agent)}) · {percent.ToString(Invariant)} %";
        }

        /// <summary>The one thing to do or fix now.</summary>
        public static string Advice(FireStage stage, FireClass fireClass, bool fleeOnly, bool extinguished, bool alarm,
            FireAgent agent, float charge, bool discharging, float aim, float standoff, float distance,
            bool pinPulled = true, bool hoseHeld = true, bool tooClose = false)
        {
            switch (stage)
            {
                case FireStage.None:
                    return "Start a fire, then take the extinguisher that suits its class.";
                case FireStage.Out:
                    return extinguished
                        ? "Fire out. Watch it for reflash, then report it."
                        : "Knocked down: keep the stream on it a moment longer.";
            }
            if (tooClose && FireProximity.Burning(stage))
            {
                if (agent == FireAgent.None)
                    return "Too close: the heat will burn you. Step back out of the red ring.";
                (double _, double from, double to, double _) = ExtinguisherRules.Range(agent);
                return $"Too close: step back out of the red ring, to {from.ToString("0.#", Invariant)}–{to.ToString("0.#", Invariant)} m.";
            }
            if (fleeOnly)
                return "Lithium-ion battery: do not fight it. Raise the alarm and leave.";
            if (stage == FireStage.BeyondIncipient)
                return "Beyond the incipient stage: stop. Raise the alarm and evacuate.";
            if (!alarm)
                return stage == FireStage.Smoke ? "Smoke: raise the alarm first." : "Raise the alarm before you fight it.";
            if (stage == FireStage.Smoke)
                return "Smoke, no flame yet. Keep your exit behind you and watch the source.";

            string cls = ClassLetter(fireClass);
            if (agent == FireAgent.None)
                return $"Take an extinguisher for a Class {cls} fire from the stand: grip its handle.";
            if (charge <= 0f)
                return "Extinguisher empty: back away, keep the alarm on, and leave.";
            double effect = FireModel.Effectiveness(agent, fireClass);
            if (effect < 0d)
                return $"{ExtinguisherRules.Name(agent)} on a Class {cls} fire spreads it. Stop and change extinguisher.";
            if (effect < 0.5d)
                return $"{ExtinguisherRules.Name(agent)} barely touches a Class {cls} fire. Change extinguisher.";
            if (!pinPulled)
                return "Pull the pin: grab its ring with your other hand and pull.";
            if (!hoseHeld)
                return "Take the hose in your other hand and point it at the base of the fire.";
            if (!discharging)
                return "Aim at the base, then squeeze the lever (trigger on the hand holding it) and sweep.";
            if (standoff < 0.6f)
            {
                (double _, double bestFrom, double bestTo, double _) = ExtinguisherRules.Range(agent);
                string band = $"{bestFrom.ToString("0.#", Invariant)}–{bestTo.ToString("0.#", Invariant)} m";
                return distance < bestFrom ? $"Too close: step back to {band}." : $"Too far: move in to {band}.";
            }
            if (aim < 0.5f)
                return "Aim at the base of the fire, not the flames, and sweep.";
            return stage == FireStage.Growing ? "It is growing: keep sweeping the base, or back off and leave." : "Good: keep sweeping the base.";
        }

        public static string ClassLetter(FireClass fireClass)
        {
            switch (fireClass)
            {
                case FireClass.A: return "A";
                case FireClass.B: return "B";
                case FireClass.C: return "C";
                default: return "?";
            }
        }

        private static string Capitalise(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
