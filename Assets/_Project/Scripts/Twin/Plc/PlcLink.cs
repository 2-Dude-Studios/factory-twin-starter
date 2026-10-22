using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Soft PLC link: connects the bus to a soft PLC over Modbus TCP (OpenPLC Runtime v4). A background thread polls
    /// the PLC: it writes the sensor registers from the I/O map, then reads the command coils and status registers.
    /// While cell/plc/source says PLC, the PLC's commands are published on the bus in place of CellSequencer's,
    /// and if the PLC's heartbeat stops the line is held. The PLC address comes from the CellContext's TwinConfig.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlcLink : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private PlcIoMap ioMap;
        [Tooltip("Logic source published when Play starts. Change it at runtime on cell/plc/source.")]
        [SerializeField] private LogicSource startSource = LogicSource.Unity;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private LogicSource source;
        [SerializeField] private bool linkUp;
        [SerializeField] private int replies;
        [SerializeField] private int errors;
        [SerializeField] private float roundTripMs;
        [SerializeField] private string lastError;

        private readonly object _gate = new object();
        private readonly List<(TopicPath Topic, SignalValue Value)> _decoded = new List<(TopicPath, SignalValue)>();
        private readonly Dictionary<TopicPath, SignalValue> _published = new Dictionary<TopicPath, SignalValue>();

        private PlcImage _image;
        private PlcWatchdog _watchdog;
        private SignalBus _bus;
        private Func<TopicPath, double> _readBus;
        private Poller _poller;

        // Main thread → poll thread: the register block to write next. Guarded by _gate.
        private ushort[] _writeScratch;
        private ushort[] _writeShared;

        // Poll thread → main thread: the latest reply. Guarded by _gate.
        private bool[] _coilsShared;
        private ushort[] _registersShared;
        private int _replySeq;
        private int _errorCount;
        private double _roundTripMs;
        private string _error;
        private int _seenSeq;

        private bool _holding;
        private bool _lastLink;
        private bool _linkPublished;

        private void Awake()
        {
            _bus = context.Bus;
            _readBus = ReadBus;
            try
            {
                if (context.Config == null)
                    throw new InvalidOperationException("No TwinConfig on the CellContext; the PLC address lives there.");
                if (ioMap == null)
                    throw new InvalidOperationException("No PlcIoMap assigned.");
                _image = ioMap.BuildImage();
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException || e is FormatException)
            {
                Debug.LogError($"[PlcLink] {e.Message}", this);
                enabled = false;
                return;
            }

            _watchdog = new PlcWatchdog(context.Config.PlcWatchdogSeconds);
            _writeScratch = new ushort[_image.WriteBlock.Count];
            _writeShared = new ushort[_image.WriteBlock.Count];
        }

        private void Start()
        {
            _bus.Publish(CellTopics.PlcSource, SignalValue.FromNumber((int)startSource), CellContext.NowUnixMs);
        }

        private void OnEnable()
        {
            if (_image == null)
            {
                enabled = false;
                return;
            }

            TwinConfig config = context.Config;
            _poller = new Poller(this, config.PlcHost, config.PlcPort, config.PlcUnitId, config.PlcIoTimeoutMs,
                Mathf.Max(1, Mathf.RoundToInt(1000f / config.PlcPollHz)));
            _watchdog.Reset();
        }

        private void OnDisable()
        {
            _poller?.Stop();
            _poller = null;

            // Hand the line back, so it is never left running on the PLC's last commands with nobody watching.
            if (_bus != null && LogicSourceRule.Read(_bus) == LogicSource.Plc)
                _bus.Publish(CellTopics.PlcSource, SignalValue.FromNumber((int)LogicSource.Unity), CellContext.NowUnixMs);
            if (_bus != null && _lastLink)
                _bus.Publish(CellTopics.PlcLink, SignalValue.FromBool(false), CellContext.NowUnixMs);
            _lastLink = false;
            _linkPublished = false;
            Release();
        }

        private void FixedUpdate()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            _image.FillWriteBlock(_writeScratch, _readBus, (ushort)now);
            lock (_gate)
                Array.Copy(_writeScratch, _writeShared, _writeScratch.Length);

            TakeReply(now);
            bool alive = _watchdog.IsAlive(now);
            if (!_linkPublished || alive != _lastLink)
            {
                _bus.Publish(CellTopics.PlcLink, SignalValue.FromBool(alive), CellContext.NowUnixMs);
                _lastLink = alive;
                _linkPublished = true;
            }

            source = LogicSourceRule.Read(_bus);
            if (source == LogicSource.Plc)
                Drive(alive);
            else
                Release();

            linkUp = alive;
        }

        private void TakeReply(double now)
        {
            lock (_gate)
            {
                errors = _errorCount;
                lastError = _error;
                roundTripMs = (float)_roundTripMs;
                if (_replySeq == _seenSeq)
                    return;
                _seenSeq = _replySeq;
                replies = _replySeq;
                _image.Decode(_coilsShared, _registersShared, _decoded);
            }

            for (int i = 0; i < _decoded.Count; i++)
            {
                if (_decoded[i].Topic == CellTopics.PlcHeartbeat)
                    _watchdog.Observe((ushort)_decoded[i].Value.AsNumber, now);
            }
        }

        /// <summary>The PLC runs the line: publish its commands, or hold the line if it has gone quiet.</summary>
        private void Drive(bool alive)
        {
            if (!alive)
            {
                if (_holding)
                    return;
                long at = CellContext.NowUnixMs;
                PublishIfChanged(CellTopics.InfeedDriveForward, SignalValue.FromBool(false), at);
                PublishIfChanged(CellTopics.OutfeedDriveForward, SignalValue.FromBool(false), at);
                PublishIfChanged(CellTopics.ArmPickCommand, SignalValue.FromBool(false), at);
                PublishIfChanged(CellTopics.LineState, SignalValue.FromNumber((int)CellState.Held), at);
                _holding = true;
                return;
            }

            _holding = false;
            long unixMs = CellContext.NowUnixMs;
            for (int i = 0; i < _decoded.Count; i++)
                PublishIfChanged(_decoded[i].Topic, _decoded[i].Value, unixMs);
        }

        /// <summary>Unity runs the line: forget what was published, so taking over again re-sends everything.</summary>
        private void Release()
        {
            _published.Clear();
            _holding = false;
        }

        private void PublishIfChanged(TopicPath topic, SignalValue value, long unixMs)
        {
            if (_published.TryGetValue(topic, out SignalValue last) && last.Equals(value))
                return;
            _published[topic] = value;
            _bus.Publish(topic, value, unixMs);
        }

        private double ReadBus(TopicPath topic)
        {
            return _bus.TryGetLast(topic, out SignalMessage message) ? message.Value.AsNumber : 0d;
        }

        /// <summary>
        /// One poll thread with its own stop flag and client, so a slow stop can never leave two threads polling.
        /// Touches only its client, its own buffers and the owner's shared fields under the owner's lock.
        /// </summary>
        private sealed class Poller
        {
            private readonly PlcLink _owner;
            private readonly ModbusClient _client;
            private readonly int _periodMs;
            private readonly int _timeoutMs;
            private readonly Thread _thread;
            private volatile bool _running = true;

            public Poller(PlcLink owner, string host, int port, byte unitId, int timeoutMs, int periodMs)
            {
                _owner = owner;
                _client = new ModbusClient(host, port, unitId, timeoutMs);
                _periodMs = periodMs;
                _timeoutMs = timeoutMs;
                _thread = new Thread(Run) { IsBackground = true, Name = "PlcLink" };
                _thread.Start();
            }

            public void Stop()
            {
                _running = false;
                _client.Close();   // unblocks a pending read or write
                if (!_thread.Join(_timeoutMs * 2 + 100))
                    Debug.LogWarning("[PlcLink] The poll thread did not stop in time; it exits at its next timeout.");
            }

            private void Run()
            {
                PlcImage image = _owner._image;
                var write = new ushort[image.WriteBlock.Count];
                var noCoils = Array.Empty<bool>();
                var noRegisters = Array.Empty<ushort>();
                var clock = new Stopwatch();

                while (_running)
                {
                    clock.Restart();
                    try
                    {
                        if (!_client.IsConnected)
                            _client.Connect();

                        lock (_owner._gate)
                            Array.Copy(_owner._writeShared, write, write.Length);
                        _client.WriteHoldingRegisters(image.WriteBlock.Start, write);

                        bool[] coils = image.CoilBlock.Count > 0
                            ? _client.ReadCoils(image.CoilBlock.Start, image.CoilBlock.Count)
                            : noCoils;
                        ushort[] registers = image.RegisterBlock.Count > 0
                            ? _client.ReadHoldingRegisters(image.RegisterBlock.Start, image.RegisterBlock.Count)
                            : noRegisters;

                        lock (_owner._gate)
                        {
                            if (!_running)
                                break;
                            _owner._coilsShared = coils;
                            _owner._registersShared = registers;
                            _owner._replySeq++;
                            _owner._roundTripMs = clock.Elapsed.TotalMilliseconds;
                            _owner._error = null;
                        }
                    }
                    catch (Exception e)
                    {
                        // Any failure (refused, reset, timeout, a malformed reply) closes the socket and retries.
                        _client.Close();
                        if (!_running)
                            break;
                        lock (_owner._gate)
                        {
                            _owner._errorCount++;
                            _owner._error = e.Message;
                        }
                        Sleep(1000);
                        continue;
                    }

                    Sleep(_periodMs - (int)clock.ElapsedMilliseconds);
                }

                _client.Close();
            }

            // Sleeps in short slices so Stop never waits long for the thread.
            private void Sleep(int ms)
            {
                while (ms > 0 && _running)
                {
                    int slice = Math.Min(ms, 50);
                    Thread.Sleep(slice);
                    ms -= slice;
                }
            }
        }
    }
}
