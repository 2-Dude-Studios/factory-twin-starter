using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: plays particle effects (sparks, steam) on the machine while cell/&lt;station&gt;/health reports
    /// Fault, and stops them when the fault clears. The effects are set to loop at runtime so a burst effect keeps
    /// going for as long as the fault lasts; nothing reads the machine itself.
    /// </summary>
    public sealed class FaultEffects : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("Station segment of the health topic, e.g. press for cell/press/health.")]
        [SerializeField] private string station = "press";
        [SerializeField] private ParticleSystem[] effects;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool playing;

        private IDisposable _subscription;
        private TopicPath _topic;

        private void Awake()
        {
            _topic = new TopicPath(station, "health");
        }

        private void OnEnable()
        {
            SetPlaying(false, force: true);
            SignalBus bus = context.Bus;
            if (bus.TryGetLast(_topic, out SignalMessage last))
                OnHealth(last);
            _subscription = bus.Subscribe(_topic.ToString(), OnHealth);
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
            SetPlaying(false, force: true);
        }

        private void OnHealth(SignalMessage message)
        {
            SetPlaying((MachineHealth)(int)message.Value.AsNumber == MachineHealth.Fault, force: false);
        }

        private void SetPlaying(bool on, bool force)
        {
            if (!force && on == playing)
                return;
            playing = on;
            if (effects == null)
                return;

            foreach (ParticleSystem effect in effects)
            {
                if (effect == null)
                    continue;
                if (on)
                {
                    ParticleSystem.MainModule main = effect.main;
                    main.loop = true;
                    effect.Play(true);
                }
                else
                {
                    effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }
    }
}
