using System;
using System.Collections;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub (M29): the AI trainer's voice. Recorded lines (ElevenLabs, the Tech Narrator voice; the script is
    /// tools/voice/trainer_lines.json) greet the trainee at the hub, introduce each drill, praise steps, warn on mistakes,
    /// call the fire and the PPE, coach every drill step by step (the next step's prompt, <see cref="DrillCoach"/>), and
    /// read out the end of a run (<see cref="TrainerLines"/> picks which). Important lines
    /// cut in over minor ones; praise is spaced out so it never talks over every step. Muted from the comfort panel.
    /// It reads the signal bus, the hub session and the run results only, and pauses with the cell (HeadsetPause stops
    /// the audio listener).
    /// </summary>
    public sealed class TrainerVoice : MonoBehaviour
    {
        /// <summary>One cue's recorded takes (cue_1, cue_2…), one picked at random each time.</summary>
        [Serializable]
        public sealed class Cue
        {
            public string id;
            public AudioClip[] clips = Array.Empty<AudioClip>();
        }

        private enum Weight { Praise = 0, Normal = 1, Urgent = 2 }

        [SerializeField] private CellContext context;
        [SerializeField] private HubSession hub;
        [SerializeField] private AudioSource source;
        [Tooltip("For the robot cell's next step (step-by-step coaching).")]
        [SerializeField] private RobotCellScorer robot;
        [Tooltip("For the fire drill's PASS sequence (step-by-step coaching).")]
        [SerializeField] private FireDrillScorer fire;
        [SerializeField] private Cue[] cues = Array.Empty<Cue>();
        [Tooltip("A coaching prompt waits for whatever the trainer is saying, then this long.")]
        [SerializeField, Min(0f)] private float coachPause = 0.6f;
        [Tooltip("Seconds after start-up before the greeting (the headset settles first).")]
        [SerializeField, Min(0f)] private float greetDelay = 3f;
        [Tooltip("At least this many seconds between two lines of praise.")]
        [SerializeField, Min(0f)] private float praiseGap = 8f;
        [SerializeField, Range(0f, 1f)] private float praiseChance = 0.6f;
        [Tooltip("A robot level's intro waits this long, as the hub may replace the first level with the one picked.")]
        [SerializeField, Min(0f)] private float robotIntroDelay = 2.5f;

        private const string GreetedKey = "ft.trainer.greeted";
        private readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        private IDisposable[] _subscriptions = Array.Empty<IDisposable>();
        private Weight _playing;
        private float _lastPraise = -100f;
        private Coroutine _sequence, _robotIntro;
        private int _robotSteps, _lockoutStep = -1, _ppeWorn;
        private int _fireStage;
        private string _lastCoach;
        private Coroutine _coach;
        private readonly Bay _bay = new Bay();

        private void Awake()
        {
            foreach (Cue cue in cues)
            {
                if (cue != null && !string.IsNullOrEmpty(cue.id) && cue.clips != null && cue.clips.Length > 0)
                    _clips[cue.id] = cue.clips;
            }
        }

        private void OnEnable()
        {
            RunResults.Published += OnRun;
            if (hub != null)
            {
                hub.TrainingStarted += OnTrainingStarted;
                hub.ReturnedToHub += OnHub;
            }
            if (context == null)
                return;
            _subscriptions = new[]
            {
                context.Bus.Subscribe(CellTopics.TrainingRobotLevel.ToString(), OnRobotLevel),
                context.Bus.Subscribe(CellTopics.TrainingRobotStep.ToString(), OnRobotStep),
                context.Bus.Subscribe(CellTopics.TrainingStep.ToString(), OnLockoutStep),
                context.Bus.Subscribe(CellTopics.TrainingRun.ToString(), _ => _lockoutStep = -1),
                context.Bus.Subscribe(CellTopics.TrainingMistake.ToString(), _ => Say(TrainerLines.Mistake, Weight.Normal)),
                context.Bus.Subscribe(CellTopics.ElecPpeWorn.ToString(), OnPpeWorn),
                context.Bus.Subscribe(CellTopics.ElecPpeRefused.ToString(), _ => Say(TrainerLines.Refused, Weight.Normal)),
                context.Bus.Subscribe(CellTopics.CabinetRefused.ToString(), _ => Say(TrainerLines.Refused, Weight.Normal)),
                context.Bus.Subscribe(CellTopics.FireStage.ToString(), OnFireStage),
                context.Bus.Subscribe(CellTopics.FireAlarm.ToString(), _ => CoachFire()),
                context.Bus.Subscribe(CellTopics.FireDischarge.ToString(), _ => CoachFire()),
                context.Bus.Subscribe(CellTopics.FireExtinguished.ToString(), OnExtinguished),
                Cabinet(CellTopics.ElecGearUp, v => _bay.State.GearingUp = v > 0d),
                Cabinet(CellTopics.ElecScenario, v => _bay.State.ScenarioRunning = v > 0d),
                Cabinet(CellTopics.ElecPpeWorn, v => _bay.State.PpeWorn = (int)v),
                Cabinet(CellTopics.CabinetDisconnect, v => _bay.State.Q1Open = v > 0d),
                Cabinet(CellTopics.CabinetLocked, v => _bay.State.Q1Locks = (int)v),
                Cabinet(CellTopics.CabinetUpsFeed, v => _bay.State.UpsOpen = v > 0d),
                Cabinet(CellTopics.CabinetUpsLocked, v => _bay.State.UpsLocks = (int)v),
                Cabinet(CellTopics.CabinetDoorOpen, v => _bay.State.DoorOpen = v > 0d),
                Cabinet(CellTopics.CabinetControlVolts, v => _bay.State.ControlVolts = v),
                Cabinet(CellTopics.CabinetDcBusVolts, v => _bay.State.DcBusVolts = v),
            };
            if (fire != null)
                fire.Scored += OnFireScored;
        }

        // One electrical topic into the bay's state, then the coaching looks again.
        private IDisposable Cabinet(TopicPath topic, Action<double> set) =>
            context.Bus.Subscribe(topic.ToString(), m =>
            {
                set(m.Value.AsNumber);
                string cue = DrillCoach.Electrical(_bay.State);
                Coach(cue, cue == "coach_elec_backfeed" ? Weight.Urgent : Weight.Normal);
            });

        // The electrical bay as the coaching sees it (a class, so the per-topic setters change one copy).
        private sealed class Bay
        {
            public CabinetState State;
        }

        private void OnDisable()
        {
            RunResults.Published -= OnRun;
            if (hub != null)
            {
                hub.TrainingStarted -= OnTrainingStarted;
                hub.ReturnedToHub -= OnHub;
            }
            foreach (IDisposable s in _subscriptions)
                s?.Dispose();
            _subscriptions = Array.Empty<IDisposable>();
            if (fire != null)
                fire.Scored -= OnFireScored;
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(greetDelay);
            bool first = !PlayerPrefs.HasKey(GreetedKey);
            Say(first ? TrainerLines.GreetFirst : TrainerLines.GreetBack, Weight.Normal);
            if (first)
            {
                PlayerPrefs.SetInt(GreetedKey, 1);
                PlayerPrefs.Save();
            }
        }

        private void OnTrainingStarted(TrainingMode mode, int level)
        {
            _robotSteps = 0;
            _lockoutStep = -1;
            _ppeWorn = 0;
            _lastCoach = null;
            // The robot drill's intro comes with its level (OnRobotLevel), once the hub has settled which one.
            if (mode != TrainingMode.RobotZone)
                Say(TrainerLines.Intro(mode, level), Weight.Normal);
        }

        private void OnHub() => Say(TrainerLines.HubBack, Weight.Praise);

        private void OnRobotLevel(SignalMessage m)
        {
            int level = (int)m.Value.AsNumber;
            _robotSteps = 0;
            if (_robotIntro != null)
                StopCoroutine(_robotIntro);
            _robotIntro = level > 0 ? StartCoroutine(RobotIntro(level)) : null;
            _lastCoach = null;
        }

        private IEnumerator RobotIntro(int level)
        {
            yield return new WaitForSeconds(robotIntroDelay);
            _robotIntro = null;
            Say(TrainerLines.RobotIntro(level), Weight.Normal);
            CoachRobot();
        }

        private void CoachRobot()
        {
            if (robot != null)
                Coach(DrillCoach.Robot(robot.RunningLevel, robot.NextStep), Weight.Normal);
        }

        private void OnRobotStep(SignalMessage m)
        {
            int steps = (int)m.Value.AsNumber;
            if (steps > _robotSteps)
            {
                Praise();
                CoachRobot();
            }
            _robotSteps = steps;
        }

        private void OnLockoutStep(SignalMessage m)
        {
            int step = (int)m.Value.AsNumber;
            if (_lockoutStep >= 0 && step > _lockoutStep)
                Praise();
            _lockoutStep = step;
            // training/step is the next step's number (1 = the first), 0 when no run is going.
            Coach(DrillCoach.Lockout(step - 1), Weight.Normal);
        }

        private void OnPpeWorn(SignalMessage m)
        {
            int worn = (int)m.Value.AsNumber;
            if (worn > _ppeWorn)
            {
                if (worn >= PpeDressing.Kit.Count)
                    Say(TrainerLines.PpeDone, Weight.Normal);
                else
                    Praise();
            }
            _ppeWorn = worn;
        }

        // The fire's first sign calls for the alarm; past incipient, the call to leave.
        private void OnFireStage(SignalMessage m)
        {
            int stage = (int)m.Value.AsNumber;
            bool burning = stage > (int)FireStage.None && stage < (int)FireStage.Out;
            bool wasBurning = _fireStage > (int)FireStage.None && _fireStage < (int)FireStage.Out;
            if (burning && !wasBurning)
                Say(TrainerLines.FireStart, Weight.Urgent);
            else if (stage == (int)FireStage.BeyondIncipient && _fireStage != stage)
                Say(TrainerLines.FireLeave, Weight.Urgent);
            _fireStage = stage;
        }

        private void OnFireScored(ScoreEvent e) => CoachFire();

        private void CoachFire()
        {
            FireDrillScore score = fire != null ? fire.Score : null;
            if (score == null || !score.Running)
                return;
            Coach(DrillCoach.Fire(score.AlarmDone, score.PinDone, score.HoseDone, score.DischargeDone, score.KnockedDownDone, score.FleeOnly), Weight.Normal);
        }

        private void OnExtinguished(SignalMessage m)
        {
            if (m.Value.AsNumber > 0d && _fireStage > (int)FireStage.None)
                Coach("coach_fire_out", Weight.Normal);
        }

        // Step-by-step coaching: the prompt for what comes next, once, after whatever is being said (praise, an intro).
        // An urgent prompt (a live control circuit) cuts in at once.
        private void Coach(string cue, Weight weight)
        {
            if (cue == null || cue == _lastCoach)
                return;
            _lastCoach = cue;
            if (_coach != null)
                StopCoroutine(_coach);
            _coach = weight == Weight.Urgent ? null : StartCoroutine(CoachAfter(cue));
            if (weight == Weight.Urgent)
                Say(cue, Weight.Urgent);
        }

        private IEnumerator CoachAfter(string cue)
        {
            yield return new WaitForSeconds(coachPause);
            float waited = 0f;
            while (source != null && (source.isPlaying || AudioListener.pause) && waited < 12f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(coachPause);
            _coach = null;
            if (cue == _lastCoach)
                Say(cue, Weight.Normal);
        }

        private void OnRun(RunResult result)
        {
            if (_sequence != null)
                StopCoroutine(_sequence);
            _sequence = StartCoroutine(Sequence(TrainerLines.Result(result)));
        }

        // The end of a run: the verdict, then each extra in turn, after the results card's first chime.
        private IEnumerator Sequence(List<string> lines)
        {
            yield return new WaitForSeconds(0.6f);
            foreach (string cue in lines)
            {
                if (!Say(cue, Weight.Urgent))
                    continue;
                while (source != null && (source.isPlaying || AudioListener.pause))
                    yield return null;
                yield return new WaitForSeconds(0.25f);
            }
            _sequence = null;
        }

        private void Praise()
        {
            if (Time.time - _lastPraise < praiseGap || UnityEngine.Random.value > praiseChance)
                return;
            if (Say(TrainerLines.Step, Weight.Praise))
                _lastPraise = Time.time;
        }

        // Plays a cue's take; a line never cuts off a more important one, and praise never cuts off anything.
        private bool Say(string cue, Weight weight)
        {
            if (cue == null || source == null || !ComfortSettings.VoiceOn || !_clips.TryGetValue(cue, out AudioClip[] takes))
                return false;
            if (source.isPlaying && (weight < _playing || weight == Weight.Praise))
                return false;
            AudioClip clip = takes[UnityEngine.Random.Range(0, takes.Length)];
            if (clip == null)
                return false;
            source.Stop();
            source.clip = clip;
            source.Play();
            _playing = weight;
            Debug.Log($"[TrainerVoice] {clip.name}", this);
            return true;
        }
    }
}
