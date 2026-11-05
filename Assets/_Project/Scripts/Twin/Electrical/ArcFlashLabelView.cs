using TMPro;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Electrical safety (M19): the arc flash label on the drive cabinet's door, filled from the
    /// <see cref="ArcFlashStudy"/> at start: the signal word in the header band (ANSI Z535 style, orange for WARNING,
    /// red for DANGER), the study's values below. Change the study asset and the label follows; nothing is typed in
    /// the scene.
    /// </summary>
    public sealed class ArcFlashLabelView : MonoBehaviour
    {
        [SerializeField] private ArcFlashStudy study;
        [SerializeField] private TMP_Text header;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Renderer headerBand;
        [SerializeField] private Color warningColor = new Color(1f, 0.45f, 0f);
        [SerializeField] private Color dangerColor = new Color(0.78f, 0.05f, 0.05f);

        private void Awake() => Show();

        private void OnValidate()
        {
            if (study != null && header != null && body != null)
                Show();
        }

        private void Show()
        {
            if (study == null)
                return;
            string text = ArcFlashLabel.Text(study.Values);
            int firstBreak = text.IndexOf('\n');
            string word = firstBreak < 0 ? text : text.Substring(0, firstBreak);
            if (header != null)
                header.text = word;
            if (body != null)
                body.text = firstBreak < 0 ? string.Empty : text.Substring(firstBreak + 1);
            if (headerBand != null)
            {
                var block = new MaterialPropertyBlock();
                headerBand.GetPropertyBlock(block);
                block.SetColor("_BaseColor", word == "DANGER" ? dangerColor : warningColor);
                headerBand.SetPropertyBlock(block);
            }
        }
    }
}
