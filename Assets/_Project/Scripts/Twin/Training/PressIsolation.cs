using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Press station: what a command to the CNC's isolator or lock did.</summary>
    public enum IsolationResult
    {
        Done = 0,

        /// <summary>Already in the state asked for.</summary>
        NoChange = 1,

        /// <summary>The isolator cannot be switched back on while a lock hangs on it.</summary>
        RefusedLocked = 2,

        /// <summary>A lock only fits the hasp while the isolator is off.</summary>
        RefusedNotIsolated = 3,
    }

    /// <summary>
    /// Press station: the CNC's main isolator and its lock hasp (M11), as pure rules. The isolator switches off at
    /// any time; a lock fits only while it is off; while a lock hangs on it, it cannot be switched back on.
    /// </summary>
    public sealed class IsolatorLock
    {
        public bool Isolated { get; private set; }
        public bool Locked { get; private set; }

        /// <summary>Switch the isolator off (<paramref name="off"/> true, isolating the CNC) or back on.</summary>
        public IsolationResult SetIsolator(bool off)
        {
            if (off == Isolated)
                return IsolationResult.NoChange;
            if (!off && Locked)
                return IsolationResult.RefusedLocked;
            Isolated = off;
            return IsolationResult.Done;
        }

        /// <summary>Hang a lock and tag on the isolator (<paramref name="apply"/> true) or take it off.</summary>
        public IsolationResult SetLock(bool apply)
        {
            if (apply == Locked)
                return IsolationResult.NoChange;
            if (apply && !Isolated)
                return IsolationResult.RefusedNotIsolated;
            Locked = apply;
            return IsolationResult.Done;
        }
    }

    /// <summary>
    /// Press station: owns the CNC's isolator and lock (M11). Acts on cell/press/isolator_cmd (1 off, 0 on) and
    /// cell/press/lock_cmd (1 apply, 0 remove) under the rules of <see cref="IsolatorLock"/>, and publishes
    /// cell/press/isolated and cell/press/lockout. The logic sources read those two: an isolated CNC holds the line,
    /// and a start is refused while the CNC is isolated or locked out.
    /// </summary>
    public sealed class PressIsolation : MonoBehaviour
    {
        [SerializeField] private CellContext context;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool isolated;
        [SerializeField] private bool locked;
        [SerializeField] private int refused;

        private readonly IsolatorLock _state = new IsolatorLock();
        private IDisposable _isolatorSubscription;
        private IDisposable _lockSubscription;

        public bool Isolated => _state.Isolated;
        public bool Locked => _state.Locked;

        private void OnEnable()
        {
            SignalBus bus = context.Bus;
            _isolatorSubscription = bus.Subscribe(CellTopics.PressIsolatorCommand.ToString(), OnIsolator);
            _lockSubscription = bus.Subscribe(CellTopics.PressLockCommand.ToString(), OnLock);
        }

        private void OnDisable()
        {
            _isolatorSubscription?.Dispose();
            _isolatorSubscription = null;
            _lockSubscription?.Dispose();
            _lockSubscription = null;
        }

        // Baselines: the CNC starts energised and unlocked.
        private void Start()
        {
            Publish();
        }

        private void OnIsolator(SignalMessage message)
        {
            Apply(_state.SetIsolator(message.Value.AsNumber != 0d), message.Value.AsNumber != 0d ? "switch the isolator off" : "switch the isolator on");
        }

        private void OnLock(SignalMessage message)
        {
            Apply(_state.SetLock(message.Value.AsNumber != 0d), message.Value.AsNumber != 0d ? "apply the lock" : "remove the lock");
        }

        private void Apply(IsolationResult result, string what)
        {
            if (result == IsolationResult.Done)
            {
                Publish();
                return;
            }
            if (result == IsolationResult.NoChange)
                return;
            refused++;
            Debug.Log($"[PressIsolation] Could not {what}: {(result == IsolationResult.RefusedLocked ? "a lock hangs on the isolator" : "the isolator is not off")}.", this);
        }

        private void Publish()
        {
            isolated = _state.Isolated;
            locked = _state.Locked;
            long now = CellContext.NowUnixMs;
            context.Bus.Publish(CellTopics.PressIsolated, SignalValue.FromBool(isolated), now);
            context.Bus.Publish(CellTopics.PressLockout, SignalValue.FromBool(locked), now);
        }
    }
}
