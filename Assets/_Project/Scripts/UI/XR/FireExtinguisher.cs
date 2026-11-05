using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): the extinguisher in use. Follows whichever <see cref="ExtinguisherUnit"/> on the stand is in
    /// a hand, works out how well its stream meets the base of the fire at cell/fire/location and how well the
    /// distance suits its agent (<see cref="ExtinguisherRules"/>), plays the spray at its nozzle, and publishes
    /// cell/fire/agent, discharge, charge, aim and stand-off; the FireStation does the rest. It reads the fire's
    /// position from its anchor, never its flames.
    /// </summary>
    public sealed class FireExtinguisher : MonoBehaviour
    {
        [Serializable]
        private sealed class FireAt
        {
            [SerializeField] private FireLocation location;
            [SerializeField] private Transform baseOfFire;

            public FireLocation Location => location;
            public Transform BaseOfFire => baseOfFire;
        }

        [Serializable]
        private sealed class SprayColour
        {
            [SerializeField] private FireAgent agent;
            [SerializeField] private Color colour = Color.white;

            public FireAgent Agent => agent;
            public Color Colour => colour;
        }

        [SerializeField] private CellContext context;
        [SerializeField] private ExtinguisherUnit[] units = Array.Empty<ExtinguisherUnit>();
        [SerializeField] private FireAt[] fires = Array.Empty<FireAt>();
        [Tooltip("Played at the nozzle while discharging; tinted per agent.")]
        [SerializeField] private ParticleSystem spray;
        [SerializeField] private SprayColour[] sprayColours = Array.Empty<SprayColour>();
        [SerializeField, Range(0f, 0.2f)] private float publishStep = 0.04f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private FireAgent agent;
        [SerializeField] private float charge;
        [SerializeField] private bool discharging;
        [SerializeField] private float aim;
        [SerializeField] private float standoff;
        [SerializeField] private float distance;

        private SignalBus _bus;
        private ExtinguisherUnit _active;
        private bool _publishedDischarge;
        private FireAgent _publishedAgent = (FireAgent)(-1);
        private float _publishedCharge = -1f, _publishedAim = -1f, _publishedStandoff = -1f;

        public FireAgent Agent => agent;
        public float Charge => charge;
        public bool Discharging => discharging;
        public float Aim => aim;
        public float Standoff => standoff;
        public float Distance => distance;
        public bool PinPulled => _active != null && _active.PinPulled;
        public bool HoseHeld => _active != null && _active.HoseHeld;

        private void OnEnable()
        {
            if (context == null)
            {
                Debug.LogError("[FireExtinguisher] Assign Context (Cell).", this);
                enabled = false;
                return;
            }
            _bus = context.Bus;
            PublishAll(force: true);
        }

        private void OnDisable()
        {
            SetSpray(false);
            _active = null;
            agent = FireAgent.None;
            discharging = false;
            PublishAll(force: true);
        }

        /// <summary>Every extinguisher back on the stand, full and pinned.</summary>
        public void PutAllBack()
        {
            foreach (ExtinguisherUnit unit in units)
            {
                if (unit != null)
                    unit.PutBack();
            }
        }

        /// <summary>The base of the fire at a location (its effects anchor), or null.</summary>
        public Transform BaseOf(FireLocation at)
        {
            foreach (FireAt fire in fires)
            {
                if (fire != null && fire.Location == at)
                    return fire.BaseOfFire;
            }
            return null;
        }

        private void Update()
        {
            _active = null;
            foreach (ExtinguisherUnit unit in units)
            {
                if (unit != null && unit.Held)
                {
                    _active = unit;
                    break;
                }
            }
            agent = _active != null ? _active.Agent : FireAgent.None;
            charge = _active != null ? _active.Charge : 0f;
            bool on = _active != null && _active.Squeezing;
            if (on != discharging)
                SetSpray(on);
            discharging = on;
            Measure();
            PublishAll(force: false);
        }

        private void LateUpdate()
        {
            // The spray lives outside the hand so its particles stay in the world; it follows the nozzle.
            if (discharging && spray != null && _active != null)
                spray.transform.SetPositionAndRotation(_active.NozzleTip.position, _active.NozzleTip.rotation);
        }

        // Aim and stand-off against the base of the fire now burning, from the nozzle along its pointing direction.
        private void Measure()
        {
            Transform target = _active != null && _bus.TryGetLast(CellTopics.FireLocation, out SignalMessage m)
                ? BaseOf((FireLocation)(int)m.Value.AsNumber) : null;
            if (target == null)
            {
                aim = standoff = distance = 0f;
                return;
            }
            Transform from = _active.NozzleTip;
            Vector3 o = from.position, d = from.forward, t = target.position;
            distance = Vector3.Distance(o, t);
            aim = (float)ExtinguisherRules.Aim(ExtinguisherRules.MissDistance(o.x, o.y, o.z, d.x, d.y, d.z, t.x, t.y, t.z));
            standoff = (float)ExtinguisherRules.Standoff(agent, distance);
        }

        private void SetSpray(bool on)
        {
            if (spray == null)
                return;
            if (on && _active != null)
            {
                spray.transform.SetPositionAndRotation(_active.NozzleTip.position, _active.NozzleTip.rotation);
                ParticleSystem.MainModule main = spray.main;
                main.startColor = ColourFor(_active.Agent);
                spray.Play(true);
            }
            else
            {
                spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private Color ColourFor(FireAgent kind)
        {
            foreach (SprayColour c in sprayColours)
            {
                if (c != null && c.Agent == kind)
                    return c.Colour;
            }
            return Color.white;
        }

        private void PublishAll(bool force)
        {
            if (_bus == null)
                return;
            long now = CellContext.NowUnixMs;
            if (force || agent != _publishedAgent)
            {
                _publishedAgent = agent;
                _bus.Publish(CellTopics.FireAgent, SignalValue.FromNumber((int)agent), now);
            }
            if (force || discharging != _publishedDischarge)
            {
                _publishedDischarge = discharging;
                _bus.Publish(CellTopics.FireDischarge, SignalValue.FromBool(discharging), now);
            }
            if (force || Changed(charge, _publishedCharge))
            {
                _publishedCharge = charge;
                _bus.Publish(CellTopics.FireCharge, SignalValue.FromNumber(Math.Round(charge, 3)), now);
            }
            if (force || Changed(aim, _publishedAim))
            {
                _publishedAim = aim;
                _bus.Publish(CellTopics.FireAim, SignalValue.FromNumber(Math.Round(aim, 3)), now);
            }
            if (force || Changed(standoff, _publishedStandoff))
            {
                _publishedStandoff = standoff;
                _bus.Publish(CellTopics.FireStandoff, SignalValue.FromNumber(Math.Round(standoff, 3)), now);
            }
        }

        // A step's worth of change, or reaching an end (0 or 1) exactly.
        private bool Changed(float value, float published) =>
            Mathf.Abs(value - published) >= publishStep || ((value <= 0f || value >= 1f) && value != published);
    }
}
