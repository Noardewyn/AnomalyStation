// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/verb_manager.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M1 P1, the client layer), but stat_entry: stat panels are not translated (translation-rules.md 13), so
// the override of :164 is not declared.

/**
 * SSverb_manager, a subsystem that runs every tick and runs through its entire queue without yielding like SSinput.
 * this exists because of how the byond tick works and where user inputted verbs are put within it.
 *
 * see TICK_ORDER.md for more info on how the byond tick is structured.
 *
 * The way the MC allots its time is via TICK_LIMIT_RUNNING, it simply subtracts the cost of SendMaps (MAPTICK_LAST_INTERNAL_TICK_USAGE)
 * plus TICK_BYOND_RESERVE from the tick and uses up to that amount of time (minus the percentage of the tick used by the time it executes subsystems)
 * on subsystems running cool things like atmospherics or Life or SSInput or whatever.
 *
 * Without this subsystem, verbs are likely to cause overtime if the MC uses all of the time it has allotted for itself in the tick, and SendMaps
 * uses as much as its expected to, and an expensive verb ends up executing that tick. This is because the MC is completely blind to the cost of
 * verbs, it can't account for it at all. The only chance for verbs to not cause overtime in a tick where the MC used as much of the tick
 * as it allotted itself and where SendMaps costed as much as it was expected to is if the verb(s) take less than TICK_BYOND_RESERVE percent of
 * the tick, which isn't much. Not to mention if SendMaps takes more than 30% of the tick and the MC forces itself to take at least 70% of the
 * normal tick duration which causes ticks to naturally overrun even in the absence of verbs.
 *
 * With this subsystem, the MC can account for the cost of verbs and thus stop major overruns of ticks. This means that the most important subsystems
 * like SSinput can start at the same time they were supposed to, leading to a smoother experience for the player since ticks aren't riddled with
 * minor hangs over and over again.
 */
