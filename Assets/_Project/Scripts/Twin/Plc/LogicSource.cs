namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Production cell: which controller runs the line. Published as a number on cell/plc/source.</summary>
    public enum LogicSource
    {
        /// <summary>The in-Unity virtual PLC, CellSequencer.</summary>
        Unity = 0,

        /// <summary>The soft PLC outside Unity, through PlcLink.</summary>
        Plc = 1,
    }

    /// <summary>Production cell: reads the logic source from the bus. No message yet means Unity.</summary>
    public static class LogicSourceRule
    {
        public static LogicSource Read(SignalBus bus)
        {
            return bus.TryGetLast(CellTopics.PlcSource, out SignalMessage message) && message.Value.AsNumber >= 0.5d
                ? LogicSource.Plc
                : LogicSource.Unity;
        }
    }
}
