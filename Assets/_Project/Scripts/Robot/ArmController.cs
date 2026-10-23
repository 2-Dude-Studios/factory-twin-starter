using System.Collections.Generic;
using UnityEngine;

namespace TwoDude.FactoryTwin.Robot
{
    /// <summary>
    /// Pick station: drives the six UR5e ArticulationBody joints through the baked program and reports the
    /// arm's state. Knows nothing about signals or MQTT — ArmSignalBridge does that, so this stays testable
    /// and the arm can be swapped without touching the data spine.
    /// </summary>
    public sealed class ArmController : MonoBehaviour
    {
        [Header("Joints, base to tool")]
        [SerializeField] private ArticulationBody[] joints = new ArticulationBody[ArmWaypoint.JointCount];

        [Header("Joint drives")]
        [Tooltip("Position-drive gains applied to every joint on Awake; the URDF import leaves them at zero.")]
        [SerializeField, Min(0f)] private float driveStiffness = 100000f;
        [SerializeField, Min(0f)] private float driveDamping = 10000f;
        [SerializeField, Min(0f)] private float driveForceLimit = 1000f;

        [Header("Motion")]
        [SerializeField, Min(1f)] private float maxSpeedDegPerSecond = 110f;
        [SerializeField, Min(0.05f)] private float arrivalToleranceDeg = 1.5f;
        [SerializeField] private List<SerializedWaypoint> waypoints = new List<SerializedWaypoint>();

        [Header("Status (play mode, read-only)")]
        [SerializeField] private string currentWaypoint;
        [SerializeField] private bool busy;
        [SerializeField] private bool gripClosed;
        [SerializeField] private int cyclesCompleted;
        [SerializeField, Range(0f, 1f)] private float speedScale = 1f;

        private ArmProgram _program;
        private readonly float[] _measured = new float[ArmWaypoint.JointCount];

        /// <summary>True from the moment a pick is accepted until the arm is home again.</summary>
        public bool IsBusy => busy;

        /// <summary>True while the gripper should hold a part.</summary>
        public bool GripClosed => gripClosed;

        public int CyclesCompleted => cyclesCompleted;

        /// <summary>Measured joint angles in degrees, base to tool.</summary>
        public IReadOnlyList<float> JointAnglesDeg => _measured;

        /// <summary>
        /// The arm's speed as a fraction of full speed (0–1), set by the safety zone: reduced in the warning zone,
        /// 0 on a protective stop. At 0 the program is frozen where it is, dwell timers included, and resumes from
        /// the same point when the speed comes back.
        /// </summary>
        public float SpeedScale
        {
            get => speedScale;
            set => speedScale = Mathf.Clamp01(value);
        }

        /// <summary>Start one pick-and-place pass. Returns false if the arm is already moving.</summary>
        public bool RequestPick() => _program != null && _program.Start();

        private void Awake()
        {
            var baked = new List<ArmWaypoint>(waypoints.Count);
            foreach (SerializedWaypoint w in waypoints)
                baked.Add(w.ToWaypoint());
            _program = new ArmProgram(baked, maxSpeedDegPerSecond, arrivalToleranceDeg);
            ConfigureDrives();
        }

        /// <summary>The URDF import leaves the drives limp; without gains the arm sags instead of holding a pose.</summary>
        private void ConfigureDrives()
        {
            foreach (ArticulationBody body in joints)
            {
                if (body == null)
                    continue;
                ArticulationDrive drive = body.xDrive;
                drive.stiffness = driveStiffness;
                drive.damping = driveDamping;
                drive.forceLimit = driveForceLimit;
                body.xDrive = drive;
            }
        }

        private void FixedUpdate()
        {
            ReadJoints();
            _program.Tick(_measured, Time.fixedDeltaTime * speedScale);
            ApplyTargets();

            busy = _program.IsRunning;
            gripClosed = _program.GripClosed;
            currentWaypoint = _program.CurrentWaypoint;
            cyclesCompleted = _program.CompletedCycles;
        }

        private void ReadJoints()
        {
            for (int j = 0; j < joints.Length && j < _measured.Length; j++)
            {
                ArticulationBody body = joints[j];
                // jointPosition holds this body's own degrees of freedom; a revolute joint has one, in radians.
                _measured[j] = body != null && body.dofCount > 0 ? Mathf.Rad2Deg * body.jointPosition[0] : 0f;
            }
        }

        private void ApplyTargets()
        {
            IReadOnlyList<float> commanded = _program.Commanded;
            for (int j = 0; j < joints.Length && j < commanded.Count; j++)
            {
                ArticulationBody body = joints[j];
                if (body == null)
                    continue;
                ArticulationDrive drive = body.xDrive;
                drive.target = commanded[j];
                body.xDrive = drive;
            }
        }

        [System.Serializable]
        private sealed class SerializedWaypoint
        {
            [SerializeField] private string name;
            [SerializeField] private float[] jointsDeg = new float[ArmWaypoint.JointCount];
            [SerializeField, Min(0f)] private float dwellSeconds;
            [SerializeField] private GripAction grip = GripAction.None;

            public ArmWaypoint ToWaypoint() => new ArmWaypoint(name, jointsDeg, dwellSeconds, grip);
        }
    }
}