// tg: code/controllers/subsystem/verb_manager.dm:22  SUBSYSTEM_DEF(verb_manager)
// verb_manager.dm is the earliest .dme file that declares /datum/controller/subsystem/verb_manager in its own text (:118;
// the SUBSYSTEM_DEF type block is a macro expansion, rules 2.3).
[DmPath("/datum/controller/subsystem/verb_manager")]
public partial class datum_controller_subsystem_verb_manager : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/verb_manager.dm:23
    public datum_controller_subsystem_verb_manager()
    {
        name = "Verb Manager";
        // tg: code/controllers/subsystem/verb_manager.dm:24
        wait = 1;
        // tg: code/controllers/subsystem/verb_manager.dm:25
        ss_flags = Dm.bitor(SS_TICKER, SS_NO_INIT);
        // tg: code/controllers/subsystem/verb_manager.dm:26
        priority = FIRE_PRIORITY_DELAYED_VERBS;
        // tg: code/controllers/subsystem/verb_manager.dm:27
        runlevels = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT);
    }

    // The New() that SUBSYSTEM_DEF(verb_manager) defines (__DEFINES/MC.dm:92-96), with NEW_SS_GLOBAL(SSverb_manager)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/verb_manager.dm:22
    public override async DmTask New(params object[] __extra)
    {
        if (SSverb_manager != this)
        {
            if (SSverb_manager is datum_controller_subsystem_verb_manager)
            {
                Recover();
                qdel(SSverb_manager);
            }
            SSverb_manager = this;
        }
        PreInit();
    }

    ///list of callbacks to procs called from verbs or verblike procs that were executed when the server was overloaded and had to delay to the next tick.
    ///this list is ran through every tick, and the subsystem does not yield until this queue is finished.
    // tg: code/controllers/subsystem/verb_manager.dm:30 (list(): dm_init_vars below)
    public DmList verb_queue;

    ///running average of how many verb callbacks are executed every second. used for the stat entry
    // tg: code/controllers/subsystem/verb_manager.dm:33
    public float verbs_executed_per_second = 0;

    ///if TRUE we treat usr's with holders just like usr's without holders. otherwise they always execute immediately
    // tg: code/controllers/subsystem/verb_manager.dm:36
    public float can_queue_admin_verbs = FALSE;

    ///if this is true all verbs immediately execute and don't queue. in case the mc is fucked or something
    // tg: code/controllers/subsystem/verb_manager.dm:39
    public float FOR_ADMINS_IF_VERBS_FUCKED_immediately_execute_all_verbs = FALSE;

    ///used for subtypes to determine if they use their own stats for the stat entry
    // tg: code/controllers/subsystem/verb_manager.dm:42
    public float use_default_stats = TRUE;

    ///if TRUE this will... message admins every time a verb is queued to this subsystem for the next tick with stats.
    ///for obvious reasons don't make this be TRUE on the code level this is for admins to turn on
    // tg: code/controllers/subsystem/verb_manager.dm:46
    public float message_admins_on_queue = FALSE;

    ///always queue if possible. overrides can_queue_admin_verbs but not FOR_ADMINS_IF_VERBS_FUCKED_immediately_execute_all_verbs
    // tg: code/controllers/subsystem/verb_manager.dm:49
    public float always_queue = FALSE;

    // The object-creating defaults of the var block (rules 10.2).
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // tg: code/controllers/subsystem/verb_manager.dm:30
        verb_queue = Dm.list();
    }

    /**
     * subsystem-specific check for whether a callback can be queued.
     * intended so that subsystem subtypes can verify whether
     *
     * subtypes may include additional arguments here if they need them! you just need to include them properly
     * in TRY_QUEUE_VERB() and co.
     */
    // tg: code/controllers/subsystem/verb_manager.dm:118
    public virtual object can_queue_verb(datum_callback_verb_callback incoming_callback = null, params object[] __extra)
    {
        if (Dm.truthy(always_queue) && !Dm.truthy(FOR_ADMINS_IF_VERBS_FUCKED_immediately_execute_all_verbs))
            return TRUE;

        if ((Dm.truthy(usr.client?.holder) && !Dm.truthy(can_queue_admin_verbs))
        || (!Dm.truthy(initialized) && !Dm.truthy(Dm.bitand(ss_flags, SS_NO_INIT)))
        || Dm.truthy(FOR_ADMINS_IF_VERBS_FUCKED_immediately_execute_all_verbs)
        || !Dm.truthy(Dm.bitand(runlevels, Master.current_runlevel)))
            return FALSE;

        return TRUE;
    }

    /**
     * queue a callback for the given proc, so that it is invoked in the next tick.
     * intended to only work with verbs or verblike procs called directly from client input, use as part of TRY_QUEUE_VERB()
     *
     * returns TRUE if the queuing was successful, FALSE otherwise.
     */
    // tg: code/controllers/subsystem/verb_manager.dm:136
    // Rules 9.3: `. = FALSE //errored`, the `.`-on-runtime idiom (the caller sees FALSE if the proc runtimes).
    public virtual object queue_verb(datum_callback_verb_callback incoming_callback = null)
    {
        object dot = null;
        try
        {
            dot = FALSE; //errored
            if (Dm.truthy(message_admins_on_queue))
                message_admins(Dm.text("[] verb queuing: tick usage: []%, proc: [], object: [], usr: []", name, TICK_USAGE(), incoming_callback.@delegate, incoming_callback.@object, usr));
            Dm.append(ref verb_queue, incoming_callback);
            return TRUE;
        }
        catch (Exception e) { World.Current.ReportRuntime(e); return dot; } // DM: a runtime ends only this proc and returns '.'
    }

    // tg: code/controllers/subsystem/verb_manager.dm:143
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        run_verb_queue();
    }

    /// runs through all of this subsystems queue of verb callbacks.
    /// goes through the entire verb queue without yielding.
    /// used so you can flush the queue outside of fire() without interfering with anything else subtype subsystems might do in fire().
    // tg: code/controllers/subsystem/verb_manager.dm:149
    public virtual void run_verb_queue()
    {
        float executed_verbs = 0;

        // Rules 6.5: a typed `as anything` loop var that the body type-tests is object (Dm.each_cast would raise at the loop
        // head where tg's istype skips the item), and the use casts.
        foreach (object verb_callback in Dm.each(verb_queue))
        {
            if (!(verb_callback is datum_callback_verb_callback))
            {
                stack_trace(Dm.text("non /datum/callback/verb_callback inside []'s verb_queue!", name), "code/controllers/subsystem/verb_manager.dm", 154);
                continue;
            }

            ((datum_callback_verb_callback)verb_callback).InvokeAsync();
            executed_verbs++;
        }

        verb_queue.Cut();
        verbs_executed_per_second = MC_AVG_SECONDS(verbs_executed_per_second, executed_verbs, wait * SECONDS);
        //note that wait SECONDS is incorrect if this is called outside of fire() but because byond is garbage i need to add a timer to rustg to find a valid solution
    }
}

