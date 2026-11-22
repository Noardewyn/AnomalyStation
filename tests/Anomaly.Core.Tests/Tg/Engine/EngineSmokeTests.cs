using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Engine;

// Fixtures of the engine package's smoke tests (rules 15: port fixtures have test_<package>_<name> paths). Their New
// does not call ..(): the engine is exercised without tg's /atom/New (SSatoms), whatever the atoms package lands.

[DmPath("/turf/test_engine_smoke_floor")]
public class turf_test_engine_smoke_floor : turf
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

[DmPath("/turf/test_engine_smoke_wall")]
public class turf_test_engine_smoke_wall : turf
{
    public turf_test_engine_smoke_wall() { name = "wall"; }

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

[DmPath("/area/test_engine_smoke_room")]
public class area_test_engine_smoke_room : area
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

[DmPath("/obj/test_engine_smoke_box")]
public class obj_test_engine_smoke_box : obj
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

[DmPath("/mob/test_engine_smoke_mob")]
public class mob_test_engine_smoke_mob : mob
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>
/// Smoke tests of the engine package (wave 3, F): the atom engine parts, the map grid and the map builtins compile and
/// run on the main paths. The behaviour tests are the independent test-writer's.
/// </summary>
public class EngineSmokeTests
{
    // A world whose map is maxx x maxy x maxz of smoke floors in one smoke room.
    private static World MapWorld(int maxx, int maxy, int maxz, List<Exception> errors = null, ulong seed = 7)
    {
        var w = new World(seed) { RuntimeErrorSink = e => errors?.Add(e) };
        MapGrid.Attach(w);
        using (w.Enter())
        {
            Dm.world.turf = typeof(turf_test_engine_smoke_floor);
            Dm.world.area = typeof(area_test_engine_smoke_room);
            Dm.world.maxx = maxx;
            Dm.world.maxy = maxy;
            Dm.world.maxz = maxz;
        }
        return w;
    }

    [Fact]
    public void GrowingTheMapFillsItWithWorldTurfInWorldAreaXFastest()
    {
        World w = MapWorld(3, 2, 1);
        using (w.Enter())
        {
            turf first = locate(1, 1, 1);
            Assert.IsType<turf_test_engine_smoke_floor>(first);
            var room = (area)first.loc;
            Assert.IsType<area_test_engine_smoke_room>(room);
            Assert.Equal(6, room.contents.Count);
            var order = new List<(float, float)>();
            foreach (turf t in Dm.each<turf>(room)) order.Add((t.x, t.y));
            Assert.Equal(new[] { (1f, 1f), (2f, 1f), (3f, 1f), (1f, 2f), (2f, 2f), (3f, 2f) }, order);
            Assert.True(locate(1, 1, 1).dm_id < locate(2, 1, 1).dm_id);

            Dm.world.maxx = 4; // the new column, y by y
            Assert.Equal(8, room.contents.Count);
            Assert.Same(locate(4, 1, 1), room.contents[7f]);
            Assert.Same(locate(4, 2, 1), room.contents[8f]);
            Assert.Same(first, locate(1, 1, 1));

            Assert.Null(locate(0, 1, 1));
            Assert.Null(locate(5, 1, 1));
            Assert.Null(locate(1, 1, 2));
            Assert.Same(locate(2, 2, 1), locate(2.7f, 2.2f, 1));
            Assert.Equal(1f, room.x); // an area's coordinates are its lowest z, y, x turf's
            Assert.Equal(1f, room.y);
            Assert.Equal(1f, room.z);
        }
    }

    [Fact]
    public void ShrinkingTheMapIsARuntimeAndKeepsTheSize()
    {
        World w = MapWorld(3, 3, 1);
        using (w.Enter())
        {
            Assert.Throws<DmRuntimeException>(() => Dm.world.maxx = 2);
            Assert.Equal(3f, Dm.world.maxx);
            Assert.NotNull(locate(3, 3, 1));
        }
    }

