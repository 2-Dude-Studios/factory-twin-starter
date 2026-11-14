using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone mode (M27): the left-hand tablet's pages (VrMenu.uxml), one display and one set of options each:
    /// the main page with the lockout display, and the fire, robot cell and electrical drills' pages. A drill page shows
    /// itself and hides every other, so two pages never draw over each other. While the comfort page is open (M29) it
    /// stands in for whichever page's options would show, so a drill page refreshing itself does not close it, and the
    /// page it came from is remembered for Back.
    /// </summary>
    public static class TabletPages
    {
        public const string ComfortOptions = "comfort-options";

        private static readonly string[] All =
        {
            "main-options", "hud", "fire-options", "fire-hud", "robot-options", "robot-hud", "elec-options", "elec-hud",
            ComfortOptions,
        };

        /// <summary>The comfort page is open over this page's options (null while it is closed).</summary>
        public static string ComfortOver { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => ComfortOver = null;

        /// <summary>Shows the named elements and hides the other pages' (only where they are not already so).</summary>
        public static void ShowOnly(VisualElement root, string options, string display)
        {
            if (root == null)
                return;
            if (ComfortOver != null)
            {
                ComfortOver = options;
                options = ComfortOptions;
            }
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

        /// <summary>Opens the comfort page over the options showing now, keeping the display.</summary>
        public static void OpenComfort(VisualElement root)
        {
            if (root == null || ComfortOver != null)
                return;
            string options = Visible(root, "main-options", "fire-options", "robot-options", "elec-options") ?? "main-options";
            string display = Visible(root, "hud", "fire-hud", "robot-hud", "elec-hud") ?? "hud";
            ComfortOver = options;
            ShowOnly(root, options, display);
        }

        /// <summary>Back from the comfort page to the options it was opened over.</summary>
        public static void CloseComfort(VisualElement root)
        {
            if (ComfortOver == null)
                return;
            string options = ComfortOver;
            ComfortOver = null;
            string display = root != null ? Visible(root, "hud", "fire-hud", "robot-hud", "elec-hud") ?? "hud" : "hud";
            ShowOnly(root, options, display);
        }

        private static string Visible(VisualElement root, params string[] names)
        {
            foreach (string name in names)
            {
                VisualElement page = root.Q(name);
                if (page != null && page.resolvedStyle.display == DisplayStyle.Flex)
                    return name;
            }
            return null;
        }
    }
}
