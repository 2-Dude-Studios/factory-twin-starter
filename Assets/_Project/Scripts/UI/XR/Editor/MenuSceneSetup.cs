using System.Linq;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M15), editor tooling: builds Menu.unity, the start menu that loads first: a quiet dark room, the
    /// menu on the desktop screen and on a panel in the headset (Menu.uxml, MenuController), the same VR rig and
    /// hands as the cell (XrSetup), and VrSwitch. Puts Menu then Cell in the build settings. Running it again
    /// rebuilds the menu scene; the cell is reopened at the end.
    /// </summary>
    internal static class MenuSceneSetup
    {
        private const string MenuScenePath = "Assets/_Project/Scenes/Menu.unity";
        private const string CellScenePath = "Assets/_Project/Scenes/Cell.unity";
        private const string MenuUxml = "Assets/_Project/UI/Menu.uxml";
        private const string ScreenPanelPath = "Assets/_Project/UI/MenuPanelSettings.asset";
        private const string FloorMaterialPath = "Assets/_Project/Materials/MenuFloor.mat";
        private const float PanelWidth = 1720f, PanelHeight = 1020f;

        [MenuItem("Tools/Factory Twin/Build Menu Scene")]
        private static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Room();
            Camera desktopCamera = DesktopCamera();
            UIDocument screen = ScreenMenu();
            UIDocument headset = HeadsetMenu();
            (GameObject rig, XROrigin origin) = Rig();

            var root = new GameObject("Menu");
            var vrSwitch = root.AddComponent<VrSwitch>();
            var switchSo = new SerializedObject(vrSwitch);
            switchSo.FindProperty("xrRig").objectReferenceValue = rig;
            SerializedProperty desktopOnly = switchSo.FindProperty("desktopOnly");
            desktopOnly.arraySize = 0;
            switchSo.FindProperty("desktopCamera").objectReferenceValue = desktopCamera;
            switchSo.FindProperty("desktopListener").objectReferenceValue = desktopCamera.GetComponent<AudioListener>();
            switchSo.FindProperty("vrPipeline").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(XrSetup.VrPipelinePath);
            switchSo.ApplyModifiedProperties();
            var controller = root.AddComponent<MenuController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("desktopDocument").objectReferenceValue = screen;
            controllerSo.FindProperty("headsetDocument").objectReferenceValue = headset;
            controllerSo.FindProperty("vrSwitch").objectReferenceValue = vrSwitch;
            controllerSo.FindProperty("badgeIcons").objectReferenceValue = FireXrSetup.BadgeIconsAsset();
            controllerSo.ApplyModifiedProperties();
            rig.SetActive(false);

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(CellScenePath, true),
            };
            Debug.Log($"[MenuSceneSetup] {MenuScenePath} built; build settings: Menu, Cell.");
            EditorSceneManager.OpenScene(CellScenePath, OpenSceneMode.Single);
        }

        // A dark room: a floor disc, a soft key light, no skybox, so the menu reads.
        private static void Room()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.27f);
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(14f, 0.01f, 14f);
            floor.transform.position = new Vector3(0f, -0.01f, 0f);
            // The cylinder primitive's capsule collider, flattened and widened, becomes a 7 m ball the rig's gravity
            // stood the trainee on top of (seen in the headset, 2026-11-04). A flat box instead, its top at y 0.
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.AddComponent<BoxCollider>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(0.09f, 0.1f, 0.11f));
                material.SetFloat("_Smoothness", 0.6f);
                AssetDatabase.CreateAsset(material, FloorMaterialPath);
            }
            floor.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Camera DesktopCamera()
        {
            var go = new GameObject("Desktop Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.055f, 0.06f);
            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>();
            go.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, -1.5f), Quaternion.identity);
            return camera;
        }

        // The desktop menu: a screen-space document, scaled for 1920 x 1080.
        private static UIDocument ScreenMenu()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(ScreenPanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, ScreenPanelPath);
            }
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(XrSetup.ThemePath);
            EditorUtility.SetDirty(panel);
            var go = new GameObject("Menu (Screen)");
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MenuUxml);
            return document;
        }

        // The headset menu: a world-space panel 1.6 m wide, 1.6 m ahead of where the trainee stands, at eye height.
        private static UIDocument HeadsetMenu()
        {
            var go = new GameObject("Menu (Headset)") { layer = LayerMask.NameToLayer("UI") };
            go.transform.SetPositionAndRotation(new Vector3(0f, 1.45f, 1.6f), Quaternion.identity);
            UIDocument document = XrSetup.Document(go, XrSetup.WorldPanel(), MenuUxml, PanelWidth, PanelHeight, 1.6f / (PanelWidth / 100f));
            XrSetup.Pressable(go, PanelWidth, PanelHeight);
            XrSetup.Backing(go, PanelWidth, PanelHeight, 16f, 0.03f);
            return document;
        }

        // The cell's rig, without the tablet or the lockout props: rays and pokes press the menu, the hands are drawn.
        private static (GameObject, XROrigin) Rig()
        {
            string prefabPath = AssetDatabase.FindAssets("t:Prefab XR Origin (XR Rig)")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.Replace('\\', '/').EndsWith(XrSetup.RigPrefabSuffix));
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (CurveInteractionCaster caster in rig.GetComponentsInChildren<CurveInteractionCaster>(true))
            {
                caster.raycastTriggerInteraction = QueryTriggerInteraction.Collide;
                caster.raycastMask = ~LayerMask.GetMask("Ignore Raycast", "Trainee", "SafetyZone");
            }
            XROrigin origin = rig.GetComponent<XROrigin>();
            XrSetup.Body(rig.transform, origin.Camera.transform);
            Camera eye = origin.Camera;
            eye.farClipPlane = 50f;
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = new Color(0.05f, 0.055f, 0.06f);
            var eyeData = eye.GetComponent<UniversalAdditionalCameraData>();
            if (eyeData != null)
            {
                eyeData.renderPostProcessing = false;
                eyeData.renderShadows = false;
            }
            rig.AddComponent<XRUIToolkitManager>();
            var panelInput = rig.AddComponent<PanelInputConfiguration>();
            panelInput.panelInputRedirection = PanelInputConfiguration.PanelInputRedirection.Never;
            rig.AddComponent<VrBackgroundInput>();
            XrSetup.OnePointer(rig);
            XrSetup.Hands(rig, origin);
            return (rig, origin);
        }
    }
}
