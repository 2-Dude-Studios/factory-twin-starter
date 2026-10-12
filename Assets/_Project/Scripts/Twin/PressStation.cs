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
        [Tooltip("Only replay rows with at least one of these faults. Set by the FaultInjector; None is the baseline.")]
        [SerializeField] private FaultMode faultFilter = FaultMode.None;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private int rowsLoaded;
        [SerializeField] private int rowsPublished;
        [SerializeField] private bool finished;

        private DatasetReplayer _replayer;

        /// <summary>The failure modes the press is currently made to report; None returns it to the baseline.</summary>
        public FaultMode FaultFilter
        {
            get => faultFilter;
            set => faultFilter = value;
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

            _replayer.Tick(Time.deltaTime, CellContext.NowUnixMs);
            rowsPublished = _replayer.RowsPublished;
            finished = _replayer.Finished;
        }
    }
}
