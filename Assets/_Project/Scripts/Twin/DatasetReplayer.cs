using System;
using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: replays AI4I rows onto the signal bus as machine telemetry, one row per tick of the press.
    /// In-process twin of tools/replay/replay.py (same topics and payloads) for running the demo without a broker.
    /// </summary>
    public sealed class DatasetReplayer
    {
        // Never publish more than this much backlog in one Tick, so an editor hitch cannot flood the bus.
        private const double MaxBacklogSeconds = 1d;

        private readonly IReadOnlyList<Ai4iRow> _rows;
        private readonly SignalBus _bus;

        private readonly TopicPath _productQuality;
        private readonly TopicPath _airTemp;
        private readonly TopicPath _processTemp;
        private readonly TopicPath _speed;
        private readonly TopicPath _torque;
        private readonly TopicPath _toolWear;
        private readonly TopicPath _machineFailure;
        private readonly TopicPath[] _faultTopics;

        private double _rowsPerSecond = 2d;
        private double _backlogSeconds;
        private int _cursor;

        public DatasetReplayer(IReadOnlyList<Ai4iRow> rows, SignalBus bus, string station = "press")
        {
            _rows = rows ?? throw new ArgumentNullException(nameof(rows));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));

            _productQuality = new TopicPath(station, "product_quality");
            _airTemp = new TopicPath(station, "air_temp_k");
            _processTemp = new TopicPath(station, "process_temp_k");
            _speed = new TopicPath(station, "speed_rpm");
            _torque = new TopicPath(station, "torque_nm");
            _toolWear = new TopicPath(station, "tool_wear_min");
            _machineFailure = new TopicPath(station, "machine_failure");
            _faultTopics = new[]
            {
                new TopicPath(station, "fault_twf"),
                new TopicPath(station, "fault_hdf"),
                new TopicPath(station, "fault_pwf"),
                new TopicPath(station, "fault_osf"),
                new TopicPath(station, "fault_rnf"),
            };
        }

        public double RowsPerSecond
        {
            get => _rowsPerSecond;
            set
            {
                if (!(value > 0d))
                    throw new ArgumentOutOfRangeException(nameof(value), "Rate must be positive.");
                _rowsPerSecond = value;
            }
        }

        /// <summary>Restart from the first row at end of file. Off means <see cref="Finished"/> becomes true instead.</summary>
        public bool Loop { get; set; } = true;

        /// <summary>When not None, only rows with at least one of these faults are replayed (the fault injector's lever).</summary>
        public FaultMode FaultFilter { get; set; } = FaultMode.None;

        /// <summary>
        /// With no <see cref="FaultFilter"/> set, skip rows that report a failure, so the baseline is a healthy
        /// machine and a fault only appears when one is injected. Off replays every row, as replay.py does.
        /// </summary>
        public bool SkipFailures { get; set; }

        /// <summary>
        /// Extra test for baseline rows (with <see cref="SkipFailures"/> on and no fault filter): a row is replayed
        /// only if this returns true. Null accepts every healthy row.
        /// </summary>
        public Predicate<Ai4iRow> BaselineFilter { get; set; }

        /// <summary>
        /// When not NaN, published instead of each row's tool wear. The degradation scenario uses it to make the tool
        /// wear out on a schedule while the other channels keep the dataset's real variation.
        /// </summary>
        public double ToolWearOverride { get; set; } = double.NaN;

        public bool Finished { get; private set; }
        public int RowsPublished { get; private set; }

        /// <summary>
        /// Moves the replay to the next row (wrapping) whose tool wear is at most <paramref name="maxWearMin"/>, as if a
        /// fresh tool had just gone in; wear then grows again row by row from there. Returns false if no row qualifies.
        /// </summary>
        public bool SeekFreshTool(double maxWearMin)
        {
            for (int step = 0; step < _rows.Count; step++)
            {
                int index = (_cursor + step) % _rows.Count;
                if (_rows[index].ToolWearMin <= maxWearMin)
                {
                    _cursor = index;
                    Finished = false;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Advance the replay clock and publish every row that has come due. Returns the rows published this tick.</summary>
        public int Tick(double deltaSeconds, long nowUnixMs)
        {
            if (Finished || deltaSeconds <= 0d)
                return 0;

            _backlogSeconds = Math.Min(_backlogSeconds + deltaSeconds, MaxBacklogSeconds);
            double period = 1d / _rowsPerSecond;

            int published = 0;
            while (_backlogSeconds >= period && !Finished)
            {
                if (!TryNextRow(out Ai4iRow row))
                {
                    Finished = true;
                    break;
                }
                Publish(row, nowUnixMs);
                _backlogSeconds -= period;
                published++;
            }

            RowsPublished += published;
            return published;
        }

        private bool TryNextRow(out Ai4iRow row)
        {
            // At most one full pass, so a filter that matches nothing ends the replay instead of spinning.
            for (int scanned = 0; scanned < _rows.Count; scanned++)
            {
                if (_cursor >= _rows.Count)
                {
                    if (!Loop)
                        break;
                    _cursor = 0;
                }

                Ai4iRow candidate = _rows[_cursor++];
                if (Wanted(candidate))
                {
                    row = candidate;
                    return true;
                }
            }

            row = default;
            return false;
        }

        private bool Wanted(Ai4iRow row)
        {
            if (FaultFilter != FaultMode.None)
                return (row.Faults & FaultFilter) != 0;
            if (!SkipFailures)
                return true;
            if (row.Faults != FaultMode.None || row.MachineFailure)
                return false;
            return BaselineFilter == null || BaselineFilter(row);
        }

        private void Publish(Ai4iRow row, long nowUnixMs)
        {
            // First, so anything reacting to this row's telemetry already knows the product it belongs to.
            _bus.Publish(_productQuality, SignalValue.FromNumber((int)row.Quality), nowUnixMs);
            _bus.Publish(_airTemp, SignalValue.FromNumber(row.AirTempK), nowUnixMs);
            _bus.Publish(_processTemp, SignalValue.FromNumber(row.ProcessTempK), nowUnixMs);
            _bus.Publish(_speed, SignalValue.FromNumber(row.SpeedRpm), nowUnixMs);
            _bus.Publish(_torque, SignalValue.FromNumber(row.TorqueNm), nowUnixMs);
            double wear = double.IsNaN(ToolWearOverride) ? row.ToolWearMin : ToolWearOverride;
            _bus.Publish(_toolWear, SignalValue.FromNumber(wear), nowUnixMs);
            for (int f = 0; f < _faultTopics.Length; f++)
                _bus.Publish(_faultTopics[f], SignalValue.FromBool((row.Faults & (FaultMode)(1 << f)) != 0), nowUnixMs);
            _bus.Publish(_machineFailure, SignalValue.FromBool(row.MachineFailure), nowUnixMs);
        }
    }
}
