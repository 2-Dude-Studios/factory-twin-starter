using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: drives a stack light and a coloured glow light from cell/&lt;station&gt;/health, so the 3D
    /// machine shows the same health the dashboard does. Lamps are tinted through a MaterialPropertyBlock
    /// (_BaseColor and _EmissionColor on a URP Lit material with emission on); nothing reads the machine itself.
    /// While cell/&lt;station&gt;/isolated is set (M11) the machine has no power, so every lamp is dark.
    /// </summary>
    public sealed class StackLight : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private CellContext context;
        [Tooltip("Station segment of the health topic, e.g. press for cell/press/health.")]
        [SerializeField] private string station = "press";
        [SerializeField] private Renderer greenLamp;
        [SerializeField] private Renderer amberLamp;
        [SerializeField] private Renderer redLamp;
        [Tooltip("Optional light that casts the warning or fault colour onto the machine.")]
        [SerializeField] private Light glow;
        [SerializeField] private Color green = new Color(0.15f, 0.85f, 0.35f);
        [SerializeField] private Color amber = new Color(1f, 0.68f, 0.05f);
        [SerializeField] private Color red = new Color(1f, 0.12f, 0.08f);
        [Tooltip("HDR multiplier on a lit lamp's emission, so bloom picks it up.")]
        [SerializeField, Min(0f)] private float lampEmission = 4f;
        [Tooltip("Glow light intensity at full pulse.")]
        [SerializeField, Min(0f)] private float glowIntensity = 3f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool known;
        [SerializeField] private MachineHealth health;
        [SerializeField] private bool isolated;

        private MaterialPropertyBlock _block;
        private IDisposable _subscription;
        private IDisposable _isolatedSubscription;
        private TopicPath _topic;
        private TopicPath _isolatedTopic;
        private StackLightOutput _shown = new StackLightOutput(-1f, -1f, -1f, -1f);
        private MachineHealth _shownHealth;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _topic = new TopicPath(station, "health");
            _isolatedTopic = new TopicPath(station, "isolated");
        }

        private void OnEnable()
        {
            SignalBus bus = context.Bus;
            if (bus.TryGetLast(_topic, out SignalMessage last))
                OnHealth(last);
            _subscription = bus.Subscribe(_topic.ToString(), OnHealth);
            if (bus.TryGetLast(_isolatedTopic, out SignalMessage power))
                OnIsolated(power);
            _isolatedSubscription = bus.Subscribe(_isolatedTopic.ToString(), OnIsolated);
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
            _isolatedSubscription?.Dispose();
            _isolatedSubscription = null;
        }

        private void OnIsolated(SignalMessage message)
        {
            isolated = message.Value.AsNumber != 0d;
        }

        private void OnHealth(SignalMessage message)
        {
            known = true;
            health = (MachineHealth)(int)message.Value.AsNumber;
        }

        private void Update()
        {
            StackLightOutput output = isolated
                ? StackLightOutput.Dark
                : StackLightPattern.Evaluate(known, health, Time.timeAsDouble);

            if (output.Green != _shown.Green)
                SetLamp(greenLamp, green, output.Green);
            if (output.Amber != _shown.Amber)
                SetLamp(amberLamp, amber, output.Amber);
            if (output.Red != _shown.Red)
                SetLamp(redLamp, red, output.Red);

            // Only a fault's pulse changes every frame; otherwise the light is left alone.
            if (glow != null && (output.Glow != _shown.Glow || health != _shownHealth))
            {
                glow.enabled = output.Glow > 0f;
                glow.color = health == MachineHealth.Warning ? amber : red;
                glow.intensity = output.Glow * glowIntensity;
            }
            _shown = output;
            _shownHealth = health;
        }

        private void SetLamp(Renderer lamp, Color color, float level)
        {
            if (lamp == null)
                return;
            _block.Clear();
            _block.SetColor(BaseColorId, color * Mathf.Lerp(0.3f, 1f, level));
            _block.SetColor(EmissionColorId, color * (level * lampEmission));
            lamp.SetPropertyBlock(_block);
        }
    }
}
