using System;
using System.Globalization;
using TwoDude.FactoryTwin.Twin;
using Unity.Properties;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Dashboard: the data source the UXML binds to. Listens to the whole bus, keeps display text, sparkline series
    /// and the event log, and tells the binding system only when something changed. Reads the bus, never the scene.
    /// Main thread only, like the bus. Dispose it to stop listening.
    /// </summary>
    public sealed class DashboardViewModel : INotifyBindablePropertyChanged, IDataSourceViewHashProvider, IDisposable
    {
        public const string NoValue = "—";
        public const int SparkSamples = 120;
        public const int LogLines = 50;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        // Topics read once at start, so a dashboard enabled mid-run shows the current state straight away.
        private static readonly TopicPath[] SeedTopics =
        {
            CellTopics.LinkConnected, CellTopics.PressHealth, CellTopics.PressFailureRisk,
            CellTopics.PressAirTemp, CellTopics.PressProcessTemp, CellTopics.PressSpeed, CellTopics.PressTorque,
            CellTopics.PressToolWear, CellTopics.LineState, CellTopics.LinePartsOut, CellTopics.LinePartsPerHour,
            CellTopics.LinePartsLost, CellTopics.LineCycleTime,
        };

        private readonly IDisposable _subscription;
        private long _version;

        private string _linkText = "Broker offline";
        private string _pressHealthText = NoValue;
        private string _failureRiskText = NoValue;
        private float _failureRiskPercent;
        private string _airTempText = NoValue;
        private string _processTempText = NoValue;
        private string _speedText = NoValue;
        private string _torqueText = NoValue;
        private string _toolWearText = NoValue;
        private string _lineStateText = NoValue;
        private string _partsOutText = NoValue;
        private string _partsPerHourText = NoValue;
        private string _partsLostText = NoValue;
        private string _cycleTimeText = NoValue;

        public DashboardViewModel(SignalBus bus)
        {
            if (bus == null)
                throw new ArgumentNullException(nameof(bus));

            foreach (TopicPath topic in SeedTopics)
            {
                if (bus.TryGetLast(topic, out SignalMessage message))
                    Apply(message);
            }
            _subscription = bus.Subscribe(TopicFilter.All, Apply);
        }

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        /// <summary>Raised when press health, line state or the broker link changes, for USS class swaps.</summary>
        public event Action StatusChanged;

        public bool LinkUp { get; private set; }
        public MachineHealth PressHealth { get; private set; } = MachineHealth.Ok;
        public bool HasPressHealth { get; private set; }
        public CellState LineState { get; private set; } = CellState.Feeding;

        public SparkSeries AirTemp { get; } = new SparkSeries(SparkSamples);
        public SparkSeries ProcessTemp { get; } = new SparkSeries(SparkSamples);
        public SparkSeries Speed { get; } = new SparkSeries(SparkSamples);
        public SparkSeries Torque { get; } = new SparkSeries(SparkSamples);
        public SparkSeries ToolWear { get; } = new SparkSeries(SparkSamples);
        public SparkSeries PartsPerHour { get; } = new SparkSeries(SparkSamples);

        public EventLog Log { get; } = new EventLog(LogLines);

        [CreateProperty] public string LinkText => _linkText;
        [CreateProperty] public string PressHealthText => _pressHealthText;
        [CreateProperty] public string FailureRiskText => _failureRiskText;
        [CreateProperty] public float FailureRiskPercent => _failureRiskPercent;
        [CreateProperty] public string AirTempText => _airTempText;
        [CreateProperty] public string ProcessTempText => _processTempText;
        [CreateProperty] public string SpeedText => _speedText;
        [CreateProperty] public string TorqueText => _torqueText;
        [CreateProperty] public string ToolWearText => _toolWearText;
        [CreateProperty] public string LineStateText => _lineStateText;
        [CreateProperty] public string PartsOutText => _partsOutText;
        [CreateProperty] public string PartsPerHourText => _partsPerHourText;
        [CreateProperty] public string PartsLostText => _partsLostText;
        [CreateProperty] public string CycleTimeText => _cycleTimeText;

        public long GetViewHashCode() => _version;

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void Apply(SignalMessage message)
        {
            Log.Observe(message);

            TopicPath topic = message.Topic;
            double v = message.Value.AsNumber;

            if (topic == CellTopics.PressAirTemp)
                Sample(AirTemp, v, ref _airTempText, Celsius(v), nameof(AirTempText));
            else if (topic == CellTopics.PressProcessTemp)
                Sample(ProcessTemp, v, ref _processTempText, Celsius(v), nameof(ProcessTempText));
            else if (topic == CellTopics.PressSpeed)
                Sample(Speed, v, ref _speedText, v.ToString("#,0", Invariant) + " rpm", nameof(SpeedText));
            else if (topic == CellTopics.PressTorque)
                Sample(Torque, v, ref _torqueText, v.ToString("0.0", Invariant) + " N·m", nameof(TorqueText));
            else if (topic == CellTopics.PressToolWear)
                Sample(ToolWear, v, ref _toolWearText, v.ToString("0", Invariant) + " min", nameof(ToolWearText));
            else if (topic == CellTopics.PressFailureRisk)
                ApplyRisk(v);
            else if (topic == CellTopics.PressHealth)
                ApplyHealth((MachineHealth)(int)v);
            else if (topic == CellTopics.LineState)
                ApplyLineState((CellState)(int)v);
            else if (topic == CellTopics.LinePartsOut)
                Set(ref _partsOutText, v.ToString("0", Invariant), nameof(PartsOutText));
            else if (topic == CellTopics.LinePartsPerHour)
                Sample(PartsPerHour, v, ref _partsPerHourText, v.ToString("0", Invariant), nameof(PartsPerHourText));
            else if (topic == CellTopics.LinePartsLost)
                Set(ref _partsLostText, v.ToString("0", Invariant), nameof(PartsLostText));
            else if (topic == CellTopics.LineCycleTime)
                Set(ref _cycleTimeText, v.ToString("0.0", Invariant) + " s", nameof(CycleTimeText));
            else if (topic == CellTopics.LinkConnected)
                ApplyLink(v != 0d);
        }

        private void ApplyRisk(double risk)
        {
            double clamped = Math.Max(0d, Math.Min(1d, risk));
            float percent = (float)(clamped * 100d);
            if (!percent.Equals(_failureRiskPercent))
            {
                _failureRiskPercent = percent;
                Notify(nameof(FailureRiskPercent));
            }
            Set(ref _failureRiskText, percent.ToString("0", Invariant) + " %", nameof(FailureRiskText));
        }

        private void ApplyHealth(MachineHealth health)
        {
            bool changed = !HasPressHealth || health != PressHealth;
            PressHealth = health;
            HasPressHealth = true;
            Set(ref _pressHealthText, HealthText(health), nameof(PressHealthText));
            if (changed)
                StatusChanged?.Invoke();
        }

        private void ApplyLineState(CellState state)
        {
            bool changed = state != LineState || _lineStateText == NoValue;
            LineState = state;
            Set(ref _lineStateText, LineStateWords(state), nameof(LineStateText));
            if (changed)
                StatusChanged?.Invoke();
        }

        private void ApplyLink(bool up)
        {
            bool changed = up != LinkUp;
            LinkUp = up;
            Set(ref _linkText, up ? "Broker connected" : "Broker offline", nameof(LinkText));
            if (changed)
                StatusChanged?.Invoke();
        }

        public static string HealthText(MachineHealth health)
        {
            switch (health)
            {
                case MachineHealth.Ok: return "Healthy";
                case MachineHealth.Warning: return "Warning";
                case MachineHealth.Fault: return "Fault";
                default: return NoValue;
            }
        }

        /// <summary>The line's state in the words the video uses; feeding and transferring are both just running.</summary>
        public static string LineStateWords(CellState state)
        {
            switch (state)
            {
                case CellState.Feeding:
                case CellState.Transferring: return "Running";
                case CellState.Stalled: return "Stalled";
                case CellState.Held: return "Stopped — press fault";
                default: return NoValue;
            }
        }

        private static string Celsius(double kelvin)
        {
            return (kelvin - 273.15).ToString("0.0", Invariant) + " °C";
        }

        private void Sample(SparkSeries series, double value, ref string field, string text, string property)
        {
            series.Add((float)value);
            Set(ref field, text, property);
        }

        private void Set(ref string field, string value, string property)
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
                return;
            field = value;
            Notify(property);
        }

        private void Notify(string property)
        {
            _version++;
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));
        }
    }
}
