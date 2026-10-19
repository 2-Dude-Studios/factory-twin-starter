using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Dashboard: fills the Credits overlay's licence section with the full third-party notices
    /// (Assets/_Project/Licenses) and toggles it with the "Full licence texts" button. The same files are copied
    /// next to the .exe after each build.
    /// </summary>
    [DefaultExecutionOrder(220)]
    public sealed class LicenseTextsView : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [Tooltip("In display order: Assets/_Project/Licenses/00-… to 05-….")]
        [SerializeField] private TextAsset[] notices;

        private Button _toggle;
        private VisualElement _scroll;

        private void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
                return;

            _toggle = root.Q<Button>("license-toggle");
            _scroll = root.Q("license-scroll");
            Label text = root.Q<Label>("license-text");
            if (text != null)
                text.text = Join(notices);
            if (_toggle != null)
                _toggle.clicked += Toggle;
        }

        private void OnDisable()
        {
            if (_toggle != null)
                _toggle.clicked -= Toggle;
        }

        private void Toggle()
        {
            _scroll?.ToggleInClassList("hidden");
        }

        private static string Join(TextAsset[] assets)
        {
            if (assets == null)
                return string.Empty;
            var all = new StringBuilder();
            foreach (TextAsset asset in assets)
            {
                if (asset == null)
                    continue;
                if (all.Length > 0)
                    all.Append("\n\n────────────────────────────────\n\n");
                all.Append(asset.text.TrimEnd());
            }
            return all.ToString();
        }
    }
}
