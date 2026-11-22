// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/garbage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*!
## Debugging GC issues

In order to debug `qdel()` failures, there are several tools available.
To enable these tools, define `TESTING` in [_compile_options.dm](https://github.com/tgstation/-tg-station/blob/master/code/_compile_options.dm).

First is a verb called "Find References", which lists **every** refererence to an object in the world. This allows you to track down any indirect or obfuscated references that you might have missed.

Complementing this is another verb, "qdel() then Find References".
This does exactly what you'd expect; it calls `qdel()` on the object and then it finds all references remaining.
This is great, because it means that `Destroy()` will have been called before it starts to find references,
so the only references you'll find will be the ones preventing the object from `qdel()`ing gracefully.

If you have a datum or something you are not destroying directly (say via the singulo),
the next tool is `QDEL_HINT_FINDREFERENCE`. You can return this in `Destroy()` (where you would normally `return ..()`),
to print a list of references once it enters the GC queue.

Finally is a verb, "Show qdel() Log", which shows the deletion log that the garbage subsystem keeps. This is helpful if you are having race conditions or need to review the order of deletions.

Note that for any of these tools to work `TESTING` must be defined.
By using these methods of finding references, you can make your life far, far easier when dealing with `qdel()` failures.
*/

// DEVIATION(DV7): SSgarbage is the shim of docs/ai/specs/object-model.md section 7: the GC queues, refcount() checks and
// BYOND's hard delete are not ported. Queue does to the datum what tg's Queue does (gc_destroyed) and then removes it
// from the world registry instead of queueing it; HardDelete never deletes, it only removes the datum from the registry
// (and runs a client's Del, BYOND's del of a client: the player is disconnected).
// fire and HandleQueue are translated and run on queues that stay empty (the refcount() branch of HandleQueue is
// Dm.NotTranslated and unreachable); the stat panel, Shutdown's qdel log and Recover are Dm.NotTranslated. A qdel'd datum
// stays in memory while it is referenced and is freed by the C# GC.

// tg: code/controllers/subsystem/garbage.dm:24 SUBSYSTEM_DEF(garbage)
[DmPath("/datum/controller/subsystem/garbage")]
public partial class datum_controller_subsystem_garbage : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/garbage.dm:25
    public datum_controller_subsystem_garbage()
    {
        name = "Garbage";
        priority = FIRE_PRIORITY_GARBAGE;
        wait = 2 * SECONDS;
        ss_flags = Dm.bitor(Dm.bitor(SS_POST_FIRE_TIMING, SS_BACKGROUND), SS_NO_INIT);
        runlevels = Dm.bitor(RUNLEVELS_DEFAULT, RUNLEVEL_LOBBY);
        init_stage = INITSTAGE_FIRST;
    }

    // tg: code/controllers/subsystem/garbage.dm:32
    public DmList collection_timeout; // deciseconds to wait before moving something up in the queue to the next level

    //Stat tracking
    // tg: code/controllers/subsystem/garbage.dm:35
    public float delslasttick = 0; // number of del()'s we've done this tick
    // tg: code/controllers/subsystem/garbage.dm:36
    public float gcedlasttick = 0; // number of things that gc'ed last tick
    // tg: code/controllers/subsystem/garbage.dm:37
    public float totaldels = 0;
    // tg: code/controllers/subsystem/garbage.dm:38
    public float totalgcs = 0;

    // tg: code/controllers/subsystem/garbage.dm:40
    public float highest_del_ms = 0;
    // tg: code/controllers/subsystem/garbage.dm:41
    public string highest_del_type_string = "";

    // tg: code/controllers/subsystem/garbage.dm:43
    public DmList pass_counts;
    // tg: code/controllers/subsystem/garbage.dm:44
    public DmList fail_counts;

    // tg: code/controllers/subsystem/garbage.dm:46
    public DmList items; // Holds our qdel_item statistics datums

    //Queue
    // tg: code/controllers/subsystem/garbage.dm:49
    public DmList queues;
    // tg: #ifdef REFERENCE_TRACKING ... #endif not translated (inactive in production)

    // tg: code/controllers/subsystem/garbage.dm:32, :46 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        collection_timeout = Dm.list(GC_FILTER_QUEUE, GC_CHECK_QUEUE, GC_DEL_QUEUE);
        items = Dm.list();
    }

    // The New() that SUBSYSTEM_DEF(garbage) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSgarbage)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/garbage.dm:24
    public override async DmTask New(params object[] __extra)
    {
        if (SSgarbage != this)
        {
            if (SSgarbage is datum_controller_subsystem_garbage)
            {
                Recover();
                qdel(SSgarbage);
            }
            SSgarbage = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/garbage.dm:59
    public override void PreInit()
    {
        InitQueues();
    }

    // tg: code/controllers/subsystem/garbage.dm:62
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/garbage.dm:62", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return default;
    }

    // tg: code/controllers/subsystem/garbage.dm:82
    public override async DmTask Shutdown()
    {
        //Adds the del() log to the qdel log file
        DmList del_log = Dm.list();

        //sort by how long it's wasted hard deleting
        sortTim(items, cmp: "/proc/" + nameof(GlobalProcs.cmp_qdel_item_time), associative: TRUE);
        foreach (object path in Dm.each(items))
        {
            var I = items[path] as datum_qdel_item;
            DmList entry = Dm.list();
            del_log[path] = entry;

            if (Dm.truthy(Dm.bitand(I.qdel_flags, QDEL_ITEM_SUSPENDED_FOR_LAG)))
                entry["SUSPENDED FOR LAG"] = TRUE;
            if (Dm.truthy(I.failures))
                entry["Failures"] = I.failures;
            entry["qdel() Count"] = I.qdels;
            entry["Destroy() Cost (ms)"] = I.destroy_time;

            if (Dm.truthy(I.hard_deletes))
            {
                entry["Total Hard Deletes"] = I.hard_deletes;
                entry["Time Spend Hard Deleting (ms)"] = I.hard_delete_time;
                entry["Highest Time Spend Hard Deleting (ms)"] = I.hard_delete_max;
                if (Dm.truthy(I.hard_deletes_over_threshold))
                    entry["Hard Deletes Over Threshold"] = I.hard_deletes_over_threshold;
            }
            if (Dm.truthy(I.slept_destroy))
                entry["Total Sleeps"] = I.slept_destroy;
            if (Dm.truthy(I.no_respect_force))
                entry["Total Ignored Force"] = I.no_respect_force;
            if (Dm.truthy(I.no_hint))
                entry["Total No Hint"] = I.no_hint;
            if (Dm.truthy(LAZYLEN(I.extra_details)))
                entry["Deleted Metadata"] = I.extra_details;
        }

        log_qdel("", del_log);
    }

    // tg: code/controllers/subsystem/garbage.dm:117
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        //the fact that this resets its processing each fire (rather then resume where it left off) is intentional.
        float queue = GC_QUEUE_FILTER;

        while (Dm.eq(state, SS_RUNNING))
        {
            float sw__1 = queue;
            if (sw__1 == GC_QUEUE_FILTER)
            {
                HandleQueue(GC_QUEUE_FILTER);
                queue = GC_QUEUE_FILTER + 1;
            }
            else if (sw__1 == GC_QUEUE_CHECK)
            {
                HandleQueue(GC_QUEUE_CHECK);
                queue = GC_QUEUE_CHECK + 1;
            }
            else if (sw__1 == GC_QUEUE_HARDDELETE)
            {
                HandleQueue(GC_QUEUE_HARDDELETE);
                if (Dm.eq(state, SS_PAUSED)) //make us wait again before the next run.
                    state = SS_RUNNING;
                break;
            }
        }
    }

    // tg: code/controllers/subsystem/garbage.dm:137
    public virtual void InitQueues()
    {
        if (queues == null) // Only init the queues if they don't already exist, prevents overriding of recovered lists
        {
            queues = Dm.list_n(GC_QUEUE_COUNT);
            pass_counts = Dm.list_n(GC_QUEUE_COUNT);
            fail_counts = Dm.list_n(GC_QUEUE_COUNT);
            for (float i = 1, i__end = GC_QUEUE_COUNT; i <= i__end; i++)
            {
                queues[i] = Dm.list();
                pass_counts[i] = 0;
                fail_counts[i] = 0;
            }
        }
    }

    // tg: code/controllers/subsystem/garbage.dm:148
    public virtual void HandleQueue(float? level = null)
    {
        level ??= GC_QUEUE_FILTER;
        if (Dm.eq(level, GC_QUEUE_FILTER))
        {
            delslasttick = 0;
            gcedlasttick = 0;
        }
        object cut_off_time = Dm.sub(world.time, collection_timeout[level]); //ignore entries newer then this
        DmList queue = queues[level] as DmList;
        if (Dm.truthy(HandleQueue__count)) //runtime last run before we could do this.
        {
            float c = HandleQueue__count;
            HandleQueue__count = 0; //so if we runtime on the Cut, we don't try again.
            DmList lastqueue = queues[HandleQueue__lastlevel] as DmList;
            lastqueue.Cut(1, c + 1);
        }

        HandleQueue__lastlevel = level;

        //We do this rather then for(var/list/ref_info in queue) because that sort of for loop copies the whole list.
        //Normally this isn't expensive, but the gc queue can grow to 40k items, and that gets costly/causes overrun.
        for (float i = 1, i__end = length(queue); i <= i__end; i++)
        {
            DmList L = queue[i] as DmList;
            if (length(L) < GC_QUEUE_ITEM_INDEX_COUNT)
            {
                HandleQueue__count++;
                if (Dm.truthy(((TICK_USAGE() > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)))
                    return;
                continue;
            }

            object queued_at_time = L[GC_QUEUE_ITEM_QUEUE_TIME];
            if (Dm.gt(queued_at_time, cut_off_time))
                break; // Everything else is newer, skip them
            HandleQueue__count++;

            datum D = L[GC_QUEUE_ITEM_REF] as datum;

            // DEVIATION(DV7): the queues stay empty (Queue removes the datum from the world registry instead of
            // queueing it), so nothing reaches this point. The rest of the loop body (garbage.dm:184-259) tests
            // refcount(D) == REFS_WE_EXPECT, which has no C# counterpart (BYOND reference counting).
            Dm.NotTranslated("code/controllers/subsystem/garbage.dm:184", "HandleQueue: refcount() and the GC check and hard-delete passes (DV7)");
        }
        if (Dm.truthy(HandleQueue__count))
        {
            queue.Cut(1, HandleQueue__count + 1);
            HandleQueue__count = 0;
        }
    }

    // tg: code/controllers/subsystem/garbage.dm:154  var/static/lastlevel (in HandleQueue; rules 10.6)
    private static ref object HandleQueue__lastlevel => ref TgState.Current.datum_controller_subsystem_garbage__HandleQueue__lastlevel;
    // tg: code/controllers/subsystem/garbage.dm:155  var/static/count = 0 (in HandleQueue; rules 10.6)
    private static ref float HandleQueue__count => ref TgState.Current.datum_controller_subsystem_garbage__HandleQueue__count;

    // 1 from the hard reference in the queue, and 1 from the variable used before this
    // tg: code/controllers/subsystem/garbage.dm:165  #define REFS_WE_EXPECT 2 (file-local, #undef at :264); it is read only by
    // the refcount() branch of HandleQueue, which is not translated (DV7).
    private const int REFS_WE_EXPECT = 2;

    // tg: code/controllers/subsystem/garbage.dm:266
    public virtual void Queue(datum D = null, float? level = null)
    {
        level ??= GC_QUEUE_FILTER;
        if (D == null)
            return;
        if (Dm.num(level) > GC_QUEUE_COUNT)
        {
            HardDelete(D);
            return;
        }
        float queue_time = world.time;

        if (Dm.le(D.gc_destroyed, 0))
            D.gc_destroyed = queue_time;

        // DEVIATION(DV7): the GC queue is not ported: instead of appending list(queue_time, D, D.gc_destroyed) to
        // queues[level] (garbage.dm:277-278), the datum leaves the world registry (object-model.md section 7).
        World.Current.Objects.Unregister(D.dm_id);
    }

    //this is mainly to separate things profile wise.
    // tg: code/controllers/subsystem/garbage.dm:281
    public virtual void HardDelete(datum D = null)
    {
        ++delslasttick;
        ++totaldels;
        object type = D.type;
        string refID = text_ref(D);
        datum_qdel_item type_info = items[type] as datum_qdel_item;
        object detail = D.dump_harddel_info();
        if (Dm.truthy(detail))
            LAZYADD(ref type_info.extra_details, detail);

        float tick_usage = world.tick_usage;
        // del(D): BYOND's del of a client calls its Del, which disconnects the player (the /client engine part,
        // Builtin/client.builtin.cs; client/Destroy returns QDEL_HINT_HARDDEL_NOW for this, client_procs.dm:659).
        if (D is client deleted_client)
            deleted_client.dm_del();
        // DEVIATION(DV7): del(D) is not ported otherwise: the datum only leaves the world registry; references to it stay valid.
        // BYOND's del nulls every reference, so a later qdel of a stale reference is qdel(null) and returns; the port
        // marks the datum deleted at this time instead (as Queue does), so QDELETED holds and a later qdel returns
        // quietly instead of crashing on GC_CURRENTLY_BEING_QDELETED.
        World.Current.Objects.Unregister(D.dm_id);
        D.gc_destroyed = world.time;
        tick_usage = TICK_USAGE_TO_MS(tick_usage);

        type_info.hard_deletes++;
        type_info.hard_delete_time += tick_usage;
        if (tick_usage > type_info.hard_delete_max)
            type_info.hard_delete_max = tick_usage;
        if (tick_usage > highest_del_ms)
        {
            highest_del_ms = tick_usage;
            highest_del_type_string = Dm.text("[]", type);
        }

        float time = MS2DS(tick_usage);

        if (time > 0.1f * SECONDS)
            postpone(time);
        // DEVIATION(DV7): the overrun checks against CONFIG_GET(number/hard_deletes_overrun_threshold) and
        // CONFIG_GET(number/hard_deletes_overrun_limit) with their log_game/message_admins warnings (garbage.dm:307-316)
        // are not ported: hard-delete detection does not exist, and a hard delete takes no time here (tick usage is
        // virtual and always 0, DV3), so time is 0 and the checks could never trip.
    }

    // tg: code/controllers/subsystem/garbage.dm:318
    public override void Recover()
    {
        Dm.NotTranslated("code/controllers/subsystem/garbage.dm:318", "Recover: not translated (translation-rules.md 13)");
    }
}

