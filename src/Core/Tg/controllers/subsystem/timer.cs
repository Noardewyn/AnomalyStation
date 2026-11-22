// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/timer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * # Timer Subsystem
 *
 * Handles creation, callbacks, and destruction of timed events.
 *
 * It is important to understand the buckets used in the timer subsystem are just a series of doubly-linked
 * lists. The object at a given index in bucket_list is a /datum/timedevent, the head of a list, which has prev
 * and next references for the respective elements in that bucket's list.
 */
// tg: code/controllers/subsystem/timer.dm:17 SUBSYSTEM_DEF(timer)
[DmPath("/datum/controller/subsystem/timer")]
public partial class datum_controller_subsystem_timer : datum_controller_subsystem
{
    // The three file-local #defines of timer.dm (#undef at :728-730, rules 11.4). They are members, as object-like and
    // function-like macros that 11.1 does not expand inline are (REALTIMEOFDAY, TICK_LIMIT_RUNNING).

    /// Controls how many buckets should be kept, each representing a tick. (1 minutes worth)
    // tg: code/controllers/subsystem/timer.dm:2  #define BUCKET_LEN (world.fps*1*60)
    internal static float BUCKET_LEN() => (world.fps * 1 * 60);

    /// Helper for getting the correct bucket for a given timer
    // tg: code/controllers/subsystem/timer.dm:4  #define BUCKET_POS(timer) (((ROUND_UP((timer.timeToRun - timer.timer_subsystem.head_offset) / world.tick_lag)+1) % BUCKET_LEN) || BUCKET_LEN)
    internal static float BUCKET_POS(object timer) => ((Dm.mod(ROUND_UP(Dm.num(Dm.div(Dm.sub(((datum_timedevent)timer).timeToRun, ((datum_timedevent)timer).timer_subsystem.head_offset), world.tick_lag))) + 1, BUCKET_LEN()) is var or__1 && Dm.truthy(or__1) ? or__1 : BUCKET_LEN()));

    /// Gets the maximum time at which timers will be invoked from buckets, used for deferring to secondary queue
    // tg: code/controllers/subsystem/timer.dm:6  #define TIMER_MAX(timer_ss) (timer_ss.head_offset + TICKS2DS(BUCKET_LEN + timer_ss.practical_offset - 1))
    internal static float TIMER_MAX(object timer_ss) => (((datum_controller_subsystem_timer)timer_ss).head_offset + TICKS2DS(BUCKET_LEN() + ((datum_controller_subsystem_timer)timer_ss).practical_offset - 1));

    // tg: code/controllers/subsystem/timer.dm:18
    public datum_controller_subsystem_timer()
    {
        name = "Timer";
        wait = 1; // SS_TICKER subsystem, so wait is in ticks
        priority = FIRE_PRIORITY_TIMER;
        ss_flags = Dm.bitor(SS_TICKER, SS_NO_INIT);
    }

    /// Queue used for storing timers that do not fit into the current buckets
    // tg: code/controllers/subsystem/timer.dm:24
    public DmList second_queue;
    /// A hashlist dictionary used for storing unique timers
    // tg: code/controllers/subsystem/timer.dm:26
    public DmList hashes;
    /// world.time of the first entry in the bucket list, effectively the 'start time' of the current buckets
    // tg: code/controllers/subsystem/timer.dm:28
    public float head_offset = 0;
    /// Index of the wrap around pivot for buckets. buckets before this are later running buckets wrapped around from the end of the bucket list.
    // tg: code/controllers/subsystem/timer.dm:30
    public float practical_offset = 1;
    /// world.tick_lag the bucket was designed for
    // tg: code/controllers/subsystem/timer.dm:32  (float?: tg assigns null to it, :164, :200, :207, rules 3.2 V4)
    public float? bucket_resolution = 0;
    /// How many timers are in the buckets
    // tg: code/controllers/subsystem/timer.dm:34
    public float bucket_count = 0;
    /// List of buckets, each bucket holds every timer that has to run that byond tick
    // tg: code/controllers/subsystem/timer.dm:36
    public DmList bucket_list;
    /// List of all active timers associated to their timer ID (for easy lookup)
    // tg: code/controllers/subsystem/timer.dm:38
    public DmList timer_id_dict;
    /// Special timers that run in real-time, not BYOND time; these are more expensive to run and maintain
    // tg: code/controllers/subsystem/timer.dm:40
    public DmList clienttime_timers;
    /// Contains the last time that a timer's callback was invoked, or the last tick the SS fired if no timers are being processed
    // tg: code/controllers/subsystem/timer.dm:42
    public float last_invoke_tick = 0;
    /// Keeps track of the next index to work on for client timers
    // tg: code/controllers/subsystem/timer.dm:44
    public float next_clienttime_timer_index = 0;
    /// Contains the last time that a warning was issued for not invoking callbacks
    // tg: code/controllers/subsystem/timer.dm:46  var/static/last_invoke_warning = 0 (rules 10.6: a TgState field)
    public static ref float last_invoke_warning => ref TgState.Current.datum_controller_subsystem_timer__last_invoke_warning;
    /// Boolean operator controlling if the timer SS will automatically reset buckets if it fails to invoke callbacks for an extended period of time
    // tg: code/controllers/subsystem/timer.dm:48  var/static/bucket_auto_reset = TRUE (rules 10.6: a TgState field)
    public static ref float bucket_auto_reset => ref TgState.Current.datum_controller_subsystem_timer__bucket_auto_reset;
    /// How many times bucket was reset
    // tg: code/controllers/subsystem/timer.dm:50
    public float bucket_reset_count = 0;

