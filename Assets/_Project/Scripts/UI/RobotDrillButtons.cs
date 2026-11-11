using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training areas (M24): the dashboard's Robot cell drill buttons (robot-1 … robot-4, robot-end in
    /// Dashboard.uxml). Each publishes cell/training/robot_level: the level to start, or 0 to end the one running.
    /// The trainer's controls; the RobotCellScorer does the rest.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class RobotDrillButtons : MonoBehaviour
    {
        private static readonly string[] Names = { "robot-1", "robot-2", "robot-3", "robot-4", "robot-end" };

        [SerializeField] private UIDocument document;
        [SerializeField] private CellContext context;

        private Action[] _handlers = Array.Empty<Action>();

        private void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null || context == null)
            {
                Debug.LogError("[RobotDrillButtons] Assign the dashboard's UIDocument and Context (Cell).", this);
                enabled = false;
                return;
            }
            _handlers = new Action[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                int level = i < DrillLevels.Count ? i + 1 : 0;
                _handlers[i] = () => context.Bus.Publish(CellTopics.TrainingRobotLevel, SignalValue.FromNumber(level), CellContext.NowUnixMs);
                Button button = root.Q<Button>(Names[i]);
                if (button != null)
                    button.clicked += _handlers[i];
            }
        }

        private void OnDisable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            for (int i = 0; root != null && i < _handlers.Length; i++)
            {
                Button button = root.Q<Button>(Names[i]);
                if (button != null)
                    button.clicked -= _handlers[i];
            }
            _handlers = Array.Empty<Action>();
        }
    }
}
