using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// The movement gate (DV15): client/Move's checks in tg's order (mob_movement.dm:53-97) minus the move_delay test and the
/// move itself. Each tick the wish is not zero it is asked with the turf the wish points at and its BYOND direction; the first
/// check that says no ends it, and no later signal is sent. Signals: COMSIG_MOB_CLIENT_PRE_LIVING_MOVE (:66, args new_loc and
/// direct) and COMSIG_MOB_CLIENT_PRE_MOVE (:97, one argument: the Move's argument list).
/// </summary>
public class GateTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);

    private static (T2Room Room, mob_living_carbon_human Mover) Setup(ulong seed)
    {
        var r = new T2Room(seed);
        mob_living_carbon_human m = r.Join(8, 8, "Alice", name: "mover");
        r.Go(m, 0, 0, East, 2);
        r.In(() =>
        {
            On(m, COMSIG_MOB_CLIENT_PRE_LIVING_MOVE, "pre_living", 2);
            On(m, COMSIG_MOB_CLIENT_PRE_MOVE, "pre_move_gate", 1);
        });
        return (r, m);
    }

    [Fact]
    public void TheChecksCometInTgsOrderAndAFailedOneSendsNoLaterSignal()
    {
        // no_transform (:57) comes before PRE_LIVING_MOVE (:66), which comes before MOBILITY_MOVE (:87) and PRE_MOVE (:97)
        var (r, m) = Setup(901);
        r.In(() => ADD_TRAIT(m, TRAIT_NO_TRANSFORM, "t2"));
        r.Go(m, 127, 0, East, 2);
        Assert.Empty(r.Trace);
        r.In(() => REMOVE_TRAIT(m, TRAIT_NO_TRANSFORM, "t2"));

        r.In(() => m.stat = DEAD);                    // :63 comes before :66; it ghostizes the dead player (:64, the ghosts, D151)
        client player = m.client;
        r.Go(m, 127, 0, East, 2);
        Assert.Empty(r.Trace);
        var ghost = player.mob as mob_dead_observer;
        Assert.NotNull(ghost);
        r.In(() =>
        {
            m.stat = 0 /* CONSCIOUS (stat.dm) */;
            World.Current.usr = ghost;
            ghost.reenter_corpse(); // the player back in the body for the checks below
        });
        Assert.Same(m, player.mob);
        r.Go(m, 0, 0, East, 2); // the client's move_delay of the ghost's step (DV54: a tile every 1 ds) runs out before the body's checks

        r.In(() => m.mobility_flags = Dm.bitand(m.mobility_flags, Dm.bitnot(MOBILITY_MOVE)));
        r.Go(m, 127, 0, East, 2);
        Assert.Equal(2, r.Trace.Count);                // PRE_LIVING_MOVE ran (:66, once per tick), PRE_MOVE (:97) did not
        Assert.All(r.Trace, l => Assert.StartsWith("pre_living", l));
        r.In(() => m.mobility_flags = MOBILITY_FLAGS_DEFAULT);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APreLivingMoveBlockStopsTheMobBeforeTheMobilityAndPreMoveChecks()
    {
        var r = new T2Room(902);
        mob_living_carbon_human m = r.Join(8, 8, "Alice", name: "mover");
        r.Go(m, 0, 0, East, 2);
        r.In(() =>
        {
            On(m, COMSIG_MOB_CLIENT_PRE_LIVING_MOVE, "pre_living", 2, result: COMSIG_MOB_CLIENT_BLOCK_PRE_LIVING_MOVE);
            On(m, COMSIG_MOB_CLIENT_PRE_MOVE, "pre_move_gate", 1);
        });
        int x0 = r.State(m).X;
        r.Go(m, 127, 0, East, 4);
        Assert.Equal(4, r.Trace.Count(l => l.StartsWith("pre_living", StringComparison.Ordinal)));
        Assert.DoesNotContain(r.Trace, l => l.StartsWith("pre_move_gate", StringComparison.Ordinal));
        Assert.Equal(x0, r.State(m).X);
        // (the private block's CanMove is the side-effect-free preview of the checks, which sends no signal and so cannot see a
        // signal handler's veto: locomotion.md section 4.4. The capsule did not move, which is what the gate decides.)
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGateIsAskedForTheTurfTheWishPointsAtAndItsByondDirection()
    {
        // new_loc = get_step(mob, direct); a component counts when it is more than 22.5 degrees off the other axis
        var (r, m) = Setup(903);
        var expected = new (sbyte X, sbyte Y, float Dir, int Tx, int Ty)[]
        {
            (127, 0, EAST, 9, 8), (-127, 0, WEST, 7, 8), (0, 127, NORTH, 8, 9), (0, -127, SOUTH, 8, 7),
            (127, 127, NORTHEAST, 9, 9), (-127, 127, NORTHWEST, 7, 9), (127, -127, SOUTHEAST, 9, 7), (-127, -127, SOUTHWEST, 7, 7),
            (127, 52, EAST, 9, 8),        // 22.3 degrees off east: east alone
            (127, 54, NORTHEAST, 9, 9),   // 23.0 degrees: both
            (52, 127, NORTH, 8, 9),
            (54, 127, NORTHEAST, 9, 9),
        };
        foreach ((sbyte x, sbyte y, float dir, int tx, int ty) in expected)
        {
            r.Trace.Clear();
            // one tick, from the centre of the tile: stay inside it by waiting at the tile after each probe
            r.Go(m, x, y, East, 1);
            string pre = r.Trace.First(l => l.StartsWith("pre_living", StringComparison.Ordinal));
            Assert.Equal($"pre_living mover ({tx},{ty}) {Dm.str(dir)}", pre);
            r.Command(() => Waiting.NoSleep(m.forceMove(locate(8, 8, 1))));
            r.Go(m, 0, 0, East, 1);
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PreMoveGetsTheMovesOwnArgumentList()
    {
        var r = new T2Room(904);
        mob_living_carbon_human m = r.Join(8, 8, "Alice", name: "mover");
        r.Go(m, 0, 0, East, 2);
        DmList args = null;
        r.In(() => On(m, COMSIG_MOB_CLIENT_PRE_MOVE, "pre_move_gate", 0, act: a => args = a[1] as DmList));
        r.Go(m, 127, 0, East, 1);
        Assert.NotNull(args);
        Assert.Equal(2, (int)args.len);
        Assert.Same(r.In(() => locate(9, 8, 1)), args[1f]);
        Assert.Equal(EAST, Dm.num(args[2f]));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WithNoWishNothingIsAskedAndThePreviewSendsNoSignal()
    {
        var (r, m) = Setup(905);
        r.Go(m, 0, 0, East, 10);
        Assert.Empty(r.Trace);
        Assert.True(r.Params(m).CanMove);
        r.In(() => m.mobility_flags = Dm.bitand(m.mobility_flags, Dm.bitnot(MOBILITY_MOVE)));
        r.Step();
        Assert.False(r.Params(m).CanMove);
        Assert.Empty(r.Trace); // the preview reads the same checks without the signals
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMobWithNoPlayerHasNoGateAndStandsStill()
    {
        // a dummy has no client: SetFrame is meaningless for it, and the preview says it cannot move
        var r = new T2Room(906);
        mob_living_carbon_human dummy = r.Dummy(8, 8, name: "dummy");
        Assert.False(r.Params(dummy).CanMove);
        r.Frame(dummy, 127, 0, East);
        r.Step(5);
        Assert.Equal((8 * T2Room.C, 8 * T2Room.C), (r.State(dummy).X, r.State(dummy).Y));
        r.AssertNoRuntimes();
    }
}
