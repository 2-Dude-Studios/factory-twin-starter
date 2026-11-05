using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Dashboard: the electrical panel (M19) in Dashboard.uxml. Shows the drive fault, Q1 and its locks, the door,
    /// the main bus and the infeed VFD's DC bus, from the bus only, while anything on the cabinet is out of normal
    /// service. Like a real HMI it reads the main bus: it never shows the control voltage, so in E2 it says 0 V while
    /// the control terminals are live. Its buttons are the trainer's: start E1–E4, end, and work Q1, the UPS feed,
    /// their locks and the door until M20/M21 put those in the trainee's hands.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class ElectricalPanel : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;

        private VisualElement _panel;
        private Label _scenario;
        private Label _fault;
        private Label _disconnect;
        private Label _locks;
        private Label _ups;
        private Label _door;
        private Label _bus;
        private Label _dcBus;
        private IDisposable _subscription;
        private Action[] _handlers = Array.Empty<Action>();
        private string[] _buttons = Array.Empty<string>();

        private int _scenarioNumber;
        private bool _driveFault;
        private bool _disconnectOpen;
        private bool _doorOpen;
        private int _lockCount;
        private bool _upsOpen;
        private int _upsLocks;

        private void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null || context == null)
            {
                Debug.LogError("[ElectricalPanel] Assign the dashboard's UIDocument and Context (Cell).", this);
                enabled = false;
                return;
            }
            _panel = root.Q("elec-panel");
            _scenario = root.Q<Label>("elec-scenario");
            _fault = root.Q<Label>("elec-fault");
            _disconnect = root.Q<Label>("elec-disconnect");
            _locks = root.Q<Label>("elec-locks");
            _ups = root.Q<Label>("elec-ups");
            _door = root.Q<Label>("elec-door");
            _bus = root.Q<Label>("elec-bus");
            _dcBus = root.Q<Label>("elec-dc-bus");
            if (_panel == null)
            {
                Debug.LogWarning("[ElectricalPanel] Dashboard.uxml has no elec-panel.", this);
                enabled = false;
                return;
            }

            SignalBus bus = context.Bus;
            _buttons = new[] { "elec-1", "elec-2", "elec-3", "elec-4", "elec-end", "elec-q1", "elec-lock", "elec-unlock", "elec-ups", "elec-ups-lock", "elec-door" };
            _handlers = new Action[]
            {
                () => Send(CellTopics.ElecScenario, 1), () => Send(CellTopics.ElecScenario, 2),
                () => Send(CellTopics.ElecScenario, 3), () => Send(CellTopics.ElecScenario, 4),
                () => Send(CellTopics.ElecScenario, 0),
                () => Send(CellTopics.CabinetDisconnectCommand, _disconnectOpen ? 0 : 1),
                () => Send(CellTopics.CabinetLockCommand, 1),
                () => Send(CellTopics.CabinetLockCommand, 0),
                () => Send(CellTopics.CabinetUpsCommand, _upsOpen ? 0 : 1),
                () => Send(CellTopics.CabinetUpsLockCommand, _upsLocks > 0 ? 0 : 1),
                () => Send(CellTopics.CabinetDoorCommand, _doorOpen ? 0 : 1),
            };
            for (int i = 0; i < _buttons.Length; i++)
            {
                Button b = root.Q<Button>(_buttons[i]);
                if (b != null)
                    b.clicked += _handlers[i];
            }

            foreach (TopicPath topic in new[]
                     {
                         CellTopics.ElecScenario, CellTopics.InfeedDriveFault, CellTopics.CabinetDisconnect, CellTopics.CabinetLocked,
                         CellTopics.CabinetUpsFeed, CellTopics.CabinetUpsLocked, CellTopics.CabinetDoorOpen,
                         CellTopics.CabinetBusVolts, CellTopics.CabinetDcBusVolts,
                     })
            {
                if (bus.TryGetLast(topic, out SignalMessage m))
                    Apply(m);
            }
            _subscription = bus.Subscribe(TopicFilter.All, Apply);
            ShowPanel();
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
            VisualElement root = document != null ? document.rootVisualElement : null;
            for (int i = 0; root != null && i < _buttons.Length; i++)
            {
                Button b = root.Q<Button>(_buttons[i]);
                if (b != null)
                    b.clicked -= _handlers[i];
            }
        }

        private void Send(TopicPath topic, double value) =>
            context.Bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);

        private void Apply(SignalMessage message)
        {
            TopicPath topic = message.Topic;
            double v = message.Value.AsNumber;
            if (topic == CellTopics.ElecScenario)
            {
                _scenarioNumber = (int)v;
                Set(_scenario, ElectricalWords.Scenario(_scenarioNumber));
            }
            else if (topic == CellTopics.InfeedDriveFault)
            {
                _driveFault = v != 0d;
                Set(_fault, ElectricalWords.DriveFault(_driveFault));
                _fault?.EnableInClassList("elec__fault--on", _driveFault);
            }
            else if (topic == CellTopics.CabinetDisconnect)
            {
                _disconnectOpen = v != 0d;
                Set(_disconnect, ElectricalWords.Disconnect(_disconnectOpen));
            }
            else if (topic == CellTopics.CabinetLocked)
            {
                _lockCount = (int)v;
                Set(_locks, ElectricalWords.Locks(_lockCount));
            }
            else if (topic == CellTopics.CabinetUpsFeed)
            {
                _upsOpen = v != 0d;
                Set(_ups, ElectricalWords.UpsFeed(_upsOpen, _upsLocks));
            }
            else if (topic == CellTopics.CabinetUpsLocked)
            {
                _upsLocks = (int)v;
                Set(_ups, ElectricalWords.UpsFeed(_upsOpen, _upsLocks));
            }
            else if (topic == CellTopics.CabinetDoorOpen)
            {
                _doorOpen = v != 0d;
                Set(_door, ElectricalWords.Door(_doorOpen));
            }
            else if (topic == CellTopics.CabinetBusVolts)
                Set(_bus, ElectricalWords.Volts(v));
            else if (topic == CellTopics.CabinetDcBusVolts)
                Set(_dcBus, ElectricalWords.Volts(v, dc: true));
            else
                return;
            ShowPanel();
        }

        private void ShowPanel()
        {
            bool active = ElectricalWords.PanelActive(_scenarioNumber, _driveFault, _disconnectOpen, _doorOpen, _lockCount, _upsOpen, _upsLocks);
            _panel.EnableInClassList("hidden", !active);
            _panel.EnableInClassList("elec--fault", _driveFault);
            _ups?.EnableInClassList("hidden", string.IsNullOrEmpty(_ups.text));
        }

        private static void Set(Label label, string text)
        {
            if (label != null && label.text != text)
                label.text = text;
        }
    }
}
