using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

// Test-assembly types of M3 T2's fixture maps (rules 15: a fixture path no tg path starts with). They record what the load
// path does to them in the world's trace (T2Trace): the vars they hold when New and Initialize start.

/// <summary>
/// An object with one var of each kind a map can edit (text, number, list, any value, typepath). New records the vars
/// before tg's /atom/New runs (so before the preloader's first pass), Initialize records them when SSatoms calls it.
/// </summary>
[DmPath("/obj/t2_probe")]
public class obj_t2_probe : obj
{
    public string text_var = "default";
    public float num_var = 7;
    public DmList list_var = null;
    public object any_var = "default";
    public Type path_var = typeof(obj_item);

    public string Snapshot() => Dm.text("[]|[]|[]|[]|[]", text_var, num_var, list_var == null ? "null" : Dm.text("list[]", list_var.len), any_var == null ? "null" : any_var, path_var == null ? "null" : DmTypes.PathOf(path_var));

    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        T2Trace.Add(this, "New " + Snapshot());
        await base.New(Dm.forward(__extra, loc));
    }

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        T2Trace.Add(this, "Initialize " + Snapshot());
        return base.Initialize(mapload, __extra);
    }
}

/// <summary>
/// A probe whose New does not call tg's /atom/New (tg's types that never call ..() in New): the preloader's first pass
/// (inside /atom/New) never runs for it, and build_coordinate's second pass sets the map's vars after New returned.
/// </summary>
[DmPath("/obj/t2_probe_nonew")]
public class obj_t2_probe_nonew : obj_t2_probe
{
    [DmParams("loc")]
    public override DmTask New(params object[] __extra)
    {
        T2Trace.Add(this, "New " + Snapshot());
        return DmTask.CompletedTask;
    }
}

// ---- initialization order and hints (InitOrderTests): every probe writes "<label> New" / "<label> Initialize mapload=<m>" /
// "<label> LateInitialize" / "<label> Destroy" into the world trace, the label being its map name (turfs: their coordinates).

/// <summary>An object that traces its New, Initialize (returning INITIALIZE_HINT_NORMAL), LateInitialize and Destroy.</summary>
[DmPath("/obj/t2_init")]
public class obj_t2_init : obj
{
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        T2Trace.Of(World.Current).Lines.Add(name + " New");
    }

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        T2Trace.Of(World.Current).Lines.Add(name + " Initialize mapload=" + Dm.str(mapload));
        return Hint();
    }

    protected virtual object Hint() => INITIALIZE_HINT_NORMAL;

    public override void LateInitialize(params object[] __extra)
    {
        T2Trace.Of(World.Current).Lines.Add(name + " LateInitialize");
    }

    public override object Destroy(float? force = null)
    {
        T2Trace.Of(World.Current).Lines.Add(name + " Destroy");
        return base.Destroy(force);
    }
}

/// <summary>Returns INITIALIZE_HINT_LATELOAD: its LateInitialize runs after every Initialize of the map.</summary>
[DmPath("/obj/t2_init/late")]
public class obj_t2_init_late : obj_t2_init
{
    protected override object Hint() => INITIALIZE_HINT_LATELOAD;
}

/// <summary>Returns INITIALIZE_HINT_QDEL: SSatoms deletes it right after its Initialize.</summary>
[DmPath("/obj/t2_init/qdel")]
public class obj_t2_init_qdel : obj_t2_init
{
    protected override object Hint() => INITIALIZE_HINT_QDEL;
}

/// <summary>INITIALIZE_IMMEDIATE(/obj/t2_init/immediate) (__DEFINES/subsystems.dm:96-105), expanded inline as in rules 11.1 (b), (d).</summary>
[DmPath("/obj/t2_init/immediate")]
public class obj_t2_init_immediate : obj_t2_init
{
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        DmList args = Dm.args(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }
}

/// <summary>A type marked inert (D112): SSatoms must not run its Initialize; if it does, the trace says BAD.</summary>
[DmPath("/obj/t2_inert"), DmInert]
public class obj_t2_inert : obj
{
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        T2Trace.Of(World.Current).Lines.Add(name + " BAD own Initialize ran");
        return base.Initialize(mapload, __extra);
    }
}

[DmPath("/mob/t2_init")]
public class mob_t2_init : mob
{
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object result = base.Initialize(mapload, __extra);
        T2Trace.Of(World.Current).Lines.Add(name + " Initialize mapload=" + Dm.str(mapload));
        return result;
    }
}

[DmPath("/turf/open/indestructible/t2_init")]
public class turf_open_indestructible_t2_init : turf_open_indestructible
{
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object result = base.Initialize(mapload, __extra);
        T2Trace.Of(World.Current).Lines.Add("turf(" + Dm.str(x) + "," + Dm.str(y) + ") Initialize mapload=" + Dm.str(mapload));
        return result;
    }
}

[DmPath("/area/t2_init")]
public class area_t2_init : area
{
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object result = base.Initialize(mapload, __extra);
        T2Trace.Of(World.Current).Lines.Add("area Initialize mapload=" + Dm.str(mapload));
        return result;
    }

    public override void LateInitialize(params object[] __extra)
    {
        T2Trace.Of(World.Current).Lines.Add("area LateInitialize");
        base.LateInitialize(__extra);
    }
}
