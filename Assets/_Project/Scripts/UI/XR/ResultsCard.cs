using System;
using System.Collections;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub's game layer (M28): the results card. When a scorer announces a finished run
    /// (<see cref="RunResults"/>) a panel comes up in front of the trainee: the stars revealed one at a time with a chime,
    /// the points counting up, the XP earned (doubled for the daily challenge), the rank bar moving and a rank-up,
    /// a new best and new badges, then Again, Next level, Hub and Close. It follows the trainee if they are taken back
    /// to the hub while it is up. Buttons go through the <see cref="HubSession"/>.
    /// </summary>
    public sealed class ResultsCard : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private HubSession hub;
        [Tooltip("The headset camera: the card comes up in front of it.")]
        [SerializeField] private Transform head;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Min(0.5f)] private float distance = 1.25f;
        [SerializeField] private float belowEyes = 0.12f;
        [SerializeField, Min(0.1f)] private float starSeconds = 0.45f;

        private VisualElement _root;
        private Label _title, _verdict, _points, _xp, _daily, _rank, _extra, _debriefText;
        private VisualElement _debrief;
        private VisualElement _starsRow, _fill;
        private readonly Label[] _stars = new Label[DrillStars.Max];
        private Button _again, _next, _hub, _close;
        private RunResult _shown;
        private Coroutine _reveal;
        private AudioClip _chime, _fanfare;
        private readonly List<InputDevice> _hands = new List<InputDevice>();
        private Collider[] _press = Array.Empty<Collider>();

        private void Awake()
        {
            _chime = ProceduralAudio.Clip("ResultsStar", ProceduralAudio.ChimeSamples(0.45f));
            _fanfare = ProceduralAudio.Clip("ResultsRankUp", ProceduralAudio.ChimeSamples(0.7f));
        }

        private void OnDestroy()
        {
            if (_chime != null)
                Destroy(_chime);
            if (_fanfare != null)
                Destroy(_fanfare);
        }

        private void OnEnable() => RunResults.Published += Show;

        private void OnDisable() => RunResults.Published -= Show;

        // Start, not OnEnable: the document builds its tree in its own OnEnable.
        private void Start()
        {
            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
                return;
            _title = _root.Q<Label>("rc-title");
            _verdict = _root.Q<Label>("rc-verdict");
            _points = _root.Q<Label>("rc-points");
            _xp = _root.Q<Label>("rc-xp");
            _daily = _root.Q<Label>("rc-daily");
            _rank = _root.Q<Label>("rc-rank");
            _extra = _root.Q<Label>("rc-extra");
            _debrief = _root.Q("rc-debrief");
            _debriefText = _root.Q<Label>("rc-debrief-text");
            _starsRow = _root.Q("rc-stars");
            _fill = _root.Q("rc-bar-fill");
            for (int i = 0; i < _stars.Length; i++)
                _stars[i] = _root.Q<Label>("rc-star-" + (i + 1));
            _again = _root.Q<Button>("rc-again");
            _next = _root.Q<Button>("rc-next");
            _hub = _root.Q<Button>("rc-hub");
            _close = _root.Q<Button>("rc-close");
            if (_again != null) _again.clicked += Again;
            if (_next != null) _next.clicked += Next;
            if (_hub != null) _hub.clicked += ToHub;
            if (_close != null) _close.clicked += Hide;
            _press = GetComponentsInChildren<Collider>(true);
            Hide();
        }

        private void Update()
        {
            // Taken back to the hub (or teleported) with the card up: it comes along.
            if (_shown != null && head != null && Vector3.Distance(transform.position, head.position) > distance * 2.5f)
                PlaceInFront();
        }

        private void Show(RunResult result)
        {
            if (_root == null || result == null)
                return;
            _shown = result;
            PlaceInFront();
            _root.style.display = DisplayStyle.Flex;
            Pressable(true);
            _title.text = result.Title;
            _verdict.text = result.Passed ? "Passed" : "Not passed this time: have another go";
            _starsRow.style.display = result.Stars >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (Label star in _stars)
                star.RemoveFromClassList("rc__star--on");
            _points.text = "0";
            _xp.text = "+0";
            _daily.text = result.Gain.Daily ? $"Daily challenge: XP x{TraineeProgress.DailyMultiplier}" : string.Empty;
            bool debrief = result.Debrief != null && result.Debrief.Count > 0;
            if (_debrief != null)
                _debrief.style.display = debrief ? DisplayStyle.Flex : DisplayStyle.None;
            if (_debriefText != null)
                _debriefText.text = debrief ? string.Join("\n", result.Debrief) : string.Empty;
            var extras = new List<string>();
            if (result.NewBest && result.Points > 0)
                extras.Add("New best!");
            if (result.NewBadges.Count > 0)
                extras.Add("New badge" + (result.NewBadges.Count > 1 ? "s" : string.Empty) + ": " + string.Join(", ", result.NewBadges));
            if (result.Gain.Streak > 1)
                extras.Add($"{result.Gain.Streak}-day streak");
            _extra.text = string.Join("   ·   ", extras);
            bool levels = result.Mode == TrainingMode.RobotZone && result.Level > 0 && result.Level < DrillLevels.Count && result.Passed;
            _next.style.display = levels ? DisplayStyle.Flex : DisplayStyle.None;
            SetRank(result.XpBefore);
            if (_reveal != null)
                StopCoroutine(_reveal);
            _reveal = StartCoroutine(Reveal(result));
            Buzz(0.6f, 0.15f);
        }

        private IEnumerator Reveal(RunResult result)
        {
            yield return Wait(0.3f);
            for (int i = 0; i < Math.Max(result.Stars, 0) && i < _stars.Length; i++)
            {
                _stars[i].AddToClassList("rc__star--on");
                Play(_chime);
                Buzz(0.4f, 0.08f);
                yield return Wait(starSeconds);
            }
            // The points and the XP count up together, the rank bar with them.
            const float countSeconds = 1.0f;
            for (float t = 0f; t < countSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / countSeconds;
                _points.text = Mathf.RoundToInt(result.Points * k).ToString();
                _xp.text = "+" + Mathf.RoundToInt(result.Gain.Xp * k);
                SetRank(Mathf.RoundToInt(Mathf.Lerp(result.XpBefore, result.XpAfter, k)));
                yield return null;
            }
            _points.text = result.Points.ToString();
            _xp.text = "+" + result.Gain.Xp;
            SetRank(result.XpAfter);
            if (TraineeRank.For(result.XpAfter).Name != TraineeRank.For(result.XpBefore).Name)
            {
                _rank.text = "RANK UP: " + TraineeRank.For(result.XpAfter).Name.ToUpperInvariant();
                Play(_fanfare);
                Buzz(0.9f, 0.3f);
            }
            _reveal = null;
        }

        private void SetRank(int xp)
        {
            (string name, int nextAt, float progress) = TraineeRank.For(xp);
            _rank.text = nextAt > 0 ? $"{name.ToUpperInvariant()}  ·  {xp} / {nextAt} XP" : $"{name.ToUpperInvariant()}  ·  {xp} XP";
            if (_fill != null)
                _fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
        }

        private void Again()
        {
            RunResult r = _shown;
            Hide();
            if (r != null && hub != null)
                hub.StartTraining(r.Mode, r.Level);
        }

        private void Next()
        {
            RunResult r = _shown;
            Hide();
            if (r != null && hub != null)
                hub.StartTraining(r.Mode, r.Level + 1);
        }

        private void ToHub()
        {
            Hide();
            if (hub != null)
                hub.ReturnToHub();
        }

        private void Hide()
        {
            _shown = null;
            if (_root != null)
                _root.style.display = DisplayStyle.None;
            Pressable(false);
        }

        // The panel's press colliders only while it shows, so a hidden card never catches a ray or a teleport.
        private void Pressable(bool on)
        {
            foreach (Collider c in _press)
            {
                if (c != null)
                    c.enabled = on;
            }
        }

        private void PlaceInFront()
        {
            if (head == null)
                return;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.forward;
            forward.Normalize();
            transform.SetPositionAndRotation(head.position + forward * distance + Vector3.down * belowEyes, Quaternion.LookRotation(forward, Vector3.up));
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        // A short buzz on both controllers.
        private void Buzz(float amplitude, float seconds)
        {
            _hands.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand, _hands);
            foreach (InputDevice hand in _hands)
                hand.SendHapticImpulse(0u, amplitude, seconds);
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                yield return null;
        }
    }
}
