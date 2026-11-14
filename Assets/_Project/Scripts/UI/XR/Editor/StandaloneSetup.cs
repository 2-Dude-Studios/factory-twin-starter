using System.Collections.Generic;
using System.Linq;
using System.Text;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone mode (M27). Two parts, both re-runnable:
    /// <list type="bullet">
    /// <item>The standalone switch, applied by the Quest variant builder to CellQuest only, so the headset build is
    /// standalone by construction and the desktop and Link builds are untouched: the MQTT bridge and the soft PLC link
    /// off (the in-Unity sequencer runs the line), the desktop dashboard and the capture rig's director and take
    /// sequencer off, the URDF importer's components (missing scripts on Android) left out, and the headset pause on
    /// the rig.</item>
    /// <item>The headset controls, applied to CellQuest too and to the open scene from the menu (for Link): the robot
    /// cell drill's page on the tablet (<see cref="VrRobotMenu"/>) and a teleport floor over the walkways between the
    /// training areas.</item>
    /// </list>
    /// </summary>
    internal static class StandaloneSetup
    {
        private const string WalkwayName = "WalkwayTeleportFloor";

        [MenuItem("Tools/Factory Twin/Quest/Add Headset Controls to Open Scene")]
        private static void ControlsMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            var log = new StringBuilder("[StandaloneSetup] ");
            Controls(scene, log);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.Append("Save the scene to keep them.").ToString());
        }

        /// <summary>Makes the scene run on the headset alone. For CellQuest only.</summary>
        public static void Standalone(Scene scene, StringBuilder log)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            int off = 0;
            off += Disable<MqttBridge>(roots);
            off += Disable<PlcLink>(roots);
            off += Disable<ShotDirector>(roots);
            off += Disable<TakeSequencer>(roots);
            int dashboards = 0;
            foreach (DashboardController dashboard in roots.SelectMany(g => g.GetComponentsInChildren<DashboardController>(true)))
            {
                dashboard.gameObject.SetActive(false);
                dashboards++;
            }

            // The URDF importer's runtime components only describe the arm as imported; on Android their assembly is
            // not built, so each showed as a missing script. Nothing reads them at run time.
            // Removed in passes: a component another one requires goes once that one has gone.
            int urdf = 0;
            for (int pass = 0; pass < 6; pass++)
            {
                int removed = 0;
                foreach (MonoBehaviour b in roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToList())
                {
                    if (b == null || !IsUrdf(b) || RequiredByAnother(b))
                        continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(b))
                        PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(b), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    Object.DestroyImmediate(b);
                    removed++;
                }
                urdf += removed;
                if (removed == 0)
                    break;
            }

            XROrigin origin = roots.SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).FirstOrDefault();
            if (origin != null && origin.GetComponent<HeadsetPause>() == null)
                origin.gameObject.AddComponent<HeadsetPause>();

            log.Append($"Standalone: MQTT bridge, PLC link, shot director and take sequencer off ({off}), dashboard off ({dashboards}), " +
                       $"{urdf} URDF components left out, headset pause on the rig. ");
            Hub(scene, roots, origin, log);
        }

        private const string HubMenuUxml = "Assets/_Project/UI/Vr/HubMenu.uxml";
        private const string LicensesFolder = "Assets/_Project/Licenses";
        private const float HubMenuWidthPx = 1800f, HubMenuHeightPx = 1100f, HubMenuWidthMetres = 2.5f;
        private const string ResultsUxml = "Assets/_Project/UI/Vr/ResultsCard.uxml";
        private const float ResultsWidthPx = 900f, ResultsHeightPx = 820f, ResultsWidthMetres = 1.0f;
        private const float HubMenuAhead = 2.3f, HubMenuHeight = 1.5f;

        /// <summary>
        /// The headset hub (M28): the menu inside the cell. A HubSession beside the VR switch (SceneNavigation hands it the
        /// menu's choices and every main-menu button) and the hub menu, a world-space panel 2.3 m ahead of the hub's start,
        /// facing the trainee.
        /// </summary>
        private static void Hub(Scene scene, GameObject[] roots, XROrigin origin, StringBuilder log)
        {
            CellContext context = roots.SelectMany(g => g.GetComponentsInChildren<CellContext>(true)).FirstOrDefault();
            VrSwitch vrSwitch = roots.SelectMany(g => g.GetComponentsInChildren<VrSwitch>(true)).FirstOrDefault();
            TrainingLauncher launcher = roots.SelectMany(g => g.GetComponentsInChildren<TrainingLauncher>(true)).FirstOrDefault();
            TrainingAreaStart hubStart = roots.SelectMany(g => g.GetComponentsInChildren<TrainingAreaStart>(true)).FirstOrDefault(t => t.Area == TrainingArea.Hub);
            if (vrSwitch == null || launcher == null || hubStart == null)
            {
                log.Append("No VR switch, launcher or hub start: hub skipped. ");
                return;
            }
            HubSession session = vrSwitch.GetComponent<HubSession>() ?? vrSwitch.gameObject.AddComponent<HubSession>();
            var so = new SerializedObject(session);
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("vrSwitch").objectReferenceValue = vrSwitch;
            so.FindProperty("launcher").objectReferenceValue = launcher;
            so.FindProperty("origin").objectReferenceValue = origin;
            so.FindProperty("hubStart").objectReferenceValue = hubStart;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject old = roots.SelectMany(g => g.GetComponentsInChildren<HubMenu>(true)).Select(m => m.gameObject).FirstOrDefault();
            if (old != null)
                Object.DestroyImmediate(old);
            var go = new GameObject("HubMenu") { layer = LayerMask.NameToLayer("UI") };
            SceneManager.MoveGameObjectToScene(go, scene);
            if (context != null)
                go.transform.SetParent(context.transform, false);
            Quaternion facing = Quaternion.Euler(0f, hubStart.YawDegrees, 0f);
            go.transform.SetPositionAndRotation(hubStart.FloorPosition + facing * Vector3.forward * HubMenuAhead + Vector3.up * HubMenuHeight, facing);
            UIDocument document = XrSetup.Document(go, XrSetup.WorldPanel(), HubMenuUxml, HubMenuWidthPx, HubMenuHeightPx,
                HubMenuWidthMetres / (HubMenuWidthPx / 100f));
            XrSetup.Pressable(go, HubMenuWidthPx, HubMenuHeightPx);
            XrSetup.Backing(go, HubMenuWidthPx, HubMenuHeightPx, 16f, 0.03f);
            var menu = go.AddComponent<HubMenu>();
            var ms = new SerializedObject(menu);
            ms.FindProperty("document").objectReferenceValue = document;
            ms.FindProperty("hub").objectReferenceValue = session;
            ms.FindProperty("context").objectReferenceValue = context;
            ms.FindProperty("logo").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(StudioLogoPath);

            // Credits and licences in the headset (M29): every notice file, in file-name order.
            TextAsset[] notices = AssetDatabase.FindAssets("t:TextAsset", new[] { LicensesFolder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>).Where(t => t != null).ToArray();
            SerializedProperty noticeList = ms.FindProperty("notices");
            noticeList.arraySize = notices.Length;
            for (int i = 0; i < notices.Length; i++)
                noticeList.GetArrayElementAtIndex(i).objectReferenceValue = notices[i];
            log.Append($"Credits: {notices.Length} notice files. ");

            // The Meta account (the trainee's name) and the leaderboards beside the session.
            MetaAccount account = session.GetComponent<MetaAccount>() ?? session.gameObject.AddComponent<MetaAccount>();
            var acc = new SerializedObject(account);
            acc.FindProperty("context").objectReferenceValue = context;
            acc.FindProperty("lockout").objectReferenceValue = roots.SelectMany(g => g.GetComponentsInChildren<TrainingController>(true)).FirstOrDefault();
            acc.FindProperty("robot").objectReferenceValue = roots.SelectMany(g => g.GetComponentsInChildren<RobotCellScorer>(true)).FirstOrDefault();
            acc.FindProperty("fire").objectReferenceValue = roots.SelectMany(g => g.GetComponentsInChildren<FireDrillScorer>(true)).FirstOrDefault();
            acc.ApplyModifiedPropertiesWithoutUndo();
            MetaLeaderboards boards = session.GetComponent<MetaLeaderboards>() ?? session.gameObject.AddComponent<MetaLeaderboards>();
            var bs = new SerializedObject(boards);
            bs.FindProperty("account").objectReferenceValue = account;
            bs.ApplyModifiedPropertiesWithoutUndo();
            ms.FindProperty("account").objectReferenceValue = account;
            ms.FindProperty("boards").objectReferenceValue = boards;
            ms.ApplyModifiedPropertiesWithoutUndo();
            string appId = context != null && context.Config != null ? context.Config.QuestAppId : string.Empty;
            log.Append(string.IsNullOrWhiteSpace(appId) ? "Meta account: no App ID in TwinConfig yet (offline). " : "Meta account and leaderboards on (App ID set). ");
            ResultsAndFeedback(scene, roots, origin, context, session, log);
            VoiceAndComfort(origin, context, session, menu, log);
            log.Append($"Hub: session on {vrSwitch.name}, menu at {go.transform.position.x:F1}, {go.transform.position.z:F1}. ");
        }

        // The game layer's results card (comes up in front of the trainee when a run ends) and the step popups and buzz.
        private static void ResultsAndFeedback(Scene scene, GameObject[] roots, XROrigin origin, CellContext context, HubSession session, StringBuilder log)
        {
            GameObject old = roots.SelectMany(g => g.GetComponentsInChildren<ResultsCard>(true)).Select(c => c.gameObject).FirstOrDefault();
            if (old != null)
                Object.DestroyImmediate(old);
            var go = new GameObject("ResultsCard") { layer = LayerMask.NameToLayer("UI") };
            SceneManager.MoveGameObjectToScene(go, scene);
            if (context != null)
                go.transform.SetParent(context.transform, false);
            UIDocument document = XrSetup.Document(go, XrSetup.WorldPanel(), ResultsUxml, ResultsWidthPx, ResultsHeightPx,
                ResultsWidthMetres / (ResultsWidthPx / 100f));
            XrSetup.Pressable(go, ResultsWidthPx, ResultsHeightPx);
            XrSetup.Backing(go, ResultsWidthPx, ResultsHeightPx, 12f, 0.02f);
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            var card = go.AddComponent<ResultsCard>();
            var cs = new SerializedObject(card);
            cs.FindProperty("document").objectReferenceValue = document;
            cs.FindProperty("hub").objectReferenceValue = session;
            cs.FindProperty("head").objectReferenceValue = origin != null && origin.Camera != null ? origin.Camera.transform : null;
            cs.FindProperty("audioSource").objectReferenceValue = audio;
            cs.ApplyModifiedPropertiesWithoutUndo();

            DrillFeedback feedback = session.GetComponent<DrillFeedback>() ?? session.gameObject.AddComponent<DrillFeedback>();
            var fs = new SerializedObject(feedback);
            fs.FindProperty("context").objectReferenceValue = context;
            fs.FindProperty("popups").objectReferenceValue = roots.SelectMany(g => g.GetComponentsInChildren<ScorePopups>(true)).FirstOrDefault();
            fs.FindProperty("robot").objectReferenceValue = roots.SelectMany(g => g.GetComponentsInChildren<RobotCellScorer>(true)).FirstOrDefault();
            fs.ApplyModifiedPropertiesWithoutUndo();
            log.Append($"Results card and step feedback (popups {(fs.FindProperty("popups").objectReferenceValue != null ? "found" : "MISSING")}). ");
        }

        private const string TrainerAudioFolder = "Assets/_Project/Audio/Trainer";
        private const string VignettePrefabPath = "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/TunnelingVignette/TunnelingVignette.prefab";

        // The AI trainer's voice (M29): every clip in Audio/Trainer, grouped by cue (cue_1, cue_2… are takes of one cue).
        // Comfort (M29): the rig's two controller managers and a tunnelling vignette under the headset camera for
        // smooth movement and turning; the hub menu's comfort page drives them.
        private static void VoiceAndComfort(XROrigin origin, CellContext context, HubSession session, HubMenu menu, StringBuilder log)
        {
            Transform old = session.transform.Find("TrainerVoice");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("TrainerVoice");
            go.transform.SetParent(session.transform, false);
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.priority = 0;
            var voice = go.AddComponent<TrainerVoice>();
            var takes = AssetDatabase.FindAssets("t:AudioClip", new[] { TrainerAudioFolder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null)
                .GroupBy(c => System.Text.RegularExpressions.Regex.Replace(c.name, @"_\d+$", string.Empty)).ToArray();
            var vs = new SerializedObject(voice);
            vs.FindProperty("context").objectReferenceValue = context;
            vs.FindProperty("hub").objectReferenceValue = session;
            vs.FindProperty("source").objectReferenceValue = audio;
            GameObject[] sceneRoots = session.gameObject.scene.GetRootGameObjects();
            vs.FindProperty("robot").objectReferenceValue = sceneRoots.SelectMany(g => g.GetComponentsInChildren<RobotCellScorer>(true)).FirstOrDefault();
            vs.FindProperty("fire").objectReferenceValue = sceneRoots.SelectMany(g => g.GetComponentsInChildren<FireDrillScorer>(true)).FirstOrDefault();
            SerializedProperty cues = vs.FindProperty("cues");
            cues.arraySize = takes.Length;
            for (int i = 0; i < takes.Length; i++)
            {
                SerializedProperty cue = cues.GetArrayElementAtIndex(i);
                cue.FindPropertyRelative("id").stringValue = takes[i].Key;
                SerializedProperty clips = cue.FindPropertyRelative("clips");
                AudioClip[] list = takes[i].ToArray();
                clips.arraySize = list.Length;
                for (int k = 0; k < list.Length; k++)
                    clips.GetArrayElementAtIndex(k).objectReferenceValue = list[k];
            }
            vs.ApplyModifiedPropertiesWithoutUndo();
            log.Append($"Trainer voice: {takes.Length} cues, {takes.Sum(g => g.Count())} clips, coaching the robot cell {(vs.FindProperty("robot").objectReferenceValue != null ? "on" : "MISSING")}, fire {(vs.FindProperty("fire").objectReferenceValue != null ? "on" : "MISSING")}. ");

            var managers = origin != null
                ? origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.ControllerInputActionManager>(true)
                : new UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.ControllerInputActionManager[0];
            GameObject vignette = null;
            Camera head = origin != null ? origin.Camera : null;
            if (head != null)
            {
                Transform existing = head.transform.Find("TunnelingVignette");
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VignettePrefabPath);
                if (prefab != null)
                {
                    vignette = (GameObject)PrefabUtility.InstantiatePrefab(prefab, head.transform);
                    vignette.name = "TunnelingVignette";
                    var controller = vignette.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.TunnelingVignetteController>();
                    if (controller != null)
                    {
                        var providers = new List<UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.LocomotionVignetteProvider>();
                        foreach (var p in origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true))
                        {
                            if (p is UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider ||
                                p is UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.ContinuousTurnProvider)
                                providers.Add(new UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.LocomotionVignetteProvider { locomotionProvider = p, enabled = true });
                        }
                        controller.locomotionVignetteProviders = providers;
                        EditorUtility.SetDirty(controller);
                        log.Append($"Comfort vignette on {providers.Count} smooth providers. ");
                    }
                    vignette.SetActive(false);
                }
            }
            ComfortSettings comfort = session.GetComponent<ComfortSettings>() ?? session.gameObject.AddComponent<ComfortSettings>();
            var cs = new SerializedObject(comfort);
            cs.FindProperty("leftController").objectReferenceValue = managers.FirstOrDefault(m => m.name.Contains("Left"));
            cs.FindProperty("rightController").objectReferenceValue = managers.FirstOrDefault(m => m.name.Contains("Right"));
            cs.FindProperty("vignette").objectReferenceValue = vignette;
            cs.ApplyModifiedPropertiesWithoutUndo();
            var ms = new SerializedObject(menu);
            ms.FindProperty("comfort").objectReferenceValue = comfort;
            ms.ApplyModifiedPropertiesWithoutUndo();

            // The same options on the tablet (M29), beside the robot page that already drives the tablet's document.
            VrRobotMenu robotPage = sceneRoots.SelectMany(g => g.GetComponentsInChildren<VrRobotMenu>(true)).FirstOrDefault();
            Object tablet = robotPage != null ? new SerializedObject(robotPage).FindProperty("document").objectReferenceValue : null;
            if (robotPage != null && tablet != null)
            {
                VrComfortMenu page = robotPage.GetComponent<VrComfortMenu>() ?? robotPage.gameObject.AddComponent<VrComfortMenu>();
                var ps = new SerializedObject(page);
                ps.FindProperty("document").objectReferenceValue = tablet;
                ps.FindProperty("comfort").objectReferenceValue = comfort;
                ps.FindProperty("context").objectReferenceValue = context;
                ps.ApplyModifiedPropertiesWithoutUndo();
                log.Append("Comfort page on the tablet. ");
            }
            else
            {
                log.Append("Comfort page on the tablet: NO tablet found. ");
            }
            log.Append($"Comfort: {managers.Length} controller managers, teleport only and snap turn by default. ");
        }

        /// <summary>The 2 Dude Studios logo, when there is one (M28, banners and the hub menu's header).</summary>
        public const string StudioLogoPath = "Assets/_Project/Art/Brand/2DudeStudios_Logo.png";

        /// <summary>The headset's own controls: the robot drill page on the tablet and the walkway teleport floor.</summary>
        public static void Controls(Scene scene, StringBuilder log)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            CellContext context = roots.SelectMany(g => g.GetComponentsInChildren<CellContext>(true)).FirstOrDefault();
            XROrigin origin = roots.SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).FirstOrDefault();
            VrSwitch vrSwitch = roots.SelectMany(g => g.GetComponentsInChildren<VrSwitch>(true)).FirstOrDefault();
            RobotCellScorer scorer = roots.SelectMany(g => g.GetComponentsInChildren<RobotCellScorer>(true)).FirstOrDefault();
            TrainingAreaStart start = roots.SelectMany(g => g.GetComponentsInChildren<TrainingAreaStart>(true)).FirstOrDefault(s => s.Area == TrainingArea.RobotCell);
            VrMenu tablet = roots.SelectMany(g => g.GetComponentsInChildren<VrMenu>(true)).FirstOrDefault();

            if (tablet != null && tablet.TryGetComponent(out UIDocument document))
            {
                VrRobotMenu menu = tablet.GetComponent<VrRobotMenu>() ?? tablet.gameObject.AddComponent<VrRobotMenu>();
                var so = new SerializedObject(menu);
                so.FindProperty("document").objectReferenceValue = document;
                so.FindProperty("context").objectReferenceValue = context;
                so.FindProperty("scorer").objectReferenceValue = scorer;
                so.FindProperty("vrSwitch").objectReferenceValue = vrSwitch;
                so.FindProperty("origin").objectReferenceValue = origin;
                so.FindProperty("start").objectReferenceValue = start;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.Append($"Robot drill page on the tablet (scorer {(scorer != null ? "found" : "MISSING")}, start {(start != null ? "found" : "MISSING")}). ");

                TrainingAreaStart bay = roots.SelectMany(g => g.GetComponentsInChildren<TrainingAreaStart>(true)).FirstOrDefault(s => s.Area == TrainingArea.Electrical);
                VrElectricalMenu elec = tablet.GetComponent<VrElectricalMenu>() ?? tablet.gameObject.AddComponent<VrElectricalMenu>();
                var es = new SerializedObject(elec);
                es.FindProperty("document").objectReferenceValue = document;
                es.FindProperty("context").objectReferenceValue = context;
                es.FindProperty("vrSwitch").objectReferenceValue = vrSwitch;
                es.FindProperty("origin").objectReferenceValue = origin;
                es.FindProperty("start").objectReferenceValue = bay;
                es.ApplyModifiedPropertiesWithoutUndo();
                log.Append($"Electrical drill page on the tablet (start {(bay != null ? "found" : "MISSING")}). ");
            }
            else
            {
                log.Append("No tablet (VrMenu with a UIDocument): robot drill page skipped. ");
            }

            InteractionManagerAtRoot(scene, log);
            AreaSpots(roots, tablet, log);

            // One floor over the box that holds every training area, under the areas' own floors: the walkways between
            // them. The teleport ray lands on the first collider it meets, so machines and walls still block it.
            Transform training = context != null ? context.transform.Find("Training") : null;
            if (training == null)
            {
                log.Append("No Cell/Training: walkway floor skipped. ");
                return;
            }
            Transform old = training.Find(WalkwayName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            Rect areas = QuestVariantSetup.AreasBox();
            var floor = new GameObject(WalkwayName);
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.SetParent(training, false);
            floor.transform.position = new Vector3(areas.center.x, -0.015f, areas.center.y);
            var box = floor.AddComponent<BoxCollider>();
            box.size = new Vector3(areas.width, 0.02f, areas.height);
            floor.AddComponent<TeleportationArea>();
            log.Append($"Walkway teleport floor {areas.width:F0} x {areas.height:F0} m. ");
        }

        // The tablet main page's Areas row (VrMenu.uxml, menu-area-*): each training area's start spot, added to the
        // tablet's Go to spots (any from an earlier run replaced).
        private static readonly (string Button, TrainingArea Area)[] AreaButtons =
        {
            ("menu-area-hub", TrainingArea.Hub), ("menu-area-robot", TrainingArea.RobotCell), ("menu-area-lockout", TrainingArea.Lockout),
            ("menu-area-fire", TrainingArea.Fire), ("menu-area-electrical", TrainingArea.Electrical),
        };

        private static void AreaSpots(GameObject[] roots, VrMenu tablet, StringBuilder log)
        {
            if (tablet == null)
                return;
            TrainingAreaStart[] starts = roots.SelectMany(g => g.GetComponentsInChildren<TrainingAreaStart>(true)).ToArray();
            var so = new SerializedObject(tablet);
            SerializedProperty spots = so.FindProperty("spots");
            for (int i = spots.arraySize - 1; i >= 0; i--)
            {
                if (spots.GetArrayElementAtIndex(i).FindPropertyRelative("button").stringValue.StartsWith("menu-area-"))
                    spots.DeleteArrayElementAtIndex(i);
            }
            int added = 0;
            foreach ((string button, TrainingArea area) in AreaButtons)
            {
                TrainingAreaStart start = starts.FirstOrDefault(s => s.Area == area);
                if (start == null)
                    continue;
                spots.arraySize++;
                SerializedProperty entry = spots.GetArrayElementAtIndex(spots.arraySize - 1);
                entry.FindPropertyRelative("button").stringValue = button;
                entry.FindPropertyRelative("floorPosition").vector3Value = start.FloorPosition;
                entry.FindPropertyRelative("yawDegrees").floatValue = start.YawDegrees;
                added++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Append($"Tablet: {added} area spots. ");
        }

        /// <summary>
        /// The XR interaction manager came with the rig, which is saved inactive (VrSwitch turns it on when VR
        /// starts). So when the scene loaded, every interactable found no manager and XRI parked it until the load
        /// finished; one of them was gone by then, the callback threw, and the interactables after it in the list were
        /// never registered (the extinguisher could not be picked up). Out of the rig, at the scene's root, the manager
        /// is active from the start and each interactable registers as it wakes.
        /// </summary>
        internal static void InteractionManagerAtRoot(Scene scene, StringBuilder log)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            List<XRInteractionManager> managers = roots.SelectMany(g => g.GetComponentsInChildren<XRInteractionManager>(true)).ToList();
            XRInteractionManager manager = managers.FirstOrDefault();
            if (manager == null)
            {
                var go = new GameObject("XR Interaction Manager");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<XRInteractionManager>();
                log.Append("Interaction manager added at the scene root. ");
                return;
            }
            if (manager.transform.parent != null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(manager))
                    PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(manager), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                manager.transform.SetParent(null, true);
            }
            manager.gameObject.SetActive(true);
            manager.enabled = true;
            log.Append($"Interaction manager at the scene root, active from load ({managers.Count} in the scene). ");
        }

        /// <summary>
        /// The start menu had the same gap: its interactables were parked for a manager that never came, and when the
        /// cell loaded XRI tripped over them, gone by then. Menu.unity gets its own manager at the root, saved.
        /// </summary>
        public static void MenuManager(StringBuilder log)
        {
            const string path = "Assets/_Project/Scenes/Menu.unity";
            Scene menu = SceneManager.GetSceneByPath(path);
            bool wasOpen = menu.isLoaded;
            if (!wasOpen)
                menu = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            bool had = menu.GetRootGameObjects().Any(g => g.GetComponent<XRInteractionManager>() != null);
            if (!had)
            {
                log.Append("Menu: ");
                InteractionManagerAtRoot(menu, log);
                EditorSceneManager.SaveScene(menu);
            }
            if (!wasOpen)
                EditorSceneManager.CloseScene(menu, true);
        }

        private static bool IsUrdf(Component c) => c.GetType().Namespace?.StartsWith("Unity.Robotics") == true;

        private static bool RequiredByAnother(Component target)
        {
            System.Type type = target.GetType();
            foreach (Component other in target.GetComponents<Component>())
            {
                if (other == null || other == target)
                    continue;
                foreach (RequireComponent rc in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if ((rc.m_Type0 != null && rc.m_Type0.IsAssignableFrom(type)) ||
                        (rc.m_Type1 != null && rc.m_Type1.IsAssignableFrom(type)) ||
                        (rc.m_Type2 != null && rc.m_Type2.IsAssignableFrom(type)))
                        return true;
                }
            }
            return false;
        }

        private static int Disable<T>(GameObject[] roots) where T : Behaviour
        {
            int n = 0;
            foreach (T c in roots.SelectMany(g => g.GetComponentsInChildren<T>(true)))
            {
                c.enabled = false;
                n++;
            }
            return n;
        }
    }
}
