using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M15): the start menu (Menu.unity). Four trainings as cards on the desktop screen and on a panel in
    /// the headset; picking one loads Cell.unity, which starts it (TrainingLauncher). Shows the trainee's badges per
    /// training. Capture runs (-takes) and -cell skip the menu and go straight to the cell.
    /// </summary>
    public sealed class MenuController : MonoBehaviour
    {
        [Tooltip("The desktop (screen) document and the headset (world) document, both Menu.uxml.")]
        [SerializeField] private UIDocument desktopDocument;
        [SerializeField] private UIDocument headsetDocument;
        [SerializeField] private VrSwitch vrSwitch;
        [SerializeField] private string traineeName = "Sam Rivera";
        [SerializeField] private BadgeIcons badgeIcons;

        private readonly System.Collections.Generic.List<(Button button, Action action)> _wired =
            new System.Collections.Generic.List<(Button, Action)>();
        private bool _wasVr;

        private void Awake()
        {
            if (TrainingLaunch.SkipMenu(Environment.GetCommandLineArgs()))
                SceneManager.LoadScene(TrainingLaunch.CellScene);
        }

        // Start, not OnEnable: the documents build their trees in their own OnEnable, in no set order with this one.
        private void Start()
        {
            Wire(desktopDocument);
            Wire(headsetDocument);
            ShowFor(vrSwitch != null && vrSwitch.VrRunning);
        }

        private void OnDestroy()
        {
            foreach ((Button button, Action action) in _wired)
                button.clicked -= action;
            _wired.Clear();
        }

        private void Update()
        {
            bool vr = vrSwitch != null && vrSwitch.VrRunning;
            if (vr != _wasVr)
                ShowFor(vr);
        }

        private void Wire(UIDocument document)
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
                return;
            WireButton(root, "train-fire", () => Launch(TrainingMode.FireSafety, root));
            WireButton(root, "train-lockout", () => Launch(TrainingMode.Lockout, root));
            WireButton(root, "train-zone", () => Launch(TrainingMode.RobotZone, root));
            WireButton(root, "train-free", () => Launch(TrainingMode.FreeRoam, root));
            WireButton(root, "menu-quit", Quit);
            Label trainee = root.Q<Label>("trainee");
            if (trainee != null)
                trainee.text = "Trainee: " + traineeName;
            FillBadges(root);
        }

        private void FillBadges(VisualElement root)
        {
            BadgeBook book = BadgeBook.Load(traineeName);
            SetText(root, "badges-fire", BadgeText(book, TrainingMode.FireSafety));
            SetText(root, "badges-lockout", BadgeText(book, TrainingMode.Lockout));
            SetText(root, "badges-zone", BadgeText(book, TrainingMode.RobotZone));
        }

        private static string BadgeText(BadgeBook book, TrainingMode mode)
        {
            int best = book.BestScore(mode);
            string badges = string.Join(" · ", book.Earned(mode));
            if (best <= 0 && badges.Length == 0)
                return string.Empty;
            return best > 0 ? $"Best {best} pts" + (badges.Length > 0 ? "\n★ " + badges : string.Empty) : "★ " + badges;
        }

        private void Launch(TrainingMode mode, VisualElement root)
        {
            bool vr = vrSwitch != null && vrSwitch.VrRunning;
            if (TrainingLaunch.NeedsHeadset(mode) && !vr)
            {
                Note(root, $"{TrainingLaunch.Title(mode)} is worked with both hands in the headset: press F11 (Quest 3 over Link), then choose it again.");
                return;
            }
            SceneNavigation.ToCell(vrSwitch, mode);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // The headset panel only in VR; the desktop document only on the desktop (in VR the window mirrors the eye).
        private void ShowFor(bool vr)
        {
            _wasVr = vr;
            if (desktopDocument != null && desktopDocument.rootVisualElement != null)
                desktopDocument.rootVisualElement.style.display = vr ? DisplayStyle.None : DisplayStyle.Flex;
            if (headsetDocument != null && headsetDocument.rootVisualElement != null)
                headsetDocument.rootVisualElement.style.display = vr ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (UIDocument doc in new[] { desktopDocument, headsetDocument })
            {
                Label hint = doc != null && doc.rootVisualElement != null ? doc.rootVisualElement.Q<Label>("menu-vr-hint") : null;
                if (hint != null)
                    hint.text = vr ? "In the headset · F11 or the left menu button: back to the desktop" : "F11: headset (Quest 3 over Link)";
            }
        }

        private static void Note(VisualElement root, string text)
        {
            Label note = root.Q<Label>("menu-note");
            if (note == null)
                return;
            note.text = text;
            note.EnableInClassList("hidden", string.IsNullOrEmpty(text));
        }

        private static void SetText(VisualElement root, string name, string text)
        {
            Label label = root.Q<Label>(name);
            if (label != null)
                label.text = text;
        }

        private void WireButton(VisualElement root, string name, Action action)
        {
            Button button = root.Q<Button>(name);
            if (button == null)
                return;
            button.clicked += action;
            _wired.Add((button, action));
        }
    }
}
