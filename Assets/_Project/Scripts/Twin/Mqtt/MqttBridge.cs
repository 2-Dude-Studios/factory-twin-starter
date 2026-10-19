using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Data spine: the cell's link to the MQTT broker. Outbound traffic is whatever <see cref="SignalPublisher"/>
    /// releases; inbound messages are queued off the network thread and replayed onto the bus in Update, because
    /// the bus is main-thread only. Broker address, rate and deadband all come from TwinConfig.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class MqttBridge : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("What the cell subscribes to. Our own publishes are filtered out by the broker (MQTT 5 no-local).")]
        [SerializeField] private string subscribeFilter = TopicFilter.All;
        [SerializeField] private bool logTraffic;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool connected;
        [SerializeField] private int published;
        [SerializeField] private int received;
        [SerializeField] private int dropped;

        private readonly ConcurrentQueue<SignalMessage> _inbound = new ConcurrentQueue<SignalMessage>();
        private IMqttClient _client;
        private SignalPublisher _publisher;
        private CancellationTokenSource _life;
        private SignalBus _bus;
        private volatile bool _connected;
        private int _droppedCount;

        public bool IsConnected => _connected;
        public int Published => published;
        public int Received => received;

        private void OnEnable()
        {
            _bus = context.Bus;
            TwinConfig config = context.Config;
            if (config == null)
            {
                Debug.LogError("[MqttBridge] No TwinConfig on the CellContext; the broker address lives there.", this);
                enabled = false;
                return;
            }

            if (!TopicFilter.IsValid(subscribeFilter))
            {
                Debug.LogError($"[MqttBridge] '{subscribeFilter}' is not a valid topic filter.", this);
                enabled = false;
                return;
            }

            _publisher = new SignalPublisher(_bus, Send, config.PublishRateHz, config.DefaultDeadband);
            _life = new CancellationTokenSource();
            _ = RunAsync(config, _life.Token);
        }

        private void OnDisable()
        {
            _publisher?.Dispose();
            _publisher = null;
            _life?.Cancel();
            _life?.Dispose();
            _life = null;

            IMqttClient client = _client;
            _client = null;
            _connected = false;
            connected = false;
            if (client != null)
                _ = CloseAsync(client);
        }

        private static async Task CloseAsync(IMqttClient client)
        {
            try
            {
                await client.DisconnectAsync();
            }
            catch (Exception)
            {
                // Shutting down; a broker that already went away is not interesting.
            }
            finally
            {
                client.Dispose();
            }
        }

        private void Update()
        {
            while (_inbound.TryDequeue(out SignalMessage message))
            {
                received++;
                _publisher?.ExpectInbound(message);
                _bus.Publish(message);
            }

            if (connected != _connected)
            {
                connected = _connected;
                // The broker saw nothing while the link was down, so it gets the whole cell again.
                if (connected)
                    _publisher?.ResendAll();
                _bus.Publish(CellTopics.LinkConnected, SignalValue.FromBool(connected), CellContext.NowUnixMs);
            }

            // Hold changes back while the link is down; the publisher keeps only the latest value per topic.
            if (connected)
                _publisher?.Flush(Time.unscaledTimeAsDouble);
            if (_publisher != null)
                published = _publisher.Sent;
            dropped = _droppedCount;
        }

        private async Task RunAsync(TwinConfig config, CancellationToken token)
        {
            MqttClientOptions options = new MqttClientOptionsBuilder()
                .WithTcpServer(config.BrokerHost, config.BrokerPort)
                .WithClientId(config.ClientId)
                .WithProtocolVersion(MqttProtocolVersion.V500)
                .WithCleanSession()
                .Build();

            // Failed connects since the last success; only the first of a run is logged.
            int failedAttempts = 0;
            while (!token.IsCancellationRequested)
            {
                IMqttClient client = new MqttFactory().CreateMqttClient();
                client.ApplicationMessageReceivedAsync += OnBrokerMessage;
                try
                {
                    await client.ConnectAsync(options, token);

                    // No-local stops the broker echoing our own publishes straight back onto the bus.
                    await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic(subscribeFilter)
                                               .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                               .WithNoLocal(true))
                        .Build(), token);

                    _client = client;
                    _connected = true;
                    string after = failedAttempts > 0 ? $" after {failedAttempts} failed attempts" : string.Empty;
                    Debug.Log($"[MqttBridge] Connected to {config.BrokerHost}:{config.BrokerPort}{after}; subscribed to {subscribeFilter}.");
                    failedAttempts = 0;

                    while (!token.IsCancellationRequested && client.IsConnected)
                        await Task.Delay(250, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    // Warn once per outage; a broker-less demo would otherwise fill the player log every few seconds.
                    if (failedAttempts == 0)
                        Debug.LogWarning($"[MqttBridge] {config.BrokerHost}:{config.BrokerPort} unreachable ({e.Message}); retrying every {config.ReconnectSeconds:0.#} s without further warnings until it connects.");
                    failedAttempts++;
                }

                _connected = false;
                _client = null;
                client.ApplicationMessageReceivedAsync -= OnBrokerMessage;
                client.Dispose();

                if (token.IsCancellationRequested)
                    break;

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(config.ReconnectSeconds), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private Task OnBrokerMessage(MqttApplicationMessageReceivedEventArgs args)
        {
            ArraySegment<byte> payload = args.ApplicationMessage.PayloadSegment;
            string topic = args.ApplicationMessage.Topic;
            string json = payload.Array != null
                ? Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count)
                : string.Empty;

            if (TopicPath.TryParse(topic, out TopicPath path) && SignalPayload.TryParse(json, out SignalValue value, out long unixMs))
            {
                _inbound.Enqueue(new SignalMessage(path, value, unixMs));
                if (logTraffic)
                    Debug.Log($"[MqttBridge] in  {topic} {json}");
            }
            else
            {
                // Anything that is not our scheme is someone else's traffic on this broker.
                Interlocked.Increment(ref _droppedCount);
            }
            return Task.CompletedTask;
        }

        private void Send(SignalMessage message)
        {
            IMqttClient client = _client;
            if (client == null || !client.IsConnected)
                return;

            string json = SignalPayload.Serialize(message.Value, message.UnixMs);
            if (logTraffic)
                Debug.Log($"[MqttBridge] out {message.Topic} {json}");

            MqttApplicationMessage payload = new MqttApplicationMessageBuilder()
                .WithTopic(message.Topic.ToString())
                .WithPayload(json)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            // Fire and forget: a slow broker must never stall the simulation.
            client.PublishAsync(payload, CancellationToken.None).ContinueWith(
                t => Debug.LogWarning($"[MqttBridge] publish of {message.Topic} failed: {t.Exception?.GetBaseException().Message}"),
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
