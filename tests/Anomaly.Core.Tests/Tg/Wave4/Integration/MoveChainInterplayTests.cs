using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.Wave4.Integration;

/// <summary>
/// Cases where two packages' real code meet, which no fixture could show: the chain (A) with the real turf and area procs (B)
/// and the real mob (C). Written from atoms_movable.dm, turf/open/_open.dm:226-279 (<c>Entered</c> and <c>transfer_occupant</c>) and
/// areas.dm:581-606.
/// </summary>
public class MoveChainInterplayTests
{
    private static area Carve(string name, int fromX, int toX, int fromY, int toY)
    {
        var made = Dm.@new<area_misc_testroom>();
        made.name = name;
        foreach (turf t in Dm.each<turf>(block(fromX, fromY, 1, toX, toY, 1)))
            t.change_area((area)t.loc, made);
        return made;
    }

    [Fact]
    public void ARealTransitTurfSendsItsEnteredThenZMovesTheArrivalAndTheChainResolvesTheOuterMovedFirst()
    {
        // _open.dm:226-234: turf/open/Entered calls ..() first, then transfer_occupant, which zMoves the arrival to the
        // destination while Move is still inside newloc.Entered: the nested forceMove flushes the outer Moved first
        var room = TgToyRoom.Create(levels: 2);
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), transit = locate(4, 3, 1), landing = locate(3, 3, 2);
            var open_transit = Assert.IsAssignableFrom<turf_open>(transit);
            open_transit.destination_x = 3;
            open_transit.destination_y = 3;
            open_transit.destination_z = 2;
            var m = Thing("mover", a);
            On(transit, COMSIG_ATOM_ENTERED, "entered_transit", 1);
            On(transit, COMSIG_ATOM_EXITED, "exited_transit", 1);
            On(landing, COMSIG_ATOM_ENTERED, "entered_landing", 2);
            On(m, COMSIG_MOVABLE_MOVED, "moved", 3, act: _ => ToyTrace.Add("   z_moving=" + Dm.str(m.currently_z_moving)));
            On(m, COMSIG_MOVABLE_Z_CHANGED, "z_changed", 2);
            Trace(room.World).Clear();

            object result = Step(m, EAST);

