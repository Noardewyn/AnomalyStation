using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Protocol version 80 (DV403): the trams' announced trips as the client reads them. The own predictor steps a frame at the tick the server
// will step it (the private block's tick plus the frames after its acknowledged one: the server takes one frame a tick), so it carries
// a walker on a tram on the ticks the tram steps and sees the tram's walls where they will be then; the renderer draws the tram and
// what rides it at the own prediction's time, moving evenly.
public sealed partial class ReplicaWorld
{
    private readonly List<(int Z, TramRide Ride)> _trams = new();
    private long _tramsScanned = long.MinValue;

    /// <summary>The trams' announced trips now (version 80), each with its platform's level.</summary>
    public IReadOnlyList<(int Z, TramRide Ride)> TramTrips
    {
        get
        {
            if (_tramsScanned != Tick)
            {
                _tramsScanned = Tick;
                _trams.Clear();
                foreach (ReplicaEntity e in Entities)
                    if (!e.State.Tram.IsNone)
                        _trams.Add((e.State.Z, e.State.Tram));
            }
            return _trams;
        }
    }

    /// <summary>
    /// The server tick that steps the own frame <paramref name="seq"/>: the tick of the newest private block plus the frames after the one it
    /// acknowledged (the server's jitter buffer takes one frame a tick); the replica's tick before any private block.
    /// </summary>
    public long TickOfFrame(uint seq) => _own.Tick < 0 ? Tick : _own.Tick + ((long)seq - _own.AckedSeq);

    /// <summary>
    /// DV403: where the replica holds what stands on tile (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) at tick
    /// <paramref name="tick"/>: a tile a tram covers then is the tile of the car that will be there (the replica holds the car at its own tick).
    /// </summary>
    public (int X, int Y) TramMap(int x, int y, int z, long tick)
    {
        IReadOnlyList<(int Z, TramRide Ride)> trips = TramTrips;
        for (int i = 0; i < trips.Count; i++)
        {
            (int tz, TramRide ride) = trips[i];
            if (tz != z)
                continue;
            int then = ride.StepsBy(tick), now = ride.StepsBy(Tick);
            if (then == now || !ride.Covers(x, y, then))
                continue;
            (int ux, int uy) = ride.Unit;
            return (x - (then - now) * ux, y - (then - now) * uy);
        }
        return (x, y);
    }

    /// <summary>DV403: the whole tiles a tram moves a capsule standing on (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) in tick <paramref name="tick"/>.</summary>
    public (int Dx, int Dy) TramCarry(int x, int y, int z, long tick)
    {
        IReadOnlyList<(int Z, TramRide Ride)> trips = TramTrips;
        for (int i = 0; i < trips.Count; i++)
            if (trips[i].Z == z && trips[i].Ride.CarriesAt(tick, x, y, out int dx, out int dy))
                return (dx, dy);
        return (0, 0);
    }

    /// <summary>
    /// DV403: the tiles a thing on tile (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) as of tick <paramref name="at"/> is drawn
    /// moved by at the time <paramref name="drawTick"/>: its tram's even progress since then (0 off any tram).
    /// </summary>
    public (float Dx, float Dy) TramDrawShift(int x, int y, int z, long at, double drawTick)
    {
        IReadOnlyList<(int Z, TramRide Ride)> trips = TramTrips;
        for (int i = 0; i < trips.Count; i++)
        {
            (int tz, TramRide ride) = trips[i];
            int steps = ride.StepsBy(at);
            if (tz != z || !ride.Covers(x, y, steps))
                continue;
            float moved = (float)(ride.StepsAt(drawTick) - steps);
            (int ux, int uy) = ride.Unit;
            return (moved * ux, moved * uy);
        }
        return (0f, 0f);
    }

    /// <summary>DV403: the whole tiles a tram carries a thing standing on (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) at tick <paramref name="from"/> by tick <paramref name="to"/>.</summary>
    public (int Dx, int Dy) TramMoved(int x, int y, int z, long from, long to)
    {
        IReadOnlyList<(int Z, TramRide Ride)> trips = TramTrips;
        for (int i = 0; i < trips.Count; i++)
        {
            (int tz, TramRide ride) = trips[i];
            int steps = ride.StepsBy(from);
            if (tz != z || !ride.Covers(x, y, steps))
                continue;
            int moved = ride.StepsBy(to) - steps;
            (int ux, int uy) = ride.Unit;
            return (moved * ux, moved * uy);
        }
        return (0, 0);
    }

    /// <summary>DV403: the trip of the tram covering tile (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) at tick <paramref name="at"/>, if one does.</summary>
    public bool TryTramAt(int x, int y, int z, long at, out TramRide ride)
    {
        IReadOnlyList<(int Z, TramRide Ride)> trips = TramTrips;
        for (int i = 0; i < trips.Count; i++)
        {
            if (trips[i].Z == z && trips[i].Ride.Covers(x, y, trips[i].Ride.StepsBy(at)))
            {
                ride = trips[i].Ride;
                return true;
            }
        }
        ride = TramRide.None;
        return false;
    }
}
