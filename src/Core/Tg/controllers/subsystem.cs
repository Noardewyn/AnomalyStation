// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
	* # Subsystem base class
	*
	* Defines a subsystem to be managed by the [Master Controller][/datum/controller/master]
	*
	* Simply define a child of this subsystem, using the [SUBSYSTEM_DEF] macro, and the MC will handle registration.
	* Changing the name is required
**/
// tg: code/controllers/subsystem.dm:10
[DmPath("/datum/controller/subsystem")]
public partial class datum_controller_subsystem : datum_controller
{
    // Metadata; you should define these.

    // tg: code/controllers/subsystem.dm:13
    public datum_controller_subsystem()
    {
        /// Name of the subsystem - you must change this
        name = "fire coderbus";
    }

    /// Determines which subsystems this subsystem is dependant on to initialize. Will initialize after all specified subsystems.
    /// If init_stage is earlier than a dependent subsystem, will throw an error and push the init stage forward to that subsystem.
    /// Usage: Put the typepaths of the subsystems that need to init before this one in this list.
    // tg: code/controllers/subsystem.dm:19
    public DmList dependencies;

    /// The inverse of the dependencies. Can be set manually, but will also get evaluated at runtime. Turns into a list of instances at runtime.
    /// Usage: Put the typepaths of the subsystems that need to init after this one in this list.
    // tg: code/controllers/subsystem.dm:23
    public DmList dependents;

    /// ID of the subsystem. Set automatically when the dependency graph is evaluated. Used primarily in determining order.
    // tg: code/controllers/subsystem.dm:26
    public float ordering_id = 0;

    /// Do not modify. Automatically set when the dependency graph is evaluated. Similar to ordering_id, but evaluated after init_stage.
    // tg: code/controllers/subsystem.dm:29
    public float init_order = 0;

    /// Time to wait (in deciseconds) between each call to fire(). Must be a positive integer.
    // tg: code/controllers/subsystem.dm:32
    public float wait = 20;

    /// Priority Weight: When multiple subsystems need to run in the same tick, higher priority subsystems will be given a higher share of the tick before MC_TICK_CHECK triggers a sleep, higher priority subsystems also run before lower priority subsystems
    // tg: code/controllers/subsystem.dm:35
    public float priority = FIRE_PRIORITY_DEFAULT;

    /// [Subsystem flags][SS_NO_INIT] to control binary behavior. ss_flags must be set at compile time or before preinit finishes to take full effect. (You can also restart the mc to force them to process again)
    // tg: code/controllers/subsystem.dm:38
    public float ss_flags = NONE;

    /// Which stage does this subsystem init at. Earlier stages can fire while later stages init.
    // tg: code/controllers/subsystem.dm:41
    public float init_stage = INITSTAGE_MAIN;

    /// This var is set to `INITIALIZATION_INNEW_REGULAR` after the subsystem has been initialized.
    // tg: code/controllers/subsystem.dm:44
    public float initialized = FALSE;

    /// Set to 0 to prevent fire() calls, mostly for admin use or subsystems that may be resumed later
    /// use the [SS_NO_FIRE] flag instead for systems that never fire to keep it from even being added to list that is checked every tick
    // tg: code/controllers/subsystem.dm:48
    public float can_fire = TRUE;

    ///Bitmap of what game states can this subsystem fire at. See [RUNLEVELS_DEFAULT] for more details.
    // tg: code/controllers/subsystem.dm:51
    public float runlevels = RUNLEVELS_DEFAULT; //points of the game at which the SS can fire

    /**
	 * boolean set by admins. if TRUE then this subsystem will stop the world profiler after ignite() returns and start it again when called.
	 * used so that you can audit a specific subsystem or group of subsystems' synchronous call chain.
	 */
    // tg: code/controllers/subsystem.dm:57
    public float profiler_focused = FALSE;

    /*
	 * The following variables are managed by the MC and should not be modified directly.
	 */

    /// Last world.time the subsystem completed a run (as in wasn't paused by [MC_TICK_CHECK])
    // tg: code/controllers/subsystem.dm:64
    public float last_fire = 0;

    /// Scheduled world.time for next fire()
    // tg: code/controllers/subsystem.dm:67
    public float next_fire = 0;

    /// Running average of the amount of milliseconds it takes the subsystem to complete a run (including all resumes but not the time spent paused)
    // tg: code/controllers/subsystem.dm:70
    public float cost = 0;

    /// Running average of the amount of tick usage in percents of a tick it takes the subsystem to complete a run
    // tg: code/controllers/subsystem.dm:73
    public float tick_usage = 0;

