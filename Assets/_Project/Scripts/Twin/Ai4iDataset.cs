using System;
using System.Collections.Generic;
using System.Globalization;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Press station: AI4I 2020 failure modes, matching the dataset's flag columns.</summary>
    [Flags]
    public enum FaultMode
    {
        None = 0,
        ToolWear = 1 << 0,        // TWF
        HeatDissipation = 1 << 1, // HDF
        Power = 1 << 2,           // PWF
        Overstrain = 1 << 3,      // OSF
        Random = 1 << 4,          // RNF
    }

    /// <summary>Press station: one row of the AI4I 2020 dataset, in the dataset's own units (K, rpm, Nm, min).</summary>
    public readonly struct Ai4iRow
    {
        public double AirTempK { get; }
        public double ProcessTempK { get; }
        public double SpeedRpm { get; }
        public double TorqueNm { get; }
        public double ToolWearMin { get; }
        public bool MachineFailure { get; }
        public FaultMode Faults { get; }

        public Ai4iRow(double airTempK, double processTempK, double speedRpm, double torqueNm, double toolWearMin,
            bool machineFailure, FaultMode faults)
        {
            AirTempK = airTempK;
            ProcessTempK = processTempK;
            SpeedRpm = speedRpm;
            TorqueNm = torqueNm;
            ToolWearMin = toolWearMin;
            MachineFailure = machineFailure;
            Faults = faults;
        }
    }

    /// <summary>
    /// Press station: parses ai4i2020.csv (UCI ML Repository, doi:10.24432/C5HS5C, CC BY 4.0).
    /// Columns are located by header name so extra or reordered columns are tolerated.
    /// </summary>
    public static class Ai4iCsv
    {
        private const string AirTemp = "Air temperature [K]";
        private const string ProcessTemp = "Process temperature [K]";
        private const string Speed = "Rotational speed [rpm]";
        private const string Torque = "Torque [Nm]";
        private const string ToolWear = "Tool wear [min]";
        private const string Failure = "Machine failure";

        private static readonly string[] FaultColumns = { "TWF", "HDF", "PWF", "OSF", "RNF" };

        public static List<Ai4iRow> Parse(string csvText)
        {
            if (string.IsNullOrEmpty(csvText))
                throw new FormatException("AI4I CSV is empty.");

            string[] lines = csvText.Split('\n');
            string[] header = SplitLine(lines[0].TrimStart('﻿'));

            int airTemp = Find(header, AirTemp);
            int processTemp = Find(header, ProcessTemp);
            int speed = Find(header, Speed);
            int torque = Find(header, Torque);
            int toolWear = Find(header, ToolWear);
            int failure = Find(header, Failure);
            var faults = new int[FaultColumns.Length];
            for (int f = 0; f < FaultColumns.Length; f++)
                faults[f] = Find(header, FaultColumns[f]);

            var rows = new List<Ai4iRow>(lines.Length - 1);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                string[] cells = SplitLine(lines[i]);
                if (cells.Length < header.Length)
                    throw new FormatException($"AI4I CSV line {i + 1}: expected {header.Length} columns, found {cells.Length}.");

                FaultMode mode = FaultMode.None;
                for (int f = 0; f < faults.Length; f++)
                {
                    if (Flag(cells[faults[f]], i))
                        mode |= (FaultMode)(1 << f);
                }

                rows.Add(new Ai4iRow(
                    Number(cells[airTemp], i),
                    Number(cells[processTemp], i),
                    Number(cells[speed], i),
                    Number(cells[torque], i),
                    Number(cells[toolWear], i),
                    Flag(cells[failure], i),
                    mode));
            }
            return rows;
        }

        // The dataset has no quoted fields, so a plain split is enough.
        private static string[] SplitLine(string line) => line.TrimEnd('\r').Split(',');

        private static int Find(string[] header, string column)
        {
            int index = Array.IndexOf(header, column);
            if (index < 0)
                throw new FormatException($"AI4I CSV is missing the '{column}' column.");
            return index;
        }

        private static double Number(string cell, int lineIndex)
        {
            if (!double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new FormatException($"AI4I CSV line {lineIndex + 1}: '{cell}' is not a number.");
            return value;
        }

        private static bool Flag(string cell, int lineIndex)
        {
            if (cell == "1")
                return true;
            if (cell == "0")
                return false;
            throw new FormatException($"AI4I CSV line {lineIndex + 1}: '{cell}' is not a 0/1 flag.");
        }
    }
}
