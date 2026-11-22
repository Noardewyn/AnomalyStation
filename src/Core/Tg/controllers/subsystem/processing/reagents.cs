// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//Used for active reactions in reagents/equilibrium datums
// The whole file (gaps F1: the reaction engine's gradual reactions; it was an inert stub until then).

// tg: code/controllers/subsystem/processing/reagents.dm:3 PROCESSING_SUBSYSTEM_DEF(reagents)
[DmPath("/datum/controller/subsystem/processing/reagents")]
public partial class datum_controller_subsystem_processing_reagents : datum_controller_subsystem_processing
{
    // tg: code/controllers/subsystem/processing/reagents.dm:4
    public datum_controller_subsystem_processing_reagents()
    {
        name = "Reagents";
        priority = FIRE_PRIORITY_REAGENTS;
        wait = 0.25f * SECONDS; //You might think that rate_up_lim has to be set to half, but since everything is normalised around seconds_per_tick, it automatically adjusts it to be per second. Magic!
        ss_flags = SS_KEEP_TIMING;
        runlevels = Dm.bitor(RUNLEVEL_GAME, RUNLEVEL_POSTGAME);
        init_stage = INITSTAGE_EARLY;
    }

    // The New() that PROCESSING_SUBSYSTEM_DEF(reagents) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSreagents) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/reagents.dm:3
    public override async DmTask New(params object[] __extra)
    {
        if (SSreagents != this)
        {
            if (SSreagents is datum_controller_subsystem_processing_reagents)
            {
                Recover();
                qdel(SSreagents);
            }
            SSreagents = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/processing/reagents.dm:13
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        //So our first step isn't insane
        previous_world_time = world.time;
        //Build GLOB lists - see holder.dm
        build_chemical_reactions_lists();
        return SS_INIT_SUCCESS;
    }

    // The fire() {..()} that PROCESSING_SUBSYSTEM_DEF(reagents) defines (__DEFINES/MC.dm:139) is the override below: the type's
    // own fire (:20) replaces it, as the macro's body only calls the parent.
    // tg: code/controllers/subsystem/processing/reagents.dm:20
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            currentrun = processing.Copy();
        //cache for sanic speed (lists are references anyways)
        DmList current_run = currentrun;

        //Attempt to realtime reactions in a way that doesn't make them overtly dangerous
        float delta_realtime = (world.time - previous_world_time) / 10; //normalise to s from ds
        previous_world_time = world.time;

        while (Dm.truthy(current_run.len))
        {
            datum thing = current_run[current_run.len] as datum;
            current_run.len--;
            if (QDELETED(thing))
            {
                stack_trace(Dm.text("Found qdeleted thing in [], in the current_run list.", type), "code/controllers/subsystem/processing/reagents.dm", 34);
                Dm.remove(ref processing, thing);
            }
            else
            {
                object t__1 = null;
                try { t__1 = thing.process(delta_realtime); } //we are realtime
                catch (Exception e) { World.Current.ReportRuntime(e); t__1 = null; } // isolation boundary (translation-rules.md 9.2)
                if (Dm.eq(t__1, PROCESS_KILL))
                    // fully stop so that a future START_PROCESSING will work
                    STOP_PROCESSING(this, thing);
            }
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0))
                return;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/reagents.dm:3  PROCESSING_SUBSYSTEM_DEF(reagents): GLOBAL_REAL(SSreagents, /datum/controller/subsystem/processing/reagents)
    internal datum_controller_subsystem_processing_reagents SSreagents;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_reagents SSreagents => ref TgState.Current.SSreagents;
}
