using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Press station: the risk model's margins and the warning band, as an asset so they can be tuned without code.
    /// One instance lives at Assets/_Project/Data/PressRiskConfig.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Factory Twin/Press Risk Config", fileName = "PressRiskConfig")]
    public sealed class PressRiskConfig : ScriptableObject
    {
        [SerializeField] private FailureRiskSettings settings = new FailureRiskSettings();

        [Tooltip("Once warned, the warning clears only when risk falls below this, so noise near the warning level cannot make the stack light flicker.")]
        [SerializeField, Range(0f, 1f)] private float warningClearRisk = 0.4f;

        public FailureRiskSettings Settings => settings;
        public float WarningRisk => settings.WarningRisk;
        public float WarningClearRisk => Mathf.Min(warningClearRisk, settings.WarningRisk);
    }
}
