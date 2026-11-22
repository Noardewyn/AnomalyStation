// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/ai_controllers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The var block is generated. The pass statistics of the MC panel (stat_entry's summing_*, most_expensive) are left out with the panel (rules 13).

/// The subsystem used to tick [/datum/ai_controllers] instances. Handling the re-checking of plans.
// tg: code/controllers/subsystem/ai_controllers.dm:5 SUBSYSTEM_DEF(ai_controllers)
public partial class datum_controller_subsystem_ai_controllers : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(ai_controllers) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/ai_controllers.dm:5
    public override async DmTask New(params object[] __extra)
    {
        if (SSai_controllers != this)
        {
            if (SSai_controllers is datum_controller_subsystem_ai_controllers)
            {
                Recover();
                qdel(SSai_controllers);
            }
            SSai_controllers = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/ai_controllers.dm:54
    public override void Recover()
    {
        if (islist(SSai_controllers.ai_controllers_by_status))
            ai_controllers_by_status = SSai_controllers.ai_controllers_by_status;
        if (islist(SSai_controllers.ai_controllers_by_zlevel))
            ai_controllers_by_zlevel = SSai_controllers.ai_controllers_by_zlevel;
    }

    // tg: code/controllers/subsystem/ai_controllers.dm:60
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        setup_targeting_strats();
        setup_target_priority_strats();
        setup_target_sources();
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/ai_controllers.dm:66
    public override object stat_entry(object msg = null)
    {
        Dm.Deferred("code/controllers/subsystem/ai_controllers.dm:66", "stat_entry: the MC stat panel line (stat panels, translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/ai_controllers.dm:75
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
        {
            DmList planning_list = SSai_controllers.ai_controllers_by_status[planning_status] as DmList;
            currentrun = planning_list.Copy();
        }

        //cache for sanic speed (lists are references anyways)
        DmList current_run = this.currentrun;
        while (Dm.truthy(length(current_run)))
        {
            datum_ai_controller ai_controller = current_run[length(current_run)] as datum_ai_controller;
            current_run.len--;
            // Pass the real time since this controller last ticked, so SPT_PROB rolls and
            // time accumulators stay time-correct even when a pass takes several seconds.
            float seconds_per_tick = wait * 0.1f;
            if (Dm.truthy(ai_controller.last_bt_tick))
                seconds_per_tick = (world.time - ai_controller.last_bt_tick) * 0.1f;
            ai_controller.last_bt_tick = world.time;
            try { ai_controller.SelectBehaviors(seconds_per_tick); }
            catch (Exception e) { World.Current.ReportRuntime(e); } // isolation boundary (translation-rules.md 9.2)

            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                break;
        }

        if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
            return;
    }

    ///Called when the max Z level was changed, updating our coverage.
    // tg: code/controllers/subsystem/ai_controllers.dm:148
    public virtual void on_max_z_changed()
    {
        if (!Dm.truthy(length(ai_controllers_by_zlevel)))
            ai_controllers_by_zlevel = Dm.@new<DmList>(world.maxz, 0);
        while (ai_controllers_by_zlevel.len < world.maxz)
        {
            ai_controllers_by_zlevel.len++;
            ai_controllers_by_zlevel[ai_controllers_by_zlevel.len] = Dm.list();
        }
    }

    // tg: code/controllers/subsystem/ai_controllers.dm:155
    public virtual void setup_targeting_strats()
    {
        targeting_strategies = Dm.list();
        foreach (Type target_type in Dm.each_cast<Type>(subtypesof(typeof(datum_targeting_strategy))))
            targeting_strategies[target_type] = Dm.@new(target_type);
    }

    // tg: code/controllers/subsystem/ai_controllers.dm:161
    public virtual void setup_target_priority_strats()
    {
        target_priority_strategies = Dm.list();
        foreach (Type target_type in Dm.each_cast<Type>(subtypesof(typeof(datum_target_priority_strategy))))
            target_priority_strategies[target_type] = Dm.@new(target_type);
    }

    // tg: code/controllers/subsystem/ai_controllers.dm:167
    public virtual void setup_target_sources()
    {
        target_sources = Dm.list();
        foreach (Type source_type in Dm.each_cast<Type>(subtypesof(typeof(datum_target_source))))
            target_sources[source_type] = Dm.@new(source_type);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/ai_controllers.dm:5
    internal datum_controller_subsystem_ai_controllers SSai_controllers;
    // tg: code/controllers/subsystem/ai_controllers_low_priority.dm:2
    internal datum_controller_subsystem_ai_controllers_low_priority_ai_controllers SSlow_priority_ai_controllers;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_ai_controllers SSai_controllers => ref TgState.Current.SSai_controllers;
    public static ref datum_controller_subsystem_ai_controllers_low_priority_ai_controllers SSlow_priority_ai_controllers => ref TgState.Current.SSlow_priority_ai_controllers;
}
