using System;
using System.Globalization;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Data spine: reads and writes the MQTT payload { "v": &lt;value&gt;, "t": &lt;unix_ms&gt; }.</summary>
    public static class SignalPayload
    {
        public static string Serialize(SignalValue value, long unixMs)
        {
            if (value.Kind == SignalKind.Number && (double.IsNaN(value.Number) || double.IsInfinity(value.Number)))
                throw new ArgumentException("NaN and Infinity cannot be written as JSON.", nameof(value));

            return "{\"v\":" + value + ",\"t\":" + unixMs.ToString(CultureInfo.InvariantCulture) + "}";
        }

        /// <summary>Strict parser for the flat two-key payload. Accepts either key order and any JSON whitespace.</summary>
        public static bool TryParse(string json, out SignalValue value, out long unixMs)
        {
            value = default;
            unixMs = 0;
            if (string.IsNullOrEmpty(json))
                return false;

            ReadOnlySpan<char> s = json.AsSpan();
            int i = 0;
            SkipWhitespace(s, ref i);
            if (i >= s.Length || s[i] != '{')
                return false;
            i++;

            bool hasValue = false;
            bool hasTime = false;
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i + 2 >= s.Length || s[i] != '"' || s[i + 2] != '"')
                    return false;
                char key = s[i + 1];
                i += 3;

                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':')
                    return false;
                i++;
                SkipWhitespace(s, ref i);

                int start = i;
                while (i < s.Length && s[i] != ',' && s[i] != '}' && !IsWhitespace(s[i]))
                    i++;
                ReadOnlySpan<char> token = s.Slice(start, i - start);

                if (key == 'v' && !hasValue)
                {
                    if (!TryParseValue(token, out value))
                        return false;
                    hasValue = true;
                }
                else if (key == 't' && !hasTime)
                {
                    if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out unixMs))
                        return false;
                    hasTime = true;
                }
                else
                {
                    return false;
                }

                SkipWhitespace(s, ref i);
                if (i >= s.Length)
                    return false;
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (s[i] != '}')
                    return false;
                i++;
                break;
            }

            SkipWhitespace(s, ref i);
            return i == s.Length && hasValue && hasTime;
        }

        private static bool TryParseValue(ReadOnlySpan<char> token, out SignalValue value)
        {
            if (token.SequenceEqual("true".AsSpan()))
            {
                value = SignalValue.FromBool(true);
                return true;
            }
            if (token.SequenceEqual("false".AsSpan()))
            {
                value = SignalValue.FromBool(false);
                return true;
            }

            // JSON has no NaN/Infinity literals; double.TryParse would accept them, so reject explicitly.
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                && !double.IsNaN(number) && !double.IsInfinity(number))
            {
                value = SignalValue.FromNumber(number);
                return true;
            }

            value = default;
            return false;
        }

        private static void SkipWhitespace(ReadOnlySpan<char> s, ref int i)
        {
            while (i < s.Length && IsWhitespace(s[i]))
                i++;
        }

        private static bool IsWhitespace(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r';
    }
}
