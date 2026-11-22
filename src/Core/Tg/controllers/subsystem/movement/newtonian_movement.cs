// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/movement/newtonian_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated (M4b slice 1: the zero-gravity drift's loops). The var block (name, ss_flags = SS_NO_INIT|SS_TICKER, runlevels,
// stat_tag, processing, currentrun) is generated (D111).

/// The subsystem is intended to tick things related to space/newtonian movement, such as constant sources of inertia
// tg: code/controllers/subsystem/movement/newtonian_movement.dm:2 MOVEMENT_SUBSYSTEM_DEF(newtonian_movement)
public partial class datum_controller_subsystem_movement_newtonian_movement : datum_controller_subsystem_movement
{
    // The New() that MOVEMENT_SUBSYSTEM_DEF(newtonian_movement) defines (__DEFINES/MC.dm:107-111), with
    // NEW_SS_GLOBAL(SSnewtonian_movement) (__DEFINES/MC.dm:29) expanded inline (it reads src). The macro's fire() (:112,
    // ..() for the profiler) is replaced by the type's own fire below (a second definition of the same proc on the type, which DM takes).
    // tg: code/controllers/subsystem/movement/newtonian_movement.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSnewtonian_movement != this)
        {
            if (SSnewtonian_movement is datum_controller_subsystem_movement_newtonian_movement)
            {
                Recover();
                qdel(SSnewtonian_movement);
            }
            SSnewtonian_movement = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/movement/newtonian_movement.dm:11
    public override object stat_entry(object msg = null)
    {
        Dm.Deferred("code/controllers/subsystem/movement/newtonian_movement.dm:11", "stat_entry: the MC stat panel line (stat panels, translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/movement/newtonian_movement.dm:15
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
        {
            canonical_time = world.time;
            this.currentrun = processing.Copy();
        }

        //cache for sanic speed (lists are references anyways)
        DmList current_run = this.currentrun;

        while (Dm.truthy(current_run.len))
        {
            datum thing = current_run[current_run.len] as datum;
            current_run.len--;
            bool kill__1 = QDELETED(thing);
            if (kill__1)
                Dm.remove(ref processing, thing);
            else
            {
                object t__1 = null;
                try { t__1 = thing.process(TICKS2DS(wait) * 0.1f); }
                catch (Exception e) { World.Current.ReportRuntime(e); t__1 = null; } // isolation boundary (translation-rules.md 9.2)
                if (Dm.eq(t__1, PROCESS_KILL))
                {
                    // fully stop so that a future START_PROCESSING will work
                    STOP_PROCESSING(this, thing);
                }
            }
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }

        foreach (DmList bucket_info in Dm.each<DmList>(sorted_buckets))
        {
            float time = Dm.num(bucket_info[MOVEMENT_BUCKET_TIME]);
            if (time > canonical_time || Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
            pour_bucket(bucket_info);
        }
    }

    // tg: code/controllers/subsystem/movement/newtonian_movement.dm:40
    public virtual void fire_moveloop(datum_move_loop loop = null)
    {
        // Loop isn't even running right now
        if (!Dm.truthy(Dm.bitand(loop.status, MOVELOOP_STATUS_QUEUED)))
            return;
        // Drop the loop, process it, and if its still valid - queue it again
        dequeue_loop(loop);
        loop.process();
        if (QDELETED(loop))
            return;
        loop.timer = world.time + loop.delay;
        queue_loop(loop);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/movement/newtonian_movement.dm:2  MOVEMENT_SUBSYSTEM_DEF(newtonian_movement):
    // GLOBAL_REAL(SSnewtonian_movement, /datum/controller/subsystem/movement/newtonian_movement)
    internal datum_controller_subsystem_movement_newtonian_movement SSnewtonian_movement;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_movement_newtonian_movement SSnewtonian_movement => ref TgState.Current.SSnewtonian_movement;
}
