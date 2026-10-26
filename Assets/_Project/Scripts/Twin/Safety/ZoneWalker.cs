using System;
using UnityEngine;
using UnityEngine.AI;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Safety zone: where the zone walker is sent, published as a number on cell/safety/worker_walk.</summary>
    public enum WorkerWalk
    {
        /// <summary>Home, outside both zones.</summary>
        Home = 0,

        /// <summary>Into the warning zone only: the arm slows, nothing stops.</summary>
        WarningZone = 1,

        /// <summary>Into the protective zone: the protective stop.</summary>
        ProtectiveZone = 2,
    }

    /// <summary>
    /// Safety zone (M12): a simulated worker the commissioning suite walks into the robot zone. A NavMeshAgent on
    /// the cell's walk area (baked by Build Simulated Workers) finds the way round the conveyors, cones and CNC to
    /// each spot. The body is a kinematic capsule on the Trainee layer, so the real zone sensors see the worker
    /// exactly as they see the trainee; nothing is forced.
    /// </summary>
    public sealed class ZoneWalker : MonoBehaviour
    {
        [Serializable]
        private sealed class Spot
        {
            [SerializeField] private Vector3 position;
            [SerializeField] private float yawDegrees;

            public Vector3 Position => position;
            public float YawDegrees => yawDegrees;
        }

        [SerializeField] private CellContext context;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [Tooltip("Spots on the walk area, snapped to the NavMesh when it is baked.")]
        [SerializeField] private Spot home = new Spot();
        [SerializeField] private Spot warningZone = new Spot();
        [SerializeField] private Spot protectiveZone = new Spot();
        [SerializeField] private float turnDegreesPerSecond = 360f;
        [SerializeField] private string walkState = "Walk";
        [SerializeField] private string standState = "Idle";

        private IDisposable _subscription;
        private Spot _target;
        private bool _walking;
        private int _walkHash, _standHash;

        /// <summary>Parses a cell/safety/worker_walk value; anything unknown sends the worker home.</summary>
        public static WorkerWalk Parse(double value)
        {
            int v = (int)Math.Round(value);
            return v == (int)WorkerWalk.WarningZone || v == (int)WorkerWalk.ProtectiveZone ? (WorkerWalk)v : WorkerWalk.Home;
        }

        private void Awake()
        {
            _walkHash = Animator.StringToHash(walkState);
            _standHash = Animator.StringToHash(standState);
        }

        private void OnEnable()
        {
            _subscription = context.Bus.Subscribe(CellTopics.SafetyWorkerWalk.ToString(), OnCommand);
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void Start()
        {
            _target = home;
            agent.Warp(home.Position);
            transform.rotation = Quaternion.Euler(0f, home.YawDegrees, 0f);
            Play(_standHash);
        }

        private void OnCommand(SignalMessage message)
        {
            switch (Parse(message.Value.AsNumber))
            {
                case WorkerWalk.WarningZone: _target = warningZone; break;
                case WorkerWalk.ProtectiveZone: _target = protectiveZone; break;
                default: _target = home; break;
            }
            if (!agent.isOnNavMesh)
                return;
            agent.SetDestination(_target.Position);
            if (!_walking)
            {
                _walking = true;
                agent.updateRotation = true;
                Play(_walkHash);
            }
        }

        private void Update()
        {
            if (_target == null)
                return;
            if (_walking)
            {
                if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.02f)
                    return;
                _walking = false;
                agent.updateRotation = false;
                Play(_standHash);
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, _target.YawDegrees, 0f),
                turnDegreesPerSecond * Time.deltaTime);
        }

        private void Play(int stateHash)
        {
            if (animator != null)
                animator.CrossFadeInFixedTime(stateHash, 0.2f);
        }
    }
}
