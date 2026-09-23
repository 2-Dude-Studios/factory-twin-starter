using realvirtual;
using TwoDude.FactoryTwin.Robot;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Pick station: wires the arm to realvirtual signals, keeping signals the only bridge between the 3D arm
    /// and the data spine. Reads the pick command, reports busy and the six joint angles, and drives the
    /// gripper's command signal.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class ArmSignalBridge : MonoBehaviour
    {
        [SerializeField] private ArmController arm;

        [Header("Signals")]
        [SerializeField] private PLCOutputBool pickCommand;
        [SerializeField] private PLCInputBool busy;
        [SerializeField] private PLCOutputBool gripCommand;
        [SerializeField] private PLCInputFloat[] jointAnglesDeg = new PLCInputFloat[6];

        private bool _lastCommand;

        private void FixedUpdate()
        {
            bool commanded = pickCommand != null && pickCommand.Value;
            if (commanded && !_lastCommand)
                arm.RequestPick();
            _lastCommand = commanded;

            if (busy != null)
                busy.Value = arm.IsBusy;
            if (gripCommand != null)
                gripCommand.Value = arm.GripClosed;

            var angles = arm.JointAnglesDeg;
            for (int j = 0; j < jointAnglesDeg.Length && j < angles.Count; j++)
            {
                if (jointAnglesDeg[j] != null)
                    jointAnglesDeg[j].Value = angles[j];
            }
        }
    }
}
