using System;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub's menu (M28): a world-space panel at the hub (HubMenu.uxml). The trainee, their rank and the
    /// stars to the next; one card per training with its levels and stars, best score and badges, and a start button
    /// (the robot cell's levels can be picked directly). It reads the trainee's badge book and refreshes when the book
    /// is saved or the trainee comes back to the hub. Starting goes through the <see cref="HubSession"/>.
    /// </summary>
    public sealed class HubMenu : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private HubSession hub;
        [SerializeField] private CellContext context;
        [Tooltip("The studio's logo for the header (optional).")]
        [SerializeField] private Texture2D logo;

        private sealed class Card
        {
            public TrainingMode Mode;
            public string Css, Tag, Title, Text;
            public bool Levels;
            public Label Stars, Best, Badges;
            public Button[] LevelButtons = Array.Empty<Button>();
        }

        private readonly List<Card> _cards = new List<Card>
        {
            new Card { Mode = TrainingMode.RobotZone, Css = "robot", Tag = "ROBOT SAFETY", Title = "Robot cell safety", Levels = true,
                Text = "Walk up to the running cell: the arm slows, then stops. Four levels, awareness to assessment." },
            new Card { Mode = TrainingMode.Lockout, Css = "lockout", Tag = "ENERGY CONTROL", Title = "Lockout / tagout",
                Text = "Clear a CNC fault the right way: lock and tag, try-start, restore. OSHA 1910.147." },
            new Card { Mode = TrainingMode.FireSafety, Css = "fire", Tag = "FIRE SAFETY", Title = "Fire safety",
                Text = "Raise the alarm, pick the right extinguisher, sweep the base, know when to leave." },
            new Card { Mode = TrainingMode.Electrical, Css = "electrical", Tag = "ELECTRICAL SAFETY", Title = "Electrical safety",
                Text = "Gear up in arc flash PPE, lock out the drive cabinet, find the backfeed. NFPA 70E." },
            new Card { Mode = TrainingMode.FreeRoam, Css = "free", Tag = "EXPLORE", Title = "Free roam",
                Text = "Walk the live cell and the hall at your own pace." },
        };

        private readonly List<(Button button, Action action)> _wired = new List<(Button, Action)>();
        private IDisposable _hubSubscription;
        private VisualElement _root;

        // Start, not OnEnable: the document builds its tree in its own OnEnable.
        private void Start()
        {
            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
                return;
            VisualElement logoBox = _root.Q("hub-logo");
            if (logoBox != null)
            {
                logoBox.style.backgroundImage = logo != null ? new StyleBackground(logo) : StyleKeyword.None;
                logoBox.style.display = logo != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            BuildCards();
            Refresh();
            BadgeBook.Saved += OnBookSaved;
            if (context != null)
                _hubSubscription = context.Bus.Subscribe(CellTopics.TrainingHub.ToString(), _ => Refresh());
        }

        private void OnDestroy()
        {
            foreach ((Button button, Action action) in _wired)
                button.clicked -= action;
            _wired.Clear();
            BadgeBook.Saved -= OnBookSaved;
            _hubSubscription?.Dispose();
        }

        private void OnBookSaved(string trainee) => Refresh();

        private void BuildCards()
        {
            VisualElement row = _root.Q("hub-cards");
            if (row == null)
                return;
            row.Clear();
            foreach (Card card in _cards)
            {
                var box = new VisualElement();
                box.AddToClassList("hubcard");
                box.AddToClassList("hubcard--" + card.Css);
                box.Add(Text(card.Tag, "hubcard__tag"));
                box.Add(Text(card.Title, "hubcard__title"));
                box.Add(Text(card.Text, "hubcard__text"));
                card.Stars = Text(string.Empty, "hubcard__stars");
                card.Best = Text(string.Empty, "hubcard__stat");
                card.Badges = Text(string.Empty, "hubcard__stat");
                box.Add(card.Stars);
                box.Add(card.Best);
                box.Add(card.Badges);
                var spacer = new VisualElement();
                spacer.AddToClassList("hubcard__spacer");
                box.Add(spacer);
                if (card.Levels)
                {
                    var levels = new VisualElement();
                    levels.AddToClassList("hubcard__levels");
                    card.LevelButtons = new Button[DrillLevels.Count];
                    for (int i = 0; i < DrillLevels.Count; i++)
                    {
                        int level = i + 1;
                        TrainingMode mode = card.Mode;
                        Button b = Button(string.Empty, "hubcard__level", () => Begin(mode, level));
                        card.LevelButtons[i] = b;
                        levels.Add(b);
                    }
                    box.Add(levels);
                }
                TrainingMode start = card.Mode;
                box.Add(Button(card.Mode == TrainingMode.FreeRoam ? "Explore" : "Start", "hubcard__start", () => Begin(start, 0)));
                row.Add(box);
            }
        }

        private void Begin(TrainingMode mode, int level)
        {
            if (hub != null)
                hub.StartTraining(mode, level);
        }

        /// <summary>The trainee's name, rank and every card's progress, from the badge book.</summary>
        public void Refresh()
        {
            if (_root == null)
                return;
            string trainee = TraineeName();
            SetText("hub-trainee", trainee);
            BadgeBook book = BadgeBook.Load(trainee);
            int totalStars = 0;
            foreach (Card card in _cards)
            {
                IReadOnlyList<int> stars = book.Stars(card.Mode);
                totalStars += DrillProgress.TotalStars(stars);
                card.Stars.text = stars.Count > 0 && DrillProgress.TotalStars(stars) > 0 ? LevelStars(stars) : string.Empty;
                int best = book.BestScore(card.Mode);
                card.Best.text = best > 0 ? $"Best {best} points" : card.Mode == TrainingMode.FreeRoam ? string.Empty : "Not played yet";
                IReadOnlyList<string> badges = book.Earned(card.Mode);
                card.Badges.text = badges.Count > 0 ? $"{badges.Count} badge{(badges.Count == 1 ? "" : "s")}: {string.Join(", ", badges)}" : string.Empty;
                for (int i = 0; i < card.LevelButtons.Length; i++)
                {
                    int level = i + 1;
                    bool open = DrillProgress.Unlocked(stars, level);
                    int got = i < stars.Count ? stars[i] : 0;
                    card.LevelButtons[i].text = $"{level} {DrillLevels.LevelName(level)}\n{(open ? DrillStars.Text(got) : "locked")}";
                    card.LevelButtons[i].SetEnabled(open);
                }
            }
            (string rank, int nextAt, float progress) = TraineeRank.For(totalStars);
            SetText("hub-rank", rank.ToUpperInvariant());
            SetText("hub-progress", nextAt > 0 ? $"{totalStars} stars  ·  {nextAt - totalStars} to the next rank" : $"{totalStars} stars  ·  top rank");
            VisualElement fill = _root.Q("hub-bar-fill");
            if (fill != null)
                fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
        }

        private static string LevelStars(IReadOnlyList<int> stars)
        {
            var parts = new List<string>();
            for (int i = 0; i < stars.Count; i++)
                parts.Add(DrillStars.Text(stars[i]));
            return string.Join("  ", parts);
        }

        private string TraineeName()
        {
            var controller = context != null ? context.GetComponentInChildren<TrainingController>(true) : null;
            return controller != null ? controller.TraineeName : "Trainee";
        }

        private void SetText(string name, string text)
        {
            Label label = _root.Q<Label>(name);
            if (label != null)
                label.text = text;
        }

        private static Label Text(string text, string css)
        {
            var label = new Label(text);
            label.AddToClassList(css);
            return label;
        }

        private Button Button(string text, string css, Action action)
        {
            var button = new Button { text = text };
            button.AddToClassList(css);
            button.clicked += action;
            _wired.Add((button, action));
            return button;
        }
    }
}
