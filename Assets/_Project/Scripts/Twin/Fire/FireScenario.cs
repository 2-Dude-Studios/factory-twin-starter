using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Fire safety (M14): one training scenario (F1–F6 in docs/fire-safety-training.md): where the fire starts, its
    /// class, whether the dashboard sees a precursor, its timings, and whether it is a flee-only fire. Started by
    /// number on cell/fire/scenario.
    /// </summary>
    [CreateAssetMenu(menuName = "Factory Twin/Fire Scenario", fileName = "FireScenario")]
    public sealed class FireScenario : ScriptableObject
    {
        [Tooltip("1–6: F1 cabinet, F2 CNC, F3 waste, F4 fire past incipient, F5 F4 with the primary exit blocked, F6 charger.")]
        [SerializeField, Range(1, 9)] private int number = 1;
        [SerializeField] private string title = "Drive cabinet overheats";
        [SerializeField] private FireLocation location = FireLocation.DriveCabinet;
        [SerializeField] private FireClass fireClass = FireClass.C;
        [Tooltip("The drive cabinet heats up on the dashboard before it smokes (F1).")]
        [SerializeField] private bool precursor;
        [Tooltip("A fire nobody should fight (lithium-ion): the trainee must raise the alarm and leave.")]
        [SerializeField] private bool fleeOnly;
        [Tooltip("The primary exit route is blocked (F5), used from M16.")]
        [SerializeField] private bool primaryRouteBlocked;
        [SerializeField] private FireModelSettings settings = new FireModelSettings();

        public int Number => number;
        public string Title => title;
        public FireLocation Location => location;
        public FireClass Class => fireClass;
        public bool Precursor => precursor;
        public bool FleeOnly => fleeOnly;
        public bool PrimaryRouteBlocked => primaryRouteBlocked;
        public FireModelSettings Settings => settings;

        public FireModel CreateModel() => new FireModel(settings, fireClass, precursor);

        /// <summary>Editor setup: fills a new asset with one scenario's values.</summary>
        public void Configure(int scenarioNumber, string scenarioTitle, FireLocation at, FireClass cls, bool hasPrecursor,
            bool flee, bool routeBlocked, FireModelSettings modelSettings)
        {
            number = scenarioNumber;
            title = scenarioTitle;
            location = at;
            fireClass = cls;
            precursor = hasPrecursor;
            fleeOnly = flee;
            primaryRouteBlocked = routeBlocked;
            settings = modelSettings ?? new FireModelSettings();
        }
    }
}
