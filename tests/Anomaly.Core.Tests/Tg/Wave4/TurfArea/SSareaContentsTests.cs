using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// SSarea_contents (code/controllers/subsystem/area_contents.dm), the first resumable subsystem: the scan of GLOB.areas
/// through currentrun, the 100 loose turfs threshold, the removal of one entry at a time and MC_TICK_CHECK, which pauses and
/// returns from fire() (not a sleep) so that the Master calls fire(resumed = TRUE) again. DV3: tick usage is virtual and
/// always 0, so MC_TICK_CHECK trips only on `src.state != SS_RUNNING`; the tests drive "the tick ran out" by leaving the
/// subsystem in a state other than SS_RUNNING before each call.
/// </summary>
public class SSareaContentsTests
{
    private const int ALLOWED_LOOSE_TURFS = 100;   // area_contents.dm:1 (a file-local #define)

    private static datum_controller_subsystem_area_contents Subsystem()
    {
        var ss = Dm.@new<datum_controller_subsystem_area_contents>();
        ss.state = SS_RUNNING;   // the Master sets it before every fire()
        return ss;
    }

    /// <summary>One fire() call that the tick runs out in after one unit of work: MC_TICK_CHECK trips on a non-running state.</summary>
    private static void FireOneUnit(datum_controller_subsystem_area_contents ss, bool resumed)
    {
        ss.state = SS_PAUSED;
        Waiting.NoSleep(ss.fire(resumed ? TRUE : FALSE));
    }

    private static void RegisterWorldArea(area space, int maxx, int maxy, int maxz)
    {
        space.turfs_by_zlevel.len = maxz;
        for (int z = 1; z <= maxz; z++)
            space.turfs_by_zlevel[(float)z] = block(1, 1, z, maxx, maxy, z);
    }

    private static int Listed(area a, int z) => (int)((DmList)a.turfs_by_zlevel[(float)z]).len;

    private static int Loose(area a, int z) => (int)((DmList)a.turfs_to_uncontain_by_zlevel[(float)z]).len;

    // ---- the subsystem itself ----

