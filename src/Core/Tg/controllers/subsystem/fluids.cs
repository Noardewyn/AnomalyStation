// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/fluids.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * # Fluid Subsystem
 *
 * A subsystem that processes the propagation and effects of a particular fluid.
 *
 * Both fluid spread and effect processing are handled through a carousel system.
 * Fluids being spread and fluids being processed are organized into buckets.
 * Each fresh (non-resumed) fire one bucket of each is selected to be processed.
 * These selected buckets are then fully processed.
 * The next fresh fire selects the next bucket in each set for processing.
 * If this would walk off the end of a carousel list we wrap back to the first element.
 * This effectively makes each set a circular list, hence a carousel.
 */
// tg: code/controllers/subsystem/fluids.dm:20 SUBSYSTEM_DEF(fluids)
[DmPath("/datum/controller/subsystem/fluids")]
public partial class datum_controller_subsystem_fluids : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/fluids.dm:21
    public datum_controller_subsystem_fluids()
    {
        name = "Fluid";
        wait = 0; // Will be autoset to whatever makes the most sense given the spread and effect waits.
        ss_flags = SS_KEEP_TIMING;
        runlevels = Dm.bitor(RUNLEVEL_GAME, RUNLEVEL_POSTGAME);
        priority = FIRE_PRIORITY_FLUIDS;
    }

    // The New() that SUBSYSTEM_DEF(fluids) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSfluids) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/fluids.dm:20
    public override async DmTask New(params object[] __extra)
    {
        if (SSfluids != this)
        {
            if (SSfluids is datum_controller_subsystem_fluids)
            {
                Recover();
                qdel(SSfluids);
            }
            SSfluids = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/fluids.dm:51
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        initialize_waits();
        initialize_spread_carousel();
        initialize_effect_carousel();
        return SS_INIT_SUCCESS;
    }

    /**
     * Initializes the subsystem waits.
     *
     * Ensures that the subsystem's fire wait evenly splits the spread and effect waits.
     */
    // tg: code/controllers/subsystem/fluids.dm:62
    public virtual void initialize_waits()
    {
        if (spread_wait <= 0)
        {
            // WARNING(MSG) (__DEFINES/logging.dm), expanded inline
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("[] has the invalid spread wait [].", this, spread_wait), "code/controllers/subsystem/fluids.dm", 64, this, usr));
            spread_wait = 1 * SECONDS;
        }
        if (effect_wait <= 0)
        {
            // WARNING(MSG) (__DEFINES/logging.dm), expanded inline
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("[] has the invalid effect wait [].", this, effect_wait), "code/controllers/subsystem/fluids.dm", 67, this, usr));
            spread_wait = 1 * SECONDS;
        }

        // Sets the overall wait of the subsystem to evenly divide both the effect and spread waits.
        float max_wait = Gcd(spread_wait, effect_wait);
        if (max_wait < wait || wait <= 0)
            wait = max_wait;
        else
            // If the wait of the subsystem overall is set to a valid value make the actual wait of the subsystem evenly divide that as well.
            // Makes effect bubbling possible with identical spread and effect waits.
            wait = Gcd(wait, max_wait);
    }

    /**
     * Initializes the carousel used to process fluid spreading.
     *
     * Synchronizes the spread delta time with the actual target spread tick rate.
     * Builds the carousel buckets used to queue spreads.
     */
    // tg: code/controllers/subsystem/fluids.dm:86
    public virtual void initialize_spread_carousel()
    {
        // Make absolutely certain that the spread wait is in sync with the target spread tick rate.
        num_spread_buckets = round(spread_wait / wait);
        spread_wait = wait * Dm.num(num_spread_buckets);

        spread_carousel = Dm.list();
        spread_carousel.len = Dm.num(num_spread_buckets);
        for (int i = 1; i <= Dm.num(num_spread_buckets); i++)
            spread_carousel[i] = Dm.list();
        currently_spreading = Dm.list();
        spread_bucket_index = 1f;
    }

    /**
     * Initializes the carousel used to process fluid effects.
     *
     * Synchronizes the spread delta time with the actual target spread tick rate.
     * Builds the carousel buckets used to bubble processing.
     */
    // tg: code/controllers/subsystem/fluids.dm:104
    public virtual void initialize_effect_carousel()
    {
        // Make absolutely certain that the effect wait is in sync with the target effect tick rate.
        num_effect_buckets = round(effect_wait / wait);
        effect_wait = wait * Dm.num(num_effect_buckets);

        effect_carousel = Dm.list();
        effect_carousel.len = Dm.num(num_effect_buckets);
        for (int i = 1; i <= Dm.num(num_effect_buckets); i++)
            effect_carousel[i] = Dm.list();
        currently_processing = Dm.list();
        effect_bucket_index = 1f;
    }

    // tg: code/controllers/subsystem/fluids.dm:117
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        float seconds_per_tick;
        object cached_bucket_index;
        DmList currentrun;
        // Ok so like I get the lighting style splittick but why are we doing this churn thing
        // It seems like a bad idea for processing to get out of step with spreading
        // MC_SPLIT_TICK_INIT(2) (__DEFINES/MC.dm:3), inline (rules 11.3)
        float original_tick_limit = datum_controller_master.current_ticklimit;
        float split_tick_phases = 2;

        MC_SPLIT_TICK(original_tick_limit, ref split_tick_phases); // Start processing fluid spread (we take a lot of cpu for ourselves, spreading is more important after all)
        if (!Dm.truthy(resumed))
        {
            spread_bucket_index = WRAP_UP(Dm.num(spread_bucket_index), Dm.num(num_spread_buckets));
            currently_spreading = spread_carousel[spread_bucket_index] as DmList;
            spread_carousel[spread_bucket_index] = Dm.list(); // Reset the bucket so we don't process an _entire station's worth of foam_ spreading every 2 ticks when the foam flood event happens.
        }

        seconds_per_tick = spread_wait / (1 * SECONDS);
        currentrun = currently_spreading;
        while (Dm.truthy(currentrun.len))
        {
            obj_effect_particle_effect_fluid to_spread = currentrun[currentrun.len] as obj_effect_particle_effect_fluid;
            currentrun.len--;

            if (!QDELETED(to_spread))
            {
                await to_spread.spread(seconds_per_tick);
                to_spread.spread_bucket = null;
            }

            if (MC_TICK_CHECK())
                break;
        }

        MC_SPLIT_TICK(original_tick_limit, ref split_tick_phases); // Start processing fluid effects:
        if (!Dm.truthy(resumed))
        {
            effect_bucket_index = WRAP_UP(Dm.num(effect_bucket_index), Dm.num(num_effect_buckets));
            DmList tmp_list = effect_carousel[effect_bucket_index] as DmList;
            currently_processing = tmp_list.Copy();
        }

        seconds_per_tick = effect_wait / (1 * SECONDS);
        cached_bucket_index = effect_bucket_index;
        currentrun = currently_processing;
        while (Dm.truthy(currentrun.len))
        {
            obj_effect_particle_effect_fluid to_process = currentrun[currentrun.len] as obj_effect_particle_effect_fluid;
            currentrun.len--;

            if (QDELETED(to_process) || Dm.eq(to_process.process(seconds_per_tick), PROCESS_KILL))
            {
                (effect_carousel[cached_bucket_index] as DmList).Remove(to_process);
                if (to_process != null)
                {
                    to_process.effect_bucket = null;
                    to_process.datum_flags = Dm.bitand(to_process.datum_flags, Dm.bitnot(DF_ISPROCESSING));
                }
            }

            if (MC_TICK_CHECK())
                break;
        }
    }

    // MC_TICK_CHECK (__DEFINES/MC.dm:1), inline (rules 11.3)
    private bool MC_TICK_CHECK() => Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0);

    // MC_SPLIT_TICK (__DEFINES/MC.dm:4-10), over the locals MC_SPLIT_TICK_INIT declared
    private static void MC_SPLIT_TICK(float original_tick_limit, ref float split_tick_phases)
    {
        if (split_tick_phases > 1)
        {
            datum_controller_master.current_ticklimit = ((original_tick_limit - TICK_USAGE()) / split_tick_phases) + TICK_USAGE();
            --split_tick_phases;
        }
        else
            datum_controller_master.current_ticklimit = original_tick_limit;
    }

    /**
     * Queues a fluid node to spread later after one full carousel rotation.
     *
     * Arguments:
     * - [node][/obj/effect/particle_effect/fluid]: The node to queue to spread.
     */
    // tg: code/controllers/subsystem/fluids.dm:171
    public virtual void queue_spread(obj_effect_particle_effect_fluid node = null)
    {
        if (Dm.truthy(node.spread_bucket))
            return;

        (spread_carousel[spread_bucket_index] as DmList).Add(node);
        node.spread_bucket = spread_bucket_index;
    }

    /**
     * Cancels a queued spread of a fluid node.
     *
     * Arguments:
     * - [node][/obj/effect/particle_effect/fluid]: The node to cancel the spread of.
     */
    // tg: code/controllers/subsystem/fluids.dm:184
    public virtual void cancel_spread(obj_effect_particle_effect_fluid node = null)
    {
        if (!Dm.truthy(node.spread_bucket))
            return;

        object bucket_index = node.spread_bucket;
        (spread_carousel[bucket_index] as DmList).Remove(node);
        if (Dm.eq(bucket_index, spread_bucket_index))
            currently_spreading.Remove(node);

        node.spread_bucket = null;
    }

    /**
     * Starts processing the effects of a fluid node.
     *
     * The fluid node will next process after one full bucket rotation.
     *
     * Arguments:
     * - [node][/obj/effect/particle_effect/fluid]: The node to start processing.
     */
    // tg: code/controllers/subsystem/fluids.dm:203
    public virtual void start_processing(obj_effect_particle_effect_fluid node = null)
    {
        if (Dm.truthy(Dm.bitand(node.datum_flags, DF_ISPROCESSING)) || Dm.truthy(node.effect_bucket))
            return;
        if (effect_carousel == null)
            return; // not initialized yet: a test room without the Master, which tg never is

        // Edit this value to make all fluids process effects (at the same time|offset by when they started processing| -> offset by a random amount <- )
        float bucket_index = rand(1, Dm.num(num_effect_buckets));
        (effect_carousel[bucket_index] as DmList).Add(node);
        node.effect_bucket = bucket_index;
        node.datum_flags = Dm.bitor(node.datum_flags, DF_ISPROCESSING);
    }

    /**
     * Stops processing the effects of a fluid node.
     *
     * Arguments:
     * - [node][/obj/effect/particle_effect/fluid]: The node to stop processing.
     */
    // tg: code/controllers/subsystem/fluids.dm:219
    public virtual void stop_processing(obj_effect_particle_effect_fluid node = null)
    {
        if (!Dm.truthy(Dm.bitand(node.datum_flags, DF_ISPROCESSING)))
            return;

        object bucket_index = node.effect_bucket;
        if (!Dm.truthy(bucket_index))
            return;

        (effect_carousel[bucket_index] as DmList).Remove(node);
        if (Dm.eq(bucket_index, effect_bucket_index))
            currently_processing.Remove(node);

        node.effect_bucket = null;
        node.datum_flags = Dm.bitand(node.datum_flags, Dm.bitnot(DF_ISPROCESSING));
    }
}

