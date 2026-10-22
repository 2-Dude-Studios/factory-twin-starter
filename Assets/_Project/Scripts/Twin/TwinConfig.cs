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

        [Header("Soft PLC (Modbus TCP)")]
        [SerializeField] private string plcHost = "127.0.0.1";
        [SerializeField, Range(1, 65535)] private int plcPort = 502;
        [SerializeField, Range(0, 255)] private int plcUnitId = 1;
        [Tooltip("Read/write cycles per second against the PLC.")]
        [SerializeField, Range(5f, 100f)] private float plcPollHz = 50f;
        [Tooltip("The line holds if the PLC's heartbeat has not moved for this long.")]
        [SerializeField, Range(0.1f, 5f)] private float plcWatchdogSeconds = 0.5f;
        [SerializeField, Range(50, 2000)] private int plcIoTimeoutMs = 250;

        public string BrokerHost => brokerHost;
        public int BrokerPort => brokerPort;
        public string ClientId => clientId;
        public float ReconnectSeconds => reconnectSeconds;
        public float PublishRateHz => publishRateHz;
        public float DefaultDeadband => defaultDeadband;
        public string PlcHost => plcHost;
        public int PlcPort => plcPort;
        public byte PlcUnitId => (byte)plcUnitId;
        public float PlcPollHz => plcPollHz;
        public float PlcWatchdogSeconds => plcWatchdogSeconds;
        public int PlcIoTimeoutMs => plcIoTimeoutMs;
    }
}
