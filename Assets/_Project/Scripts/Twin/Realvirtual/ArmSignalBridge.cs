using realvirtual;
using TwoDude.FactoryTwin.Robot;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Pick station: wires the arm to realvirtual signals, keeping signals the only bridge between the 3D arm
    /// and the data spine. Reads the pick command, reports busy and the six joint angles, and drives the
    /// gripper's command signal. While cell/arm/inhibit is set (a commissioning stimulus) the arm ignores pick
    /// commands, as an arm that is not ready would. The speed-scale signal (cell/arm/speed_scale, from the safety
    /// zone) slows or freezes the arm; with no signal assigned the arm runs at full speed.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class ArmSignalBridge : MonoBehaviour
    {
        [SerializeField] private ArmController arm;
        [SerializeField] private CellContext context;

        [Header("Signals")]
        [SerializeField] private PLCOutputBool pickCommand;
        [SerializeField] private PLCInputBool busy;
        [SerializeField] private PLCOutputBool gripCommand;
        [SerializeField] private PLCInputFloat[] jointAnglesDeg = new PLCInputFloat[6];
        [SerializeField] private PLCOutputFloat speedScale;

        private bool _lastCommand;

        private bool Inhibited() =>
            context != null && context.Bus.TryGetLast(CellTopics.ArmInhibit, out SignalMessage m) && m.Value.AsNumber != 0d;

        private void FixedUpdate()
        {
            arm.SpeedScale = speedScale != null ? speedScale.Value : 1f;

            bool commanded = pickCommand != null && pickCommand.Value;
            if (commanded && !_lastCommand && !Inhibited())
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