    [Fact]
    public void LocIsSilentAppendsAndRefusesCycles()
    {
        World w = MapWorld(3, 3, 1);
        using (w.Enter())
        {
            turf t = locate(2, 2, 1);
            var a = Dm.@new<obj_test_engine_smoke_box>(t);
            var b = Dm.@new<obj_test_engine_smoke_box>(t);
            var m = Dm.@new<mob_test_engine_smoke_mob>();
            Assert.Same(t, a.loc);
            Assert.Null(m.loc);
            Assert.Equal(0f, m.x);
            m.loc = t;
            Assert.Equal(new object[] { a, b, m }, Dm.each(t).Cast<object>().ToArray());
            Assert.Equal(2f, m.x);

            a.loc = b; // inside b: x, y and z are 0, get_turf is the outer turf
            Assert.Equal(new object[] { b, m }, Dm.each(t).Cast<object>().ToArray());
            Assert.Equal(0f, a.x);
            Assert.Same(t, get_turf(a));
            Assert.Same(t.loc, get_area(a));

            b.loc = a; // cyclic: refused, nothing changes
            Assert.Same(t, b.loc);
            b.loc = b;
            Assert.Same(t, b.loc);

            a.loc = null;
            Assert.Null(a.loc);
            Assert.Equal(0f, a.z);
            Assert.Equal(0, b.contents.Count);
        }
    }

    [Fact]
    public void ContentsOperatorsMoveAtoms()
    {
        World w = MapWorld(3, 3, 1);
        using (w.Enter())
        {
            turf here = locate(1, 1, 1);
            turf there = locate(3, 3, 1);
            var box = Dm.@new<obj_test_engine_smoke_box>(here);
            var crate = Dm.@new<obj_test_engine_smoke_box>(here);

            Dm.append(there.contents, box); // contents += X is X.loc = src
            Assert.Same(there, box.loc);
            Assert.Equal(new object[] { crate }, Dm.each(here).Cast<object>().ToArray());

            Dm.append(crate.contents, crate); // cyclic: dropped
            Assert.Equal(0, crate.contents.Count);
            Assert.Same(here, crate.loc);

            Dm.remove(there.contents, box); // contents -= X is X.loc = null
            Assert.Null(box.loc);

            here.contents = Dm.list(box, crate); // assigning a list makes it the contents
            Assert.Equal(new object[] { box, crate }, Dm.each(here).Cast<object>().ToArray());
            here.contents = null;
            Assert.Null(box.loc);
            Assert.Null(crate.loc);

            var room = (area)there.loc;
            var other = Dm.@new<area_test_engine_smoke_room>();
            Dm.append(other.contents, there); // area.contents += turf moves the turf to that area
            Assert.Same(other, there.loc);
            Assert.False(Dm.@in(there, room));
            Assert.Equal(3f, other.x);
        }
    }

    [Fact]
    public void TheMapBuiltinsFollowTheGrid()
    {
        World w = MapWorld(3, 3, 2);
        using (w.Enter())
        {
            turf middle = locate(2, 2, 1);
            var box = Dm.@new<obj_test_engine_smoke_box>(middle);
            var inner = Dm.@new<obj_test_engine_smoke_box>(box);

            Assert.Same(middle, get_step(inner, 0));
            Assert.Same(locate(2, 3, 1), get_step(inner, Dm.NORTH));
            Assert.Same(locate(3, 1, 1), get_step(middle, Dm.SOUTHEAST));
            Assert.Same(locate(2, 2, 2), get_step(middle, Dm.UP));
            Assert.Null(get_step(locate(3, 3, 1), Dm.EAST)); // the map edge
            Assert.Null(get_step(Dm.@new<obj_test_engine_smoke_box>(), 0)); // nullspace

            Assert.Equal(Dm.NORTHEAST, get_dir(locate(1, 1, 1), locate(3, 2, 1)));
            Assert.Equal(Dm.WEST, get_dir(inner, locate(1, 2, 1)));
            Assert.Equal(0f, get_dir(middle, inner));
            Assert.Equal(-1f, get_dist(box, box));
            Assert.Equal(0f, get_dist(box, inner));
            Assert.Equal(2f, get_dist(locate(1, 1, 1), locate(3, 2, 2)));
            Assert.Equal(float.PositiveInfinity, get_dist(box, Dm.@new<obj_test_engine_smoke_box>()));

            DmList cells = block(locate(3, 2, 1), locate(2, 3, 1));
            Assert.Equal(new object[] { locate(2, 2, 1), locate(3, 2, 1), locate(2, 3, 1), locate(3, 3, 1) }, Dm.each(cells).Cast<object>().ToArray());
            Assert.Equal(6, block(1, 1, 1, 3, 1, 2).Count);
            Assert.Equal(9, block(0, 0, 2, 9, 9).Count); // cut to the map

            Assert.True(isturf(middle) && isarea(middle.loc) && ismovable(box) && isloc(box) && !ismob(box));
        }
    }

