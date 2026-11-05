using UnityEngine.SceneManagement;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Training (M15): moving between the start menu and the cell, keeping the headset on if it is on.</summary>
    public static class SceneNavigation
    {
        public static void ToCell(VrSwitch vrSwitch, TrainingMode mode)
        {
            TrainingLaunch.Request(mode);
            vrSwitch?.CarryVrToNextScene();
            SceneManager.LoadSceneAsync(TrainingLaunch.CellScene);
        }

        public static void ToMenu(VrSwitch vrSwitch)
        {
            vrSwitch?.CarryVrToNextScene();
            SceneManager.LoadSceneAsync(TrainingLaunch.MenuScene);
        }
    }
}
