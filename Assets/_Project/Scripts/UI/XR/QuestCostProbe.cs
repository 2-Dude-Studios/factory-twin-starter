using System;
using System.Collections;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone Quest (M26): a measuring run for the headset, started only with the -probe argument
    /// (adb shell am start ... -e unity "-cell -probe"). It stands the rig at each training area's start, then hides
    /// one kind of content at a time (decals, glass, walls, ceiling, shelving and pallets, the whole hall) and tries a
    /// lower render scale, no MSAA, no sun and no foveation, each for a fixed time, logging "[QuestProbe] ..." at every step. Read
    /// against the headset's own once-a-second frame line (logcat VrApi), it shows what each part costs.
    /// With -probe-fire instead it stands in the fire area and keeps the largest fire (scenario 4, past incipient,
    /// smoke and all) burning for ten minutes, relighting it when it burns out: the frame rate under the heaviest
    /// effects and the headset's temperature over a long drill.
    /// </summary>
    public sealed class QuestCostProbe : MonoBehaviour
    {
        public const string CommandLineFlag = "-probe";
        public const string FireFlag = "-probe-fire";

        [SerializeField] private Transform env;
        [SerializeField] private Light sun;
        [SerializeField] private XROrigin origin;
        [SerializeField] private TrainingAreaStart[] starts = Array.Empty<TrainingAreaStart>();
        [SerializeField] private CellContext context;
        [SerializeField, Min(1)] private int fireScenario = 4;
        [SerializeField, Min(10f)] private float fireSeconds = 600f;
        [SerializeField, Min(1f)] private float settleSeconds = 15f;
        [SerializeField, Min(1f)] private float stepSeconds = 10f;

        private static readonly string[] Shelving = { "shelf", "pallet", "trailer", "truck" };

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            _fire = Array.IndexOf(args, FireFlag) >= 0;
            if (!_fire && Array.IndexOf(args, CommandLineFlag) < 0)
                enabled = false;
        }

        private bool _fire;

        private void Start()
        {
            if (enabled)
                StartCoroutine(_fire ? FireRun() : Run());
        }

        private IEnumerator FireRun()
        {
            Debug.Log($"[QuestProbe] fire run start; settling {settleSeconds:F0} s");
            yield return new WaitForSeconds(settleSeconds);
            if (context == null)
            {
                Debug.LogError("[QuestProbe] fire run needs the Cell context.");
                yield break;
            }
            foreach (TrainingAreaStart start in starts)
            {
                if (start != null && origin != null && start.Area == TrainingArea.Fire)
                    RigTeleport.GoTo(origin, start.FloorPosition, start.YawDegrees);
            }

            SignalBus bus = context.Bus;
            float end = Time.realtimeSinceStartup + fireSeconds;
            FireStage last = FireStage.None;
            int lights = 0, minute = -1;
            while (Time.realtimeSinceStartup < end)
            {
                FireStage stage = bus.TryGetLast(CellTopics.FireStage, out SignalMessage m) ? (FireStage)(int)m.Value.AsNumber : FireStage.None;
                if (stage == FireStage.None || stage == FireStage.Out)
                {
                    bus.Publish(CellTopics.FireScenario, SignalValue.FromNumber(0d), CellContext.NowUnixMs);
                    bus.Publish(CellTopics.FireScenario, SignalValue.FromNumber(fireScenario), CellContext.NowUnixMs);
                    lights++;
                    Debug.Log($"[QuestProbe] fire {fireScenario} lit ({lights})");
                    last = FireStage.None;
                    yield return new WaitForSeconds(3f);
                    continue;
                }
                if (stage != last)
                {
                    Debug.Log($"[QuestProbe] fire stage {stage}");
                    last = stage;
                }
                int now = (int)((fireSeconds - (end - Time.realtimeSinceStartup)) / 60f);
                if (now != minute)
                {
                    minute = now;
                    Debug.Log($"[QuestProbe] fire minute {minute}");
                }
                yield return new WaitForSeconds(1f);
            }
            bus.Publish(CellTopics.FireScenario, SignalValue.FromNumber(0d), CellContext.NowUnixMs);
            Debug.Log("[QuestProbe] done");
        }

        private IEnumerator Run()
        {
            Debug.Log($"[QuestProbe] start; settling {settleSeconds:F0} s");
            yield return new WaitForSeconds(settleSeconds);
            foreach (Camera cam in Camera.allCameras)
                Debug.Log($"[QuestProbe] camera {cam.name} enabled, depth {cam.depth}, far {cam.farClipPlane:F0}");

            TrainingAreaStart robot = null;
            foreach (TrainingAreaStart start in starts)
            {
                if (start == null || origin == null)
                    continue;
                RigTeleport.GoTo(origin, start.FloorPosition, start.YawDegrees);
                if (start.Area == TrainingArea.RobotCell)
                    robot = start;
                Debug.Log($"[QuestProbe] area {start.Area}");
                yield return new WaitForSeconds(stepSeconds);
            }
            if (robot != null)
                RigTeleport.GoTo(origin, robot.FloorPosition, robot.YawDegrees);

            List<Renderer> all = env != null ? new List<Renderer>(env.GetComponentsInChildren<Renderer>(false)) : new List<Renderer>();
            yield return Hide("baseline", new List<Renderer>());
            yield return Hide("no decals", ByShader(all, "decal"));
            yield return Hide("no glass", ByShader(all, "glass"));
            yield return Hide("no walls", Under("walls"));
            yield return Hide("no ceiling", Under("ceiling"));
            yield return Hide("no shelving and pallets", ByRootName());
            yield return Hide("no hall at all", all);

            // URP takes the render scale and MSAA from its pipeline asset, not from XRSettings.
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                float scale = urp.renderScale;
                urp.renderScale = 0.7f;
                Debug.Log($"[QuestProbe] step render scale 0.7 (was {scale:F2})");
                yield return new WaitForSeconds(stepSeconds);
                urp.renderScale = scale;

                int msaa = urp.msaaSampleCount;
                urp.msaaSampleCount = 1;
                Debug.Log($"[QuestProbe] step MSAA off (was {msaa}x)");
                yield return new WaitForSeconds(stepSeconds);
                urp.msaaSampleCount = msaa;
            }

            if (sun != null)
            {
                sun.enabled = false;
                Debug.Log("[QuestProbe] step sun off");
                yield return new WaitForSeconds(stepSeconds);
                sun.enabled = true;
            }

            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (XRDisplaySubsystem d in displays)
            {
                float level = d.foveatedRenderingLevel;
                d.foveatedRenderingLevel = 0f;
                Debug.Log($"[QuestProbe] step foveation off (was {level:F1})");
                yield return new WaitForSeconds(stepSeconds);
                d.foveatedRenderingLevel = level;
            }
            Debug.Log("[QuestProbe] done");
        }

        private IEnumerator Hide(string label, List<Renderer> renderers)
        {
            var was = new List<bool>(renderers.Count);
            foreach (Renderer r in renderers)
            {
                was.Add(r.enabled);
                r.enabled = false;
            }
            Debug.Log($"[QuestProbe] step {label} ({renderers.Count} renderers hidden)");
            yield return new WaitForSeconds(stepSeconds);
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = was[i];
            }
        }

        private static List<Renderer> ByShader(List<Renderer> all, string part)
        {
            var found = new List<Renderer>();
            foreach (Renderer r in all)
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m != null && m.shader.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found.Add(r);
                        break;
                    }
                }
            }
            return found;
        }

        private List<Renderer> Under(string child)
        {
            Transform t = env != null ? env.Find(child) : null;
            return t != null ? new List<Renderer>(t.GetComponentsInChildren<Renderer>(false)) : new List<Renderer>();
        }

        private List<Renderer> ByRootName()
        {
            var found = new List<Renderer>();
            if (env == null)
                return found;
            foreach (Transform child in env)
            {
                foreach (string word in Shelving)
                {
                    if (child.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found.AddRange(child.GetComponentsInChildren<Renderer>(false));
                        break;
                    }
                }
            }
            return found;
        }
    }
}
