using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Wave3.Engine.EngineTestHelpers;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Engine;

/// <summary>
/// <c>new T(loc)</c> for atoms: the silent placement before New (object-model.md section 4 step 5.2; move-chain.md E1),
/// a new area at a turf, and a new turf at a turf, which replaces it (BYOND ref /proc/new: "Newly created areas or turfs
/// replace any existing area or turf at the specified location"; scope R16).
/// </summary>
public class CreationTests
{
    [Fact]
    public void NewPlacesTheAtomBeforeNewRuns()
    {
        var t = new EngineWorld(3, 3, 1);
        using (t.World.Enter())
        {
            turf cell = locate(3, 2, 1);
            var resident = Dm.@new<obj_test_engine_box>(cell);
            t.Trace.Clear();

            // BYOND ref New proc (atom): "By the time New() is called, the object has already been created at the
            // specified location and all of its variables have been initialized" (particle_holder.dm:28 reads loc in New).
            var box = Dm.@new<obj_test_engine_box>(cell);
            var inner = Dm.@new<obj_test_engine_box>(box);
            var mob = Dm.@new<mob_test_engine_mob>(cell);
            Assert.Equal(new[]
            {
                "New " + EngineTrace.D(box) + " loc=" + EngineTrace.D(cell) + " xyz=3,2,1 listed=yes",
                "New " + EngineTrace.D(inner) + " loc=" + EngineTrace.D(box) + " xyz=0,0,0 listed=yes",
                "New " + EngineTrace.D(mob) + " loc=" + EngineTrace.D(cell) + " xyz=3,2,1",
            }, t.Trace);
            Assert.Equal(new object[] { resident, box, mob }, Items(cell)); // appended in creation order
        }
    }

    [Fact]
    public void CreationAtALocationFiresNoEnterEnteredCrossOrSignal()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            turf cell = locate(1, 1, 1);
            var resident = Dm.@new<obj_test_engine_box>(cell);
            var listeners = new List<datum_test_engine_listener>();
            foreach (string signal in new[] { Defines.COMSIG_ATOM_ENTERED, Defines.COMSIG_ATOM_ABSTRACT_ENTERED, Defines.COMSIG_ATOM_EXIT })
            {
                var l = Dm.@new<datum_test_engine_listener>();
                l.label = signal;
                l.RegisterSignal(cell, signal, nameof(datum_test_engine_listener.on_signal));
                l.RegisterSignal(resident, signal, nameof(datum_test_engine_listener.on_signal));
            }
            var area_listener = Dm.@new<datum_test_engine_listener>();
            area_listener.label = Defines.COMSIG_AREA_ENTERED;
            area_listener.RegisterSignal((area)cell.loc, Defines.COMSIG_AREA_ENTERED, nameof(datum_test_engine_listener.on_signal));
            t.Trace.Clear();

