using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tg.Wave3.Engine;

// Fixtures of the engine package's behaviour tests (wave 3, F), written by the independent test-writer from the BYOND
// reference and tg's use of the builtins (docs/ai/specs/move-chain.md E1-E5, docs/ai/specs/object-model.md sections 2 and 4,
// the wave-3 scope's R4, R5, R16). Fixture paths are /<kind>/test_engine_<name> (translation-rules.md 15), which no tg
// path starts with. Their New does not call ..(): the engine is exercised without tg's /atom/New (SSatoms, the atoms
// package), so these tests keep their meaning once that package lands. What the fixtures record goes to a per-world
// trace (EngineTrace), never to a static, so parallel test classes do not share it.

/// <summary>The per-world recording harness of the engine tests (translation-rules.md 15): lines in call order.</summary>
internal sealed class EngineTrace
{
    public readonly List<string> Lines = new();

    public static EngineTrace Of(World world) => world.GetExtension(static _ => new EngineTrace());

    /// <summary>Appends to the trace of the ambient world (nothing outside a world scope).</summary>
    public static void Add(string line)
    {
        World world = World.CurrentOrNull;
        if (world != null) Of(world).Lines.Add(line);
    }

    /// <summary>An object as the trace writes it: C# type and world id (RecordingSinks' form), null as "null".</summary>
    public static string D(object value) => RecordingSinks.Describe(value);

    /// <summary>Where an atom is, read through the engine as tg code would: loc, x, y, z.</summary>
    public static string Where(atom a) => "loc=" + D(a.loc) + " xyz=" + D(a.x) + "," + D(a.y) + "," + D(a.z);
}

/// <summary>A map of maxx x maxy x maxz built the way BYOND builds one: world.turf and world.area set, then the size grown.</summary>
internal sealed class EngineWorld
{
    public readonly World World;
    public readonly List<Exception> Runtimes = new();

    public EngineWorld(int maxx, int maxy, int maxz, Type turfType = null, Type areaType = null, ulong seed = 7)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        // What the bootstrap does right after creating the world (MapGrid.Attach's contract), so every turf is created
        // at the assignment that makes its cell, as in BYOND.
        MapGrid.Attach(World);
        using (World.Enter())
        {
            Dm.world.turf = turfType ?? typeof(turf_test_engine_floor);
            Dm.world.area = areaType ?? typeof(area_test_engine_room);
            Dm.world.maxx = maxx;
            Dm.world.maxy = maxy;
            Dm.world.maxz = maxz;
        }
    }

    public List<string> Trace => EngineTrace.Of(World).Lines;
}

/// <summary>The default floor: New records where the turf already is (BYOND creates it at its cell before New).</summary>
[DmPath("/turf/test_engine_floor")]
public class turf_test_engine_floor : turf
{
    public override DmTask New(params object[] __extra)
    {
        EngineTrace.Add("New " + EngineTrace.D(this) + " " + EngineTrace.Where(this) + " contents=" + contents.Count);
        return DmTask.CompletedTask;
    }

    // BYOND's builtin Enter: recorded, so a test can show that loc, contents and new never call it (move-chain.md E1).
    public override async DmTask<object> Enter(atom_movable O = null, atom oldloc = null)
    {
        EngineTrace.Add("Enter " + EngineTrace.D(this) + " " + EngineTrace.D(O));
        return await base.Enter(O, oldloc);
    }
}

/// <summary>A dense turf with an explicit name, for replacement and Enter tests.</summary>
[DmPath("/turf/test_engine_wall")]
public class turf_test_engine_wall : turf
{
    public turf_test_engine_wall()
    {
        density = Dm.TRUE;
        name = "wall";
    }

    public override DmTask New(params object[] __extra)
    {
        atom first = contents.Count > 0 ? contents[1f] as atom : null;
        EngineTrace.Add("New " + EngineTrace.D(this) + " " + EngineTrace.Where(this) + " first=" + EngineTrace.D(first) + " contents=" + contents.Count);
        return DmTask.CompletedTask;
    }
}

/// <summary>A turf whose New is a runtime at x == 2 (the map keeps filling, an engine isolation boundary).</summary>
[DmPath("/turf/test_engine_faulty")]
public class turf_test_engine_faulty : turf
{
    public override DmTask New(params object[] __extra)
    {
        if (x == 2) throw new DmRuntimeException("faulty turf at x 2");
        return DmTask.CompletedTask;
    }
}

