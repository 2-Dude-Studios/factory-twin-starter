using System.Linq;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): sets up the Quest 3 view over Link, repeatably. "Configure XR" puts OpenXR on the Windows
    /// (Standalone) target without starting it at launch, turns on the Quest controller profiles, and imports the
    /// XR Interaction Toolkit's Starter Assets sample. "Build VR Rig" places the sample's XR Origin in Cell.unity
    /// (inactive until VrSwitch turns it on), gives it a body for the safety zones, lets its rays hit the training
    /// points' trigger colliders, adds a teleport floor around the cell, and makes every training point usable with
    /// a controller.
    /// </summary>
    internal static class XrSetup
    {
        private const string XriPackage = "com.unity.xr.interaction.toolkit";
        private const string StarterAssets = "Starter Assets";
        private const string RigPrefabSuffix = "Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string SettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        private const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string VrPipelinePath = "Assets/_Project/Settings/VR_RPAsset.asset";
        private const string VrRendererPath = "Assets/_Project/Settings/VR_Renderer.asset";

        private static readonly Vector3 RigStart = new Vector3(-2.0f, 0f, -2.6f);
        private const float VrFarClip = 60f;

        [MenuItem("Tools/Factory Twin/Configure XR (OpenXR, off at start)")]
        private static void Configure()
        {
            const BuildTargetGroup target = BuildTargetGroup.Standalone;

            XRGeneralSettingsPerBuildTarget perTarget = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(target) != null
                ? FindPerTarget()
                : FindPerTarget() ?? CreatePerTarget();
            if (!perTarget.HasManagerSettingsForBuildTarget(target))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(target);

            XRGeneralSettings settings = perTarget.SettingsForBuildTarget(target);
            settings.InitManagerOnStart = false;
            EditorUtility.SetDirty(settings);
            if (!settings.Manager.activeLoaders.Any(l => l != null && l.GetType().FullName == OpenXRLoaderType))
                XRPackageMetadataStore.AssignLoader(settings.Manager, OpenXRLoaderType, target);

            OpenXRSettings openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(target);
            Enable<OculusTouchControllerProfile>(openXr);
            Enable<MetaQuestTouchPlusControllerProfile>(openXr);
            EditorUtility.SetDirty(openXr);
            AssetDatabase.SaveAssets();

            ImportStarterAssets();
            VrPipeline();
            Debug.Log("[XrSetup] OpenXR on Windows: loader assigned, not started at launch; Oculus Touch and Meta Quest Touch Plus profiles on.");
        }

        [MenuItem("Tools/Factory Twin/Build VR Rig")]
        private static void BuildRig()
        {
            string prefabPath = AssetDatabase.FindAssets("t:Prefab XR Origin (XR Rig)")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.Replace('\\', '/').EndsWith(RigPrefabSuffix));
            GameObject captureRig = GameObject.Find("CaptureRig");
            GameObject cell = GameObject.Find("Cell");
            GameObject trainee = GameObject.Find("CaptureRig/Trainee");
            GameObject hud = GameObject.Find("CaptureRig/TraineeHud");
            if (prefabPath == null || captureRig == null || cell == null || trainee == null)
            {
                Debug.LogError("[XrSetup] Run Configure XR first (it imports Starter Assets), and open Cell.unity with CaptureRig/Trainee built.");
                return;
            }

            Undo.SetCurrentGroupName("Build VR Rig");
            int undoGroup = Undo.GetCurrentGroup();

            Transform old = captureRig.transform.Find("XR Origin (XR Rig)");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), captureRig.transform);
            Undo.RegisterCreatedObjectUndo(rig, "Build VR Rig");
            rig.transform.SetPositionAndRotation(RigStart, Quaternion.identity);

            // The training points use trigger colliders; let every caster on the rig hit them.
            foreach (CurveInteractionCaster caster in rig.GetComponentsInChildren<CurveInteractionCaster>(true))
            {
                Undo.RecordObject(caster, "Build VR Rig");
                caster.raycastTriggerInteraction = QueryTriggerInteraction.Collide;
            }

            XROrigin origin = rig.GetComponent<XROrigin>();
            Body(rig.transform, origin.Camera.transform);

            // The headset camera draws the cell and its surroundings, not the far end of the hall, and no post.
            Camera eye = origin.Camera;
            Undo.RecordObject(eye, "Build VR Rig");
            eye.farClipPlane = VrFarClip;
            var eyeData = eye.GetComponent<UniversalAdditionalCameraData>();
            if (eyeData != null)
            {
                Undo.RecordObject(eyeData, "Build VR Rig");
                eyeData.renderPostProcessing = false;
                eyeData.renderShadows = false;
            }

            Camera desktopCamera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c.CompareTag("MainCamera") && !c.transform.IsChildOf(rig.transform));

            var vrSwitch = GetOrAdd<VrSwitch>(captureRig);
            var so = new SerializedObject(vrSwitch);
            so.FindProperty("xrRig").objectReferenceValue = rig;
            SerializedProperty desktopOnly = so.FindProperty("desktopOnly");
            desktopOnly.arraySize = hud != null ? 2 : 1;
            desktopOnly.GetArrayElementAtIndex(0).objectReferenceValue = trainee;
            if (hud != null)
                desktopOnly.GetArrayElementAtIndex(1).objectReferenceValue = hud;
            so.FindProperty("desktopCamera").objectReferenceValue = desktopCamera;
            so.FindProperty("desktopListener").objectReferenceValue = desktopCamera != null ? desktopCamera.GetComponent<AudioListener>() : null;
            so.FindProperty("vrPipeline").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(VrPipelinePath);
            so.ApplyModifiedProperties();

            TeleportFloor(cell.transform);
            int points = 0;
            foreach (TrainingPoint point in cell.GetComponentsInChildren<TrainingPoint>(true))
            {
                GetOrAdd<XRSimpleInteractable>(point.gameObject);
                GetOrAdd<XrTrainingPoint>(point.gameObject);
                points++;
            }

            rig.SetActive(false);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(cell.scene);
            Debug.Log($"[XrSetup] VR rig built (inactive until F11 or -vr); {points} training points usable by controller; desktop camera '{(desktopCamera != null ? desktopCamera.name : "none")}'.", rig);
        }

        // The pipeline VrSwitch uses while VR runs. The hall's Deferred+ asset (SSAO, decals, shadows on every extra
        // light, each point light rendering six shadow faces) ran at 15–40 fps in the headset, and main-light shadows
        // alone still cast from 1,300 objects. This one is Forward+ with no renderer features, no realtime shadows and
        // no extra realtime lights (the hall's baked lighting carries the look), no HDR or post, 2x MSAA, render scale
        // 0.9, and the GPU Resident Drawer: low enough to keep the PC cool over Link.
        private static void VrPipeline()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(VrRendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.renderingMode = RenderingMode.ForwardPlus;
                AssetDatabase.CreateAsset(renderer, VrRendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(VrPipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, VrPipelinePath);
            }
            var so = new SerializedObject(pipeline);
            so.FindProperty("m_MSAA").intValue = 2;
            so.FindProperty("m_RenderScale").floatValue = 0.9f;
            so.FindProperty("m_SupportsHDR").boolValue = false;
            so.FindProperty("m_MainLightShadowsSupported").boolValue = false;
            so.FindProperty("m_AdditionalLightsRenderingMode").intValue = 0;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            so.FindProperty("m_SoftShadowsSupported").boolValue = false;
            so.FindProperty("m_ShadowDistance").floatValue = 0f;
            so.FindProperty("m_RequireDepthTexture").boolValue = false;
            so.FindProperty("m_RequireOpaqueTexture").boolValue = false;
            // GPU Resident Drawer, as the hall's own pipeline uses: draw submission moves to the GPU, the CPU cost that
            // was left once shadows were gone. Meshes under 1% of the screen are culled.
            so.FindProperty("m_GPUResidentDrawerMode").intValue = 1;
            so.FindProperty("m_SmallMeshScreenPercentage").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        private static XRGeneralSettingsPerBuildTarget FindPerTarget()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget != null)
                return perTarget;
            string guid = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget").FirstOrDefault();
            return guid != null ? AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }

        private static XRGeneralSettingsPerBuildTarget CreatePerTarget()
        {
            var perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, SettingsPath);
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            AssetDatabase.SaveAssets();
            return perTarget;
        }

        private static void Enable<T>(OpenXRSettings settings) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            foreach (var feature in settings.GetFeatures<T>())
            {
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
        }

        private static void ImportStarterAssets()
        {
            Sample sample = Sample.FindByPackage(XriPackage, string.Empty).FirstOrDefault(s => s.displayName == StarterAssets);
            if (string.IsNullOrEmpty(sample.displayName))
            {
                Debug.LogError("[XrSetup] XR Interaction Toolkit's Starter Assets sample not found.");
                return;
            }
            if (!sample.isImported)
                sample.Import(Sample.ImportOptions.HideImportWindow);
        }

        private static void Body(Transform rig, Transform head)
        {
            var body = new GameObject("HeadBody");
            Undo.RegisterCreatedObjectUndo(body, "Build VR Rig");
            body.transform.SetParent(rig, false);
            body.layer = LayerMask.NameToLayer("Trainee");
            var rb = body.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.25f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            var headBody = body.AddComponent<HeadBody>();
            var so = new SerializedObject(headBody);
            so.FindProperty("head").objectReferenceValue = head;
            so.FindProperty("origin").objectReferenceValue = rig;
            so.ApplyModifiedProperties();
        }

        // A thin invisible floor around the cell that teleport rays land on.
        private static void TeleportFloor(Transform cell)
        {
            Transform old = cell.Find("Training/TeleportFloor");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var floor = new GameObject("TeleportFloor");
            Undo.RegisterCreatedObjectUndo(floor, "Build VR Rig");
            floor.transform.SetParent(cell.Find("Training"), false);
            floor.transform.position = new Vector3(-1.5f, -0.01f, 1.0f);
            var box = floor.AddComponent<BoxCollider>();
            box.size = new Vector3(13f, 0.02f, 12f);
            floor.AddComponent<TeleportationArea>();
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            // GetComponent returns a fake null in the Editor, so ?? cannot be used here.
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }
    }
}