/// Qdel Item: Holds statistics on each type that passes thru qdel
// tg: code/controllers/subsystem/garbage.dm:325
[DmPath("/datum/qdel_item")]
public partial class datum_qdel_item : datum
{
    // tg: code/controllers/subsystem/garbage.dm:326
    public string name = ""; //!Holds the type as a string for this type
    // tg: code/controllers/subsystem/garbage.dm:327
    public float qdels = 0; //!Total number of times it's passed thru qdel.
    // tg: code/controllers/subsystem/garbage.dm:328
    public float destroy_time = 0; //!Total amount of milliseconds spent processing this type's Destroy()
    // tg: code/controllers/subsystem/garbage.dm:329
    public float failures = 0; //!Times it was queued for soft deletion but failed to soft delete.
    // tg: code/controllers/subsystem/garbage.dm:330
    public float hard_deletes = 0; //!Different from failures because it also includes QDEL_HINT_HARDDEL deletions
    // tg: code/controllers/subsystem/garbage.dm:331
    public float hard_delete_time = 0; //!Total amount of milliseconds spent hard deleting this type.
    // tg: code/controllers/subsystem/garbage.dm:332
    public float hard_delete_max = 0; //!Highest time spent hard_deleting this in ms.
    // tg: code/controllers/subsystem/garbage.dm:333
    public float hard_deletes_over_threshold = 0; //!Number of times hard deletes took longer than the configured threshold
    // tg: code/controllers/subsystem/garbage.dm:334
    public float no_respect_force = 0; //!Number of times it's not respected force=TRUE
    // tg: code/controllers/subsystem/garbage.dm:335
    public float no_hint = 0; //!Number of times it's not even bother to give a qdel hint
    // tg: code/controllers/subsystem/garbage.dm:336
    public float slept_destroy = 0; //!Number of times it's slept in its destroy
    // tg: code/controllers/subsystem/garbage.dm:337
    public float qdel_flags = 0; //!Flags related to this type's trip thru qdel.
    // tg: code/controllers/subsystem/garbage.dm:338
    public DmList extra_details; //!Lazylist of string metadata about the deleted objects

