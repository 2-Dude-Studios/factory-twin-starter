using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Build: after every standalone build, copies the third-party notices in Assets/_Project/Licenses into a
    /// Licenses folder next to the executable, so the licence texts travel with the binary (BSD-3, Apache-2.0,
    /// MIT and the CC BY attribution all ask for that).
    /// </summary>
    public sealed class LicenseFilesPostprocessor : IPostprocessBuildWithReport
    {
        private const string SourceFolder = "Assets/_Project/Licenses";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneWindows
                && target != BuildTarget.StandaloneLinux64 && target != BuildTarget.StandaloneOSX)
                return;

            string buildFolder = Path.GetDirectoryName(report.summary.outputPath);
            if (string.IsNullOrEmpty(buildFolder) || !Directory.Exists(SourceFolder))
            {
                Debug.LogWarning($"[LicenseFiles] Nothing copied: build folder '{buildFolder}', source '{SourceFolder}'.");
                return;
            }

            string destination = Path.Combine(buildFolder, "Licenses");
            Directory.CreateDirectory(destination);
            int copied = 0;
            foreach (string file in Directory.GetFiles(SourceFolder, "*.txt"))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
                copied++;
            }
            Debug.Log($"[LicenseFiles] Copied {copied} notice files to {destination}.");
        }
    }
}
