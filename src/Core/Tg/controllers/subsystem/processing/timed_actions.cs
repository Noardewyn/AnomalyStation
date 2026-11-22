// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/timed_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/controllers/subsystem/processing/timed_actions.dm:1 PROCESSING_SUBSYSTEM_DEF(timed_actions)
[DmPath("/datum/controller/subsystem/processing/timed_actions")]
public partial class datum_controller_subsystem_processing_timed_actions : datum_controller_subsystem_processing
{
    // tg: code/controllers/subsystem/processing/timed_actions.dm:2
    public datum_controller_subsystem_processing_timed_actions()
    {
        name = "Timed Actions";
        priority = FIRE_PRIORITY_TIMED_ACTIONS;
        ss_flags = Dm.bitor(SS_TICKER, SS_NO_INIT);
        runlevels = Dm.bitor(RUNLEVEL_GAME, RUNLEVEL_POSTGAME);
        wait = 1;
    }

    // The New() that PROCESSING_SUBSYSTEM_DEF(timed_actions) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SStimed_actions)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/timed_actions.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SStimed_actions != this)
        {
            if (SStimed_actions is datum_controller_subsystem_processing_timed_actions)
            {
                Recover();
                qdel(SStimed_actions);
            }
            SStimed_actions = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(timed_actions) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/timed_actions.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/timed_actions.dm:1  PROCESSING_SUBSYSTEM_DEF(timed_actions): GLOBAL_REAL(SStimed_actions, /datum/controller/subsystem/processing/timed_actions)
    internal datum_controller_subsystem_processing_timed_actions SStimed_actions;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_timed_actions SStimed_actions => ref TgState.Current.SStimed_actions;
}
