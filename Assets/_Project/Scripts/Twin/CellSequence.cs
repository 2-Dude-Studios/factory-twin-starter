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
    }

    /// <summary>Production cell: sensor and actuator feedback the sequence reacts to.</summary>
    public readonly struct CellInputs
    {
        public bool PartAtGate { get; }
        public bool TransferBusy { get; }
        public bool PartAtExit { get; }

        /// <summary>An upstream station reports a fault; the line must not move parts while this is set.</summary>
        public bool Hold { get; }

        public CellInputs(bool partAtGate, bool transferBusy, bool partAtExit, bool hold = false)
        {
            PartAtGate = partAtGate;
            TransferBusy = transferBusy;
            PartAtExit = partAtExit;
            Hold = hold;
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

        /// <summary>Seconds between the last two parts leaving the cell; NaN until two parts have left.</summary>
        public double LastCycleSeconds { get; private set; } = double.NaN;

        public void Tick(CellInputs inputs, double deltaSeconds)
        {
            if (deltaSeconds < 0d || double.IsNaN(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            ElapsedSeconds += deltaSeconds;
            _stateSeconds += deltaSeconds;
            CountExits(inputs.PartAtExit);

            switch (State)
            {
                case CellState.Feeding:
                    if (inputs.Hold)
                        Enter(CellState.Held);
                    else if (inputs.PartAtGate)
                        BeginTransfer();
                    break;

                case CellState.Transferring:
                    TickTransfer(inputs, deltaSeconds);
                    if (State == CellState.Feeding && inputs.Hold)
                        Enter(CellState.Held);
                    break;

                case CellState.Stalled:
                    if (!inputs.PartAtGate && !inputs.TransferBusy)
                        Enter(inputs.Hold ? CellState.Held : CellState.Feeding);
                    break;

                case CellState.Held:
                    if (!inputs.Hold)
                        Enter(CellState.Feeding);
                    break;
            }

            Outputs = new CellOutputs(
                infeedRun: State == CellState.Feeding,
                outfeedRun: State != CellState.Held,
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
                    LastCycleSeconds = ElapsedSeconds - _lastExitAt;
                _lastExitAt = ElapsedSeconds;
            }
            _lastExit = partAtExit;
        }
    }
}