    [Fact]
    public void ANewTurfReplacesTheTurfAtItsLocation()
    {
        World w = MapWorld(2, 2, 1);
        using (w.Enter())
        {
            turf old = locate(2, 1, 1);
            var room = (area)old.loc;
            var box = Dm.@new<obj_test_engine_smoke_box>(old);
            var mob = Dm.@new<mob_test_engine_smoke_mob>(old);

            var wall = Dm.@new<turf_test_engine_smoke_wall>(old);
            Assert.Same(wall, locate(2, 1, 1));
            Assert.Equal(2f, wall.x);
            Assert.Same(room, wall.loc);
            Assert.Same(wall, room.contents[2f]); // the old turf's place in the area
            Assert.Equal(new object[] { box, mob }, Dm.each(wall).Cast<object>().ToArray());
            Assert.Null(old.loc);
            Assert.Equal(0f, old.x);
            Assert.False(w.Objects.TryGet(old.dm_id, out _));
            Assert.NotNull(old.gc_destroyed);
            Assert.Equal("wall", wall.name);
        }
    }

    [Fact]
    public void BuiltinDefaultsAndNames()
    {
        Assert.Equal(2f, new turf_test_engine_smoke_floor().layer);
        Assert.Equal("test engine smoke floor", new turf_test_engine_smoke_floor().name);
        Assert.Equal(1f, new area_test_engine_smoke_room().layer);
        Assert.Equal(1f, new area_test_engine_smoke_room().luminosity);
        Assert.Equal(3f, new obj_test_engine_smoke_box().layer);
        var m = new mob_test_engine_smoke_mob();
        Assert.Equal(4f, m.layer);
        Assert.Equal(1f, m.density);
        Assert.Equal(1e6f, m.see_in_dark); // tg's /mob override (mob_defines.dm:22), rules 10.2 "Builtin types" (W4.0)
        Assert.Equal(32f, m.bound_width);
        Assert.Equal(Dm.SLIDE_STEPS, m.animate_movement); // tg's /mob override (mob_defines.dm:13)
        Assert.Equal("/client", DmTypes.PathOf(typeof(client)));
    }

    [Fact]
    public void AppearanceVarsOfRegisteredAtomsNotifyTheSink()
    {
        World w = MapWorld(1, 1, 1);
        using (w.Enter())
        {
            RecordingSinks sinks = RecordingSinks.Install(w);
            var box = Dm.@new<obj_test_engine_smoke_box>(locate(1, 1, 1));
            box.dir = Dm.NORTH;
            box.icon_state = "open";
            box.opacity = 1;
            box.overlays = null;
            box.glide_size = 4;
            Assert.Equal(5, sinks.Lines.Count);
            Assert.Contains("Dir", sinks.Lines[0]);
            Assert.Contains("visibility", sinks.Lines[2]);
            Assert.Contains("Glide", sinks.Lines[4]);
        }
        var prototype = new obj_test_engine_smoke_box();
        prototype.dir = Dm.EAST; // unregistered: stored only
        Assert.Equal(Dm.EAST, prototype.dir);
    }

    [Fact]
    public void TwoIdenticalBuildsHashAlike()
    {
        static ulong Build()
        {
            World w = MapWorld(3, 2, 1, seed: 11);
            using (w.Enter())
            {
                var box = Dm.@new<obj_test_engine_smoke_box>(locate(2, 2, 1));
                Dm.@new<obj_test_engine_smoke_box>(box);
                Dm.append(locate(1, 1, 1).contents, box);
                Dm.@new<turf_test_engine_smoke_wall>(locate(3, 1, 1));
            }
            return w.ComputeStateHash();
        }
        Assert.Equal(Build(), Build());
    }
}
