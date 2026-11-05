using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Fire safety (M14), dev tooling: a headless smoke check of the fire drill in Cell.unity. Enters Play, lets the
    /// line run, starts a scenario, logs the fire, alarm and line signals each second, renders a frame of the fire,
    /// and exits. Survives the domain reload on entering Play through SessionState.
    /// unity -batchmode -projectPath . -executeMethod TwoDude.FactoryTwin.Twin.FireDrillCheck.Run -logFile &lt;file&gt;
    /// (no -quit; it exits itself). Writes PNGs to Builds/FireDrillCheck.
    /// </summary>
    [InitializeOnLoad]
    internal static class FireDrillCheck
    {
        private const string CellScene = "Assets/_Project/Scenes/Cell.unity";
        private const string ActiveKey = "FireDrillCheck.Active";
        private const int Scenario = 4;
        private const double StartAt = 8;
        private const double EndAt = 52;
        private static readonly double[] ShotsAt = { 20, 34, 48 };
        private static readonly Vector3 ShotFrom = new Vector3(-5.5f, 2.2f, 5.2f);
        private static readonly Vector3 ShotAt = new Vector3(-1.55f, 1.8f, 3.4f);

        private static double _playStarted = -1;
        private static int _lastSecond = -1;
        private static bool _started;
        private static int _shot;

        static FireDrillCheck()
        {
            if (SessionState.GetBool(ActiveKey, false))
                EditorApplication.update += Tick;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(CellScene, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;
            CellContext context = Object.FindAnyObjectByType<CellContext>();
            if (context == null)
                return;
            if (_playStarted < 0)
                _playStarted = EditorApplication.timeSinceStartup;
            double t = EditorApplication.timeSinceStartup - _playStarted;
            SignalBus bus = context.Bus;

            if (!_started && t >= StartAt)
            {
                _started = true;
                Debug.Log($"[FireDrillCheck] t={t:F1} start F{Scenario}");
                bus.Publish(CellTopics.FireScenario, SignalValue.FromNumber(Scenario), CellContext.NowUnixMs);
            }
            if ((int)t != _lastSecond)
            {
                _lastSecond = (int)t;
                Debug.Log($"[FireDrillCheck] t={t:F1} stage={Read(bus, CellTopics.FireStage)} size={Read(bus, CellTopics.FireSize):F3} " +
                    $"smoke={Read(bus, CellTopics.FireSmoke):F3} smokeDet={Read(bus, CellTopics.FireSmokeDetected)} heatDet={Read(bus, CellTopics.FireHeatDetected)} " +
                    $"alarm={Read(bus, CellTopics.FireAlarm)} src={Read(bus, CellTopics.FireAlarmSource)} line={Read(bus, CellTopics.LineState)} " +
                    $"partsOut={Read(bus, CellTopics.LinePartsOut)} fps={1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f):F0}");
            }
            if (_shot < ShotsAt.Length && t >= ShotsAt[_shot])
            {
                LogEffects();
                Shot(_shot++);
            }
            if (t >= EndAt)
            {
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.update -= Tick;
                Debug.Log("[FireDrillCheck] done");
                EditorApplication.Exit(0);
            }
        }

        private static double Read(SignalBus bus, TopicPath topic) =>
            bus.TryGetLast(topic, out SignalMessage m) ? m.Value.AsNumber : double.NaN;

        private static void LogEffects()
        {
            GameObject anchor = GameObject.Find("Cell/Fire/Fire_Cnc");
            if (anchor == null)
                return;
            foreach (ParticleSystem ps in anchor.GetComponentsInChildren<ParticleSystem>(true))
            {
                Debug.Log($"[FireDrillCheck] fx {ps.name} playing={ps.isPlaying} emitting={ps.isEmitting} count={ps.particleCount} " +
                    $"rate={ps.emission.rateOverTimeMultiplier:F1} loop={ps.main.loop} pos={ps.transform.position:F2} fwd={ps.transform.forward:F2}");
            }
        }

        private static void Shot(int index)
        {
            var go = new GameObject("FireDrillCheckCamera");
            var cam = go.AddComponent<Camera>();
            go.transform.position = ShotFrom;
            go.transform.LookAt(ShotAt);
            cam.fieldOfView = 45f;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory("Builds/FireDrillCheck");
            string path = $"Builds/FireDrillCheck/shot{index}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log($"[FireDrillCheck] wrote {path}");
        }
    }
}