            AssertNumber(TRUE, result);   // the Move itself succeeded
            AssertTrace(new[]
            {
                "entered_transit (4,3) mover",
                "moved mover (3,3) 4 0",          // flushed by the zMove's forceMove, while the mover is still on the transit turf
                "   z_moving=2",                   // zMove set CURRENTLY_Z_MOVING_GENERIC for it
                "exited_transit (4,3) mover",
                "entered_landing (3,3,2) mover (4,3)",
                "moved mover (4,3) 0 1",
                "   z_moving=2",
                "z_changed mover (4,3) (3,3,2)",
            }, Trace(room.World));
            Assert.Same(landing, m.loc);
            AssertNumber(FALSE, m.currently_z_moving);   // cleared after the forceMove (zMove)
            Assert.Null(m.active_movement);
            Assert.Empty(room.Runtimes);
        }
    }

    private static turf_open Transit(turf at, float x, float y, float z)
    {
        var open = Assert.IsAssignableFrom<turf_open>(at);
        open.destination_x = x;
        open.destination_y = y;
        open.destination_z = z;
        return open;
    }

    [Fact]
    public void ATransitTurfTransfersAnOrdinaryArrivalButNotOneThatIsAlreadyZMoving()
    {
        var room = TgToyRoom.Create(levels: 2);
        using (room.World.Enter())
        {
            turf a = locate(3, 3, 1), transit = locate(4, 3, 1);
            Transit(transit, 3, 3, 2);

            var moving = Thing("moving", a);
            moving.currently_z_moving = CURRENTLY_Z_FALLING;   // `occupant.currently_z_moving` (_open.dm:249)
            Force(moving, transit);
            Assert.Same(transit, moving.loc);

            var plain = Thing("plain", a);
            Force(plain, transit);   // an ordinary arrival is transferred
            Assert.Equal(2f, plain.z);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void APullerStepsOntoATransitTurfAndTheThingItPullsFollowsToTheTurfBehindItOnTheNewLevel()
    {
        // _open.dm:262-268: after zMove-ing the occupant, every thing it pulls is zMoved to the turf behind its puller
        // (get_step(puller.loc, REVERSE_DIR(puller.dir)) || puller.loc); the puller keeps pulling it (ZMOVE_ALLOW_BUCKLED has
        // no pull-check flags), and Move's own pull-follow block finds the crate one tile away and does nothing
        var room = TgToyRoom.Create(levels: 2);
        using (room.World.Enter())
        {
            turf behind = locate(2, 3, 1), here = locate(3, 3, 1), transit = locate(4, 3, 1);
            Transit(transit, 3, 3, 2);
            var puller = Dm.@new<mob_living>(here);
            puller.name = "puller";
            var crate = Thing("crate", locate(2, 3, 1));
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE)); // the living start_pulling returns nothing for an object (living.dm:392)
            Assert.Same(crate, puller.pulling);
            On(crate, COMSIG_MOVABLE_Z_CHANGED, "crate_z_changed", 2);
            On(puller, COMSIG_MOVABLE_Z_CHANGED, "puller_z_changed", 2);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(puller, EAST));

            AssertTrace(new[]
            {
                "puller_z_changed puller (4,3) (3,3,2)",
                "crate_z_changed crate (2,3) (2,3,2)",
            }, Trace(room.World));
            Assert.Same(locate(3, 3, 2), puller.loc);
            Assert.Same(locate(2, 3, 2), crate.loc);
            Assert.Same(crate, puller.pulling);
            Assert.Same(puller, crate.pulledby);
            AssertNumber(FALSE, crate.currently_z_moving);
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void AnAreaSensitiveItemInABoxGetsEnterAreaAndExitAreaWhenTheBoxCrossesAnAreaBorderButTheBoxItselfDoesNot()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            room.Room.name = "room";
            Carve("annex", 4, 6, 2, 6);
            var box = Thing("box", locate(3, 3, 1));
            var item = Thing("item", box);
            item.become_area_sensitive("integration");
            Assert.True(Dm.truthy(box.important_recursive_contents?[RECURSIVE_CONTENTS_AREA_SENSITIVE]));   // the box lists it
            On(item, COMSIG_EXIT_AREA, "item_exit_area", 1);
            On(item, COMSIG_ENTER_AREA, "item_enter_area", 1);
            On(box, COMSIG_EXIT_AREA, "box_exit_area", 1);
            On(box, COMSIG_ENTER_AREA, "box_enter_area", 1);
            Trace(room.World).Clear();

            Step(box, EAST);

            // area/Exited and area/Entered fan out to the recipients of the arrived thing's channel: the item, not the box
            AssertTrace(new[] { "item_exit_area item area:room", "item_enter_area item area:annex" }, Trace(room.World));
            Assert.Empty(room.Runtimes);
        }
    }

    [Fact]
    public void ARealMobWalksTheRoomMovesThroughAnAreaBorderBumpsAWallAndSendsItsMovedToItsListeners()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            room.Room.name = "room";
            Carve("annex", 4, 6, 2, 6);
            var m = Dm.@new<mob>(locate(3, 3, 1));
            m.name = "mob";
            On(m, COMSIG_MOVABLE_MOVED, "moved", 3);
            On(m, COMSIG_MOVABLE_BUMP, "bump", 1);
            Trace(room.World).Clear();

            AssertNumber(TRUE, Step(m, EAST));    // (3,3) to (4,3): across the area border
            AssertNumber(TRUE, Step(m, NORTH));
            AssertNumber(TRUE, Step(m, EAST));
            AssertNumber(TRUE, Step(m, EAST));
            AssertNumber(FALSE, Step(m, EAST));   // (7,4) is the wall

            AssertTrace(new[]
            {
                "moved mob (3,3) 4 0",
                "moved mob (4,3) 1 0",
                "moved mob (4,4) 4 0",
                "moved mob (5,4) 4 0",
                "bump mob (7,4)",
            }, Trace(room.World));
            Assert.Same(locate(6, 4, 1), m.loc);
            Assert.Equal(EAST, m.dir);
            Assert.Empty(room.Runtimes);
        }
    }
}