    // tg: code/controllers/subsystem/timer.dm:24, :26, :36, :38, :40 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        second_queue = Dm.list();
        hashes = Dm.list();
        bucket_list = Dm.list();
        timer_id_dict = Dm.list();
        clienttime_timers = Dm.list();
    }

    // The New() that SUBSYSTEM_DEF(timer) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SStimer)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/timer.dm:17
    public override async DmTask New(params object[] __extra)
    {
        if (SStimer != this)
        {
            if (SStimer is datum_controller_subsystem_timer)
            {
                Recover();
                qdel(SStimer);
            }
            SStimer = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/timer.dm:52
    public override void PreInit()
    {
        bucket_list.len = BUCKET_LEN();
        head_offset = world.time;
        bucket_resolution = world.tick_lag;
    }

    // tg: code/controllers/subsystem/timer.dm:57
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/timer.dm:57", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/timer.dm:61
    public virtual void dump_timer_buckets(float? full = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/timer.dm:61", "dump_timer_buckets: the timer debug dump is not translated (only reached when no timer ran for 1.5 minutes)");
    }

    // tg: code/controllers/subsystem/timer.dm:85
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        // Store local references to datum vars as it is faster to access them
        float lit = last_invoke_tick;
        DmList bucket_list = this.bucket_list;
        float last_check = world.time - TICKS2DS(BUCKET_LEN() * 1.5f);

        // If there are no timers being tracked, then consider now to be the last invoked time
        if (!Dm.truthy(bucket_count))
            last_invoke_tick = world.time;

        // Check that we have invoked a callback in the last 1.5 minutes of BYOND time,
        // and throw a warning and reset buckets if this is true
        if (Dm.truthy(lit) && lit < last_check && head_offset < last_check && last_invoke_warning < last_check)
        {
            last_invoke_warning = world.time;
            string msg = Dm.text("No regular timers processed in the last [] ticks[]!", BUCKET_LEN() * 1.5f, (Dm.truthy(bucket_auto_reset) ? ", resetting buckets" : ""));
            message_admins(msg);
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", msg, "code/controllers/subsystem/timer.dm", 101, this, usr));
            if (Dm.truthy(bucket_auto_reset))
                bucket_resolution = 0;
            dump_timer_buckets(Dm.num_or_null(config.Get(typeof(datum_config_entry_flag_log_timers_on_bucket_reset))));
        }

        // Process client-time timers
        if (Dm.truthy(next_clienttime_timer_index))
        {
            clienttime_timers.Cut(1, next_clienttime_timer_index + 1);
            next_clienttime_timer_index = 0;
        }
        for (float next_clienttime_timer_index__it = 1, next_clienttime_timer_index__end = length(clienttime_timers); next_clienttime_timer_index__it <= next_clienttime_timer_index__end; next_clienttime_timer_index__it++)
        {
            next_clienttime_timer_index = next_clienttime_timer_index__it;
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0))
            {
                next_clienttime_timer_index--;
                break;
            }
            datum_timedevent ctime_timer = clienttime_timers[next_clienttime_timer_index] as datum_timedevent;
            if (Dm.gt(ctime_timer.timeToRun, REALTIMEOFDAY()))
            {
                next_clienttime_timer_index--;
                break;
            }

            datum_callback callBack = ctime_timer.callBack;
            if (!Dm.truthy(callBack))
                throw Dm.CRASH(Dm.text("Invalid timer: [] world.time: [], head_offset: [], practical_offset: [], REALTIMEOFDAY: []", get_timer_debug_string(ctime_timer), world.time, head_offset, practical_offset, REALTIMEOFDAY()), "code/controllers/subsystem/timer.dm", 121);

            ctime_timer.spent = REALTIMEOFDAY();
            try { callBack.InvokeAsync(); }
            catch (Exception e) { World.Current.ReportRuntime(e); } // isolation boundary (translation-rules.md 9.2)

            if (Dm.truthy(Dm.bitand(ctime_timer.flags, TIMER_LOOP))) // Re-insert valid looping client timers into the client timer list.
            {
                if (QDELETED(ctime_timer)) // Don't re-insert timers deleted inside their callbacks.
                    continue;
                ctime_timer.spent = 0;
                ctime_timer.timeToRun = Dm.add(REALTIMEOFDAY(), ctime_timer.wait);
                // BINARY_INSERT(ctime_timer, clienttime_timers, /datum/timedevent, ctime_timer, timeToRun, COMPARE_KEY)
                // (__HELPERS/_lists.dm:142, inline by rules 11.1 (a), (b))
                do
                {
                    DmList __BIN_LIST = clienttime_timers;
                    float __BIN_CTTL = length(__BIN_LIST);
                    if (!Dm.truthy(__BIN_CTTL))
                        Dm.append(ref __BIN_LIST, ctime_timer);
                    else
                    {
                        float __BIN_LEFT = 1;
                        float __BIN_RIGHT = __BIN_CTTL;
                        float __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                        datum_timedevent __BIN_ITEM = null;
                        while (__BIN_LEFT < __BIN_RIGHT)
                        {
                            __BIN_ITEM = __BIN_LIST[__BIN_MID] as datum_timedevent;
                            if (Dm.le(__BIN_ITEM.timeToRun, ctime_timer.timeToRun))
                                __BIN_LEFT = __BIN_MID + 1;
                            else
                                __BIN_RIGHT = __BIN_MID;
                            __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                        }
                        __BIN_ITEM = __BIN_LIST[__BIN_MID] as datum_timedevent;
                        __BIN_MID = (Dm.gt(__BIN_ITEM.timeToRun, ctime_timer.timeToRun) ? __BIN_MID : __BIN_MID + 1);
                        __BIN_LIST.Insert(__BIN_MID, ctime_timer);
                    }
                }
                while (Dm.truthy(FALSE));
            }
            else
                qdel(ctime_timer);
        }

        // Remove invoked client-time timers
        if (Dm.truthy(next_clienttime_timer_index))
        {
            clienttime_timers.Cut(1, next_clienttime_timer_index + 1);
            next_clienttime_timer_index = 0;
        }

        // Check for when we need to loop the buckets, this occurs when
        // the head_offset is approaching BUCKET_LEN ticks in the past
        if (practical_offset > BUCKET_LEN())
        {
            head_offset += TICKS2DS(BUCKET_LEN());
            practical_offset = 1;
            resumed = FALSE;
        }

        // Check for when we have to reset buckets, typically from auto-reset
        if ((length(bucket_list) != BUCKET_LEN()) || (!Dm.eq(world.tick_lag, bucket_resolution)))
        {
            reset_buckets();
            bucket_list = this.bucket_list;
            resumed = FALSE;
        }


        // Iterate through each bucket starting from the practical offset
        while (practical_offset <= BUCKET_LEN() && head_offset + ((practical_offset - 1) * world.tick_lag) <= world.time)
        {
            datum_timedevent timer = null;
            while (Dm.truthy((timer = bucket_list[practical_offset] as datum_timedevent)))
            {
                datum_callback callBack = timer.callBack;
                if (!Dm.truthy(callBack))
                {
                    stack_trace(Dm.text("Invalid timer: [] world.time: [], head_offset: [], practical_offset: [], bucket_joined: []", get_timer_debug_string(timer), world.time, head_offset, practical_offset, timer.bucket_joined), "code/controllers/subsystem/timer.dm", 161);
                    if (!Dm.truthy(timer.spent))
                    {
                        bucket_resolution = null; // force bucket recreation
                        return;
                    }
                }

                timer.bucketEject(); //pop the timer off of the bucket list.

                // Invoke callback if possible
                if (!Dm.truthy(timer.spent))
                {
                    timer.spent = world.time;
                    // PORT(measurement, M3 I1b): the host's profiler times each callback, named by its object's type and proc
                    // (World.Profiler, outside the world).
                    IStepProfiler profiler = World.Current.Profiler;
                    string profileName = profiler != null ? "timer " + TimerProfileName(callBack) : null;
                    profiler?.Begin(profileName);
                    try { callBack.InvokeAsync(); }
                    catch (Exception e) { World.Current.ReportRuntime(e); } // isolation boundary (translation-rules.md 9.2)
                    finally { profiler?.End(profileName); }
                    last_invoke_tick = world.time;
                }

                if (Dm.truthy(Dm.bitand(timer.flags, TIMER_LOOP))) // Prepare valid looping timers to re-enter the queue
                {
                    if (QDELETED(timer)) // If a loop is deleted in its callback, we need to avoid re-inserting it.
                        continue;
                    timer.spent = 0;
                    timer.timeToRun = Dm.add(world.time, timer.wait);
                    timer.bucketJoin();
                }
                else
                    qdel(timer);

                if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0))
                    break;
            }

            if (!Dm.truthy(bucket_list[practical_offset]))
            {
                // Empty the bucket, check if anything in the secondary queue should be shifted to this bucket
                bucket_list[practical_offset] = null; // Just in case
                practical_offset++;
                float i = 0;
                for (float i__it = 1, i__end = length(second_queue); i__it <= i__end; i__it++)
                {
                    i = i__it;
                    timer = second_queue[i] as datum_timedevent;
                    if (Dm.ge(timer.timeToRun, TIMER_MAX(this)))
                    {
                        i--;
                        break;
                    }

                    // Check for timers that are scheduled to run in the past
                    if (Dm.lt(timer.timeToRun, head_offset))
                    {
                        bucket_resolution = null; // force bucket recreation
                        stack_trace(Dm.text("[] Invalid timer state: Timer in long run queue with a time to run less then head_offset. [] world.time: [], head_offset: [], practical_offset: []", i, get_timer_debug_string(timer), world.time, head_offset, practical_offset), "code/controllers/subsystem/timer.dm", 201);
                        break;
                    }

                    // Check for timers that are not capable of being scheduled to run without rebuilding buckets
                    if (Dm.lt(timer.timeToRun, head_offset + TICKS2DS(practical_offset - 1)))
                    {
                        bucket_resolution = null; // force bucket recreation
                        stack_trace(Dm.text("[] Invalid timer state: Timer in long run queue that would require a backtrack to transfer to short run queue. [] world.time: [], head_offset: [], practical_offset: []", i, get_timer_debug_string(timer), world.time, head_offset, practical_offset), "code/controllers/subsystem/timer.dm", 208);
                        break;
                    }

                    timer.bucketJoin();
                }
                if (Dm.truthy(i))
                    second_queue.Cut(1, i + 1);
            }
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0))
                break;
        }
    }

    /**
     * Generates a string with details about the timed event for debugging purposes
     */
    // tg: code/controllers/subsystem/timer.dm:221
    public virtual object get_timer_debug_string(datum_timedevent TE = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/timer.dm:221", "get_timer_debug_string: the timer debug text is not translated (only the invalid-timer errors build it)");
        return null;
    }

    /**
     * Destroys the existing buckets and creates new buckets from the existing timed events
     */
    // tg: code/controllers/subsystem/timer.dm:234
    public virtual void reset_buckets()
    {
        warning(Dm.text("[] in [] at line [] src: [] usr: [].", "Timer buckets has been reset, this may cause timer to lag", "code/controllers/subsystem/timer.dm", 235, this, usr));
        bucket_reset_count++;

        DmList bucket_list = this.bucket_list; // Store local reference to datum var, this is faster
        DmList alltimers = Dm.list();

        // Get all timers currently in the buckets
        foreach (object bucket_head in Dm.each(bucket_list))
        {
            if (!Dm.truthy(bucket_head)) // if bucket is empty for this tick
                continue;
            datum_timedevent bucket_node = bucket_head as datum_timedevent;
            do
            {
                Dm.append(ref alltimers, bucket_node);
                bucket_node = bucket_node.next;
            }
            while (Dm.truthy(bucket_node) && !Dm.eq(bucket_node, bucket_head));
        }

        // Empty the list by zeroing and re-assigning the length
        bucket_list.len = 0;
        bucket_list.len = BUCKET_LEN();

        // Reset values for the subsystem to their initial values
        practical_offset = 1;
        bucket_count = 0;
        head_offset = world.time;
        bucket_resolution = world.tick_lag;

        // Add all timed events from the secondary queue as well
        Dm.append(ref alltimers, second_queue);

        foreach (datum_timedevent t in Dm.each_cast<datum_timedevent>(alltimers))
        {
            t.timer_subsystem = this; // Recovered timers need to be reparented
            t.bucket_joined = FALSE;
            t.bucket_pos = -1;
            t.prev = null;
            t.next = null;
        }

        // If there are no timers being tracked by the subsystem,
        // there is no need to do any further rebuilding
        if (!Dm.truthy(length(alltimers)))
            return;

        // Sort all timers by time to run
        sortTim(alltimers, "/proc/" + nameof(GlobalProcs.cmp_timer));

        // Get the earliest timer, and if the TTR is earlier than the current world.time,
        // then set the head offset appropriately to be the earliest time tracked by the
        // current set of buckets
        datum_timedevent head = alltimers[1] as datum_timedevent;
        if (Dm.lt(head.timeToRun, head_offset))
            head_offset = Dm.num(head.timeToRun);

        // Iterate through each timed event and insert it into an appropriate bucket,
        // up unto the point that we can no longer insert into buckets as the TTR
        // is outside the range we are tracking, then insert the remainder into the
        // secondary queue
        float new_bucket_count = 0;
        float i = 1;
        for (float i__it = 1, i__end = length(alltimers); i__it <= i__end; i__it++)
        {
            i = i__it;
            datum_timedevent timer = alltimers[i] as datum_timedevent;
            if (!Dm.truthy(timer))
                continue;

            // Check that the TTR is within the range covered by buckets, when exceeded we've finished
            if (Dm.ge(timer.timeToRun, TIMER_MAX(this)))
            {
                i--;
                break;
            }

            // Check that timer has a valid callback and hasn't been invoked
            if (!Dm.truthy(timer.callBack) || Dm.truthy(timer.spent))
            {
                warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("Invalid timer: [] world.time: [], head_offset: [], practical_offset: []", get_timer_debug_string(timer), world.time, head_offset, practical_offset), "code/controllers/subsystem/timer.dm", 304, this, usr));
                if (Dm.truthy(timer.callBack))
                    qdel(timer);
                continue;
            }

            // Insert the timer into the bucket, and perform necessary doubly-linked list operations
            new_bucket_count++;
            float bucket_pos = BUCKET_POS(timer);
            timer.bucket_pos = bucket_pos;
            timer.bucket_joined = TRUE;

            datum_timedevent bucket_head = bucket_list[bucket_pos] as datum_timedevent;
            if (!Dm.truthy(bucket_head))
            {
                bucket_list[bucket_pos] = timer;
                timer.next = null;
                timer.prev = null;
                continue;
            }

            bucket_head.prev = timer;
            timer.next = bucket_head;
            timer.prev = null;
            bucket_list[bucket_pos] = timer;
        }

        // Cut the timers that are tracked by the buckets from the secondary queue
        if (Dm.truthy(i))
            alltimers.Cut(1, i + 1);
        second_queue = alltimers;
        bucket_count = new_bucket_count;
    }


    // tg: code/controllers/subsystem/timer.dm:335
    public override void Recover()
    {
        Dm.NotTranslated("code/controllers/subsystem/timer.dm:335", "Recover: not translated (translation-rules.md 13)");
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/timer.dm:17  SUBSYSTEM_DEF(timer): GLOBAL_REAL(SStimer, /datum/controller/subsystem/timer)
    internal datum_controller_subsystem_timer SStimer;
    // tg: code/controllers/subsystem/timer.dm:46  var/static/last_invoke_warning = 0
    internal float datum_controller_subsystem_timer__last_invoke_warning = 0;
    // tg: code/controllers/subsystem/timer.dm:48  var/static/bucket_auto_reset = TRUE
    internal float datum_controller_subsystem_timer__bucket_auto_reset = TRUE;
    // tg: code/controllers/subsystem/timer.dm:392  var/static/nextid = 1 (in /datum/timedevent/New)
    internal float datum_timedevent__New__nextid = 1;
    // tg: code/controllers/subsystem/timer.dm:498  var/static/list/bitfield_flags (in /datum/timedevent/proc/operator"")
    internal DmList datum_timedevent__operator__text__bitfield_flags = Dm.list("TIMER_UNIQUE", "TIMER_OVERRIDE", "TIMER_CLIENT_TIME", "TIMER_STOPPABLE", "TIMER_NO_HASH_WAIT", "TIMER_LOOP");
}

