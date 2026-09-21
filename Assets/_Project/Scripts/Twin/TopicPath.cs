using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Data spine: a concrete MQTT topic of the form cell/&lt;station&gt;/&lt;signal&gt;.</summary>
    public readonly struct TopicPath : IEquatable<TopicPath>
    {
        public const string Root = "cell";

        private readonly string _topic;

        public string Station { get; }
        public string Signal { get; }

        public TopicPath(string station, string signal)
        {
            if (!IsValidSegment(station))
                throw new ArgumentException($"Invalid station '{station}'. Use lower-case letters, digits and '_'.", nameof(station));
            if (!IsValidSegment(signal))
                throw new ArgumentException($"Invalid signal '{signal}'. Use lower-case letters, digits and '_'.", nameof(signal));

            Station = station;
            Signal = signal;
            _topic = Root + "/" + station + "/" + signal;
        }

        public static TopicPath Parse(string topic)
        {
            if (!TryParse(topic, out TopicPath path))
                throw new FormatException($"'{topic}' is not a valid topic. Expected {Root}/<station>/<signal>.");
            return path;
        }

        public static bool TryParse(string topic, out TopicPath path)
        {
            path = default;
            if (string.IsNullOrEmpty(topic))
                return false;

            int first = topic.IndexOf('/');
            if (first < 0)
                return false;
            int second = topic.IndexOf('/', first + 1);
            if (second < 0 || topic.IndexOf('/', second + 1) >= 0)
                return false;
            if (first != Root.Length || string.CompareOrdinal(topic, 0, Root, 0, Root.Length) != 0)
                return false;

            string station = topic.Substring(first + 1, second - first - 1);
            string signal = topic.Substring(second + 1);
            if (!IsValidSegment(station) || !IsValidSegment(signal))
                return false;

            path = new TopicPath(station, signal);
            return true;
        }

        /// <summary>Segments are lower-case letters, digits and '_' only, so wildcards and separators can never leak in.</summary>
        public static bool IsValidSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment))
                return false;

            for (int i = 0; i < segment.Length; i++)
            {
                char c = segment[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                    return false;
            }
            return true;
        }

        public bool Equals(TopicPath other) => string.Equals(_topic, other._topic, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TopicPath other && Equals(other);
        public override int GetHashCode() => _topic == null ? 0 : _topic.GetHashCode();
        public override string ToString() => _topic ?? string.Empty;

        public static bool operator ==(TopicPath a, TopicPath b) => a.Equals(b);
        public static bool operator !=(TopicPath a, TopicPath b) => !a.Equals(b);
    }
}
