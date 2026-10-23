using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Commissioning: a force command, published as a number on a force topic such as cell/gate1/force.</summary>
    public enum ForceMode
    {
        /// <summary>No force: the input follows the plant.</summary>
        None = 0,

        /// <summary>The input reads false whatever the plant does.</summary>
        Off = 1,

        /// <summary>The input reads true whatever the plant does.</summary>
        On = 2,
    }

    /// <summary>
    /// Commissioning: the cell's force table, like a PLC's. A forced input is published with its forced value in
    /// place of the plant's, so every logic source sees the same thing. Only TwinSignals applies it.
    /// </summary>
    public sealed class SignalForces
    {
        private readonly Dictionary<TopicPath, SignalValue> _forced = new Dictionary<TopicPath, SignalValue>();

        public int Count => _forced.Count;

        public void Apply(TopicPath input, ForceMode mode)
        {
            if (mode == ForceMode.None)
                _forced.Remove(input);
            else
                _forced[input] = SignalValue.FromBool(mode == ForceMode.On);
        }

        public void ClearAll() => _forced.Clear();

        /// <summary>The value to publish for <paramref name="input"/>: the forced one if set, else the plant's.</summary>
        public SignalValue Resolve(TopicPath input, SignalValue plant) =>
            _forced.TryGetValue(input, out SignalValue forced) ? forced : plant;

        public bool IsForced(TopicPath input) => _forced.ContainsKey(input);

        /// <summary>Reads a force command value: 0 none, 1 off, 2 on; anything else counts as none.</summary>
        public static ForceMode ParseMode(double value)
        {
            int mode = (int)System.Math.Round(value);
            return mode == (int)ForceMode.Off ? ForceMode.Off : mode == (int)ForceMode.On ? ForceMode.On : ForceMode.None;
        }
    }
}
