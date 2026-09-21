using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>App-wide: locks player builds to 60 fps, which the video capture pipeline depends on.</summary>
    public static class FrameRateLock
    {
        public const int TargetFps = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (Application.isEditor)
                return;

            // targetFrameRate is ignored while vSync is on.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;
        }
    }
}
