using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Production cell: the rule both logic sources use to read a press from a command count (M11). A press is the
    /// count going up by exactly one (16-bit, as a Modbus register wraps). Any other change, such as the count
    /// starting again from 0 when the twin restarts, only moves the baseline.
    /// </summary>
    public static class CommandCount
    {
        public static bool IsPress(int last, int current) => current == ((last + 1) & 0xFFFF);
    }

    /// <summary>
    /// Production cell: counts the operator's Start and Stop presses (M11). Each true on cell/line/start or
    /// cell/line/stop adds one to cell/line/start_count or cell/line/stop_count. The counts, not the presses, go to
    /// the logic source, so a press survives the gap between two Modbus polls.
    /// </summary>
    public sealed class LineCommands : MonoBehaviour
    {
        [SerializeField] private CellContext context;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private int starts;
        [SerializeField] private int stops;

        private IDisposable _startSubscription;
        private IDisposable _stopSubscription;

        private void OnEnable()
        {
            SignalBus bus = context.Bus;
            _startSubscription = bus.Subscribe(CellTopics.LineStart.ToString(), OnStart);
            _stopSubscription = bus.Subscribe(CellTopics.LineStop.ToString(), OnStop);
        }

        private void OnDisable()
        {
            _startSubscription?.Dispose();
            _startSubscription = null;
            _stopSubscription?.Dispose();
            _stopSubscription = null;
        }

        // Baselines, so the logic sources have a count to compare the first press against.
        private void Start()
        {
            Publish(CellTopics.LineStartCount, starts);
            Publish(CellTopics.LineStopCount, stops);
        }

        private void OnStart(SignalMessage message)
        {
            if (message.Value.AsNumber == 0d)
                return;
            starts = (starts + 1) & 0xFFFF;
            Publish(CellTopics.LineStartCount, starts);
        }

        private void OnStop(SignalMessage message)
        {
            if (message.Value.AsNumber == 0d)
                return;
            stops = (stops + 1) & 0xFFFF;
            Publish(CellTopics.LineStopCount, stops);
        }

        private void Publish(TopicPath topic, int count)
        {
            context.Bus.Publish(topic, SignalValue.FromNumber(count), CellContext.NowUnixMs);
        }
    }
}
