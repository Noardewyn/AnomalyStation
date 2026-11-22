using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

// Fixtures of the independent behaviour tests of package B (wave 4: turf and area), written from tg's DM. Fixture paths are
// /<kind>/test_turfarea_<name> (translation-rules.md 15). The movable fixtures' New does not call ..(), so these tests do
// not run tg's /atom/New (SSatoms.InitAtom) for them: SSatoms initializes only what a test creates through a turf or area
// type. What a fixture records goes to the per-world TurfAreaTrace, never to a static (host-side counters are
// [NotHashed]: they are not simulation state).

/// <summary>A DM list as a .NET list of its items, for assertions (the DM list's own order).</summary>
internal static class TurfAreaLists
{
    public static List<T> Of<T>(object list) where T : class => Dm.each(list).Cast<object>().Select(x => (T)x).ToList();
}

/// <summary>The per-world recording harness (translation-rules.md 15): lines in call order.</summary>
internal sealed class TurfAreaTrace
{
    public readonly List<string> Lines = new();

    public static TurfAreaTrace Of(World world) => world.GetExtension(static _ => new TurfAreaTrace());

    public static void Add(string line) => Of(World.Current).Lines.Add(line);

    /// <summary>A value as the trace writes it: turfs by coordinates, areas by type, named things by name.</summary>
    public static string D(object value) => value switch
    {
        null => "null",
        turf t => $"turf({Dm.str(t.x)},{Dm.str(t.y)},{Dm.str(t.z)})",
        area a => "area:" + a.GetType().Name,
        atom a => a.name,
        DmList l => "list(" + Dm.str(l.len) + ")",
        _ => Dm.str(value),
    };
}

/// <summary>
/// Signal handlers that write the trace: "<c>label</c> source(args)". <see cref="returns"/> is what every handler returns
/// (a COMPONENT_* flag for the tests that block something).
/// </summary>
[DmPath("/datum/test_turfarea_listener")]
public class datum_test_turfarea_listener : datum
{
    public object returns = NONE;
    public string label = "";

    [SignalHandler]
    public virtual object on_any(datum source = null, object a = null, object b = null, object c = null, object d = null)
    {
        TurfAreaTrace.Add(label + " " + TurfAreaTrace.D(source) + "(" + TurfAreaTrace.D(a) + "," + TurfAreaTrace.D(b) + "," + TurfAreaTrace.D(c) + "," + TurfAreaTrace.D(d) + ")");
        return returns;
    }

    /// <summary>A handler that takes <see cref="victim"/> to <see cref="destination"/> (a raw loc set, nothing is announced).</summary>
    public atom_movable victim;
    public atom destination;

    [SignalHandler]
    public virtual object on_move_victim(datum source = null)
    {
        TurfAreaTrace.Add(label + " moves " + TurfAreaTrace.D(victim) + " to " + TurfAreaTrace.D(destination));
        victim.loc = destination;
        return NONE;
    }

    /// <summary>Records the area a turf is in when the signal arrives (change_area moves the turf before it signals).</summary>
    [SignalHandler]
    public virtual object on_turf_area_changed(turf source = null, area old_area = null)
    {
        TurfAreaTrace.Add(label + " " + TurfAreaTrace.D(source) + " loc=" + TurfAreaTrace.D(source.loc) + " old=" + TurfAreaTrace.D(old_area));
        return returns;
    }
}

/// <summary>A turf that counts its Initialize calls (the base is tg's /turf/Initialize) and is a listener on itself.</summary>
[DmPath("/turf/open/indestructible/test_turfarea_floor")]
public class turf_test_turfarea_floor : turf_open_indestructible
{
    [NotHashed] public int init_calls;

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        init_calls++;
        TurfAreaTrace.Add("Initialize " + TurfAreaTrace.D(this) + " mapload=" + Dm.str(mapload) + " extras=" + __extra.Length);
        return base.Initialize(mapload, __extra);
    }

    /// <summary>What a turf registers on itself (cleared by _clear_signal_refs).</summary>
    [SignalHandler]
    public virtual void on_self_signal(datum source = null)
    {
        TurfAreaTrace.Add("self " + TurfAreaTrace.D(source));
    }
}

[DmPath("/turf/closed/indestructible/test_turfarea_wall")]
public class turf_test_turfarea_wall : turf_closed_indestructible
{
    [NotHashed] public int init_calls;

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        init_calls++;
        return base.Initialize(mapload, __extra);
    }
}

[DmPath("/turf/open/space/basic/test_turfarea_space")]
public class turf_test_turfarea_space : turf_open_space_basic
{
    [NotHashed] public int init_calls;

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        init_calls++;
        return base.Initialize(mapload, __extra);
    }
}

/// <summary>A floor with plating-like underfloor accessibility, to see what levelupdate sends.</summary>
[DmPath("/turf/open/indestructible/test_turfarea_floor/interactable")]
public class turf_test_turfarea_floor_interactable : turf_test_turfarea_floor
{
    public turf_test_turfarea_floor_interactable()
    {
        underfloor_accessibility = UNDERFLOOR_INTERACTABLE;
    }
}

