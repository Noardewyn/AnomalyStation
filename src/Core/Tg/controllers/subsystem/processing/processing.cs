// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/processing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//Used to process objects.

// tg: code/controllers/subsystem/processing/processing.dm:3 SUBSYSTEM_DEF(processing)
[DmPath("/datum/controller/subsystem/processing")]
public partial class datum_controller_subsystem_processing : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/processing/processing.dm:4
    public datum_controller_subsystem_processing()
    {
        name = "Processing";
        priority = FIRE_PRIORITY_PROCESS;
        ss_flags = Dm.bitor(Dm.bitor(SS_BACKGROUND, SS_POST_FIRE_TIMING), SS_NO_INIT);
        wait = 1 * SECONDS;
    }

    // tg: code/controllers/subsystem/processing/processing.dm:9
    public string stat_tag = "P"; //Used for logging
    // tg: code/controllers/subsystem/processing/processing.dm:10
    public DmList processing;
    // tg: code/controllers/subsystem/processing/processing.dm:11
    public DmList currentrun;

    // tg: code/controllers/subsystem/processing/processing.dm:10-11 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        processing = Dm.list();
        currentrun = Dm.list();
    }

    // The New() that SUBSYSTEM_DEF(processing) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSprocessing)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/processing.dm:3
    public override async DmTask New(params object[] __extra)
    {
        if (SSprocessing != this)
        {
            if (SSprocessing is datum_controller_subsystem_processing)
            {
                Recover();
                qdel(SSprocessing);
            }
            SSprocessing = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/processing/processing.dm:13
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/processing/processing.dm:13", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/processing/processing.dm:17
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            currentrun = processing.Copy();
        //cache for sanic speed (lists are references anyways)
        DmList current_run = currentrun;

        while (Dm.truthy(current_run.len))
        {
            datum thing = current_run[current_run.len] as datum;
            current_run.len--;
            if (QDELETED(thing))
                Dm.remove(ref processing, thing);
            else
            {
                // RULE-GAP: rules 9.2 has no form for a boundary call inside an else-if condition: its value goes to the temp t__1.
                object t__1 = null;
                try { t__1 = thing.process(wait * 0.1f); }
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
    // tg: code/controllers/subsystem/processing/processing.dm:3  SUBSYSTEM_DEF(processing): GLOBAL_REAL(SSprocessing, /datum/controller/subsystem/processing)
    internal datum_controller_subsystem_processing SSprocessing;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing SSprocessing => ref TgState.Current.SSprocessing;
}

public partial class datum
{
    /**
     * This proc is called on a datum on every "cycle" if it is being processed by a subsystem. The time between each cycle is determined by the subsystem's "wait" setting.
     * You can start and stop processing a datum using the START_PROCESSING and STOP_PROCESSING defines.
     *
     * Since the wait setting of a subsystem can be changed at any time, it is important that any rate-of-change that you implement in this proc is multiplied by the seconds_per_tick that is sent as a parameter,
     * Additionally, any "prob" you use in this proc should instead use the SPT_PROB define to make sure that the final probability per second stays the same even if the subsystem's wait is altered.
     * Examples where this must be considered:
     * - Implementing a cooldown timer, use `mytimer -= seconds_per_tick`, not `mytimer -= 1`. This way, `mytimer` will always have the unit of seconds
     * - Damaging a mob, do `L.adjust_fire_loss(20 * seconds_per_tick)`, not `L.adjust_fire_loss(20)`. This way, the damage per second stays constant even if the wait of the subsystem is changed
     * - Probability of something happening, do `if(SPT_PROB(25, seconds_per_tick))`, not `if(prob(25))`. This way, if the subsystem wait is e.g. lowered, there won't be a higher chance of this event happening per second
     *
     * If you override this do not call parent, as it will return PROCESS_KILL. This is done to prevent objects that dont override process() from staying in the processing list
     */
    // tg: code/controllers/subsystem/processing/processing.dm:48
    // set waitfor = FALSE: this body cannot sleep, so it is a plain method (waiting-model.md 5.4); the overrides that may
    // sleep inherit waitfor = FALSE and get the wrapper.
    public virtual object process(object seconds_per_tick = null, params object[] __extra)
    {
        return PROCESS_KILL;
    }
}
