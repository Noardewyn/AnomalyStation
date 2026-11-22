// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/turrets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4d, D316: the portable turrets process on it). The var block is generated (D111).

/// The subsystem used for portable turrets, as they're relatively more intensive compared to most other machines, so we don't want them hogging tick usage from everything else.
// tg: code/controllers/subsystem/processing/turrets.dm:2 PROCESSING_SUBSYSTEM_DEF(turrets)
public partial class datum_controller_subsystem_processing_turrets : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(turrets) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSturrets)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/turrets.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSturrets != this)
        {
            if (SSturrets is datum_controller_subsystem_processing_turrets)
            {
                Recover();
                qdel(SSturrets);
            }
            SSturrets = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(turrets) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/turrets.dm:2
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/turrets.dm:2  PROCESSING_SUBSYSTEM_DEF(turrets): GLOBAL_REAL(SSturrets, /datum/controller/subsystem/processing/turrets)
    internal datum_controller_subsystem_processing_turrets SSturrets;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_turrets SSturrets => ref TgState.Current.SSturrets;
}
