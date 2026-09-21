using System;
using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Data spine: in-process publish/subscribe hub that every station, the MQTT bridge and the dashboard share.
    /// Main-thread only; the MQTT bridge must marshal broker callbacks before publishing here.
    /// </summary>
    public sealed class SignalBus
    {
        private readonly Dictionary<TopicPath, SignalMessage> _last = new Dictionary<TopicPath, SignalMessage>();
        private readonly List<Subscription> _subscriptions = new List<Subscription>();
        private int _dispatchDepth;
        private bool _needsCompaction;

        /// <summary>Raised for every message, after filtered subscribers. Used by the event log and the MQTT publisher.</summary>
        public event Action<SignalMessage> MessagePublished;

        public int TopicCount => _last.Count;

        /// <summary>Dispose the returned handle to unsubscribe. Filters use MQTT wildcards, e.g. cell/press/# or cell/+/state.</summary>
        public IDisposable Subscribe(string filter, Action<SignalMessage> handler)
        {
            if (!TopicFilter.IsValid(filter))
                throw new ArgumentException($"'{filter}' is not a valid topic filter.", nameof(filter));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = new Subscription(this, filter, handler);
            _subscriptions.Add(subscription);
            return subscription;
        }

        public void Publish(TopicPath topic, SignalValue value, long unixMs)
        {
            Publish(new SignalMessage(topic, value, unixMs));
        }

        public void Publish(SignalMessage message)
        {
            _last[message.Topic] = message;
            string topic = message.Topic.ToString();

            _dispatchDepth++;
            try
            {
                // Snapshot the count so handlers that subscribe during dispatch start with the next message.
                int count = _subscriptions.Count;
                for (int i = 0; i < count; i++)
                {
                    Subscription subscription = _subscriptions[i];
                    if (subscription.Active && TopicFilter.Matches(subscription.Filter, topic))
                        subscription.Handler(message);
                }
                MessagePublished?.Invoke(message);
            }
            finally
            {
                _dispatchDepth--;
                if (_dispatchDepth == 0 && _needsCompaction)
                {
                    _subscriptions.RemoveAll(s => !s.Active);
                    _needsCompaction = false;
                }
            }
        }

        public bool TryGetLast(TopicPath topic, out SignalMessage message) => _last.TryGetValue(topic, out message);

        private void Remove(Subscription subscription)
        {
            if (_dispatchDepth > 0)
                _needsCompaction = true;
            else
                _subscriptions.Remove(subscription);
        }

        private sealed class Subscription : IDisposable
        {
            private readonly SignalBus _bus;

            public string Filter { get; }
            public Action<SignalMessage> Handler { get; }
            public bool Active { get; private set; } = true;

            public Subscription(SignalBus bus, string filter, Action<SignalMessage> handler)
            {
                _bus = bus;
                Filter = filter;
                Handler = handler;
            }

            public void Dispose()
            {
                if (!Active)
                    return;
                Active = false;
                _bus.Remove(this);
            }
        }
    }
}
