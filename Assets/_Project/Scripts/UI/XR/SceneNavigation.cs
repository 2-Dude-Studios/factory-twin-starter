using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Training (M15): moving between the start menu and the cell, keeping the headset on if it is on.</summary>
    public static class SceneNavigation
    {
        // The load under way, if any. A press that counts twice (ray and poke, or a double pull of the trigger) asked
        // for the same scene twice: the second load threw the first away, and with it the training it had started
        // and the headset's session (M27). Further requests wait until the load has finished.
        private static AsyncOperation _loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _loading = null;

        private static bool Busy => _loading != null && !_loading.isDone;

        public static void ToCell(VrSwitch vrSwitch, TrainingMode mode)
        {
            if (Busy)
                return;
            TrainingLaunch.Request(mode);
            vrSwitch?.CarryVrToNextScene();
            _loading = SceneManager.LoadSceneAsync(TrainingLaunch.CellScene);
        }

        public static void ToMenu(VrSwitch vrSwitch)
        {
            if (Busy)
                return;
            vrSwitch?.CarryVrToNextScene();
            _loading = SceneManager.LoadSceneAsync(TrainingLaunch.MenuScene);
        }
    }
}