/// <summary>A floor whose Initialize runs tg's, then fails (a runtime after ..() in tg's terms; DV1).</summary>
[DmPath("/turf/open/indestructible/test_turfarea_floor/crashing")]
public class turf_test_turfarea_floor_crashing : turf_test_turfarea_floor
{
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        throw Dm.CRASH("test_turfarea_floor_crashing: runtime in Initialize", "code/test_turfarea.dm", 1);
    }
}

// A baseturfs chain A -> B -> C -> D, D its own baseturf (turf.dm:469-516 walks it from the turf's own baseturfs var).

[DmPath("/turf/open/indestructible/test_turfarea_chain_d")]
public class turf_test_turfarea_chain_d : turf_open_indestructible
{
    public turf_test_turfarea_chain_d() { baseturfs = typeof(turf_test_turfarea_chain_d); }
}

[DmPath("/turf/open/indestructible/test_turfarea_chain_c")]
public class turf_test_turfarea_chain_c : turf_open_indestructible
{
    public turf_test_turfarea_chain_c() { baseturfs = typeof(turf_test_turfarea_chain_d); }
}

[DmPath("/turf/open/indestructible/test_turfarea_chain_b")]
public class turf_test_turfarea_chain_b : turf_open_indestructible
{
    public turf_test_turfarea_chain_b() { baseturfs = typeof(turf_test_turfarea_chain_c); }
}

[DmPath("/turf/open/indestructible/test_turfarea_chain_a")]
public class turf_test_turfarea_chain_a : turf_open_indestructible
{
    public turf_test_turfarea_chain_a() { baseturfs = typeof(turf_test_turfarea_chain_b); }
}

/// <summary>A second turf type with the same baseturfs as chain_a (its cache entry is keyed by the baseturf, not the type).</summary>
[DmPath("/turf/open/indestructible/test_turfarea_chain_a2")]
public class turf_test_turfarea_chain_a2 : turf_open_indestructible
{
    public turf_test_turfarea_chain_a2() { baseturfs = typeof(turf_test_turfarea_chain_b); }
}

/// <summary>A floor that smooths with others: its Initialize parses the groups (deferred, M3) and queues it.</summary>
[DmPath("/turf/open/indestructible/test_turfarea_floor/smooth")]
public class turf_test_turfarea_floor_smooth : turf_test_turfarea_floor
{
    public turf_test_turfarea_floor_smooth()
    {
        smoothing_flags = SMOOTH_BITMASK;
        smoothing_groups = SMOOTH_GROUP_TURF_OPEN;
        canSmoothWith = SMOOTH_GROUP_TURF_OPEN;
    }
}

/// <summary>A floor that uses integrity and has opacity set, for the optional parts of /turf/Initialize.</summary>
[DmPath("/turf/open/indestructible/test_turfarea_floor/integral")]
public class turf_test_turfarea_floor_integral : turf_test_turfarea_floor
{
    public turf_test_turfarea_floor_integral()
    {
        uses_integrity = TRUE;
        max_integrity = 77;
        opacity = TRUE;
        post_init_icon_state = "set_by_atom_initialize";
    }
}

/// <summary>A dense floor, to see /turf/set_density and the explosion block arithmetic.</summary>
[DmPath("/turf/open/indestructible/test_turfarea_floor/blocker")]
public class turf_test_turfarea_floor_blocker : turf_test_turfarea_floor
{
    public turf_test_turfarea_floor_blocker()
    {
        explosive_resistance = 5;
    }
}

/// <summary>
/// A movable that records the Bump calls /turf/Enter makes. Optionally deletes itself when it bumps (tg's Bump can qdel
/// its mover, turf.dm:449-458). Package A's real /atom/movable/Bump (move-chain.md 3c) was not merged when this fixture was
/// written; the override records the calls.
/// </summary>
[DmPath("/obj/test_turfarea_probe")]
public class obj_test_turfarea_probe : obj
{
    [NotHashed] public readonly List<atom> bumped = new();
    public float qdel_on_bump = FALSE;

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        bumped.Add(bumped_atom);
        TurfAreaTrace.Add("Bump " + TurfAreaTrace.D(this) + " -> " + TurfAreaTrace.D(bumped_atom));
        if (Dm.truthy(qdel_on_bump))
            qdel(this);
        return null;
    }
}

/// <summary>
/// An occupant with tg's /atom/movable/Cross (atoms_movable.dm:935-939). Package A's real Cross (game/atoms_movable.cs) was
/// not merged when this fixture was written, so the occupant keeps an ADAPTED copy: the COMSIG_MOVABLE_CROSS and
/// COMSIG_MOVABLE_CROSS_OVER blocks and the CanPass fall-through reach /turf/Enter as they do in the real chain (W4.M runs the real Move chain over the real turfs in
/// Integration/MoveChainT4Tests.cs).
/// <see cref="cross_result"/> and <see cref="qdel_crossing"/> script the exceptions.
/// </summary>
[DmPath("/obj/test_turfarea_occupant")]
public class obj_test_turfarea_occupant : obj
{
    /// <summary>-1 = tg's Cross; 0 = refuse every mover; 1 = allow every mover (without calling CanPass).</summary>
    public float cross_result = -1;