/// The subsystem responsible for processing smoke propagation and effects.
// tg: code/controllers/subsystem/fluids.dm:241 FLUID_SUBSYSTEM_DEF(smoke)
public partial class datum_controller_subsystem_fluids_smoke : datum_controller_subsystem_fluids
{
    // The New() that FLUID_SUBSYSTEM_DEF(smoke) defines (__DEFINES/MC.dm:124-127)
    // tg: code/controllers/subsystem/fluids.dm:241
    public override async DmTask New(params object[] __extra)
    {
        if (SSsmoke != this)
        {
            if (SSsmoke is datum_controller_subsystem_fluids_smoke)
            {
                Recover();
                qdel(SSsmoke);
            }
            SSsmoke = this;
        }
        PreInit();
    }

    // The fire() that FLUID_SUBSYSTEM_DEF(smoke) defines, just so it shows up on the profiler (__DEFINES/MC.dm:128)
    // tg: code/controllers/subsystem/fluids.dm:241
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra);
    }
}

/// The subsystem responsible for processing foam propagation and effects.
// tg: code/controllers/subsystem/fluids.dm:247 FLUID_SUBSYSTEM_DEF(foam)
public partial class datum_controller_subsystem_fluids_foam : datum_controller_subsystem_fluids
{
    // The New() that FLUID_SUBSYSTEM_DEF(foam) defines (__DEFINES/MC.dm:124-127)
    // tg: code/controllers/subsystem/fluids.dm:247
    public override async DmTask New(params object[] __extra)
    {
        if (SSfoam != this)
        {
            if (SSfoam is datum_controller_subsystem_fluids_foam)
            {
                Recover();
                qdel(SSfoam);
            }
            SSfoam = this;
        }
        PreInit();
    }

