using System.Collections;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub (M28): on the Quest the start menu is in the cell, so a training starts and ends here instead of
    /// with a scene load. It sits beside the VR switch; SceneNavigation hands it the menu's choices and every
    /// "main menu" button. Starting a training puts the cell back first (if one was running) and hands the mode to the
    /// TrainingLauncher, as a scene load with a request did; returning to the hub puts the cell back (fire out and
    /// the alarm reset, the robot drill and the electrical scenario ended, the CNC unlocked and on, the safety circuit
    /// reset, the line started) and says so on cell/training/hub, which the drill pages, the PPE bench and the hub menu
    /// listen to, then stands the trainee at the hub facing the menu.
    /// </summary>
    public sealed class HubSession : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private VrSwitch vrSwitch;
        [SerializeField] private TrainingLauncher launcher;
        [SerializeField] private XROrigin origin;
        [Tooltip("The hub's start spot (TrainingAreaStart, Hub): where the trainee stands, facing the hub menu.")]
        [SerializeField] private TrainingAreaStart hubStart;
        [SerializeField, Min(1f)] private float vrWaitSeconds = 10f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private TrainingMode current;

        public TrainingMode Current => current;

        // At start-up the trainee begins at the hub, once the headset is up (the Quest build opens on the cell).
        private IEnumerator Start()
        {
            float waited = 0f;
            while (vrSwitch != null && !vrSwitch.VrRunning && waited < vrWaitSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
            if (current == TrainingMode.None)
                StandAtHub();
        }

        /// <summary>Starts a training from the hub menu (or another training), optionally at a given level.</summary>
        public void StartTraining(TrainingMode mode, int level = 0)
        {
            if (mode == TrainingMode.None || launcher == null)
                return;
            if (current != TrainingMode.None)
                PutCellBack();
            current = mode;
            launcher.Launch(mode);
            if (level > 0 && mode == TrainingMode.RobotZone)
                StartCoroutine(RobotLevel(level));
        }

        /// <summary>Ends whatever runs and brings the trainee back to the hub.</summary>
        public void ReturnToHub()
        {
            PutCellBack();
            current = TrainingMode.None;
            StandAtHub();
        }

        // The launcher starts the robot drill's first level without a star; a level picked on the menu replaces it.
        private IEnumerator RobotLevel(int level)
        {
            float waited = 0f;
            while (waited < 2f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Publish(CellTopics.TrainingRobotLevel, level);
        }

        private void PutCellBack()
        {
            Publish(CellTopics.FireScenario, 0);
            Publish(CellTopics.FireAlarmReset, 1);
            Publish(CellTopics.TrainingRobotLevel, 0);
            Publish(CellTopics.ElecScenario, 0);
            Publish(CellTopics.PressLockCommand, 0);
            Publish(CellTopics.PressIsolatorCommand, 0);
            Publish(CellTopics.SafetyReset, 1);
            Publish(CellTopics.SafetyReset, 0);
            Publish(CellTopics.LineStart, 1);
            Publish(CellTopics.LineStart, 0);
            Publish(CellTopics.TrainingHub, 1);
        }

        private void StandAtHub()
        {
            if (hubStart != null && origin != null)
                RigTeleport.GoTo(origin, hubStart.FloorPosition, hubStart.YawDegrees);
        }

        private void Publish(TopicPath topic, double value) =>
            context.Bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);
    }
}
