// Translated from tgstation (snapshot 1d99bd7f): code/controllers/globals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Not translated: stat_entry (stat panels, rules 13) and the VV procs vv_edit_var (:37) and vv_get_var (:42) (VV, rules 13;
// their /datum roots do not exist in the port). The [DmPath] of /datum/controller/global_vars is here: globals.dm declares the
// type in its own text, and the GLOBAL_* macro expansions in earlier files do not count (rules 2.3).

// See initialization order in /code/game/world.dm
// tg: code/controllers/globals.dm:2  GLOBAL_REAL(GLOB, /datum/controller/global_vars): TgState.GLOB, GlobalProcs.GLOB
// (src/Core/Tg/Infrastructure).

// tg: code/controllers/globals.dm:4
[DmPath("/datum/controller/global_vars")]
public partial class datum_controller_global_vars : datum_controller
{
    public datum_controller_global_vars()
    {
        name = "Global Variables";
    }

    // tg: code/controllers/globals.dm:7  var/static/list/gvars_datum_protected_varlist (rules 10.6: a TgState field)
    public static ref DmList gvars_datum_protected_varlist => ref TgState.Current.datum_controller_global_vars__gvars_datum_protected_varlist;
    // tg: code/controllers/globals.dm:8
    // DEVIATION(DV9): never set; tg fills it from the vars of a fresh controller (:20-22), which the port does not have.
    public DmList gvars_datum_in_built_vars;
    // tg: code/controllers/globals.dm:9
    public DmList gvars_datum_init_order;

    // tg: code/controllers/globals.dm:11
    public override async DmTask New(params object[] __extra)
    {
        if (Dm.truthy(GLOB__var))
            throw Dm.CRASH("Multiple instances of global variable controller created", "code/controllers/globals.dm", 13);
        GLOB__var = this;

        datum_controller exclude_these = Dm.@new<datum_controller>();
        // I know this is dumb but the nested vars list hangs a ref to the datum. This fixes that
        // I have an issue report open, lummox has not responded. It might be a FeaTuRE
        // Sooo we gotta be dumb
        // DEVIATION(DV9): var/list/controller_vars = exclude_these.vars.Copy(); controller_vars["vars"] = null;
        // gvars_datum_in_built_vars = controller_vars + list(...) (:20-22) are left out: vars is not translated (rules 10.7).
        // exclude_these is still created and queued for deletion, so that world ids and timer ids match tg's.

        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), (0 > GC_FILTER_QUEUE ? WEAKREF(exclude_these) : exclude_these)), 0, file: "code/controllers/globals.dm", line: 24); //signal logging isn't ready

        await Initialize();
    }

    // tg: code/controllers/globals.dm:28
    public override object Destroy(float? force = null)
    {
        // This is done to prevent an exploit where admins can get around protected vars
        return QDEL_HINT_IWILLGC;
    }

    // tg: code/controllers/globals.dm:33
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/globals.dm:33", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/globals.dm:48
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        gvars_datum_init_order = Dm.list();
        gvars_datum_protected_varlist = Dm.list(Dm.kv(nameof(gvars_datum_protected_varlist), TRUE));
        DmList global_procs = typesof("/datum/controller/global_vars/proc");
        // DEVIATION(DV9): the check of the InitGlobal procs against the vars (:52-59: expected_len from vars.len and
        // gvars_datum_in_built_vars.len, the warning and the "Missing procs" log) is left out: vars is not translated (rules
        // 10.7), and typesof lists only the procs the port has translated (W3.0, S3), so the initializers of globals that are
        // not translated yet are skipped.

        foreach (object I in Dm.each(global_procs))
        {
            float start_tick = world.time;
            await Dm.Call(this, (string)I);
            float end_tick = world.time;
            if (Dm.truthy(end_tick - start_tick))
                warning(Dm.text("Global [] slept during initialization!", replacetext(Dm.text("[]", I), "InitGlobal", "")));
        }

        // Someone make it so this call isn't necessary
        await make_datum_reference_lists();
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/globals.dm:7  /datum/controller/global_vars var/static/list/gvars_datum_protected_varlist
    internal DmList datum_controller_global_vars__gvars_datum_protected_varlist;
}