    // tg: code/controllers/subsystem/garbage.dm:340
    [DmParams("mytype")]
    public override async DmTask New(params object[] __extra)
    {
        object mytype = Dm.arg(__extra, 0);
        // The name of a non-atom datum is not player-visible: Dm.text (rules 5.3).
        name = Dm.text("[]", mytype);
    }
}

public static partial class GlobalProcs
{
    /// tg <c>SUBSYSTEM_DEF(garbage)</c>: <c>GLOBAL_REAL(SSgarbage, /datum/controller/subsystem/garbage)</c> (rules 10.6).
    // tg: code/controllers/subsystem/garbage.dm:24
    public static ref datum_controller_subsystem_garbage SSgarbage => ref TgState.Current.SSgarbage;

    /// Should be treated as a replacement for the 'del' keyword.
    ///
    /// Datums passed to this will be given a chance to clean up references to allow the GC to collect them.
    // tg: code/controllers/subsystem/garbage.dm:346
    public static void qdel(object to_delete = null, float? force = null)
    {
        force ??= FALSE;
        // BYOND's /image is a /datum (OpenDream's Image.dm), so tg qdels images as datums (an antag HUD's, antag_hud.dm:54): /datum/Destroy
        // has nothing to clean on the port's image (no tag, signals or components) and tg defines no /image/Destroy; the garbage
        // subsystem's later hard delete of a still-referenced one is DV7's
        if (to_delete is image)
            return;
        if (!(to_delete is datum))
        {
            if (to_delete == null)
                return;
            else if (islist(to_delete))
                stack_trace("Lists should not be directly passed to qdel! You likely want either list.Cut(), QDEL_LIST(list), QDEL_LIST_ASSOC(list), or QDEL_LIST_ASSOC_VAL(list)", "code/controllers/subsystem/garbage.dm", 351);
            else if (!Dm.eq(to_delete, world))
                stack_trace(Dm.text("Tried to qdel possibly invalid value: []", to_delete), "code/controllers/subsystem/garbage.dm", 353);
            // tg: #ifndef DISABLE_DREAMLUAU ... #endif not translated (inactive in production)
            Dm.NotTranslated("code/controllers/subsystem/garbage.dm:357", "del(to_delete): hard delete is not translated (object-model.md section 7, DV7)");
            return;
        }

        datum_qdel_item trash = SSgarbage.items[((datum)to_delete).type] as datum_qdel_item;
        if (trash == null)
            trash = (SSgarbage.items[((datum)to_delete).type] = Dm.@new<datum_qdel_item>(((datum)to_delete).type)) as datum_qdel_item;
        trash.qdels++;

        if (((datum)to_delete).gc_destroyed != null)
        {
            if (Dm.eq(((datum)to_delete).gc_destroyed, GC_CURRENTLY_BEING_QDELETED))
                throw Dm.CRASH(Dm.text("[] destroy proc was called multiple times, likely due to a qdel loop in the Destroy logic", ((datum)to_delete).type), "code/controllers/subsystem/garbage.dm", 367);
            return;
        }

        if (Dm.truthy((!Dm.truthy(((datum)to_delete)._listen_lookup?[COMSIG_PREQDELETED]) ? NONE : ((datum)to_delete)._SendSignal(COMSIG_PREQDELETED, Dm.list(to_delete, force))))) // Give the components a chance to prevent their parent from being deleted
            return;

        ((datum)to_delete).gc_destroyed = GC_CURRENTLY_BEING_QDELETED;
        float start_time = world.time;
        float start_tick = world.tick_usage;
        if (Dm.truthy(((datum)to_delete)._listen_lookup?[COMSIG_QDELETING]))
            ((datum)to_delete)._SendSignal(COMSIG_QDELETING, Dm.list(to_delete, force)); // Let the (remaining) components know about the result of Destroy
        // In DM a runtime in Destroy ends only Destroy, whose '.' is still null, and qdel carries on with a null hint
        // (no_hint, Queue). Without the boundary the runtime would unwind out of qdel and leave the datum registered and
        // marked GC_CURRENTLY_BEING_QDELETED (DV1).
        object hint = null; // Let our friend know they're about to get fucked up.
        try { hint = ((datum)to_delete).Destroy(force); }
        catch (Exception e) { World.Current.ReportRuntime(e); hint = null; } // isolation boundary (translation-rules.md 9.2)

        if (world.time != start_time)
            trash.slept_destroy++;
        else
            trash.destroy_time += TICK_USAGE_TO_MS(start_tick);

        if (to_delete == null)
            return;

        object sw__1 = hint;
        if (Dm.eq(sw__1, QDEL_HINT_QUEUE)) //qdel should queue the object for deletion.
            SSgarbage.Queue(to_delete as datum);
        else if (Dm.eq(sw__1, QDEL_HINT_IWILLGC))
        {
            ((datum)to_delete).gc_destroyed = world.time;
            World.Current.Objects.Unregister(((datum)to_delete).dm_id); // DEVIATION(DV7): the datum leaves the world registry (object-model.md section 7)
            return;
        }
        else if (Dm.eq(sw__1, QDEL_HINT_LETMELIVE)) //qdel should let the object live after calling destory.
        {
            if (!Dm.truthy(force))
            {
                ((datum)to_delete).gc_destroyed = null; //clear the gc variable (important!)
                return;
            }
            // Returning LETMELIVE after being told to force destroy
            // indicates the objects Destroy() does not respect force
            // tg: #ifdef TESTING ... #endif not translated (inactive in production)
            trash.no_respect_force++;

            SSgarbage.Queue(to_delete as datum);
        }
        else if (Dm.eq(sw__1, QDEL_HINT_HARDDEL)) //qdel should assume this object won't gc, and queue a hard delete
            SSgarbage.Queue(to_delete as datum, GC_QUEUE_HARDDELETE);
        else if (Dm.eq(sw__1, QDEL_HINT_HARDDEL_NOW)) //qdel should assume this object won't gc, and hard del it post haste.
            SSgarbage.HardDelete(to_delete as datum);
        // tg: #ifdef REFERENCE_TRACKING ... #endif not translated (inactive in production)
        else
        {
            // tg: #ifdef TESTING ... #endif not translated (inactive in production)
            trash.no_hint++;
            SSgarbage.Queue(to_delete as datum);
        }
    }
}

public sealed partial class TgState
{
    /// tg <c>SUBSYSTEM_DEF(garbage)</c>: <c>GLOBAL_REAL(SSgarbage, /datum/controller/subsystem/garbage)</c>.
    // tg: code/controllers/subsystem/garbage.dm:24
    // Internal, with the public accessor GlobalProcs.SSgarbage (rules 10.6).
    internal datum_controller_subsystem_garbage SSgarbage;
    // tg: code/controllers/subsystem/garbage.dm:154-155  var/static/lastlevel, var/static/count = 0 (in HandleQueue)
    internal object datum_controller_subsystem_garbage__HandleQueue__lastlevel;
    internal float datum_controller_subsystem_garbage__HandleQueue__count = 0;
}
