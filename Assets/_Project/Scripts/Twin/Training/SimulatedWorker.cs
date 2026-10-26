using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training (M12): a simulated co-worker beside the cell, scene dressing for the training beats. It walks between
    /// its stops and plays each stop's animation there (working, or standing), always outside the robot zone. It has
    /// no collider and is not on the Trainee layer, so the zone sensors never see it, and it publishes nothing.
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
        [Tooltip("Where the worker stands, which way it faces, for how long and in which animator state, in world space. Two stops at one spot: it changes what it does without walking.")]
        [SerializeField] private Stop[] stops = Array.Empty<Stop>();
        [SerializeField] private float walkSpeed = 1.1f;
        [SerializeField] private float turnDegreesPerSecond = 270f;
        [SerializeField] private string walkState = "Walk";

        private int _walkHash;
        private int[] _stateHashes = Array.Empty<int>();
        private int _index;
        private float _workLeft;
        private bool _walking;

        private void Awake()
        {
            _walkHash = Animator.StringToHash(walkState);
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
            Stop stop = stops[_index];
            if (_walking)
            {
                Vector3 to = stop.Position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f)
                    Turn(Quaternion.LookRotation(to, Vector3.up));
                transform.position = Vector3.MoveTowards(transform.position, stop.Position, walkSpeed * Time.deltaTime);
                if ((transform.position - stop.Position).sqrMagnitude < 0.0001f)
                    BeginWork(_index);
                return;
            }

            Turn(Quaternion.Euler(0f, stop.YawDegrees, 0f));
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
