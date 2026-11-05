using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Fire safety (M14): the flames, smoke, sparks and fire light at one location, driven only by cell/fire/*. Three
    /// flame tiers come on as the fire grows (small from ignition, medium from the growing stage, large beyond
    /// incipient); smoke emission follows cell/fire/smoke; sparks play while the drive cabinet smokes. Nothing plays
    /// unless cell/fire/location is this location.
    /// </summary>
    public sealed class FireEffects : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private FireLocation location = FireLocation.DriveCabinet;
        [SerializeField] private ParticleSystem[] smallFlames;
        [SerializeField] private ParticleSystem[] mediumFlames;
        [SerializeField] private ParticleSystem[] largeFlames;
        [Tooltip("Smoke systems; their emission rate scales with cell/fire/smoke.")]
        [SerializeField] private ParticleSystem[] smoke;
        [Tooltip("Played from the smoke stage until the flame takes hold (arcing in the drive cabinet).")]
        [SerializeField] private ParticleSystem[] sparks;
        [Tooltip("One flickering point light at most: the VR pipeline budget.")]
        [SerializeField] private Light fireLight;
        [SerializeField, Min(0f)] private float lightIntensity = 6f;
        [SerializeField, Range(0f, 1f)] private float mediumAt = 0.3f;
        [SerializeField, Range(0f, 1f)] private float largeAt = 0.6f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool here;
        [SerializeField] private float size;
        [SerializeField] private float smokeLevel;

        private IDisposable _subscription;
        private float[] _smokeRates;
        private bool _small, _medium, _large, _sparks;
        private FireStage _stage;

        private void Awake()
        {
            _smokeRates = new float[smoke?.Length ?? 0];
            for (int i = 0; i < _smokeRates.Length; i++)
                _smokeRates[i] = smoke[i] != null ? smoke[i].emission.rateOverTimeMultiplier : 0f;
        }

        private void OnEnable()
        {
            if (context == null)
            {
                Debug.LogError("[FireEffects] Assign Context (Cell).", this);
                enabled = false;
                return;
            }
            StopAll();
            SignalBus bus = context.Bus;
            Seed(bus, CellTopics.FireLocation);
            Seed(bus, CellTopics.FireSize);
            Seed(bus, CellTopics.FireSmoke);
            Seed(bus, CellTopics.FireStage);
            _subscription = bus.Subscribe("cell/fire/+", OnFire);
        }

        // Enabled mid-fire: pick up where the fire is now.
        private void Seed(SignalBus bus, TopicPath topic)
        {
            if (bus.TryGetLast(topic, out SignalMessage last))
                OnFire(last);
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
            StopAll();
        }

        private void Update()
        {
            if (fireLight == null || !fireLight.enabled)
                return;
            float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 7f, 0.37f);
            fireLight.intensity = lightIntensity * Mathf.Lerp(0.3f, 1f, size) * flicker;
        }

        private void OnFire(SignalMessage message)
        {
            TopicPath topic = message.Topic;
            double v = message.Value.AsNumber;
            if (topic == CellTopics.FireLocation)
                here = (FireLocation)(int)v == location;
            else if (topic == CellTopics.FireSize)
                size = (float)v;
            else if (topic == CellTopics.FireSmoke)
                smokeLevel = (float)v;
            else if (topic == CellTopics.FireStage)
                _stage = (FireStage)(int)v;
            else
                return;
            Apply();
        }

        private void Apply()
        {
            bool burning = here && size > 0f && (_stage == FireStage.Incipient || _stage == FireStage.Growing || _stage == FireStage.BeyondIncipient);
            Set(smallFlames, burning, ref _small);
            Set(mediumFlames, burning && size >= mediumAt, ref _medium);
            Set(largeFlames, burning && size >= largeAt, ref _large);
            Set(sparks, here && (_stage == FireStage.Smoke || (_stage == FireStage.Incipient && size < mediumAt * 0.5f)), ref _sparks);

            float level = here ? smokeLevel : 0f;
            for (int i = 0; i < _smokeRates.Length; i++)
            {
                ParticleSystem system = smoke[i];
                if (system == null)
                    continue;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTimeMultiplier = _smokeRates[i] * level;
                if (level > 0f && !system.isPlaying)
                    system.Play(true);
                else if (level <= 0f && system.isPlaying)
                    system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (fireLight != null)
                fireLight.enabled = burning;
        }

        private static void Set(ParticleSystem[] systems, bool on, ref bool state)
        {
            if (on == state)
                return;
            state = on;
            if (systems == null)
                return;
            foreach (ParticleSystem system in systems)
            {
                if (system == null)
                    continue;
                if (on)
                {
                    ParticleSystem.MainModule main = system.main;
                    main.loop = true;
                    system.Play(true);
                }
                else
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private void StopAll()
        {
            _small = _medium = _large = _sparks = true;
            Set(smallFlames, false, ref _small);
            Set(mediumFlames, false, ref _medium);
            Set(largeFlames, false, ref _large);
            Set(sparks, false, ref _sparks);
            if (smoke != null)
            {
                foreach (ParticleSystem system in smoke)
                {
                    if (system != null)
                        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            if (fireLight != null)
                fireLight.enabled = false;
        }
    }
}