    // The fire() that FLUID_SUBSYSTEM_DEF(foam) defines, just so it shows up on the profiler (__DEFINES/MC.dm:128)
    // tg: code/controllers/subsystem/fluids.dm:247
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/fluids.dm:20  SUBSYSTEM_DEF(fluids): GLOBAL_REAL(SSfluids, /datum/controller/subsystem/fluids)
    internal datum_controller_subsystem_fluids SSfluids;
    // tg: code/controllers/subsystem/fluids.dm:241  FLUID_SUBSYSTEM_DEF(smoke): GLOBAL_REAL(SSsmoke, /datum/controller/subsystem/fluids/smoke)
    internal datum_controller_subsystem_fluids_smoke SSsmoke;
    // tg: code/controllers/subsystem/fluids.dm:247  FLUID_SUBSYSTEM_DEF(foam): GLOBAL_REAL(SSfoam, /datum/controller/subsystem/fluids/foam)
    internal datum_controller_subsystem_fluids_foam SSfoam;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_fluids SSfluids => ref TgState.Current.SSfluids;
    public static ref datum_controller_subsystem_fluids_smoke SSsmoke => ref TgState.Current.SSsmoke;
    public static ref datum_controller_subsystem_fluids_foam SSfoam => ref TgState.Current.SSfoam;
}
