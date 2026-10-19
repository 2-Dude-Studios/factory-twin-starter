namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Press station: the health the dashboard and the 3D press show. Published as a number on cell/press/health.</summary>
    public enum MachineHealth
    {
        Ok = 0,
        Warning = 1,
        Fault = 2,
    }

    /// <summary>Press station: reads the press's fault signals and turns them into one health value.</summary>
    public static class PressHealthRule
    {
        /// <summary>True while the press reports its overall failure flag or any single failure mode.</summary>
        public static bool IsFaulted(SignalBus bus)
        {
            if (ReadBool(bus, CellTopics.PressMachineFailure))
                return true;
            for (int i = 0; i < CellTopics.PressFaults.Count; i++)
            {
                if (ReadBool(bus, CellTopics.PressFaults[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Warning with hysteresis: it comes on when risk reaches <paramref name="onAt"/> and goes off only when
        /// risk falls below <paramref name="offBelow"/>, so noise around the warning level cannot flicker it.
        /// </summary>
        public static bool UpdateWarning(bool warning, double risk, double onAt, double offBelow)
        {
            return warning ? risk >= offBelow : risk >= onAt;
        }

        /// <summary>A fault outranks a warning.</summary>
        public static MachineHealth Evaluate(bool faulted, bool warning)
        {
            if (faulted)
                return MachineHealth.Fault;
            return warning ? MachineHealth.Warning : MachineHealth.Ok;
        }

        private static bool ReadBool(SignalBus bus, TopicPath topic)
        {
            return bus.TryGetLast(topic, out SignalMessage message) && message.Value.AsNumber != 0d;
        }
    }
}
