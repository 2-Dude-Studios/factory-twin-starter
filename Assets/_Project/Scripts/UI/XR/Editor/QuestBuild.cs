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
    /// started at launch: VrSwitch starts it, as on the PC, and always does on Android. With the Quest variant built
    /// (Build Quest Variant, M26) the APK holds Menu.unity and CellQuest.unity; the player loads CellQuest on Android.
    /// </summary>
    [InitializeOnLoad]
    internal static class QuestBuild
    {
        static QuestBuild() => KeepDesktopPluginsOffAndroid();

        public const string ApkPath = "Builds/Quest/FactoryVRSafetyTraining.apk";
        public const string ReleaseApkPath = "Builds/Quest/FactoryVRSafetyTraining-release.apk";

        /// <summary>The spike's own id (M25 to M27), kept here so its old install can be found and removed.</summary>
        public const string SpikePackageId = "com.twodudestudios.factorytwin.spike";

        // The store identity (M28, decided 2026-11-13: free, Quest 3 and 3S). The package id never changes after the
        // first upload to the Meta Horizon Store. The name and company are applied to Android builds only and put back
        // after each build, so the desktop build's name and its data folder stay as they are.
        public const string StorePackageId = "com.twodudestudios.factorytwin";
        public const string StoreProductName = "Factory VR Safety Training";
        public const string StoreCompanyName = "2 Dude Studios";

        // The desktop build's names: its data folder (records, badge book) is under these, so they never change.
        public const string DesktopProductName = "factory-twin-starter";
        public const string DesktopCompanyName = "DefaultCompany";

        [MenuItem("Tools/Factory Twin/Quest/Restore Desktop Name")]
        private static void RestoreDesktopName()
        {
            PlayerSettings.productName = DesktopProductName;
            PlayerSettings.companyName = DesktopCompanyName;
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestBuild] Desktop name restored: {DesktopCompanyName} / {DesktopProductName}.");
        }

        // The version: bump it before every upload; the code must only ever go up (major*10000 + minor*100 + patch).
        public const int VersionMajor = 0, VersionMinor = 1, VersionPatch = 0;
        public static string VersionName => $"{VersionMajor}.{VersionMinor}.{VersionPatch}";
        public static int VersionCode => VersionMajor * 10000 + VersionMinor * 100 + VersionPatch;

        // Quest 3 (eureka) and Quest 3S only, as decided; the Meta Quest feature's names for them.
        private static readonly string[] TargetDevices = { "eureka", "quest3s" };

        // The release keystore lives outside the repo and its passwords are never written down here: the release build
        // reads them from these environment variables, set by you before starting Unity.
        private const string KeystoreVar = "FT_KEYSTORE", KeystorePassVar = "FT_KEYSTORE_PASS", KeyAliasVar = "FT_KEY_ALIAS", KeyPassVar = "FT_KEY_PASS";

        // URDF Importer's Assimp natives are Windows, Linux and macOS libraries marked "any platform" in the package, so an
        // Android build tries to pack both Windows assimp.dll files and fails. Only the Editor's URDF import uses them.
        private const string DesktopOnlyNatives = "/UnityMeshImporter/Plugins/AssimpNet/Native/";

        private const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const int MinSdk = 32, TargetSdk = 34;

        // By type name, so this file needs no reference to the feature assemblies (some exist only with the Android module).
        private static readonly string[] Features =
        {
            "MetaQuestFeature", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "HandTracking",
            "FoveatedRenderingFeature",
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
                    if (feature.GetType().Name == "MetaQuestFeature")
                        MetaQuestStoreSettings(feature);
                    if (feature.GetType().Name == "FoveatedRenderingFeature")
                    {
                        // Quest 3 has no eye tracking: fixed foveation, and no eye-tracking permission asked for.
                        var so = new SerializedObject(feature);
                        SerializedProperty eyes = so.FindProperty("useEyeTracking");
                        if (eyes != null)
                            eyes.boolValue = false;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
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
            // Meta's release manifest rules: immersive apps target 34 exactly (32–34 allowed, 34 for apps made since
            // March 2026; "highest installed" gave 37), and install location auto (the project had prefer-external).
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetApplicationIdentifier(android, StorePackageId);
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            AssetDatabase.SaveAssets();

            string missing = string.Join(", ", Features.Except(enabled));
            Debug.Log($"[QuestBuild] Configured for Quest: OpenXR on Android with {string.Join(", ", enabled)}" +
                      $"{(missing.Length > 0 ? " (NOT FOUND: " + missing + ")" : string.Empty)}; IL2CPP, ARM64, Vulkan, ASTC, min SDK {MinSdk}, target SDK {TargetSdk}, install location auto, id {StorePackageId} {VersionName} ({VersionCode}), Quest 3 and 3S, no internet permission.");
        }

        // The Meta Quest feature's store settings: only the headsets decided on, and no internet permission (the
        // standalone app has no network use; Unity would otherwise add it for the MQTT and Modbus code it links).
        private static void MetaQuestStoreSettings(OpenXRFeature feature)
        {
            var so = new SerializedObject(feature);
            SerializedProperty devices = so.FindProperty("targetDevices");
            for (int i = 0; devices != null && i < devices.arraySize; i++)
            {
                SerializedProperty device = devices.GetArrayElementAtIndex(i);
                device.FindPropertyRelative("enabled").boolValue = TargetDevices.Contains(device.FindPropertyRelative("manifestName").stringValue);
            }
            SerializedProperty internet = so.FindProperty("forceRemoveInternetPermission");
            if (internet != null)
                internet.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
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
        private static void Build() => Build(release: false);

        /// <summary>
        /// The store build: release-signed with your keystore (path and passwords from FT_KEYSTORE, FT_KEYSTORE_PASS,
        /// FT_KEY_ALIAS, FT_KEY_PASS), to Builds/Quest/FactoryVRSafetyTraining-release.apk. Nothing about the key is saved.
        /// </summary>
        [MenuItem("Tools/Factory Twin/Quest/4 Build Release APK")]
        private static void BuildRelease() => Build(release: true);

        private static void Build(bool release)
        {
            string keystore = System.Environment.GetEnvironmentVariable(KeystoreVar);
            if (release && (string.IsNullOrEmpty(keystore) || !System.IO.File.Exists(keystore) || string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(KeyAliasVar))))
            {
                Debug.LogError($"[QuestBuild] Release build: set {KeystoreVar} (the keystore file, outside the repo), {KeystorePassVar}, {KeyAliasVar} and {KeyPassVar} before starting Unity.");
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("[QuestBuild] Switch to Android first (Tools > Factory Twin > Quest > 1 Switch to Android).");
                return;
            }
            // The headset gets the Quest variant of the cell only (M26), rebuilt from the saved Cell.unity first so it is
            // never stale (it is derived, and kept out of git): its menu is the hub inside the cell (M28), no Menu.unity.
            QuestVariantSetup.Build();
            string[] scenes = AssetDatabase.LoadAssetAtPath<SceneAsset>(QuestVariantSetup.QuestScene) != null
                ? new[] { QuestVariantSetup.QuestScene }
                : EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            string apk = release ? ReleaseApkPath : ApkPath;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(apk) ?? ".");
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            int filtered = KeepDesktopPluginsOffAndroid();
            Debug.Log($"[QuestBuild] Keeping {filtered} desktop-only native plugins out of the Android build.");
            // The store name and version for this build only; the desktop's name, company and signing come back after.
            bool customKeystore = PlayerSettings.Android.useCustomKeystore;
            string keystoreName = PlayerSettings.Android.keystoreName, aliasName = PlayerSettings.Android.keyaliasName;
            BuildReport report;
            try
            {
                PlayerSettings.productName = StoreProductName;
                PlayerSettings.companyName = StoreCompanyName;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, StorePackageId);
                PlayerSettings.Android.bundleVersionCode = VersionCode;
                PlayerSettings.bundleVersion = VersionName;
                if (release)
                {
                    PlayerSettings.Android.useCustomKeystore = true;
                    PlayerSettings.Android.keystoreName = keystore;
                    PlayerSettings.Android.keystorePass = System.Environment.GetEnvironmentVariable(KeystorePassVar);
                    PlayerSettings.Android.keyaliasName = System.Environment.GetEnvironmentVariable(KeyAliasVar);
                    PlayerSettings.Android.keyaliasPass = System.Environment.GetEnvironmentVariable(KeyPassVar);
                }
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                // Always back to the desktop's own names (not whatever was set before: that could be a store name left
                // on disk by an earlier build), so the desktop build and its data folder never move.
                PlayerSettings.productName = DesktopProductName;
                PlayerSettings.companyName = DesktopCompanyName;
                PlayerSettings.Android.useCustomKeystore = customKeystore;
                PlayerSettings.Android.keystoreName = keystoreName;
                PlayerSettings.Android.keyaliasName = aliasName;
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
                // The build writes ProjectSettings to disk with the store names; save the restored ones too, or an
                // Editor restart brings the store names back as the "desktop" ones (seen 2026-11-13).
                AssetDatabase.SaveAssets();
            }
            BuildSummary summary = report.summary;
            var text = new System.Text.StringBuilder();
            text.Append($"[QuestBuild] Build{(release ? " (release)" : string.Empty)} {summary.result}: {StorePackageId} {VersionName} ({VersionCode}), {summary.totalSize / (1024d * 1024d):F0} MB, {summary.totalTime.TotalMinutes:F1} min, " +
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
