using System.Collections.Generic;
using System.Globalization;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): the fire drill's HUD in the headset (FireHud.uxml). While a drill session is open it floats
    /// low and ahead of the trainee and follows the view lazily: the points, the time since the flame took hold, the
    /// badges earned, the fire (where, class, stage), the extinguisher in hand and its charge, the run's checklist
    /// (alarm, right extinguisher, pin, hose, discharge, out) and the one thing to do now. Reads only; changes nothing.
    /// </summary>
    public sealed class FireHud : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;
        [SerializeField] private FireDrillScorer scorer;
        [SerializeField] private FireExtinguisher extinguisher;
        [SerializeField] private BadgeIcons icons;
        [Tooltip("The headset camera the HUD follows.")]
        [SerializeField] private Transform head;
        [Tooltip("The drill's fires, to know which are flee-only.")]
        [SerializeField] private FireScenario[] scenarios = System.Array.Empty<FireScenario>();

        [Header("Placement")]
        [SerializeField, Min(0.3f)] private float distance = 0.85f;
        [SerializeField] private float drop = 0.32f;
        [Tooltip("Tilt back towards the eye, degrees.")]
        [SerializeField] private float tilt = 25f;
        [Tooltip("The HUD re-centres once the view has turned this far away from it.")]
        [SerializeField, Range(5f, 60f)] private float recentreDegrees = 22f;
        [SerializeField, Min(0.5f)] private float followSpeed = 4f;
        [SerializeField, Range(2f, 30f)] private float refreshHz = 8f;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly string[] StepNames = { "Alarm", "Right type", "Pin", "Hose", "Squeeze", "Out" };

        private VisualElement _root, _badges, _charge;
        private Label _score, _timer, _fire, _stage, _ext, _advice;
        private readonly List<(VisualElement chip, VisualElement icon)> _steps = new List<(VisualElement, VisualElement)>();
        private readonly bool[] _stepState = new bool[6];
        private bool _shown, _placed, _following;
        private float _nextRefresh;
        private int _lastScore = int.MinValue, _lastBadges = -1, _lastSeconds = int.MinValue, _lastPercent = -1;
        private string _lastAdvice, _lastFire, _lastStage, _lastExt;
        private Vector3 _yawForward = Vector3.forward;

        private void Start()
        {
            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
            {
                enabled = false;
                return;
            }
            // Display only: rays and pokes pass through it.
            _root.pickingMode = PickingMode.Ignore;
            _score = _root.Q<Label>("hud-score");
            _timer = _root.Q<Label>("hud-timer");
            _fire = _root.Q<Label>("hud-fire");
            _stage = _root.Q<Label>("hud-stage");
            _ext = _root.Q<Label>("hud-ext");
            _advice = _root.Q<Label>("hud-advice");
            _badges = _root.Q("hud-badges");
            _charge = _root.Q("hud-charge");
            Icon("icon-score", icons != null ? icons.Score : null);
            Icon("icon-timer", icons != null ? icons.Timer : null);
            Icon("icon-fire", icons != null ? icons.Fire : null);
            Icon("icon-ext", icons != null ? icons.Extinguisher : null);
            BuildSteps(_root.Q("hud-steps"));
            Show(false);
        }

        private void LateUpdate()
        {
            bool want = scorer != null && scorer.SessionOpen && head != null && head.gameObject.activeInHierarchy;
            if (want != _shown)
                Show(want);
            if (!_shown)
                return;
            Follow();
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 1f / refreshHz;
            Refresh();
        }

        private void Show(bool on)
        {
            _shown = on;
            _placed = false;
            if (_root != null)
                _root.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on)
            {
                _lastScore = int.MinValue;
                _lastBadges = -1;
                _lastSeconds = int.MinValue;
                _lastPercent = -1;
                _lastAdvice = _lastFire = _lastStage = _lastExt = null;
                Refresh();
            }
        }

        // Lazy follow: stays put while the trainee looks around it, glides back once they turn well away.
        private void Follow()
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = _yawForward;
            forward.Normalize();
            if (!_placed)
            {
                _yawForward = forward;
                _placed = true;
                Place(1f);
                return;
            }
            float off = Vector3.Angle(_yawForward, forward);
            if (off > recentreDegrees)
                _following = true;
            if (_following)
            {
                _yawForward = Vector3.Slerp(_yawForward, forward, 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime)).normalized;
                if (Vector3.Angle(_yawForward, forward) < 2f)
                    _following = false;
            }
            Place(1f - Mathf.Exp(-followSpeed * 2f * Time.unscaledDeltaTime));
        }

        private void Place(float blend)
        {
            Vector3 target = head.position + _yawForward * distance + Vector3.down * drop;
            // The document's front faces -Z: look along the view direction, tipped back towards the eye.
            Quaternion rotation = Quaternion.LookRotation(_yawForward, Vector3.up) * Quaternion.Euler(tilt, 0f, 0f);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, target, blend),
                Quaternion.Slerp(transform.rotation, rotation, blend));
        }

        private void Refresh()
        {
            FireDrillScore score = scorer.Score;
            if (score.Total != _lastScore)
            {
                _lastScore = score.Total;
                _score.text = score.Total.ToString(Invariant);
            }
            if (score.Badges.Count != _lastBadges)
            {
                _lastBadges = score.Badges.Count;
                BadgeChips.Fill(_badges, FireDrillRecord.BadgesInOrder(score), icons, false);
            }

            double since = score.Running ? score.SinceIgnition(scorer.Now) : -1d;
            int seconds = since < 0d ? -1 : (int)since;
            if (seconds != _lastSeconds)
            {
                _lastSeconds = seconds;
                _timer.text = seconds < 0 ? "--:--" : $"{seconds / 60:00}:{seconds % 60:00}";
            }

            FireLocation location = (FireLocation)(int)Read(CellTopics.FireLocation);
            var fireClass = (FireClass)(int)Read(CellTopics.FireClass);
            var stage = (FireStage)(int)Read(CellTopics.FireStage);
            bool flee = FleeOnly(location);
            bool alarm = Read(CellTopics.FireAlarm) != 0d;
            bool extinguished = Read(CellTopics.FireExtinguished) != 0d;

            string fire = stage == FireStage.None || location == FireLocation.None
                ? "No fire: start one from the tablet"
                : $"{Capitalise(FireWords.Location(location))} · " + (flee ? "Lithium-ion" : $"Class {FireDrillText.ClassLetter(fireClass)}");
            SetText(_fire, fire, ref _lastFire);
            SetText(_stage, stage == FireStage.None ? string.Empty : FireWords.Stage(stage), ref _lastStage);
            _stage.EnableInClassList("fhud__stage--danger", stage == FireStage.BeyondIncipient || stage == FireStage.Growing || flee);

            FireAgent agent = extinguisher != null ? extinguisher.Agent : FireAgent.None;
            float charge = extinguisher != null ? extinguisher.Charge : 0f;
            SetText(_ext, agent == FireAgent.None ? "No extinguisher" : $"{ExtinguisherRules.Name(agent)} · {ExtinguisherRules.Rating(agent)}", ref _lastExt);
            int percent = agent == FireAgent.None ? 0 : Mathf.RoundToInt(charge * 100f);
            if (percent != _lastPercent)
            {
                _lastPercent = percent;
                _charge.style.width = Length.Percent(percent);
                _charge.EnableInClassList("fhud__bar-fill--low", percent < 25);
            }

            // The last run's checklist stays up after it ends, until the next fire starts a new one.
            SetStep(0, score.AlarmDone);
            SetStep(1, score.RightAgentDone);
            SetStep(2, score.PinDone);
            SetStep(3, score.HoseDone);
            SetStep(4, score.DischargeDone);
            SetStep(5, score.OutDone);

            string advice = FireDrillText.Advice(stage, fireClass, flee, extinguished, alarm, agent, charge,
                extinguisher != null && extinguisher.Discharging, extinguisher != null ? extinguisher.Aim : 0f,
                extinguisher != null ? extinguisher.Standoff : 0f, extinguisher != null ? extinguisher.Distance : 0f,
                extinguisher != null && extinguisher.PinPulled, extinguisher != null && extinguisher.HoseHeld);
            SetText(_advice, advice, ref _lastAdvice);
        }

        private void BuildSteps(VisualElement row)
        {
            if (row == null)
                return;
            row.Clear();
            _steps.Clear();
            foreach (string name in StepNames)
            {
                var chip = new VisualElement { pickingMode = PickingMode.Ignore };
                chip.AddToClassList("fhud__step");
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("fhud__step-icon");
                if (icons != null && icons.Todo != null)
                    icon.style.backgroundImage = new StyleBackground(icons.Todo);
                var label = new Label(name) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("fhud__step-text");
                chip.Add(icon);
                chip.Add(label);
                row.Add(chip);
                _steps.Add((chip, icon));
            }
        }

        private void SetStep(int index, bool done)
        {
            if (index >= _steps.Count || _stepState[index] == done)
                return;
            _stepState[index] = done;
            (VisualElement chip, VisualElement icon) = _steps[index];
            chip.EnableInClassList("fhud__step--done", done);
            Texture2D texture = icons != null ? (done ? icons.Done : icons.Todo) : null;
            if (texture != null)
                icon.style.backgroundImage = new StyleBackground(texture);
        }

        private void Icon(string name, Texture2D texture)
        {
            VisualElement element = _root.Q(name);
            if (element == null)
                return;
            if (texture != null)
                element.style.backgroundImage = new StyleBackground(texture);
            else
                element.style.display = DisplayStyle.None;
        }

        private static void SetText(Label label, string text, ref string last)
        {
            if (label == null || text == last)
                return;
            last = text;
            label.text = text;
        }

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
            context != null && context.Bus.TryGetLast(topic, out SignalMessage message) ? message.Value.AsNumber : 0d;

        private static string Capitalise(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
