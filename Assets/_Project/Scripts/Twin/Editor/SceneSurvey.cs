using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Editor tooling: logs where the cell's objects sit (world bounds), for placing new props from the command line.</summary>
    internal static class SceneSurvey
    {
        private const string CellScene = "Assets/_Project/Scenes/Cell.unity";

        // unity -batchmode -projectPath . -executeMethod TwoDude.FactoryTwin.Twin.SceneSurvey.Run -logFile <file> -quit
        public static void Run()
        {
            EditorSceneManager.OpenScene(CellScene, OpenSceneMode.Single);
            var text = new StringBuilder("[SceneSurvey]\n");
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                Walk(root.transform, 0, root.name == "Cell" ? 3 : 1, text);
            Debug.Log(text.ToString());
        }

        private static void Walk(Transform t, int depth, int maxDepth, StringBuilder text)
        {
            text.Append(new string(' ', depth * 2)).Append(t.name)
                .Append(t.gameObject.activeInHierarchy ? "" : " (inactive)")
                .Append("  pos ").Append(t.position.ToString("F2"));
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers)
                    b.Encapsulate(r.bounds);
                text.Append("  bounds min ").Append(b.min.ToString("F2")).Append(" max ").Append(b.max.ToString("F2"));
            }
            text.Append('\n');
            if (depth >= maxDepth)
                return;
            foreach (Transform child in t)
                Walk(child, depth + 1, maxDepth, text);
        }
    }
}
