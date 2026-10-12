using System;
using System.Collections.Generic;
using realvirtual;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Data spine: the only bridge between realvirtual signals and the signal bus. PLC inputs (sensor and drive
    /// state coming out of the simulation) are published to cell/&lt;station&gt;/&lt;signal&gt; when they change;
    /// PLC outputs (commands into the simulation) are written whenever their topic is published on the bus, and
    /// published themselves when a station writes one directly, so every signal in the cell is visible on the bus.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class TwinSignals : MonoBehaviour
    {
        [Serializable]
        private sealed class Binding
        {
            [SerializeField] private string station;
            [SerializeField] private string signal;
            [SerializeField] private Signal target;

            public string Station => station;
            public string SignalName => signal;
            public Signal Target => target;
        }

        [SerializeField] private CellContext context;
        [SerializeField] private List<Binding> bindings = new List<Binding>();

        private readonly List<Watched> _watched = new List<Watched>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private SignalBus _bus;
        private double _deadband;

        private void OnEnable()
        {
            _bus = context.Bus;
            _deadband = context.Config != null ? context.Config.DefaultDeadband : 0d;

            foreach (Binding binding in bindings)
            {
                if (binding.Target == null)
                {
                    Debug.LogError($"[TwinSignals] cell/{binding.Station}/{binding.SignalName} has no realvirtual signal assigned.", this);
                    continue;
                }

                var watched = new Watched(new TopicPath(binding.Station, binding.SignalName), binding.Target);
                _watched.Add(watched);
                if (!binding.Target.IsInput())
                    _subscriptions.Add(_bus.Subscribe(watched.Topic.ToString(), message => WriteFromBus(watched, message.Value)));
            }
        }

        private void OnDisable()
        {
            foreach (IDisposable subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
            _watched.Clear();
        }

        private void FixedUpdate()
        {
            long now = CellContext.NowUnixMs;
            for (int i = 0; i < _watched.Count; i++)
            {
                Watched watched = _watched[i];
                if (!TryRead(watched.Signal, out SignalValue value))
                    continue;
                if (watched.HasValue && !Changed(watched.Last, value))
                    continue;

                watched.Last = value;
                watched.HasValue = true;
                _bus.Publish(watched.Topic, value, now);
            }
        }

        private bool Changed(SignalValue last, SignalValue value)
        {
            if (last.Kind != value.Kind)
                return true;
            if (value.Kind == SignalKind.Bool)
                return last.Bool != value.Bool;
            return Math.Abs(last.Number - value.Number) > _deadband;
        }

        private static bool TryRead(Signal signal, out SignalValue value)
        {
            switch (signal)
            {
                case PLCInputBool b:
                    value = SignalValue.FromBool(b.Value);
                    return true;
                case PLCInputFloat f:
                    value = SignalValue.FromNumber(f.Value);
                    return true;
                case PLCInputInt n:
                    value = SignalValue.FromNumber(n.Value);
                    return true;
                case PLCOutputBool b:
                    value = SignalValue.FromBool(b.Value);
                    return true;
                case PLCOutputFloat f:
                    value = SignalValue.FromNumber(f.Value);
                    return true;
                case PLCOutputInt n:
                    value = SignalValue.FromNumber(n.Value);
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        private void WriteFromBus(Watched watched, SignalValue value)
        {
            Write(watched.Signal, value);

            // This command is already on the bus; remember it so FixedUpdate does not publish it a second time.
            if (TryRead(watched.Signal, out SignalValue written))
            {
                watched.Last = written;
                watched.HasValue = true;
            }
        }

        private void Write(Signal signal, SignalValue value)
        {
            switch (signal)
            {
                case PLCOutputBool b:
                    b.Value = value.AsNumber != 0d;
                    break;
                case PLCOutputFloat f:
                    f.Value = (float)value.AsNumber;
                    break;
                case PLCOutputInt n:
                    n.Value = (int)Math.Round(value.AsNumber);
                    break;
                default:
                    Debug.LogWarning($"[TwinSignals] {signal.GetType().Name} on '{signal.name}' is not a supported output type.", signal);
                    break;
            }
        }

        private sealed class Watched
        {
            public readonly TopicPath Topic;
            public readonly Signal Signal;
            public SignalValue Last;
            public bool HasValue;

            public Watched(TopicPath topic, Signal signal)
            {
                Topic = topic;
                Signal = signal;
            }
        }
    }
}
