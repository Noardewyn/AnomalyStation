using System.Globalization;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Atoms;

// Fixtures of the atoms package's behaviour tests (wave 3, E; written by the independent test-writer from the DM of
// controllers/subsystem/atoms.dm, game/atom/atoms_initializing_EXPENSIVE.dm and game/atom/_atom.dm). Fixture paths are
// /<kind>/test_atoms_<name> (rules 15), shaped like translated tg types (slot signatures of slots.tsv). Each records what
// happens to it in the world's trace (the recording harness of rules 15): "<dm_id> <event>". Host-side helpers are
// [NotHashed]: they are not simulation state.

/// <summary>The per-world trace of the fixtures (a Kernel extension, so no static state).</summary>
internal sealed class AtomsTrace
{
    public readonly List<string> Lines = new();

    public static AtomsTrace Of(World world) => world.GetExtension(static _ => new AtomsTrace());

    /// <summary>Appends "<paramref name="who"/>'s id <paramref name="what"/>" to the current world's trace.</summary>
    public static void Add(datum who, string what) => Of(World.Current).Lines.Add(Id(who) + " " + what);

    public static string Id(datum d) => d == null ? "null" : d.dm_id.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// A world as tg's world start leaves it before <c>SSatoms</c> initializes (Master/New has created the subsystems,
/// GLOB has run its InitGlobal procs: code/game/world.dm:100, controllers/master.dm:110-128), with the subsystems the
/// atom paths reach (SSgarbage for qdel, SSdcs for the global signal, SSmapping for SET_PLANE_IMPLICIT, SSicon_smooth for
/// InitializeAtoms, SSatoms) and an optional map of <c>/turf/test_atoms_floor</c> in one <c>/area/test_atoms_room</c>
/// (BYOND fills a grown map with world.turf in world.area). Until the boot package (C) lands its bootstrap, the test
/// does what it would do; the GLOB procs called are the ones translated so far.
/// </summary>
internal sealed class AtomsTestWorld
{
    public readonly World World;
    public readonly List<Exception> Runtimes = new();

