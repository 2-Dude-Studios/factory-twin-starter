using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Store readiness (M29): the manifest entries Meta's release check asks for that Unity's player settings cannot
    /// set, written into the generated Gradle project before every Android build. The launch activity gets
    /// android:excludeFromRecents="true" (Meta's "Application Manifests for Release Builds"); without the Meta XR Core
    /// SDK nothing else adds it. Every Android build of this project is the Quest app, so it runs on all of them.
    /// </summary>
    internal sealed class QuestManifest : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 100;

        // path is the unityLibrary module; the launcher module's manifest sits beside it.
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            int changed = 0;
            foreach (string manifest in new[]
                     {
                         Path.Combine(path, "src", "main", "AndroidManifest.xml"),
                         Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"),
                     })
            {
                if (File.Exists(manifest))
                    changed += ExcludeLauncherFromRecents(manifest);
            }
            Debug.Log(changed > 0
                ? $"[QuestManifest] excludeFromRecents set on the launch activity ({changed})."
                : "[QuestManifest] No launch activity found in the generated manifests: excludeFromRecents NOT set.");
        }

        private static int ExcludeLauncherFromRecents(string file)
        {
            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.Load(file);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("android", AndroidNs);
            int changed = 0;
            XmlNodeList activities = doc.SelectNodes("/manifest/application/activity[intent-filter/action[@android:name='android.intent.action.MAIN']]", ns);
            if (activities == null)
                return 0;
            foreach (XmlElement activity in activities)
            {
                if (activity.GetAttribute("excludeFromRecents", AndroidNs) == "true")
                    continue;
                activity.SetAttribute("excludeFromRecents", AndroidNs, "true");
                changed++;
            }
            if (changed > 0)
                doc.Save(file);
            return changed;
        }
    }
}
