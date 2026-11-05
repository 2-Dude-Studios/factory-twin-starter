using System.Collections;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M15): starts the training the start menu asked for once Cell.unity has loaded (and the headset is
    /// back on, if it was on): the fire drill at the fire training station, the lockout run at the line control
    /// station, the robot zone from the cell's start; free roam starts nothing. Opening Cell.unity directly asks for
    /// nothing, so the cell behaves as before.
    /// </summary>
    public sealed class TrainingLauncher : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private VrSwitch vrSwitch;
        [SerializeField] private VrMenu vrMenu;
        [SerializeField] private VrFireMenu fireMenu;
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

            switch (mode)
            {
                case TrainingMode.FireSafety:
                    if (vr && fireMenu != null)
                        fireMenu.EnterDrill();
                    break;
                case TrainingMode.Lockout:
                    TrainingCommands.StartTraining(context.Bus);
                    if (vr && vrMenu != null)
                        vrMenu.GoToSpot("menu-go-station");
                    break;
                case TrainingMode.RobotZone:
                    if (vr && vrMenu != null)
                        vrMenu.GoToSpot("menu-go-cell");
                    break;
            }
            started = mode;
            Debug.Log($"[TrainingLauncher] {TrainingLaunch.Title(mode)} started ({(vr ? "headset" : "desktop")}).", this);
        }
    }
}
