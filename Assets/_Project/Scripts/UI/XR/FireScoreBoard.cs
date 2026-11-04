using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): the score board at the fire training station (FireScoreBoard.uxml): the session's points,
    /// fires, badges and the latest scored steps; at the end of the session, the badges new to this trainee.
    /// Reads the FireDrillScorer; changes nothing.
    /// </summary>
    public sealed class FireScoreBoard : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private FireDrillScorer scorer;
        [SerializeField, Range(3, 10)] private int latest = 6;
        [SerializeField] private BadgeIcons badgeIcons;

        private Label _trainee, _total, _runs, _badges, _steps, _news;
        private VisualElement _badgeRow;
        private int _badgesShown = -1;

        private void OnEnable()
        {
            if (scorer != null)
            {
                scorer.Scored += OnScored;
                scorer.SessionEnded += OnSessionEnded;
            }
        }

        private void Start()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null)
                return;
            _trainee = root.Q<Label>("board-trainee");
            _total = root.Q<Label>("board-total");
            _runs = root.Q<Label>("board-runs");
            _badges = root.Q<Label>("board-badges");
            _badgeRow = BadgeChips.RowAfter(_badges, "board-badge-row");
            _steps = root.Q<Label>("board-steps");
            _news = root.Q<Label>("board-news");
            Refresh();
        }

        private void OnDisable()
        {
            if (scorer != null)
            {
                scorer.Scored -= OnScored;
                scorer.SessionEnded -= OnSessionEnded;
            }
        }

        private void OnScored(ScoreEvent e) => Refresh();

        private void OnSessionEnded(IReadOnlyList<string> fresh)
        {
            Refresh();
            if (_news != null)
                _news.text = fresh.Count > 0 ? "New for you: " + string.Join(" · ", fresh) : string.Empty;
        }

        private void Refresh()
        {
            if (_total == null || scorer == null)
                return;
            FireDrillScore score = scorer.Score;
            _trainee.text = "Trainee: " + scorer.TraineeName;
            _total.text = score.Total.ToString(CultureInfo.InvariantCulture);
            _runs.text = $"{score.Runs} fire{(score.Runs == 1 ? "" : "s")} done";
            List<string> badges = FireDrillRecord.BadgesInOrder(score);
            _badges.text = badges.Count > 0 ? string.Empty : "No badges yet";
            _badges.style.display = badges.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (badges.Count != _badgesShown)
            {
                _badgesShown = badges.Count;
                BadgeChips.Fill(_badgeRow, badges, badgeIcons, true);
            }
            var steps = new StringBuilder();
            int shown = 0;
            for (int i = score.Events.Count - 1; i >= 0 && shown < latest; i--)
            {
                ScoreEvent e = score.Events[i];
                if (e.Points == 0)
                    continue;
                if (shown > 0)
                    steps.Append('\n');
                steps.Append(e.Points > 0 ? "+" : string.Empty).Append(e.Points.ToString(CultureInfo.InvariantCulture)).Append("   ").Append(e.Text);
                shown++;
            }
            _steps.text = shown > 0 ? steps.ToString() : "Start a fire from the tablet.";
        }
    }
}
