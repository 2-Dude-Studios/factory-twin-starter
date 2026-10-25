using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training: the desktop trainee's HUD (M11), a crosshair and the prompt for the training point under it
    /// (TraineeHud.uxml). Shown only while the trainee's eye is the live shot.
    /// </summary>
    public sealed class TraineeHud : MonoBehaviour
    {
        [SerializeField] private UIDocument document;

        private VisualElement _root;
        private Label _prompt;
        private bool _visible = true;

        private void OnEnable()
        {
            _root = document != null ? document.rootVisualElement : null;
            _prompt = _root?.Q<Label>("prompt");
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (visible == _visible || _root == null)
                return;
            _visible = visible;
            _root.EnableInClassList("hidden", !visible);
        }

        public void SetPrompt(string text)
        {
            if (_prompt == null)
                return;
            bool has = !string.IsNullOrEmpty(text);
            _prompt.text = has ? text + "  ·  click" : string.Empty;
            _prompt.EnableInClassList("hidden", !has);
        }
    }
}
