using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Production cell: owns the cell's signal bus, force table and config so components get them by reference, never by search.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class CellContext : MonoBehaviour
    {
        [SerializeField] private TwinConfig config;

        private SignalBus _bus;
        private SignalForces _forces;

        public TwinConfig Config => config;

        // Created on first use so Awake order between objects can never hand out a null bus.
        public SignalBus Bus => _bus ??= new SignalBus();

        /// <summary>Inputs forced for commissioning; TwinSignals publishes the forced value in place of the plant's.</summary>
        public SignalForces Forces => _forces ??= new SignalForces();

        public static long NowUnixMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
