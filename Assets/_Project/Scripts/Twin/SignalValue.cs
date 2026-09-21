using System;

namespace TwoDude.FactoryTwin.Twin
{
    public enum SignalKind
    {
        Number,
        Bool,
    }

    /// <summary>Data spine: the "v" of a signal payload, either a number or a boolean.</summary>
    public readonly struct SignalValue : IEquatable<SignalValue>
    {
        public SignalKind Kind { get; }
        public double Number { get; }
        public bool Bool { get; }

        private SignalValue(SignalKind kind, double number, bool flag)
        {
            Kind = kind;
            Number = number;
            Bool = flag;
        }

        public static SignalValue FromNumber(double value) => new SignalValue(SignalKind.Number, value, false);
        public static SignalValue FromBool(bool value) => new SignalValue(SignalKind.Bool, 0d, value);

        /// <summary>Numeric view for gauges and deadband checks; booleans read as 0 or 1.</summary>
        public double AsNumber => Kind == SignalKind.Bool ? (Bool ? 1d : 0d) : Number;

        public bool Equals(SignalValue other) => Kind == other.Kind && Number.Equals(other.Number) && Bool == other.Bool;
        public override bool Equals(object obj) => obj is SignalValue other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Number.GetHashCode() ^ (Bool ? 1 : 0);
        public override string ToString() => Kind == SignalKind.Bool ? (Bool ? "true" : "false") : Number.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Data spine: one signal sample as it travels over the bus and MQTT.</summary>
    public readonly struct SignalMessage
    {
        public TopicPath Topic { get; }
        public SignalValue Value { get; }
        public long UnixMs { get; }

        public SignalMessage(TopicPath topic, SignalValue value, long unixMs)
        {
            Topic = topic;
            Value = value;
            UnixMs = unixMs;
        }
    }
}
