using System;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Training (M15): which training the start menu asked the cell to begin.</summary>
    public enum TrainingMode
    {
        None = 0,
        FreeRoam = 1,
        Lockout = 2,
        RobotZone = 3,
        FireSafety = 4,

        /// <summary>Electrical safety and arc flash (Rev 7); its menu card comes with M22.</summary>
        Electrical = 5,
    }

    /// <summary>
    /// Training (M15): the start menu's request, carried from Menu.unity into Cell.unity across the scene load.
    /// The menu sets it, the cell takes it once. Also decides when the menu is skipped: capture runs (-takes) and an
    /// explicit -cell go straight to the cell, so the recording tools keep working unchanged.
    /// </summary>
    public static class TrainingLaunch
    {
        public const string MenuScene = "Menu";
        public const string CellScene = "Cell";
        public const string SkipMenuFlag = "-cell";

        private static readonly string[] SkipFlags = { SkipMenuFlag, "-takes" };

        private static TrainingMode _requested;

        public static void Request(TrainingMode mode) => _requested = mode;

        /// <summary>The request, once; None after that (and when the cell was opened directly).</summary>
        public static TrainingMode Take()
        {
            TrainingMode mode = _requested;
            _requested = TrainingMode.None;
            return mode;
        }

        public static bool SkipMenu(string[] commandLine)
        {
            if (commandLine == null)
                return false;
            foreach (string flag in SkipFlags)
            {
                if (Array.IndexOf(commandLine, flag) >= 0)
                    return true;
            }
            return false;
        }

        public static string Title(TrainingMode mode)
        {
            switch (mode)
            {
                case TrainingMode.FireSafety: return "Fire safety";
                case TrainingMode.Lockout: return "Lockout / tagout";
                case TrainingMode.RobotZone: return "Robot safety zone";
                case TrainingMode.FreeRoam: return "Free roam";
                case TrainingMode.Electrical: return "Electrical safety";
                default: return string.Empty;
            }
        }

        /// <summary>The area in the hall where a training starts (M24); free roam starts at the hub.</summary>
        public static TrainingArea AreaFor(TrainingMode mode)
        {
            switch (mode)
            {
                case TrainingMode.FireSafety: return TrainingArea.Fire;
                case TrainingMode.Lockout: return TrainingArea.Lockout;
                case TrainingMode.RobotZone: return TrainingArea.RobotCell;
                case TrainingMode.Electrical: return TrainingArea.Electrical;
                default: return TrainingArea.Hub;
            }
        }

        /// <summary>Whether the training needs the headset (the fire drill is worked with two hands).</summary>
        public static bool NeedsHeadset(TrainingMode mode) => mode == TrainingMode.FireSafety;
    }
}
