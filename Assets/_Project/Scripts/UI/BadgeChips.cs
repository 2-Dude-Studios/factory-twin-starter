using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M15): draws badges as icon chips (icon, tinted in the badge's colour, and optionally its name) in a
    /// UI Toolkit row. Used by the start menu, the score board and the fire drill HUD. Styles: badge-chip,
    /// badge-chip__icon, badge-chip__name (Menu.uss and VrPanels.uss).
    /// </summary>
    public static class BadgeChips
    {
        public static void Fill(VisualElement row, IEnumerable<string> badges, BadgeIcons icons, bool withNames)
        {
            if (row == null)
                return;
            row.Clear();
            if (badges == null)
                return;
            foreach (string badge in badges)
                row.Add(Chip(badge, icons, withNames));
        }

        public static VisualElement Chip(string badge, BadgeIcons icons, bool withName)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore, tooltip = badge };
            chip.AddToClassList("badge-chip");
            Texture2D texture = icons != null ? icons.IconFor(badge) : null;
            Color colour = icons != null ? icons.ColourFor(badge) : new Color(1f, 0.8f, 0.25f);
            if (texture != null)
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("badge-chip__icon");
                icon.style.backgroundImage = new StyleBackground(texture);
                icon.style.unityBackgroundImageTintColor = colour;
                chip.Add(icon);
            }
            if (withName || texture == null)
            {
                var name = new Label(texture == null ? "★ " + badge : badge) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("badge-chip__name");
                name.style.color = colour;
                chip.Add(name);
            }
            return chip;
        }

        /// <summary>A row inserted right after <paramref name="anchor"/> (made once, found again by name).</summary>
        public static VisualElement RowAfter(VisualElement anchor, string rowName)
        {
            if (anchor == null || anchor.parent == null)
                return null;
            VisualElement row = anchor.parent.Q(rowName);
            if (row != null)
                return row;
            row = new VisualElement { name = rowName, pickingMode = PickingMode.Ignore };
            row.AddToClassList("badge-row");
            anchor.parent.Insert(anchor.parent.IndexOf(anchor) + 1, row);
            return row;
        }
    }
}