/**
 * # Timed Event
 *
 * This is the actual timer, it contains the callback and necessary data to maintain
 * the timer.
 *
 * See the documentation for the timer subsystem for an explanation of the buckets referenced
 * below in next and prev
 */
// tg: code/controllers/subsystem/timer.dm:360
[DmPath("/datum/timedevent")]
public partial class datum_timedevent : datum
{
    /// ID used for timers when the TIMER_STOPPABLE flag is present
    // tg: code/controllers/subsystem/timer.dm:362
    public object id;
    /// The callback to invoke after the timer completes
    // tg: code/controllers/subsystem/timer.dm:364
    public datum_callback callBack;
    /// The time at which the callback should be invoked at
    // tg: code/controllers/subsystem/timer.dm:366
    public object timeToRun;
    /// The length of the timer
    // tg: code/controllers/subsystem/timer.dm:368
    public object wait;
    /// Unique hash generated when TIMER_UNIQUE flag is present
    // tg: code/controllers/subsystem/timer.dm:370
    public object hash;
    /// The source of the timedevent, whatever called addtimer
    // tg: code/controllers/subsystem/timer.dm:372
    public object source;
    /// Flags associated with the timer, see _DEFINES/subsystems.dm
    // tg: code/controllers/subsystem/timer.dm:374
    // tg declares var/list/flags but stores the TIMER_* bit flags (a number) in it and tests it with &: by the "-" cell of
    // rules 3.5 (a number in a DmList var) the var is object.
    public object flags;
    /// Time at which the timer was invoked or destroyed
    // tg: code/controllers/subsystem/timer.dm:376
    public float spent = 0;
    /// Holds info about this timer, stored from the moment it was created
    /// Used to create a visible "name" whenever the timer is stringified
    // tg: code/controllers/subsystem/timer.dm:379
    public DmList timer_info;
    /// Next timed event in the bucket
    // tg: code/controllers/subsystem/timer.dm:381
    public datum_timedevent next;
    /// Previous timed event in the bucket
    // tg: code/controllers/subsystem/timer.dm:383
    public datum_timedevent prev;
    /// The timer subsystem this event is associated with
    // tg: code/controllers/subsystem/timer.dm:385
    public datum_controller_subsystem_timer timer_subsystem;
    /// Boolean indicating if timer joined into bucket
    // tg: code/controllers/subsystem/timer.dm:387
    public float bucket_joined = FALSE;
    /// Initial bucket position
    // tg: code/controllers/subsystem/timer.dm:389
    public float bucket_pos = -1;

