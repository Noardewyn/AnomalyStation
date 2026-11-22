using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.P4;

/// <summary>
/// The capsule-to-Move bridge on a booted world with humans (M2 P4; D105, DV15, DV16; docs/ai/specs/locomotion.md section 4):
/// the phase, the crossings as tg's real Move with PASSMOB, presses at tg's move delay, contacts with MobBump's signals
/// only, the gate in tg's order, the displacement by tg, the facing, the speed scale, the state hash and the invariant
/// "the capsule's tile is the mob's loc at the end of every tick". The independent goldens are T2's (M2Locomotion/T2).
/// </summary>
public class BridgeTests
{
    private const int C = 256;

    [Fact]
    public void TheSeamInstallsOnceAndRegistersOnlyWhenInstalled()
    {
        var room = new CapsuleRoom(seed: 41);
        Assert.True(LocomotionSeam.IsInstalled(room.World));
        Assert.Throws<InvalidOperationException>(() => LocomotionSeam.Install(room.World));
        var bare = new World(3);
        Assert.False(LocomotionSeam.IsInstalled(bare));
        Assert.Equal(1f, LocomotionSeam.SpeedScale(bare));
        mob_living_carbon_human h = room.In(() => Dm.@new<mob_living_carbon_human>(locate(4, 4, 1)));
        using (bare.Enter())
            Assert.Throws<InvalidOperationException>(() => LocomotionSeam.Register(h));
    }

