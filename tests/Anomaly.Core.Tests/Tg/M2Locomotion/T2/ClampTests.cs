using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.Wave4.Integration;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// A crossing whose Move tg refuses puts the capsule back into its tile (D105; scope 2a: "A FALSE result (a stale grid, a
/// PRE_MOVE veto) clamps the capsule back into its tile"), so "the capsule's tile is loc" holds at the end of every tick.
/// The clamp keeps the centre one radius (45) off the border it tried to cross: the tile's last subunit is
/// 256 x + 127, so the centre rests at 256 x + 127 - 45 on that side and at 256 x - 128 + 45 on the west and south ones.
/// </summary>
public class ClampTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);
    private const int C = T2Room.C;

    [Theory]
    [InlineData(127, 0, 6, 5, 5 * C + 127 - 45, 5 * C)]    // east: x clamps below the border, y is untouched
    [InlineData(-127, 0, 4, 5, 5 * C - 128 + 45, 5 * C)]   // west
    [InlineData(0, 127, 5, 6, 5 * C, 5 * C + 127 - 45)]    // north
    [InlineData(0, -127, 5, 4, 5 * C, 5 * C - 128 + 45)]   // south
    public void AVetoedCrossingClampsTheCentreInsideItsOwnTile(int mx, int my, int refusedX, int refusedY, int expectedX, int expectedY)
    {
        var r = new T2Room(601);
        mob_living_carbon_human m = r.Join(5, 5, "Alice", name: "mover");
        r.Go(m, 0, 0, East, 2);
        turf refused = r.In(() => locate(refusedX, refusedY, 1));
        r.In(() =>
        {
            T2Room.WatchMover(m);
            On(m, COMSIG_MOB_CLIENT_MOVED, "client_moved", 3);
            // the veto: PRE_MOVE into the refused tile only
            On(m, COMSIG_MOVABLE_PRE_MOVE, "veto", 1, result: COMPONENT_MOVABLE_BLOCK_PRE_MOVE);
        });
        for (int tick = 0; tick < 5; tick++)
        {
            r.Go(m, (sbyte)mx, (sbyte)my, East);
            Assert.Equal((5, 5, 1), r.LocOf(m));
            r.AssertOnLoc(m, "tick " + tick);
            MotionState s = r.State(m);
            if (tick >= 2) // the first ticks are still inside the tile
                Assert.Equal((expectedX, expectedY), (s.X, s.Y));
        }
        // a refused Move (move-chain 3g): the attempt, the Exit and the CanPass ran, PRE_MOVE vetoed, nothing after it; no tail
        Assert.Contains("attempted mover " + Toy.D(refused) + " " + Dm.str(mx != 0 ? (mx > 0 ? EAST : WEST) : (my > 0 ? NORTH : SOUTH)), r.Trace);
        Assert.Contains("pre_move mover " + Toy.D(refused), r.Trace);
        Assert.DoesNotContain(r.Trace, l => l.StartsWith("moved", StringComparison.Ordinal) || l.StartsWith("exited", StringComparison.Ordinal) || l.StartsWith("entering", StringComparison.Ordinal) || l.StartsWith("client_moved", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALiftedVetoLetsTheNextCrossingThrough()
    {
        var r = new T2Room(602);
        mob_living_carbon_human m = r.Join(5, 5, "Alice", name: "mover");
        r.Go(m, 0, 0, East, 2);
        var veto = r.In(() => On(m, COMSIG_MOVABLE_PRE_MOVE, "veto", 1, result: COMPONENT_MOVABLE_BLOCK_PRE_MOVE));
        r.Go(m, 127, 0, East, 4);
        Assert.Equal((5, 5, 1), r.LocOf(m));
        r.In(() => veto.result = NONE);
        r.Go(m, 127, 0, East, 2);
        Assert.Equal((6, 5, 1), r.LocOf(m));
        r.AssertOnLoc(m);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADiagonalWhoseSecondStepIsRefusedStopsOnTheFirstTilesClamp()
    {
        // exact corner NE from (5,5): the north step into (5,6) goes, the east step into (6,6) is vetoed; the mob is on (5,6)
        // and the capsule is clamped into (5,6): x under the east border of column 5, y already past the north border of row 5
        var r = new T2Room(603);
        mob_living_carbon_human m = r.Join(5, 5, "Alice", name: "mover");
        r.Go(m, 0, 0, East, 2);
        turf blocked = r.In(() => locate(6, 6, 1));
        datum_test_integration_probe veto = null;
        r.In(() => veto = On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1, act: a => veto.result = ReferenceEquals(a[1], blocked) ? COMPONENT_MOVABLE_BLOCK_PRE_MOVE : NONE));
        r.Go(m, 127, 127, East, 3); // 60 a tick on both axes: both borders (1408) fall in tick 3
        Assert.Equal((5, 6, 1), r.LocOf(m));
        MotionState s = r.State(m);
        Assert.Equal((5 * C + 127 - 45, 5 * C + 180), (s.X, s.Y));
        r.AssertOnLoc(m);
        Assert.Equal(new[] { "pre_move mover (5,6)", "pre_move mover (6,6)" }, r.Trace.Where(l => l.StartsWith("pre_move", StringComparison.Ordinal)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AStaleGridDenseEdgeObjectRefusesTheCrossingBumpsItAndClamps()
    {
        // the controller's grid does not see an ON_BORDER_1 object (its tile is not solid, edges are M3), tg's Enter does:
        // Cross is false for a dense object, so first_bump = the object and Move fails (turf.dm:441-466)
        var r = new T2Room(604);
        mob_living_carbon_human m = r.Join(5, 5, "Alice", name: "mover");
        obj edge = r.Edge(6, 5);
        Assert.False(r.In(() => LocomotionSeam.IsSolid(r.World, 6, 5, 1)));
        r.Go(m, 0, 0, East, 2);
        r.In(() => T2Room.WatchChain(m, locate(5, 5, 1), locate(6, 5, 1)));
        r.Trace.Clear();
        r.Go(m, 127, 0, East, 3);
        Assert.Equal((5, 5, 1), r.LocOf(m));
        Assert.Equal(5 * C + 127 - 45, r.State(m).X);
        r.AssertOnLoc(m);
        // the bump went to the edge object, not the turf; Enter failed so nothing after it ran
        Assert.Contains("bump mover edge", r.Trace);
        Assert.DoesNotContain(r.Trace, l => l.StartsWith("pre_move", StringComparison.Ordinal) || l.StartsWith("moved", StringComparison.Ordinal));
        // the way is free again once the object goes
        r.In(() => qdel(edge));
        r.Go(m, 127, 0, East, 2);
        Assert.Equal((6, 5, 1), r.LocOf(m));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMoveThatMovesTheMobElsewhereEndsTheStepAndTheCapsuleFollows()
    {
        // a PRE_MOVE handler that teleports the mob (a portal's Entered, a trap): the Move reports it did not arrive; the
        // capsule follows loc and is not clamped back into a tile the mob left
        var r = new T2Room(605);
        mob_living_carbon_human m = r.Join(5, 5, "Alice", name: "mover");
        r.Go(m, 0, 0, East, 2);
        bool once = true;
        r.In(() => On(m, COMSIG_ATOM_ENTERED, "never", 0)); // no-op probe to keep the trace non-empty
        r.In(() => On(locate(6, 5, 1), COMSIG_ATOM_ENTERED, "entered", 1, act: _ =>
        {
            if (once)
            {
                once = false;
                Waiting.Detach(m.forceMove(locate(12, 12, 1)));
            }
        }));
        r.Go(m, 127, 0, East, 2); // the crossing is in the second tick
        Assert.Equal((12, 12, 1), r.LocOf(m));
        MotionState s = r.State(m);
        r.AssertOnLoc(m);
        Assert.Equal((12 * C, 12 * C), (s.X, s.Y)); // snapped to the new tile's centre, not left half way
        r.Go(m, 127, 0, East, 1);
        Assert.Equal((12 * C + 85, 12 * C), (r.State(m).X, r.State(m).Y)); // and it walks on from there
        r.AssertNoRuntimes();
    }
}
