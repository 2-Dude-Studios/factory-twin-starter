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
        [Tooltip("The Meta account and leaderboards (optional; offline without them).")]
        [SerializeField] private MetaAccount account;
        [SerializeField] private MetaLeaderboards boards;

        [Header("Credits and licences (M29)")]
        [Tooltip("The first credits page: who made what in the app.")]
        [SerializeField, TextArea(6, 20)] private string credits = DefaultCredits;
        [Tooltip("The third-party notices in full, in order (Assets/_Project/Licenses).")]
        [SerializeField] private TextAsset[] notices = Array.Empty<TextAsset>();
        [SerializeField, Min(5)] private int creditLines = 20;
        [SerializeField, Min(20)] private int creditChars = 105;

        public const string DefaultCredits =
            "Factory VR Safety Training, by 2 Dude Studios (2dudestudios.com).\n\n" +
            "Made with Unity, the Unity XR Interaction Toolkit, OpenXR and XR Hands, and the Meta XR Platform SDK. " +
            "The production cell runs on realvirtual.io Digital Twin Starter, included as a compiled component.\n\n" +
            "Factory hall and props: Factory Training | Industry Sample (Unity Asset Store). Fire and smoke: Unity Particle Pack. Arm IK: Fast IK.\n" +
            "Workers: Microsoft Rocketbox avatars and animations (MIT); Animated Human by Quaternius (CC0).\n" +
            "Robot arm: our own cobot model; its joint layout comes from the UR5e description in ros-industrial/universal_robot " +
            "(BSD 3-Clause), imported with Unity's URDF Importer (Apache 2.0). Not affiliated with Universal Robots.\n" +
            "Sounds: Large Warehouse/Factory Ambience by fimrod and Electric Sparks by kev_durr, on Freesound (CC0).\n" +
            "Icons: Material Icons by Google (Apache 2.0).\n" +
            "The cobot, the electrical cabinet, the arc flash PPE kit and the lockout props were made for this app.\n\n" +
            "A training demo, not certified training. The safety content is guidance, not legal advice, and the arc flash label " +
            "values come from a made-up study.\n\n" +
            "Next: each third-party notice and licence in full.";

        private List<string> _creditPages;
        private int _creditPage;

        [Header("First-run guide and comfort (M29)")]
        [SerializeField] private ComfortSettings comfort;
        private const string GuideSeenKey = "ft.guide.seen";
        private int _guidePage;

        private sealed class Card
        {
            public TrainingMode Mode;
            public string Css, Tag, Title, Text;
            public bool Levels;
            public Label Stars, Best, Badges, Board;
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
            Wire("hub-credits", () => ShowCredits(true));
            Wire("hub-credits-close", () => ShowCredits(false));
            Wire("hub-credits-prev", () => CreditPage(_creditPage - 1));
            Wire("hub-credits-next", () => CreditPage(_creditPage + 1));
            Wire("hub-guide", () => ShowGuide(0));
            Wire("hub-comfort", () => ShowGuide(1));
            Wire("hub-guide-back", () => GuidePage(_guidePage - 1));
            Wire("hub-guide-next", () => GuidePage(_guidePage + 1));
            if (comfort != null)
            {
                Wire("hub-comfort-move", comfort.ToggleSmoothMove);
                Wire("hub-comfort-turn", comfort.ToggleSmoothTurn);
                Wire("hub-comfort-vignette", comfort.ToggleVignette);
                Wire("hub-comfort-voice", comfort.ToggleVoice);
                comfort.Changed += ComfortText;
            }
            ComfortText();
            // The first time the app opens: the guide, before anything else.
            if (!PlayerPrefs.HasKey(GuideSeenKey))
                ShowGuide(0);
            Button daily = _root.Q<Button>("hub-daily-go");
            if (daily != null)
            {
                Action play = PlayDaily;
                daily.clicked += play;
                _wired.Add((daily, play));
            }
            Refresh();
            BadgeBook.Saved += OnBookSaved;
            if (account != null)
                account.SignedIn += OnSignedIn;
            if (boards != null)
                boards.Updated += OnBoard;
            if (context != null)
                _hubSubscription = context.Bus.Subscribe(CellTopics.TrainingHub.ToString(), _ => Refresh());
        }

        private void OnDestroy()
        {
            foreach ((Button button, Action action) in _wired)
                button.clicked -= action;
            _wired.Clear();
            BadgeBook.Saved -= OnBookSaved;
            if (comfort != null)
                comfort.Changed -= ComfortText;
            if (account != null)
                account.SignedIn -= OnSignedIn;
            if (boards != null)
                boards.Updated -= OnBoard;
            _hubSubscription?.Dispose();
        }

        private void Wire(string name, Action action)
        {
            Button button = _root.Q<Button>(name);
            if (button == null)
                return;
            button.clicked += action;
            _wired.Add((button, action));
        }

        // The credits overlay covers the cards; the pages are made the first time it opens.
        private void ShowCredits(bool show)
        {
            VisualElement panel = _root?.Q("hub-credits-panel");
            if (panel == null)
                return;
            panel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show)
                return;
            Show("hub-guide-panel", false);
            if (_creditPages == null)
            {
                var texts = new List<string> { credits };
                foreach (TextAsset notice in notices)
                {
                    if (notice != null)
                        texts.Add(notice.text);
                }
                _creditPages = CreditPages.Split(texts, creditLines, creditChars);
            }
            CreditPage(0);
        }

        private void CreditPage(int page)
        {
            if (_creditPages == null || _creditPages.Count == 0)
                return;
            _creditPage = Mathf.Clamp(page, 0, _creditPages.Count - 1);
            SetText("hub-credits-text", _creditPages[_creditPage]);
            SetText("hub-credits-page", $"Page {_creditPage + 1} of {_creditPages.Count}");
            _root.Q<Button>("hub-credits-prev")?.SetEnabled(_creditPage > 0);
            _root.Q<Button>("hub-credits-next")?.SetEnabled(_creditPage < _creditPages.Count - 1);
        }

        // The guide: page 0 how to play, page 1 comfort. Next on the last page closes it (and the first-run flag is set).
        private void ShowGuide(int page)
        {
            ShowCredits(false);
            VisualElement panel = _root?.Q("hub-guide-panel");
            if (panel == null)
                return;
            panel.style.display = DisplayStyle.Flex;
            GuidePage(page);
        }

        private void GuidePage(int page)
        {
            VisualElement panel = _root?.Q("hub-guide-panel");
            if (panel == null)
                return;
            if (page > 1)
            {
                panel.style.display = DisplayStyle.None;
                PlayerPrefs.SetInt(GuideSeenKey, 1);
                PlayerPrefs.Save();
                return;
            }
            _guidePage = Mathf.Max(0, page);
            bool how = _guidePage == 0;
            Show("hub-guide-how", how);
            Show("hub-guide-comfort", !how);
            SetText("hub-guide-title", how ? "HOW TO PLAY" : "COMFORT");
            SetText("hub-guide-page", $"{_guidePage + 1} of 2");
            _root.Q<Button>("hub-guide-back")?.SetEnabled(!how);
            Button next = _root.Q<Button>("hub-guide-next");
            if (next != null)
                next.text = how ? "Next" : "Done";
        }

        private void ComfortText()
        {
            if (_root == null || comfort == null)
                return;
            SetButton("hub-comfort-move", comfort.SmoothMove ? "Movement: walk with the left stick, teleport with the right  (tap to change)" : "Movement: teleport only, most comfortable  (tap to change)");
            SetButton("hub-comfort-turn", comfort.SmoothTurn ? "Turning: smooth  (tap to change)" : "Turning: snap 45°, most comfortable  (tap to change)");
            SetButton("hub-comfort-vignette", comfort.Vignette ? "Comfort vignette: on while moving smoothly  (tap to change)" : "Comfort vignette: off  (tap to change)");
            SetButton("hub-comfort-voice", ComfortSettings.VoiceOn ? "Trainer voice: on  (tap to change)" : "Trainer voice: off  (tap to change)");
        }

        private void Show(string name, bool show)
        {
            VisualElement e = _root?.Q(name);
            if (e != null)
                e.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetButton(string name, string text)
        {
            Button button = _root?.Q<Button>(name);
            if (button != null)
                button.text = text;
        }

        private void OnBookSaved(string trainee) => Refresh();

        private void OnSignedIn(string name) => Refresh();

        private void OnBoard(TrainingMode mode) => Refresh();

        // A card's leaderboard: the top lines once the Meta account is up.
        private string BoardText(TrainingMode mode)
        {
            if (MetaLeaderboards.BoardFor(mode) == null)
                return string.Empty;
            if (account == null || !account.Ready)
                return "Leaderboard: with your Meta account in the store app";
            IReadOnlyList<MetaLeaderboards.Line> top = boards != null ? boards.Top(mode) : null;
            if (top == null || top.Count == 0)
                return "Leaderboard: no scores yet. Be first!";
            var lines = new List<string> { "LEADERBOARD" };
            foreach (MetaLeaderboards.Line line in top)
                lines.Add($"{line.Rank}. {line.Name}  {line.Score}");
            return string.Join("\n", lines);
        }

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
                card.Board = Text(string.Empty, "hubcard__board");
                box.Add(card.Board);
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

        private void PlayDaily()
        {
            (TrainingArea area, int level) = BadgeBook.Load(TraineeName()).Daily(DateTime.UtcNow);
            Begin(TrainingLaunch.ModeFor(area), level);
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
            foreach (Card card in _cards)
            {
                IReadOnlyList<int> stars = book.Stars(card.Mode);
                card.Stars.text = stars.Count > 0 && DrillProgress.TotalStars(stars) > 0 ? LevelStars(stars) : string.Empty;
                int best = book.BestScore(card.Mode);
                card.Best.text = best > 0 ? $"Best {best} points" : card.Mode == TrainingMode.FreeRoam ? string.Empty : "Not played yet";
                IReadOnlyList<string> badges = book.Earned(card.Mode);
                card.Board.text = BoardText(card.Mode);
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
            int xp = book.Xp;
            (string rank, int nextAt, float progress) = TraineeRank.For(xp);
            SetText("hub-rank", rank.ToUpperInvariant());
            SetText("hub-progress", nextAt > 0 ? $"{xp} XP  ·  {nextAt - xp} to the next rank" : $"{xp} XP  ·  top rank");

            // The day's challenge: which training and level, double XP once today.
            DateTime now = DateTime.UtcNow;
            (TrainingArea dailyArea, int dailyLevel) = book.Daily(now);
            TrainingMode dailyMode = TrainingLaunch.ModeFor(dailyArea);
            string what = TrainingLaunch.Title(dailyMode) + (dailyLevel > 0 ? $", level {dailyLevel} {DrillLevels.LevelName(dailyLevel)}" : string.Empty);
            bool done = book.DailyDone(now);
            SetText("hub-daily", done ? $"{what}: done today. A new one tomorrow." : $"{what}: double XP today.");
            SetText("hub-streak", book.Streak > 1 ? $"{book.Streak}-day streak" : string.Empty);
            Button go = _root.Q<Button>("hub-daily-go");
            go?.SetEnabled(!done);
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
