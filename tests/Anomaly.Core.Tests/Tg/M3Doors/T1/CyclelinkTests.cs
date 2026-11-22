using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// T1: cyclelink_helper and cyclelink_helper_multi (mapping_helpers.dm:182-203) and what the links do when an airlock opens
// (airlock.dm:1327-1360), with airlock.dm:224 cyclelinkairlock and :216 update_other_id.

public class CyclelinkTests
{
    private static obj_machinery_door_airlock Cycle(float dirToPartner, int x, int y)
    {
        var door = Dm.@new<obj_machinery_door_airlock_public>(locate(x, y, 1));
        Dm.@new<obj_effect_mapping_helpers_airlock_cyclelink_helper>(locate(x, y, 1)).dir = dirToPartner;
        return door;
    }

    [Fact]
    public void TwoFacingHelpersLinkTheirAirlocksBothWays()
    {
        obj_machinery_door_airlock a = null, b = null;
        var room = T1.Room(mapExtras: w =>
        {
            a = Cycle(EAST, 2, 4);
            b = Cycle(WEST, 6, 4);
        });
        room.In(() =>
        {
            Assert.True(Dm.eq(a.cyclelinkeddir, EAST)); // payload (:186)
            Assert.True(Dm.eq(b.cyclelinkeddir, WEST));
            Assert.Same(b, a.cyclelinkedairlock);
            Assert.Same(a, b.cyclelinkedairlock);
            Assert.Null(Dm.locate<obj_effect_mapping_helpers>(locate(2, 4, 1)));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ALinkNeedsThePartnerToFaceBack()
    {
        // airlock.dm:236: `FoundDoor.cyclelinkeddir != get_dir(FoundDoor, src)` -> not a partner; no link, only a mapping log
        obj_machinery_door_airlock a = null, b = null, c = null;
        var room = T1.Room(mapExtras: w =>
        {
            a = Cycle(EAST, 2, 3);
            b = Cycle(NORTH, 6, 3); // faces north: not back at a
            c = Cycle(EAST, 2, 5);
            Dm.@new<obj_machinery_door_airlock_public>(locate(6, 5, 1)); // no helper: cyclelinkeddir 0
        });
        room.In(() =>
        {
            Assert.Null(a.cyclelinkedairlock);
            Assert.Null(b.cyclelinkedairlock);
            Assert.Null(c.cyclelinkedairlock);
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ThePartnerSearchReachesElevenTilesAndNoFurther()
    {
        // airlock.dm:229-240: limit = DOOR_VISION_DISTANCE (11); the loop steps, looks, then decrements
        obj_machinery_door_airlock a11 = null, b11 = null, a12 = null, b12 = null;
        var room = T1.Room(width: 17, height: 8, mapExtras: w =>
        {
            a11 = Cycle(EAST, 2, 3);
            b11 = Cycle(WEST, 13, 3); // 11 tiles away
            a12 = Cycle(EAST, 2, 5);
            b12 = Cycle(WEST, 14, 5); // 12 tiles away
        });
        room.In(() =>
        {
            Assert.Same(b11, a11.cyclelinkedairlock);
            Assert.Same(a11, b11.cyclelinkedairlock);
            Assert.Null(a12.cyclelinkedairlock);
            Assert.Null(b12.cyclelinkedairlock);
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void OpeningOneClosesTheOtherAfterTwoDecisecondsAndItsOwnSafetyRules()
    {
        // airlock.dm:1351-1357: `addtimer(CALLBACK(cyclelinkedairlock, PROC_REF(close)), BYPASS_DOOR_CHECKS)`: tg passes
        // the check mode (2) as the WAIT, so the partner shuts 2 ds (4 ticks) after the open() call
        obj_machinery_door_airlock a = null, b = null;
        var room = T1.Room(mapExtras: w =>
        {
            a = Cycle(EAST, 2, 4);
            b = Cycle(WEST, 6, 4);
        });
        room.In(() => Waiting.Detach(b.open()));
        room.Step(20);
        room.In(() => Assert.False(T1.Dense(b)));
        room.In(() => Waiting.Detach(a.open()));
        var s = T1.Record(room, b, 30);
        Assert.Equal(4, T1.FirstTick(s, x => x.State == AIRLOCK_CLOSING));
        Assert.True(s[3].State == AIRLOCK_OPEN);
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AnEmergencyAirlockDoesNotCycleItsPartnerShut()
    {
        // airlock.dm:1354: `if(!shuttledocked && !emergency && !cyclelinkedairlock.shuttledocked && !cyclelinkedairlock.emergency)`
        obj_machinery_door_airlock a = null, b = null;
        var room = T1.Room(mapExtras: w =>
        {
            a = Cycle(EAST, 2, 4);
            b = Cycle(WEST, 6, 4);
        });
        room.In(() =>
        {
            Waiting.Detach(b.open());
            a.emergency = TRUE;
        });
        room.Step(20);
        room.In(() => Waiting.Detach(a.open()));
        room.Step(60);
        room.In(() => Assert.False(T1.Dense(b)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void OpeningWhileThePartnerIsStillOpeningMakesItCloseRightAfter()
    {
        // airlock.dm:1355-1356 `if(cyclelinkedairlock.operating) cyclelinkedairlock.delayed_close_requested = TRUE`; the end of
        // open() (:1385-1387) then `addtimer(CALLBACK(src, PROC_REF(close)), FORCING_DOOR_CHECKS)` (1 ds)
        obj_machinery_door_airlock a = null, b = null;
        var room = T1.Room(mapExtras: w =>
        {
            a = Cycle(EAST, 2, 4);
            b = Cycle(WEST, 6, 4);
        });
        room.In(() => Waiting.Detach(b.open()));
        room.Step(4); // b is mid-opening
        room.In(() =>
        {
            Assert.True(Dm.truthy(b.operating));
            Waiting.Detach(a.open());
            Assert.True(Dm.truthy(b.delayed_close_requested));
        });
        room.Step(8); // b finished opening at tick 12 of its own count (tick 4 + 8 now)
        room.In(() =>
        {
            Assert.False(Dm.truthy(b.delayed_close_requested));
            Assert.Equal(AIRLOCK_OPEN, b.airlock_state);
        });
        room.Step(2);
        room.In(() => Assert.Equal(AIRLOCK_CLOSING, b.airlock_state));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void MultiHelpersJoinAirlocksOfOneCycleIdAndOpeningOneClosesTheOthers()
    {
        // cyclelink_helper_multi :197 sets closeOtherId; LateInitialize's update_other_id (:216) fills close_others of every
        // airlock with that id; airlock.dm:1345-1350 closes them (the same 2 ds wait)
        obj_machinery_door_airlock a = null, b = null, c = null, other = null;
        var room = T1.Room(mapExtras: w =>
        {
            a = Dm.@new<obj_machinery_door_airlock_public>(locate(2, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_cyclelink_helper_multi>(locate(2, 2, 1)).cycle_id = "t1_cycle";
            b = Dm.@new<obj_machinery_door_airlock_public>(locate(4, 3, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_cyclelink_helper_multi>(locate(4, 3, 1)).cycle_id = "t1_cycle";
            c = Dm.@new<obj_machinery_door_airlock_public>(locate(6, 5, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_cyclelink_helper_multi>(locate(6, 5, 1)).cycle_id = "t1_cycle";
            other = Dm.@new<obj_machinery_door_airlock_public>(locate(3, 6, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_cyclelink_helper_multi>(locate(3, 6, 1)).cycle_id = "t1_other";
        });
        room.In(() =>
        {
            Assert.Equal("t1_cycle", a.closeOtherId);
            Assert.Equal(2f, Dm.num(a.close_others.len));
            Assert.True(Dm.@in(b, a.close_others));
            Assert.True(Dm.@in(c, a.close_others));
            Assert.False(Dm.@in(a, a.close_others)); // `Airlock != src`
            Assert.False(Dm.@in(other, a.close_others));
            Assert.Equal(0f, Dm.num(other.close_others.len)); // alone with its id
            Waiting.Detach(b.open());
            Waiting.Detach(c.open());
            Waiting.Detach(other.open());
        });
        room.Step(20);
        room.In(() => Waiting.Detach(a.open()));
        room.Step(20);
        room.In(() =>
        {
            Assert.True(T1.Dense(b));
            Assert.True(T1.Dense(c));
            Assert.False(T1.Dense(other)); // another cycle id: left open
            Assert.False(T1.Dense(a));
        });
        T1.NoRuntimes(room);
    }
}