            // BYOND ref Entered proc: "The same goes for creation or deletion of an object at a location." tg treats new
            // as firing no Entered and no COMSIG_ATOM_ENTERED (effects_sparks.dm:87-95; aquarium_kit.dm:43 creates in
            // null and then forceMoves "to trigger handle_entered").
            var box = Dm.@new<obj_test_engine_box>(cell);
            var inner = Dm.@new<obj_test_engine_box>(resident);
            var mob = Dm.@new<mob_test_engine_mob>(cell);
            Assert.Equal(new[]
            {
                "New " + EngineTrace.D(box) + " loc=" + EngineTrace.D(cell) + " xyz=1,1,1 listed=yes",
                "New " + EngineTrace.D(inner) + " loc=" + EngineTrace.D(resident) + " xyz=0,0,0 listed=yes",
                "New " + EngineTrace.D(mob) + " loc=" + EngineTrace.D(cell) + " xyz=1,1,1",
            }, t.Trace); // only the New lines: no Enter, Cross, Crossed, signal
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheInitialLocationIsTheFirstPositionalArgumentElseTheOneNamedLoc()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            turf a = locate(1, 1, 1);
            turf b = locate(2, 1, 1);
            // BYOND ref New proc (atom): "the initial location will be taken from the first positional argument, or from
            // the argument named loc if there are no positional arguments."
            var named = Dm.@new<obj_test_engine_box>(Dm.named("loc", b));
            Assert.Same(b, named.loc);
            var both = Dm.@new<obj_test_engine_box>(a, Dm.named("loc", b));
            Assert.Same(a, both.loc);
            Assert.Equal(new object[] { named }, Items(b));
        }
    }

    [Fact]
    public void ANonAtomLocationLeavesTheNewAtomInNullspace()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            // "The container is always an atom (or null)" (BYOND ref loc var).
            Assert.Null(Dm.@new<obj_test_engine_box>("somewhere").loc);
            Assert.Null(Dm.@new<obj_test_engine_box>(Dm.@new<datum>()).loc);
            Assert.Null(Dm.@new<obj_test_engine_box>(Dm.list(locate(1, 1, 1))).loc);
            Assert.Null(Dm.@new<obj_test_engine_box>(5).loc);
            Assert.Empty(Items(locate(1, 1, 1)));
        }
    }

    [Fact]
    public void ANewMovableCreatedInAMappedAreaLandsOnItsFirstTurf()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            // BYOND (OpenDream's SetLoc): a movable given an area on the map is put on the area's first turf; tg's supply pods
            // wait in CentCom's holding area that way (supplypod.dm:160). An area off the map stays a room (LocContractTests).
            area room = (area)locate(1, 1, 1).loc;
            var box = Dm.@new<obj_test_engine_box>(room);
            Assert.Same(locate(1, 1, 1), box.loc);
            Assert.Equal(new object[] { locate(1, 1, 1) }, Items(room));
        }
    }

    [Fact]
    public void ANewAreaAtATurfTakesThatTurf()
    {
        var t = new EngineWorld(3, 1, 1);
        using (t.World.Enter())
        {
            turf cell = locate(2, 1, 1);
            area room = (area)cell.loc;
            var box = Dm.@new<obj_test_engine_box>(cell);
            t.Trace.Clear();

            var hall = Dm.@new<area_test_engine_hall>(cell);
            // The area is never inside anything; the turf is already its own when New runs.
            Assert.Equal(new[] { "New " + EngineTrace.D(hall) + " loc=null contents=1" }, t.Trace);
            Assert.Null(hall.loc);
            Assert.Same(hall, cell.loc);
            Assert.Equal(new object[] { cell }, Items(hall));
            Assert.Equal(new object[] { locate(1, 1, 1), locate(3, 1, 1) }, Items(room));
            Assert.Equal(new[] { 2f, 1f, 1f }, new[] { hall.x, hall.y, hall.z });
            Assert.Same(hall, get_area(box));

            var empty_room = Dm.@new<area_test_engine_hall>(); // "explicitly creating them with null as the initial location"
            Assert.Null(empty_room.loc);
            Assert.Empty(Items(empty_room));
        }
    }

    [Fact]
    public void ANewTurfAtATurfReplacesItInItsCellAndTakesItsContentsInOrder()
    {
        var t = new EngineWorld(3, 2, 1);
        using (t.World.Enter())
        {
            turf old = locate(2, 1, 1);
            area room = (area)old.loc;
            var box = Dm.@new<obj_test_engine_box>(old);
            var mob = Dm.@new<mob_test_engine_mob>(old);
            var held = Dm.@new<obj_test_engine_box>(mob);
            t.Trace.Clear();

            // BYOND ref /turf: "When you create a new turf with new(), it always replaces the old one":
            // var/turf/wall/T = new(old_turf). The movables stay at the location, now on the new turf.
            var wall = Dm.@new<turf_test_engine_wall>(old);
            Assert.Equal(new[]
            {
                "New " + EngineTrace.D(wall) + " loc=" + EngineTrace.D(room) + " xyz=2,1,1 first=" + EngineTrace.D(box) + " contents=2",
            }, t.Trace); // in its cell, in the area, with the movables, before New
            Assert.Same(wall, locate(2, 1, 1));
            Assert.Same(room, wall.loc);
            Assert.Same(wall, room.contents[2f]); // the old turf's place among the area's turfs
            Assert.Equal(6, room.contents.Count);
            Assert.Equal(new object[] { box, mob }, Items(wall));
            Assert.Same(wall, box.loc);
            Assert.Same(mob, held.loc);
            Assert.Same(wall, get_turf(held));
            Assert.Equal(new[] { 2f, 1f, 1f }, new[] { mob.x, mob.y, mob.z });
            Assert.Same(wall, get_step(locate(1, 1, 1), Dm.EAST));
            Assert.Contains(wall, Items(block(1, 1, 1, 3, 2, 1)));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void DV7_AReplacedTurfLeavesTheMapAndTheWorldMarkedDeleted()
    {
        var t = new EngineWorld(2, 2, 1);
        using (t.World.Enter())
        {
            turf old = locate(1, 2, 1);
            var box = Dm.@new<obj_test_engine_box>(old);
            t.World.Step(4); // world.time 2
            var wall = Dm.@new<turf_test_engine_wall>(old);

            // Port (R16; the turf identity across ChangeTurf is the M3 decision): the old object is off the map, holds
            // nothing, and is unregistered and marked deleted as qdel would mark it (gc_destroyed = world.time).
            Assert.Equal("loc=null xyz=0,0,0", EngineTrace.Where(old));
            Assert.Empty(Items(old));
            Assert.False(t.World.Objects.TryGet(old.dm_id, out _));
            Assert.True(Dm.eq(old.gc_destroyed, 2f));
            Assert.DoesNotContain(old, Dm.each<turf>(Dm.world).Cast<turf>());
            Assert.Equal(4, Dm.each<turf>(Dm.world).Count());
            Assert.Null(get_turf(old));
            Assert.Equal(float.PositiveInfinity, get_dist(old, wall));
            Assert.Same(wall, box.loc);
        }
    }

    [Fact]
    public void DV2_ATurfReplacedAtWorldTimeZeroReadsAsDeleted()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            turf old = locate(1, 1, 1);
            Assert.False(QDELETED(old));
            Dm.@new<turf_test_engine_wall>(old);
            // gc_destroyed = world.time = 0: tg's QDELETED would read it as alive; DV2 tests gc_destroyed != null.
            Assert.True(Dm.eq(old.gc_destroyed, 0));
            Assert.True(QDELETED(old));
            Assert.False(QDELETED(locate(1, 1, 1)));
        }
    }

    [Fact]
    public void ReplacingEveryTurfOfAnAreaWhileIteratingItKeepsTheAreaInOrder()
    {
        var t = new EngineWorld(3, 2, 1);
        using (t.World.Enter())
        {
            area room = (area)locate(1, 1, 1).loc;
            var replaced = new List<turf>();
            // The toy builder's pattern (scope R16): `for(var/turf/T in area) new /turf/wall(T)`; the loop walks a snapshot.
            foreach (turf T in Dm.each<turf>(room))
                replaced.Add(Dm.@new<turf_test_engine_wall>(T));
            Assert.Equal(6, replaced.Count);
            Assert.Equal(replaced.Cast<object>().ToArray(), Items(room));
            Assert.All(Items(block(1, 1, 1, 3, 2, 1)), c => Assert.IsType<turf_test_engine_wall>(c));
            Assert.Same(replaced[4], locate(2, 2, 1));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TgQuirk_ChangeTurfQdelsTheOldTurfFirstAndTheReplacementKeepsThatDeletionTime()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            turf old = locate(2, 1, 1);
            var box = Dm.@new<obj_test_engine_box>(old);
            // ChangeTurf (turfs/change_turf.dm:105-111) runs `qdel(src)` (/turf/Destroy returns QDEL_HINT_IWILLGC,
            // turf.dm:207: qdel sets gc_destroyed = world.time and, DV7, unregisters it) and then `new path(src, ...)`:
            // the old turf, already deleted, is still in its cell when the new one replaces it, and the replacement keeps
            // that deletion time.
            t.World.Step(3);
            old.gc_destroyed = t.World.Time;
            t.World.Objects.Unregister(old.dm_id);
            t.World.Step(10);
            var wall = Dm.@new<turf_test_engine_wall>(old);
            Assert.True(Dm.eq(old.gc_destroyed, 1.5f));
            Assert.Same(wall, locate(2, 1, 1));
            Assert.Equal(new[] { 2f, 1f, 1f }, new[] { wall.x, wall.y, wall.z });
            Assert.Equal(new object[] { box }, Items(wall));
            Assert.Same(locate(1, 1, 1).loc, wall.loc);
            Assert.Equal(2, Dm.each<turf>(Dm.world).Count());
        }
    }

    [Fact]
    public void ReplacingTwiceKeepsTheCellAndTheAreaPlace()
    {
        var t = new EngineWorld(3, 1, 1);
        using (t.World.Enter())
        {
            area room = (area)locate(1, 1, 1).loc;
            var box = Dm.@new<obj_test_engine_box>(locate(3, 1, 1));
            var first = Dm.@new<turf_test_engine_wall>(locate(3, 1, 1));
            var second = Dm.@new<turf_test_engine_floor>(first);
            Assert.Same(second, locate(3, 1, 1));
            Assert.Equal(new object[] { locate(1, 1, 1), locate(2, 1, 1), second }, Items(room));
            Assert.Equal(new object[] { box }, Items(second));
            Assert.Equal("loc=null xyz=0,0,0", EngineTrace.Where(first));

            var stale = Dm.@new<turf_test_engine_wall>(first); // `first` is no longer on the map: nothing is replaced
            Assert.Same(second, locate(3, 1, 1));
            Assert.Equal("loc=null xyz=0,0,0", EngineTrace.Where(stale));
        }
    }

    [Fact]
    public void KnownGapM3_AReferenceToTheReplacedTurfNoLongerNamesTheCell()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            turf remembered = locate(2, 1, 1);
            Dm.@new<turf_test_engine_wall>(remembered);
            // BYOND: a turf reference names its cell, so `remembered` would now be the wall (x = 2, density 1). The port
            // gives a new object (ChangeTurf identity is the M3 decision, scope R16): the old reference is a dead turf.
            Assert.NotSame(remembered, locate(2, 1, 1));
            Assert.Equal(0f, remembered.x);
            Assert.Equal(0f, remembered.density);
        }
    }

    [Fact]
    public void ANewTurfWithoutATurfLocationStaysOffTheMap()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            // RULE-GAP of the engine package (BYOND does not say): new /turf/T(null or a non-turf) is not on the map.
            var loose = Dm.@new<turf_test_engine_wall>();
            Assert.Equal("loc=null xyz=0,0,0", EngineTrace.Where(loose));
            var in_box = Dm.@new<turf_test_engine_wall>(Dm.@new<obj_test_engine_box>(locate(1, 1, 1)));
            Assert.Equal("loc=null xyz=0,0,0", EngineTrace.Where(in_box));
            Assert.IsType<turf_test_engine_floor>(locate(1, 1, 1));
            Assert.Null(get_turf(loose));
        }
    }
}
