using System;
using System.Collections.Generic;
using realvirtual;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Data spine: the only bridge between realvirtual signals and the signal bus. PLC inputs (sensor and drive
    /// state coming out of the simulation) are published to cell/&lt;station&gt;/&lt;signal&gt; when they change;
    /// PLC outputs (commands into the simulation) are written whenever their topic is published on the bus.
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

        private readonly List<WatchedInput> _inputs = new List<WatchedInput>();
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

                var topic = new TopicPath(binding.Station, binding.SignalName);
                if (binding.Target.IsInput())
                {
                    _inputs.Add(new WatchedInput(topic, binding.Target));
                }
                else
                {
                    Signal target = binding.Target;
                    _subscriptions.Add(_bus.Subscribe(topic.ToString(), message => Write(target, message.Value)));
                }
            }
        }

        private void OnDisable()
        {
            foreach (IDisposable subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
            _inputs.Clear();
        }

        private void FixedUpdate()
        {
            long now = CellContext.NowUnixMs;
            for (int i = 0; i < _inputs.Count; i++)
            {
                WatchedInput input = _inputs[i];
                if (!TryRead(input.Signal, out SignalValue value))
                    continue;
                if (input.HasValue && !Changed(input.Last, value))
                    continue;

                input.Last = value;
                input.HasValue = true;
                _bus.Publish(input.Topic, value, now);
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
                default:
                    value = default;
                    return false;
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

        private sealed class WatchedInput
        {
            public readonly TopicPath Topic;
            public readonly Signal Signal;
            public SignalValue Last;
            public bool HasValue;

            public WatchedInput(TopicPath topic, Signal signal)
            {
                Topic = topic;
                Signal = signal;
            }
        }
    }
}