[DmPath("/area/test_engine_room")]
public class area_test_engine_room : area
{
    public override DmTask New(params object[] __extra)
    {
        EngineTrace.Add("New " + EngineTrace.D(this) + " loc=" + EngineTrace.D(loc) + " contents=" + contents.Count);
        return DmTask.CompletedTask;
    }
}

[DmPath("/area/test_engine_hall")]
public class area_test_engine_hall : area
{
    public override DmTask New(params object[] __extra)
    {
        EngineTrace.Add("New " + EngineTrace.D(this) + " loc=" + EngineTrace.D(loc) + " contents=" + contents.Count);
        return DmTask.CompletedTask;
    }
}

/// <summary>A non-dense obj that records its New and BYOND's crossing procs (which the engine never calls).</summary>
[DmPath("/obj/test_engine_box")]
public class obj_test_engine_box : obj
{
    public override DmTask New(params object[] __extra)
    {
        bool listed = loc != null && Dm.@in(this, loc.contents);
        EngineTrace.Add("New " + EngineTrace.D(this) + " " + EngineTrace.Where(this) + " listed=" + (listed ? "yes" : "no"));
        return DmTask.CompletedTask;
    }

    public override DmTask<object> Cross(atom_movable O = null)
    {
        EngineTrace.Add("Cross " + EngineTrace.D(this) + " " + EngineTrace.D(O));
        return base.Cross(O);
    }

    public override void Crossed(atom_movable O = null, params object[] __extra) => EngineTrace.Add("Crossed " + EngineTrace.D(this));

    public override object Uncross(atom_movable O = null)
    {
        EngineTrace.Add("Uncross " + EngineTrace.D(this));
        return base.Uncross(O);
    }

    public override void Uncrossed(atom_movable O = null) => EngineTrace.Add("Uncrossed " + EngineTrace.D(this));
}

/// <summary>A subtype with no name of its own: BYOND names it after the last path element.</summary>
[DmPath("/obj/test_engine_box/inner_kind")]
public class obj_test_engine_box_inner_kind : obj_test_engine_box
{
}

/// <summary>A dense obj (BYOND Cross: overlap is refused when both atoms are dense).</summary>
[DmPath("/obj/test_engine_crate")]
public class obj_test_engine_crate : obj
{
    public obj_test_engine_crate()
    {
        density = Dm.TRUE;
    }

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>An obj type with an explicit name; its subtype inherits it (an ancestor's explicit name wins over the type name).</summary>
[DmPath("/obj/test_engine_named")]
public class obj_test_engine_named : obj
{
    public obj_test_engine_named()
    {
        name = "named thing";
    }

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

[DmPath("/obj/test_engine_named/plain_child")]
public class obj_test_engine_named_plain_child : obj_test_engine_named
{
}

[DmPath("/mob/test_engine_mob")]
public class mob_test_engine_mob : mob
{
    public override DmTask New(params object[] __extra)
    {
        EngineTrace.Add("New " + EngineTrace.D(this) + " " + EngineTrace.Where(this));
        return DmTask.CompletedTask;
    }
}

/// <summary>A movable that is neither an obj nor a mob (tg has many: /atom/movable/screen, lighting and emissive objects).</summary>
[DmPath("/atom/movable/test_engine_effect")]
public class atom_movable_test_engine_effect : atom_movable
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>A signal listener: every handler call is recorded with the listener's label.</summary>
[DmPath("/datum/test_engine_listener")]
public class datum_test_engine_listener : datum
{
    [NotHashed]
    public string label;

    [SignalHandler]
    public virtual object on_signal(datum source = null, object a = null, object b = null, object c = null)
    {
        EngineTrace.Add("signal " + label + " " + EngineTrace.D(source) + " " + EngineTrace.D(a));
        return Defines.NONE;
    }
}

/// <summary>Small helpers shared by the engine tests.</summary>
internal static class EngineTestHelpers
{
    /// <summary>The items of a list or of an atom's contents, in order, through Dm.each as tg's for-loops read them.</summary>
    public static object[] Items(object listOrAtom) => Dm.each(listOrAtom is IDmAtom atom ? atom.contents : listOrAtom).Cast<object>().ToArray();
}
