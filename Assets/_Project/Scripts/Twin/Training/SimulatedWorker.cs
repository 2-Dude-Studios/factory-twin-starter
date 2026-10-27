using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training (M12): a simulated co-worker beside the cell, one of the affected employees of a lockout. It walks
    /// between its stops and plays each stop's animation there (working, or standing), always outside the robot
    /// zone. When the trainee notifies the crew of the shutdown it stops work and stands clear at its stand-clear
    /// spot, facing the cell, until it is told the lock has come off (<see cref="CrewRules"/>). It has no collider
    /// and is not on the Trainee layer, so the zone sensors never see it, and it publishes nothing.
    /// </summary>
    public sealed class SimulatedWorker : MonoBehaviour
    {
        [Serializable]
        private sealed class Stop
        {
            [SerializeField] private Vector3 position;
            [SerializeField] private float yawDegrees;
            [SerializeField] private float workSeconds = 8f;
            [SerializeField] private string state = "Working";

            public Vector3 Position => position;
            public float YawDegrees => yawDegrees;
            public float WorkSeconds => workSeconds;
            public string State => state;
        }

        [SerializeField] private Animator animator;
        [Tooltip("Reads the training run, so the worker can stand clear during a lockout. Left empty, it just works.")]
        [SerializeField] private CellContext context;
        [Tooltip("Where the worker stands, which way it faces, for how long and in which animator state, in world space. Two stops at one spot: it changes what it does without walking.")]
        [SerializeField] private Stop[] stops = Array.Empty<Stop>();
        [Tooltip("Where the worker waits, facing the cell, while the CNC is locked out.")]
        [SerializeField] private Stop standClear = new Stop();
        [SerializeField] private float walkSpeed = 1.1f;
        [SerializeField] private float turnDegreesPerSecond = 270f;
        [SerializeField] private string walkState = "Walk";

        private int _walkHash, _clearHash;
        private int[] _stateHashes = Array.Empty<int>();
        private int _index;
        private float _workLeft;
        private bool _walking;
        private bool _clear;

        private void Awake()
        {
            _walkHash = Animator.StringToHash(walkState);
            _clearHash = Animator.StringToHash(standClear.State);
            _stateHashes = new int[stops.Length];
            for (int i = 0; i < stops.Length; i++)
                _stateHashes[i] = Animator.StringToHash(stops[i].State);
        }

        private void Start()
        {
            if (stops.Length == 0)
                return;
            transform.SetPositionAndRotation(stops[0].Position, Quaternion.Euler(0f, stops[0].YawDegrees, 0f));
            BeginWork(0);
        }

        private void Update()
        {
            if (stops.Length == 0)
                return;
            bool clear = ShouldStandClear();
            if (clear != _clear)
            {
                _clear = clear;
                _walking = true;
                Play(_walkHash);
            }

            Stop stop = _clear ? standClear : stops[_index];
            if (_walking)
            {
                if (Walk(stop.Position))
                {
                    _walking = false;
                    if (_clear)
                        Play(_clearHash);
                    else
                        BeginWork(_index);
                }
                return;
            }

            Turn(Quaternion.Euler(0f, stop.YawDegrees, 0f));
            if (_clear)
                return;
            _workLeft -= Time.deltaTime;
            if (_workLeft <= 0f && stops.Length > 1)
            {
                int next = (_index + 1) % stops.Length;
                if ((stops[next].Position - transform.position).sqrMagnitude < 0.0001f)
                {
                    BeginWork(next);
                    return;
                }
                _index = next;
                _walking = true;
                Play(_walkHash);
            }
        }

        private bool ShouldStandClear()
        {
            if (context == null)
                return false;
            SignalBus bus = context.Bus;
            int step = bus.TryGetLast(CellTopics.TrainingStep, out SignalMessage s) ? (int)s.Value.AsNumber : 0;
            var outcome = bus.TryGetLast(CellTopics.TrainingResult, out SignalMessage r) ? (TrainingOutcome)(int)r.Value.AsNumber : TrainingOutcome.Idle;
            return CrewRules.StandClear(step, outcome);
        }

        // One frame of walking toward the target; true on arrival.
        private bool Walk(Vector3 target)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
                Turn(Quaternion.LookRotation(to, Vector3.up));
            transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
            return (transform.position - target).sqrMagnitude < 0.0001f;
        }

        private void BeginWork(int index)
        {
            _index = index;
            _walking = false;
            _workLeft = stops[index].WorkSeconds;
            Play(_stateHashes[index]);
        }

        private void Turn(Quaternion target)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnDegreesPerSecond * Time.deltaTime);
        }

        private void Play(int stateHash)
        {
            if (animator != null)
                animator.CrossFadeInFixedTime(stateHash, 0.25f);
        }
    }
}
