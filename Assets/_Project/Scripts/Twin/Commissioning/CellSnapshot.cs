namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Commissioning: the cell's signals at one instant, as the suite's checks see them.</summary>
    public readonly struct CellSnapshot
    {
        public bool InfeedRun { get; }
        public bool OutfeedRun { get; }
        public bool ArmBusy { get; }
        public bool ArmHolding { get; }
        public bool PartAtGate { get; }
        public CellState State { get; }
        public int PartsOut { get; }
        public bool PlcLink { get; }
        public double RealtimeRatio { get; }

        public CellSnapshot(bool infeedRun, bool outfeedRun, bool armBusy, bool armHolding, bool partAtGate,
            CellState state, int partsOut, bool plcLink = true, double realtimeRatio = 1d)
        {
            InfeedRun = infeedRun;
            OutfeedRun = outfeedRun;
            ArmBusy = armBusy;
            ArmHolding = armHolding;
            PartAtGate = partAtGate;
            State = state;
            PartsOut = partsOut;
            PlcLink = plcLink;
            RealtimeRatio = realtimeRatio;
        }

        /// <summary>Feeding or transferring: the line is producing.</summary>
        public bool Running => State == CellState.Feeding || State == CellState.Transferring;

        /// <summary>Reads the latest value of every signal the checks use from the bus.</summary>
        public static CellSnapshot Read(SignalBus bus)
        {
            return new CellSnapshot(
                Bool(bus, CellTopics.InfeedDriveForward),
                Bool(bus, CellTopics.OutfeedDriveForward),
                Bool(bus, CellTopics.ArmBusy),
                Bool(bus, CellTopics.ArmHolding),
                Bool(bus, CellTopics.GatePartPresent),
                (CellState)(int)Number(bus, CellTopics.LineState, 0d),
                (int)Number(bus, CellTopics.LinePartsOut, 0d),
                Bool(bus, CellTopics.PlcLink),
                Number(bus, CellTopics.SimRealtimeRatio, 1d));
        }

        private static bool Bool(SignalBus bus, TopicPath topic) =>
            bus.TryGetLast(topic, out SignalMessage message) && message.Value.AsNumber != 0d;

        private static double Number(SignalBus bus, TopicPath topic, double fallback) =>
            bus.TryGetLast(topic, out SignalMessage message) ? message.Value.AsNumber : fallback;
    }
}
