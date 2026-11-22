using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// /turf/change_area (turf.dm:251-267) and the area's turf bookkeeping (areas.dm:283-396), from the DM and the reading
/// report (turf-area-obj-mob.md section 4): the lazy lists turfs_by_zlevel and turfs_to_uncontain_by_zlevel, the signals,
/// the canonizing procs and the readers.
/// </summary>
public class TurfChangeAreaTests
{
    // What SSmapping's build_area_turfs does for a z level of default tiles (controllers/subsystem/mapping.dm:776-781): the
    // world.area instance lists every turf of the map in turfs_by_zlevel.
    private static area RegisteredWorldArea(int maxx, int maxy, int maxz)
    {
        var space = (area)locate(1, 1, 1).loc;
        space.turfs_by_zlevel.len = maxz;
        for (int z = 1; z <= maxz; z++)
            space.turfs_by_zlevel[(float)z] = block(1, 1, z, maxx, maxy, z);
        return space;
    }

    private static List<turf> ListedAt(area a, int z) => TurfAreaLists.Of<turf>(a.turfs_by_zlevel[(float)z]);

    private static List<string> Lines(TurfAreaWorld t) => TurfAreaTrace.Of(t.World).Lines;

    // ---- change_area ----

    [Fact]
    public void ChangeAreaMovesTheTurfIntoTheNewAreaAndSendsTheThreeSignalsInOrder()
    {
        var t = new TurfAreaWorld(3, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(3, 1, 1);
            var room = Dm.@new<area_misc_testroom>();
            turf T = locate(2, 1, 1);
            var watch = Dm.@new<datum_test_turfarea_listener>();
            watch.label = "changed";
            watch.RegisterSignal(T, COMSIG_TURF_AREA_CHANGED, nameof(datum_test_turfarea_listener.on_turf_area_changed));
            var added = Dm.@new<datum_test_turfarea_listener>();
            added.label = "added";
            added.RegisterSignal(room, COMSIG_AREA_TURF_ADDED, nameof(datum_test_turfarea_listener.on_any));
            var removed = Dm.@new<datum_test_turfarea_listener>();
            removed.label = "removed";
            removed.RegisterSignal(space, COMSIG_AREA_TURF_REMOVED, nameof(datum_test_turfarea_listener.on_any));

            T.change_area(space, room);

            // turf.dm:259-264: contents first (so the turf is already in the new area for the first handler), then
            // TURF_AREA_CHANGED(old_area) on the turf, AREA_TURF_ADDED(turf, old_area) on the new area and
            // AREA_TURF_REMOVED(turf, new_area) on the old one
            Assert.Equal(new[]
            {
                "changed turf(2,1,1) loc=area:area_misc_testroom old=area:area_space",
                "added area:area_misc_testroom(turf(2,1,1),area:area_space,null,null)",
                "removed area:area_space(turf(2,1,1),area:area_misc_testroom,null,null)",
            }, Lines(t));
            Assert.Same(room, T.loc);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ChangeAreaToTheSameAreaDoesNothing()
    {
        var t = new TurfAreaWorld(2, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(2, 1, 1);
            turf T = locate(2, 1, 1);
            var watch = Dm.@new<datum_test_turfarea_listener>();
            watch.label = "changed";
            watch.RegisterSignal(T, COMSIG_TURF_AREA_CHANGED, nameof(datum_test_turfarea_listener.on_any));

            T.change_area(space, space);

            Assert.Empty(Lines(t));
            Assert.Equal(0, space.turfs_to_uncontain_by_zlevel.len);   // turf.dm:254-256: "don't waste our time"
            Assert.Equal(new[] { locate(1, 1, 1), T }, ListedAt(space, 1));
        }
    }

    [Fact]
    public void ChangeAreaAppendsToTheNewAreasListAndOnlyMarksTheOldOneForRemoval()
    {
        var t = new TurfAreaWorld(3, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(3, 1, 1);
            var room = Dm.@new<area_misc_testroom>();
            turf T = locate(2, 1, 1);

            T.change_area(space, room);

            // the removal from the old area is lazy: the old list still holds the turf, the uncontain list names it
            Assert.Equal(new[] { locate(1, 1, 1), T, locate(3, 1, 1) }, ListedAt(space, 1));
            Assert.Equal(new[] { T }, TurfAreaLists.Of<turf>(space.turfs_to_uncontain_by_zlevel[1f]));
            Assert.Equal(new[] { T }, ListedAt(room, 1));
            Assert.Equal(0, room.turfs_to_uncontain_by_zlevel.len);
            // area.contents is the engine's reparent primitive (turf.dm:261): the turf is in the room's contents now
            Assert.True(Dm.@in(T, room.contents));
            Assert.False(Dm.@in(T, space.contents));
        }
    }

    [Fact]
    public void ChangeAreaGrowsBothListsToTheTurfsZLevelWithDistinctFreshListsInTheGap()
    {
        var t = new TurfAreaWorld(1, 1, 3, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = (area)locate(1, 1, 3).loc;   // nothing registered: its lists are the empty ones of /area/New
            var room = Dm.@new<area_misc_testroom>();
            turf T = locate(1, 1, 3);

            T.change_area(space, room);

            // LISTASSERTLEN (turf.dm:257-258): `L.len = I`, then a fresh list() in every new slot, not one shared list
            Assert.Equal(3, room.turfs_by_zlevel.len);
            Assert.Equal(3, space.turfs_to_uncontain_by_zlevel.len);
            foreach (var lists in new[] { room.turfs_by_zlevel, space.turfs_to_uncontain_by_zlevel })
            {
                var l1 = Assert.IsType<DmList>(lists[1f]);
                var l2 = Assert.IsType<DmList>(lists[2f]);
                var l3 = Assert.IsType<DmList>(lists[3f]);
                Assert.Equal(0, l1.len);
                Assert.Equal(0, l2.len);
                Assert.Equal(new[] { T }, TurfAreaLists.Of<turf>(l3));
                Assert.NotSame(l1, l2);
                Assert.NotSame(l2, l3);
            }
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ChangeAreaKeepsTheListsThatAreAlreadyLongEnough()
    {
        var t = new TurfAreaWorld(2, 1, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(2, 1, 2);
            var room = Dm.@new<area_misc_testroom>();
            locate(1, 1, 2).change_area(space, room);
            var lists_before = room.turfs_by_zlevel;
            DmList z1_before = (DmList)room.turfs_by_zlevel[1f];
            locate(2, 1, 2).change_area(space, room);
            Assert.Same(lists_before, room.turfs_by_zlevel);
            Assert.Same(z1_before, room.turfs_by_zlevel[1f]);
            Assert.Equal(new[] { locate(1, 1, 2), locate(2, 1, 2) }, ListedAt(room, 2));
            Assert.Equal(0, ((DmList)room.turfs_by_zlevel[1f]).len);
        }
    }

    [Fact]
    public void ChangeAreaCallsOnChangeAreaLastAndTransfersTheLighting()
    {
        var t = new TurfAreaWorld(2, 1, 1, areaType: typeof(area_space), turfType: typeof(turf_test_turfarea_recording_floor));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(2, 1, 1);
            var room = Dm.@new<area_misc_testroom>();
            turf T = locate(2, 1, 1);
            var watch = Dm.@new<datum_test_turfarea_listener>();
            watch.label = "removed";
            watch.RegisterSignal(space, COMSIG_AREA_TURF_REMOVED, nameof(datum_test_turfarea_listener.on_any));

            T.change_area(space, room);

            Assert.Equal(new[]
            {
                "removed area:area_space(turf(2,1,1),area:area_misc_testroom,null,null)",
                "on_change_area turf(2,1,1) area:area_space -> area:area_misc_testroom loc=area:area_misc_testroom",
            }, Lines(t));
            // M7 lumcount: transfer_area_lighting (lighting_turf.dm:163) builds no lighting mark before SSlighting initialized
            Assert.DoesNotContain(DmWorldState.Current.DeferredSites, s => s.Contains("lighting_turf.dm:163"));
            Assert.False(Dm.truthy(T.dm_lighting_object));
        }
    }

    // ---- A to B to A: the last occurrence goes, the turf stays once at its original position ----

    [Fact]
    public void ATurfThatLeavesAndComesBackStaysOnceAtItsOriginalPlaceAfterCanonizing()
    {
        var t = new TurfAreaWorld(4, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(4, 1, 1);
            var room = Dm.@new<area_misc_testroom>();
            turf T2 = locate(2, 1, 1);
            var original = ListedAt(space, 1);

            T2.change_area(space, room);
            T2.change_area(room, space);

            // the back move appended the turf again (turf.dm:260): it is listed twice until canonized
            Assert.Equal(new[] { locate(1, 1, 1), T2, locate(3, 1, 1), locate(4, 1, 1), T2 }, ListedAt(space, 1));
            Assert.Same(space, T2.loc);

            space.cannonize_contained_turfs();
            room.cannonize_contained_turfs();

            // list minus list removes the LAST occurrence of each item (BYOND ref Remove): the re-appended copy goes
            Assert.Equal(original, ListedAt(space, 1));
            Assert.Empty(ListedAt(room, 1));
            Assert.Equal(0, space.turfs_to_uncontain_by_zlevel.len);
            Assert.Equal(0, room.turfs_to_uncontain_by_zlevel.len);
        }
    }

    [Fact]
    public void ARepeatedRoundTripLeavesTheTurfOnceAndEachRemovalTakesOneOccurrence()
    {
        var t = new TurfAreaWorld(3, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(3, 1, 1);
            var room = Dm.@new<area_misc_testroom>();
            turf T2 = locate(2, 1, 1);
            var original = ListedAt(space, 1);
            for (int i = 0; i < 3; i++)
            {
                T2.change_area(space, room);
                T2.change_area(room, space);
            }
            Assert.Equal(6, ListedAt(space, 1).Count);   // 3 original + three re-appended copies of T2
            Assert.Equal(3, ((DmList)space.turfs_to_uncontain_by_zlevel[1f]).len);

            var canonical = space.get_turfs_by_zlevel(1);   // the reader canonizes first (areas.dm:309-310)

            Assert.Equal(original, TurfAreaLists.Of<turf>(canonical));
            room.cannonize_contained_turfs();
            Assert.Empty(ListedAt(room, 1));
        }
    }

    [Fact]
    public void ATurfThatMovedOnAndNeverCameBackIsGoneFromTheOldListAfterCanonizing()
    {
        var t = new TurfAreaWorld(3, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(3, 1, 1);
            var room = Dm.@new<area_misc_testroom>();
            locate(2, 1, 1).change_area(space, room);
            space.cannonize_contained_turfs();
            Assert.Equal(new[] { locate(1, 1, 1), locate(3, 1, 1) }, ListedAt(space, 1));
            Assert.Equal(new[] { locate(2, 1, 1) }, ListedAt(room, 1));
        }
    }

    // ---- the canonizing procs (areas.dm:326-358) ----

    [Fact]
    public void CannonizeByZLevelRemovesOnlyThatLevelsEntriesAndClearsItsUncontainList()
    {
        var t = new TurfAreaWorld(2, 1, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(2, 1, 2);
            var room = Dm.@new<area_misc_testroom>();
            locate(1, 1, 1).change_area(space, room);
            locate(1, 1, 2).change_area(space, room);

            space.cannonize_contained_turfs_by_zlevel(1);

            Assert.Equal(new[] { locate(2, 1, 1) }, ListedAt(space, 1));
            Assert.Equal(new[] { locate(1, 1, 2), locate(2, 1, 2) }, ListedAt(space, 2));   // level 2 not touched yet
            Assert.Equal(1, ((DmList)space.turfs_to_uncontain_by_zlevel[2f]).len);
        }
    }

    [Fact]
    public void TgQuirkAutocleanTrimsTrailingEmptyUncontainListsButKeepsOneBelowTheLastUsefulOne()
    {
        // areas.dm:337-345: the walk assigns new_length = i before looking at the next lower list, so it stops one list
        // above the last list that has entries
        var t = new TurfAreaWorld(1, 1, 4, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(1, 1, 4);
            var room = Dm.@new<area_misc_testroom>();
            locate(1, 1, 1).change_area(space, room);
            // uncontain lists: [ list(T1), list(), list(), list() ]
            space.turfs_to_uncontain_by_zlevel.len = 4;
            space.turfs_to_uncontain_by_zlevel[2f] = Dm.list();
            space.turfs_to_uncontain_by_zlevel[3f] = Dm.list();
            space.turfs_to_uncontain_by_zlevel[4f] = Dm.list();

            space.cannonize_contained_turfs_by_zlevel(1);

            Assert.Equal(2, space.turfs_to_uncontain_by_zlevel.len);
            Assert.Equal(0, ((DmList)space.turfs_to_uncontain_by_zlevel[1f]).len);
            Assert.Equal(0, ((DmList)space.turfs_to_uncontain_by_zlevel[2f]).len);
            Assert.DoesNotContain(locate(1, 1, 1), ListedAt(space, 1));
        }
    }

    [Fact]
    public void AutocleanOfAnAllEmptyUncontainListCutsItToNothing()
    {
        var t = new TurfAreaWorld(1, 1, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(1, 1, 2);
            space.turfs_to_uncontain_by_zlevel = Dm.list(Dm.list(), Dm.list());
            space.cannonize_contained_turfs_by_zlevel(1);
            Assert.Equal(0, space.turfs_to_uncontain_by_zlevel.len);
        }
    }

    [Fact]
    public void WithoutAutocleanTheLevelsUncontainListIsReplacedByAnEmptyOneAndNothingIsCut()
    {
        var t = new TurfAreaWorld(2, 1, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(2, 1, 2);
            var room = Dm.@new<area_misc_testroom>();
            locate(1, 1, 1).change_area(space, room);
            space.turfs_to_uncontain_by_zlevel.len = 2;   // a trailing empty list that autoclean would cut
            space.turfs_to_uncontain_by_zlevel[2f] = Dm.list();
            space.cannonize_contained_turfs_by_zlevel(1, _autoclean: FALSE);
            Assert.Equal(2, space.turfs_to_uncontain_by_zlevel.len);
            Assert.Equal(0, ((DmList)space.turfs_to_uncontain_by_zlevel[1f]).len);
            Assert.Equal(new[] { locate(2, 1, 1) }, ListedAt(space, 1));
        }
    }

    [Fact]
    public void CannonizeAllLevelsEmptiesTheUncontainListsAndReplacesTheListItself()
    {
        var t = new TurfAreaWorld(2, 1, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(2, 1, 2);
            var room = Dm.@new<area_misc_testroom>();
            locate(1, 1, 1).change_area(space, room);
            locate(2, 1, 2).change_area(space, room);
            var before = space.turfs_to_uncontain_by_zlevel;

            space.cannonize_contained_turfs();

            Assert.NotSame(before, space.turfs_to_uncontain_by_zlevel);   // `turfs_to_uncontain_by_zlevel = list()` (areas.dm:357)
            Assert.Equal(0, space.turfs_to_uncontain_by_zlevel.len);
            Assert.Equal(new[] { locate(2, 1, 1) }, ListedAt(space, 1));
            Assert.Equal(new[] { locate(1, 1, 2) }, ListedAt(space, 2));
        }
    }

    [Fact]
    public void CannonizeOnAnAreaWithNoPendingRemovalsChangesNothing()
    {
        var t = new TurfAreaWorld(2, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(2, 1, 1);
            space.cannonize_contained_turfs();
            Assert.Equal(new[] { locate(1, 1, 1), locate(2, 1, 1) }, ListedAt(space, 1));
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- the readers (areas.dm:283-396) ----

    [Fact]
    public void GetTurfsByZLevelCanonizesThatLevelFirstAndAnswersAnEmptyListBeyondTheLists()
    {
        var t = new TurfAreaWorld(2, 1, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(2, 1, 2);
            var room = Dm.@new<area_misc_testroom>();
            locate(1, 1, 1).change_area(space, room);
            locate(1, 1, 2).change_area(space, room);

            var level1 = (DmList)space.get_turfs_by_zlevel(1);
            Assert.Equal(new[] { locate(2, 1, 1) }, TurfAreaLists.Of<turf>(level1));
            // level 2 was not asked for, so its entry is still pending
            Assert.Equal(1, ((DmList)space.turfs_to_uncontain_by_zlevel[2f]).len);
            Assert.Equal(0, ((DmList)space.get_turfs_by_zlevel(7)).len);
            Assert.Equal(new[] { locate(1, 1, 1) }, TurfAreaLists.Of<turf>(room.get_turfs_by_zlevel(1)));
        }
    }

    [Fact]
    public void GetZlevelTurfListsCanonizesAndSkipsEmptyLevels()
    {
        var t = new TurfAreaWorld(2, 1, 3, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(2, 1, 3);
            var room = Dm.@new<area_misc_testroom>();
            locate(1, 1, 2).change_area(space, room);
            locate(2, 1, 2).change_area(space, room);   // level 2 of space is now empty once canonized

            var lists = (DmList)space.get_zlevel_turf_lists();

            Assert.Equal(2, lists.len);   // levels 1 and 3
            Assert.Equal(new[] { locate(1, 1, 1), locate(2, 1, 1) }, TurfAreaLists.Of<turf>(lists[1f]));
            Assert.Equal(new[] { locate(1, 1, 3), locate(2, 1, 3) }, TurfAreaLists.Of<turf>(lists[2f]));
            Assert.Equal(0, space.turfs_to_uncontain_by_zlevel.len);
        }
    }

    [Fact]
    public void GetTurfsFromAllZlevelsConcatenatesTheLevelsInOrder()
    {
        var t = new TurfAreaWorld(2, 1, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(2, 1, 2);
            var all = (DmList)space.get_turfs_from_all_zlevels();
            Assert.Equal(new[] { locate(1, 1, 1), locate(2, 1, 1), locate(1, 1, 2), locate(2, 1, 2) }, TurfAreaLists.Of<turf>(all));
        }
    }

    [Fact]
    public void HasContainedTurfsAndHighestZlevelSubtractTheLoosePartWithoutCanonizing()
    {
        var t = new TurfAreaWorld(1, 1, 3, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(1, 1, 3);
            var room = Dm.@new<area_misc_testroom>();
            Assert.True(Dm.eq(space.has_contained_turfs(), TRUE));
            Assert.True(Dm.eq(space.get_highest_zlevel(), 3));
            Assert.False(Dm.truthy(room.has_contained_turfs()));
            Assert.True(Dm.eq(room.get_highest_zlevel(), 0));

            locate(1, 1, 3).change_area(space, room);
            // space still lists the turf of level 3 but has one pending removal there: arithmetic, no canonizing (:296-299)
            Assert.Equal(1, ((DmList)space.turfs_by_zlevel[3f]).len);
            Assert.True(Dm.eq(space.get_highest_zlevel(), 2));
            Assert.True(Dm.eq(room.get_highest_zlevel(), 3));
            Assert.True(Dm.eq(room.has_contained_turfs(), TRUE));

            locate(1, 1, 2).change_area(space, room);
            locate(1, 1, 1).change_area(space, room);
            Assert.False(Dm.truthy(space.has_contained_turfs()));   // every level has as many pending removals as turfs
            Assert.True(Dm.eq(space.get_highest_zlevel(), 0));
            Assert.Equal(3, space.turfs_to_uncontain_by_zlevel.len);
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- reg_in_areas_in_z and update_areasize (areas.dm:377-390, :614-624) ----

    [Fact]
    public void AnAreaRegistersAtItsLowestZLevelOnlyAndTwiceWhenAskedTwice()
    {
        var t = new TurfAreaWorld(2, 1, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(2, 1, 2);
            var room = Dm.@new<area_misc_testroom>();
            locate(2, 1, 1).change_area(space, room);
            locate(2, 1, 2).change_area(space, room);
            var empty = Dm.@new<area_test_turfarea_common>();

            room.reg_in_areas_in_z();
            empty.reg_in_areas_in_z();   // no turfs: returns before touching SSmapping (areas.dm:376-377)

            // area.z is the z of the area's turf with the lowest z (BYOND ref), so the level-2 turf adds no entry
            var in_z = SSmapping.areas_in_z;
            Assert.Equal(new[] { room }, TurfAreaLists.Of<area>(in_z["1"]));
            Assert.Null(in_z["2"]);
            room.reg_in_areas_in_z();
            Assert.Equal(new[] { room, room }, TurfAreaLists.Of<area>(in_z["1"]));   // no duplicate check (areas.dm:388)
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void UpdateAreasizeCountsOnlyTheOpenTurfsOfTheAreaAndSkipsOutdoors()
    {
        var t = new TurfAreaWorld(4, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = RegisteredWorldArea(4, 1, 1);
            Dm.@new<turf_closed_indestructible>(locate(3, 1, 1));
            Dm.@new<turf_closed_indestructible>(locate(4, 1, 1));
            // the replacement leaves the dead turf objects listed in the old area (R13), so register the room with the new ones
            var room = Dm.@new<area_misc_testroom>();
            foreach (turf T in Dm.each<turf>(block(1, 1, 1, 4, 1, 1)))
                T.change_area(space, room);
            Assert.Equal(0, room.areasize);

            room.update_areasize();

            Assert.Equal(2, room.areasize);   // the two floors, not the two walls (areas.dm:422: `for(var/turf/open/...)`)
            room.outdoors = TRUE;
            room.areasize = 99;
            Assert.True(Dm.eq(room.update_areasize(), FALSE));
            Assert.Equal(99, room.areasize);
        }
    }
}

/// <summary>A floor that records on_change_area, to see the order of change_area's last step.</summary>
[DmPath("/turf/open/indestructible/test_turfarea_recording_floor")]
public class turf_test_turfarea_recording_floor : turf_open_indestructible
{
    public override void on_change_area(area old_area = null, area new_area = null)
    {
        TurfAreaTrace.Add("on_change_area " + TurfAreaTrace.D(this) + " " + TurfAreaTrace.D(old_area) + " -> " + TurfAreaTrace.D(new_area) + " loc=" + TurfAreaTrace.D(loc));
        base.on_change_area(old_area, new_area);
    }
}
