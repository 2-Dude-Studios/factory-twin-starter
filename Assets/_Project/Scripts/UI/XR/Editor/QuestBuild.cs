using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone Quest (M25 spike): the steps from the Windows project to an APK on a Quest 3, as menu items so each
    /// can be run and timed on its own. 1 switches the Editor to Android (a full reimport the first time). 2 sets the
    /// project up for the headset: OpenXR for Android with the Meta Quest features, the Touch controller profiles and
    /// hand tracking; IL2CPP, ARM64, Vulkan, ASTC textures; a spike package id, so the real one is not used up.
    /// 3 builds Menu.unity and Cell.unity into Builds/Quest. Back to Windows switches the Editor back. XR is not
    /// started at launch: VrSwitch starts it, as on the PC, and always does on Android.
    /// </summary>
    [InitializeOnLoad]
    internal static class QuestBuild
    {
        static QuestBuild() => KeepDesktopPluginsOffAndroid();

        public const string ApkPath = "Builds/Quest/FactoryTwin-spike.apk";

        /// <summary>The spike's own id. The store id is decided separately and never changes after the first upload.</summary>
        public const string SpikePackageId = "com.twodudestudios.factorytwin.spike";

        // URDF Importer's Assimp natives are Windows, Linux and macOS libraries marked "any platform" in the package, so an
        // Android build tries to pack both Windows assimp.dll files and fails. Only the Editor's URDF import uses them.
        private const string DesktopOnlyNatives = "/UnityMeshImporter/Plugins/AssimpNet/Native/";

        private const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const int MinSdk = 32;

        // By type name, so this file needs no reference to the feature assemblies (some exist only with the Android module).
        private static readonly string[] Features =
        {
            "MetaQuestFeature", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "HandTracking",
        };

        [MenuItem("Tools/Factory Twin/Quest/1 Switch to Android")]
        private static void SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Debug.Log("[QuestBuild] Already on Android.");
                return;
            }
            double started = EditorApplication.timeSinceStartup;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log($"[QuestBuild] Switch to Android {(ok ? "done" : "FAILED")} in {EditorApplication.timeSinceStartup - started:F0} s.");
        }

        [MenuItem("Tools/Factory Twin/Quest/Back to Windows")]
        private static void BackToWindows()
        {
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            Debug.Log($"[QuestBuild] Switch to Windows {(ok ? "done" : "FAILED")}.");
        }

        [MenuItem("Tools/Factory Twin/Quest/2 Configure for Quest")]
        private static void Configure()
        {
            const BuildTargetGroup group = BuildTargetGroup.Android;

            // XR: the OpenXR loader for Android, not started at launch (VrSwitch starts it).
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                Debug.LogError("[QuestBuild] No XR settings yet: run Tools > Factory Twin > Configure XR first.");
                return;
            }
            if (!perTarget.HasManagerSettingsForBuildTarget(group))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            XRGeneralSettings settings = perTarget.SettingsForBuildTarget(group);
            settings.InitManagerOnStart = false;
            EditorUtility.SetDirty(settings);
            if (!settings.Manager.activeLoaders.Any(l => l != null && l.GetType().FullName == OpenXRLoaderType))
                XRPackageMetadataStore.AssignLoader(settings.Manager, OpenXRLoaderType, group);

            FeatureHelpers.RefreshFeatures(group);
            OpenXRSettings openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            var enabled = new List<string>();
            foreach (OpenXRFeature feature in openXr.GetFeatures())
            {
                if (feature != null && Features.Contains(feature.GetType().Name))
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                    enabled.Add(feature.GetType().Name);
                }
            }
            openXr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(openXr);

            // Player: what the Quest needs and the store asks for.
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinSdk;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetApplicationIdentifier(android, SpikePackageId);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            AssetDatabase.SaveAssets();

            string missing = string.Join(", ", Features.Except(enabled));
            Debug.Log($"[QuestBuild] Configured for Quest: OpenXR on Android with {string.Join(", ", enabled)}" +
                      $"{(missing.Length > 0 ? " (NOT FOUND: " + missing + ")" : string.Empty)}; IL2CPP, ARM64, Vulkan, ASTC, min SDK {MinSdk}, id {SpikePackageId}.");
        }

        /// <summary>
        /// Leaves the package's desktop natives out of Android builds, without editing the package (it is read-only):
        /// the include-in-build delegate is not saved, so it is set again on every domain load and before each build.
        /// </summary>
        private static int KeepDesktopPluginsOffAndroid()
        {
            int count = 0;
            foreach (PluginImporter plugin in PluginImporter.GetAllImporters())
            {
                if (plugin == null || !plugin.isNativePlugin || !plugin.assetPath.Contains(DesktopOnlyNatives))
                    continue;
                plugin.SetIncludeInBuildDelegate(_ => EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android);
                count++;
            }
            return count;
        }

        [MenuItem("Tools/Factory Twin/Quest/3 Build APK")]
        private static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("[QuestBuild] Switch to Android first (Tools > Factory Twin > Quest > 1 Switch to Android).");
                return;
            }
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ApkPath) ?? ".");
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            int filtered = KeepDesktopPluginsOffAndroid();
            Debug.Log($"[QuestBuild] Keeping {filtered} desktop-only native plugins out of the Android build.");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            var text = new System.Text.StringBuilder();
            text.Append($"[QuestBuild] Build {summary.result}: {summary.totalSize / (1024d * 1024d):F0} MB, {summary.totalTime.TotalMinutes:F1} min, " +
                        $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, scenes {string.Join(", ", scenes)} -> {summary.outputPath}");
            int shown = 0;
            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage message in step.messages)
                {
                    if (message.type == LogType.Error && shown++ < 20)
                        text.Append("\n  ERROR ").Append(step.name).Append(": ").Append(message.content);
                }
            }
            if (summary.result == BuildResult.Succeeded)
                Debug.Log(text.ToString());
            else
                Debug.LogError(text.ToString());
        }
    }
}
