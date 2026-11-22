using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// DEVIATION(DV371): a level change that tg makes after a wait (an elevator's ride, a ladder's climb) is announced to the clients
// when the wait starts, so they draw the thing rising or sinking over it instead of jumping a level at its end (protocol version 77,
// NetField.Ride). tg's own state is not touched: this is a record beside the movable, read by the replicator.

public partial class atom_movable
{
    /// <summary>DV371: the announced ride: the levels it crosses (-1 down, +1 up, 0 none), the tick it ends at and how many ticks it takes.</summary>
    public int dm_ride_dz;

    /// <summary>DV371: the world tick the ride arrives at.</summary>
    public long dm_ride_end;

    /// <summary>DV371: the ride's length in ticks.</summary>
    public int dm_ride_ticks;
}

/// <summary>DV371: the movables riding now (the replicator polls them, since a ride starts without any tg change).</summary>
public sealed class LevelRides
{
    private readonly HashSet<long> _riding = new();

    public static LevelRides Of(World world) => world.GetExtension(static _ => new LevelRides());

    /// <summary>The ids of the movables with an announced ride (the replicator reads each once more after it ends).</summary>
    public IReadOnlyCollection<long> Riding => _riding;

    /// <summary>Announces that <paramref name="movable"/> moves <paramref name="dz"/> levels in <paramref name="deciseconds"/>.</summary>
    public static void Start(atom_movable movable, int dz, float deciseconds)
    {
        if (movable == null || dz == 0)
            return;
        World world = World.Current;
        int ticks = Math.Max(1, (int)MathF.Ceiling(GlobalProcs.DS2TICKS(deciseconds)));
        movable.dm_ride_dz = Math.Sign(dz);
        movable.dm_ride_ticks = ticks;
        movable.dm_ride_end = world.Tick + ticks;
        Of(world)._riding.Add(movable.dm_id);
    }

    /// <summary>
    /// <paramref name="movable"/> stepped onto <paramref name="carrier"/> while it rides (a rider walking from one tile of a moving car to the next: tg's module
    /// it left let it go, the one it entered takes it): it rides the rest of the carrier's ride.
    /// </summary>
    public static void Join(atom_movable movable, atom_movable carrier)
    {
        if (movable == null || carrier == null || carrier.dm_ride_dz == 0 || World.Current.Tick >= carrier.dm_ride_end)
            return;
        movable.dm_ride_dz = carrier.dm_ride_dz;
        movable.dm_ride_ticks = carrier.dm_ride_ticks;
        movable.dm_ride_end = carrier.dm_ride_end;
        Of(World.Current)._riding.Add(movable.dm_id);
    }

    /// <summary>Ends the ride of <paramref name="movable"/> (arrived or given up).</summary>
    public static void End(atom_movable movable)
    {
        if (movable == null || movable.dm_ride_dz == 0)
            return;
        movable.dm_ride_dz = 0;
        movable.dm_ride_end = 0;
        movable.dm_ride_ticks = 0;
    }

    /// <summary>The replicator read <paramref name="id"/> with no ride: it is no longer polled.</summary>
    public void Forget(long id) => _riding.Remove(id);
}
