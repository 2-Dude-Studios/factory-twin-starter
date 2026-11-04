using System;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): the fire drill page of the tablet in the left hand (VrMenu.uxml). "Fire safety training" on
    /// the main page takes the trainee to the fire training station and swaps the tablet to the drill: start a fire
    /// at any spot in the hall (or at random), take an extinguisher from the stand by hand (ExtinguisherUnit: handle, pin, hose, lever),
    /// go to the fire, raise the alarm, end the drill. The display shows the fire, the extinguisher in hand and one
    /// line of advice (<see cref="FireDrillText"/>). Commands go out on the bus; it reads the bus for what it shows.
    /// </summary>
    public sealed class VrFireMenu : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;
        [SerializeField] private FireExtinguisher extinguisher;
        [SerializeField] private XROrigin origin;
        [Tooltip("The fires the drill can start, one button each (F1–F9 assets in Assets/_Project/Data/Fire).")]
        [SerializeField] private FireScenario[] scenarios = Array.Empty<FireScenario>();
        [Tooltip("The fire training station: where the trainee stands to start, facing the extinguisher stand.")]
        [SerializeField] private Vector3 stationFloor = new Vector3(-4.8f, 0f, -12.6f);
        [SerializeField] private float stationYaw = 180f;
        [Tooltip("How far back from the base of the fire 'Go to the fire' puts the trainee.")]
        [SerializeField, Min(1f)] private float standOffMetres = 3f;
        [SerializeField, Min(0.05f)] private float refreshSeconds = 0.2f;

        private VisualElement _root, _mainOptions, _fireOptions, _hud, _fireHud, _starts;
        private Label _fireLine, _extLine, _advice;
        private readonly List<(Button button, Action action)> _wired = new List<(Button, Action)>();
        private bool _inDrill;
        private float _nextRefresh;
        private System.Random _random;

        private void OnEnable()
        {
            _root = document != null ? document.rootVisualElement : null;
            if (_root == null || context == null)
                return;
            _mainOptions = _root.Q("main-options");
            _fireOptions = _root.Q("fire-options");
            _hud = _root.Q("hud");
            _fireHud = _root.Q("fire-hud");
            _starts = _root.Q("fire-starts");
            _fireLine = _root.Q<Label>("fire-line");
            _extLine = _root.Q<Label>("fire-ext");
            _advice = _root.Q<Label>("fire-advice");
            _random ??= new System.Random();

            Wire("menu-fire", EnterDrill);
            Wire("ext-none", PutBack);
            Wire("fire-random", StartRandom);
            Wire("fire-goto", GoToFire);
            Wire("fire-station", GoToStation);
            Wire("fire-alarm", RaiseAlarm);
            Wire("fire-end", EndDrill);
            BuildStartButtons();
            ShowDrill(_inDrill);
        }

        private void OnDisable()
        {
            foreach ((Button button, Action action) in _wired)
                button.clicked -= action;
            _wired.Clear();
            _starts?.Clear();
        }

        private void Update()
        {
            if (!_inDrill || Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + refreshSeconds;
            Refresh();
        }

        private void BuildStartButtons()
        {
            if (_starts == null)
                return;
            _starts.Clear();
            foreach (FireScenario scenario in scenarios)
            {
                if (scenario == null)
                    continue;
                FireScenario s = scenario;
                var button = new Button { text = StartLabel(s), name = "fire-start-" + s.Number };
                button.AddToClassList("menu__button");
                button.AddToClassList("tablet__half");
                Action action = () => StartFire(s);
                button.clicked += action;
                _wired.Add((button, action));
                _starts.Add(button);
            }
        }

        // Short, to fit two to a row on the tablet: where, and the class (or Li-ion for the flee-only battery fire).
        private static string StartLabel(FireScenario s)
        {
            string where;
            switch (s.Location)
            {
                case FireLocation.DriveCabinet: where = "Cabinet"; break;
                case FireLocation.Cnc: where = "CNC"; break;
                case FireLocation.InfeedWaste: where = "Waste bin"; break;
                case FireLocation.Chargers: where = "Chargers"; break;
                case FireLocation.StorageRack: where = "Rack"; break;
                case FireLocation.ElectricalPanel: where = "Panel"; break;
                case FireLocation.SolventDrums: where = "Drums"; break;
                default: where = s.Title; break;
            }
            return s.FleeOnly ? where + " (Li-ion)" : $"{where} ({FireDrillText.ClassLetter(s.Class)})";
        }

        private void EnterDrill()
        {
            ClearFire();
            PutBack();
            GoToStation();
            ShowDrill(true);
        }

        private void EndDrill()
        {
            ClearFire();
            PutBack();
            Publish(CellTopics.FireAlarmReset, SignalValue.FromBool(true));
            ShowDrill(false);
        }

        private void StartFire(FireScenario scenario)
        {
            Publish(CellTopics.FireScenario, SignalValue.FromNumber(scenario.Number));
            Refresh();
        }

        private void StartRandom()
        {
            var candidates = new List<FireScenario>();
            foreach (FireScenario s in scenarios)
            {
                if (s != null)
                    candidates.Add(s);
            }
            if (candidates.Count > 0)
                StartFire(candidates[_random.Next(candidates.Count)]);
        }

        private void ClearFire() => Publish(CellTopics.FireScenario, SignalValue.FromNumber(0));

        private void PutBack()
        {
            if (extinguisher != null)
                extinguisher.PutAllBack();
            Refresh();
        }

        private void RaiseAlarm()
        {
            Publish(CellTopics.FireCallPoint, SignalValue.FromBool(true));
            Publish(CellTopics.FireCallPoint, SignalValue.FromBool(false));
            Refresh();
        }

        private void GoToStation() => RigTeleport.GoTo(origin, stationFloor, stationYaw);

        private void GoToFire()
        {
            Transform target = extinguisher != null ? extinguisher.BaseOf(CurrentLocation()) : null;
            if (target == null || origin == null)
                return;
            (Vector3 floor, float yaw) = RigTeleport.StandOff(target.position, stationFloor, standOffMetres);
            RigTeleport.GoTo(origin, floor, yaw);
        }

        private void ShowDrill(bool on)
        {
            _inDrill = on;
            Toggle(_mainOptions, !on);
            Toggle(_hud, !on);
            Toggle(_fireOptions, on);
            Toggle(_fireHud, on);
            if (on)
                Refresh();
        }

        private void Refresh()
        {
            if (_fireLine == null)
                return;
            FireLocation location = CurrentLocation();
            var fireClass = (FireClass)(int)Read(CellTopics.FireClass);
            var stage = (FireStage)(int)Read(CellTopics.FireStage);
            bool flee = FleeOnly(location);
            bool alarm = Read(CellTopics.FireAlarm) != 0d;
            FireAgent agent = extinguisher != null ? extinguisher.Agent : FireAgent.None;
            float charge = extinguisher != null ? extinguisher.Charge : 0f;

            _fireLine.text = FireDrillText.Fire(location, fireClass, stage, flee);
            _extLine.text = FireDrillText.Extinguisher(agent, charge);
            bool extinguished = Read(CellTopics.FireExtinguished) != 0d;
            _advice.text = FireDrillText.Advice(stage, fireClass, flee, extinguished, alarm, agent, charge,
                extinguisher != null && extinguisher.Discharging, extinguisher != null ? extinguisher.Aim : 0f,
                extinguisher != null ? extinguisher.Standoff : 0f, extinguisher != null ? extinguisher.Distance : 0f,
                extinguisher != null && extinguisher.PinPulled, extinguisher != null && extinguisher.HoseHeld);
        }

        private FireLocation CurrentLocation() => (FireLocation)(int)Read(CellTopics.FireLocation);

        private bool FleeOnly(FireLocation location)
        {
            foreach (FireScenario s in scenarios)
            {
                if (s != null && s.Location == location)
                    return s.FleeOnly;
            }
            return false;
        }

        private double Read(TopicPath topic) =>
            context.Bus.TryGetLast(topic, out SignalMessage message) ? message.Value.AsNumber : 0d;

        private void Publish(TopicPath topic, SignalValue value) => context.Bus.Publish(topic, value, CellContext.NowUnixMs);

        private void Wire(string buttonName, Action action)
        {
            Button button = _root.Q<Button>(buttonName);
            if (button == null)
                return;
            button.clicked += action;
            _wired.Add((button, action));
        }

        private static void Toggle(VisualElement element, bool shown)
        {
            if (element != null)
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