public static partial class GlobalProcs
{
    /**
     * queue a callback for the given verb/verblike proc and any given arguments to the specified verb subsystem, so that they process in the next tick.
     * intended to only work with verbs or verblike procs called directly from client input, use as part of TRY_QUEUE_VERB() and co.
     *
     * returns TRUE if the queuing was successful, FALSE otherwise.
     */
    // tg: code/controllers/subsystem/verb_manager.dm:58
    public static object _queue_verb(datum_callback_verb_callback incoming_callback = null, object tick_check = null, datum_controller_subsystem_verb_manager subsystem_to_use = null, params object[] __extra)
    {
        subsystem_to_use ??= SSverb_manager;
        if (QDELETED(incoming_callback))
        {
            string destroyed_string = null;
            if (!Dm.truthy(incoming_callback))
                destroyed_string = "callback is null.";
            else
                destroyed_string = Dm.text("callback was deleted [] ticks ago. callback was created [] ticks ago.", DS2TICKS(Dm.num(Dm.sub(world.time, incoming_callback.gc_destroyed))), DS2TICKS(world.time) - incoming_callback.creation_time);

            stack_trace(Dm.text("_queue_verb() returned false because it was given a deleted callback! []", destroyed_string), "code/controllers/subsystem/verb_manager.dm", 66);
            return FALSE;
        }

        if (!istext(incoming_callback.@object) && QDELETED(incoming_callback.@object)) //just in case the object is GLOBAL_PROC
        {
            string destroyed_string = null;
            if (!Dm.truthy(incoming_callback.@object))
                destroyed_string = "callback.object is null.";
            else
                destroyed_string = Dm.text("callback.object was deleted [] ticks ago. callback was created [] ticks ago.", DS2TICKS(Dm.num(Dm.sub(world.time, ((datum)incoming_callback.@object).gc_destroyed))), DS2TICKS(world.time) - incoming_callback.creation_time);

            stack_trace(Dm.text("_queue_verb() returned false because it was given a callback acting on a qdeleted object! []", destroyed_string), "code/controllers/subsystem/verb_manager.dm", 76);
            return FALSE;
        }

        //we want unit tests to be able to directly call verbs that attempt to queue, and since unit tests should test internal behavior, we want the queue
        //to happen as if it was actually from player input if its called on a mob.
        // tg: #ifdef UNIT_TESTS ... #else not translated (inactive in production)

        if (QDELETED(usr) || usr.client == null)
        {
            stack_trace("_queue_verb() returned false because it wasn't called from player input!", "code/controllers/subsystem/verb_manager.dm", 90);
            return FALSE;
        }

        if (!(subsystem_to_use is datum_controller_subsystem_verb_manager))
        {
            stack_trace("_queue_verb() returned false because it was given an invalid subsystem to queue for!", "code/controllers/subsystem/verb_manager.dm", 96);
            return FALSE;
        }

        if (Dm.lt(TICK_USAGE(), tick_check) && !Dm.truthy(subsystem_to_use.always_queue))
            return FALSE;

        DmList args = Dm.args(incoming_callback, tick_check, subsystem_to_use, __extra);
        DmList args_to_check = args.Copy();
        args_to_check.Cut(2, 4);//cut out tick_check and subsystem_to_use

        //any subsystem can use the additional arguments to refuse queuing
        if (!Dm.truthy(Waiting.NoSleep(Dm.Call(subsystem_to_use, nameof(datum_controller_subsystem_verb_manager.can_queue_verb), Dm.arglist(args_to_check)))))
            return FALSE;

        return subsystem_to_use.queue_verb(incoming_callback);
    }

    public static ref datum_controller_subsystem_verb_manager SSverb_manager => ref TgState.Current.SSverb_manager;
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/verb_manager.dm:22  SUBSYSTEM_DEF(verb_manager): GLOBAL_REAL(SSverb_manager, /datum/controller/subsystem/verb_manager)
    internal datum_controller_subsystem_verb_manager SSverb_manager;
}
