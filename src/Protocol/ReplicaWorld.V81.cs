using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Protocol version 81 (DV416): an entity's free motion (NetField.Flight). A new segment starts within the tick that sent it (a hit, a
// bounce, a landing), while the client draws a little in the past: the segments it replaced are kept, so the one under way at the drawn
// time is drawn.

public sealed partial class ReplicaEntity
{
    private const int FlightHistory = 6;
    private readonly FlightArc[] _flights = new FlightArc[FlightHistory]; // the segments before the state's, newest first
    private int _flightCount;

    private void KeepFlight(FlightArc old)
    {
        for (int i = Math.Min(_flightCount, FlightHistory - 1); i > 0; i--)
            _flights[i] = _flights[i - 1];
        _flights[0] = old;
        _flightCount = Math.Min(_flightCount + 1, FlightHistory);
    }

    private void ForgetFlights() => _flightCount = 0;

    /// <summary>
    /// The free motion to draw at <paramref name="renderTick"/>: the newest segment that started by then (the state's or one it replaced);
    /// none when the newest that started is none. Before the oldest kept one started it is held at its start (a thing thrown out of a hand
    /// is in the world only from its throw on).
    /// </summary>
    public FlightArc FlightAt(double renderTick)
    {
        FlightArc arc = _state.Flight;
        if (arc.IsNone || arc.StartTick <= renderTick)
            return arc;
        for (int i = 0; i < _flightCount; i++)
        {
            FlightArc older = _flights[i];
            if (older.IsNone || older.StartTick <= renderTick)
                return older;
            arc = older;
        }
        return arc;
    }
}
