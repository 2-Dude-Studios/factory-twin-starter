using System;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone mode (M27): the robot cell safety drill's page on the tablet in the left hand (VrMenu.uxml), so the
    /// drill runs with the headset alone, as the dashboard's Robot cell buttons run it from the desktop. It opens when a
    /// level starts (cell/training/robot_level, from the start menu's card or here): one button per level with its
    /// best stars (a level opens when the one before has a star), end the level, safety reset, back to the start, and
    /// back to the main menu. The display shows the level running and its steps, then the result.
    /// </summary>
    public sealed class VrRobotMenu : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;
        [SerializeField] private RobotCellScorer scorer;
        [SerializeField] private VrSwitch vrSwitch;
        [SerializeField] private XROrigin origin;
        [Tooltip("The robot area's start spot (TrainingAreaStart): where Back to the start puts the trainee.")]
        [SerializeField] private TrainingAreaStart start;
        [SerializeField, Min(0.05f)] private float refreshSeconds = 0.25f;

        private VisualElement _root, _mainOptions, _hud, _fireOptions, _fireHud, _robotOptions, _robotHud, _levels;
        private Label _live, _result;
        private readonly List<(Button button, Action action)> _wired = new List<(Button, Action)>();
        private readonly Button[] _levelButtons = new Button[DrillLevels.Count];
        private IDisposable[] _subscriptions = Array.Empty<IDisposable>();
        private bool _shown;
        private int _liveVersion = -1;
        private float _nextRefresh;
        private string _resultText = string.Empty;

        private void OnEnable()
        {
            _root = document != null ? document.rootVisualElement : null;
            if (_root == null || context == null)
                return;
            _mainOptions = _root.Q("main-options");
            _hud = _root.Q("hud");
            _fireOptions = _root.Q("fire-options");
            _fireHud = _root.Q("fire-hud");
            _robotOptions = _root.Q("robot-options");
            _robotHud = _root.Q("robot-hud");
            _levels = _root.Q("robot-levels");
            _live = _root.Q<Label>("robot-live");
            _result = _root.Q<Label>("robot-result");

            Wire("robot-end", () => Publish(CellTopics.TrainingRobotLevel, 0d));
            Wire("robot-reset", () => TrainingCommands.SafetyReset(context.Bus));
            Wire("robot-start", BackToStart);
            Wire("robot-home", Home);
            BuildLevelButtons();

            _subscriptions = new[]
            {
                context.Bus.Subscribe(CellTopics.TrainingRobotLevel.ToString(), OnLevel),
                context.Bus.Subscribe(CellTopics.TrainingRobotStars.ToString(), OnStars),
            };
            BadgeBook.Saved += OnBookSaved;
            Show(_shown);
        }

        private void OnDisable()
        {
            foreach ((Button button, Action action) in _wired)
                button.clicked -= action;
            _wired.Clear();
            _levels?.Clear();
            foreach (IDisposable s in _subscriptions)
                s?.Dispose();
            _subscriptions = Array.Empty<IDisposable>();
            BadgeBook.Saved -= OnBookSaved;
        }

        private void Update()
        {
            if (!_shown || Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + refreshSeconds;
            HideOtherPages();
            RefreshLive();
        }

        private void BuildLevelButtons()
        {
            if (_levels == null)
                return;
            _levels.Clear();
            for (int i = 0; i < DrillLevels.Count; i++)
            {
                int level = i + 1;
                var button = new Button { name = "robot-level-" + level };
                button.AddToClassList("menu__button");
                button.AddToClassList("tablet__half");
                Action action = () => Publish(CellTopics.TrainingRobotLevel, level);
                button.clicked += action;
                _wired.Add((button, action));
                _levels.Add(button);
                _levelButtons[i] = button;
            }
            RefreshLevels();
        }

        // Each level's best stars from the trainee's badge book; a level opens when the one before it has a star.
        private void RefreshLevels()
        {
            IReadOnlyList<int> stars = BadgeBook.Load(TraineeName()).Stars(TrainingMode.RobotZone);
            for (int i = 0; i < _levelButtons.Length; i++)
            {
                Button button = _levelButtons[i];
                if (button == null)
                    continue;
                int level = i + 1;
                bool open = DrillProgress.Unlocked(stars, level);
                int best = i < stars.Count ? stars[i] : 0;
                button.text = $"{level} {DrillLevels.LevelName(level)}\n{(open ? DrillStars.Text(best) : "locked")}";
                button.SetEnabled(open);
            }
        }

        private void OnLevel(SignalMessage message)
        {
            if (message.Value.AsNumber > 0d)
            {
                _resultText = string.Empty;
                Show(true);
            }
        }

        private void OnStars(SignalMessage message)
        {
            int stars = (int)message.Value.AsNumber;
            _resultText = $"Level ended: {DrillStars.Text(stars)}  {stars} of {DrillStars.Max} stars. " +
                          (stars > 0 ? "Pick the next level, or play it again." : "Not passed this time: play it again.");
            _liveVersion = -1;
            RefreshLive();
        }

        private void OnBookSaved(string trainee) => RefreshLevels();

        private void RefreshLive()
        {
            if (_live == null)
                return;
            bool running = scorer != null && scorer.Running;
            if (running && scorer.LiveVersion != _liveVersion)
            {
                _liveVersion = scorer.LiveVersion;
                _live.text = scorer.LiveText;
            }
            else if (!running && _liveVersion != -2)
            {
                _liveVersion = -2;
                _live.text = "No level running. Pick one on the right.";
            }
            _result.text = running ? string.Empty : _resultText;
        }

        private void BackToStart()
        {
            if (start != null && origin != null)
                RigTeleport.GoTo(origin, start.FloorPosition, start.YawDegrees);
        }

        private void Home()
        {
            if (scorer != null && scorer.Running)
                Publish(CellTopics.TrainingRobotLevel, 0d);
            Show(false);
            SceneNavigation.ToMenu(vrSwitch);
        }

        private void Show(bool on)
        {
            _shown = on;
            Toggle(_robotOptions, on);
            Toggle(_robotHud, on);
            if (on)
            {
                HideOtherPages();
                RefreshLevels();
                _liveVersion = -1;
                RefreshLive();
            }
        }

        // The robot page is the only one on the tablet while it is open: the main page and the lockout display, and
        // the fire page, stay hidden whatever else wakes the tablet.
        private void HideOtherPages() => TabletPages.ShowOnly(_root, "robot-options", "robot-hud");

        private string TraineeName()
        {
            var controller = context != null ? context.GetComponentInChildren<TrainingController>(true) : null;
            return controller != null ? controller.TraineeName : "Sam Rivera";
        }

        private void Publish(TopicPath topic, double value) =>
            context.Bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);

        private void Wire(string buttonName, Action action)
        {
            Button button = _root.Q<Button>(buttonName);
            if (button == null)
                return;
            button.clicked += action;
            _wired.Add((button, action));
        }

        private static void Toggle(VisualElement element, bool shown)
        {
            if (element != null)
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
