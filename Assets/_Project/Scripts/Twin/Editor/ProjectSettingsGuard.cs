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

        // CLAUDE.md: two scenes, the start menu first (it loads the cell), then the cell. Until the menu is built
        // (Tools > Factory Twin > Build Menu Scene) the cell alone.
        private const string MenuScenePath = "Assets/_Project/Scenes/Menu.unity";
        private const string CellScenePath = "Assets/_Project/Scenes/Cell.unity";

        static ProjectSettingsGuard()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CellScenePath) != null)
            {
                bool hasMenu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) != null;
                string[] wanted = hasMenu ? new[] { MenuScenePath, CellScenePath } : new[] { CellScenePath };
                EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
                bool ok = scenes.Length == wanted.Length;
                for (int i = 0; ok && i < wanted.Length; i++)
                    ok = scenes[i].enabled && scenes[i].path == wanted[i];
                if (!ok)
                {
                    var fixedScenes = new EditorBuildSettingsScene[wanted.Length];
                    for (int i = 0; i < wanted.Length; i++)
                        fixedScenes[i] = new EditorBuildSettingsScene(wanted[i], true);
                    EditorBuildSettings.scenes = fixedScenes;
                    Debug.Log("[ProjectSettingsGuard] Build Settings now: " + string.Join(", ", wanted) + ".");
                }
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
