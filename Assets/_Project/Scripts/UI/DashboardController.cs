using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Dashboard: hosts <see cref="DashboardViewModel"/> as the data source of Dashboard.uxml, hands sparklines
    /// their series, fills the event log, swaps status classes and sends the fault buttons to the FaultInjector.
    /// Runs after UIDocument has built its tree.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class DashboardController : MonoBehaviour
    {
        private static readonly string[] HealthClasses = { "health--ok", "health--warning", "health--fault" };

        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;
        [SerializeField] private FaultInjector faultInjector;

        private DashboardViewModel _model;
        private VisualElement _root;
        private VisualElement _pressPanel;
        private Label _pressHealth;
        private Label _lineState;
        private Label _link;
        private VisualElement _credits;
        private ListView _log;

        private readonly (string name, FaultMode mode)[] _faultButtons =
        {
            ("inject-twf", FaultMode.ToolWear),
            ("inject-hdf", FaultMode.HeatDissipation),
            ("inject-pwf", FaultMode.Power),
            ("inject-osf", FaultMode.Overstrain),
            ("inject-rnf", FaultMode.Random),
        };
        private Action[] _injectHandlers;
        private Action _clearHandler;
        private Action _degradeHandler;
        private Action _changeToolHandler;
        private Label _toolLife;
        private Label _toolLifeNote;
        private Action _toggleCredits;

        private void OnEnable()
        {
            if (context == null || faultInjector == null)
            {
                Debug.LogError("[DashboardController] Assign Context (Cell) and Fault Injector (Cell/Press).", this);
                enabled = false;
                return;
            }

            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogError("[DashboardController] UIDocument has no visual tree; assign Dashboard.uxml and a Panel Settings asset.", this);
                enabled = false;
                return;
            }

            _model = new DashboardViewModel(context.Bus);
            _root.pickingMode = PickingMode.Ignore;
            _root.dataSource = _model;

            _pressPanel = _root.Q("press-panel");
            _pressHealth = _root.Q<Label>("press-health");
            _lineState = _root.Q<Label>("line-state");
            _link = _root.Q<Label>("link");
            _credits = _root.Q("credits");
            _toolLife = _root.Q<Label>("tool-life-value");
            _toolLifeNote = _root.Q<Label>("tool-life-note");

            AssignSeries("air-temp-spark", _model.AirTemp);
            AssignSeries("process-temp-spark", _model.ProcessTemp);
            AssignSeries("speed-spark", _model.Speed);
            AssignSeries("torque-spark", _model.Torque);
            AssignSeries("tool-wear-spark", _model.ToolWear);
            AssignSeries("parts-per-hour-spark", _model.PartsPerHour);

            _log = _root.Q<ListView>("event-log");
            if (_log != null)
            {
                _log.makeItem = MakeLogLine;
                _log.bindItem = BindLogLine;
                _log.itemsSource = _model.Log.ItemsSource;
            }

            WireButtons(true);
            _model.Log.Changed += OnLogChanged;
            _model.StatusChanged += ApplyStatus;
            ApplyStatus();
        }

        private void OnDisable()
        {
            if (_model == null)
                return;

            _model.Log.Changed -= OnLogChanged;
            _model.StatusChanged -= ApplyStatus;
            WireButtons(false);
            if (_root != null)
                _root.dataSource = null;
            _model.Dispose();
            _model = null;
        }

        private void AssignSeries(string elementName, SparkSeries series)
        {
            Sparkline sparkline = _root.Q<Sparkline>(elementName);
            if (sparkline != null)
                sparkline.Series = series;
        }

        private void WireButtons(bool wire)
        {
            if (_injectHandlers == null)
            {
                _injectHandlers = new Action[_faultButtons.Length];
                for (int i = 0; i < _faultButtons.Length; i++)
                {
                    FaultMode mode = _faultButtons[i].mode;
                    _injectHandlers[i] = () => faultInjector.Inject(mode);
                }
                _clearHandler = () => faultInjector.Clear();
                _degradeHandler = () => context.Bus.Publish(CellTopics.PressDegrade, SignalValue.FromBool(true), CellContext.NowUnixMs);
                _changeToolHandler = () => context.Bus.Publish(CellTopics.PressToolChange, SignalValue.FromBool(true), CellContext.NowUnixMs);
                _toggleCredits = () => _credits?.ToggleInClassList("hidden");
            }

            for (int i = 0; i < _faultButtons.Length; i++)
                Wire(_faultButtons[i].name, _injectHandlers[i], wire);
            Wire("clear-fault", _clearHandler, wire);
            Wire("degrade-tool", _degradeHandler, wire);
            Wire("change-tool", _changeToolHandler, wire);
            Wire("credits-button", _toggleCredits, wire);
            Wire("credits-close", _toggleCredits, wire);
        }

        private void Wire(string buttonName, Action handler, bool wire)
        {
            Button button = _root.Q<Button>(buttonName);
            if (button == null)
                return;
            if (wire)
                button.clicked += handler;
            else
                button.clicked -= handler;
        }

        private void ApplyStatus()
        {
            for (int i = 0; i < HealthClasses.Length; i++)
            {
                bool on = _model.HasPressHealth && (int)_model.PressHealth == i;
                _pressHealth?.EnableInClassList(HealthClasses[i], on);
                _pressPanel?.EnableInClassList(HealthClasses[i], on);
            }

            CellState state = _model.LineState;
            _lineState?.EnableInClassList("state--running", state == CellState.Feeding || state == CellState.Transferring);
            _lineState?.EnableInClassList("state--stalled", state == CellState.Stalled);
            _lineState?.EnableInClassList("state--held", state == CellState.Held);

            _toolLife?.EnableInClassList("tool-life--due", _model.MaintenanceDue);
            if (_toolLifeNote != null)
                _toolLifeNote.text = _model.MaintenanceDue ? "Change due" : string.Empty;

            _link?.EnableInClassList("link--up", _model.LinkUp);
            _link?.EnableInClassList("link--down", !_model.LinkUp);
        }

        private void OnLogChanged()
        {
            _log?.RefreshItems();
        }

        private static VisualElement MakeLogLine()
        {
            var line = new Label();
            line.AddToClassList("log-line");
            return line;
        }

        private void BindLogLine(VisualElement element, int index)
        {
            LogEntry entry = _model.Log.Entries[index];
            var line = (Label)element;
            line.text = DateTimeOffset.FromUnixTimeMilliseconds(entry.UnixMs).ToLocalTime().ToString("HH:mm:ss") + "   " + entry.Text;
            line.EnableInClassList("log--info", entry.Severity == LogSeverity.Info);
            line.EnableInClassList("log--warning", entry.Severity == LogSeverity.Warning);
            line.EnableInClassList("log--fault", entry.Severity == LogSeverity.Fault);
        }
    }
}
