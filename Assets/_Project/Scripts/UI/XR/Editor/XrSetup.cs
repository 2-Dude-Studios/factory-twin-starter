using System.Collections.Generic;
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
using UnityEngine.UIElements;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Gaze;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
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
    /// points' trigger colliders, adds a teleport floor around the cell, makes every training point usable with a
    /// controller, and builds the world-space panels: the training board above the control station, the HUD that
    /// follows the view, and the selection panel on the left controller.
    /// </summary>
    internal static class XrSetup
    {
        private const string XriPackage = "com.unity.xr.interaction.toolkit";
        private const string StarterAssets = "Starter Assets";
        private const string HandsPackage = "com.unity.xr.hands";
        private const string HandVisualizer = "HandVisualizer";
        private const string HandMaterialPath = "Assets/_Project/Art/Hands/VrHand.mat";
        private const string UiLayer = "UI";
        private const string RigPrefabSuffix = "Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string SettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        private const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string VrPipelinePath = "Assets/_Project/Settings/VR_RPAsset.asset";
        private const string VrRendererPath = "Assets/_Project/Settings/VR_Renderer.asset";

        private static readonly Vector3 RigStart = new Vector3(-2.0f, 0f, -2.6f);
        private const float VrFarClip = 60f;

        // World-space UI Toolkit panels (M12). 100 panel pixels are one unit: a 900 px board at scale 0.1 is 0.9 m.
        private const string WorldPanelPath = "Assets/_Project/UI/Vr/VrWorldPanel.asset";
        private const string ThemePath = "Assets/_Project/UI/DashboardTheme.tss";
        private const string BoardUxml = "Assets/_Project/UI/Vr/TrainingBoard.uxml";
        private const string MenuUxml = "Assets/_Project/UI/Vr/VrMenu.uxml";
        private const string TabletName = "VrTablet";
        private const string TabletControllerMount = "Tablet Mount";

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
            // Hand joints (XR_EXT_hand_tracking) for the tracked hands; over Link it needs the Link app's developer
            // runtime features on. The controllers work as before without it.
            Enable<HandTracking>(openXr);
            EditorUtility.SetDirty(openXr);
            AssetDatabase.SaveAssets();

            ImportSample(XriPackage, StarterAssets);
            ImportSample(HandsPackage, HandVisualizer);
            VrPipeline();
            Debug.Log("[XrSetup] OpenXR on Windows: loader assigned, not started at launch; Oculus Touch and Meta Quest Touch Plus profiles and hand tracking on.");
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
                // Rays skip the trainee's own body, the safety-zone volumes and the lockout sockets' trigger spheres
                // (Ignore Raycast), so none of them blocks a ray from the thing behind it.
                caster.raycastMask = ~LayerMask.GetMask("Ignore Raycast", "Trainee", "SafetyZone");
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

            PanelSettings worldPanel = WorldPanel();
            CellContext context = cell.GetComponent<CellContext>();
            Undo.AddComponent<XRUIToolkitManager>(rig);
            // realvirtual's EventSystem would otherwise feed every UI Toolkit panel (AutoSwitch), and XRI's rays and
            // pokes would never reach the world panels. On the rig, so it only applies while VR runs.
            var panelInput = Undo.AddComponent<PanelInputConfiguration>(rig);
            panelInput.panelInputRedirection = PanelInputConfiguration.PanelInputRedirection.Never;
            // The trainee is in the headset; another window on the PC may hold the focus, which UI Toolkit would
            // otherwise take as a reason to drop every press.
            Undo.AddComponent<VrBackgroundInput>(rig);
            Board(cell.transform, worldPanel, context);
            VrHud vrHud = Tablet(origin, vrSwitch, worldPanel, context, cell.transform);

            LotoProps.Build(cell.transform, vrHud, context);
            int points = 0;
            foreach (TrainingPoint point in cell.GetComponentsInChildren<TrainingPoint>(true))
            {
                // Isolator, hasp, valve and tool change are used hands-on through their props (LotoProps), not by a
                // ray and a trigger on the point.
                if (System.Array.IndexOf(LotoProps.Physical, point.Kind) >= 0)
                {
                    foreach (Component clickTarget in new Component[] { point.GetComponent<XrTrainingPoint>(), point.GetComponent<XRSimpleInteractable>() })
                    {
                        if (clickTarget != null)
                            Undo.DestroyObjectImmediate(clickTarget);
                    }
                    continue;
                }
                // The point's collider is a trigger, which XRI leaves out when it collects colliders itself, so it is
                // listed by hand; without it no ray can hover the point.
                var interactable = GetOrAdd<XRSimpleInteractable>(point.gameObject);
                interactable.colliders.Clear();
                interactable.colliders.Add(point.GetComponent<Collider>());
                EditorUtility.SetDirty(interactable);
                if (point.Kind == TrainingPointKind.StartButton || point.Kind == TrainingPointKind.StopButton
                    || point.Kind == TrainingPointKind.NotifyButton || point.Kind == TrainingPointKind.AllClearButton)
                    PokeButton(point, interactable);
                var xrPoint = GetOrAdd<XrTrainingPoint>(point.gameObject);
                var pointSo = new SerializedObject(xrPoint);
                pointSo.FindProperty("hud").objectReferenceValue = vrHud;
                pointSo.ApplyModifiedProperties();
                points++;
            }

            OnePointer(rig);
            Hands(rig, origin);
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

        private static void ImportSample(string package, string displayName)
        {
            Sample sample = Sample.FindByPackage(package, string.Empty).FirstOrDefault(s => s.displayName == displayName);
            if (string.IsNullOrEmpty(sample.displayName))
            {
                Debug.LogError($"[XrSetup] {package}'s {displayName} sample not found.");
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

        // The panel settings every headset panel shares: world space, a trigger collider the controller rays hit.
        private static PanelSettings WorldPanel()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(WorldPanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, WorldPanelPath);
            }
            panel.renderMode = PanelRenderMode.WorldSpace;
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            var so = new SerializedObject(panel);
            // Keep existing colliders (the internal ColliderUpdateMode: 0 MatchBoundingBox, 1 Keep, 2 MatchDocumentRect).
            // Panels that take input carry their own collider (Pressable); the HUD has none, so it never blocks a ray.
            so.FindProperty("m_ColliderUpdateMode").intValue = 1;
            so.FindProperty("m_ColliderIsTrigger").boolValue = true;
            so.FindProperty("m_PixelsPerUnit").floatValue = 100f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            return panel;
        }

        private static UIDocument Document(GameObject go, PanelSettings panel, string uxmlPath, float width, float height, float scale)
        {
            var document = GetOrAdd<UIDocument>(go);
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            var so = new SerializedObject(document);
            so.FindProperty("m_WorldSpaceSizeMode").intValue = 1;
            so.FindProperty("m_WorldSpaceWidth").floatValue = width;
            so.FindProperty("m_WorldSpaceHeight").floatValue = height;
            so.ApplyModifiedProperties();
            go.transform.localScale = Vector3.one * scale;
            return document;
        }

        // The lockout board above the line control station, readable on the desktop as well as in the headset.
        private static void Board(Transform cell, PanelSettings panel, CellContext context)
        {
            Transform training = cell.Find("Training");
            Transform station = training.Find("Points/ControlStation_Panel");
            Transform old = training.Find("TrainingBoard");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var go = new GameObject("TrainingBoard");
            Undo.RegisterCreatedObjectUndo(go, "Build VR Rig");
            go.transform.SetParent(training, false);
            go.transform.position = (station != null ? station.position : new Vector3(1.8f, 1.1f, 2.0f)) + new Vector3(0f, 0.68f, 0.02f);
            go.transform.rotation = Quaternion.identity;
            UIDocument document = Document(go, panel, BoardUxml, 900f, 600f, 0.1f);
            Pressable(go, 900f, 600f);
            Backing(go, 900f, 600f, 12f, 0.02f);
            var board = go.AddComponent<TrainingBoard>();
            var so = new SerializedObject(board);
            so.FindProperty("document").objectReferenceValue = document;
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("training").objectReferenceValue = Object.FindAnyObjectByType<TrainingController>();
            so.ApplyModifiedProperties();
        }

        // The tablet held in the left hand, tilted toward the eyes (24 x 18 cm): its screen shows the training display
        // (VrHud: the task at hand, where, the next one, status, the prompt) beside the options (VrMenu), which the
        // right controller's ray or a fingertip presses. It replaces the HUD that floated in the view.
        private static VrHud Tablet(XROrigin origin, VrSwitch vrSwitch, PanelSettings panel, CellContext context, Transform cell)
        {
            Transform left = origin.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Left Controller");
            if (left == null)
            {
                Debug.LogWarning("[XrSetup] No Left Controller on the rig; tablet skipped.");
                return null;
            }
            var mount = new GameObject(TabletControllerMount);
            Undo.RegisterCreatedObjectUndo(mount, "Build VR Rig");
            mount.transform.SetParent(left, false);
            mount.transform.localPosition = new Vector3(0f, 0.12f, 0.08f);
            mount.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            // On the UI layer, which the hands' near grab leaves out: the tablet is always within reach of a hand.
            var go = new GameObject(TabletName) { layer = LayerMask.NameToLayer(UiLayer) };
            go.transform.SetParent(mount.transform, false);
            UIDocument document = Document(go, panel, MenuUxml, 800f, 600f, 0.03f);
            Pressable(go, 800f, 600f);
            Backing(go, 800f, 600f, 8f, 0.012f);
            var hud = go.AddComponent<VrHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("document").objectReferenceValue = document;
            hudSo.FindProperty("context").objectReferenceValue = context;
            hudSo.ApplyModifiedProperties();
            var menu = go.AddComponent<VrMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("document").objectReferenceValue = document;
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("vrSwitch").objectReferenceValue = vrSwitch;
            so.FindProperty("origin").objectReferenceValue = origin;

            // Teleport spots from where the training points were built: the cell's start, in front of the control
            // station, beside the CNC's isolator.
            Transform station = cell.Find("Training/Points/ControlStation_Panel");
            Transform isolator = cell.Find("Training/Points/Isolator");
            var spots = new (string button, Vector3 floor, float yaw)[]
            {
                ("menu-go-cell", RigStart, 0f),
                ("menu-go-station", station != null ? new Vector3(station.position.x, 0f, station.position.z - 0.8f) : RigStart, 0f),
                ("menu-go-cnc", isolator != null ? new Vector3(isolator.position.x + 0.8f, 0f, isolator.position.z) : RigStart, -90f),
            };
            SerializedProperty list = so.FindProperty("spots");
            list.arraySize = spots.Length;
            for (int i = 0; i < spots.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("button").stringValue = spots[i].button;
                entry.FindPropertyRelative("floorPosition").vector3Value = spots[i].floor;
                entry.FindPropertyRelative("yawDegrees").floatValue = spots[i].yaw;
            }
            so.ApplyModifiedProperties();
            return hud;
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

            // The hall's own floor is on realvirtual's rvTransport layer, which the rig's CharacterController
            // (Ignore Raycast) does not collide with, so walking off the teleport floor fell through. A metre-deep
            // box under the whole hall, top at the floor, on Default, holds the rig wherever it walks.
            Transform oldWalk = cell.Find("Training/WalkFloor");
            if (oldWalk != null)
                Undo.DestroyObjectImmediate(oldWalk.gameObject);
            var walk = new GameObject("WalkFloor");
            Undo.RegisterCreatedObjectUndo(walk, "Build VR Rig");
            walk.transform.SetParent(cell.Find("Training"), false);
            walk.transform.position = new Vector3(-5f, -0.5f, 0f);
            var walkBox = walk.AddComponent<BoxCollider>();
            walkBox.size = new Vector3(110f, 1f, 110f);
        }

        // One pointer: the right controller's ray. The starter rig's gaze interactor falls back to the head without eye
        // tracking, so it hovered and pressed whatever the trainee looked at, and the left hand holds the selection
        // panel, so its ray only got in the way. Both hands keep teleport and poke.
        private static void OnePointer(GameObject rig)
        {
            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Gaze Interactor")
                {
                    Undo.RecordObject(t.gameObject, "Build VR Rig");
                    t.gameObject.SetActive(false);
                }
                // The left hand grabs what it touches (the padlock, a tool) but casts no ray and presses no UI.
                if (t.name == "Near-Far Interactor" && t.parent != null && t.parent.name == "Left Controller")
                {
                    var nearFar = t.GetComponent<NearFarInteractor>();
                    Undo.RecordObject(nearFar, "Build VR Rig");
                    nearFar.enableFarCasting = false;
                    nearFar.enableUIInteraction = false;
                    foreach (LineRenderer line in t.GetComponentsInChildren<LineRenderer>(true))
                    {
                        Undo.RecordObject(line, "Build VR Rig");
                        line.enabled = false;
                    }
                }
            }
            // No near grab reaches the tablet (UI layer) or a body: the left hand holds the tablet, and the right
            // hand's grab would otherwise take it when it comes close to press a button.
            foreach (SphereInteractionCaster near in rig.GetComponentsInChildren<SphereInteractionCaster>(true))
            {
                var nearSo = new SerializedObject(near);
                nearSo.FindProperty("m_PhysicsLayerMask").intValue = ~LayerMask.GetMask(UiLayer, "Ignore Raycast", "Trainee", "SafetyZone");
                nearSo.ApplyModifiedProperties();
            }
            var gazeAssistance = rig.GetComponent<XRGazeAssistance>();
            if (gazeAssistance != null)
            {
                Undo.RecordObject(gazeAssistance, "Build VR Rig");
                gazeAssistance.enabled = false;
            }
        }

        // Hands (M12): on each controller a hand in place of the controller model, curled by the grip and trigger and
        // closed round whatever it holds (ControllerHand); and a tracked hand for when the controllers are put down
        // (hand tracking over Link), driven joint by joint by XR Hands, with a fingertip poke and a fist-or-pinch
        // grab. XRInputModalityManager shows one pair or the other. Both use the XR Hands sample's hand model; a glove
        // model with the same L_/R_ joint names can replace it.
        private static void Hands(GameObject rig, XROrigin origin)
        {
            Material material = HandMaterial();
            var modality = rig.GetComponentInChildren<XRInputModalityManager>(true);
            Transform cameraOffset = origin.CameraFloorOffsetObject.transform;
            Transform tabletHandMount = null;
            foreach (bool left in new[] { true, false })
            {
                string side = left ? "Left" : "Right";
                GameObject model = HandModel(left);
                Transform controller = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == side + " Controller");
                if (model == null || controller == null)
                {
                    Debug.LogWarning($"[XrSetup] No {side} hand model (run Configure XR to import the XR Hands sample) or no {side} Controller; hands skipped.");
                    return;
                }
                string prefix = left ? "L_" : "R_";

                // The controller model gives way to the hand.
                Transform visual = controller.Find(side + " Controller Visual");
                if (visual != null)
                {
                    Undo.RecordObject(visual.gameObject, "Build VR Rig");
                    visual.gameObject.SetActive(false);
                }
                var nearFar = controller.GetComponentInChildren<NearFarInteractor>(true);
                GameObject held = InstantiateHand(model, controller, side + " Hand (Controller)", material);
                var hand = held.AddComponent<ControllerHand>();
                var handSo = new SerializedObject(hand);
                handSo.FindProperty("leftHand").boolValue = left;
                handSo.FindProperty("interactor").objectReferenceValue = nearFar;
                handSo.FindProperty("controller").objectReferenceValue = controller;
                handSo.ApplyModifiedPropertiesWithoutUndo();
                // The controller's poke follows the index fingertip, so buttons are pressed with the finger drawn.
                var poke = controller.GetComponentInChildren<XRPokeInteractor>(true);
                Transform heldTip = FindDeep(held.transform, prefix + "IndexTip");
                if (poke != null && heldTip != null)
                {
                    Undo.RecordObject(poke, "Build VR Rig");
                    poke.attachTransform = heldTip;
                }

                // The tracked hand, under the camera offset where XR Hands puts its joints.
                GameObject tracked = InstantiateHand(model, cameraOffset, side + " Hand (Tracked)", material);
                var events = tracked.AddComponent<XRHandTrackingEvents>();
                events.handedness = left ? Handedness.Left : Handedness.Right;
                events.updateType = XRHandTrackingEvents.UpdateTypes.Dynamic | XRHandTrackingEvents.UpdateTypes.BeforeRender;
                var driver = tracked.AddComponent<XRHandSkeletonDriver>();
                driver.handTrackingEvents = events;
                driver.rootTransform = FindDeep(tracked.transform, prefix + "Wrist");
                // Added from code the list is not created yet; FindJointsFromRoot fills it.
                driver.jointTransformReferences = new List<JointToTransformReference>();
                var missing = new List<string>();
                driver.FindJointsFromRoot(missing);
                if (missing.Count > 0)
                    Debug.LogWarning($"[XrSetup] {side} tracked hand is missing joints: {string.Join(", ", missing)}");

                var tip = new GameObject("Poke") { layer = rig.layer };
                tip.transform.SetParent(FindDeep(tracked.transform, prefix + "IndexTip"), false);
                tip.AddComponent<XRPokeInteractor>();

                var grab = new GameObject("Grab");
                Transform palm = FindDeep(tracked.transform, prefix + "Palm");
                grab.transform.SetParent(palm, false);
                // The grab's attach sits where a controller's frame would in this palm, so props hold the same in both.
                Pose grip = HandPoseMath.ControllerInPalm(left);
                grab.transform.SetLocalPositionAndRotation(grip.position, grip.rotation);
                var sphere = grab.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 0.06f;
                var grasp = grab.AddComponent<HandGrasp>();
                var graspSo = new SerializedObject(grasp);
                graspSo.FindProperty("trackingEvents").objectReferenceValue = events;
                graspSo.ApplyModifiedPropertiesWithoutUndo();
                var direct = grab.AddComponent<XRDirectInteractor>();
                direct.improveAccuracyWithSphereCollider = true;
                direct.physicsLayerMask = ~LayerMask.GetMask(UiLayer, "Ignore Raycast", "Trainee", "SafetyZone");
                direct.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ObjectReference;
                direct.selectInput.SetObjectReference(grasp);
                direct.activateInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ObjectReference;
                direct.activateInput.SetObjectReference(grasp);

                if (left)
                {
                    // On the tracked left hand the tablet lies along the forearm behind the wrist, its screen on the
                    // side of the back of the hand and its top toward the thumb.
                    var mount = new GameObject("Tablet Mount (Hand)");
                    mount.transform.SetParent(driver.rootTransform, false);
                    mount.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.04f, -0.13f), Quaternion.LookRotation(Vector3.down, Vector3.right));
                    tabletHandMount = mount.transform;
                }
                tracked.SetActive(false);

                if (modality != null)
                {
                    var modalitySo = new SerializedObject(modality);
                    modalitySo.FindProperty(left ? "m_LeftHand" : "m_RightHand").objectReferenceValue = tracked;
                    modalitySo.ApplyModifiedProperties();
                }
            }

            Transform tablet = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == TabletName);
            if (tablet != null && modality != null)
            {
                var mountSwitch = GetOrAdd<TabletMount>(rig);
                var so = new SerializedObject(mountSwitch);
                so.FindProperty("modality").objectReferenceValue = modality;
                so.FindProperty("tablet").objectReferenceValue = tablet;
                so.FindProperty("controllerMount").objectReferenceValue = tablet.parent;
                so.FindProperty("handMount").objectReferenceValue = tabletHandMount;
                so.ApplyModifiedProperties();
            }
        }

        private static GameObject HandModel(bool left)
        {
            string file = left ? "LeftHand.fbx" : "RightHand.fbx";
            return AssetDatabase.FindAssets("t:Model " + (left ? "LeftHand" : "RightHand"), new[] { "Assets/Samples/XR Hands" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.Replace('\\', '/').EndsWith("/Models/" + file))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .FirstOrDefault();
        }

        private static GameObject InstantiateHand(GameObject model, Transform parent, string name, Material material)
        {
            var go = (GameObject)Object.Instantiate(model, parent, false);
            go.name = name;
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (SkinnedMeshRenderer skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.sharedMaterial = material;
                skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // The fingers move outside the bind pose's bounds; never cull the hand by them.
                skin.updateWhenOffscreen = true;
            }
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            return go;
        }

        // A work glove: dark grey fabric, matte. Kept under Art/Hands so a glove model's own material can replace it.
        private static Material HandMaterial()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Hands"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Hands");
            var material = AssetDatabase.LoadAssetAtPath<Material>(HandMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, HandMaterialPath);
            }
            material.SetColor("_BaseColor", new Color(0.24f, 0.26f, 0.29f));
            material.SetFloat("_Smoothness", 0.18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            return parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        // A station button pressed with a fingertip: the poke runs along the button's own +Y, which faces into the
        // panel (the button cylinders are turned 90 degrees about X), and selects the point like a trigger press.
        private static void PokeButton(TrainingPoint point, XRSimpleInteractable interactable)
        {
            XRPokeFilter poke = point.GetComponent<XRPokeFilter>();
            if (poke == null)
                poke = Undo.AddComponent<XRPokeFilter>(point.gameObject);
            var so = new SerializedObject(poke);
            so.FindProperty("m_Interactable").objectReferenceValue = interactable;
            so.FindProperty("m_PokeCollider").objectReferenceValue = point.GetComponent<Collider>();
            so.FindProperty("m_PokeConfiguration.m_UseConstant").boolValue = true;
            so.FindProperty("m_PokeConfiguration.m_ConstantValue.m_PokeDirection").enumValueIndex = (int)PokeAxis.Y;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A panel the controllers press: a trigger collider the size of the document (rays), and an interactable
        // with a poke filter on it (touch). The collider is listed on the interactable by hand, since XRI skips
        // trigger colliders when it collects them itself.
        private static void Pressable(GameObject go, float width, float height)
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(width / 100f, height / 100f, 0.05f);
            var interactable = go.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Clear();
            interactable.colliders.Add(box);
            var poke = go.AddComponent<XRPokeFilter>();
            poke.pokeInteractable = interactable;
            poke.pokeCollider = box;
            EditorUtility.SetDirty(interactable);
            EditorUtility.SetDirty(poke);
        }

        // A solid slab behind a panel: the board's back and the tablet's body. World-space UI draws in the transparent
        // pass, so without it the hall, the safety-zone volumes and the decals showed through the panels in the headset.
        // The slab sits just behind the document (whose front faces -Z), a margin larger all round, with no collider.
        private static void Backing(GameObject panel, float width, float height, float margin, float depth)
        {
            const string materialPath = "Assets/_Project/UI/Vr/PanelBacking.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetColor("_BaseColor", new Color(0.035f, 0.04f, 0.045f));
            material.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(material);

            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Backing";
            Object.DestroyImmediate(slab.GetComponent<Collider>());
            slab.transform.SetParent(panel.transform, false);
            float scale = panel.transform.localScale.x;
            float thick = depth / scale;
            slab.transform.localPosition = new Vector3(0f, 0f, 0.002f / scale + thick * 0.5f);
            slab.transform.localScale = new Vector3((width + 2f * margin) / 100f, (height + 2f * margin) / 100f, thick);
            var renderer = slab.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            // GetComponent returns a fake null in the Editor, so ?? cannot be used here.
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }
    }
}
