// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/antag_hud.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A1 (D316): the whole file (it cycles a mind's antagonist HUD icons). The var block (name, flags, wait) is generated.

// tg: code/controllers/subsystem/processing/antag_hud.dm:1 PROCESSING_SUBSYSTEM_DEF(antag_hud)
public partial class datum_controller_subsystem_processing_antag_hud : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(antag_hud) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSantag_hud)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/antag_hud.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSantag_hud != this)
        {
            if (SSantag_hud is datum_controller_subsystem_processing_antag_hud)
            {
                Recover();
                qdel(SSantag_hud);
            }
            SSantag_hud = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(antag_hud) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/antag_hud.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/antag_hud.dm:1  PROCESSING_SUBSYSTEM_DEF(antag_hud): GLOBAL_REAL(SSantag_hud, /datum/controller/subsystem/processing/antag_hud)
    internal datum_controller_subsystem_processing_antag_hud SSantag_hud;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_antag_hud SSantag_hud => ref TgState.Current.SSantag_hud;
}
