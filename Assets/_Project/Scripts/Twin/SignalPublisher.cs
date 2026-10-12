using System;
using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Data spine: decides what leaves the cell over MQTT. Watches the bus and forwards a topic only when its
    /// value moved beyond the deadband, and never faster than the configured rate; changes arriving inside that
    /// window are coalesced and sent when it expires, so a fast signal costs bandwidth but never floods.
    /// Pure logic with no MQTT dependency, so the policy is testable on its own.
    /// </summary>
    public sealed class SignalPublisher : IDisposable
    {
        private sealed class Entry
        {
            public SignalMessage Latest;
            public SignalValue LastSent;
            public bool HasSent;
            public bool Dirty;
            public bool Remote;
            public double LastSentAt;
        }

        private readonly Dictionary<TopicPath, Entry> _entries = new Dictionary<TopicPath, Entry>();
        private readonly List<TopicPath> _due = new List<TopicPath>();
        private readonly Action<SignalMessage> _send;
        private readonly double _period;
        private readonly double _deadband;
        private IDisposable _subscription;
        private SignalMessage _inbound;
        private bool _expectInbound;

        public SignalPublisher(SignalBus bus, Action<SignalMessage> send, double maxRateHz, double deadband,
            string filter = TopicFilter.All)
        {
            if (bus == null)
                throw new ArgumentNullException(nameof(bus));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            if (!(maxRateHz > 0d))
                throw new ArgumentOutOfRangeException(nameof(maxRateHz));
            if (deadband < 0d)
                throw new ArgumentOutOfRangeException(nameof(deadband));

            _period = 1d / maxRateHz;
            _deadband = deadband;
            _subscription = bus.Subscribe(filter, OnMessage);
        }

        public int Sent { get; private set; }
        public int Suppressed { get; private set; }

        private void OnMessage(SignalMessage message)
        {
            if (!_entries.TryGetValue(message.Topic, out Entry entry))
            {
                entry = new Entry { LastSentAt = double.NegativeInfinity };
                _entries.Add(message.Topic, entry);
            }

            entry.Latest = message;
            if (_expectInbound && message.Topic.Equals(_inbound.Topic) && message.UnixMs == _inbound.UnixMs
                && message.Value.Equals(_inbound.Value))
            {
                // The far side of the link already has this value; sending it back would only duplicate it there.
                _expectInbound = false;
                entry.Remote = true;
                entry.Dirty = false;
                entry.LastSent = message.Value;
                entry.HasSent = true;
                return;
            }

            entry.Remote = false;
            if (entry.HasSent && !Moved(entry.LastSent, message.Value))
            {
                Suppressed++;
                return;
            }
            entry.Dirty = true;
        }

        /// <summary>Send everything that is due. <paramref name="nowSeconds"/> is a monotonic clock.</summary>
        public void Flush(double nowSeconds)
        {
            _due.Clear();
            foreach (KeyValuePair<TopicPath, Entry> pair in _entries)
            {
                Entry entry = pair.Value;
                if (entry.Dirty && nowSeconds - entry.LastSentAt >= _period)
                    _due.Add(pair.Key);
            }

            for (int i = 0; i < _due.Count; i++)
            {
                Entry entry = _entries[_due[i]];
                entry.Dirty = false;
                entry.LastSent = entry.Latest.Value;
                entry.HasSent = true;
                entry.LastSentAt = nowSeconds;
                Sent++;
                _send(entry.Latest);
            }
        }

        /// <summary>
        /// Queue the latest value of every known topic again, still rate-limited. For a link that has just come
        /// up: the broker missed whatever was sent into the outage, and steady signals would otherwise never repeat.
        /// </summary>
        public void ResendAll()
        {
            foreach (KeyValuePair<TopicPath, Entry> pair in _entries)
            {
                // Topics last written from the far side are theirs to repeat, not ours.
                if (!pair.Value.Remote)
                    pair.Value.Dirty = true;
            }
        }

        /// <summary>
        /// Call just before putting a message that arrived over the link onto the bus. That one message is
        /// recorded as already known on the far side instead of being sent back to where it came from.
        /// </summary>
        public void ExpectInbound(SignalMessage message)
        {
            _inbound = message;
            _expectInbound = true;
        }

        private bool Moved(SignalValue last, SignalValue value)
        {
            if (last.Kind != value.Kind)
                return true;
            if (value.Kind == SignalKind.Bool)
                return last.Bool != value.Bool;            // a boolean has no meaningful deadband
            return Math.Abs(last.Number - value.Number) > _deadband;
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
