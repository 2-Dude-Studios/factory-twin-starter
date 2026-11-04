using System.Collections.Generic;
using System.Linq;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15), editor tooling: makes the extinguishers on the fire training station's stand work with two
    /// hands, and wires the tablet's fire page. Each one gets a grab on its carry handle, a pin to pull, a hose to take
    /// by its nozzle (with a hose line that follows the hand) and an ExtinguisherUnit; FireExtinguisher on the station
    /// follows the one in use; VrFireMenu goes on the tablet. Run Build Fire Drill first (it rebuilds the stand), then
    /// this. Running it again replaces what it built.
    /// </summary>
    internal static class FireXrSetup
    {
        private const string ScenarioFolder = "Assets/_Project/Data/Fire";
        // The tablet starts one fire per spot; F4 and F5 are timing variants of the CNC fire for the dashboard.
        private static readonly int[] TabletScenarios = { 1, 2, 3, 7, 8, 9, 6 };
        private static readonly Vector3 StationFloor = new Vector3(-4.8f, 0f, -12.6f);
        private const float StationYaw = 180f;
        private const float HoseWidth = 0.018f;
        private const int HosePoints = 12;

        private static readonly Dictionary<string, (FireAgent agent, Color spray)> Kinds = new Dictionary<string, (FireAgent, Color)>
        {
            { "Ext_Water", (FireAgent.Water, new Color(0.75f, 0.88f, 1f, 0.7f)) },
            { "Ext_CO2", (FireAgent.Co2, new Color(1f, 1f, 1f, 0.85f)) },
            { "Ext_ABC", (FireAgent.DryChemicalAbc, new Color(1f, 0.96f, 0.84f, 0.95f)) },
        };

        [MenuItem("Tools/Factory Twin/Build Fire Drill (VR)")]
        private static void Build()
        {
            GameObject captureRig = GameObject.Find("CaptureRig");
            GameObject cell = GameObject.Find("Cell");
            Transform rigTransform = captureRig != null ? captureRig.transform.Find("XR Origin (XR Rig)") : null;
            Transform fire = cell != null ? cell.transform.Find("Fire") : null;
            Transform station = fire != null ? fire.Find("TrainingStation") : null;
            CellContext context = cell != null ? cell.GetComponent<CellContext>() : null;
            if (rigTransform == null || station == null || context == null)
            {
                Debug.LogError("[FireXrSetup] Open Cell.unity with the VR rig built (Build VR Rig) and the fire drill built (Build Fire Drill).");
                return;
            }
            XROrigin origin = rigTransform.GetComponent<XROrigin>();
            Transform tablet = rigTransform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "VrTablet");
            if (tablet == null)
            {
                Debug.LogError("[FireXrSetup] The rig has no VrTablet; rebuild the VR rig first.");
                return;
            }

            Undo.SetCurrentGroupName("Build Fire Drill (VR)");
            int undoGroup = Undo.GetCurrentGroup();
            RemoveOldHandSetup(rigTransform);

            // The extinguishers on the stand, each worked with two hands.
            var units = new List<ExtinguisherUnit>();
            foreach (KeyValuePair<string, (FireAgent agent, Color spray)> kind in Kinds)
            {
                Transform group = station.Find("Stowed_" + kind.Key);
                if (group == null)
                {
                    Debug.LogWarning($"[FireXrSetup] No Stowed_{kind.Key} on the stand; run Build Fire Drill first.");
                    continue;
                }
                units.Add(Unit(group, kind.Value.agent));
            }

            // The extinguisher in use: on the station, reading whichever unit is in a hand.
            var extinguisher = GetOrAdd<FireExtinguisher>(station.gameObject);
            var so = new SerializedObject(extinguisher);
            so.FindProperty("context").objectReferenceValue = context;
            SetList(so.FindProperty("units"), units.Cast<Object>().ToList());
            FireEffects[] spots = fire.GetComponentsInChildren<FireEffects>(true);
            SerializedProperty fires = so.FindProperty("fires");
            fires.arraySize = spots.Length;
            for (int i = 0; i < spots.Length; i++)
            {
                SerializedProperty entry = fires.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("location").intValue = new SerializedObject(spots[i]).FindProperty("location").intValue;
                entry.FindPropertyRelative("baseOfFire").objectReferenceValue = spots[i].transform;
            }
            Transform spray = fire.Find("ExtinguisherSpray");
            so.FindProperty("spray").objectReferenceValue = spray != null ? spray.GetComponent<ParticleSystem>() : null;
            SerializedProperty colours = so.FindProperty("sprayColours");
            colours.arraySize = Kinds.Count;
            int c = 0;
            foreach (KeyValuePair<string, (FireAgent agent, Color spray)> kind in Kinds)
            {
                SerializedProperty entry = colours.GetArrayElementAtIndex(c++);
                entry.FindPropertyRelative("agent").intValue = (int)kind.Value.agent;
                entry.FindPropertyRelative("colour").colorValue = kind.Value.spray;
            }
            so.ApplyModifiedProperties();

            // The tablet's fire page.
            var menu = GetOrAdd<VrFireMenu>(tablet.gameObject);
            var menuSo = new SerializedObject(menu);
            menuSo.FindProperty("document").objectReferenceValue = tablet.GetComponent<UIDocument>();
            menuSo.FindProperty("context").objectReferenceValue = context;
            menuSo.FindProperty("extinguisher").objectReferenceValue = extinguisher;
            menuSo.FindProperty("origin").objectReferenceValue = origin;
            menuSo.FindProperty("stationFloor").vector3Value = StationFloor;
            menuSo.FindProperty("stationYaw").floatValue = StationYaw;
            List<Object> scenarios = TabletScenarios
                .Select(n => (Object)AssetDatabase.LoadAssetAtPath<FireScenario>($"{ScenarioFolder}/F{n}.asset"))
                .Where(s => s != null).ToList();
            SetList(menuSo.FindProperty("scenarios"), scenarios);

            // Scoring (M15): the scorer on the station, the popups in front of the headset, the board by the stand.
            VrSwitch vrSwitch = captureRig.GetComponent<VrSwitch>();
            var trainingController = Object.FindAnyObjectByType<TrainingController>();
            string trainee = trainingController != null ? trainingController.TraineeName : "Sam Rivera";
            var scorer = GetOrAdd<FireDrillScorer>(station.gameObject);
            var scorerSo = new SerializedObject(scorer);
            scorerSo.FindProperty("context").objectReferenceValue = context;
            scorerSo.FindProperty("extinguisher").objectReferenceValue = extinguisher;
            SetList(scorerSo.FindProperty("scenarios"), scenarios);
            scorerSo.FindProperty("traineeName").stringValue = trainee;
            scorerSo.ApplyModifiedProperties();
            menuSo.FindProperty("scorer").objectReferenceValue = scorer;
            menuSo.FindProperty("vrSwitch").objectReferenceValue = vrSwitch;
            menuSo.ApplyModifiedProperties();
            BadgeIcons icons = BadgeIconsAsset();
            Popups(rigTransform, origin, scorer, icons);
            ScoreBoard(station, scorer, icons);
            Hud(rigTransform, origin, context, scorer, extinguisher, icons, scenarios);

            // The start menu's request, carried out once the cell has loaded.
            var launcher = GetOrAdd<TrainingLauncher>(captureRig);
            var launcherSo = new SerializedObject(launcher);
            launcherSo.FindProperty("context").objectReferenceValue = context;
            launcherSo.FindProperty("vrSwitch").objectReferenceValue = vrSwitch;
            launcherSo.FindProperty("vrMenu").objectReferenceValue = tablet.GetComponent<VrMenu>();
            launcherSo.FindProperty("fireMenu").objectReferenceValue = menu;
            launcherSo.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(cell.scene);
            Debug.Log($"[FireXrSetup] {units.Count} two-handed extinguishers on the stand, {spots.Length} fire spots, {scenarios.Count} fires on the tablet.");
        }

        // The points and badges in front of the headset, with a chime: on the rig, so only while VR runs.
        private static void Popups(Transform rig, XROrigin origin, FireDrillScorer scorer, BadgeIcons icons)
        {
            Transform t = rig.Find("ScorePopups");
            if (t == null)
            {
                t = new GameObject("ScorePopups").transform;
                Undo.RegisterCreatedObjectUndo(t.gameObject, "Score popups");
                t.SetParent(rig, false);
            }
            var chime = GetOrAdd<AudioSource>(t.gameObject);
            chime.playOnAwake = false;
            chime.spatialBlend = 0f;
            chime.volume = 0.6f;
            var popups = GetOrAdd<ScorePopups>(t.gameObject);
            var so = new SerializedObject(popups);
            so.FindProperty("scorer").objectReferenceValue = scorer;
            so.FindProperty("head").objectReferenceValue = origin.Camera.transform;
            so.FindProperty("chimeSource").objectReferenceValue = chime;
            so.FindProperty("badgeIcons").objectReferenceValue = icons;
            so.ApplyModifiedProperties();
        }

        // The drill HUD: a small world panel on the rig that follows the view, low and ahead (1000 x 340 px, 0.55 m wide).
        private const string HudUxml = "Assets/_Project/UI/Vr/FireHud.uxml";

        private static void Hud(Transform rig, XROrigin origin, CellContext context, FireDrillScorer scorer,
            FireExtinguisher extinguisher, BadgeIcons icons, List<Object> scenarios)
        {
            Transform old = rig.Find("FireHud");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var go = new GameObject("FireHud") { layer = LayerMask.NameToLayer("Ignore Raycast") };
            Undo.RegisterCreatedObjectUndo(go, "Fire HUD");
            go.transform.SetParent(rig, false);
            go.transform.localPosition = new Vector3(0f, 1.2f, 0.85f);
            UIDocument document = XrSetup.Document(go, XrSetup.WorldPanel(), HudUxml, 1000f, 340f, 0.055f);
            var hud = go.AddComponent<FireHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("document").objectReferenceValue = document;
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("scorer").objectReferenceValue = scorer;
            so.FindProperty("extinguisher").objectReferenceValue = extinguisher;
            so.FindProperty("icons").objectReferenceValue = icons;
            so.FindProperty("head").objectReferenceValue = origin.Camera.transform;
            SetList(so.FindProperty("scenarios"), scenarios);
            so.ApplyModifiedProperties();
        }

        // Material Icons (Apache-2.0, docs/LICENSES.md), white, tinted by the UI.
        private const string IconFolder = "Assets/_Project/Art/Icons";
        private const string BadgeIconsPath = "Assets/_Project/Data/BadgeIcons.asset";

        /// <summary>The badge and HUD icons: import settings for UI, and the asset that maps badges to icons.</summary>
        internal static BadgeIcons BadgeIconsAsset()
        {
            Texture2D Load(string name)
            {
                string path = $"{IconFolder}/ms_{name}.png";
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    bool changed = importer.textureType != TextureImporterType.Default || importer.mipmapEnabled
                        || !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp
                        || importer.alphaSource != TextureImporterAlphaSource.FromInput;
                    if (changed)
                    {
                        importer.textureType = TextureImporterType.Default;
                        importer.alphaSource = TextureImporterAlphaSource.FromInput;
                        importer.alphaIsTransparency = true;
                        importer.mipmapEnabled = false;
                        importer.wrapMode = TextureWrapMode.Clamp;
                        importer.filterMode = FilterMode.Bilinear;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                    }
                }
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                    Debug.LogWarning($"[FireXrSetup] Missing icon {path}.");
                return texture;
            }

            var gold = new Color(1f, 0.78f, 0.27f);
            (string, Texture2D, Color)[] badges =
            {
                (FireDrillScore.BadgeAlarmFirst, Load("notifications_active"), new Color(1f, 0.42f, 0.36f)),
                (FireDrillScore.BadgeRightClass, Load("fire_extinguisher"), new Color(1f, 0.33f, 0.3f)),
                (FireDrillScore.BadgeCleanKnockdown, Load("local_fire_department"), new Color(1f, 0.6f, 0.2f)),
                (FireDrillScore.BadgeSharpAim, Load("my_location"), new Color(0.38f, 0.78f, 1f)),
                (FireDrillScore.BadgeKnewWhenToLeave, Load("directions_run"), new Color(0.35f, 0.9f, 0.55f)),
                (FireDrillScore.BadgePerfectDrill, Load("workspace_premium"), gold),
                (FireDrillScore.BadgeFireMarshal, Load("military_tech"), gold),
            };
            var asset = AssetDatabase.LoadAssetAtPath<BadgeIcons>(BadgeIconsPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BadgeIcons>();
                AssetDatabase.CreateAsset(asset, BadgeIconsPath);
            }
            asset.Configure(badges, Load("emoji_events"), Load("timer"), Load("local_fire_department"),
                Load("fire_extinguisher"), Load("check_circle"), Load("radio_button_unchecked"));
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // The score board beside the stand, facing the trainee's spot like the stand (900 x 600 px, 0.9 x 0.6 m).
        private const string BoardUxml = "Assets/_Project/UI/Vr/FireScoreBoard.uxml";

        private static void ScoreBoard(Transform station, FireDrillScorer scorer, BadgeIcons icons)
        {
            Transform old = station.Find("ScoreBoard");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var go = new GameObject("ScoreBoard");
            Undo.RegisterCreatedObjectUndo(go, "Score board");
            go.transform.SetParent(station, false);
            // The document's front faces -Z; turned round it faces +Z, the way the stand does.
            go.transform.SetLocalPositionAndRotation(new Vector3(1.3f, 1.35f, 0f), Quaternion.Euler(0f, 180f, 0f));
            UIDocument document = XrSetup.Document(go, XrSetup.WorldPanel(), BoardUxml, 900f, 600f, 0.1f);
            XrSetup.Backing(go, 900f, 600f, 12f, 0.02f);
            var board = go.AddComponent<FireScoreBoard>();
            var so = new SerializedObject(board);
            so.FindProperty("document").objectReferenceValue = document;
            so.FindProperty("scorer").objectReferenceValue = scorer;
            so.FindProperty("badgeIcons").objectReferenceValue = icons;
            so.ApplyModifiedProperties();
        }

        // The first version put three extinguishers in the right hand; take those out.
        private static void RemoveOldHandSetup(Transform rig)
        {
            Transform right = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Right Controller");
            if (right == null)
                return;
            foreach (Transform child in right.Cast<Transform>().Where(t => t.name.StartsWith("Held_")).ToList())
                Undo.DestroyObjectImmediate(child.gameObject);
            var old = right.GetComponent<FireExtinguisher>();
            if (old != null)
                Undo.DestroyObjectImmediate(old);
        }

        private static ExtinguisherUnit Unit(Transform group, FireAgent agent)
        {
            Transform body = group.Find("Body");
            Transform pin = group.Find("Pin");
            Transform nozzle = group.Find("Nozzle");
            Transform lever = group.Find("Lever");
            Transform hose = group.Find("Hose");

            // Carry handle: grabbed only round the handle and lever (so the pin and the nozzle stay reachable), the
            // fist closing on both, thumb forward along the handle, the body hanging below. Grip frames follow
            // HandPoseMath (+Z thumbward through the fist, +X the palm normal: towards the hand on the right, away
            // from it on the left), so the left hand gets its own, mirrored attach.
            RemoveCollider(body);
            var handle = GetOrAdd<BoxCollider>(Attach(group, "HandleGrab", HandleCentre).gameObject);
            handle.size = new Vector3(0.05f, 0.05f, 0.14f);
            Transform rightGrip = Attach(group, "Grip", HandPoseMath.ControllerAttach(new Pose(HandleCentre, Quaternion.LookRotation(Vector3.forward, Vector3.left))));
            Transform leftGrip = Attach(group, "GripLeft", HandPoseMath.ControllerAttach(new Pose(HandleCentre, Quaternion.LookRotation(Vector3.forward, Vector3.right))));
            HandedGrabInteractable bodyGrab = Grab(group.gameObject, rightGrip, leftGrip, handle);

            // Pin: a pinch on its ring; the drawn hand moves onto the ring (a right-hand frame, HandGrip turns it for
            // the left) and the pull is measured from the controller.
            var pinCollider = GetOrAdd<SphereCollider>(pin.gameObject);
            pinCollider.center = PinRing;
            pinCollider.radius = 0.025f;
            var pinSimple = GetOrAdd<XRSimpleInteractable>(pin.gameObject);
            pinSimple.colliders.Clear();
            pinSimple.colliders.Add(pinCollider);
            Transform pinAnchor = Attach(pin, "HandAnchor", new Pose(PinRing + new Vector3(-0.055f, -0.012f, 0f), Quaternion.LookRotation(Vector3.up, Vector3.forward)));

            // Nozzle: held round its rear half, the same for either hand (it is round). The CO2 horn by the handle
            // under its rear.
            Collider nozzleCollider = BoxOn(nozzle);
            float length = MeshLength(nozzle);
            // Aimed like a pistol: the nozzle's +Z lines up with the controller's own forward (where its ray points),
            // not with the fist's thumb axis. The torch grip left the tip on the thumb side, pointing up from a
            // natural hand, and hard to aim (seen in the headset, 2026-11-04).
            Vector3 heldAt = agent == FireAgent.Co2 ? HornHandle : new Vector3(0f, 0f, -length * 0.55f);
            Transform nozzleAttach = Attach(nozzle, "Grip", new Pose(heldAt, Quaternion.identity));
            HandedGrabInteractable nozzleGrab = Grab(nozzle.gameObject, nozzleAttach, null, nozzleCollider);

            // The hose line from the valve, used while the nozzle is in a hand.
            Transform start = Attach(group, "HoseStart", group.InverseTransformPoint(HighestVertex(hose)));
            Transform lineGo = group.Find("HoseLine") ?? new GameObject("HoseLine").transform;
            lineGo.SetParent(group, false);
            var line = GetOrAdd<LineRenderer>(lineGo.gameObject);
            line.useWorldSpace = true;
            line.positionCount = HosePoints;
            line.widthMultiplier = HoseWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer hoseRenderer = hose.GetComponentInChildren<Renderer>(true);
            line.sharedMaterial = hoseRenderer != null ? hoseRenderer.sharedMaterial : null;
            line.enabled = false;

            // How the hands close on each part (the rig's hands take a held part's HandGrip).
            Curls(group.gameObject, FingerCurls.Handle);                                        // carry handle and lever
            Curls(nozzle.gameObject, new FingerCurls(0.6f, 0.8f, 0.85f, 0.88f, 0.9f));        // round the hose nozzle
            Curls(pin.gameObject, new FingerCurls(0.72f, 0.78f, 0.45f, 0.5f, 0.55f), pinAnchor); // a pinch on the ring

            // The discharge hiss and the pin's click, from the unit.
            var sound = GetOrAdd<AudioSource>(group.gameObject);
            sound.playOnAwake = false;
            sound.loop = true;
            sound.spatialBlend = 1f;
            sound.minDistance = 0.6f;
            sound.maxDistance = 15f;
            sound.rolloffMode = AudioRolloffMode.Linear;
            sound.volume = agent == FireAgent.Water ? 0.45f : 0.6f;

            var unit = GetOrAdd<ExtinguisherUnit>(group.gameObject);
            var so = new SerializedObject(unit);
            so.FindProperty("agent").intValue = (int)agent;
            so.FindProperty("body").objectReferenceValue = bodyGrab;
            so.FindProperty("pin").objectReferenceValue = pinSimple;
            so.FindProperty("nozzle").objectReferenceValue = nozzleGrab;
            so.FindProperty("lever").objectReferenceValue = lever;
            so.FindProperty("hoseMesh").objectReferenceValue = hose.gameObject;
            so.FindProperty("hoseLine").objectReferenceValue = line;
            so.FindProperty("hoseStart").objectReferenceValue = start;
            so.FindProperty("nozzleLength").floatValue = length;
            so.FindProperty("sound").objectReferenceValue = sound;
            so.FindProperty("hissBrightness").floatValue = agent == FireAgent.Co2 ? 0.8f : agent == FireAgent.Water ? 0.2f : 0.45f;
            so.ApplyModifiedProperties();
            return unit;
        }

        private static void Curls(GameObject go, FingerCurls curls, Transform anchor = null)
        {
            var grip = GetOrAdd<HandGrip>(go);
            var so = new SerializedObject(grip);
            so.FindProperty("anchor").objectReferenceValue = anchor;
            SerializedProperty c = so.FindProperty("curls");
            c.FindPropertyRelative("thumb").floatValue = curls.thumb;
            c.FindPropertyRelative("index").floatValue = curls.index;
            c.FindPropertyRelative("middle").floatValue = curls.middle;
            c.FindPropertyRelative("ring").floatValue = curls.ring;
            c.FindPropertyRelative("little").floatValue = curls.little;
            so.ApplyModifiedProperties();
        }

        // Where the fist closes on the carry handle and lever, in the extinguisher's frame (its origin is the old
        // grip point; the handle runs front to back 2 cm below it, the lever just above). From fire_extinguishers.py.
        private static readonly Vector3 HandleCentre = new Vector3(0f, -0.012f, 0.014f);
        // The pin's ring, in the pin's frame; the CO2 horn's handle, in the nozzle's frame (tip at the origin).
        private static readonly Vector3 PinRing = new Vector3(-0.012f, 0f, 0f);
        private static readonly Vector3 HornHandle = new Vector3(0f, -0.035f, -0.22f);

        // Moved straight with the hand, never thrown, on a kinematic body; only its own collider counts for it.
        private static HandedGrabInteractable Grab(GameObject go, Transform attach, Transform leftAttach, Collider collider)
        {
            var body = GetOrAdd<Rigidbody>(go);
            body.isKinematic = true;
            body.useGravity = false;
            // A plain grab from an earlier build is replaced by the handed one.
            var plain = go.GetComponent<XRGrabInteractable>();
            if (plain != null && !(plain is HandedGrabInteractable))
                Undo.DestroyObjectImmediate(plain);
            var grab = GetOrAdd<HandedGrabInteractable>(go);
            var grabSo = new SerializedObject(grab);
            grabSo.FindProperty("leftAttachTransform").objectReferenceValue = leftAttach;
            grabSo.ApplyModifiedProperties();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = false;
            grab.retainTransformParent = true;
            grab.attachTransform = attach;
            grab.colliders.Clear();
            grab.colliders.Add(collider);
            return grab;
        }

        private static Collider BoxOn(Transform part)
        {
            Transform model = part.Find("Model") ?? part;
            return GetOrAdd<BoxCollider>(model.gameObject);
        }

        private static Transform Attach(Transform parent, string name, Vector3 localPosition) =>
            Attach(parent, name, new Pose(localPosition, Quaternion.identity));

        private static Transform Attach(Transform parent, string name, Pose local)
        {
            Transform t = parent.Find(name);
            if (t == null)
                t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetLocalPositionAndRotation(local.position, local.rotation);
            return t;
        }

        private static void RemoveCollider(Transform part)
        {
            Transform model = part.Find("Model") ?? part;
            var collider = model.GetComponent<Collider>();
            if (collider != null)
                Undo.DestroyObjectImmediate(collider);
        }

        private static float MeshLength(Transform part)
        {
            Renderer r = part.GetComponentInChildren<Renderer>(true);
            if (r == null)
                return 0.07f;
            Vector3 s = r.bounds.size;
            return Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        }

        private static Vector3 HighestVertex(Transform part)
        {
            MeshFilter filter = part.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null)
                return part.position;
            Vector3 best = new Vector3(0f, float.NegativeInfinity, 0f);
            foreach (Vector3 v in filter.sharedMesh.vertices)
            {
                Vector3 w = filter.transform.TransformPoint(v);
                if (w.y > best.y)
                    best = w;
            }
            return best;
        }

        private static void SetList(SerializedProperty list, List<Object> values)
        {
            list.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }
    }
}
