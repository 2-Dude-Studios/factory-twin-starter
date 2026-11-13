using System.Collections;
using System;
using TwoDude.FactoryTwin.Twin;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M15): starts the training the start menu asked for once Cell.unity has loaded (and the headset is
    /// back on, if it was on), in its own area of the hall (M24): the fire drill at the fire training station, the
    /// lockout run at the line control station, the robot zone south of the cell, the electrical drill in its bay,
    /// free roam at the hub. The desktop trainee and the headset rig are put on the same start spot. Opening
    /// Cell.unity directly asks for nothing, so the cell behaves as before.
    /// </summary>
    public sealed class TrainingLauncher : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private VrSwitch vrSwitch;
        [SerializeField] private VrMenu vrMenu;
        [SerializeField] private VrFireMenu fireMenu;
        [Tooltip("The start spot of each training area (Build Training Areas). Left empty, the tablet's Go to spots are used.")]
        [SerializeField] private TrainingAreaStart[] starts = Array.Empty<TrainingAreaStart>();
        [SerializeField] private TraineeWalker trainee;
        [SerializeField] private XROrigin origin;
        [SerializeField, Min(1f)] private float vrWaitSeconds = 10f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private TrainingMode started;

        private IEnumerator Start()
        {
            TrainingMode mode = TrainingLaunch.Take();
            if (mode == TrainingMode.None)
                yield break;
            // Let VrSwitch.Start run, then wait for OpenXR to come back if it is coming.
            yield return null;
            float waited = 0f;
            while (vrSwitch != null && vrSwitch.VrWanted && !vrSwitch.VrRunning && waited < vrWaitSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
            bool vr = vrSwitch != null && vrSwitch.VrRunning;

            TrainingAreaStart start = StartOf(TrainingLaunch.AreaFor(mode));
            if (start != null && trainee != null)
                trainee.Teleport(start.FloorPosition.x, start.FloorPosition.z, start.YawDegrees, 0f);

            switch (mode)
            {
                case TrainingMode.FireSafety:
                    // The fire page takes the headset to the station itself and swaps the tablet to the drill.
                    if (vr && fireMenu != null)
                        fireMenu.EnterDrill();
                    break;
                case TrainingMode.Lockout:
                    TrainingCommands.StartTraining(context.Bus);
                    if (vr)
                        GoTo(start, "menu-go-station");
                    break;
                case TrainingMode.RobotZone:
                    if (vr)
                        GoTo(start, "menu-go-cell");
                    StartRobotDrill();
                    break;
                case TrainingMode.Electrical:
                    // The bay's first scenario; the tablet's electrical page opens with it (VrElectricalMenu, M27).
                    if (vr)
                        GoTo(start, null);
                    context.Bus.Publish(CellTopics.ElecScenario, SignalValue.FromNumber(1), CellContext.NowUnixMs);
                    break;
                default:
                    if (vr)
                        GoTo(start, null);
                    break;
            }
            started = mode;
            Debug.Log($"[TrainingLauncher] {TrainingLaunch.Title(mode)} started ({(vr ? "headset" : "desktop")}).", this);
        }

        // The robot cell drill's next level for this trainee: the first one without a star (the assessment once all
        // have one).
        private void StartRobotDrill()
        {
            var controller = context.GetComponentInChildren<TrainingController>(true);
            BadgeBook book = BadgeBook.Load(controller != null ? controller.TraineeName : "Sam Rivera");
            System.Collections.Generic.IReadOnlyList<int> stars = book.Stars(TrainingMode.RobotZone);
            int level = DrillLevels.Count;
            for (int i = 0; i < stars.Count; i++)
            {
                if (stars[i] == 0)
                {
                    level = i + 1;
                    break;
                }
            }
            context.Bus.Publish(CellTopics.TrainingRobotLevel, SignalValue.FromNumber(level), CellContext.NowUnixMs);
        }

        private TrainingAreaStart StartOf(TrainingArea area)
        {
            foreach (TrainingAreaStart start in starts)
            {
                if (start != null && start.Area == area)
                    return start;
            }
            return null;
        }

        // The headset over the area's start spot; without one, the tablet's own Go to spot (as before M24).
        private void GoTo(TrainingAreaStart start, string fallbackSpot)
        {
            if (start != null && origin != null)
                RigTeleport.GoTo(origin, start.FloorPosition, start.YawDegrees);
            else if (fallbackSpot != null && vrMenu != null)
                vrMenu.GoToSpot(fallbackSpot);
        }
    }
}
