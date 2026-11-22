using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;

namespace Anomaly.Core.Tg;

// DEVIATION(DV403): a tram's trip is announced to the clients when it starts (the tram's own tile steps stay tg's), so they draw the
// tram and what rides it moving evenly and the own client's predictor carries a walker on it as the server's bridge does (protocol
// version 80, NetField.Tram). tg's state is not touched but for the first step's delay (LeadDeciseconds): a record beside the tram.

public partial class atom_movable
{
    /// <summary>DV403: the announced trip of a tram's platform (<see cref="TramRide.None"/> for any other movable or a tram at rest).</summary>
    public TramRide dm_tram_ride;
}

/// <summary>DV403: the trams riding now and their announced schedule.</summary>
public sealed class TramRides
{
    /// <summary>
    /// The time between a trip's start and its first step (tg's is the movement delay, half a decisecond): the announcement reaches a
    /// client and its predictor before the tram moves, whose frames run a round trip and the input buffer ahead of the server.
    /// </summary>
    public const float LeadDeciseconds = 10;

    private readonly HashSet<long> _riding = new();

    public static TramRides Of(World world) => world.GetExtension(static _ => new TramRides());

    /// <summary>The ids of the platforms with an announced trip (the replicator reads each once more after it ends).</summary>
    public IReadOnlyCollection<long> Riding => _riding;

    /// <summary>The replicator read <paramref name="id"/> with no trip: it is no longer polled.</summary>
    public void Forget(long id) => _riding.Remove(id);

    /// <summary>The trip of <paramref name="tram"/> starts (dispatch_transport, after its scheduled_move): its schedule is announced.</summary>
    public static void Start(datum_transport_controller_linear_tram tram)
    {
        World world = World.Current;
        long first = TickAt(tram.scheduled_move);
        foreach (obj_structure_transport_linear_tram module in Dm.each<obj_structure_transport_linear_tram>(tram.transport_modules))
            Announce(world, module, tram, first, (int)tram.travel_remaining);
    }

    /// <summary><paramref name="tram"/> moved a tile: a step that is not where the schedule put it, or a new speed, is announced again.</summary>
    public static void Stepped(datum_transport_controller_linear_tram tram)
    {
        World world = World.Current;
        foreach (obj_structure_transport_linear_tram module in Dm.each<obj_structure_transport_linear_tram>(tram.transport_modules))
        {
            TramRide ride = module.dm_tram_ride;
            int remaining = (int)tram.travel_remaining;
            if (ride.IsNone)
            {
                if (remaining > 0)
                    Announce(world, module, tram, TickAt(world.Time + Dm.num(tram.internal_movement_delay)), remaining);
                continue;
            }
            int done = ride.Steps - remaining;
            (int dx, int dy) = ride.Unit;
            bool onSchedule = done >= 1 && ride.StepsBy(world.Tick) == done && ride.StepsBy(world.Tick - 1) == done - 1
                && (int)module.x == ride.X0 + dx * done && (int)module.y == ride.Y0 + dy * done
                && ride.TicksPerStep == TicksPerStep(tram);
            // the last step leaves the trip announced until End: the clients' frames up to it still read it
            if (!onSchedule && remaining > 0)
                Announce(world, module, tram, TickAt(world.Time + Dm.num(tram.internal_movement_delay)), remaining);
        }
    }

    /// <summary>
    /// The trip of <paramref name="tram"/> ended (arrived, stopped): it stays announced as the steps it took, until the next one, so the clients
    /// still draw its riders in its frame while their render time catches up with the arrival (a stop on the way cuts the steps it did not take).
    /// </summary>
    public static void End(datum_transport_controller_linear_tram tram)
    {
        if (tram?.transport_modules == null)
            return;
        long now = World.Current.Tick;
        foreach (obj_structure_transport_linear_tram module in Dm.each<obj_structure_transport_linear_tram>(tram.transport_modules))
        {
            int done = module.dm_tram_ride.StepsBy(now);
            module.dm_tram_ride = done <= 0 ? TramRide.None : module.dm_tram_ride with { Steps = (ushort)done };
        }
    }

    /// <summary>The tram is gone: no trip of it is drawn on.</summary>
    public static void Gone(datum_transport_controller_linear_tram tram)
    {
        if (tram?.transport_modules == null)
            return;
        foreach (obj_structure_transport_linear_tram module in Dm.each<obj_structure_transport_linear_tram>(tram.transport_modules))
            module.dm_tram_ride = TramRide.None;
    }

    private static void Announce(World world, obj_structure_transport_linear_tram module, datum_transport_controller_linear_tram tram, long first, int steps)
    {
        if (steps <= 0 || module.loc is not turf)
        {
            module.dm_tram_ride = TramRide.None;
            return;
        }
        module.dm_tram_ride = new TramRide(Math.Max(first, world.Tick + 1), TicksPerStep(tram), (ushort)Math.Min(steps, ushort.MaxValue),
            (byte)Dm.num(tram.travel_direction), (short)module.x, (short)module.y,
            (byte)Math.Clamp((int)MathF.Ceiling(module.bound_width / 32f), 1, 255), (byte)Math.Clamp((int)MathF.Ceiling(module.bound_height / 32f), 1, 255));
        Of(world)._riding.Add(module.dm_id);
    }

    // The first tick whose world.time reaches `time` (process steps once world.time >= scheduled_move; SStransport fires every tick).
    private static long TickAt(float time) => (long)Math.Ceiling(time / World.TickLag - 1e-3);

    // Ticks between steps: process moves again once world.time reaches the last step's time plus the movement delay.
    private static ushort TicksPerStep(datum_transport_controller_linear_tram tram)
        => (ushort)Math.Clamp((int)Math.Ceiling(Dm.num(tram.internal_movement_delay) / World.TickLag - 1e-3), 1, ushort.MaxValue);
}

public static partial class LocomotionSeam
{
    /// <summary>
    /// DV403: tg's group_move took <paramref name="mob"/> a tile along with a tram (<c>mover.loc = get_step</c>): its capsule moves by exactly that
    /// tile, keeping where on the tile it stands and its speed (the follow would slide it to the tile's centre and stop it), as the own client's
    /// predictor carries it (<see cref="TramRide.CarriesAt"/>).
    /// </summary>
    public static void CarryByTram(atom_movable mob, int dx, int dy)
    {
        World world = World.CurrentOrNull;
        if (world == null || mob is not mob_living living || !Seam.Of(world).Capsules.TryGetValue(living.dm_id, out Capsule c) || !ReferenceEquals(c.Mob, living))
            return;
        MotionStep.Carry(ref c.State, dx, dy);
    }
}
