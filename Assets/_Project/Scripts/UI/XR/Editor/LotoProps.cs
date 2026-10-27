using System.Collections.Generic;
using System.Linq;
using TMPro;
using TwoDude.FactoryTwin.Twin;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): builds the hands-on lockout props on the training points, for Build VR Rig. The CNC isolator
    /// (grip and twist), the hasp with a lock board and a padlock and tag (open the lock, hang it in the hasp), the air-dump
    /// valve (grip and twist) and the spindle with two tools and a cart (swap the tool). Every part is named as in
    /// the modelling brief with its origin on its pivot. When a model in Assets/_Project/Art/LOTO holds an object of
    /// the same name, its mesh is used; otherwise a placeholder primitive stands in. Run Build VR Rig again after
    /// adding or changing a model.
    /// </summary>
    internal static class LotoProps
    {
        private const string ModelFolder = "Assets/_Project/Art/LOTO";
        private const string PlaceholderFolder = ModelFolder + "/Placeholders";
        private const string PropName = "Prop";
        private const string SocketLayer = "Ignore Raycast";

        /// <summary>The training points that get a hands-on prop instead of a point-and-click target in VR.</summary>
        public static readonly TrainingPointKind[] Physical =
        {
            TrainingPointKind.Isolator, TrainingPointKind.LockHasp, TrainingPointKind.AirDumpValve, TrainingPointKind.ToolChange,
        };

        private static readonly Color Red = new Color(0.85f, 0.08f, 0.06f);
        private static readonly Color Yellow = new Color(1f, 0.78f, 0.05f);
        private static readonly Color Steel = new Color(0.62f, 0.64f, 0.66f);
        private static readonly Color Grey = new Color(0.3f, 0.32f, 0.34f);
        private static readonly Color Blue = new Color(0.15f, 0.35f, 0.75f);
        private static readonly Color White = new Color(0.94f, 0.94f, 0.92f);

        // Where each grabbed prop sits in the palm (grip frames: +Z through the fist toward the thumb, +X the palm's
        // normal). The padlock: body in the palm, broad face against it, shackle out past the thumb. A tool: the
        // holder in the fist, the tool's top toward the thumb.
        private static readonly Pose PadlockGrip = new Pose(new Vector3(0f, -0.0535f, 0f), Quaternion.LookRotation(Vector3.up, Vector3.right));
        private static readonly Pose ToolGrip = new Pose(new Vector3(0f, -0.07f, 0f), Quaternion.LookRotation(Vector3.up, Vector3.back));

        private static Dictionary<string, Transform> _models;
        private static Dictionary<Color, Material> _materials;

        public static void Build(Transform cell, VrHud hud, CellContext context)
        {
            _models = LoadModels();
            _materials = new Dictionary<Color, Material>();
            Dictionary<TrainingPointKind, TrainingPoint> points = cell.GetComponentsInChildren<TrainingPoint>(true)
                .GroupBy(p => p.Kind).ToDictionary(g => g.Key, g => g.First());
            foreach (TrainingPointKind kind in Physical)
            {
                if (!points.ContainsKey(kind))
                {
                    Debug.LogWarning($"[LotoProps] No {kind} training point; run Build Training Points first.");
                    return;
                }
            }

            TrainingPoint isolator = points[TrainingPointKind.Isolator];
            Isolator(Strip(isolator), isolator, hud, context);
            TrainingPoint hasp = points[TrainingPointKind.LockHasp];
            Hasp(Strip(hasp), hasp, isolator.transform, hud, context);
            TrainingPoint valve = points[TrainingPointKind.AirDumpValve];
            Valve(Strip(valve), valve, hud, context);
            TrainingPoint tool = points[TrainingPointKind.ToolChange];
            ToolChange(Strip(tool), tool, hud);
            Debug.Log($"[LotoProps] Lockout props built; {_models.Count} model object(s) found in {ModelFolder}, placeholders for the rest.");
        }

        // The CNC's main isolator: a yellow box with a red handle on the east face (+X), up for ON, a quarter turn
        // to the south for OFF, and the lock tab under the handle's hub.
        private static void Isolator(Transform prop, TrainingPoint point, VrHud hud, CellContext context)
        {
            Part(prop, "Isolator_Box", Vector3.zero, PrimitiveType.Cube, new Vector3(0.12f, 0.20f, 0.15f), Vector3.zero, Yellow);
            Transform handle = Part(prop, "Isolator_Handle", new Vector3(0.065f, 0f, 0f), PrimitiveType.Cube,
                new Vector3(0.03f, 0.11f, 0.03f), new Vector3(0.015f, 0.035f, 0f), Red);
            Part(prop, "Isolator_LockTab", new Vector3(0.065f, -0.075f, 0f), PrimitiveType.Cube,
                new Vector3(0.006f, 0.03f, 0.03f), Vector3.zero, Steel, collider: false);
            XRSimpleInteractable interactable = Interactable<XRSimpleInteractable>(handle.gameObject);
            Turn(handle, interactable, point, context, TurnBinding.Isolator);
            Prompt(handle.gameObject, interactable, point, hud);
            HandOn(handle, new Vector3(0.018f, 0.045f, 0f), FingerCurls.Handle);
            Highlight(point, Red, handle);
        }

        // The hasp hangs from the isolator's lock tab, a steel plate facing east with three lock holes along its foot;
        // the lock board with its peg is on the CNC's face just south of the isolator; the padlock, its tag tied to the
        // shackle, starts on the peg. The padlock's origin is the top of its shackle: in the hasp's middle hole the
        // shackle's wire runs through the hole (3 mm above it), the body hanging below the plate.
        private static void Hasp(Transform prop, TrainingPoint point, Transform isolator, VrHud hud, CellContext context)
        {
            Vector3 tab = prop.InverseTransformPoint(isolator.TransformPoint(new Vector3(0.065f, -0.075f, 0f)));
            Part(prop, "Hasp_Jaw", tab, PrimitiveType.Cube, new Vector3(0.004f, 0.02f, 0.02f), new Vector3(0f, -0.009f, 0f), Steel, collider: false);
            Transform body = Part(prop, "Hasp_Body", tab, PrimitiveType.Cube, new Vector3(0.004f, 0.085f, 0.045f), new Vector3(0f, -0.0625f, 0f), Red);
            KeyedSocket haspSocket = Socket(prop, "Hasp_Socket", tab + new Vector3(0f, -0.087f, 0f), Quaternion.identity, "padlock", 0.05f, PadlockGrip);

            Vector3 board = tab + new Vector3(-0.05f, 0.04f, -0.3f);
            Part(prop, "LockBoard", board, PrimitiveType.Cube, new Vector3(0.01f, 0.18f, 0.14f), Vector3.zero, Grey, collider: false);
            // The peg sticks out east; the lock hangs on it turned a quarter, its shackle round the peg.
            KeyedSocket peg = Socket(prop, "LockBoard_Peg", board + new Vector3(0.03f, 0.03f, 0f), Quaternion.Euler(0f, 90f, 0f), "padlock", 0.05f, PadlockGrip);

            var padlock = new GameObject("Padlock");
            padlock.transform.SetParent(prop, false);
            padlock.transform.SetLocalPositionAndRotation(peg.transform.localPosition, peg.transform.localRotation);
            Transform shackle = Part(padlock.transform, "Padlock_Shackle", Vector3.zero, PrimitiveType.Cube, new Vector3(0.031f, 0.03f, 0.006f), new Vector3(0f, -0.012f, 0f), Steel);
            Transform lockBody = Part(padlock.transform, "Padlock_Body", new Vector3(0f, -0.03f, 0f), PrimitiveType.Cube,
                new Vector3(0.038f, 0.045f, 0.02f), new Vector3(0f, -0.0225f, 0f), Red);
            Part(padlock.transform, "Tag_Tie", Vector3.zero, PrimitiveType.Cube, new Vector3(0.002f, 0.07f, 0.002f), new Vector3(0.022f, -0.05f, 0f), White, collider: false);
            Transform tag = Part(padlock.transform, "Tag", new Vector3(0.024f, -0.08f, 0f), PrimitiveType.Cube, new Vector3(0.002f, 0.145f, 0.075f), new Vector3(0f, -0.0725f, 0f), White);
            TagText(tag);
            XRGrabInteractable grab = Grabbable(padlock, "padlock", peg, PadlockGrip, new FingerCurls(0.6f, 0.72f, 0.8f, 0.85f, 0.85f));
            peg.startingSelectedInteractable = grab;
            Prompt(padlock, grab, point, hud);
            var shackleMotion = padlock.AddComponent<PadlockShackle>();
            Set(shackleMotion, ("grab", grab), ("shackle", shackle));

            var lockSocket = prop.gameObject.AddComponent<XrLockSocket>();
            Set(lockSocket, ("context", context), ("point", point), ("lockSocket", haspSocket), ("homeSocket", peg), ("padlock", grab));
            Highlight(point, Red, lockBody, body);
        }

        // The air-dump valve, low on the CNC's east face: lever up to supply air, a quarter turn south to exhaust it.
        private static void Valve(Transform prop, TrainingPoint point, VrHud hud, CellContext context)
        {
            Part(prop, "Valve_Body", Vector3.zero, PrimitiveType.Cube, new Vector3(0.06f, 0.08f, 0.10f), Vector3.zero, Blue);
            Transform lever = Part(prop, "Valve_Lever", new Vector3(0.035f, 0f, 0f), PrimitiveType.Cube,
                new Vector3(0.02f, 0.10f, 0.02f), new Vector3(0.01f, 0.05f, 0f), Red);
            Part(prop, "Valve_Silencer", new Vector3(0f, -0.06f, 0f), PrimitiveType.Cylinder, new Vector3(0.025f, 0.015f, 0.025f), Vector3.zero, Grey, collider: false);
            XRSimpleInteractable interactable = Interactable<XRSimpleInteractable>(lever.gameObject);
            Turn(lever, interactable, point, context, TurnBinding.AirValve);
            Prompt(lever.gameObject, interactable, point, hud);
            HandOn(lever, new Vector3(0.008f, 0.075f, 0f), new FingerCurls(0.55f, 0.78f, 0.82f, 0.85f, 0.85f));
            Highlight(point, Red, lever);
        }

        // The spindle nose just in front of the CNC's front (-Z), holding the worn tool; the new one waits on a cart
        // east of the CNC, outside both safety zones, with an empty slot for the worn one.
        private static void ToolChange(Transform prop, TrainingPoint point, VrHud hud)
        {
            Part(prop, "Spindle_Nose", new Vector3(0f, 0.12f, -0.09f), PrimitiveType.Cylinder, new Vector3(0.07f, 0.015f, 0.07f), Vector3.zero, Steel, collider: false);
            KeyedSocket spindle = Socket(prop, "Spindle_Socket", new Vector3(0f, 0.105f, -0.09f), Quaternion.identity, "tool", 0.06f, ToolGrip);

            Vector3 cart = new Vector3(0.5f, -prop.position.y, -0.55f);
            Part(prop, "ToolCart", cart, PrimitiveType.Cube, new Vector3(0.45f, 0.9f, 0.3f), new Vector3(0f, 0.45f, 0f), Grey, collider: false);
            // A tool's origin is at its top; a slot holds it standing on the cart's top, 0.9 m up.
            KeyedSocket slotA = Socket(prop, "Cart_SlotA", cart + new Vector3(-0.1f, 0.9f + 0.14f, 0f), Quaternion.identity, "tool", 0.05f, ToolGrip);
            KeyedSocket slotB = Socket(prop, "Cart_SlotB", cart + new Vector3(0.1f, 0.9f + 0.14f, 0f), Quaternion.identity, "tool", 0.05f, ToolGrip);

            XRGrabInteractable worn = Tool(prop, "Tool_A", spindle.transform.localPosition, slotA, hud, point, out Transform wornHolder);
            XRGrabInteractable fresh = Tool(prop, "Tool_B", slotB.transform.localPosition, slotB, hud, point, out Transform freshHolder);
            spindle.startingSelectedInteractable = worn;
            slotB.startingSelectedInteractable = fresh;

            var change = prop.gameObject.AddComponent<XrToolChange>();
            Set(change, ("point", point), ("spindle", spindle));
            Highlight(point, new Color(0.1f, 0.8f, 0.85f), wornHolder, freshHolder);
        }

        // The tag's words on its east face below the red header (DANGER, DO NOT OPERATE, the trainee's name), set at
        // run time by TagLabel. TextMeshPro, not TextMesh: the legacy font material ignores depth, so the words
        // showed through the card, mirrored, from behind.
        private static void TagText(Transform tag)
        {
            // A plain transform carries the place on the card: a RectTransform under a non-UI parent keeps only its
            // anchored position, which put the words inside the card.
            var holder = new GameObject("Tag_Text");
            holder.transform.SetParent(tag, false);
            holder.transform.SetLocalPositionAndRotation(new Vector3(0.0012f, -0.023f, -0.033f), Quaternion.Euler(0f, -90f, 0f));
            var go = new GameObject("Words");
            go.transform.SetParent(holder.transform, false);
            var text = go.AddComponent<TextMeshPro>();
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.rectTransform.sizeDelta = new Vector2(0.066f, 0.112f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.text = TagLabel.Words("Trainee Name");
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.02f;
            text.fontSizeMax = 0.32f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = new Color(0.7f, 0.05f, 0.03f);
            var label = go.AddComponent<TagLabel>();
            Set(label, ("training", Object.FindAnyObjectByType<TrainingController>()));
        }

        private static XRGrabInteractable Tool(Transform prop, string name, Vector3 at, XRSocketInteractor home, VrHud hud, TrainingPoint point, out Transform holder)
        {
            var tool = new GameObject(name);
            tool.transform.SetParent(prop, false);
            tool.transform.localPosition = at;
            holder = Part(tool.transform, "Tool_Holder", Vector3.zero, PrimitiveType.Cylinder, new Vector3(0.04f, 0.07f, 0.04f), new Vector3(0f, -0.07f, 0f), Steel);
            XRGrabInteractable grab = Grabbable(tool, "tool", home, ToolGrip, new FingerCurls(0.5f, 0.62f, 0.66f, 0.7f, 0.72f));
            Prompt(tool, grab, point, hud);
            return grab;
        }

        // Takes the point's own click target off (its cube and trigger box) and gives it a fresh prop container. Desktop
        // clicks still find the point through the prop's colliders (TraineeInteractor looks up the parents).
        private static Transform Strip(TrainingPoint point)
        {
            GameObject go = point.gameObject;
            foreach (Component c in new Component[] { go.GetComponent<MeshRenderer>(), go.GetComponent<MeshFilter>(), go.GetComponent<Collider>() })
            {
                if (c != null)
                    Undo.DestroyObjectImmediate(c);
            }
            Transform old = go.transform.Find(PropName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var prop = new GameObject(PropName);
            Undo.RegisterCreatedObjectUndo(prop, "Build VR Rig");
            prop.transform.SetParent(go.transform, false);
            // The point primitives were scaled cubes; the prop is built in metres from the point's position.
            go.transform.localScale = Vector3.one;
            go.transform.rotation = Quaternion.identity;
            return prop.transform;
        }

        // A part: a transform at its pivot holding either the model's mesh (under "Model", with the model's own
        // rotation and scale so Blender's axes come out right) or a placeholder primitive offset from the pivot.
        private static Transform Part(Transform parent, string name, Vector3 pivot, PrimitiveType shape, Vector3 size, Vector3 offset,
            Color color, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pivot;
            GameObject visual;
            if (_models.TryGetValue(name, out Transform source))
            {
                visual = new GameObject("Model");
                visual.transform.SetParent(go.transform, false);
                visual.transform.localRotation = source.localRotation;
                visual.transform.localScale = source.localScale;
                visual.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                visual.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                if (collider)
                    visual.AddComponent<BoxCollider>();
            }
            else
            {
                visual = GameObject.CreatePrimitive(shape);
                visual.name = "Placeholder";
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = offset;
                visual.transform.localScale = size;
                visual.GetComponent<Renderer>().sharedMaterial = Placeholder(color);
                Collider c = visual.GetComponent<Collider>();
                if (!collider)
                    Object.DestroyImmediate(c);
                else if (shape == PrimitiveType.Cylinder)
                {
                    // The primitive's capsule overhangs a short cylinder; a box fits it.
                    Object.DestroyImmediate(c);
                    visual.AddComponent<BoxCollider>();
                }
            }
            return go.transform;
        }

        private static T Interactable<T>(GameObject go) where T : XRBaseInteractable
        {
            var interactable = go.AddComponent<T>();
            interactable.colliders.Clear();
            interactable.colliders.AddRange(go.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger));
            return interactable;
        }

        // Grabbed with the grip, moved instantly with the hand, never thrown; floats until it goes home if dropped.
        // It sits in the palm by its "Grip" child (a grip frame, HandPoseMath.GripInPalm), the same for the controller
        // hands and the tracked hands, and the fingers close on it with the given curls (HandGrip).
        private static XRGrabInteractable Grabbable(GameObject go, string key, XRSocketInteractor home, Pose grip, FingerCurls curls)
        {
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            XRGrabInteractable grab = Interactable<XRGrabInteractable>(go);
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = false;
            var attach = new GameObject("Grip");
            attach.transform.SetParent(go.transform, false);
            Pose pose = HandPoseMath.ControllerAttach(grip);
            attach.transform.SetLocalPositionAndRotation(pose.position, pose.rotation);
            grab.attachTransform = attach.transform;
            SetCurls(go.AddComponent<HandGrip>(), curls, null);
            Set(go.AddComponent<SocketKey>(), ("key", key));
            Set(go.AddComponent<ReturnHome>(), ("grab", grab), ("home", home));
            return grab;
        }

        // A socket on the Ignore Raycast layer: its trigger sphere catches the prop, and the controller rays (which
        // skip that layer) reach the prop inside it.
        // A socket lines the prop's grip frame up with its own attach, so the attach carries the same grip pose:
        // the prop's origin then lands on the socket itself.
        private static KeyedSocket Socket(Transform parent, string name, Vector3 at, Quaternion rotation, string key, float radius, Pose grip)
        {
            var go = new GameObject(name) { layer = LayerMask.NameToLayer(SocketLayer) };
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(at, rotation);
            var attach = new GameObject("Attach");
            attach.transform.SetParent(go.transform, false);
            Pose pose = HandPoseMath.ControllerAttach(grip);
            attach.transform.SetLocalPositionAndRotation(pose.position, pose.rotation);
            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = radius;
            var socket = go.AddComponent<KeyedSocket>();
            socket.attachTransform = attach.transform;
            Set(socket, ("key", key));
            return socket;
        }

        private static void Turn(Transform handle, XRSimpleInteractable interactable, TrainingPoint point, CellContext context, TurnBinding binding)
        {
            var turn = handle.gameObject.AddComponent<XrTurnHandle>();
            var so = new SerializedObject(turn);
            so.FindProperty("context").objectReferenceValue = context;
            so.FindProperty("point").objectReferenceValue = point;
            so.FindProperty("interactable").objectReferenceValue = interactable;
            so.FindProperty("handle").objectReferenceValue = handle;
            so.FindProperty("axis").vector3Value = Vector3.right;
            so.FindProperty("normalAngle").floatValue = 0f;
            so.FindProperty("turnedAngle").floatValue = -90f;
            so.FindProperty("binding").enumValueIndex = (int)binding;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A hand on a fixed part: the controller hand moves onto this anchor while it holds the part, a right-hand grip
        // frame round the vertical bar (thumb up, palm toward the CNC, the trainee standing east of it).
        private static void HandOn(Transform part, Vector3 at, FingerCurls curls)
        {
            var anchor = new GameObject("Hand_Anchor");
            anchor.transform.SetParent(part, false);
            anchor.transform.SetLocalPositionAndRotation(at, Quaternion.LookRotation(Vector3.up, Vector3.back));
            SetCurls(part.gameObject.AddComponent<HandGrip>(), curls, anchor.transform);
        }

        private static void SetCurls(HandGrip grip, FingerCurls curls, Transform anchor)
        {
            var so = new SerializedObject(grip);
            SerializedProperty c = so.FindProperty("curls");
            c.FindPropertyRelative("thumb").floatValue = curls.thumb;
            c.FindPropertyRelative("index").floatValue = curls.index;
            c.FindPropertyRelative("middle").floatValue = curls.middle;
            c.FindPropertyRelative("ring").floatValue = curls.ring;
            c.FindPropertyRelative("little").floatValue = curls.little;
            so.FindProperty("anchor").objectReferenceValue = anchor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Prompt(GameObject go, XRBaseInteractable interactable, TrainingPoint point, VrHud hud)
        {
            Set(go.AddComponent<XrPrompt>(), ("interactable", interactable), ("point", point), ("hud", hud));
        }

        // What lights while the point is hovered or due: the part the trainee takes hold of.
        private static void Highlight(TrainingPoint point, Color color, params Transform[] parts)
        {
            Renderer[] renderers = parts.SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).ToArray();
            var so = new SerializedObject(point);
            so.FindProperty("color").colorValue = color;
            SerializedProperty list = so.FindProperty("highlight");
            list.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, params (string field, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach ((string field, object value) in values)
            {
                SerializedProperty property = so.FindProperty(field);
                if (value is string text)
                    property.stringValue = text;
                else
                    property.objectReferenceValue = (Object)value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Every object in the models under Assets/_Project/Art/LOTO, by name, that carries a mesh.
        private static Dictionary<string, Transform> LoadModels()
        {
            var models = new Dictionary<string, Transform>();
            if (!AssetDatabase.IsValidFolder(ModelFolder))
                return models;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (root == null)
                    continue;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh != null && filter.GetComponent<MeshRenderer>() != null && !models.ContainsKey(filter.name))
                        models.Add(filter.name, filter.transform);
                }
            }
            return models;
        }

        private static Material Placeholder(Color color)
        {
            if (_materials.TryGetValue(color, out Material cached))
                return cached;
            if (!AssetDatabase.IsValidFolder(ModelFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "LOTO");
            if (!AssetDatabase.IsValidFolder(PlaceholderFolder))
                AssetDatabase.CreateFolder(ModelFolder, "Placeholders");
            string path = $"{PlaceholderFolder}/Loto_{ColorUtility.ToHtmlStringRGB(color)}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", color == Steel ? 0.6f : 0.3f);
            material.SetFloat("_Metallic", color == Steel ? 0.8f : 0f);
            EditorUtility.SetDirty(material);
            _materials[color] = material;
            return material;
        }
    }
}
