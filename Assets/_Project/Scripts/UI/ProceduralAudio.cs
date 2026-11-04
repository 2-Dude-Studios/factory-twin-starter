using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): sounds made in code, so no audio file (and no licence) is needed. A discharge hiss (noise,
    /// softened, looping without a click) and a short metallic pin click. The sample math is pure and tested.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        /// <summary>
        /// Noise for a pressurised discharge, <paramref name="seconds"/> long, -1 to 1 times <paramref name="level"/>.
        /// A one-pole low-pass takes the edge off (brighter for CO2, softer for water); the ends cross-fade so it loops.
        /// </summary>
        public static float[] HissSamples(float seconds, float brightness, float level, int seed)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            int fade = Mathf.Min(count / 4, SampleRate / 20);
            var samples = new float[count + fade];
            var random = new System.Random(seed);
            float alpha = Mathf.Clamp(brightness, 0.02f, 1f);
            float y = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float white = (float)(random.NextDouble() * 2d - 1d);
                y += alpha * (white - y);
                samples[i] = y;
            }
            // Normalise to the level, then fold the extra tail over the start so the loop point is seamless.
            float peak = 1e-6f;
            foreach (float s in samples)
                peak = Mathf.Max(peak, Mathf.Abs(s));
            var loop = new float[count];
            for (int i = 0; i < count; i++)
            {
                float s = samples[i];
                if (i < fade)
                {
                    float t = i / (float)fade;
                    s = s * t + samples[count + i] * (1f - t);
                }
                loop[i] = s / peak * level;
            }
            return loop;
        }

        /// <summary>A short ring-down at <paramref name="hertz"/>: the pin's click as it leaves the valve.</summary>
        public static float[] ClickSamples(float hertz, float seconds, float level)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-t * 60f);
                samples[i] = level * envelope * Mathf.Sin(2f * Mathf.PI * hertz * t);
            }
            return samples;
        }

        public static AudioClip Clip(string name, float[] samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
