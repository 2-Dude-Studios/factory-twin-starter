using System;
using System.Collections.Generic;

namespace TwoDude.FactoryTwin.Robot
{
    /// <summary>Pick station: what the gripper should do on arrival at a waypoint.</summary>
    public enum GripAction
    {
        None = 0,
        Close = 1,
        Open = 2,
    }

    /// <summary>Pick station: one baked arm pose — six joint angles in degrees, plus what to do there.</summary>
    public readonly struct ArmWaypoint
    {
        public const int JointCount = 6;

        public string Name { get; }
        public IReadOnlyList<float> JointsDeg { get; }
        public float DwellSeconds { get; }
        public GripAction Grip { get; }

        public ArmWaypoint(string name, IReadOnlyList<float> jointsDeg, float dwellSeconds, GripAction grip)
        {
            if (jointsDeg == null || jointsDeg.Count != JointCount)
                throw new ArgumentException($"A waypoint needs exactly {JointCount} joint angles.", nameof(jointsDeg));

            Name = name;
            JointsDeg = jointsDeg;
            DwellSeconds = dwellSeconds;
            Grip = grip;
        }
    }

    /// <summary>
    /// Pick station: plays a baked joint-angle program (the build plan's keyframed path, not runtime IK).
    /// Commanded angles ramp at a fixed joint speed; a waypoint completes once the commanded pose has arrived,
    /// the measured joints are inside the tolerance and the dwell has elapsed. Pure logic, no Unity types.
    /// </summary>
    public sealed class ArmProgram
    {
        private readonly IReadOnlyList<ArmWaypoint> _waypoints;
        private readonly float _maxSpeedDegPerSecond;
        private readonly float _toleranceDeg;
        private readonly float[] _commanded = new float[ArmWaypoint.JointCount];

        private double _dwellLeft;
        private bool _arrived;

        public ArmProgram(IReadOnlyList<ArmWaypoint> waypoints, float maxSpeedDegPerSecond, float toleranceDeg)
        {
            if (waypoints == null || waypoints.Count == 0)
                throw new ArgumentException("The arm needs at least one waypoint.", nameof(waypoints));
            if (!(maxSpeedDegPerSecond > 0f))
                throw new ArgumentOutOfRangeException(nameof(maxSpeedDegPerSecond));
            if (!(toleranceDeg > 0f))
                throw new ArgumentOutOfRangeException(nameof(toleranceDeg));

            _waypoints = waypoints;
            _maxSpeedDegPerSecond = maxSpeedDegPerSecond;
            _toleranceDeg = toleranceDeg;
            for (int j = 0; j < ArmWaypoint.JointCount; j++)
                _commanded[j] = waypoints[0].JointsDeg[j];
        }

        public bool IsRunning { get; private set; }
        public bool GripClosed { get; private set; }
        public int Index { get; private set; }
        public int CompletedCycles { get; private set; }

        /// <summary>Commanded joint angles in degrees; feed these to the joints.</summary>
        public IReadOnlyList<float> Commanded => _commanded;

        public string CurrentWaypoint => _waypoints[Index].Name;

        /// <summary>Start one pass over the program. Ignored while a pass is already running.</summary>
        public bool Start()
        {
            if (IsRunning)
                return false;
            IsRunning = true;
            Index = 0;
            _arrived = false;
            _dwellLeft = 0d;
            return true;
        }

        /// <summary>Advance the program. <paramref name="measuredDeg"/> is what the joints actually report.</summary>
        public void Tick(IReadOnlyList<float> measuredDeg, double deltaSeconds)
        {
            if (measuredDeg == null || measuredDeg.Count < ArmWaypoint.JointCount)
                throw new ArgumentException("Six measured joint angles are required.", nameof(measuredDeg));
            if (deltaSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (!IsRunning)
                return;

            ArmWaypoint target = _waypoints[Index];
            float step = (float)(_maxSpeedDegPerSecond * deltaSeconds);
            bool commandedThere = true;
            for (int j = 0; j < ArmWaypoint.JointCount; j++)
            {
                float goal = target.JointsDeg[j];
                float delta = goal - _commanded[j];
                if (Math.Abs(delta) <= step)
                {
                    _commanded[j] = goal;
                }
                else
                {
                    _commanded[j] += Math.Sign(delta) * step;
                    commandedThere = false;
                }
            }

            if (!commandedThere || !WithinTolerance(measuredDeg, target))
                return;

            if (!_arrived)
            {
                _arrived = true;
                _dwellLeft = target.DwellSeconds;
                if (target.Grip == GripAction.Close)
                    GripClosed = true;
                else if (target.Grip == GripAction.Open)
                    GripClosed = false;
            }

            _dwellLeft -= deltaSeconds;
            if (_dwellLeft > 0d)
                return;

            _arrived = false;
            if (Index + 1 < _waypoints.Count)
            {
                Index++;
            }
            else
            {
                Index = 0;
                IsRunning = false;
                CompletedCycles++;
            }
        }

        private bool WithinTolerance(IReadOnlyList<float> measuredDeg, ArmWaypoint target)
        {
            for (int j = 0; j < ArmWaypoint.JointCount; j++)
            {
                if (Math.Abs(measuredDeg[j] - target.JointsDeg[j]) > _toleranceDeg)
                    return false;
            }
            return true;
        }
    }
}
