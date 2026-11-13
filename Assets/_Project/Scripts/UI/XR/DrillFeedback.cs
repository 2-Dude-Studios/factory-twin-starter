using System;
using System.Collections.Generic;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.XR;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub's game layer (M28): live feedback in the drills that had none. Each step done floats a
    /// "+points" popup in front of the trainee with a short controller buzz; a mistake floats a red one with a
    /// stronger buzz. The robot cell's steps from cell/training/robot_step (points from its scorer), the lockout's
    /// from cell/training/step and cell/training/mistake. The fire drill has its own popups already.
    /// </summary>
    public sealed class DrillFeedback : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private ScorePopups popups;
        [SerializeField] private RobotCellScorer robot;

        private IDisposable[] _subscriptions = Array.Empty<IDisposable>();
        private readonly List<InputDevice> _hands = new List<InputDevice>();
        private int _robotSteps;
        private int _robotPoints;
        private int _lockoutStep = -1;

        private void OnEnable()
        {
            if (context == null)
                return;
            _subscriptions = new[]
            {
                context.Bus.Subscribe(CellTopics.TrainingRobotStep.ToString(), OnRobotStep),
                context.Bus.Subscribe(CellTopics.TrainingRobotLevel.ToString(), _ => { _robotSteps = 0; _robotPoints = 0; }),
                context.Bus.Subscribe(CellTopics.TrainingStep.ToString(), OnLockoutStep),
                context.Bus.Subscribe(CellTopics.TrainingRun.ToString(), _ => _lockoutStep = -1),
                context.Bus.Subscribe(CellTopics.TrainingMistake.ToString(), _ => Show(-LockoutLevel.MistakePoints, "Out of order", true)),
            };
        }

        private void OnDisable()
        {
            foreach (IDisposable s in _subscriptions)
                s?.Dispose();
            _subscriptions = Array.Empty<IDisposable>();
        }

        private void OnRobotStep(SignalMessage m)
        {
            int steps = (int)m.Value.AsNumber;
            if (steps > _robotSteps)
            {
                int points = robot != null ? robot.Points - _robotPoints : 0;
                Show(points, "Step done", false);
            }
            _robotSteps = steps;
            if (robot != null)
                _robotPoints = robot.Points;
        }

        private void OnLockoutStep(SignalMessage m)
        {
            int step = (int)m.Value.AsNumber;
            if (_lockoutStep >= 0 && step > _lockoutStep)
                Show(LockoutLevel.StepPoints, "Step done", false);
            _lockoutStep = step;
        }

        private void Show(int points, string text, bool mistake)
        {
            if (popups != null)
                popups.Show(new ScoreEvent(Time.timeAsDouble, points, text, string.Empty, mistake));
            _hands.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand, _hands);
            foreach (InputDevice hand in _hands)
                hand.SendHapticImpulse(0u, mistake ? 0.8f : 0.35f, mistake ? 0.2f : 0.06f);
        }
    }
}