    /// Running average of the amount of tick usage (in percents of a game tick) the subsystem has spent past its allocated time without pausing
    // tg: code/controllers/subsystem.dm:76
    public float tick_overrun = 0;

    /// Flat list of usage and time, every odd index is a log time, every even index is a usage
    // tg: code/controllers/subsystem.dm:79
    public DmList rolling_usage;

    /// How much of a tick (in percents of a tick) were we allocated last fire.
    // tg: code/controllers/subsystem.dm:82
    public float tick_allocation_last = 0;

    /// How much of a tick (in percents of a tick) do we get allocated by the mc on avg.
    // tg: code/controllers/subsystem.dm:85
    public float tick_allocation_avg = 0;

    /// Tracks the current execution state of the subsystem. Used to handle subsystems that sleep in fire so the mc doesn't run them again while they are sleeping
    // tg: code/controllers/subsystem.dm:88
    public float? state = SS_IDLE;

    /// Tracks how many times a subsystem has ever slept in fire().
    // tg: code/controllers/subsystem.dm:91
    public float slept_count = 0;

    /// Tracks how many fires the subsystem has consecutively paused on in the current run
    // tg: code/controllers/subsystem.dm:94
    public float paused_ticks = 0;

    /// Tracks how much of a tick the subsystem has consumed in the current run
    // tg: code/controllers/subsystem.dm:97
    public object paused_tick_usage;

    /// Tracks how many fires the subsystem takes to complete a run on average.
    // tg: code/controllers/subsystem.dm:100
    public float ticks = 1;

    /// Tracks the amount of completed runs for the subsystem
    // tg: code/controllers/subsystem.dm:103
    public float times_fired = 0;

    /// How many fires have we been requested to postpone
    // tg: code/controllers/subsystem.dm:106
    public float postponed_fires = 0;

    /// Time the subsystem entered the queue, (for timing and priority reasons)
    // tg: code/controllers/subsystem.dm:109
    public float? queued_time = 0;

    /// Priority at the time the subsystem entered the queue. Needed to avoid changes in priority (by admins and the like) from breaking things.
    // tg: code/controllers/subsystem.dm:112
    public object queued_priority;

    /// How many times we suspect a subsystem type has crashed the MC, 3 strikes and you're out!
    // tg: code/controllers/subsystem.dm:115  var/static/list/failure_strikes (rules 10.6: a TgState field)
    public static ref DmList failure_strikes => ref TgState.Current.datum_controller_subsystem__failure_strikes;

    /// Next subsystem in the queue of subsystems to run this tick
    // tg: code/controllers/subsystem.dm:118
    public datum_controller_subsystem queue_next;
    /// Previous subsystem in the queue of subsystems to run this tick
    // tg: code/controllers/subsystem.dm:120
    public datum_controller_subsystem queue_prev;

    /// String to store an applicable error message for a subsystem crashing, used to help debug crashes in contexts such as Continuous Integration/Unit Tests
    // tg: code/controllers/subsystem.dm:123
    public object initialization_failure_message = null;

    //Do not blindly add vars here to the bottom, put it where it goes above
    //If your var only has two values, put it in as a flag.