    /// <summary>The mover is deleted while this occupant crosses it.</summary>
    public float qdel_crossing = FALSE;

    [NotHashed] public readonly List<atom_movable> crossed = new();

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override async DmTask<object> Cross(atom_movable crossed_atom = null)
    {
        crossed.Add(crossed_atom);
        TurfAreaTrace.Add("Cross " + TurfAreaTrace.D(this) + " <- " + TurfAreaTrace.D(crossed_atom));
        if (Dm.truthy(qdel_crossing))
            qdel(crossed_atom);
        if (cross_result == 0)
            return FALSE;
        if (cross_result == 1)
            return TRUE;
        // tg: code/game/atoms_movable.dm:935-939
        if (Dm.truthy(Dm.bitand(TurfAreaSignals.Send(this, COMSIG_MOVABLE_CROSS, crossed_atom), COMPONENT_BLOCK_CROSS)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(TurfAreaSignals.Send(crossed_atom, COMSIG_MOVABLE_CROSS_OVER, this), COMPONENT_BLOCK_CROSS)))
            return FALSE;
        return await CanPass(crossed_atom, get_dir(this, crossed_atom));
    }
}

/// <summary>SEND_SIGNAL (dcs/helpers.dm:5): nothing registered means NONE and no call; else the signal's combined return.</summary>
internal static class TurfAreaSignals
{
    public static object Send(datum source, string signal, params object[] args)
    {
        if (!Dm.truthy(source._listen_lookup?[signal]))
            return NONE;
        object[] all = new object[args.Length + 1];
        all[0] = source;
        args.CopyTo(all, 1);
        return source._SendSignal(signal, Dm.list(all));
    }
}

/// <summary>A movable that only sits on a turf (initialize_occupant's subject); its signals are watched by listeners.</summary>
[DmPath("/obj/test_turfarea_sitter")]
public class obj_test_turfarea_sitter : obj
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>
/// A movable whose zMove (package A's, not merged when this fixture was written) only records the call and moves the movable by the engine's
/// raw loc, which is what /turf/open/transfer_occupant (_open.dm:241-274) needs to be observed.
/// </summary>
[DmPath("/obj/test_turfarea_transit")]
public class obj_test_turfarea_transit : obj
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override async DmTask<object> zMove(object dir = null, turf target = null, float? z_move_flags = null, params object[] __extra)
    {
        TurfAreaTrace.Add("zMove " + TurfAreaTrace.D(this) + " dir=" + TurfAreaTrace.D(dir) + " -> " + TurfAreaTrace.D(target) + " flags=" + Dm.str(z_move_flags));
        if (target != null)
            loc = target;
        return TRUE;
    }
}

/// <summary>A mob that records update_ambience_area (package C's, not merged when this fixture was written) for /area/Entered.</summary>
[DmPath("/mob/test_turfarea_mob")]
public class mob_test_turfarea_mob : mob
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override void update_ambience_area(area new_area = null)
    {
        TurfAreaTrace.Add("update_ambience_area " + TurfAreaTrace.D(this) + " " + TurfAreaTrace.D(new_area));
    }

    public override async DmTask<object> zMove(object dir = null, turf target = null, float? z_move_flags = null, params object[] __extra)
    {
        TurfAreaTrace.Add("zMove " + TurfAreaTrace.D(this) + " dir=" + TurfAreaTrace.D(dir) + " -> " + TurfAreaTrace.D(target) + " flags=" + Dm.str(z_move_flags));
        if (target != null)
            loc = target;
        return TRUE;
    }
}

// Areas.

/// <summary>An area that needs power (the default), to see Initialize's power and luminosity rules.</summary>
[DmPath("/area/test_turfarea_powered")]
public class area_test_turfarea_powered : area
{
}

/// <summary>An area that is not UNIQUE_AREA: the map reader's areas of this kind are not in GLOB.areas_by_type.</summary>
[DmPath("/area/test_turfarea_common")]
public class area_test_turfarea_common : area
{
    public area_test_turfarea_common() { area_flags_mapping = NONE; }
}

/// <summary>A powerless area with static lighting (the testroom turns static_lighting off; this one keeps tg's TRUE).</summary>
[DmPath("/area/test_turfarea_lit")]
public class area_test_turfarea_lit : area
{
    public area_test_turfarea_lit() { requires_power = FALSE; }
}

/// <summary>An area with its own ambience sounds and an unknown ambience index.</summary>
[DmPath("/area/test_turfarea_noisy")]
public class area_test_turfarea_noisy : area
{
    public area_test_turfarea_noisy() { ambience_index = "test_turfarea_ambience"; }
}
