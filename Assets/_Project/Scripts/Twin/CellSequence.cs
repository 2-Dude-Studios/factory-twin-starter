using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Production cell: what the line is doing. Values are published as numbers on cell/line/state.</summary>
    public enum CellState
    {
        Feeding = 0,
        Transferring = 1,
        Stalled = 2,
        Held = 3,

        /// <summary>Protective stop from the safety zone (M10): everything stops at once, mid-transfer included.</summary>
        SafetyStop = 4,

        /// <summary>Stopped by the operator (M11): everything off until a start is pressed and allowed.</summary>
        Stopped = 5,
    }

    /// <summary>Production cell: sensor and actuator feedback the sequence reacts to.</summary>
    public readonly struct CellInputs
    {
        public bool PartAtGate { get; }
        public bool TransferBusy { get; }
        public bool PartAtExit { get; }

        /// <summary>An upstream station reports a fault; the line must not move parts while this is set.</summary>
        public bool Hold { get; }

        /// <summary>The safety zone's latched protective stop (cell/safety/protective_stop).</summary>
        public bool SafetyStop { get; }

        /// <summary>The operator pressed Start on this tick (M11).</summary>
        public bool StartPressed { get; }

        /// <summary>The operator pressed Stop on this tick (M11).</summary>
        public bool StopPressed { get; }

        /// <summary>A start must be refused: the CNC is locked out or isolated (M11).</summary>
        public bool StartBlocked { get; }

        public CellInputs(bool partAtGate, bool transferBusy, bool partAtExit, bool hold = false, bool safetyStop = false,
            bool startPressed = false, bool stopPressed = false, bool startBlocked = false)
        {
            PartAtGate = partAtGate;
            TransferBusy = transferBusy;
            PartAtExit = partAtExit;
            Hold = hold;
            SafetyStop = safetyStop;
            StartPressed = startPressed;
            StopPressed = stopPressed;
            StartBlocked = startBlocked;
        }
    }

    /// <summary>Production cell: commands the sequence issues to the drives and the pick station.</summary>
    public readonly struct CellOutputs
    {
        public bool InfeedRun { get; }
        public bool OutfeedRun { get; }
        public bool PickCommand { get; }

        public CellOutputs(bool infeedRun, bool outfeedRun, bool pickCommand)
        {
            InfeedRun = infeedRun;
            OutfeedRun = outfeedRun;
            PickCommand = pickCommand;
        }
    }

    /// <summary>
    /// Production cell: the virtual PLC for infeed → gate → pick → outfeed. Pure logic with no Unity dependency,
    /// ticked with the fixed timestep. The infeed runs until a part reaches the gate, then stops while the pick
    /// station moves the part; a pick command that is never acknowledged is re-pulsed, and a transfer that never
    /// finishes stalls the line until the gate is cleared. A hold stops both conveyors for as long as it is set;
    /// a pick already under way is finished first so the arm never freezes with a part in its gripper.
    /// A protective stop is different: the safety zone has already frozen the arm, so the sequence stops at once in
    /// whatever state it is in, with every output off and its timers paused, and resumes that state when the stop is
    /// reset. Time in a protective stop counts toward neither the transfer timeout nor the cycle time.
    /// Stop and Start are the operator's (M11). Stop is orderly: a pick under way finishes, then the line is Stopped
    /// with everything off. Start in Stopped runs the line again, unless the start is blocked (the CNC is locked out
    /// or isolated): then it is refused and counted, and the line stays stopped.
    /// </summary>
    public sealed class CellSequence
    {
        private readonly double _ackTimeoutSeconds;
        private readonly double _transferTimeoutSeconds;

        private double _stateSeconds;
        private double _sinceCommandSeconds;
        private bool _ackSeen;
        private bool _repulse;
        private bool _lastExit;
        private double _lastExitAt = double.NaN;
        // Seconds the line was not held for a press fault; cycle time is measured on this clock.
        private double _runningSeconds;
        // The state a protective stop interrupted, and how long it had been in it.
        private CellState _suspendedState;
        private double _suspendedStateSeconds;
        // Stop pressed and not yet honoured: a pick under way finishes first.
        private bool _stopRequested;

        public CellSequence(double ackTimeoutSeconds = 2d, double transferTimeoutSeconds = 15d)
        {
            if (!(ackTimeoutSeconds > 0d))
                throw new ArgumentOutOfRangeException(nameof(ackTimeoutSeconds));
            if (!(transferTimeoutSeconds > ackTimeoutSeconds))
                throw new ArgumentOutOfRangeException(nameof(transferTimeoutSeconds), "Must be longer than the ack timeout.");

            _ackTimeoutSeconds = ackTimeoutSeconds;
            _transferTimeoutSeconds = transferTimeoutSeconds;
            Outputs = new CellOutputs(true, true, false);
        }

        public CellState State { get; private set; } = CellState.Feeding;
        public CellOutputs Outputs { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public int PartsOut { get; private set; }
        public int PickRetries { get; private set; }
        public int Stalls { get; private set; }

        /// <summary>Starts pressed while the start was blocked (the CNC locked out or isolated).</summary>
        public int StartsRefused { get; private set; }

        /// <summary>
        /// Running seconds between the last two parts leaving the cell, leaving out any time the line was held for a
        /// press fault, so a stop does not show up as one very long cycle. NaN until two parts have left.
        /// </summary>
        public double LastCycleSeconds { get; private set; } = double.NaN;

        public void Tick(CellInputs inputs, double deltaSeconds)
        {
            if (deltaSeconds < 0d || double.IsNaN(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            ElapsedSeconds += deltaSeconds;
            // Latched before the protective stop, so a stop pressed during one is honoured after the reset.
            if (inputs.StopPressed)
                _stopRequested = true;

            if (State == CellState.SafetyStop || inputs.SafetyStop)
            {
                if (inputs.SafetyStop)
                {
                    if (State != CellState.SafetyStop)
                    {
                        _suspendedState = State;
                        _suspendedStateSeconds = _stateSeconds;
                        Enter(CellState.SafetyStop);
                    }
                    CountExits(inputs.PartAtExit);
                    Outputs = new CellOutputs(false, false, false);
                    return;
                }

                // Reset: pick up where the stop left off.
                State = _suspendedState;
                _stateSeconds = _suspendedStateSeconds;
            }

            if (inputs.StartPressed && State == CellState.Stopped)
            {
                if (inputs.StartBlocked)
                {
                    StartsRefused++;
                }
                else
                {
                    _stopRequested = false;
                    Enter(CellState.Feeding);
                }
            }

            if (State != CellState.Held && State != CellState.Stopped)
                _runningSeconds += deltaSeconds;
            _stateSeconds += deltaSeconds;
            CountExits(inputs.PartAtExit);

            switch (State)
            {
                case CellState.Feeding:
                    if (_stopRequested)
                        Enter(CellState.Stopped);
                    else if (inputs.Hold)
                        Enter(CellState.Held);
                    else if (inputs.PartAtGate)
                        BeginTransfer();
                    break;

                case CellState.Transferring:
                    TickTransfer(inputs, deltaSeconds);
                    if (State == CellState.Feeding && _stopRequested)
                        Enter(CellState.Stopped);
                    else if (State == CellState.Feeding && inputs.Hold)
                        Enter(CellState.Held);
                    break;

                case CellState.Stalled:
                    if (_stopRequested)
                        Enter(CellState.Stopped);
                    else if (!inputs.PartAtGate && !inputs.TransferBusy)
                        Enter(inputs.Hold ? CellState.Held : CellState.Feeding);
                    break;

                case CellState.Held:
                    if (_stopRequested)
                        Enter(CellState.Stopped);
                    else if (!inputs.Hold)
                        Enter(CellState.Feeding);
                    break;
            }

            Outputs = new CellOutputs(
                infeedRun: State == CellState.Feeding,
                outfeedRun: State != CellState.Held && State != CellState.Stopped,
                pickCommand: State == CellState.Transferring && !_ackSeen && !_repulse);
            _repulse = false;
        }

        private void TickTransfer(CellInputs inputs, double deltaSeconds)
        {
            if (_stateSeconds > _transferTimeoutSeconds)
            {
                Stalls++;
                Enter(CellState.Stalled);
                return;
            }

            if (!_ackSeen)
            {
                if (inputs.TransferBusy)
                {
                    _ackSeen = true;
                    return;
                }

                _sinceCommandSeconds += deltaSeconds;
                if (_sinceCommandSeconds > _ackTimeoutSeconds)
                {
                    // Drop the command for one tick so the pick station sees a fresh rising edge.
                    _repulse = true;
                    _sinceCommandSeconds = 0d;
                    PickRetries++;
                }
                return;
            }

            if (inputs.TransferBusy)
                return;

            // The pick station reports done. If the part is still in the gate the pick missed, so try again.
            if (inputs.PartAtGate)
            {
                PickRetries++;
                BeginTransfer();
            }
            else
            {
                Enter(CellState.Feeding);
            }
        }

        private void BeginTransfer()
        {
            Enter(CellState.Transferring);
            _ackSeen = false;
            _sinceCommandSeconds = 0d;
        }

        private void Enter(CellState state)
        {
            State = state;
            _stateSeconds = 0d;
        }

        private void CountExits(bool partAtExit)
        {
            if (partAtExit && !_lastExit)
            {
                PartsOut++;
                if (!double.IsNaN(_lastExitAt))
                    LastCycleSeconds = _runningSeconds - _lastExitAt;
                _lastExitAt = _runningSeconds;
            }
            _lastExit = partAtExit;
        }
    }
}
