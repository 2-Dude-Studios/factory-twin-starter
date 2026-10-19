using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Press station: how bright each stack-light lamp and the machine glow are at one moment, 0 to 1.</summary>
    public readonly struct StackLightOutput
    {
        public float Green { get; }
        public float Amber { get; }
        public float Red { get; }

        /// <summary>The coloured light cast onto the machine; 0 means switched off.</summary>
        public float Glow { get; }

        public StackLightOutput(float green, float amber, float red, float glow)
        {
            Green = green;
            Amber = amber;
            Red = red;
            Glow = glow;
        }
    }

    /// <summary>
    /// Press station: the stack-light pattern for a machine health, like a real andon tower. Healthy is steady
    /// green, warning steady amber with a soft glow, fault flashing red with a pulsing glow. Before any health is
    /// known every lamp sits at its dim idle level.
    /// </summary>
    public static class StackLightPattern
    {
        public const float Idle = 0.12f;
        public const double FlashHz = 1.5;

        public static StackLightOutput Evaluate(bool known, MachineHealth health, double timeSeconds)
        {
            if (!known)
                return new StackLightOutput(Idle, Idle, Idle, 0f);

            switch (health)
            {
                case MachineHealth.Ok:
                    return new StackLightOutput(1f, Idle, Idle, 0f);
                case MachineHealth.Warning:
                    return new StackLightOutput(Idle, 1f, Idle, 0.5f);
                default:
                    double phase = timeSeconds * FlashHz - Math.Floor(timeSeconds * FlashHz);
                    float red = phase < 0.5 ? 1f : Idle;
                    float glow = (float)(0.6 + 0.4 * Math.Cos(phase * 2.0 * Math.PI));
                    return new StackLightOutput(Idle, Idle, red, glow);
            }
        }
    }
}
