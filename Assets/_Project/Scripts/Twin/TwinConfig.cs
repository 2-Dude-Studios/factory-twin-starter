using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Data spine: the single source of broker and publishing settings (Assets/_Project/Data/TwinConfig.asset).</summary>
    [CreateAssetMenu(menuName = "Factory Twin/Twin Config", fileName = "TwinConfig")]
    public sealed class TwinConfig : ScriptableObject
    {
        [Header("MQTT broker")]
        [SerializeField] private string brokerHost = "127.0.0.1";
        [SerializeField] private int brokerPort = 1883;
        [SerializeField] private string clientId = "factory-twin-unity";
        [SerializeField, Min(1f)] private float reconnectSeconds = 5f;

        [Header("Publishing")]
        [Tooltip("Upper limit per signal. Signals also publish only when they move by more than the deadband.")]
        [SerializeField, Range(1f, 60f)] private float publishRateHz = 10f;
        [SerializeField, Min(0f)] private float defaultDeadband = 0.001f;

        public string BrokerHost => brokerHost;
        public int BrokerPort => brokerPort;
        public string ClientId => clientId;
        public float ReconnectSeconds => reconnectSeconds;
        public float PublishRateHz => publishRateHz;
        public float DefaultDeadband => defaultDeadband;
    }
}
