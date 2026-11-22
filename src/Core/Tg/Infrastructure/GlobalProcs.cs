using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

[assembly: DmGlobalProcs(typeof(Anomaly.Core.Tg.GlobalProcs))]

namespace Anomaly.Core.Tg;

/// <summary>
/// tg's global procs and function-like macros (<c>/proc/x</c>: <c>qdel</c>, <c>get_turf</c>, <c>LAZYADD</c>, ...),
/// one part per mirrored tg file (docs/ai/translation-rules.md 1.5, 7.8, 11.1), plus the accessors of the per-world
/// globals, written bare in translated code through <c>using static Anomaly.Core.Tg.GlobalProcs;</c>. The shim finds
/// global procs for calls by name through <c>[assembly: DmGlobalProcs]</c> (object-model.md section 11).
/// <para>
/// Accessor pattern (rules 10.6, object-model.md section 10): a global lives in an <c>internal</c>
/// <see cref="TgState"/> field and is reached through a public static ref-returning property with its DM name, so
/// translated code reads, assigns and passes it by <c>ref</c> like a field (<c>SStimer.fire(...)</c>,
/// <c>SStimer = this</c>, <c>QDEL_NULL(ref GLOB.x)</c>; <c>GLOB</c> and <c>GLOB__var</c> below are plain properties):
/// <code>
/// // src/Core/Tg/controllers/subsystem/timer.cs, from SUBSYSTEM_DEF(timer)
/// public sealed partial class TgState { internal datum_controller_subsystem_timer SStimer; }
/// public static partial class GlobalProcs
/// {
///     public static ref datum_controller_subsystem_timer SStimer => ref TgState.Current.SStimer;
/// }
/// </code>
/// </para>
/// </summary>
public static partial class GlobalProcs
{
    /// <summary>
    /// tg <c>GLOB.x</c> (rules 10.6): the holder of this world's <c>GLOBAL_*</c> vars. Never null: before
    /// <c>/datum/controller/global_vars/New</c> runs it is the pre-GLOB storage, since DM's <c>var/global</c> vars
    /// exist before the datum. Use it only to reach a var (<c>GLOB.x</c>); GLOB as a value is <see cref="GLOB__var"/>.
    /// </summary>
    public static datum_controller_global_vars GLOB => TgState.Current.GlobVars;

    /// <summary>
    /// tg <c>GLOB</c> as a value (rules 10.6): the DM var itself, null until <c>/datum/controller/global_vars/New</c>
    /// sets <c>GLOB = src</c> (<c>if(GLOB)</c>, <c>GLOB = src</c>, <c>RegisterSignal(GLOB, ...)</c>,
    /// <c>HAS_TRAIT(GLOB, ...)</c>). Assigning a datum moves the pre-GLOB storage's values into it.
    /// </summary>
    public static datum_controller_global_vars GLOB__var
    {
        get => TgState.Current.GLOB;
        set => TgState.Current.SetGlob(value);
    }

    /// <summary>tg <c>Master</c> (rules 10.6): the Master Controller of this world.</summary>
    public static ref datum_controller_master Master => ref TgState.Current.Master;

    /// <summary>
    /// DM <c>usr</c> (rules 10.8): the mob the current call acts for, over the Kernel's ambient
    /// <see cref="World.usr"/> with DM's per-call semantics (waiting-model.md section 10). Not a ref accessor: the
    /// Kernel owns the value. A synchronous proc that assigns <c>usr</c> saves it on entry and restores it on every
    /// exit (<c>mob usr__caller = usr; try { ... } finally { usr = usr__caller; }</c>).
    /// </summary>
    public static mob usr
    {
        get => World.Current.usr as mob;
        set => World.Current.usr = value;
    }
}
