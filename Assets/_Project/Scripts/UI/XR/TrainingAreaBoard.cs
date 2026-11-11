using System;
using TMPro;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training areas (M24): fills a board in a training area from the trainee's badge book: the hub's badge wall (all
    /// four drills) or one area's score board (its levels, stars, best score and badges). It reads the book when it
    /// comes on and again whenever a book is saved, so a finished drill shows without a restart.
    /// </summary>
    public sealed class TrainingAreaBoard : MonoBehaviour
    {
        [SerializeField] private TrainingArea area;
        [SerializeField] private TMP_Text body;
        [SerializeField] private string traineeName = "Sam Rivera";

        public TrainingArea Area => area;

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