    public AtomsTestWorld(ulong seed = 7, int maxx = 0, int maxy = 0, int maxz = 0)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        MapGrid.Attach(World);
        Tools.GeneticsStrings.Mount(World); // gaps P3: setupGenetics' speech mutations read them
        using (World.Enter())
        {
            // GLOB is set through TgState (lead note: main splits the GLOB accessor, rules 10.6).
            TgState.Current.GLOB = new datum_controller_global_vars() /* the C# constructor: GLOB's New queues a timer (globals.dm:24) */;
            GLOB.gvars_datum_init_order = Dm.list(); // /datum/controller/global_vars/Initialize (controllers/globals.dm:47)
            GLOB.InitGlobaluse_preloader();
            GLOB.InitGlobal_preloader_attributes();
            GLOB.InitGlobal_preloader_path();
            GLOB.InitGlobalall_mutations();
            GLOB.InitGlobalfull_sequences();
            GLOB.InitGlobalbad_mutations();
            GLOB.InitGlobalgood_mutations();
            GLOB.InitGlobalnot_good_mutations();
            GLOB.InitGlobalalias_mutations();
            // Wave 4 (package C): /obj/Destroy removes its id_tag from GLOB.objects_by_id_tag (objs.dm:78). Assigned directly:
            // its InitGlobal proc would also append to gvars_datum_init_order, which AtomDefaultsTests pins.
            GLOB.objects_by_id_tag = Dm.list();
            // Wave 4 (package B): tg's /area/New registers the area (game/area/areas.dm:180-182) and /area/Initialize reads
            // the ambience lists (:198), which the world's area of these maps now runs. Assigned directly: their InitGlobal
            // procs would also append to gvars_datum_init_order, which AtomPreloaderTests pins.
            GLOB.areas = Dm.list();
            GLOB.areas_by_type = Dm.list();
            GLOB.ambience_assoc = Dm.list();
            // M3 P4: SETUP_SMOOTHING (atoms_initializing_EXPENSIVE.dm:141) parses the smoothing groups into GLOB.bitflag_lists
            // (__HELPERS/bitflag_lists.dm:1). Assigned directly, as the lists above.
            GLOB.bitflag_lists = Dm.list();
            // E14: a wire set shares its colours through GLOB.wire_color_directory and names itself in GLOB.wire_name_directory
            // (_wires.dm:92-100). Assigned directly, as the lists above.
            GLOB.wire_color_directory = Dm.list();
            GLOB.wire_name_directory = Dm.list();
            // Gaps P3: setupGenetics' speech mutations read tg's strings files through strings(), which sanitizes the path with
            // GLOB.filename_forbidden_chars (__HELPERS/files.dm:40). Assigned directly, as the lists above.
            GLOB.filename_forbidden_chars = Dm.regex(@"""|[\\\n\t/?%*:|<>]|\.\.", "g");
            Dm.@new<datum_controller_subsystem_garbage>();
            Dm.@new<datum_controller_subsystem_processing_dcs>();
            Dm.@new<datum_controller_subsystem_processing_obj>(); // wave 4: /obj/Destroy stops processing on SSobj (objs.dm:74), which tg's MC always creates
            Dm.@new<datum_controller_subsystem_mapping>();
            Dm.@new<datum_controller_subsystem_icon_smooth>();
            Dm.@new<datum_controller_subsystem_atoms>();
            if (maxx > 0)
            {
                Dm.world.turf = typeof(turf_test_atoms_floor);
                Dm.world.area = typeof(area_test_atoms_room);
                Dm.world.maxx = maxx;
                Dm.world.maxy = maxy;
                Dm.world.maxz = maxz;
            }
        }
    }

    public List<string> Trace => AtomsTrace.Of(World).Lines;

    /// <summary>The runtime messages reported so far.</summary>
    public List<string> Messages => Runtimes.Select(e => e.Message).ToList();

    /// <summary>Runs SSatoms' Initialize as the Master's init_subsystem would (it cannot sleep with no mutations).</summary>
    public static object InitializeSSatoms() => Waiting.NoSleep(SSatoms.Initialize());
}

/// <summary>
/// An /obj whose Initialize does what its vars say: call ..() or not, runtime before or after ..(), return
/// <see cref="hint"/>. Subtypes fix the behaviour in their type defaults, so that atoms created after SSatoms initialized
/// (which initialize inside New, before the creator can set anything) behave as configured.
/// </summary>
[DmPath("/obj/test_atoms_probe")]
public class obj_test_atoms_probe : obj
{
    public object hint = INITIALIZE_HINT_NORMAL;
    public float call_parent = TRUE;
    public float crash_before_parent = FALSE;
    public float crash_after_parent = FALSE;

    // What Initialize saw (host-side records).
    [NotHashed] public object seen_mapload;
    [NotHashed] public object[] seen_extras;
    [NotHashed] public object seen_source;
    [NotHashed] public object seen_initializing_something;
    [NotHashed] public float seen_initialized_state = -1;

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        seen_mapload = mapload;
        seen_extras = __extra;
        seen_source = SSatoms.get_initialized_source();
        seen_initializing_something = SSatoms.initializing_something();
        seen_initialized_state = SSatoms.initialized;
        AtomsTrace.Add(this, "Initialize mapload=" + Dm.str(mapload));
        if (Dm.truthy(crash_before_parent))
            throw Dm.CRASH("test_atoms_probe: runtime before ..()", "code/test_atoms.dm", 1);
        if (Dm.truthy(call_parent))
            base.Initialize(mapload, __extra);
        if (Dm.truthy(crash_after_parent))
            throw Dm.CRASH("test_atoms_probe: runtime after ..()", "code/test_atoms.dm", 2);
        Initialized();
        return hint;
    }

    /// <summary>A hook for subtypes that do more in Initialize, after ..().</summary>
    public virtual void Initialized()
    {
    }

    public override void LateInitialize(params object[] __extra)
    {
        AtomsTrace.Add(this, "LateInitialize initialized=" + Dm.str(SSatoms.initialized));
    }

    public override object Destroy(float? force = null)
    {
        AtomsTrace.Add(this, "Destroy");
        return base.Destroy(force);
    }
}

[DmPath("/obj/test_atoms_probe/lateload")]
public class obj_test_atoms_probe_lateload : obj_test_atoms_probe
{
    public obj_test_atoms_probe_lateload() { hint = INITIALIZE_HINT_LATELOAD; }
}

[DmPath("/obj/test_atoms_probe/lateload/crash")]
public class obj_test_atoms_probe_lateload_crash : obj_test_atoms_probe_lateload
{
    public override void LateInitialize(params object[] __extra)
    {
        base.LateInitialize(__extra);
        throw Dm.CRASH("test_atoms_probe: runtime in LateInitialize", "code/test_atoms.dm", 3);
    }
}

[DmPath("/obj/test_atoms_probe/qdel")]
public class obj_test_atoms_probe_qdel : obj_test_atoms_probe
{
    public obj_test_atoms_probe_qdel() { hint = INITIALIZE_HINT_QDEL; }
}

/// <summary>Returns no hint (DM: a bare return, null).</summary>
[DmPath("/obj/test_atoms_probe/no_hint")]
public class obj_test_atoms_probe_no_hint : obj_test_atoms_probe
{
    public obj_test_atoms_probe_no_hint() { hint = null; }
}

/// <summary>Returns INITIALIZE_HINT_NORMAL without calling ..().</summary>
[DmPath("/obj/test_atoms_probe/no_parent")]
public class obj_test_atoms_probe_no_parent : obj_test_atoms_probe
{
    public obj_test_atoms_probe_no_parent() { call_parent = FALSE; }
}

[DmPath("/obj/test_atoms_probe/crash_before")]
public class obj_test_atoms_probe_crash_before : obj_test_atoms_probe
{
    public obj_test_atoms_probe_crash_before() { crash_before_parent = TRUE; }
}

[DmPath("/obj/test_atoms_probe/crash_after")]
public class obj_test_atoms_probe_crash_after : obj_test_atoms_probe
{
    public obj_test_atoms_probe_crash_after() { crash_after_parent = TRUE; }
}

/// <summary>
/// An Initialize that reaches a proc which sleeps (tg forbids it: SHOULD_NOT_SLEEP, atoms_initializing:116). The port's
/// synchronous Initialize calls it through Waiting.NoSleep (waiting-model.md 5.3).
/// </summary>
[DmPath("/obj/test_atoms_probe/sleeper")]
public class obj_test_atoms_probe_sleeper : obj_test_atoms_probe
{
    public override void Initialized() => Waiting.NoSleep(nap());

    public virtual async DmTask nap()
    {
        AtomsTrace.Add(this, "nap start");
        await sleep(5);
        AtomsTrace.Add(this, "nap end");
    }
}

/// <summary>Creates an /obj/test_atoms_probe on its own loc during its Initialize, after ..().</summary>
[DmPath("/obj/test_atoms_probe/spawner")]
public class obj_test_atoms_probe_spawner : obj_test_atoms_probe
{
    [NotHashed] public obj_test_atoms_probe spawned;

    public override void Initialized() => spawned = Dm.@new<obj_test_atoms_probe>(loc);
}

/// <summary>LATELOAD; its LateInitialize creates an /obj/test_atoms_probe/lateload on its loc.</summary>
[DmPath("/obj/test_atoms_probe/late_spawner")]
public class obj_test_atoms_probe_late_spawner : obj_test_atoms_probe
{
    [NotHashed] public obj_test_atoms_probe spawned;

    public obj_test_atoms_probe_late_spawner() { hint = INITIALIZE_HINT_LATELOAD; }

    public override void LateInitialize(params object[] __extra)
    {
        base.LateInitialize(__extra);
        spawned = Dm.@new<obj_test_atoms_probe_lateload>(loc);
    }
}

/// <summary>Asks SSatoms to delete it once SSatoms is done (prepare_deletion, atoms.dm:219) from its Initialize.</summary>
[DmPath("/obj/test_atoms_probe/self_deleting")]
public class obj_test_atoms_probe_self_deleting : obj_test_atoms_probe
{
    public override void Initialized() => SSatoms.prepare_deletion(this);
}

/// <summary>Initializes the atoms of <see cref="nested"/> (a map template loaded during its Initialize: InitializeAtoms in InitializeAtoms).</summary>
[DmPath("/obj/test_atoms_probe/nester")]
public class obj_test_atoms_probe_nester : obj_test_atoms_probe
{
    [NotHashed] public DmList nested;
    [NotHashed] public object source_after_nested;

    public override void Initialized()
    {
        if (nested == null)
            return;
        SSatoms.InitializeAtoms(nested);
        source_after_nested = SSatoms.get_initialized_source();
    }
}

/// <summary>LATELOAD with a LateInitialize that sleeps (it inherits set waitfor = FALSE: the wrapper of waiting-model.md 5.4).</summary>
[DmPath("/obj/test_atoms_probe/late_sleeper")]
public class obj_test_atoms_probe_late_sleeper : obj_test_atoms_probe
{
    public obj_test_atoms_probe_late_sleeper() { hint = INITIALIZE_HINT_LATELOAD; }

    public override void LateInitialize(params object[] __extra) => Waiting.WaitforFalse(LateInitialize__body(__extra));

    private async DmTask LateInitialize__body(object[] __extra)
    {
        AtomsTrace.Add(this, "LateInitialize start");
        await sleep(3);
        AtomsTrace.Add(this, "LateInitialize end");
    }
}

/// <summary>
/// INITIALIZE_IMMEDIATE(/obj/test_atoms_immediate_probe) (__DEFINES/subsystems.dm:96-105), expanded inline as the New
/// override the macro defines (rules 11.1 (b), (d)); its subtypes inherit it.
/// </summary>
[DmPath("/obj/test_atoms_immediate_probe")]
public class obj_test_atoms_immediate_probe : obj_test_atoms_probe
{
    [NotHashed] public float seen_initialized_after_new = -1;

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
        seen_initialized_after_new = SSatoms.initialized;
    }
}

[DmPath("/obj/test_atoms_immediate_probe/lateload")]
public class obj_test_atoms_immediate_probe_lateload : obj_test_atoms_immediate_probe
{
    public obj_test_atoms_immediate_probe_lateload() { hint = INITIALIZE_HINT_LATELOAD; }
}

/// <summary>The world.turf of the test maps: records its Initialize.</summary>
[DmPath("/turf/test_atoms_floor")]
public class turf_test_atoms_floor : turf
{
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        AtomsTrace.Add(this, "Initialize mapload=" + Dm.str(mapload));
        return base.Initialize(mapload, __extra);
    }
}

/// <summary>The world.area of the test maps: records its Initialize.</summary>
[DmPath("/area/test_atoms_room")]
public class area_test_atoms_room : area
{
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        AtomsTrace.Add(this, "Initialize mapload=" + Dm.str(mapload));
        return base.Initialize(mapload, __extra);
    }
}

/// <summary>A listener whose signal handlers write the trace and return <see cref="returns"/>.</summary>
[DmPath("/datum/test_atoms_listener")]
public class datum_test_atoms_listener : datum
{
    public object returns = NONE;

    /// <summary>A name for the trace lines of <see cref="on_any"/>.</summary>
    public string label = "";

    [SignalHandler]
    public virtual object on_init_success(datum source = null)
    {
        AtomsTrace.Add(source, "AFTER_SUCCESSFUL_INITIALIZE");
        return returns;
    }

    [SignalHandler]
    public virtual object on_glob_post_init(datum source = null, atom created = null)
    {
        AtomsTrace.Add(created, "GLOB ATOM_AFTER_POST_INIT from " + (source == SSdcs ? "SSdcs" : AtomsTrace.Id(source)));
        return returns;
    }

    [SignalHandler]
    public virtual object on_initialized_on(datum source = null, atom created = null, object mapload = null)
    {
        AtomsTrace.Add(source, "AFTER_SUCCESSFUL_INITIALIZED_ON " + AtomsTrace.Id(created) + " mapload=" + Dm.str(mapload));
        return returns;
    }

    /// <summary>Any signal: records the source, <see cref="label"/> and up to three arguments.</summary>
    [SignalHandler]
    public virtual object on_any(datum source = null, object a = null, object b = null, object c = null)
    {
        AtomsTrace.Add(source, label + " " + Render(a) + " " + Render(b) + " " + Render(c));
        return returns;
    }

    /// <summary>Deletes the signal's source.</summary>
    [SignalHandler]
    public virtual object qdel_source(datum source = null)
    {
        AtomsTrace.Add(source, "qdel by listener");
        qdel(source);
        return NONE;
    }

    /// <summary>A COMSIG_ATOM_HAS_GRAVITY / COMSIG_TURF_HAS_GRAVITY handler: appends <see cref="returns"/> to forced_gravity.</summary>
    [SignalHandler]
    public virtual object on_has_gravity(datum source = null, object other = null, DmList forced_gravity = null)
    {
        AtomsTrace.Add(source, "has_gravity " + Render(other));
        forced_gravity.Add(returns);
        return NONE;
    }

    private static string Render(object x) => x is datum d ? "#" + AtomsTrace.Id(d) : x is DmList l ? "list(" + Dm.str(l.len) + ")" : Dm.str(x);
}

/// <summary>An /obj that lets things be dropped into it (AllowDrop, _atom.dm:455).</summary>
[DmPath("/obj/test_atoms_probe/container")]
public class obj_test_atoms_probe_container : obj_test_atoms_probe
{
    public override object AllowDrop() => TRUE;
}

// Holders that /atom/Destroy deletes (_atom.dm:163-207): each records its Destroy.

[DmPath("/datum/test_atoms_part")]
public class datum_test_atoms_part : datum
{
    public override object Destroy(float? force = null)
    {
        AtomsTrace.Add(this, "part Destroy");
        return base.Destroy(force);
    }
}

[DmPath("/datum/forensics/test_atoms_part")]
public class datum_forensics_test_atoms_part : datum_forensics
{
    public override object Destroy(float? force = null)
    {
        AtomsTrace.Add(this, "forensics Destroy");
        return base.Destroy(force);
    }
}

[DmPath("/datum/storage/test_atoms_part")]
public class datum_storage_test_atoms_part : datum_storage
{
    public override object Destroy(float? force = null)
    {
        AtomsTrace.Add(this, "storage Destroy");
        return base.Destroy(force);
    }
}

[DmPath("/datum/wires/test_atoms_part")]
public class datum_wires_test_atoms_part : datum_wires
{
    // M3 P3 translated /datum/wires/New (_wires.dm:81): it crashes unless the holder is a holder_type, as in tg
    public datum_wires_test_atoms_part() { holder_type = typeof(atom); }

    public override object Destroy(float? force = null)
    {
        AtomsTrace.Add(this, "wires Destroy");
        return base.Destroy(force);
    }
}

[DmPath("/datum/light_source/test_atoms_part")]
public class datum_light_source_test_atoms_part : datum_light_source
{
    // a stand-in for the atom's light (the Destroy order test): tg's New needs an owner to light (lighting_source.dm:54)
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override object Destroy(float? force = null)
    {
        AtomsTrace.Add(this, "light Destroy");
        return base.Destroy(force);
    }
}
