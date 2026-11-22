using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.P4;

// M3 P4's smoke tests of icon smoothing (scope S1): __HELPERS/icon_smoothing.dm, __HELPERS/bitflag_lists.dm,
// controllers/subsystem/icon_smooth.dm, SETUP_SMOOTHING and the builtin orange.
public class SmoothingTests
{
    [Fact]
    public void SetSmoothingGroupsTurnsTheGroupTextIntoCachedBitflagLists()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            object groups = null;
            SET_SMOOTHING_GROUPS("-1,58,", ref groups);
            DmList list = Assert.IsType<DmList>(groups);
            // -1 is S_OBJ(1): MAX_S_TURF + 1 + 1 = 71, so key "2" bit 23; 58 is key "2" bit 10 (bitflag_lists.dm:22-25)
            Assert.Equal(1f, list.len);
            Assert.True(Dm.eq(list["2"], (1 << 23) | (1 << 10)));
            object again = null;
            SET_SMOOTHING_GROUPS("-1,58,", ref again);
            Assert.Same(list, again); // cached in GLOB.bitflag_lists
        }
    }

    [Fact]
    public void TheJunctionHelpersMatchTgsTables()
    {
        Assert.True(Dm.eq(dir_to_junction(NORTHWEST), NORTHWEST_JUNCTION));
        Assert.True(Dm.eq(dir_to_junction(UP), NONE));
        Assert.True(Dm.eq(reverse_junction(NORTH_JUNCTION | NORTHEAST_JUNCTION), SOUTH_JUNCTION | SOUTHWEST_JUNCTION));
        Assert.True(Dm.eq(reverse_ndir(NORTH_JUNCTION | WEST_JUNCTION | NORTHWEST_JUNCTION), NORTHWEST));
        Assert.True(Dm.eq(dir_to_all_junctions(EAST), EAST_JUNCTION | SOUTHEAST_JUNCTION | NORTHEAST_JUNCTION));
        Assert.True(Dm.eq(all_junctions_of_dir(NORTHEAST), NONE));
    }

    [Fact]
    public void TheBorderObjectLookupConnectsSidesAndTheOwnTurf()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            DmList north = (DmList)GLOB.adjacent_direction_lookup[NORTH];
            // own turf (NONE + 1): an east-facing neighbour connects at the south-east corner (icon_smoothing.dm:94-96)
            Assert.True(Dm.eq(((DmList)north[NONE + 1])[EAST], SOUTHEAST_JUNCTION));
            // straight on (NORTH + 1): a south-facing window there links north (:106-109)
            Assert.True(Dm.eq(((DmList)north[NORTH + 1])[SOUTH], NORTH));
            Assert.Null(north[SOUTH + 1]); // behind: nothing encoded
        }
    }

    [Fact]
    public void WallsGetTgsJunctionStates()
    {
        var room = TgToyRoom.Create(9, 9);
        using (room.World.Enter())
        {
            // an L of walls away from the rim (the rim's indestructible walls have no smoothing groups)
            var corner = Dm.@new<turf_closed_wall>(locate(4, 4, 1));
            var east = Dm.@new<turf_closed_wall>(locate(5, 4, 1));
            var north = Dm.@new<turf_closed_wall>(locate(4, 5, 1));
            var block = Dm.@new<turf_closed_wall>(locate(5, 5, 1));
            Assert.True(Dm.truthy(Dm.bitand(corner.smoothing_flags, SMOOTH_QUEUED))); // /turf/Initialize's QUEUE_SMOOTH
            Waiting.NoSleep(SSicon_smooth.Initialize());

            // a 2x2 block: every wall has two cardinals and the diagonal between them (icon_smoothing.dm:252-271)
            Assert.Equal("wall-" + (NORTH_JUNCTION | EAST_JUNCTION | NORTHEAST_JUNCTION), corner.icon_state);
            Assert.Equal("wall-" + (NORTH_JUNCTION | WEST_JUNCTION | NORTHWEST_JUNCTION), east.icon_state);
            Assert.Equal("wall-" + (SOUTH_JUNCTION | EAST_JUNCTION | SOUTHEAST_JUNCTION), north.icon_state);
            Assert.Equal("wall-" + (SOUTH_JUNCTION | WEST_JUNCTION | SOUTHWEST_JUNCTION), block.icon_state);
            Assert.False(Dm.truthy(Dm.bitand(corner.smoothing_flags, SMOOTH_QUEUED)));
            Assert.Empty(room.RuntimeText);

            // a lone wall: junction 0; a line: only the cardinals (a diagonal needs both of its cardinals)
            var lone = Dm.@new<turf_closed_wall>(locate(7, 7, 1));
            var line_a = Dm.@new<turf_closed_wall>(locate(2, 7, 1));
            var line_b = Dm.@new<turf_closed_wall>(locate(3, 7, 1));
            Waiting.NoSleep(SSicon_smooth.Initialize());
            Assert.Equal("wall-0", lone.icon_state);
            Assert.Equal("wall-" + EAST_JUNCTION, line_a.icon_state);
            Assert.Equal("wall-" + WEST_JUNCTION, line_b.icon_state);
        }
    }

    [Fact]
    public void TheSubsystemFiresItsQueueAndDefersAtomsNotInitializedYet()
    {
        var room = TgToyRoom.Create(9, 9);
        using (room.World.Enter())
        {
            var wall = Dm.@new<turf_closed_wall>(locate(4, 4, 1));
            var neighbour = Dm.@new<turf_closed_wall>(locate(4, 5, 1));
            Dm.mask(ref neighbour.flags_1, Dm.bitnot(INITIALIZED_1));
            QUEUE_SMOOTH(neighbour); // already queued by its Initialize: QUEUE_SMOOTH does nothing twice
            Assert.True(Dm.@in(wall, SSicon_smooth.smooth_queue) && Dm.@in(neighbour, SSicon_smooth.smooth_queue));
            SSicon_smooth.state = SS_RUNNING; // as the Master's RunQueue sets it, so that MC_TICK_CHECK does not pause
            Waiting.NoSleep(SSicon_smooth.fire());
            Assert.Equal("wall-" + NORTH_JUNCTION, wall.icon_state);
            Assert.True(Dm.@in(neighbour, SSicon_smooth.smooth_queue)); // icon_smooth.dm:34-37: deferred, then swapped back
            Assert.True(Dm.truthy(SSicon_smooth.can_fire));
        }
    }

    [Fact]
    public void OrangeIsTheRangeSquareWithoutItsCentre()
    {
        var room = TgToyRoom.Create(7, 7);
        using (room.World.Enter())
        {
            turf centre = locate(4, 4, 1);
            var on_centre = Dm.@new<test_p4_live_probe>(centre);
            var next_door = Dm.@new<test_p4_live_probe>(locate(5, 4, 1));

            DmList around_turf = orange(1, centre);
            Assert.False(Dm.@in(centre, around_turf));
            Assert.False(Dm.@in(on_centre, around_turf));
            Assert.True(Dm.@in(next_door, around_turf));
            Assert.Equal(8, Dm.each_cast<object>(around_turf).Count(x => x is turf));
            Assert.True(Dm.@in(room.Room, around_turf)); // the first turf of an area brings its area (OpenDream HandleRange)
            // DantomSpiral order: the left column bottom to top first
            Assert.Same(locate(3, 3, 1), around_turf[1]);
            Assert.Same(locate(3, 4, 1), around_turf[3]);

            // a movable centre: its turf and that turf's other contents come first, the centre itself is left out
            DmList around_movable = orange(next_door, 1); // the arguments in either order
            Assert.Same(locate(5, 4, 1), around_movable[1]);
            Assert.False(Dm.@in(next_door, around_movable));
            Assert.True(Dm.@in(on_centre, around_movable));
        }
    }
}
