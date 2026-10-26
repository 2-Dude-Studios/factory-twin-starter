using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the bus commands behind the headset's buttons (the training board and the left-controller
    /// panel), the same ones the dashboard and keys send.
    /// </summary>
    public static class TrainingCommands
    {
        public static void StartTraining(SignalBus bus) => Send(bus, CellTopics.TrainingRun, 1d);

        // The scenario the lockout clears: a tool-wear failure on the CNC, as the dashboard's Tool wear button injects.
        public static void FailCnc(SignalBus bus) => Send(bus, CellTopics.PressInjectFault, (int)FaultMode.ToolWear);

        public static void SafetyReset(SignalBus bus) => Send(bus, CellTopics.SafetyReset, 1d);

        private static void Send(SignalBus bus, TopicPath topic, double value) =>
            bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);
    }
}
