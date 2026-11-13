using System;
using System.Collections.Generic;
using System.Text;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Standalone mode (M27): the electrical safety drill's page on the tablet in the left hand (VrMenu.uxml), so the
    /// cabinet scenarios run with the headset alone, as the dashboard's electrical panel runs them from the desktop.
    /// It opens when a scenario starts (cell/elec/scenario, from the start menu's Electrical card or here): E1–E4, the
    /// cabinet's controls the dashboard gives the trainer until M20/M21 put them in the trainee's hands (Q1, its lock,
    /// the UPS feed and its lock, the door), end the scenario, back to the start and back to the main menu. The display
    /// reads what the dashboard reads: the drive fault, Q1, the locks, the UPS feed, the door and the main bus as the
    /// HMI shows it, never the control voltage (only the tester finds that). No score until M22.
    /// </summary>
    public sealed class VrElectricalMenu : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;
        [SerializeField] private VrSwitch vrSwitch;
        [SerializeField] private XROrigin origin;
        [Tooltip("The electrical bay's start spot (TrainingAreaStart): where Back to the start puts the trainee.")]
        [SerializeField] private TrainingAreaStart start;

        private VisualElement _root, _scenarios;
        private Label _state;
        private readonly List<(Button button, Action action)> _wired = new List<(Button, Action)>();
        private readonly StringBuilder _text = new StringBuilder(256);
        private IDisposable _subscription;
        private bool _shown;
        private float _nextRefresh;

        private int _scenario;
        private bool _fault, _disconnectOpen, _doorOpen, _upsOpen;
        private int _locks, _upsLocks;
        private double _busVolts, _dcBusVolts;
        private string _refusal = string.Empty;
        private int _ppeWorn;
        private int _ppeCategory;
        private float _refusalUntil;

        private void OnEnable()
        {
            _root = document != null ? document.rootVisualElement : null;
            if (_root == null || context == null)
                return;
            _scenarios = _root.Q("elec-scenarios");
            _state = _root.Q<Label>("elec-state");

            Wire("elec-q1", () => Send(CellTopics.CabinetDisconnectCommand, _disconnectOpen ? 0 : 1));
            Wire("elec-lock", () => Send(CellTopics.CabinetLockCommand, _locks > 0 ? 0 : 1));
            Wire("elec-ups", () => Send(CellTopics.CabinetUpsCommand, _upsOpen ? 0 : 1));
            Wire("elec-ups-lock", () => Send(CellTopics.CabinetUpsLockCommand, _upsLocks > 0 ? 0 : 1));
            Wire("elec-door", () => Send(CellTopics.CabinetDoorCommand, _doorOpen ? 0 : 1));
            Wire("elec-end", () => Send(CellTopics.ElecScenario, 0));
            Wire("elec-start", BackToStart);
            Wire("elec-home", Home);
            BuildScenarioButtons();

            SignalBus bus = context.Bus;
            foreach (TopicPath topic in new[]
                     {
                         CellTopics.ElecScenario, CellTopics.InfeedDriveFault, CellTopics.CabinetDisconnect, CellTopics.CabinetLocked,
                         CellTopics.CabinetUpsFeed, CellTopics.CabinetUpsLocked, CellTopics.CabinetDoorOpen,
                         CellTopics.CabinetBusVolts, CellTopics.CabinetDcBusVolts,
                     })
            {
                if (bus.TryGetLast(topic, out SignalMessage m))
                    Apply(m, false);
            }
            _subscription = bus.Subscribe(TopicFilter.All, m => Apply(m, true));
            if (_shown)
                Show();
        }

        private void Update()
        {
            // The tablet's UI was rebuilt (the panel woke again): wire the new one, and keep the page on top.
            if (document != null && document.rootVisualElement != _root)
            {
                OnDisable();
                OnEnable();
            }
            if (_shown && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.5f;
                bool refusalShown = !string.IsNullOrEmpty(_refusal);
                if (refusalShown && Time.unscaledTime >= _refusalUntil)
                    _refusal = string.Empty;
                Show();
            }
        }

        private void OnDisable()
        {
            foreach ((Button button, Action action) in _wired)
                button.clicked -= action;
            _wired.Clear();
            _scenarios?.Clear();
            _subscription?.Dispose();
            _subscription = null;
        }

        private void BuildScenarioButtons()
        {
            if (_scenarios == null)
                return;
            _scenarios.Clear();
            for (int n = 1; ElectricalScenarios.ByNumber(n) != null; n++)
            {
                int number = n;
                var button = new Button { name = "elec-scenario-" + number, text = ElectricalWords.Scenario(number) };
                button.AddToClassList("menu__button");
                button.AddToClassList("tablet__half");
                Action action = () => Send(CellTopics.ElecScenario, number);
                button.clicked += action;
                _wired.Add((button, action));
                _scenarios.Add(button);
            }
        }

        private void Apply(SignalMessage message, bool live)
        {
            TopicPath topic = message.Topic;
            double v = message.Value.AsNumber;
            if (topic == CellTopics.TrainingHub)
            {
                // Back at the headset hub (M28): the tablet's main page again.
                if (live && _shown)
                {
                    _shown = false;
                    TabletPages.ShowOnly(_root, "main-options", "hud");
                }
                return;
            }
            if (topic == CellTopics.ElecGearUp)
            {
                if (live && v != 0d)
                {
                    _ppeWorn = 0;
                    _ppeCategory = 0;
                    _shown = true;
                }
            }
            else if (topic == CellTopics.ElecPpeWorn)
                _ppeWorn = (int)v;
            else if (topic == CellTopics.ElecPpeCategory)
                _ppeCategory = (int)v;
            else if (topic == CellTopics.ElecPpeRefused)
            {
                if (!live)
                    return;
                PpeKitItem next = _ppeWorn < PpeDressing.Kit.Count ? PpeDressing.Kit[_ppeWorn] : null;
                _refusal = next != null ? $"Not yet: put on the {next.Name.ToLowerInvariant()} first." : string.Empty;
                _refusalUntil = Time.unscaledTime + 5f;
            }
            else if (topic == CellTopics.CabinetRefused)
            {
                if (!live)
                    return;
                _refusal = "Not done: " + ElectricalWords.Refusal((SwitchResult)(int)v) + ".";
                _refusalUntil = Time.unscaledTime + 5f;
            }
            else if (topic == CellTopics.ElecScenario)
            {
                _scenario = (int)v;
                if (_scenario > 0 && live)
                    _shown = true;
            }
            else if (topic == CellTopics.InfeedDriveFault)
                _fault = v != 0d;
            else if (topic == CellTopics.CabinetDisconnect)
                _disconnectOpen = v != 0d;
            else if (topic == CellTopics.CabinetLocked)
                _locks = (int)v;
            else if (topic == CellTopics.CabinetUpsFeed)
                _upsOpen = v != 0d;
            else if (topic == CellTopics.CabinetUpsLocked)
                _upsLocks = (int)v;
            else if (topic == CellTopics.CabinetDoorOpen)
                _doorOpen = v != 0d;
            else if (topic == CellTopics.CabinetBusVolts)
                _busVolts = v;
            else if (topic == CellTopics.CabinetDcBusVolts)
                _dcBusVolts = v;
            else
                return;
            if (_shown && live)
                Show();
        }

        private void Show()
        {
            TabletPages.ShowOnly(_root, "elec-options", "elec-hud");
            if (_state == null)
                return;
            _text.Clear();
            bool dressed = _ppeCategory >= PpeDressing.Category;
            if (_scenarios != null)
                _scenarios.SetEnabled(dressed);
            if (!dressed)
            {
                ShowGearUp();
                return;
            }
            _text.Append("PPE category ").Append(_ppeCategory).Append(" on.").Append('\n');
            _text.Append(_scenario > 0 ? ElectricalWords.Scenario(_scenario) : "No scenario running. Pick one on the right.").Append('\n').Append('\n');
            _text.Append(ElectricalWords.DriveFault(_fault)).Append('\n');
            _text.Append(ElectricalWords.Disconnect(_disconnectOpen)).Append("   ").Append(ElectricalWords.Locks(_locks)).Append('\n');
            string ups = ElectricalWords.UpsFeed(_upsOpen, _upsLocks);
            if (!string.IsNullOrEmpty(ups))
                _text.Append(ups).Append('\n');
            _text.Append(ElectricalWords.Door(_doorOpen)).Append('\n');
            _text.Append("HMI main bus ").Append(ElectricalWords.Volts(_busVolts)).Append("   DC bus ").Append(ElectricalWords.Volts(_dcBusVolts, dc: true));
            if (Time.unscaledTime < _refusalUntil)
                _text.Append('\n').Append('\n').Append(_refusal);
            _state.text = _text.ToString();
            SetText("elec-q1", _disconnectOpen ? "Q1 on" : "Q1 off");
            SetText("elec-lock", _locks > 0 ? "Remove lock" : "Lock Q1");
            SetText("elec-ups", _upsOpen ? "UPS feed on" : "UPS feed off");
            SetText("elec-ups-lock", _upsLocks > 0 ? "Unlock UPS" : "Lock UPS");
            SetText("elec-door", _doorOpen ? "Shut door" : "Open door");
        }

        // Before the training: the kit on the bench, in order, the next one marked, and how it goes on.
        private void ShowGearUp()
        {
            _text.Append("GEAR UP: arc flash PPE, category ").Append(PpeDressing.Category).Append(" (the label: 6.1 cal/cm2)").Append('\n');
            _text.Append("Take each item from the bench, one at a time.").Append('\n').Append('\n');
            for (int i = 0; i < PpeDressing.Kit.Count; i++)
            {
                PpeKitItem item = PpeDressing.Kit[i];
                _text.Append(i < _ppeWorn ? "[x] " : i == _ppeWorn ? "> " : "[ ] ").Append(item.Name);
                if (i == _ppeWorn)
                    _text.Append(": ").Append(item.HowTo);
                _text.Append('\n');
            }
            _text.Append('\n').Append("The training starts when the kit is on.");
            if (Time.unscaledTime < _refusalUntil)
                _text.Append('\n').Append('\n').Append(_refusal);
            _state.text = _text.ToString();
        }

        private void BackToStart()
        {
            if (start != null && origin != null)
                RigTeleport.GoTo(origin, start.FloorPosition, start.YawDegrees);
        }

        private void Home()
        {
            if (_scenario > 0)
                Send(CellTopics.ElecScenario, 0);
            _shown = false;
            SceneNavigation.ToMenu(vrSwitch);
        }

        private void SetText(string buttonName, string text)
        {
            Button button = _root.Q<Button>(buttonName);
            if (button != null && button.text != text)
                button.text = text;
        }

        private void Send(TopicPath topic, double value) =>
            context.Bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);

        private void Wire(string buttonName, Action action)
        {
            Button button = _root.Q<Button>(buttonName);
            if (button == null)
                return;
            button.clicked += action;
            _wired.Add((button, action));
        }
    }
}
