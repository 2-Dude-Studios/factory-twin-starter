using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone mode (M27): the left-hand tablet's pages (VrMenu.uxml), one display and one set of options each:
    /// the main page with the lockout display, and the fire, robot cell and electrical drills' pages. A drill page shows
    /// itself and hides every other, so two pages never draw over each other.
    /// </summary>
    public static class TabletPages
    {
        private static readonly string[] All =
        {
            "main-options", "hud", "fire-options", "fire-hud", "robot-options", "robot-hud", "elec-options", "elec-hud",
        };

        /// <summary>Shows the named elements and hides the other pages' (only where they are not already so).</summary>
        public static void ShowOnly(VisualElement root, string options, string display)
        {
            if (root == null)
                return;
            foreach (string name in All)
            {
                VisualElement page = root.Q(name);
                if (page == null)
                    continue;
                DisplayStyle wanted = name == options || name == display ? DisplayStyle.Flex : DisplayStyle.None;
                if (page.style.display != wanted)
                    page.style.display = wanted;
            }
        }
    }
}
