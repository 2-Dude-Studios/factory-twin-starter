using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training (M12): a maintenance colleague who comes back to the CNC after the tool change and stands in the work
    /// area, so the trainee's area check (1910.147(e)(2)(i)) finds someone and is refused. Asked to stand clear (the
    /// trainee points at them and pulls the trigger, or clicks: cell/training/action AskStandClear), they walk out to
    /// their stand-clear spot. They go home again when the run ends. No collider but the click target, not on the
    /// Trainee layer, so the zone sensors never see them.
    /// </summary>
    public sealed class NpcColleague : MonoBehaviour
    {
        private enum Where { Home, InArea, Clear }

        [Serializable]
        private sealed class Spot
        {
            [SerializeField] private Vector3 position;
            [SerializeField] private float yawDegrees;
            [SerializeField] private string state = "Idle";

            public Vector3 Position => position;
            public float YawDegrees => yawDegrees;
            public string State => state;
        }

        [SerializeField] private CellContext context;
        [SerializeField] private Animator animator;
        [Tooltip("Where they wait while nothing is being serviced.")]
        [SerializeField] private Spot home = new Spot();
        [Tooltip("Where they stand inside the CNC's work area after the tool change.")]
        [SerializeField] private Spot workArea = new Spot();
        [Tooltip("Where they go when asked to stand clear.")]
        [SerializeField] private Spot clear = new Spot();
        [SerializeField] private float walkSpeed = 1.1f;
        [SerializeField] private float turnDegreesPerSecond = 270f;
        [SerializeField] private string walkState = "Walk";

        private IDisposable _actionSubscription, _runSubscription;
        private Where _target = Where.Home;
        private bool _walking;
        private bool _askedThisRun;

        private void OnEnable()
        {
            SignalBus bus = context.Bus;
            _actionSubscription = bus.Subscribe(CellTopics.TrainingAction.ToString(), m =>
            {
                if ((LockoutAction)(int)m.Value.AsNumber == LockoutAction.AskStandClear && _target == Where.InArea)
                {
                    _askedThisRun = true;
                    Go(Where.Clear);
                }
            });
            _runSubscription = bus.Subscribe(CellTopics.TrainingRun.ToString(), _ => _askedThisRun = false);
        }

        private void OnDisable()
        {
            _actionSubscription?.Dispose();
            _runSubscription?.Dispose();
            _actionSubscription = _runSubscription = null;
        }

        private void Start()
        {
            transform.SetPositionAndRotation(home.Position, Quaternion.Euler(0f, home.YawDegrees, 0f));
            Play(home.State);
        }

        private void Update()
        {
            SignalBus bus = context.Bus;
            int step = bus.TryGetLast(CellTopics.TrainingStep, out SignalMessage s) ? (int)s.Value.AsNumber : 0;
            var outcome = bus.TryGetLast(CellTopics.TrainingResult, out SignalMessage r) ? (TrainingOutcome)(int)r.Value.AsNumber : TrainingOutcome.Idle;
            if (outcome != TrainingOutcome.InProgress)
                _askedThisRun = false;

            Where wanted = _target;
            if (CrewRules.ColleagueInArea(step, outcome) && !_askedThisRun)
                wanted = Where.InArea;
            else if (CrewRules.StandClear(step, outcome))
                wanted = _target == Where.Home ? Where.Home : Where.Clear;
            else
                wanted = Where.Home;
            if (wanted != _target)
                Go(wanted);

            Spot spot = SpotFor(_target);
            if (_walking)
            {
                Vector3 to = spot.Position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f)
                    Turn(Quaternion.LookRotation(to, Vector3.up));
                transform.position = Vector3.MoveTowards(transform.position, spot.Position, walkSpeed * Time.deltaTime);
                if ((transform.position - spot.Position).sqrMagnitude < 0.0001f)
                {
                    _walking = false;
                    Play(spot.State);
                }
                return;
            }
            Turn(Quaternion.Euler(0f, spot.YawDegrees, 0f));
        }

        private void Go(Where where)
        {
            _target = where;
            _walking = true;
            Play(walkState);
        }

        private Spot SpotFor(Where where) => where == Where.InArea ? workArea : where == Where.Clear ? clear : home;

        private void Turn(Quaternion target)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnDegreesPerSecond * Time.deltaTime);
        }

        private void Play(string state)
        {
            if (animator != null)
                animator.CrossFadeInFixedTime(Animator.StringToHash(state), 0.25f);
        }
    }
}
