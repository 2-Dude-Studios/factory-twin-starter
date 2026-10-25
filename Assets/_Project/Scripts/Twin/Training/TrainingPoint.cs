using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Training: the things in the cell the trainee can use during a lockout (M11).</summary>
    public enum TrainingPointKind
    {
        StartButton = 0,
        StopButton = 1,
        NotifyButton = 2,
        AllClearButton = 3,
        Isolator = 4,
        LockHasp = 5,
        AirDumpValve = 6,
        ToolChange = 7,
        Inspection = 8,
    }

    /// <summary>
    /// Training: what using a training point does, and the words for it. The isolator and the hasp offer the opposite
    /// of their present state (switch off when on, lock when unlocked); nothing else depends on state, and nothing
    /// depends on the procedure, so the trainee can make the same mistakes a real one could.
    /// </summary>
    public static class TrainingPoints
    {
        public static LockoutAction ActionFor(TrainingPointKind kind, bool isolated, bool locked)
        {
            switch (kind)
            {
                case TrainingPointKind.StartButton: return LockoutAction.Start;
                case TrainingPointKind.StopButton: return LockoutAction.Stop;
                case TrainingPointKind.NotifyButton: return LockoutAction.Notify;
                case TrainingPointKind.AllClearButton: return LockoutAction.ClearAndNotify;
                case TrainingPointKind.Isolator: return isolated ? LockoutAction.IsolatorOn : LockoutAction.IsolatorOff;
                case TrainingPointKind.LockHasp: return locked ? LockoutAction.RemoveLock : LockoutAction.ApplyLock;
                case TrainingPointKind.AirDumpValve: return LockoutAction.ReleaseEnergy;
                case TrainingPointKind.ToolChange: return LockoutAction.ChangeTool;
                case TrainingPointKind.Inspection: return LockoutAction.Inspect;
                default: return LockoutAction.None;
            }
        }

        /// <summary>The prompt the trainee sees while looking at the point.</summary>
        public static string Prompt(TrainingPointKind kind, bool isolated, bool locked)
        {
            switch (kind)
            {
                case TrainingPointKind.StartButton: return "Line start";
                case TrainingPointKind.StopButton: return "Line stop";
                case TrainingPointKind.NotifyButton: return "Intercom: notify the crew of the shutdown";
                case TrainingPointKind.AllClearButton: return "Intercom: all clear, notify the crew";
                case TrainingPointKind.Isolator: return isolated ? "CNC isolator: switch on" : "CNC isolator: switch off";
                case TrainingPointKind.LockHasp: return locked ? "Remove your lock and tag" : "Apply your lock and tag";
                case TrainingPointKind.AirDumpValve: return "Dump the air and check the spindle is at rest";
                case TrainingPointKind.ToolChange: return "Change the tool";
                case TrainingPointKind.Inspection: return "Inspect: tools out, guards on";
                default: return string.Empty;
            }
        }
    }

    /// <summary>
    /// Training: one thing in the cell the trainee can use (M11). Using it publishes the matching cell/training/action;
    /// the training controller decides what happens. The isolator and the hasp read cell/press/isolated and
    /// cell/press/lockout to know which way they would go. A collider on this object is what the trainee aims at.
    /// </summary>
    public sealed class TrainingPoint : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CellContext context;
        [SerializeField] private TrainingPointKind kind;
        [Tooltip("Renderers tinted with this point's colour, and lit while the trainee looks at it.")]
        [SerializeField] private Renderer[] highlight = Array.Empty<Renderer>();
        [SerializeField] private Color color = new Color(1f, 0.68f, 0.05f);

        private MaterialPropertyBlock _block;
        private bool _hovered;

        public TrainingPointKind Kind => kind;
        public LockoutAction Action => TrainingPoints.ActionFor(kind, Read(CellTopics.PressIsolated), Read(CellTopics.PressLockout));
        public string Prompt => TrainingPoints.Prompt(kind, Read(CellTopics.PressIsolated), Read(CellTopics.PressLockout));

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            Tint();
        }

        /// <summary>Uses the point: publishes its action on cell/training/action.</summary>
        public void Use()
        {
            context.Bus.Publish(CellTopics.TrainingAction, SignalValue.FromNumber((int)Action), CellContext.NowUnixMs);
        }

        /// <summary>Lit while the trainee looks at it, so they know what a click would use.</summary>
        public void SetHovered(bool hovered)
        {
            if (hovered == _hovered)
                return;
            _hovered = hovered;
            Tint();
        }

        private void Tint()
        {
            _block.Clear();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * (_hovered ? 2f : 0.25f));
            foreach (Renderer r in highlight)
            {
                if (r != null)
                    r.SetPropertyBlock(_block);
            }
        }

        private bool Read(TopicPath topic) =>
            context != null && context.Bus.TryGetLast(topic, out SignalMessage m) && m.Value.AsNumber != 0d;
    }
}
