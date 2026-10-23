namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Safety zone: what the arm's guarding is doing. Published as a number on cell/safety/state.</summary>
    public enum SafetyState
    {
        /// <summary>Both zones clear: the arm runs at full speed.</summary>
        Normal = 0,

        /// <summary>A person is in the warning zone: the arm runs at reduced speed.</summary>
        Reduced = 1,

        /// <summary>A person is in the protective zone: the arm is stopped and the line holds.</summary>
        Stopped = 2,

        /// <summary>The protective zone is clear again, but the stop stays until someone presses reset.</summary>
        AwaitingReset = 3,
    }

    /// <summary>Safety zone: what one <see cref="SafetyZone.Update"/> call did with a reset request.</summary>
    public enum SafetyResetResult
    {
        None = 0,
        Accepted = 1,

        /// <summary>Reset pressed while a person is still in the protective zone; the stop stays.</summary>
        RefusedZoneOccupied = 2,
    }

    /// <summary>
    /// Safety zone: the guarding logic for the arm, as a simulated safety function (not safety-rated). Pure logic
    /// with no Unity dependency. The cell is a guarded, non-collaborative application (ISO 10218-2:2025): a person
    /// in the warning zone slows the arm, a person in the protective zone latches a protective stop, and leaving
    /// the zone does not restart anything. Only a deliberate reset with the protective zone clear releases it.
    /// </summary>
    public sealed class SafetyZone
    {
        private readonly double _reducedSpeedScale;
        private bool _lastResetRequest;

        /// <param name="reducedSpeedScale">Arm speed in the warning zone, as a fraction of full speed (0–1).</param>
        public SafetyZone(double reducedSpeedScale = 0.25d)
        {
            if (!(reducedSpeedScale > 0d && reducedSpeedScale <= 1d))
                throw new System.ArgumentOutOfRangeException(nameof(reducedSpeedScale), "Must be in (0, 1].");
            _reducedSpeedScale = reducedSpeedScale;
        }

        /// <summary>The latched protective stop: true from entering the protective zone until an accepted reset.</summary>
        public bool ProtectiveStop { get; private set; }

        public SafetyState State { get; private set; }

        /// <summary>The arm's speed as a fraction of full speed: 0 while stopped, reduced in the warning zone.</summary>
        public double SpeedScale
        {
            get
            {
                switch (State)
                {
                    case SafetyState.Stopped:
                    case SafetyState.AwaitingReset:
                        return 0d;
                    case SafetyState.Reduced:
                        return _reducedSpeedScale;
                    default:
                        return 1d;
                }
            }
        }

        /// <summary>
        /// Applies the latest zone sensors. A reset counts on its rising edge only, so holding the button down does
        /// not release a stop that is set again later.
        /// </summary>
        public SafetyResetResult Update(bool warningZoneOccupied, bool protectiveZoneOccupied, bool resetRequest)
        {
            bool resetPressed = resetRequest && !_lastResetRequest;
            _lastResetRequest = resetRequest;

            var result = SafetyResetResult.None;
            if (protectiveZoneOccupied)
            {
                ProtectiveStop = true;
                if (resetPressed)
                    result = SafetyResetResult.RefusedZoneOccupied;
            }
            else if (ProtectiveStop && resetPressed)
            {
                ProtectiveStop = false;
                result = SafetyResetResult.Accepted;
            }

            if (protectiveZoneOccupied)
                State = SafetyState.Stopped;
            else if (ProtectiveStop)
                State = SafetyState.AwaitingReset;
            else if (warningZoneOccupied)
                State = SafetyState.Reduced;
            else
                State = SafetyState.Normal;

            return result;
        }
    }
}