    // tg: code/controllers/subsystem/timer.dm:392  var/static/nextid = 1 (rules 10.6: a TgState field)
    private static ref float New__nextid => ref TgState.Current.datum_timedevent__New__nextid;

    // tg: code/controllers/subsystem/timer.dm:498  var/static/list/bitfield_flags (rules 10.6: a TgState field)
    private static ref DmList operator__text__bitfield_flags => ref TgState.Current.datum_timedevent__operator__text__bitfield_flags;

    // tg: code/controllers/subsystem/timer.dm:391
    [DmParams("callBack", "wait", "flags", "timer_subsystem", "hash", "source")]
    public override async DmTask New(params object[] __extra)
    {
        datum_callback callBack = Dm.arg<datum_callback>(__extra, 0);
        object wait = Dm.arg(__extra, 1);
        object flags = Dm.arg(__extra, 2);
        datum_controller_subsystem_timer timer_subsystem = Dm.arg<datum_controller_subsystem_timer>(__extra, 3);
        object hash = Dm.arg(__extra, 4);
        object source = Dm.arg(__extra, 5);
        id = TIMER_ID_NULL;
        this.callBack = callBack;
        this.wait = wait;
        this.flags = flags;
        this.hash = hash;
        this.source = source;
        this.timer_subsystem = (timer_subsystem is var or__1 && Dm.truthy(or__1) ? or__1 : SStimer);

        // Determine time at which the timer's callback should be invoked
        timeToRun = Dm.add((Dm.truthy(Dm.bitand(flags, TIMER_CLIENT_TIME)) ? REALTIMEOFDAY() : world.time), wait);

        // Include the timer in the hash table if the timer is unique
        if (Dm.truthy(Dm.bitand(flags, TIMER_UNIQUE)))
            timer_subsystem.hashes[hash] = this;

        // Generate ID for the timer if the timer is stoppable, include in the timer id dictionary
        if (Dm.truthy(Dm.bitand(flags, TIMER_STOPPABLE)))
        {
            id = num2text(New__nextid, 100);
            if (New__nextid >= SHORT_REAL_LIMIT)
                New__nextid += min(1, Dm.pow(2, round(New__nextid / SHORT_REAL_LIMIT)));
            else
                New__nextid++;
            timer_subsystem.timer_id_dict[id] = this;
        }

        if ((Dm.lt(timeToRun, world.time) || Dm.lt(timeToRun, timer_subsystem.head_offset)) && !Dm.truthy(Dm.bitand(flags, TIMER_CLIENT_TIME)))
            throw Dm.CRASH(Dm.text("Invalid timer state: Timer created that would require a backtrack to run (addtimer would never let this happen): []", SStimer.get_timer_debug_string(this)), "code/controllers/subsystem/timer.dm", 418);

        if (!Dm.eq(callBack.@object, GLOBAL_PROC) && !QDESTROYING(callBack.@object))
            LAZYADD(ref ((datum)callBack.@object)._active_timers, this);

        bucketJoin();
    }

