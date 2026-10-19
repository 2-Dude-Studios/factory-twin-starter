using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: plays the AI4I dataset onto the bus as the press's live telemetry, so cell/press/* flows
    /// without the Python replayer. Disable it when tools/replay/replay.py is feeding the broker instead, or the
    /// two will take turns writing the same topics.
    /// </summary>
    public sealed class PressStation : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("ai4i2020.csv (Assets/_Project/Data).")]
        [SerializeField] private TextAsset dataset;
        [SerializeField, Range(0.1f, 20f)] private float rowsPerSecond = 2f;
        [SerializeField] private bool loop = true;
        [Tooltip("Skip the dataset's own failure rows so the press only reports a fault when one is injected.")]
        [SerializeField] private bool healthyBaseline = true;
        [Tooltip("With a risk config, the healthy baseline also skips rows whose failure risk is at or above the warning clear level, so amber appears only when the demo asks for it (degradation).")]
        [SerializeField] private PressRiskConfig riskConfig;
        [Tooltip("Only replay rows with at least one of these faults. Set by the FaultInjector; None is the baseline.")]
        [SerializeField] private FaultMode faultFilter = FaultMode.None;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private int rowsLoaded;
        [SerializeField] private int rowsPublished;
        [SerializeField] private bool finished;

        private DatasetReplayer _replayer;
        private double _toolWearOverride = double.NaN;

        /// <summary>The failure modes the press is currently made to report; None returns it to the baseline.</summary>
        public FaultMode FaultFilter
        {
            get => faultFilter;
            set => faultFilter = value;
        }

        /// <summary>Tool wear to publish instead of each row's own; NaN publishes the dataset's value.</summary>
        public double ToolWearOverride
        {
            get => _toolWearOverride;
            set => _toolWearOverride = value;
        }

        private void Start()
        {
            if (dataset == null)
            {
                Debug.LogError("[PressStation] No dataset assigned; expected Assets/_Project/Data/ai4i2020.csv.", this);
                enabled = false;
                return;
            }

            try
            {
                var rows = Ai4iCsv.Parse(dataset.text);
                rowsLoaded = rows.Count;
                _replayer = new DatasetReplayer(rows, context.Bus);
                if (riskConfig != null)
                    _replayer.BaselineFilter = IsCalm;
            }
            catch (FormatException e)
            {
                Debug.LogError($"[PressStation] '{dataset.name}' is not a usable AI4I file: {e.Message}", this);
                enabled = false;
            }
        }

        private void Update()
        {
            // Applied every frame so the Inspector values can be changed while the demo runs.
            _replayer.RowsPerSecond = rowsPerSecond;
            _replayer.Loop = loop;
            _replayer.SkipFailures = healthyBaseline;
            _replayer.FaultFilter = faultFilter;
            _replayer.ToolWearOverride = _toolWearOverride;

            _replayer.Tick(Time.deltaTime, CellContext.NowUnixMs);
            rowsPublished = _replayer.RowsPublished;
            finished = _replayer.Finished;
        }

        // A baseline row is calm when its failure risk stays under the warning clear level. While the tool wear is
        // overridden (degradation), the row is judged at that wear and the tool-wear term is left out, so only the
        // scheduled wear drives the risk and no other mode can warn first.
        private bool IsCalm(Ai4iRow row)
        {
            bool overridden = !double.IsNaN(_toolWearOverride);
            double wear = overridden ? _toolWearOverride : row.ToolWearMin;
            var reading = new PressReading(row.AirTempK, row.ProcessTempK, row.SpeedRpm, row.TorqueNm, wear, row.Quality);
            RiskAssessment a = FailureRiskModel.Evaluate(reading, riskConfig.Settings);
            double risk = overridden
                ? System.Math.Max(a.OverstrainRisk, System.Math.Max(a.PowerRisk, a.HeatRisk))
                : a.Risk;
            return risk < riskConfig.WarningClearRisk;
        }
    }
}