    [Fact]
    public void ARegisteredHumanStandsOnItsTileFacingItsDirWithTgsSpeed()
    {
        var room = new CapsuleRoom(seed: 42);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        Assert.True(room.In(() => LocomotionSeam.IsRegistered(a)));
        MotionState s = room.State(a);
        Assert.Equal((5 * C, 5 * C, 1), (s.X, s.Y, s.Z));
        Assert.Equal(MotionFacing.YawOf(MotionDir.South), s.Yaw); // a new human faces south
        Assert.Equal(Posture.Standing, s.Posture);
        MotionParams p = room.Params(a);
        Assert.Equal(1f / 3f, p.TilesPerTick, 6); // RUN_DELAY 1.5 ds per tile
        Assert.True(p.CanMove);
        Assert.Equal(3, p.MoveDelayTicks);
        Assert.Equal(MotionMath.DefaultRadius, p.Radius);
        Assert.False(LocomotionSeam.TryGetState(room.World, 999_999, out _, out _));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WalkingEastCrossesWithTgsMoveAndRunsClientMovesTail()
    {
        var room = new CapsuleRoom(seed: 43);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        datum_test_m2p4_listener l = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => l.Watch(a));
        var xs = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            room.Walk(a, 127, 0, 1, yaw: 16384);
            xs.Add(room.State(a).X);
            room.AssertOnLoc(a);
        }
        Assert.Equal(new[] { 1365, 1450, 1535, 1620, 1705, 1790 }, xs);
        Assert.Equal((7, 5), room.Tile(a));
        Assert.Equal(EAST, room.In(() => a.dir));
        // The first tick turned the mob east (the yaw), the second crossed into (6, 5) with tg's Move and client/Move's tail.
        int dir = l.Trace.IndexOf("dir 2 4");
        int pre = l.Trace.IndexOf("pre_move 6,5");
        int moved = l.Trace.IndexOf("moved 5,5 4 forced=0");
        int tail = l.Trace.IndexOf("client_moved 4 4 5,5");
        Assert.True(dir >= 0 && dir < pre && pre < moved && moved < tail, string.Join(" | ", l.Trace));
        Assert.Equal(2, l.Count("client_moved"));
        Assert.Equal(1, l.Count("dir")); // dir changed once: Move passes update_dir FALSE
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnExactCornerCrossingIsTwoCardinalMovesNorthSouthFirst()
    {
        var room = new CapsuleRoom(seed: 61);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        datum_test_m2p4_listener l = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => l.Watch(a));
        room.Walk(a, 127, 127, 3); // 60 per axis per tick: (1460, 1460) after 3 ticks, both borders at once
        Assert.Equal((6, 6), room.Tile(a));
        string[] moves = l.Trace.Where(t => t.StartsWith("pre_move", StringComparison.Ordinal)).ToArray();
        Assert.Equal(new[] { "pre_move 5,6", "pre_move 6,6" }, moves); // tg's diagonal Move order: north/south first
        Assert.Equal(2, l.Count("client_moved"));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void PressingIntoAWallBumpsItEveryMoveDelay()
    {
        var room = new CapsuleRoom(seed: 44);
        mob_living_carbon_human a = room.Join(15, 8, "Alice"); // x 16 is the rim wall
        datum_test_m2p4_listener l = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => l.Watch(a));
        var bumpTicks = new List<long>();
        for (int i = 0; i < 12; i++)
        {
            int before = l.Count("bump turf 16,8");
            room.Walk(a, 127, 0, 1, yaw: 16384);
            if (l.Count("bump turf 16,8") > before) bumpTicks.Add(room.World.Tick);
            Assert.True(room.State(a).X <= 15 * C + 128 - 45);
        }
        Assert.Equal(15 * C + 128 - 45, room.State(a).X);
        Assert.True(bumpTicks.Count >= 3, string.Join(",", bumpTicks));
        for (int i = 1; i < bumpTicks.Count; i++)
            Assert.Equal(3, bumpTicks[i] - bumpTicks[i - 1]); // RUN_DELAY 1.5 ds = 3 ticks
        Assert.Equal((15, 8), room.Tile(a));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ACrossingIntoATileWithAStandingMobPassesWithPassmobAndKeepsTheFlagOff()
    {
        var room = new CapsuleRoom(seed: 45);
        mob_living_carbon_human b = room.Dummy(7, 5);
        mob_living_carbon_human a = room.Join(6, 5, "Alice");
        datum_test_m2p4_listener l = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => l.Watch(a));
        room.Walk(a, 0, 127, 1); // to y = 1365: 85 above b's centre row
        room.Walk(a, 127, 0, 2, yaw: 16384); // 1536 -> 1621 -> 1706: into (7, 5), b's turf
        Assert.Equal((7, 5), room.Tile(a));
        Assert.Equal((7, 5), room.Tile(b));
        Assert.Equal(0, l.Count("bump"));
        Assert.Equal(0f, room.In(() => (float)Dm.bitand(a.pass_flags, PASSMOB)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AContactRunsMobBumpsSignalsOncePerMoveDelayAndNeverSwapsOrPushes()
    {
        var room = new CapsuleRoom(seed: 46);
        mob_living_carbon_human b = room.Dummy(6, 5);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        datum_test_m2p4_listener la = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        datum_test_m2p4_listener lb = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => { la.Watch(a); lb.Watch(b); });
        var bumpTicks = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            int before = la.Count("mob_bump");
            room.Walk(a, 127, 0, 1, yaw: 16384);
            if (la.Count("mob_bump") > before) bumpTicks.Add(room.World.Tick);
            MotionState sa = room.State(a);
            MotionState sb = room.State(b);
            long dx = sa.X - sb.X, dy = sa.Y - sb.Y;
            Assert.True(dx * dx + dy * dy >= 90L * 90L, "the capsules overlap");
            room.AssertOnLoc(a);
        }
        Assert.Equal((6, 5), room.Tile(b)); // never swapped, never pushed (DV16)
        Assert.Equal((6, 5), room.Tile(a)); // a reached b's turf (PASSMOB) and stands against b
        Assert.True(bumpTicks.Count >= 3, string.Join(",", bumpTicks));
        for (int i = 1; i < bumpTicks.Count; i++)
            Assert.Equal(3, bumpTicks[i] - bumpTicks[i - 1]);
        Assert.Equal(la.Count("mob_bump"), lb.Count("mob_bumped"));
        Assert.Contains("mob_bump #" + b.dm_id, la.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WalkIntentSendsNoBumpSignals()
    {
        var room = new CapsuleRoom(seed: 47);
        mob_living_carbon_human b = room.Dummy(6, 5);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        room.In(() => a.move_intent = MOVE_INTENT_WALK);
        datum_test_m2p4_listener la = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => la.Watch(a));
        room.Walk(a, 127, 0, 6, yaw: 16384);
        Assert.Equal(0, la.Count("mob_bump"));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ALyingMobIsNoObstacle()
    {
        var room = new CapsuleRoom(seed: 48);
        mob_living_carbon_human b = room.Dummy(6, 5);
        room.In(() => b.body_position = LYING_DOWN);
        room.Step(); // the follow phase reads the posture
        Assert.Equal(Posture.Lying, room.State(b).Posture);
        Assert.Equal(MotionDriver.Prone, room.State(b).Driver);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        datum_test_m2p4_listener la = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => la.Watch(a));
        room.Walk(a, 127, 0, 6, yaw: 16384);
        Assert.True(room.State(a).X > room.State(b).X + 100, "a walked over b");
        Assert.Equal(0, la.Count("mob_bump"));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ARefusedCrossingClampsTheCapsuleBackIntoItsTile()
    {
        var room = new CapsuleRoom(seed: 49);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        datum_test_m2p4_listener l = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => { l.Watch(a); l.BlockMoves = true; });
        room.Walk(a, 127, 0, 4, yaw: 16384);
        Assert.Equal((5, 5), room.Tile(a));
        Assert.Equal(5 * C + 128 - 1 - 45, room.State(a).X);
        room.AssertOnLoc(a);
        Assert.True(l.Count("pre_move 6,5") >= 1);
        l.BlockMoves = false;
        room.Walk(a, 127, 0, 1, yaw: 16384);
        Assert.Equal((6, 5), room.Tile(a));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ATeleportSnapsAndAOneTileMoveByTgSlidesOverTheGlideTime()
    {
        var room = new CapsuleRoom(seed: 50);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        room.Command(() => Waiting.NoSleep(a.forceMove(locate(10, 10, 1))));
        room.Step();
        Assert.Equal((10 * C, 10 * C), (room.State(a).X, room.State(a).Y));
        Assert.False(room.State(a).Slide.Active);

        room.Command(() => Waiting.NoSleep(a.Move(locate(11, 10, 1), EAST)));
        room.Step();
        // tg's glide_size 8: 4 ticks per tile; the first share was played in the same tick's phase.
        MotionState s = room.State(a);
        Assert.Equal(new Slide(11 * C, 10 * C, 3), s.Slide);
        Assert.Equal(10 * C + 64, s.X);
        room.AssertOnLoc(a);
        for (int i = 0; i < 3; i++)
        {
            room.Step();
            room.AssertOnLoc(a);
        }
        Assert.Equal(11 * C, room.State(a).X);
        Assert.False(room.State(a).Slide.Active);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheGateStopsADeadMobWithoutGhostingItYet()
    {
        var room = new CapsuleRoom(seed: 51);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        room.In(() => a.stat = DEAD);
        int deferred = DmWorldState.Of(room.World).DeferredCount;
        room.Walk(a, 127, 0, 3, yaw: 16384);
        Assert.Equal(5 * C, room.State(a).X);
        Assert.True(DmWorldState.Of(room.World).DeferredCount > deferred); // client/Move's ghostize, M10
        Assert.False(room.Params(a).CanMove);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheGateStopsAMobWithoutMobilityOrWithNoTransform()
    {
        var room = new CapsuleRoom(seed: 52);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        room.In(() => a.mobility_flags = Dm.bitand(a.mobility_flags, Dm.bitnot(MOBILITY_MOVE)));
        room.Walk(a, 127, 0, 3, yaw: 16384);
        Assert.Equal(5 * C, room.State(a).X);
        Assert.False(room.Params(a).CanMove);
        room.In(() => a.mobility_flags = MOBILITY_FLAGS_DEFAULT);
        room.Walk(a, 127, 0, 1, yaw: 16384);
        Assert.Equal(5 * C + 85, room.State(a).X);
        room.In(() => ADD_TRAIT(a, TRAIT_NO_TRANSFORM, "test"));
        room.Walk(a, 127, 0, 2, yaw: 16384);
        Assert.Equal(5 * C + 85, room.State(a).X);
        room.AssertNoRuntimes();
    }

    // The owner's play test (2026-10-09): a weightless mob's view jerked forward and back, because the private block said it
    // could move while the gate's Process_Spacemove refused every step. The preview now answers as the gate does.
    [Fact]
    public void AWeightlessMobIsToldItCannotMove()
    {
        var room = new CapsuleRoom(seed: 53);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        // over space: a floor holds the feet even without gravity (DV178)
        room.In(() => ((turf)Waiting.NoSleep(((turf)a.loc).ChangeTurf(typeof(turf_open_space)))).force_no_gravity = TRUE);
        room.Step();
        Assert.False(room.Params(a).CanMove);
        room.In(() => ADD_TRAIT(a, TRAIT_SPACEWALK, "test"));
        room.Step();
        Assert.True(room.Params(a).CanMove);
        room.In(() => ((turf)a.loc).force_no_gravity = FALSE);
        room.AssertNoRuntimes();
    }

    // The owner (2026-11-18, retiring DV178): a floor without gravity is no handhold, as in tg (get_spacemove_backup: dense things only),
    // so in mid-room a mob cannot walk.
    [Fact]
    public void AFloorWithoutGravityIsNoHandhold()
    {
        var room = new CapsuleRoom(seed: 56);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        room.In(() => ((turf)a.loc).force_no_gravity = TRUE);
        room.Step();
        Assert.IsAssignableFrom<turf_open_indestructible>(a.loc); // a walking surface, not space
        Assert.False(room.Params(a).CanMove);
        room.In(() => ((turf)a.loc).force_no_gravity = FALSE);
        room.AssertNoRuntimes();
    }

    // The owner's play test (2026-10-09): the dev noclip is tg's INCORPOREAL_MOVE_BASIC; the capsule passes the rim wall by
    // forceMove crossings and stops at the map's edge (DV29); fly is tg's FLYING, which moves without gravity.
    [Fact]
    public void NoclipPassesTheRimWallAndStopsAtTheMapsEdge()
    {
        var room = new CapsuleRoom(seed: 54);
        mob_living_carbon_human a = room.Join(15, 8, "Alice"); // x 16 is the rim wall, the map ends there
        room.In(() => DevCommands.NoClip(a));
        room.Step();
        Assert.True(room.Params(a).NoClip);
        room.Walk(a, 127, 0, 12, yaw: 16384);
        // in the rim wall; the map ends east of it, where client/Move's get_step gives no turf and the gate stops (:53)
        Assert.Equal((16, 8), room.Tile(a));
        Assert.True(room.State(a).X <= 16 * C + 128 - 45);
        room.Walk(a, -127, 0, 12, yaw: 49152);
        Assert.True(room.Tile(a).X < 15);
        room.In(() => DevCommands.NoClip(a));
        room.Step();
        Assert.False(room.Params(a).NoClip);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void FlyMovesWithoutGravityAndGodTogglesAllThree()
    {
        var room = new CapsuleRoom(seed: 55);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        room.In(() =>
        {
            // over space: a floor holds the feet even without gravity (DV178)
            for (int x = 4; x <= 9; x++)
                ((turf)Waiting.NoSleep(((turf)locate(x, 5, 1)).ChangeTurf(typeof(turf_open_space)))).force_no_gravity = TRUE;
        });
        room.Step();
        Assert.False(room.Params(a).CanMove);
        room.In(() => DevCommands.Fly(a));
        room.Step();
        Assert.True(room.Params(a).CanMove);
        room.Walk(a, 127, 0, 6, yaw: 16384);
        Assert.True(room.Tile(a).X > 5);
        Assert.Contains("god on", room.In(() => DevCommands.God(a)));
        room.In(() =>
        {
            Assert.True(HAS_TRAIT_FROM(a, TRAIT_MOVE_FLYING, ADMIN_TRAIT));
            Assert.True(HAS_TRAIT_FROM(a, TRAIT_GODMODE, ADMIN_TRAIT));
            Assert.True(Dm.eq(a.incorporeal_move, INCORPOREAL_MOVE_BASIC));
        });
        Assert.Contains("god off", room.In(() => DevCommands.God(a)));
        room.In(() =>
        {
            Assert.False(HAS_TRAIT(a, TRAIT_MOVE_FLYING));
            Assert.False(HAS_TRAIT(a, TRAIT_GODMODE));
            Assert.False(Dm.truthy(a.incorporeal_move));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AClientPreMoveSignalCanStopTheCapsule()
    {
        var room = new CapsuleRoom(seed: 53);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        datum_test_m2p4_listener l = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => { l.Watch(a); l.BlockClientMoves = true; });
        room.Walk(a, 127, 0, 3, yaw: 16384);
        Assert.Equal(5 * C, room.State(a).X);
        Assert.Equal(3, l.Count("client_pre_move")); // the gate runs each tick the wish is not zero
        room.Walk(a, 0, 0, 3, yaw: 16384);
        Assert.Equal(3, l.Count("client_pre_move")); // and not while it is zero
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheFacingFollowsTheYawWithHysteresisAndADummyKeepsItsDir()
    {
        var room = new CapsuleRoom(seed: 54);
        mob_living_carbon_human dummy = room.Dummy(9, 9);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        datum_test_m2p4_listener l = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => l.Watch(a));
        room.Walk(a, 0, 0, 1, yaw: 16384);
        Assert.Equal(EAST, room.In(() => a.dir));
        room.Walk(a, 0, 0, 3, yaw: 16384 + 8192 + 500); // within 45 degrees plus 5 of east
        Assert.Equal(EAST, room.In(() => a.dir));
        room.Walk(a, 0, 0, 1, yaw: 32768);
        Assert.Equal(SOUTH, room.In(() => a.dir));
        Assert.Equal(2, l.Count("dir")); // setDir only on a change
        Assert.Equal(SOUTH, room.In(() => dummy.dir));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheSpeedScaleSlowsEveryCapsule()
    {
        var room = new CapsuleRoom(seed: 55);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        LocomotionSeam.SetSpeedScale(room.World, 0.5f);
        Assert.Equal(0.5f, LocomotionSeam.SpeedScale(room.World));
        Assert.Equal(1f / 6f, room.Params(a).TilesPerTick, 6);
        room.Walk(a, 127, 0, 1, yaw: 16384);
        Assert.Equal(5 * C + 43, room.State(a).X);
        Assert.Throws<ArgumentOutOfRangeException>(() => LocomotionSeam.SetSpeedScale(room.World, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocomotionSeam.SetSpeedScale(room.World, 4.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocomotionSeam.SetSpeedScale(room.World, float.NaN));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void SolidityIsADenseTurfOrADenseNonMobMovable()
    {
        var room = new CapsuleRoom(seed: 56);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        World w = room.World;
        Assert.True(LocomotionSeam.IsSolid(w, 1, 1, 1));   // the rim wall
        Assert.False(LocomotionSeam.IsSolid(w, 5, 5, 1));  // a floor with a human on it
        Assert.True(LocomotionSeam.IsSolid(w, 0, 5, 1));   // outside the map
        Assert.True(LocomotionSeam.IsSolid(w, 5, 5, 2));
        obj crate = room.In(() => Dm.@new<obj>(locate(8, 8, 1)));
        Assert.False(LocomotionSeam.IsSolid(w, 8, 8, 1));
        room.In(() => crate.density = 1);
        Assert.True(LocomotionSeam.IsSolid(w, 8, 8, 1));
        room.In(() => crate.flags_1 = Dm.num(Dm.bitor(crate.flags_1, ON_BORDER_1)));
        Assert.False(LocomotionSeam.IsSolid(w, 8, 8, 1)); // an edge object blocks a face, not the tile (M3)
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheServerViewListsTheStandingCapsulesInIdOrder()
    {
        var room = new CapsuleRoom(seed: 57);
        mob_living_carbon_human b = room.Dummy(6, 5);
        mob_living_carbon_human c = room.Dummy(7, 5);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        ICollisionView view = LocomotionSeam.CollisionView(room.World);
        var list = new List<CapsuleObstacle>();
        view.GatherCapsules(5 * C, 5 * C, 1, 3 * C, a.dm_id, list);
        Assert.Equal(new[] { b.dm_id, c.dm_id }, list.Select(o => o.Id).ToArray());
        room.In(() => c.body_position = LYING_DOWN);
        room.Step();
        list.Clear();
        view.GatherCapsules(5 * C, 5 * C, 1, 3 * C, a.dm_id, list);
        Assert.Equal(new[] { b.dm_id }, list.Select(o => o.Id).ToArray());
        Assert.Equal(TileEdges.None, view.EdgesOf(5, 5, 1));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADeletedOrUnregisteredMobLeavesTheSeam()
    {
        var room = new CapsuleRoom(seed: 58);
        mob_living_carbon_human b = room.Dummy(6, 5);
        mob_living_carbon_human c = room.Dummy(7, 5);
        room.Command(() => qdel(b));
        room.Step();
        Assert.False(LocomotionSeam.TryGetState(room.World, b.dm_id, out _, out _));
        room.Command(() => LocomotionSeam.Unregister(c));
        room.Step();
        Assert.False(room.In(() => LocomotionSeam.IsRegistered(c)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheCapsuleIsOnItsLocAtTheEndOfEveryTickOfARandomWalk()
    {
        var room = new CapsuleRoom(seed: 59);
        mob_living_carbon_human b = room.Dummy(8, 8);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        mob_living_carbon_human d = room.Join(10, 6, "Dana");
        var rng = new Random(11);
        for (int i = 0; i < 300; i++)
        {
            room.Frame(a, (sbyte)rng.Next(-128, 128), (sbyte)rng.Next(-128, 128), (ushort)rng.Next(65536));
            room.Frame(d, (sbyte)rng.Next(-128, 128), (sbyte)rng.Next(-128, 128), (ushort)rng.Next(65536));
            if (i % 50 == 25)
                room.Command(() => Waiting.NoSleep(b.Move(get_step(b, NORTH), NORTH))); // tg moves the dummy now and then
            room.Step();
            room.AssertOnLoc(a);
            room.AssertOnLoc(b);
            room.AssertOnLoc(d);
        }
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheSeamsStateIsHashedAndReplaysToTheSameHash()
    {
        ulong Run(sbyte moveX)
        {
            var room = new CapsuleRoom(seed: 60);
            mob_living_carbon_human a = room.Join(5, 5, "Alice");
            room.Walk(a, moveX, 30, 10, yaw: 16384);
            room.AssertNoRuntimes();
            return room.World.ComputeStateHash();
        }
        ulong first = Run(127);
        Assert.Equal(first, Run(127));
        Assert.NotEqual(first, Run(100));
    }

    [Fact]
    public void AnUnconsciousBodyKeepsItsYawWhateverItsPlayerLooksAt()
    {
        // the owner (2026-10-26): the others see a still body while its player looks around
        var room = new CapsuleRoom(seed: 97);
        var a = room.Join(5, 5, "Alice");
        room.Walk(a, 0, 0, 2, yaw: 1000);
        Assert.Equal(1000, room.State(a).Yaw);
        room.In(() => Waiting.NoSleep(a.Unconscious(100 * SECONDS)));
        room.Walk(a, 0, 0, 3, yaw: 30000);
        Assert.Equal(1000, room.State(a).Yaw);
        room.AssertNoRuntimes();
    }
}
