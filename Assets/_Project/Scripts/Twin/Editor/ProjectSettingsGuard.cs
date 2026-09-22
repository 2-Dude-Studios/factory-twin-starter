using UnityEditor;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Project setup: re-asserts the settings the cell depends on after every domain reload. Asset Store packages
    /// that import as whole projects (the Factory Training sample did) overwrite ProjectSettings silently.
    /// </summary>
    [InitializeOnLoad]
    internal static class ProjectSettingsGuard
    {
        // realvirtual's transport, sensing and MU physics rely on these layer names existing.
        private static readonly string[] RequiredLayers = { "rvMU", "rvSensor", "rvTransport", "rvMUSensor", "rvSimStatic" };

        // CLAUDE.md: Cell.unity is the only scene.
        private const string CellScenePath = "Assets/_Project/Scenes/Cell.unity";

        static ProjectSettingsGuard()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool onlyCell = scenes.Length == 1 && scenes[0].enabled && scenes[0].path == CellScenePath;
            if (!onlyCell && AssetDatabase.LoadAssetAtPath<SceneAsset>(CellScenePath) != null)
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CellScenePath, true) };
                Debug.Log("[ProjectSettingsGuard] Build Settings now contain only " + CellScenePath + ".");
            }

            // Play mode must keep simulating when the Editor is not the focused window (remote control,
            // second-monitor capture) and the kiosk build must keep its MQTT link alive in the background.
            if (!PlayerSettings.runInBackground)
            {
                PlayerSettings.runInBackground = true;
                AssetDatabase.SaveAssets();
                Debug.Log("[ProjectSettingsGuard] Run In Background was off; turned it back on.");
            }

            foreach (string layer in RequiredLayers)
            {
                if (LayerMask.NameToLayer(layer) < 0)
                {
                    Debug.LogError($"[ProjectSettingsGuard] Layer '{layer}' is missing, so realvirtual conveyors and sensors will fail. " +
                                   "Restore ProjectSettings/TagManager.asset and DynamicsManager.asset from git.");
                }
            }
        }
    }
}
