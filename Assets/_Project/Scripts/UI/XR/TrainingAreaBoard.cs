using System;
using TMPro;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training areas (M24): fills a board in a training area from the trainee's badge book: the hub's badge wall (all
    /// four drills) or one area's score board (its levels, stars, best score and badges). It reads the book when it
    /// comes on and again whenever a book is saved, so a finished drill shows without a restart. While the area's
    /// drill runs, the board shows the run instead: its checklist, points and what is due.
    /// </summary>
    public sealed class TrainingAreaBoard : MonoBehaviour
    {
        [SerializeField] private TrainingArea area;
        [SerializeField] private TMP_Text body;
        [SerializeField] private string traineeName = "Sam Rivera";
        [Tooltip("The area's drill while it runs (an IDrillLive, e.g. RobotCellScorer); empty for a board that only shows the book.")]
        [SerializeField] private MonoBehaviour liveSource;

        private IDrillLive _live;
        private int _shownVersion = -1;
        private bool _showingLive;

        public TrainingArea Area => area;

        private void Awake()
        {
            _live = liveSource as IDrillLive;
        }

        private void Update()
        {
            if (_live == null || body == null)
                return;
            if (_live.Running)
            {
                if (!_showingLive || _live.LiveVersion != _shownVersion)
                {
                    _showingLive = true;
                    _shownVersion = _live.LiveVersion;
                    body.text = _live.LiveText;
                }
            }
            else if (_showingLive)
            {
                _showingLive = false;
                Refresh();
            }
        }

        private void OnEnable()
        {
            BadgeBook.Saved += OnSaved;
            Refresh();
        }

        private void OnDisable()
        {
            BadgeBook.Saved -= OnSaved;
        }

        private void OnSaved(string trainee)
        {
            if (trainee == traineeName)
                Refresh();
        }

        /// <summary>Reads the trainee's book and writes the board.</summary>
        public void Refresh()
        {
            if (body == null)
                return;
            BadgeBook book = BadgeBook.Load(traineeName);
            body.text = area == TrainingArea.Hub
                ? DrillBoardText.BadgeWall(book, DateTime.UtcNow)
                : DrillBoardText.AreaBoard(book, area, DateTime.UtcNow);
        }
    }
}
