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

        /// <summary>The CNC's written energy control procedure, read before shutting down (1910.147(d)(1)).</summary>
        ProcedureCard = 9,

        /// <summary>A colleague who can be asked to stand clear of the work area.</summary>
        Colleague = 10,
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
                case TrainingPointKind.AllClearButton: return LockoutAction.CheckArea;
                case TrainingPointKind.ProcedureCard: return LockoutAction.ReviewProcedure;
                case TrainingPointKind.Colleague: return LockoutAction.AskStandClear;
                case TrainingPointKind.Isolator: return isolated ? LockoutAction.IsolatorOn : LockoutAction.IsolatorOff;
                case TrainingPointKind.LockHasp: return locked ? LockoutAction.RemoveLock : LockoutAction.ApplyLock;
                case TrainingPointKind.AirDumpValve: return LockoutAction.ReleaseEnergy;
                case TrainingPointKind.ToolChange: return LockoutAction.ChangeTool;
                case TrainingPointKind.Inspection: return LockoutAction.Inspect;
                default: return LockoutAction.None;
            }
        }

        /// <summary>The point that carries out <paramref name="action"/>, for guiding the trainee to the step due.</summary>
        public static bool TryKindFor(LockoutAction action, out TrainingPointKind kind)
        {
            switch (action)
            {
                case LockoutAction.Start: kind = TrainingPointKind.StartButton; return true;
                case LockoutAction.Stop: kind = TrainingPointKind.StopButton; return true;
                case LockoutAction.Notify: kind = TrainingPointKind.NotifyButton; return true;
                case LockoutAction.CheckArea: kind = TrainingPointKind.AllClearButton; return true;
                case LockoutAction.ReviewProcedure: kind = TrainingPointKind.ProcedureCard; return true;
                case LockoutAction.IsolatorOff:
                case LockoutAction.IsolatorOn: kind = TrainingPointKind.Isolator; return true;
                case LockoutAction.ApplyLock:
                case LockoutAction.RemoveLock: kind = TrainingPointKind.LockHasp; return true;
                case LockoutAction.ReleaseEnergy: kind = TrainingPointKind.AirDumpValve; return true;
                case LockoutAction.ChangeTool: kind = TrainingPointKind.ToolChange; return true;
                case LockoutAction.Inspect: kind = TrainingPointKind.Inspection; return true;
                default: kind = TrainingPointKind.StartButton; return false;
            }
        }

        /// <summary>Where the trainee finds the point and what it looks like.</summary>
        public static string Where(TrainingPointKind kind)
        {
            switch (kind)
            {
                case TrainingPointKind.StartButton: return "Control station: green Start button";
                case TrainingPointKind.StopButton: return "Control station: red Stop button";
                case TrainingPointKind.NotifyButton: return "Control station: blue intercom button";
                case TrainingPointKind.AllClearButton: return "Control station: white area-clear button";
                case TrainingPointKind.ProcedureCard: return "CNC, east end: the yellow energy control procedure card";
                case TrainingPointKind.Colleague: return "The colleague in the work area";
                case TrainingPointKind.Isolator: return "CNC, east end: red handle on the yellow isolator";
                case TrainingPointKind.LockHasp: return "CNC, east end: the hasp under the isolator; your lock is on the board beside it";
                case TrainingPointKind.AirDumpValve: return "CNC, east end, low down: the air-dump valve lever";
                case TrainingPointKind.ToolChange: return "CNC, front: the spindle; the new tool is on the cart";
                case TrainingPointKind.Inspection: return "CNC, east end: inspection door above the air valve";
                default: return string.Empty;
            }
        }

        /// <summary>The prompt the trainee sees while looking at the point.</summary>
        public static string Prompt(TrainingPointKind kind, bool isolated, bool locked)
        {
            switch (kind)
            {
                case TrainingPointKind.StartButton: return "Line start";
                case TrainingPointKind.StopButton: return "Line stop";
                case TrainingPointKind.NotifyButton: return "Intercom: notify the crew";
                case TrainingPointKind.AllClearButton: return "Area check: everyone is clear of the CNC";
                case TrainingPointKind.ProcedureCard: return "Energy control procedure: electrical, pneumatic, stored";
                case TrainingPointKind.Colleague: return "Ask your colleague to stand clear";
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
        private bool _guided;

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

        /// <summary>Pulses while it is the point the step due needs (TrainingGuide).</summary>
        public void SetGuided(bool guided)
        {
            if (guided == _guided)
                return;
            _guided = guided;
            Tint();
        }

        private void Update()
        {
            if (_guided && !_hovered)
                Tint();
        }

        private void Tint()
        {
            float glow = _hovered ? 2f : _guided ? Mathf.Lerp(0.5f, 2.5f, Mathf.PingPong(Time.time * 2f, 1f)) : 0.25f;
            _block.Clear();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * glow);
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
