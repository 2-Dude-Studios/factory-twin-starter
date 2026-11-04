using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Fire safety (M14): builds the fire drill into Cell.unity, repeatably. Writes the six scenario assets
    /// (Assets/_Project/Data/Fire), adds Cell/Fire with the FireStation and the FireAlarmPanel, one effects anchor per
    /// location (Particle Pack flames, smoke and sparks, one fire light), ceiling detector heads, and the drive
    /// cabinet. Running it again updates what is there instead of adding a second copy.
    /// </summary>
    internal static class FireSetup
    {
        private const string CellScene = "Assets/_Project/Scenes/Cell.unity";
        private const string ScenarioFolder = "Assets/_Project/Data/Fire";
        private const string ParticlePack = "Assets/ThirdParty/ParticlePack/EffectExamples/";
        private const string TinyFlames = ParticlePack + "Fire & Explosion Effects/Prefabs/TinyFlames.prefab";
        private const string MediumFlames = ParticlePack + "Fire & Explosion Effects/Prefabs/MediumFlames.prefab";
        private const string LargeFlames = ParticlePack + "Fire & Explosion Effects/Prefabs/LargeFlames.prefab";
        private const string Smoke = ParticlePack + "Smoke & Steam Effects/Prefabs/SmokeEffect.prefab";
        private const string Sparks = ParticlePack + "Misc Effects/Prefabs/ElectricalSparks.prefab";
        private const string CabinetPrefab = "Assets/realvirtual/3DPrefabs/ControlCabinet/ControlCabinet.prefab";
        private const string WasteBinPrefab = "Assets/ThirdParty/FactoryTraining/Prefabs/env/plastic_container_prefab.prefab";
        private const string LampMaterial = "Assets/_Project/Materials/StackLightLamp.mat";

        // Where each fire starts, from a survey of Cell.unity (SceneSurvey, 2026-11-04). The infeed's upstream end
        // and drive are at x 0.4, z 0.65; the warning zone ends at x 0.24; the CNC's door is on its west face at
        // x −1.45, z 3.0–3.8. The drive cabinet stands south-east of the infeed drive, on the trainee's side and
        // outside the warning zone; the waste bin east of the infeed, past the cone line at x 1.65; the CNC fire at
        // its door. The charger fire is placed at the hall's wall chargers when they are found.
        private static readonly Vector3 CabinetAt = new Vector3(0.9f, 0f, -1.3f);
        private const float CabinetYaw = 180f;
        private static readonly Vector3 CncFireAt = new Vector3(-1.55f, 0.75f, 3.4f);
        private static readonly Vector3 WasteAt = new Vector3(2.4f, 0f, 0.4f);
        private static readonly Vector3 ChargersFallback = new Vector3(6f, 0.5f, -6f);
        private const string ChargersName = "wall_chargers";
        private const string ChargerBatteryName = "wall_chargers_battery";

        // The hall's ceiling is at about 8.8 m (its lowest element); detector heads sit just under it.
        private const float CeilingHeight = 8.75f;

        private readonly struct ScenarioDef
        {
            public readonly int Number;
            public readonly string Title;
            public readonly FireLocation Location;
            public readonly FireClass Class;
            public readonly bool Precursor;
            public readonly bool Flee;
            public readonly bool Blocked;
            public readonly FireModelSettings Settings;

            public ScenarioDef(int number, string title, FireLocation location, FireClass cls, bool precursor, bool flee,
                bool blocked, FireModelSettings settings)
            {
                Number = number;
                Title = title;
                Location = location;
                Class = cls;
                Precursor = precursor;
                Flee = flee;
                Blocked = blocked;
                Settings = settings;
            }
        }

        // F1–F6 of docs/fire-safety-training.md section 2. Timings are drill time: long enough to act on, short
        // enough for a recorded session.
        private static IEnumerable<ScenarioDef> Scenarios()
        {
            yield return new ScenarioDef(1, "Drive cabinet overheats", FireLocation.DriveCabinet, FireClass.C, true, false, false,
                new FireModelSettings(precursorSeconds: 40, smokeSeconds: 12, incipientSeconds: 45, growingSeconds: 25));
            yield return new ScenarioDef(2, "Coolant-mist fire at the CNC", FireLocation.Cnc, FireClass.B, false, false, false,
                new FireModelSettings(smokeSeconds: 6, incipientSeconds: 40, growingSeconds: 20));
            yield return new ScenarioDef(3, "Oily waste fire at the infeed", FireLocation.InfeedWaste, FireClass.A, false, false, false,
                new FireModelSettings(smokeSeconds: 10, incipientSeconds: 50, growingSeconds: 25));
            yield return new ScenarioDef(4, "CNC fire grows past incipient", FireLocation.Cnc, FireClass.B, false, false, false,
                new FireModelSettings(smokeSeconds: 5, incipientSeconds: 15, growingSeconds: 12));
            yield return new ScenarioDef(5, "CNC fire grows, primary exit blocked", FireLocation.Cnc, FireClass.B, false, false, true,
                new FireModelSettings(smokeSeconds: 5, incipientSeconds: 15, growingSeconds: 12));
            yield return new ScenarioDef(6, "Lithium-ion fire at the chargers", FireLocation.Chargers, FireClass.C, false, true, false,
                new FireModelSettings(smokeSeconds: 8, incipientSeconds: 10, growingSeconds: 10, knockdownPerSecond: 0.005));
        }

        [MenuItem("Tools/Factory Twin/Build Fire Drill")]
        private static void Build()
        {
            GameObject cell = GameObject.Find("Cell");
            CellContext context = cell != null ? cell.GetComponent<CellContext>() : null;
            if (cell == null || context == null)
            {
                Debug.LogError("[FireSetup] Open Cell.unity first: Cell with its CellContext is needed.");
                return;
            }

            Undo.SetCurrentGroupName("Build Fire Drill");
            int undoGroup = Undo.GetCurrentGroup();

            List<FireScenario> scenarios = WriteScenarios();

            Transform fire = Child(cell.transform, "Fire");
            fire.localPosition = Vector3.zero;
            var station = GetOrAdd<FireStation>(fire.gameObject);
            SetObject(station, "context", context);
            SetArray(station, "scenarios", scenarios.ToArray());

            GameObject cabinet = Cabinet(fire);
            GameObject bin = WasteBin(fire);
            var detectors = new List<FireDetector>();
            Location(fire, context, FireLocation.DriveCabinet, TopOf(cabinet, CabinetAt + new Vector3(0f, 1.8f, 0f), -0.25f), sparks: true, detectors);
            Location(fire, context, FireLocation.Cnc, CncFireAt, sparks: false, detectors);
            Location(fire, context, FireLocation.InfeedWaste, TopOf(bin, WasteAt + new Vector3(0f, 0.5f, 0f), 0f), sparks: false, detectors);
            Location(fire, context, FireLocation.Chargers, Chargers(), sparks: true, detectors);

            var panel = GetOrAdd<FireAlarmPanel>(fire.gameObject);
            SetObject(panel, "context", context);
            SetArray(panel, "detectors", detectors.ToArray());

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(cell.scene);
            foreach (Transform anchor in fire)
            {
                if (anchor.name.StartsWith("Fire_"))
                    Debug.Log($"[FireSetup] {anchor.name} at {anchor.position:F2}");
            }
            Debug.Log($"[FireSetup] Fire drill built: {scenarios.Count} scenarios, 4 locations, {detectors.Count} detector heads.", fire);
        }

        // unity -batchmode -projectPath . -executeMethod TwoDude.FactoryTwin.Twin.FireSetup.BuildFromCommandLine -quit
        public static void BuildFromCommandLine()
        {
            EditorSceneManager.OpenScene(CellScene, OpenSceneMode.Single);
            Build();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        private static List<FireScenario> WriteScenarios()
        {
            if (!AssetDatabase.IsValidFolder(ScenarioFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(ScenarioFolder).Replace('\\', '/'), Path.GetFileName(ScenarioFolder));

            var list = new List<FireScenario>();
            foreach (ScenarioDef def in Scenarios())
            {
                string path = $"{ScenarioFolder}/F{def.Number}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<FireScenario>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<FireScenario>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                asset.Configure(def.Number, def.Title, def.Location, def.Class, def.Precursor, def.Flee, def.Blocked, def.Settings);
                EditorUtility.SetDirty(asset);
                list.Add(asset);
            }
            AssetDatabase.SaveAssets();
            return list;
        }

        // realvirtual's ControlCabinet is a 0.6 m wall box with its pivot at its centre. Scaled to a floor-standing
        // drive cabinet, about 0.8 × 2.0 × 0.4 m, and stood on the floor. A placeholder until our own model (M15/M16).
        private static readonly Vector3 CabinetScale = new Vector3(1.33f, 3.33f, 2.35f);

        private static GameObject Cabinet(Transform fire)
        {
            GameObject cabinet = Prop(fire, "DriveCabinet", CabinetPrefab, CabinetAt, CabinetYaw,
                "realvirtual ControlCabinet prefab is missing; the cabinet fire has no cabinet.");
            if (cabinet == null)
                return null;
            cabinet.transform.localScale = CabinetScale;
            if (TryBounds(cabinet, out Bounds b))
                cabinet.transform.position += new Vector3(0f, -b.min.y, 0f);
            if (TryBounds(cabinet, out b))
                Debug.Log($"[FireSetup] DriveCabinet stood up: bounds {b.min:F2} to {b.max:F2}");
            return cabinet;
        }

        private static GameObject WasteBin(Transform fire) =>
            Prop(fire, "WasteBin", WasteBinPrefab, WasteAt, 0f, "the Factory Training plastic container is missing; the waste fire has no bin.");

        private static GameObject Prop(Transform fire, string name, string prefabPath, Vector3 at, float yaw, string missing)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[FireSetup] " + missing);
                return null;
            }
            // Reuse the prop if it is an instance of this prefab; one left from another prefab is replaced.
            Transform existing = fire.Find(name);
            GameObject prop = existing != null ? existing.gameObject : null;
            if (prop != null && PrefabUtility.GetCorrespondingObjectFromSource(prop) != prefab)
            {
                Undo.DestroyObjectImmediate(prop);
                prop = null;
            }
            if (prop == null)
            {
                prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, fire);
                prop.name = name;
                Undo.RegisterCreatedObjectUndo(prop, name);
            }
            prop.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            if (TryBounds(prop, out Bounds b))
                Debug.Log($"[FireSetup] {name} bounds {b.min:F2} to {b.max:F2}");
            return prop;
        }

        // The top centre of a prop's renderers, lowered by drop; the fallback when there is no prop.
        private static Vector3 TopOf(GameObject prop, Vector3 fallback, float drop)
        {
            if (!TryBounds(prop, out Bounds b))
                return fallback;
            return new Vector3(b.center.x, b.max.y + drop, b.center.z);
        }

        // The hall's wall chargers (Factory Training): the fire starts at a battery unit, the first by name so the
        // choice is the same on every run; any charger object if no battery unit is found.
        private static Vector3 Chargers()
        {
            Transform best = null;
            bool bestIsBattery = false;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!t.name.Contains(ChargersName) || !TryBounds(t.gameObject, out _))
                    continue;
                bool battery = t.name.Contains(ChargerBatteryName);
                if (best == null || (battery && !bestIsBattery)
                    || (battery == bestIsBattery && string.CompareOrdinal(t.name, best.name) < 0))
                {
                    best = t;
                    bestIsBattery = battery;
                }
            }
            if (best == null)
            {
                Debug.LogWarning("[FireSetup] No wall chargers found in the scene; the charger fire uses its fallback position.");
                return ChargersFallback;
            }
            TryBounds(best.gameObject, out Bounds b);
            Debug.Log($"[FireSetup] Charger fire at {best.name}, bounds {b.min:F2} to {b.max:F2}.");
            return new Vector3(b.center.x, Mathf.Min(b.max.y, 0.6f), b.center.z);
        }

        private static bool TryBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            if (go == null)
                return false;
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return false;
            bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
                bounds.Encapsulate(r.bounds);
            return true;
        }

        private static void Location(Transform fire, CellContext context, FireLocation location, Vector3 at, bool sparks,
            List<FireDetector> detectors)
        {
            Transform anchor = Child(fire, "Fire_" + location);
            anchor.position = at;

            ParticleSystem tiny = Effect(anchor, "SmallFlames", TinyFlames, Vector3.zero);
            ParticleSystem medium = Effect(anchor, "MediumFlames", MediumFlames, Vector3.zero);
            ParticleSystem large = Effect(anchor, "LargeFlames", LargeFlames, Vector3.zero);
            ParticleSystem smoke = Effect(anchor, "Smoke", Smoke, new Vector3(0f, 0.3f, 0f));
            ParticleSystem spark = sparks ? Effect(anchor, "Sparks", Sparks, Vector3.zero) : null;
            // The Particle Pack smoke is light steam-grey and reads as nothing against the hall; fire smoke is dark.
            // An override on our instance, not an edit to the Particle Pack asset.
            // It also leans 30° off vertical at 5 m start size, drifting out of view as a faint haze: stand it up
            // and make it denser.
            if (smoke != null)
            {
                ParticleSystem.MainModule main = smoke.main;
                main.startColor = new Color(0.13f, 0.12f, 0.11f, 1f);
                main.startSizeMultiplier = 2.5f;
                smoke.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                EditorUtility.SetDirty(smoke);
                EditorUtility.SetDirty(smoke.transform);
            }
            Light light = FireLight(anchor);

            var effects = GetOrAdd<FireEffects>(anchor.gameObject);
            SetObject(effects, "context", context);
            SetEnum(effects, "location", (int)location);
            SetArray(effects, "smallFlames", tiny != null ? new Object[] { tiny } : new Object[0]);
            SetArray(effects, "mediumFlames", medium != null ? new Object[] { medium } : new Object[0]);
            SetArray(effects, "largeFlames", large != null ? new Object[] { large } : new Object[0]);
            SetArray(effects, "smoke", smoke != null ? new Object[] { smoke } : new Object[0]);
            SetArray(effects, "sparks", spark != null ? new Object[] { spark } : new Object[0]);
            SetObject(effects, "fireLight", light);

            detectors.Add(Detector(fire, "SmokeHead_" + location, DetectorKind.Smoke, location, at, 0.3f, 3f));
            if (location == FireLocation.DriveCabinet || location == FireLocation.Cnc)
                detectors.Add(Detector(fire, "HeatHead_" + location, DetectorKind.Heat, location, at + new Vector3(0.6f, 0f, 0f), 0.25f, 2f));
        }

        private static ParticleSystem Effect(Transform anchor, string name, string prefabPath, Vector3 offset)
        {
            Transform existing = anchor.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[FireSetup] Missing Particle Pack prefab {prefabPath}.");
                    return null;
                }
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
                go.name = name;
                Undo.RegisterCreatedObjectUndo(go, name);
            }
            go.transform.localPosition = offset;
            // Keep the prefab's own rotation: Particle Pack effects are tilted at the root so they emit upward.
            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            go.transform.localRotation = source != null ? source.transform.localRotation : Quaternion.identity;

            // Off until the fire's signals say otherwise: no play on awake, and every system starts stopped.
            foreach (ParticleSystem system in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
                EditorUtility.SetDirty(system);
            }
            foreach (ParticleSystemRenderer renderer in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
                UseHardMaterial(renderer);
            // Not ??: in the Editor a missing component comes back as a fake null object.
            var root = go.GetComponent<ParticleSystem>();
            return root != null ? root : go.GetComponentInChildren<ParticleSystem>(true);
        }

        // Particle Pack's flame and smoke materials fade by the camera depth texture (soft particles). The VR render
        // pipeline (VR_RPAsset) has no depth texture to hold 72 fps, so in the headset they faded to nothing and only
        // the embers showed. Our instances use our own copies with soft particles off; the pack's files are unchanged.
        private const string HardMaterialFolder = "Assets/_Project/Materials/Fire";
        private const string SoftParticlesProperty = "_SoftParticlesEnabled";
        private const string SoftParticlesKeyword = "_SOFTPARTICLES_ON";

        private static void UseHardMaterial(ParticleSystemRenderer renderer)
        {
            Material material = renderer.sharedMaterial;
            if (material == null)
                return;
            bool soft = material.IsKeywordEnabled(SoftParticlesKeyword)
                || (material.HasProperty(SoftParticlesProperty) && material.GetFloat(SoftParticlesProperty) > 0f);
            if (!soft)
                return;

            if (!AssetDatabase.IsValidFolder(HardMaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Fire");
            string path = $"{HardMaterialFolder}/{material.name}_NoSoft.mat";
            var hard = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (hard == null)
            {
                hard = new Material(material) { name = material.name + "_NoSoft" };
                if (hard.HasProperty(SoftParticlesProperty))
                    hard.SetFloat(SoftParticlesProperty, 0f);
                hard.DisableKeyword(SoftParticlesKeyword);
                AssetDatabase.CreateAsset(hard, path);
            }
            renderer.sharedMaterial = hard;
            EditorUtility.SetDirty(renderer);
        }

        private static Light FireLight(Transform anchor)
        {
            Transform t = Child(anchor, "FireLight");
            t.localPosition = new Vector3(0f, 0.5f, 0f);
            var light = GetOrAdd<Light>(t.gameObject);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.range = 5f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        private static FireDetector Detector(Transform fire, string name, DetectorKind kind, FireLocation covers, Vector3 below,
            float threshold, float response)
        {
            Transform head = fire.Find(name);
            if (head == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = name;
                go.transform.SetParent(fire, false);
                Undo.RegisterCreatedObjectUndo(go, name);
                head = go.transform;
            }
            head.position = new Vector3(below.x, CeilingHeight, below.z);
            head.localScale = new Vector3(0.14f, 0.02f, 0.14f);
            var lamp = AssetDatabase.LoadAssetAtPath<Material>(LampMaterial);
            var renderer = head.GetComponent<Renderer>();
            if (lamp != null && renderer != null)
                renderer.sharedMaterial = lamp;

            var detector = GetOrAdd<FireDetector>(head.gameObject);
            SetEnum(detector, "kind", (int)kind);
            var so = new SerializedObject(detector);
            SerializedProperty coverage = so.FindProperty("covers");
            coverage.arraySize = 1;
            coverage.GetArrayElementAtIndex(0).enumValueIndex = EnumIndex<FireLocation>((int)covers);
            so.FindProperty("threshold").floatValue = threshold;
            so.FindProperty("responseSeconds").floatValue = response;
            so.FindProperty("lamp").objectReferenceValue = renderer;
            so.ApplyModifiedProperties();
            return detector;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                return child;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go.transform;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        private static void SetObject(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedProperties();
        }

        private static void SetEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).intValue = value;
            so.ApplyModifiedProperties();
        }

        // SerializedProperty.enumValueIndex is the declaration index, not the value.
        private static int EnumIndex<TEnum>(int value) where TEnum : System.Enum =>
            System.Array.IndexOf(System.Enum.GetValues(typeof(TEnum)), System.Enum.ToObject(typeof(TEnum), value));
    }
}