    [Fact]
    public void TheSubsystemHasTgsNameFlagsAndRunlevelsAndStartsEmpty()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var ss = Dm.@new<datum_controller_subsystem_area_contents>();
            Assert.Equal("Area Contents", ss.name);
            Assert.True(Dm.eq(ss.ss_flags, SS_NO_INIT));
            Assert.True(Dm.eq(ss.runlevels, Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT)));
            Assert.Null(ss.currentrun);
            Assert.Equal(0, ss.marked_for_clearing.len);
            Assert.Same(ss, SSarea_contents);
        }
    }

    [Fact]
    public void EachSubsystemInstanceHasItsOwnMarkedForClearingList()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var first = Dm.@new<datum_controller_subsystem_area_contents>();
            var list = first.marked_for_clearing;
            var second = Dm.@new<datum_controller_subsystem_area_contents>();   // NEW_SS_GLOBAL replaces the first
            Assert.NotSame(list, second.marked_for_clearing);
            Assert.Same(second, SSarea_contents);
            Assert.True(QDELETED(first));
        }
    }

    // ---- scanning ----

    [Fact]
    public void AnAreaWithMoreThanOneHundredLooseTurfsOnOneLevelIsMarkedAndOneWithExactlyOneHundredIsNot()
    {
        var t = new TurfAreaWorld(2, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var x = Dm.@new<area_test_turfarea_common>();
            var y = Dm.@new<area_test_turfarea_common>();
            turf T = locate(1, 1, 1);
            // x gets 101 entries in its uncontain list, y 100 (a turf bouncing between them)
            T.change_area((area)T.loc, x);
            T.change_area(x, y);   // the first move out of x
            for (int i = 0; i < 100; i++)
            {
                T.change_area(y, x);
                T.change_area(x, y);
            }
            Assert.Equal(101, Loose(x, 1));
            Assert.Equal(100, Loose(y, 1));
            var ss = Subsystem();

            // `> ALLOWED_LOOSE_TURFS` (area_contents.dm:34): the scan runs backwards over a copy of GLOB.areas
            FireOneUnit(ss, resumed: false);   // y is scanned first: 100 is not over the limit
            Assert.Equal(0, ss.marked_for_clearing.len);
            FireOneUnit(ss, resumed: true);    // x: 101
            Assert.Equal(new[] { x }, TurfAreaLists.Of<area>(ss.marked_for_clearing));
            FireOneUnit(ss, resumed: true);    // space

            Assert.Equal(new[] { x }, TurfAreaLists.Of<area>(ss.marked_for_clearing));
            Assert.Equal(0, ss.currentrun.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnAreaWithSeveralOverfullLevelsIsMarkedOnce()
    {
        var t = new TurfAreaWorld(1, 1, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var x = Dm.@new<area_test_turfarea_common>();
            var turfs = new[] { locate(1, 1, 1), locate(1, 1, 2) };
            x.turfs_by_zlevel = Dm.list(Dm.list(), Dm.list());
            x.turfs_to_uncontain_by_zlevel = Dm.list(Dm.list(), Dm.list());
            for (int z = 1; z <= 2; z++)
            {
                for (int i = 0; i < ALLOWED_LOOSE_TURFS + 1; i++)
                    ((DmList)x.turfs_to_uncontain_by_zlevel[(float)z]).Add(turfs[z - 1]);
            }
            var ss = Subsystem();

            FireOneUnit(ss, resumed: false);   // x is the last area of GLOB.areas: scanned first, `break` after level 1
            FireOneUnit(ss, resumed: true);    // the world's area
            Assert.Equal(new[] { x }, TurfAreaLists.Of<area>(ss.marked_for_clearing));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnAreaWithOnlyOneOverfullHigherLevelIsMarkedToo()
    {
        var t = new TurfAreaWorld(1, 1, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var x = Dm.@new<area_test_turfarea_common>();
            var turf2 = locate(1, 1, 2);
            x.turfs_by_zlevel = Dm.list(Dm.list(), Dm.list(turf2));
            x.turfs_to_uncontain_by_zlevel = Dm.list(Dm.list(), Dm.list());
            for (int i = 0; i < ALLOWED_LOOSE_TURFS + 1; i++)
                ((DmList)x.turfs_to_uncontain_by_zlevel[2f]).Add(turf2);
            var ss = Subsystem();
            Waiting.NoSleep(ss.fire(FALSE));
            Assert.Equal(0, Listed(x, 2));                         // the entry of level 2 was removed
            Assert.Equal(0, x.turfs_to_uncontain_by_zlevel.len);   // reset to list(): nothing is left
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- clearing, in one call and in many ----

    // 11 x 11 x 2 map; every turf of level 1 moves from space to a room (121 loose entries in space) and five turfs of
    // level 2 (5 loose entries): space is marked, the room is not
    private static (area space, area room) Populate()
    {
        var space = (area)locate(1, 1, 1).loc;
        RegisterWorldArea(space, 11, 11, 2);
        var room = Dm.@new<area_misc_testroom>();
        foreach (turf T in Dm.each<turf>(block(1, 1, 1, 11, 11, 1)))
            T.change_area(space, room);
        foreach (turf T in Dm.each<turf>(block(1, 1, 2, 5, 1, 2)))
            T.change_area(space, room);
        return (space, room);
    }

    [Fact]
    public void OneCallWithAnUnlimitedTickRemovesEveryLooseEntryOfAMarkedAreaOnEveryLevel()
    {
        var t = new TurfAreaWorld(11, 11, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var (space, room) = Populate();
            Assert.Equal(121, Loose(space, 1));
            Assert.Equal(5, Loose(space, 2));
            var ss = Subsystem();

            Waiting.NoSleep(ss.fire(FALSE));

            Assert.Equal(0, Listed(space, 1));
            Assert.Equal(121 - 5, Listed(space, 2));
            Assert.Equal(0, space.turfs_to_uncontain_by_zlevel.len);   // `= list()` (area_contents.dm:67)
            Assert.Equal(0, ss.marked_for_clearing.len);
            Assert.Equal(0, ss.currentrun.len);
            // the room was not marked: its lists are as change_area left them
            Assert.Equal(121, Listed(room, 1));
            Assert.Equal(5, Listed(room, 2));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void WhenTheTickRunsOutAfterEveryUnitTheTrimTakesOneCallPerEntryAndEndsInTheSameState()
    {
        var t = new TurfAreaWorld(11, 11, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var (space, room) = Populate();
            var ss = Subsystem();

            // calls 1 and 2: the scan, room first (the scan runs from the end of GLOB.areas), then space
            FireOneUnit(ss, resumed: false);
            Assert.Equal(1, ss.currentrun.len);
            Assert.Equal(0, ss.marked_for_clearing.len);
            FireOneUnit(ss, resumed: true);
            Assert.Equal(0, ss.currentrun.len);
            Assert.Equal(new[] { space }, TurfAreaLists.Of<area>(ss.marked_for_clearing));

            // calls 3...: one removal each (area_contents.dm:53-62), the removed entry is cut off the loose list
            FireOneUnit(ss, resumed: true);
            Assert.Equal(120, Listed(space, 1));
            Assert.Equal(120, Loose(space, 1));
            FireOneUnit(ss, resumed: true);
            Assert.Equal(119, Listed(space, 1));
            Assert.Equal(119, Loose(space, 1));
            for (int i = 0; i < 119; i++)
                FireOneUnit(ss, resumed: true);
            Assert.Equal(0, Listed(space, 1));
            Assert.Equal(0, Loose(space, 1));
            Assert.Equal(5, Loose(space, 2));   // level 2 is untouched until level 1 is done

            for (int i = 0; i < 5; i++)
                FireOneUnit(ss, resumed: true);
            Assert.Equal(116, Listed(space, 2));
            Assert.Equal(1, ss.marked_for_clearing.len);   // the area is only released by a call that has nothing left to cut

            FireOneUnit(ss, resumed: true);
            Assert.Equal(0, ss.marked_for_clearing.len);
            Assert.Equal(0, space.turfs_to_uncontain_by_zlevel.len);
            Assert.Equal(0, Listed(space, 1));
            Assert.Equal(116, Listed(space, 2));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheEntriesAreRemovedInTheOrderOfTheLooseListAndOnlyOneOccurrenceEach()
    {
        var t = new TurfAreaWorld(3, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var x = Dm.@new<area_test_turfarea_common>();
            turf a = locate(1, 1, 1), b = locate(2, 1, 1), c = locate(3, 1, 1);
            // x lists a twice (once re-appended) and the loose list names a twice: both go, the others stay
            x.turfs_by_zlevel = Dm.list(Dm.list(a, b, a, c));
            var loose = Dm.list();
            for (int i = 0; i < ALLOWED_LOOSE_TURFS + 1; i++)
                loose.Add(i == 1 ? b : a);
            x.turfs_to_uncontain_by_zlevel = Dm.list(loose);
            var ss = Subsystem();
            ss.marked_for_clearing = Dm.list(x);
            ss.currentrun = Dm.list();

            // the removal list has 101 items: a, b and 99 more a's, but x lists a only twice: surplus removals do nothing
            Waiting.NoSleep(ss.fire(TRUE));

            Assert.Equal(new[] { c }, TurfAreaLists.Of<turf>(x.turfs_by_zlevel[1f]));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ALevelBeyondTheAreasListsIsAStackTraceAndItsLooseListIsReset()
    {
        var t = new TurfAreaWorld(1, 1, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var x = Dm.@new<area_test_turfarea_common>();
            x.turfs_by_zlevel = Dm.list(Dm.list());   // one level only
            x.turfs_to_uncontain_by_zlevel = Dm.list(Dm.list(), Dm.list());
            var stray = locate(1, 1, 2);
            for (int i = 0; i < ALLOWED_LOOSE_TURFS + 1; i++)
                ((DmList)x.turfs_to_uncontain_by_zlevel[2f]).Add(stray);
            var ss = Subsystem();

            Waiting.NoSleep(ss.fire(FALSE));

            // area_contents.dm:49-53: the runtime names the area and the lengths, the loose list is dropped
            Assert.Single(t.Runtimes);
            Assert.Contains("turfs_by_zlevel is length 1 but we are being asked to remove turfs from zlevel 2", t.Runtimes[0].Message);
            Assert.Equal(0, x.turfs_to_uncontain_by_zlevel.len);
            Assert.Equal(0, ss.marked_for_clearing.len);
        }
    }

    [Fact]
    public void AnAreaDestroyedBetweenTheScanAndTheClearIsHarmless()
    {
        var t = new TurfAreaWorld(11, 11, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var (space, room) = Populate();
            var ss = Subsystem();
            FireOneUnit(ss, resumed: false);   // scans room
            qdel(room);                          // room.turfs_to_uncontain_by_zlevel is null from here on
            FireOneUnit(ss, resumed: true);    // scans space
            Assert.Equal(new[] { space }, TurfAreaLists.Of<area>(ss.marked_for_clearing));
            ss.state = SS_RUNNING;
            Waiting.NoSleep(ss.fire(TRUE));
            Assert.Equal(0, Listed(space, 1));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ANonResumedCallRestartsTheScanFromAFreshCopyOfGLOBAreasAndKeepsWhatWasMarked()
    {
        var t = new TurfAreaWorld(11, 11, 2, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var (space, room) = Populate();
            var ss = Subsystem();
            FireOneUnit(ss, resumed: false);
            FireOneUnit(ss, resumed: true);
            Assert.Equal(1, ss.marked_for_clearing.len);
            var first_run = ss.currentrun;

            FireOneUnit(ss, resumed: false);   // fire(FALSE): `currentrun = GLOB.areas.Copy()` (area_contents.dm:30-31)

            Assert.NotSame(first_run, ss.currentrun);
            Assert.Equal(1, ss.currentrun.len);   // room scanned again
            Assert.Equal(1, ss.marked_for_clearing.len);
        }
    }

    [Fact]
    public void WithNothingLooseTheSubsystemOnlyScans()
    {
        var t = new TurfAreaWorld(2, 2, 1);
        using (t.World.Enter())
        {
            var ss = Subsystem();
            Waiting.NoSleep(ss.fire(FALSE));
            Assert.Equal(0, ss.currentrun.len);
            Assert.Equal(0, ss.marked_for_clearing.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheClearOfAMarkedAreaTakesTheLastMarkedAreaFirst()
    {
        var t = new TurfAreaWorld(2, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var first = Dm.@new<area_test_turfarea_common>();
            var second = Dm.@new<area_test_turfarea_common>();
            turf T = locate(1, 1, 1);
            foreach (var x in new[] { first, second })
            {
                x.turfs_by_zlevel = Dm.list(Dm.list(T));
                var loose = Dm.list();
                for (int i = 0; i < ALLOWED_LOOSE_TURFS + 1; i++)
                    loose.Add(T);
                x.turfs_to_uncontain_by_zlevel = Dm.list(loose);
            }
            var ss = Subsystem();
            ss.currentrun = Dm.list();
            ss.marked_for_clearing = Dm.list(first, second);

            FireOneUnit(ss, resumed: true);   // one removal: the last marked area (area_contents.dm:42)

            Assert.Equal(0, Listed(second, 1));
            Assert.Equal(1, Listed(first, 1));
            Assert.Equal(100, Loose(second, 1));
            Assert.Equal(101, Loose(first, 1));
        }
    }
}
