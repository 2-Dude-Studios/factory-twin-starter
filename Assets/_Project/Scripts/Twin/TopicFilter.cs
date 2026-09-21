namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Data spine: MQTT-style subscription filters ('+' one level, '#' everything below) matched against topics.</summary>
    public static class TopicFilter
    {
        public const string All = TopicPath.Root + "/#";

        public static bool IsValid(string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return false;

            int start = 0;
            while (start <= filter.Length)
            {
                int end = filter.IndexOf('/', start);
                if (end < 0)
                    end = filter.Length;
                int length = end - start;
                if (length == 0)
                    return false;

                bool isLast = end == filter.Length;
                bool hasHash = filter.IndexOf('#', start, length) >= 0;
                bool hasPlus = filter.IndexOf('+', start, length) >= 0;
                if (hasHash && (length != 1 || !isLast))
                    return false;
                if (hasPlus && length != 1)
                    return false;

                start = end + 1;
            }
            return true;
        }

        public static bool Matches(string filter, string topic)
        {
            if (string.IsNullOrEmpty(filter) || string.IsNullOrEmpty(topic))
                return false;

            int f = 0;
            int t = 0;
            while (true)
            {
                int fEnd = filter.IndexOf('/', f);
                if (fEnd < 0)
                    fEnd = filter.Length;
                int fLength = fEnd - f;

                if (fLength == 1 && filter[f] == '#')
                    return true;

                int tEnd = topic.IndexOf('/', t);
                if (tEnd < 0)
                    tEnd = topic.Length;
                int tLength = tEnd - t;

                bool wildcard = fLength == 1 && filter[f] == '+';
                if (!wildcard && (fLength != tLength || string.CompareOrdinal(filter, f, topic, t, fLength) != 0))
                    return false;

                bool filterDone = fEnd == filter.Length;
                bool topicDone = tEnd == topic.Length;
                if (filterDone)
                    return topicDone;
                if (topicDone)
                {
                    // Per the MQTT spec "a/#" also matches its parent level "a".
                    return fEnd + 2 == filter.Length && filter[fEnd + 1] == '#';
                }

                f = fEnd + 1;
                t = tEnd + 1;
            }
        }
    }
}