    // tg: code/controllers/subsystem/timer.dm:425
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        base.Destroy(force);
        if (Dm.truthy(Dm.bitand(flags, TIMER_UNIQUE)) && Dm.truthy(hash))
            Dm.remove(ref timer_subsystem.hashes, hash);

        if (Dm.truthy(callBack) && Dm.truthy(callBack.@object) && !Dm.eq(callBack.@object, GLOBAL_PROC) && Dm.truthy(((datum)callBack.@object)._active_timers))
        {
            Dm.remove(ref ((datum)callBack.@object)._active_timers, this);
            UNSETEMPTY(ref ((datum)callBack.@object)._active_timers);
        }

        callBack = null;

        if (Dm.truthy(Dm.bitand(flags, TIMER_STOPPABLE)))
            Dm.remove(ref timer_subsystem.timer_id_dict, id);

        if (Dm.truthy(Dm.bitand(flags, TIMER_CLIENT_TIME)))
        {
            if (!Dm.truthy(spent))
            {
                spent = world.time;
                Dm.remove(ref timer_subsystem.clienttime_timers, this);
            }
            return QDEL_HINT_IWILLGC;
        }

        if (!Dm.truthy(spent))
        {
            spent = world.time;
            bucketEject();
        }
        else
        {
            if (Dm.truthy(prev) && prev.next == this)
                prev.next = next;
            if (Dm.truthy(next) && next.prev == this)
                next.prev = prev;
        }
        next = null;
        prev = null;
        return QDEL_HINT_IWILLGC;
    }

    /**
     * Removes this timed event from any relevant buckets, or the secondary queue
     */
    // tg: code/controllers/subsystem/timer.dm:460
    public virtual void bucketEject()
    {
        // Store local references for the bucket list and secondary queue
        // This is faster than referencing them from the datum itself
        DmList bucket_list = timer_subsystem.bucket_list;
        DmList second_queue = timer_subsystem.second_queue;

        // Attempt to get the head of the bucket
        datum_timedevent buckethead = null;
        if (bucket_pos > 0)
            buckethead = bucket_list[bucket_pos] as datum_timedevent;

        // Decrement the number of timers in buckets if the timed event is
        // the head of the bucket, or has a TTR less than TIMER_MAX implying it fits
        // into an existing bucket, or is otherwise not present in the secondary queue
        if (buckethead == this)
        {
            bucket_list[bucket_pos] = next;
            timer_subsystem.bucket_count--;
        }
        else if (Dm.truthy(bucket_joined))
            timer_subsystem.bucket_count--;
        else
        {
            float l = length(second_queue);
            Dm.remove(ref second_queue, this);
            if (l == length(second_queue))
                timer_subsystem.bucket_count--;
        }

        // Remove the timed event from the bucket, ensuring to maintain
        // the integrity of the bucket's list if relevant
        if (Dm.truthy(prev) && prev.next == this)
            prev.next = next;
        if (Dm.truthy(next) && next.prev == this)
            next.prev = prev;
        prev = next = null;
        bucket_pos = -1;
        bucket_joined = FALSE;
    }

    // tg: code/controllers/subsystem/timer.dm:495  proc/operator"" (rules 7.10)
    public virtual object operator__text()
    {
        if (!Dm.truthy(length(timer_info)))
            return "Event not filled";
        // tg: #if defined(TIMER_DEBUG) ... #else not translated (inactive in production)
        return Dm.text("Timer: [] ([]), TTR: [], wait:[] Flags: [], callBack: [], callBack.object: []([]), callBack.delegate:[], source: []", timer_info[1], text_ref(this), timer_info[2], timer_info[3], jointext(bitfield_to_list(Dm.num_or_null(timer_info[4]), operator__text__bitfield_flags), ", "), text_ref(timer_info[5]), timer_info[6], timer_info[7], timer_info[8], timer_info[9]);
    }

    /**
     * Attempts to add this timed event to a bucket, will enter the secondary queue
     * if there are no appropriate buckets at this time.
     *
     * Secondary queueing of timed events will occur when the timespan covered by the existing
     * buckets is exceeded by the time at which this timed event is scheduled to be invoked.
     * If the timed event is tracking client time, it will be added to a special bucket.
     */
    // tg: code/controllers/subsystem/timer.dm:518
    public virtual void bucketJoin()
    {
        // tg: #if defined(TIMER_DEBUG) ... #else not translated (inactive in production)
        // Generate a debuggable list for the timer, simpler but wayyyy cheaper, string generation (and ref/copy memes) is a bitch and this saves a LOT of time
        timer_info = Dm.list(
            /* 1 = */ id,
            /* 2 = */ timeToRun,
            /* 3 = */ wait,
            /* 4 = */ flags,
            /* 5 = */ callBack, /* Safe to hold this directly because it's never del'd */
            /* 6 = */ Dm.text("[]", callBack.@object),
            /* 7 = */ getcallingtype(),
            /* 8 = */ callBack.@delegate,
            /* 9 = */ Dm.text("[]", source)
        );

        if (Dm.truthy(bucket_joined))
            stack_trace(Dm.text("Bucket already joined! []", this), "code/controllers/subsystem/timer.dm", 550);

        // Check if this timed event should be diverted to the client time bucket, or the secondary queue
        DmList L = null;
        if (Dm.truthy(Dm.bitand(flags, TIMER_CLIENT_TIME)))
            L = timer_subsystem.clienttime_timers;
        else if (Dm.ge(timeToRun, datum_controller_subsystem_timer.TIMER_MAX(timer_subsystem)))
            L = timer_subsystem.second_queue;
        if (Dm.truthy(L))
        {
            // BINARY_INSERT(src, L, /datum/timedevent, src, timeToRun, COMPARE_KEY) (__HELPERS/_lists.dm:142, inline by
            // rules 11.1 (a), (b))
            do
            {
                DmList __BIN_LIST = L;
                float __BIN_CTTL = length(__BIN_LIST);
                if (!Dm.truthy(__BIN_CTTL))
                    Dm.append(ref __BIN_LIST, this);
                else
                {
                    float __BIN_LEFT = 1;
                    float __BIN_RIGHT = __BIN_CTTL;
                    float __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                    datum_timedevent __BIN_ITEM = null;
                    while (__BIN_LEFT < __BIN_RIGHT)
                    {
                        __BIN_ITEM = __BIN_LIST[__BIN_MID] as datum_timedevent;
                        if (Dm.le(__BIN_ITEM.timeToRun, this.timeToRun))
                            __BIN_LEFT = __BIN_MID + 1;
                        else
                            __BIN_RIGHT = __BIN_MID;
                        __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                    }
                    __BIN_ITEM = __BIN_LIST[__BIN_MID] as datum_timedevent;
                    __BIN_MID = (Dm.gt(__BIN_ITEM.timeToRun, this.timeToRun) ? __BIN_MID : __BIN_MID + 1);
                    __BIN_LIST.Insert(__BIN_MID, this);
                }
            }
            while (Dm.truthy(FALSE));
            return;
        }

        // Get a local reference to the bucket list, this is faster than referencing the datum
        DmList bucket_list = timer_subsystem.bucket_list;

        // Find the correct bucket for this timed event
        bucket_pos = datum_controller_subsystem_timer.BUCKET_POS(this);

        if (bucket_pos < timer_subsystem.practical_offset && Dm.lt(timeToRun, (timer_subsystem.head_offset + TICKS2DS(datum_controller_subsystem_timer.BUCKET_LEN()))))
        {
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("Bucket pos in past: bucket_pos = [] < practical_offset = [] && timeToRun = [] < [], Timer: []", bucket_pos, timer_subsystem.practical_offset, timeToRun, timer_subsystem.head_offset + TICKS2DS(datum_controller_subsystem_timer.BUCKET_LEN()), this), "code/controllers/subsystem/timer.dm", 569, this, usr));
            bucket_pos = timer_subsystem.practical_offset; // Recover bucket_pos to avoid timer blocking queue
        }

        datum_timedevent bucket_head = bucket_list[bucket_pos] as datum_timedevent;
        timer_subsystem.bucket_count++;

        // If there is no timed event at this position, then the bucket is 'empty'
        // and we can just set this event to that position
        if (!Dm.truthy(bucket_head))
        {
            bucket_joined = TRUE;
            bucket_list[bucket_pos] = this;
            return;
        }

        // Otherwise, we merely add this timed event into the bucket, which is a
        // doubly-linked list
        bucket_joined = TRUE;
        bucket_head.prev = this;
        next = bucket_head;
        prev = null;
        bucket_list[bucket_pos] = this;
    }

    /**
     * Returns a string of the type of the callback for this timer
     */
    // tg: code/controllers/subsystem/timer.dm:594
    public virtual object getcallingtype()
    {
        object dot = null;
        dot = "ERROR";
        if (Dm.eq(callBack.@object, GLOBAL_PROC))
            dot = "GLOBAL_PROC";
        else
            dot = Dm.text("[]", ((datum)callBack.@object).type);
        return dot;
    }
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_timer SStimer => ref TgState.Current.SStimer;

    /**
     * Create a new timer and insert it in the queue.
     * You should not call this directly, and should instead use the addtimer macro, which includes source information.
     *
     * Arguments:
     * * callback the callback to call on timer finish
     * * wait deciseconds to run the timer for
     * * flags flags for this timer, see: code\__DEFINES\subsystems.dm
     * * timer_subsystem the subsystem to insert this timer into
     */
    // tg: code/controllers/subsystem/timer.dm:611
    public static object _addtimer(datum_callback callback = null, float? wait = null, float? flags = null, datum_controller_subsystem_timer timer_subsystem = null, object file = null, object line = null)
    {
        wait ??= 0;
        flags ??= 0;
        object dot = null;
        if (!(callback is datum_callback)) throw Dm.CRASH(assertion_message("code/controllers/subsystem/timer.dm", 612, "istype(callback)", Dm.text("addtimer called []", (Dm.truthy(callback) ? Dm.text("with an invalid callback ([])", callback) : "without a callback"))), "code/controllers/subsystem/timer.dm", 612);
        if (!(isnum(wait))) throw Dm.CRASH(assertion_message("code/controllers/subsystem/timer.dm", 613, "isnum(wait)", Dm.text("addtimer called with a non-numeric wait ([])", wait)), "code/controllers/subsystem/timer.dm", 613);

        if (Dm.num(wait) < 0)
            stack_trace(Dm.text("addtimer called with a negative wait. Converting to []", world.tick_lag), "code/controllers/subsystem/timer.dm", 616);

        if (!Dm.eq(callback.@object, GLOBAL_PROC) && QDELETED(callback.@object) && !QDESTROYING(callback.@object))
            stack_trace("addtimer called with a callback assigned to a qdeleted object. In the future such timers will not be supported and may refuse to run or run with a 0 wait", "code/controllers/subsystem/timer.dm", 619);

        if (Dm.truthy(Dm.bitand(Dm.num(flags), TIMER_CLIENT_TIME))) // REALTIMEOFDAY has a resolution of 1 decisecond
            wait = max(ceil(Dm.num(wait)), 1); // so if we use tick_lag timers may be inserted in the "past"
        else
            wait = max(CEILING(Dm.num(wait), world.tick_lag), world.tick_lag);

        if (Dm.num(wait) >= INFINITY)
            throw Dm.CRASH("Attempted to create timer with INFINITY delay", "code/controllers/subsystem/timer.dm", 628);

        timer_subsystem = (timer_subsystem is var or__1 && Dm.truthy(or__1) ? or__1 : SStimer);

        // Generate hash if relevant for timed events with the TIMER_UNIQUE flag
        string hash = null;
        if (Dm.truthy(Dm.bitand(Dm.num(flags), TIMER_UNIQUE)))
        {
            // A global proc's callback is owned by the GLOBAL_PROC text, and the port's ref() has no reference for text
            // (DmRefs.RefText: the TRAIT_CALLBACK_REMOVE timers of update_transform and update_offsets threw here and aborted
            // set_body_position of a floating mob: it lay down without its lying angle). BYOND's REF of a text is the text's string table
            // entry, one per value, so the text itself stands for it in the hash; the owner is in the hash by value already.
            object owner_ref = callback.@object is string ? callback.@object : REF(callback.@object);
            DmList hashlist = Dm.list(callback.@object, Dm.text("([])", owner_ref), callback.@delegate, Dm.bitand(Dm.num(flags), TIMER_CLIENT_TIME));
            if (!Dm.truthy(Dm.bitand(Dm.num(flags), TIMER_NO_HASH_WAIT)))
                Dm.append(ref hashlist, wait);
            Dm.append(ref hashlist, callback.arguments);
            hash = hashlist.Join("|||||||");

            datum_timedevent hash_timer = timer_subsystem.hashes[hash] as datum_timedevent;
            if (Dm.truthy(hash_timer))
            {
                if (Dm.truthy(hash_timer.spent)) // it's pending deletion, pretend it doesn't exist.
                    hash_timer.hash = null; // but keep it from accidentally deleting us
                else
                {
                    if (Dm.truthy(Dm.bitand(Dm.num(flags), TIMER_OVERRIDE)))
                    {
                        hash_timer.hash = null; // no need having it delete its hash if we are going to replace it
                        qdel(hash_timer);
                    }
                    else
                    {
                        if (Dm.truthy(Dm.bitand(hash_timer.flags, TIMER_STOPPABLE)))
                            dot = hash_timer.id;
                        return dot;
                    }
                }
            }
        }
        else if (Dm.truthy(Dm.bitand(Dm.num(flags), TIMER_OVERRIDE)))
            stack_trace("TIMER_OVERRIDE used without TIMER_UNIQUE", "code/controllers/subsystem/timer.dm", 654); //this is also caught by grep.

        datum_timedevent timer = Dm.@new<datum_timedevent>(callback, wait, flags, timer_subsystem, hash, (file is var and__1 && !Dm.truthy(and__1) ? and__1 : (object)Dm.text("[]:[]", file, line)));
        return timer.id;
    }

    /**
     * Delete a timer
     *
     * Arguments:
     * * id a timerid or a /datum/timedevent
     */
    // tg: code/controllers/subsystem/timer.dm:665
    public static object deltimer(object id = null, datum_controller_subsystem_timer timer_subsystem = null)
    {
        if (!Dm.truthy(id))
            return FALSE;
        if (Dm.eq(id, TIMER_ID_NULL))
            throw Dm.CRASH("Tried to delete a null timerid. Use TIMER_STOPPABLE flag", "code/controllers/subsystem/timer.dm", 669);
        if (id is datum_timedevent)
        {
            qdel(id);
            return TRUE;
        }
        timer_subsystem = (timer_subsystem is var or__1 && Dm.truthy(or__1) ? or__1 : SStimer);
        //id is string
        datum_timedevent timer = timer_subsystem.timer_id_dict[id] as datum_timedevent;
        if (Dm.truthy(timer) && (!Dm.truthy(timer.spent) || Dm.truthy(Dm.bitand(timer.flags, TIMER_DELETE_ME))))
        {
            qdel(timer);
            return TRUE;
        }
        return FALSE;
    }

    /**
     * Get the remaining deciseconds on a timer
     *
     * Arguments:
     * * id a timerid or a /datum/timedevent
     */
    // tg: code/controllers/subsystem/timer.dm:687
    public static object timeleft(object id = null, datum_controller_subsystem_timer timer_subsystem = null)
    {
        if (!Dm.truthy(id))
            return null;
        if (Dm.eq(id, TIMER_ID_NULL))
            throw Dm.CRASH("Tried to get timeleft of a null timerid. Use TIMER_STOPPABLE flag", "code/controllers/subsystem/timer.dm", 691);
        if (id is datum_timedevent)
        {
            datum_timedevent timer__1 = id as datum_timedevent;
            return Dm.sub(timer__1.timeToRun, world.time);
        }
        timer_subsystem = (timer_subsystem is var or__1 && Dm.truthy(or__1) ? or__1 : SStimer);
        //id is string
        datum_timedevent timer = timer_subsystem.timer_id_dict[id] as datum_timedevent;
        if (!Dm.truthy(timer) || Dm.truthy(timer.spent))
            return null;
        return Dm.sub(timer.timeToRun, (Dm.truthy(Dm.bitand(timer.flags, TIMER_CLIENT_TIME)) ? REALTIMEOFDAY() : world.time));
    }

    /**
     * Update the delay on an existing LOOPING timer
     * Will come into effect on the next process
     *
     * Arguments:
     * * id a timerid or a /datum/timedevent
     * * new_wait the new wait to give this looping timer
     */
    // tg: code/controllers/subsystem/timer.dm:710
    public static void updatetimedelay(object id = null, object new_wait = null, datum_controller_subsystem_timer timer_subsystem = null)
    {
        if (!Dm.truthy(id))
            return;
        if (Dm.eq(id, TIMER_ID_NULL))
            throw Dm.CRASH("Tried to update the wait of null timerid. Use TIMER_STOPPABLE flag", "code/controllers/subsystem/timer.dm", 714);
        if (id is datum_timedevent)
        {
            datum_timedevent timer__1 = id as datum_timedevent;
            timer__1.wait = new_wait;
            return;
        }
        timer_subsystem = (timer_subsystem is var or__1 && Dm.truthy(or__1) ? or__1 : SStimer);
        //id is string
        datum_timedevent timer = timer_subsystem.timer_id_dict[id] as datum_timedevent;
        if (!Dm.truthy(timer) || Dm.truthy(timer.spent))
            return;
        if (!Dm.truthy(Dm.bitand(timer.flags, TIMER_LOOP)))
            throw Dm.CRASH("Tried to update the wait of a non looping timer. This is not supported", "code/controllers/subsystem/timer.dm", 725);
        timer.wait = new_wait;
    }
}

public partial class datum_controller_subsystem_timer
{
    // PORT(measurement, M3 I1b): a timer callback's name for the host's profiler: its object's type and its proc.
    private static string TimerProfileName(datum_callback callBack)
        => (callBack.@object is datum d ? d.GetType().Name : "global") + "." + (callBack.@delegate is string s ? s : callBack.@delegate?.ToString() ?? "?");
}