    // tg: code/controllers/subsystem.dm:19, :79 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        dependencies = Dm.list();
        rolling_usage = Dm.list();
    }

    //Do not override
    ///datum/controller/subsystem/New()

    // Used to initialize the subsystem BEFORE the map has loaded
    // Called AFTER Recover if that is called
    // Prefer to use Initialize if possible
    // tg: code/controllers/subsystem.dm:135
    public virtual void PreInit()
    {
        return;
    }

    ///This is used so the mc knows when the subsystem sleeps. do not override.
    // tg: code/controllers/subsystem.dm:139  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4)
    [ShouldNotOverride]
    public virtual object ignite(float? resumed = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ignite__body(resumed, dot), dot);
    }

    // The body of the set waitfor = FALSE wrapper above (waiting-model.md 5.4): its caller (RunQueue) gets '.' as it is
    // at the first sleep of fire(), SS_SLEEPING.
    private async DmTask<object> ignite__body(float? resumed, DotBox<object> dot)
    {
        resumed ??= FALSE;
        dot.Value = SS_IDLE;

        tick_allocation_last = datum_controller_master.current_ticklimit - (TICK_USAGE());
        tick_allocation_avg = MC_AVERAGE(tick_allocation_avg, tick_allocation_last);

        dot.Value = SS_SLEEPING;
        try { await fire(resumed); }
        catch (Exception e) { World.Current.ReportRuntime(e); } // isolation boundary (translation-rules.md 9.2)
        dot.Value = state;
        if (Dm.eq(state, SS_SLEEPING))
        {
            slept_count++;
            state = SS_IDLE;
        }
        if (Dm.eq(state, SS_PAUSING))
        {
            slept_count++;
            object QT = queued_time;
            enqueue();
            state = SS_PAUSED;
            queued_time = Dm.num_or_null(QT);
        }
        return dot.Value;
    }

    ///previously, this would have been named 'process()' but that name is used everywhere for different things!
    ///fire() seems more suitable. This is the procedure that gets called every 'wait' deciseconds.
    ///Sleeping in here prevents future fires until returned.
    // tg: code/controllers/subsystem.dm:163
    public virtual async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        Dm.combine(ref ss_flags, SS_NO_FIRE);
        throw Dm.CRASH(Dm.text("Subsystem []([]) does not fire() but did not set the SS_NO_FIRE flag. Please add the SS_NO_FIRE flag to any subsystem that doesn't fire so it doesn't get added to the processing list and waste cpu.", this, type), "code/controllers/subsystem.dm", 165);
    }

    // tg: code/controllers/subsystem.dm:167
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        dequeue();
        can_fire = 0;
        Dm.combine(ref ss_flags, SS_NO_FIRE);
        if (Dm.truthy(Master))
            Dm.remove(ref Master.subsystems, this);
        return base.Destroy(force);
    }

    /** Update next_fire for the next run.
	 *  reset_time (bool) - Ignore things that would normally alter the next fire, like tick_overrun, and last_fire. (also resets postpone)
	 */
    // tg: code/controllers/subsystem.dm:179
    public virtual void update_nextfire(float? reset_time = null)
    {
        reset_time ??= FALSE;
        float queue_node_flags = ss_flags;

        if (Dm.truthy(reset_time))
        {
            postponed_fires = 0;
            if (Dm.truthy(Dm.bitand(queue_node_flags, SS_TICKER)))
                next_fire = world.time + (world.tick_lag * wait);
            else
                next_fire = world.time + wait;
            return;
        }

        if (Dm.truthy(Dm.bitand(queue_node_flags, SS_TICKER)))
            next_fire = world.time + (world.tick_lag * wait);
        else if (Dm.truthy(Dm.bitand(queue_node_flags, SS_POST_FIRE_TIMING)))
            next_fire = world.time + wait + (world.tick_lag * (tick_overrun / 100));
        else if (Dm.truthy(Dm.bitand(queue_node_flags, SS_KEEP_TIMING)))
            next_fire += wait;
        else
            next_fire = Dm.num(queued_time) + wait + (world.tick_lag * (tick_overrun / 100));
    }

    ///Queue it to run.
    /// (we loop thru a linked list until we get to the end or find the right point)
    /// (this lets us sort our run order correctly without having to re-sort the entire already sorted list)
    // tg: code/controllers/subsystem.dm:203
    public virtual void enqueue()
    {
        float SS_priority = priority;
        float SS_flags = ss_flags;
        datum_controller_subsystem queue_node = null;
        object queue_node_priority = null;
        float queue_node_flags = 0;

        for (queue_node = Master.queue_head; Dm.truthy(queue_node); queue_node = queue_node.queue_next)
        {
            queue_node_priority = queue_node.queued_priority;
            queue_node_flags = queue_node.ss_flags;

            // DM parse: queue_node_flags & ((SS_TICKER|SS_BACKGROUND) == SS_TICKER), since == binds tighter than & (BYOND
            // operator table; docs/ai/specs/m0-reading/controllers.md 3.5): (12 == 8) is 0, so the test is always false and
            // every node takes one of the two branches below (tg quirk, kept: it decides where a new ticker is inserted).
            if (Dm.truthy(Dm.bitand(queue_node_flags, Dm.num(Dm.bitor(SS_TICKER, SS_BACKGROUND) == SS_TICKER))))
            {
                if (Dm.bitand(SS_flags, Dm.bitor(SS_TICKER, SS_BACKGROUND)) != SS_TICKER)
                    continue;
                if (Dm.lt(queue_node_priority, SS_priority))
                    break;
            }

            else if (Dm.truthy(Dm.bitand(queue_node_flags, SS_BACKGROUND)))
            {
                if (!Dm.truthy(Dm.bitand(SS_flags, SS_BACKGROUND)))
                    break;
                if (Dm.lt(queue_node_priority, SS_priority))
                    break;
            }

            else
            {
                if (Dm.truthy(Dm.bitand(SS_flags, SS_BACKGROUND)))
                    continue;
                if (Dm.truthy(Dm.bitand(SS_flags, SS_TICKER)))
                    break;
                if (Dm.lt(queue_node_priority, SS_priority))
                    break;
            }
        }

        queued_time = world.time;
        queued_priority = SS_priority;
        state = SS_QUEUED;
        if (Dm.truthy(Dm.bitand(SS_flags, SS_BACKGROUND))) //update our running total
            Master.queue_priority_count_bg += SS_priority;
        else
            Master.queue_priority_count += SS_priority;

        queue_next = queue_node;
        if (!Dm.truthy(queue_node))//we stopped at the end, add to tail
        {
            queue_prev = Master.queue_tail;
            if (Dm.truthy(Master.queue_tail))
                Master.queue_tail.queue_next = this;
            else //empty queue, we also need to set the head
                Master.queue_head = this;
            Master.queue_tail = this;
        }

        else if (queue_node == Master.queue_head)//insert at start of list
        {
            Master.queue_head.queue_prev = this;
            Master.queue_head = this;
            queue_prev = null;
        }
        else
        {
            queue_node.queue_prev.queue_next = this;
            queue_prev = queue_node.queue_prev;
            queue_node.queue_prev = this;
        }
    }

    // tg: code/controllers/subsystem.dm:261
    public virtual void dequeue()
    {
        if (Dm.truthy(queue_next))
            queue_next.queue_prev = queue_prev;
        if (Dm.truthy(queue_prev))
            queue_prev.queue_next = queue_next;
        if (Dm.truthy(Master) && (this == Master.queue_tail))
            Master.queue_tail = queue_prev;
        if (Dm.truthy(Master) && (this == Master.queue_head))
            Master.queue_head = queue_next;
        queued_time = 0;
        if (Dm.eq(state, SS_QUEUED))
            state = SS_IDLE;
    }

    // tg: code/controllers/subsystem.dm:275
    public virtual object pause()
    {
        object dot = null;
        dot = 1;
        float? sw__1 = state;
        if (Dm.eq(sw__1, SS_RUNNING))
            state = SS_PAUSED;
        else if (Dm.eq(sw__1, SS_SLEEPING))
            state = SS_PAUSING;
        return dot;
    }

    /// Called after the config has been loaded or reloaded.
    // tg: code/controllers/subsystem.dm:284
    public virtual async DmTask OnConfigLoad()
    {
    }

    /**
	 * Used to initialize the subsystem. This is expected to be overridden by subtypes.
	 */
    // tg: code/controllers/subsystem.dm:289
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        return SS_INIT_NONE;
    }

    // tg: code/controllers/subsystem.dm:292
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/subsystem.dm:292", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem.dm:299
    public virtual object state_letter()
    {
        object dot = null;
        float? sw__1 = state;
        if (Dm.eq(sw__1, SS_RUNNING))
            dot = "R";
        else if (Dm.eq(sw__1, SS_QUEUED))
            dot = "Q";
        else if (Dm.eq(sw__1, SS_PAUSED) || Dm.eq(sw__1, SS_PAUSING))
            dot = "P";
        else if (Dm.eq(sw__1, SS_SLEEPING))
            dot = "S";
        else if (Dm.eq(sw__1, SS_IDLE))
            dot = " ";
        return dot;
    }

    /// Causes the next "cycle" fires to be missed. Effect is accumulative but can reset by calling update_nextfire(reset_time = TRUE)
    // tg: code/controllers/subsystem.dm:313
    public virtual void postpone(float? cycles = null)
    {
        cycles ??= 1;
        if (Dm.truthy(can_fire) && Dm.num(cycles) >= 1)
            postponed_fires += Dm.num(cycles);
    }

    /// Prunes out of date entries in our rolling usage list
    // tg: code/controllers/subsystem.dm:318
    public virtual void prune_rolling_usage()
    {
        DmList rolling_usage = this.rolling_usage;
        float cut_to = 0;
        while (cut_to + 2 <= length(rolling_usage) && Dm.lt(rolling_usage[cut_to + 1], DS2TICKS(world.time - Master.rolling_usage_length)))
            cut_to += 2;
        if (Dm.truthy(cut_to))
            rolling_usage.Cut(1, cut_to + 1);
    }

    //usually called via datum/controller/subsystem/New() when replacing a subsystem (i.e. due to a recurring crash)
    //should attempt to salvage what it can from the old instance of subsystem
    // tg: code/controllers/subsystem.dm:328
    public override void Recover()
    {
    }

    // tg: code/controllers/subsystem.dm:330  vv_edit_var(var_name, var_value): not declared, its root /datum/proc/vv_edit_var
    // (datums/datumvars.dm, VV) is not translated yet (rules 13).
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem.dm:115  var/static/list/failure_strikes
    internal DmList datum_controller_subsystem__failure_strikes;
}
