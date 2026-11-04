namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Fire safety (M14): the words the dashboard, event log and headset use for a fire's signals.</summary>
    public static class FireWords
    {
        public static string Location(FireLocation location)
        {
            switch (location)
            {
                case FireLocation.DriveCabinet: return "drive cabinet";
                case FireLocation.Cnc: return "CNC";
                case FireLocation.InfeedWaste: return "infeed waste bin";
                case FireLocation.Chargers: return "charging station";
                case FireLocation.StorageRack: return "storage rack";
                case FireLocation.ElectricalPanel: return "electrical panel";
                case FireLocation.SolventDrums: return "solvent drums";
                default: return string.Empty;
            }
        }

        public static string Class(FireClass fireClass)
        {
            switch (fireClass)
            {
                case FireClass.A: return "Class A, ordinary combustibles";
                case FireClass.B: return "Class B, flammable liquids";
                case FireClass.C: return "Class C, live electrical";
                default: return string.Empty;
            }
        }

        public static string Stage(FireStage stage)
        {
            switch (stage)
            {
                case FireStage.None: return "No fire";
                case FireStage.Smoke: return "Smoke";
                case FireStage.Incipient: return "Incipient";
                case FireStage.Growing: return "Growing";
                case FireStage.BeyondIncipient: return "Beyond incipient: evacuate";
                case FireStage.Out: return "Out";
                default: return string.Empty;
            }
        }

        public static string Source(AlarmSource source)
        {
            switch (source)
            {
                case AlarmSource.Manual: return "manual call point";
                case AlarmSource.Detector: return "detector";
                default: return string.Empty;
            }
        }
    }
}
