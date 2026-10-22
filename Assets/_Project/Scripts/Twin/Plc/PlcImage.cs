using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Soft PLC link: where a point lives in the PLC's Modbus address space.</summary>
    public enum PlcArea
    {
        /// <summary>A coil, %QX in OpenPLC (address = byte × 8 + bit).</summary>
        Coil = 0,

        /// <summary>A holding register: %QW at 0–1023, %MW from 1024 in OpenPLC.</summary>
        HoldingRegister = 1,
    }

    /// <summary>Soft PLC link: which way a point's value travels.</summary>
    public enum PlcDirection
    {
        /// <summary>The twin writes it: a sensor or feedback the PLC program reads.</summary>
        ToPlc = 0,

        /// <summary>The PLC program writes it: a command or status the twin publishes on the bus.</summary>
        FromPlc = 1,
    }

    /// <summary>Soft PLC link: one row of the I/O list, binding a PLC address to a bus topic.</summary>
    [Serializable]
    public struct PlcPoint
    {
        [SerializeField] private string topic;
        [SerializeField] private PlcArea area;
        [SerializeField, Range(0, 65535)] private int address;
        [SerializeField] private PlcDirection direction;
        [SerializeField] private bool isBool;
        [SerializeField] private double scale;
        [SerializeField] private string plcName;
        [SerializeField] private string description;

        public PlcPoint(string topic, PlcArea area, ushort address, PlcDirection direction, bool isBool,
            string plcName, string description = "", double scale = 1d)
        {
            this.topic = topic;
            this.area = area;
            this.address = address;
            this.direction = direction;
            this.isBool = isBool;
            this.scale = scale;
            this.plcName = plcName;
            this.description = description;
        }

        public string Topic => topic;
        public PlcArea Area => area;
        public ushort Address => (ushort)address;
        public PlcDirection Direction => direction;

        /// <summary>True: the value is a bool (0 or 1). False: a number, raw register × <see cref="Scale"/>.</summary>
        public bool IsBool => isBool;

        public double Scale => scale;

        /// <summary>The PLC-side name (the located variable in the Structured Text), for the I/O list.</summary>
        public string PlcName => plcName;

        public string Description => description;

        /// <summary>The IEC 61131-3 address as OpenPLC writes it, e.g. %QX0.2, %QW3, %MW1.</summary>
        public string IecAddress => area == PlcArea.Coil
            ? $"%QX{address / 8}.{address % 8}"
            : address < 1024 ? $"%QW{address}" : $"%MW{address - 1024}";
    }

    /// <summary>
    /// Soft PLC link: turns the I/O list into three contiguous Modbus blocks (registers the twin writes, coils and
    /// registers it reads) and converts between those blocks and bus values. Pure; the socket lives elsewhere.
    /// </summary>
    public sealed class PlcImage
    {
        private readonly List<(TopicPath Topic, PlcPoint Point)> _toPlc = new List<(TopicPath, PlcPoint)>();
        private readonly List<(TopicPath Topic, PlcPoint Point)> _fromPlc = new List<(TopicPath, PlcPoint)>();

        public PlcImage(IReadOnlyList<PlcPoint> points, ushort twinHeartbeatAddress)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));

            TwinHeartbeatAddress = twinHeartbeatAddress;
            var used = new HashSet<(PlcArea, ushort)> { (PlcArea.HoldingRegister, twinHeartbeatAddress) };

            foreach (PlcPoint point in points)
            {
                TopicPath topic = TopicPath.Parse(point.Topic);
                if (!used.Add((point.Area, point.Address)))
                    throw new ArgumentException($"{point.Area} {point.Address} is used twice ({point.Topic}).");
                if (point.Direction == PlcDirection.ToPlc && point.Area != PlcArea.HoldingRegister)
                    throw new ArgumentException($"{point.Topic}: the twin writes only holding registers (%MW).");
                if (!point.IsBool && !(point.Scale > 0d))
                    throw new ArgumentException($"{point.Topic}: a number needs a scale above zero.");

                (point.Direction == PlcDirection.ToPlc ? _toPlc : _fromPlc).Add((topic, point));
            }

            WriteBlock = Span(PlcArea.HoldingRegister, PlcDirection.ToPlc, twinHeartbeatAddress);
            CoilBlock = Span(PlcArea.Coil, PlcDirection.FromPlc, null);
            RegisterBlock = Span(PlcArea.HoldingRegister, PlcDirection.FromPlc, null);
        }

        public ushort TwinHeartbeatAddress { get; }

        /// <summary>Holding registers the twin writes each poll: sensor values and its own heartbeat.</summary>
        public (ushort Start, ushort Count) WriteBlock { get; }

        /// <summary>Coils the twin reads each poll; Count is 0 when the list has none.</summary>
        public (ushort Start, ushort Count) CoilBlock { get; }

        /// <summary>Holding registers the twin reads each poll; Count is 0 when the list has none.</summary>
        public (ushort Start, ushort Count) RegisterBlock { get; }

        public IReadOnlyList<(TopicPath Topic, PlcPoint Point)> ToPlc => _toPlc;
        public IReadOnlyList<(TopicPath Topic, PlcPoint Point)> FromPlc => _fromPlc;

        /// <summary>Fills the register block to write (length WriteBlock.Count) from the current bus values. Gaps are 0.</summary>
        public void FillWriteBlock(ushort[] block, Func<TopicPath, double> readBus, ushort twinHeartbeat)
        {
            if (block == null || block.Length != WriteBlock.Count)
                throw new ArgumentException($"Need a block of {WriteBlock.Count} registers.", nameof(block));
            if (readBus == null)
                throw new ArgumentNullException(nameof(readBus));

            Array.Clear(block, 0, block.Length);
            block[TwinHeartbeatAddress - WriteBlock.Start] = twinHeartbeat;
            for (int i = 0; i < _toPlc.Count; i++)
                block[_toPlc[i].Point.Address - WriteBlock.Start] = ToRegister(readBus(_toPlc[i].Topic), _toPlc[i].Point);
        }

        /// <summary>Every PLC-written point's value, decoded from the blocks just read.</summary>
        public void Decode(bool[] coils, ushort[] registers, List<(TopicPath Topic, SignalValue Value)> into)
        {
            if (into == null)
                throw new ArgumentNullException(nameof(into));
            into.Clear();

            for (int i = 0; i < _fromPlc.Count; i++)
            {
                (TopicPath topic, PlcPoint point) = _fromPlc[i];
                if (point.Area == PlcArea.Coil)
                {
                    into.Add((topic, SignalValue.FromBool(coils[point.Address - CoilBlock.Start])));
                    continue;
                }

                ushort raw = registers[point.Address - RegisterBlock.Start];
                into.Add((topic, point.IsBool ? SignalValue.FromBool(raw != 0) : SignalValue.FromNumber(raw * point.Scale)));
            }
        }

        /// <summary>A bus value as a register: bools as 0/1, numbers divided by the scale, rounded and clamped to 0–65535.</summary>
        public static ushort ToRegister(double value, PlcPoint point)
        {
            if (point.IsBool)
                return value != 0d ? (ushort)1 : (ushort)0;

            double raw = Math.Round(value / point.Scale);
            if (double.IsNaN(raw) || raw < 0d)
                return 0;
            return raw > ushort.MaxValue ? ushort.MaxValue : (ushort)raw;
        }

        private (ushort, ushort) Span(PlcArea area, PlcDirection direction, ushort? extra)
        {
            int min = extra ?? int.MaxValue;
            int max = extra ?? int.MinValue;
            foreach ((TopicPath _, PlcPoint point) in direction == PlcDirection.ToPlc ? _toPlc : _fromPlc)
            {
                if (point.Area != area)
                    continue;
                min = Math.Min(min, point.Address);
                max = Math.Max(max, point.Address);
            }

            if (max < min)
                return (0, 0);

            int count = max - min + 1;
            int limit = area == PlcArea.Coil ? ModbusTcp.MaxReadBits
                : direction == PlcDirection.ToPlc ? ModbusTcp.MaxWriteRegisters : ModbusTcp.MaxReadRegisters;
            if (count > limit)
                throw new ArgumentException($"{direction} {area} points span {count} addresses; one request takes {limit}.");
            return ((ushort)min, (ushort)count);
        }
    }
}
