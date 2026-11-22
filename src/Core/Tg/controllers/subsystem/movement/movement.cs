// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/movement/movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated (M4b slice 1: the zero-gravity drift's move loop runs on SSnewtonian_movement, a subtype). The var block
// (name, ss_flags = SS_NO_INIT|SS_TICKER, wait = 1, buckets, sorted_buckets, canonical_time, visual_delay) is generated
// (D111). Recover is not translated: it finds the old subsystem through global.vars (rules 10.7, 13).

// tg: code/controllers/subsystem/movement/movement.dm:1 SUBSYSTEM_DEF(movement)
public partial class datum_controller_subsystem_movement : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(movement) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSmovement)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/movement/movement.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSmovement != this)
        {
            if (SSmovement is datum_controller_subsystem_movement)
            {
                Recover();
                qdel(SSmovement);
            }
            SSmovement = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/movement/movement.dm:22
    public override object stat_entry(object msg = null)
    {
        Dm.Deferred("code/controllers/subsystem/movement/movement.dm:22", "stat_entry: the MC stat panel line (stat panels, translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/movement/movement.dm:29
    public override void Recover()
    {
        Dm.NotTranslated("code/controllers/subsystem/movement/movement.dm:29", "Recover: the old subsystem is found through global.vars and return_typenames (rules 10.7, 13)");
    }

    // tg: code/controllers/subsystem/movement/movement.dm:39
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            canonical_time = world.time;

        foreach (DmList bucket_info in Dm.each<DmList>(sorted_buckets))
        {
            float time = Dm.num(bucket_info[MOVEMENT_BUCKET_TIME]);
            if (time > canonical_time || Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
            pour_bucket(bucket_info);
        }
    }

    /// Processes a bucket of movement loops (This should only ever be called by fire(), it exists to prevent runtime fuckery)
    // tg: code/controllers/subsystem/movement/movement.dm:50
    public virtual void pour_bucket(DmList bucket_info = null)
    {
        DmList processing = bucket_info[MOVEMENT_BUCKET_LIST] as DmList; // Cache for lookup speed
        while (Dm.truthy(processing.len))
        {
            datum_move_loop loop = processing[processing.len] as datum_move_loop;
            processing.len--;
            // No longer queued since we just got removed from the loop
            loop.queued_time = null;
            loop.process(); //This shouldn't get nulls, if it does, runtime
            if (!QDELETED(loop) && Dm.truthy(Dm.bitand(loop.status, MOVELOOP_STATUS_QUEUED))) //Re-Insert the loop
            {
                Dm.mask(ref loop.status, Dm.bitnot(MOVELOOP_STATUS_QUEUED));
                loop.timer = world.time + loop.delay;
                queue_loop(loop);
            }
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                break;
        }

        if (Dm.truthy(length(processing)))
            return; // Still work to be done
        float bucket_time = Dm.num(bucket_info[MOVEMENT_BUCKET_TIME]);
        smash_bucket(1, bucket_time); // We assume we're the first bucket in the queue right now
        visual_delay = MC_AVERAGE_FAST(visual_delay, max((world.time - canonical_time) / TICKS2DS(wait), 1));
    }

    /// Removes a bucket from our system. You only need to pass in the time, but if you pass in the index of the list you save us some work
    // tg: code/controllers/subsystem/movement/movement.dm:72
    public virtual void smash_bucket(float? index = null, float? bucket_time = null)
    {
        float sorted_length = length(sorted_buckets);
        if (!Dm.truthy(index))
        {
            index = sorted_length + 1; // let's setup the failure condition
            for (float i = 1; i <= sorted_length; i++)
            {
                DmList bucket_info = sorted_buckets[i] as DmList;
                if (!Dm.eq(bucket_info[MOVEMENT_BUCKET_TIME], bucket_time))
                    continue;
                index = i;
                break;
            }
        }
        //This is technically possible, if our bucket is smashed inside the loop's process
        //Let's be nice, the cost of doing it is cheap
        if (Dm.num(index) > sorted_length || !Dm.truthy(buckets[Dm.text("[]", bucket_time)]))
            return;

        sorted_buckets.Cut(Dm.num(index), Dm.num(index) + 1); //Removes just this list
        //Removes the assoc lookup too
        Dm.remove(ref buckets, Dm.text("[]", bucket_time));
    }

    // tg: code/controllers/subsystem/movement/movement.dm:92
    public virtual void queue_loop(datum_move_loop loop = null)
    {
        if (Dm.truthy(Dm.bitand(loop.status, MOVELOOP_STATUS_QUEUED)))
        {
            stack_trace("A move loop attempted to queue while already queued", "code/controllers/subsystem/movement/movement.dm", 94);
            return;
        }
        loop.queued_time = loop.timer;
        Dm.combine(ref loop.status, MOVELOOP_STATUS_QUEUED);
        DmList our_bucket = buckets[Dm.text("[]", loop.queued_time)] as DmList;
        // If there's no bucket for this, lets set them up
        if (our_bucket == null)
        {
            buckets[Dm.text("[]", loop.queued_time)] = Dm.list();
            our_bucket = buckets[Dm.text("[]", loop.queued_time)] as DmList;
            // This makes assoc buckets and sorted buckets point to the same place, allowing for quicker inserts
            DmList new_bucket = Dm.list(Dm.list(loop.queued_time, our_bucket));
            DmList compare_item = Dm.list(loop.queued_time);
            // BINARY_INSERT_DEFINE(new_bucket, sorted_buckets, SORT_VAR_NO_TYPE, compare_item, SORT_FIRST_INDEX, COMPARE_KEY)
            // (__HELPERS/_lists.dm:216), inline (rules 11.1 (b)): SORT_FIRST_INDEX(list) is list[1], COMPARE_KEY is
            // __BIN_LIST[__BIN_MID]
            DmList __BIN_LIST = sorted_buckets;
            float __BIN_CTTL = length(__BIN_LIST);
            if (__BIN_CTTL == 0)
                Dm.append(ref __BIN_LIST, new_bucket);
            else
            {
                float __BIN_LEFT = 1;
                float __BIN_RIGHT = __BIN_CTTL;
                float __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                object __BIN_ITEM;
                while (__BIN_LEFT < __BIN_RIGHT)
                {
                    __BIN_ITEM = __BIN_LIST[__BIN_MID];
                    if (Dm.le(((DmList)__BIN_ITEM)[1], compare_item[1]))
                        __BIN_LEFT = __BIN_MID + 1;
                    else
                        __BIN_RIGHT = __BIN_MID;
                    __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                }
                __BIN_ITEM = __BIN_LIST[__BIN_MID];
                __BIN_MID = Dm.gt(((DmList)__BIN_ITEM)[1], compare_item[1]) ? __BIN_MID : __BIN_MID + 1;
                __BIN_LIST.Insert(__BIN_MID, new_bucket);
            }
        }

        Dm.append(ref our_bucket, loop);
    }

    // tg: code/controllers/subsystem/movement/movement.dm:111
    public virtual void dequeue_loop(datum_move_loop loop = null)
    {
        // Go home, you're not here anyway
        if (!Dm.truthy(Dm.bitand(loop.status, MOVELOOP_STATUS_QUEUED)))
            return;
        if (loop.queued_time == null) // This happens if a moveloop is dequeued while handling process()
        {
            Dm.mask(ref loop.status, Dm.bitnot(MOVELOOP_STATUS_QUEUED));
            return;
        }
        DmList our_entries = buckets[Dm.text("[]", loop.queued_time)] as DmList;
        Dm.remove(ref our_entries, loop);
        if (!Dm.truthy(length(our_entries)))
            smash_bucket(bucket_time: loop.queued_time); // We can't pass an index in for context because we don't know our position
        loop.queued_time = null;
        Dm.mask(ref loop.status, Dm.bitnot(MOVELOOP_STATUS_QUEUED));
    }

    // tg: code/controllers/subsystem/movement/movement.dm:125
    public virtual void add_loop(datum_move_loop add = null)
    {
        if (Dm.truthy(Dm.bitand(add.status, MOVELOOP_STATUS_QUEUED)))
            throw Dm.CRASH("Loop being added that is already queued.", "code/controllers/subsystem/movement/movement.dm", 127);
        add.loop_started();
        if (QDELETED(add) || Dm.truthy(Dm.bitand(add.status, MOVELOOP_STATUS_QUEUED)))
            return;
        queue_loop(add);
    }

    // tg: code/controllers/subsystem/movement/movement.dm:132
    public virtual void remove_loop(datum_move_loop remove = null)
    {
        dequeue_loop(remove);
        remove.loop_stopped();
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/movement/movement.dm:1  SUBSYSTEM_DEF(movement): GLOBAL_REAL(SSmovement, /datum/controller/subsystem/movement)
    internal datum_controller_subsystem_movement SSmovement;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_movement SSmovement => ref TgState.Current.SSmovement;
}
