using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4b;

/// <summary>
/// M4b slice 1 (D134): tg's zero-gravity drift (drift_handler.dm, a smooth move loop on SSnewtonian_movement): a human pushed
/// into space keeps drifting at tg's inertia speed until it reaches gravity again; the capsule
/// follows every drift step.
/// </summary>
public class SpaceDriftTests
{
    private const int Second = 20;

    [Fact]
    public void APushSurelyDriftsInOpenSpaceOnly()
    {
        // DV177, protocol 51: the drift a push from rest would start, for the client's prediction; none with stabilizers or beside a handhold
        var r = new T2Room(4307);
        r.In(() =>
        {
            for (int x = 2; x <= 14; x++)
                for (int y = 2; y <= 14; y++)
                {
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_space)));
                    ((turf_open)locate(x, y, 1)).air = SSair.parse_gas_string(AIRLESS_ATMOS) as datum_gas_mixture;
                }
        });
        var bob = r.Join(8, 8, "bob");
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_GODMODE, "test");
            if (bob is mob_living_carbon_human human && human.back != null)
                Waiting.NoSleep(human.dropItemToGround(human.back));
            var pack = Dm.@new<obj_item_tank_jetpack_carbondioxide>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_or_del(pack, ITEM_SLOT_BACK))));
            Assert.True(Dm.truthy(Waiting.NoSleep(pack.turn_on(bob))));
            Assert.True(LocomotionSeam.PushDrifts(bob));

            var jetpack = Assert.IsType<datum_component_jetpack>(bob._listen_lookup[COMSIG_MOVABLE_SPACEMOVE]);
            jetpack.stabilize = TRUE;
            Assert.False(LocomotionSeam.PushDrifts(bob));
            jetpack.stabilize = FALSE;

            // beside a wall a lit jetpack still drifts (tg's TRAIT_NOGRAV_ALWAYS_DRIFT, jetpack.dm:59)
            Waiting.NoSleep(locate(9, 8, 1).ChangeTurf(typeof(turf_closed_wall)));
            Assert.True(LocomotionSeam.PushDrifts(bob));
            pack.turn_off(bob);
            // DV534: without it the wall holds the push only while in the hand's reach, which the client tells (SteersByHandhold)
            Assert.True(LocomotionSeam.PushDrifts(bob));
            Waiting.NoSleep(locate(9, 8, 1).ChangeTurf(typeof(turf_open_space)));
            Assert.True(Dm.truthy(Waiting.NoSleep(pack.turn_on(bob))));
            Assert.True(LocomotionSeam.PushDrifts(bob));

            Assert.True(Dm.truthy(Waiting.NoSleep(bob.newtonian_move(90))));
            Assert.True(LocomotionSeam.DriftOf(bob).X > 0);
        });
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(115, 54)]
    [InlineData(115, -54)]
    [InlineData(-115, 54)]
    [InlineData(-115, -54)]
    [InlineData(54, 115)]
    [InlineData(54, -115)]
    [InlineData(-54, 115)]
    [InlineData(115, 11)]
    public void LeavingAFloorKeepsTheActualWalkingAngleAfterReleasingTheKeys(int moveX, int moveY)
    {
        var r = new T2Room(4306);
        r.In(() =>
        {
            // Isolate walking inertia from decompression's separate, cardinal push.
            for (int x = 2; x < T2Room.Size; x++)
                for (int y = 2; y < T2Room.Size; y++)
                    ((turf_open)locate(x, y, 1)).air = SSair.parse_gas_string(AIRLESS_ATMOS) as datum_gas_mixture;
            for (int x = 2; x <= 14; x++)
                for (int y = 2; y <= 14; y++)
                    if (moveX > 0 ? x > 6 : x < 10)
                        Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_space)));
        });
        var bob = r.Join(moveX > 0 ? 6 : 10, 8, "bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        var trace = new List<string>();
        for (int tick = 0; tick < Second && r.In(() => bob.drift_handler) == null; tick++)
        {
            var old = r.State(bob);
            r.Go(bob, (sbyte)moveX, (sbyte)moveY, 0);
            var now = r.State(bob);
            trace.Add($"{tick}: ({now.X - old.X},{now.Y - old.Y}) at ({now.X},{now.Y}), slide {now.Slide.Active}");
        }
        var drift = Assert.IsType<datum_drift_handler>(r.In(() => bob.drift_handler));
        double expectedAngle = (Math.Atan2(moveX, moveY) * 180 / Math.PI + 360) % 360;
        Assert.True(Math.Abs(Dm.num(drift.drifting_loop.angle) - expectedAngle) <= 1,
            $"angle {drift.drifting_loop.angle}, expected {expectedAngle}: {string.Join("; ", trace)}");
        MotionState before = r.State(bob);
        // Looking another way and releasing all keys must not steer inertia.
        r.Go(bob, 0, 0, 32768, Second);
        MotionState after = r.State(bob);
        int dx = after.X - before.X, dy = after.Y - before.Y;
        Assert.True(Math.Sign(dx) == Math.Sign(moveX) && Math.Sign(dy) == Math.Sign(moveY), $"drift ({dx}, {dy})");
        Assert.InRange((double)dy / dx, (double)moveY / moveX - 0.04, (double)moveY / moveX + 0.04);
        Assert.False(after.Slide.Active);
        r.AssertOnLoc(bob);
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void APlayerStopsAfterRepeatedLandingsOnGravityOrLattice(bool lattice)
    {
        var r = new T2Room(4305);
        r.In(() =>
        {
            for (int x = 3; x <= 13; x++)
                for (int y = 3; y <= 13; y++)
                    if (lattice || x != 9)
                        Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_space)));
            if (lattice)
                Dm.@new<obj_structure_lattice>(locate(9, 8, 1));
        });
        var bob = r.Join(4, 8, "bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        for (int landing = 0; landing < 3; landing++)
        {
            r.In(() => Waiting.NoSleep(bob.forceMove(locate(4, 8, 1))));
            r.Step(2);
            r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.newtonian_move(dir2angle(EAST))))));
            r.Step(6 * Second);
            Assert.InRange(r.LocOf(bob).X, 8, 9);
            Assert.Null(r.In(() => bob.drift_handler));
            MotionState stopped = r.State(bob);
            r.Step(Second);
            Assert.Equal((stopped.X, stopped.Y), (r.State(bob).X, r.State(bob).Y));
            Assert.Equal((short)0, r.Params(bob).DriftX);
            Assert.Equal((short)0, r.Params(bob).DriftY);
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHumanPushedIntoSpaceDriftsAcrossItAndStopsAtTheFarSide()
    {
        var r = new T2Room(4301);
        // a lane of space across the room: (3..13, 8); the floors at x = 2 and x = 14 keep their gravity
        r.In(() =>
        {
            for (int x = 3; x <= 13; x++)
                Waiting.NoSleep(locate(x, 8, 1).ChangeTurf(typeof(turf_open_space)));
        });
        mob_living_carbon_human bob = r.Dummy(4, 8, name: "bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        r.Step(Second);
        Assert.False(r.In(() => Dm.truthy(bob.has_gravity())));

        r.In(() => Waiting.NoSleep(bob.newtonian_move(dir2angle(EAST))));
        Assert.NotNull(r.In(() => bob.drift_handler));
        var xs = new List<int>();
        for (int i = 0; i < 10 * Second; i++)
        {
            r.Step();
            int x = r.LocOf(bob).X;
            if (xs.Count == 0 || xs[^1] != x)
                xs.Add(x);
            r.AssertOnLoc(bob, $"tick {i}");
        }
        Assert.True(xs.Count >= 6, "bob drifted east tile by tile: " + string.Join(",", xs));
        Assert.Equal(xs.OrderBy(x => x).ToList(), xs); // always east
        // a plain floor is no push-off (get_spacemove_backup skips floors without include_floors), so bob drifts onto the floor
        // at 14, where its gravity ends the drift (handle_move: Process_Spacemove is true)
        Assert.Equal(14, xs[^1]);
        Assert.Null(r.In(() => bob.drift_handler));
        r.AssertNoRuntimes();
    }

    // DV412 (the owner, 2026-11-13): the wind that sucks a player out of a breach sends it on into space; tg's walls beside the breach held it there
    [Fact]
    public void AHumanTheWindBlewIntoSpaceDriftsPastTheHullsHandholds()
    {
        var r = new T2Room(4308);
        // a lane of space between two walls: every tile of it has a handhold beside it
        r.In(() =>
        {
            for (int x = 3; x <= 13; x++)
            {
                Waiting.NoSleep(locate(x, 8, 1).ChangeTurf(typeof(turf_open_space)));
                Waiting.NoSleep(locate(x, 7, 1).ChangeTurf(typeof(turf_closed_wall)));
                Waiting.NoSleep(locate(x, 9, 1).ChangeTurf(typeof(turf_closed_wall)));
            }
        });
        mob_living_carbon_human bob = r.Dummy(4, 8, name: "bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        r.Step(Second);
        // tg: the walls are handholds, so a push starts no drift
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(bob.newtonian_move(dir2angle(EAST))))));
        Assert.Null(r.In(() => bob.drift_handler));
        r.In(() =>
        {
            bob.dm_blown_at = Dm.num(world.time); // the wind's push
            Waiting.NoSleep(bob.newtonian_move(dir2angle(EAST)));
        });
        Assert.True(r.In(() => bob.drift_handler)?.dm_blown);
        r.Step(10 * Second);
        Assert.Equal(14, r.LocOf(bob).X); // across the lane to the floor's gravity
        Assert.Null(r.In(() => bob.drift_handler));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADriftWaitsItsFirstDelayBeforeItsFirstStep()
    {
        // tg's loop starts at NEXT_VISUAL_TICK + delay (movement_types.dm:71), NEXT_VISUAL_TICK being the world.time of the last
        // world/Tick (world.dm:274) plus a tick, so the drift handler's "Fire the engines" (drift_handler.dm:58) never moves a
        // thing before newtonian_move returns (the singularity's thrown items were eaten inside finalize, throwing.dm:229)
        var r = new T2Room(4303);
        r.In(() =>
        {
            for (int x = 3; x <= 13; x++)
                Waiting.NoSleep(locate(x, 8, 1).ChangeTurf(typeof(turf_open_space)));
        });
        mob_living_carbon_human bob = r.Dummy(4, 8, name: "bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(world.time, GLOB.last_maptick_time);
            Waiting.NoSleep(bob.newtonian_move(dir2angle(EAST)));
            datum_move_loop loop = bob.drift_handler.drifting_loop;
            // not fired yet: fire_moveloop would have moved its timer to world.time + delay (newtonian_movement.dm:49)
            Assert.Equal(world.time + world.tick_lag + Dm.num(loop.delay), Dm.num(loop.timer));
            Assert.Equal(4f, bob.x);
        });
        r.Step(Second);
        Assert.True(r.LocOf(bob).X > 4, "bob drifts east after the delay");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAtmosSensitiveThingSteppingIntoSpaceMovesOneTileAndItsTurfExposeFollowsIt()
    {
        // Moved's newtonian_move (atoms_movable.dm:899) starts a drift that waits its delay, so atmos_sensitive's react_to_move
        // (atmos_sensitive.dm:31) moves the canister's turf_expose to the one tile it stepped onto, never registering it twice
        var r = new T2Room(4304);
        r.In(() =>
        {
            for (int x = 3; x <= 13; x++)
                Waiting.NoSleep(locate(x, 8, 1).ChangeTurf(typeof(turf_open_space)));
        });
        var canister = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister>(locate(4, 8, 1)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(canister.Move(locate(5, 8, 1), EAST))));
        });
        r.AssertNoRuntimes();
        r.In(() =>
        {
            Assert.Equal(5f, canister.x);
            Assert.NotNull(canister.drift_handler);
        });
        r.Step(Second);
        Assert.True(r.In(() => canister.x) > 5, "the canister drifts on");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AThrowWhileDriftingSendsTheThrowerTheOtherWay()
    {
        // tg's throw_item (living_item_handling.dm:119): newtonian_move(get_angle(target, src)), and the drift's
        // newtonian_impulse (drift_handler.dm:100) does not add the impulses: a new direction replaces the old one
        var r = new T2Room(4302);
        r.In(() =>
        {
            for (int x = 2; x < T2Room.Size; x++)
                for (int y = 2; y < T2Room.Size; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_space)));
        });
        mob_living_carbon_human bob = r.Join(4, 8, "bob", name: "bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        obj_item rock = M4aHands.P2.HandsTests.Item(r, 4, 8, "rock");
        r.In(() => Waiting.NoSleep(bob.put_in_hands(rock)));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.newtonian_move(dir2angle(EAST))));
        r.Step(Second);
        Assert.Equal(90f, r.In(() => Dm.num(bob.drift_handler.drifting_loop.angle)));
        (int X, int Y, int _) before = r.LocOf(bob);

        turf north = r.In(() => locate(before.X, 14, 1));
        r.In(() => bob.throw_mode_on(THROW_MODE_TOGGLE));
        r.Command(() => Waiting.Detach(bob.throw_item(north)));
        r.Step();
        r.In(() =>
        {
            Assert.Equal(180f, Dm.num(bob.drift_handler.drifting_loop.angle)); // south, away from the throw
            Assert.True(bob.drift_handler.drift_force > 1f, "the throw pushes harder than the first push: " + bob.drift_handler.drift_force);
        });
        r.Step(2 * Second);
        (int X, int Y, int _) after = r.LocOf(bob);
        Assert.Equal(before.X, after.X); // no east drift left
        Assert.True(after.Y < before.Y, $"bob drifts south: {before} -> {after}");
        Assert.True(r.In(() => Dm.num(rock.loc.y)) > before.Y, "the rock flew north");
        r.AssertOnLoc(bob, "after the throw");
        r.AssertNoRuntimes();
    }
}
