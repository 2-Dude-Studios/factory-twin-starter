using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M15): the icons for the badges and the fire drill's HUD (Material Icons, Apache-2.0, white so the UI
    /// tints them). One asset, read by the menu, the popups, the score board and the HUD.
    /// </summary>
    [CreateAssetMenu(menuName = "Factory Twin/Badge Icons", fileName = "BadgeIcons")]
    public sealed class BadgeIcons : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string badge;
            [SerializeField] private Texture2D icon;
            [SerializeField] private Color colour = new Color(1f, 0.8f, 0.25f);

            public Entry(string badge, Texture2D icon, Color colour)
            {
                this.badge = badge;
                this.icon = icon;
                this.colour = colour;
            }

            public string Badge => badge;
            public Texture2D Icon => icon;
            public Color Colour => colour;
        }

        [SerializeField] private Entry[] badges = Array.Empty<Entry>();
        [Header("HUD")]
        [SerializeField] private Texture2D score;
        [SerializeField] private Texture2D timer;
        [SerializeField] private Texture2D fire;
        [SerializeField] private Texture2D extinguisher;
        [SerializeField] private Texture2D done;
        [SerializeField] private Texture2D todo;

        public Texture2D Score => score;
        public Texture2D Timer => timer;
        public Texture2D Fire => fire;
        public Texture2D Extinguisher => extinguisher;
        public Texture2D Done => done;
        public Texture2D Todo => todo;

        public Texture2D IconFor(string badge)
        {
            foreach (Entry e in badges)
            {
                if (e != null && e.Badge == badge)
                    return e.Icon;
            }
            return null;
        }

        public Color ColourFor(string badge)
        {
            foreach (Entry e in badges)
            {
                if (e != null && e.Badge == badge)
                    return e.Colour;
            }
            return new Color(1f, 0.8f, 0.25f);
        }

        /// <summary>Editor setup: fills the asset.</summary>
        public void Configure((string badge, Texture2D icon, Color colour)[] entries, Texture2D scoreIcon, Texture2D timerIcon,
            Texture2D fireIcon, Texture2D extinguisherIcon, Texture2D doneIcon, Texture2D todoIcon)
        {
            badges = new Entry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                badges[i] = new Entry(entries[i].badge, entries[i].icon, entries[i].colour);
            score = scoreIcon;
            timer = timerIcon;
            fire = fireIcon;
            extinguisher = extinguisherIcon;
            done = doneIcon;
            todo = todoIcon;
        }
    }
}
