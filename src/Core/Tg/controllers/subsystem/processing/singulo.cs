// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/singulo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5x slice 5 (the singularity): the whole file. The var block is generated (D111).

/// Very rare subsystem, provides any active singularities with the timings and seclusion they need to succeed
// tg: code/controllers/subsystem/processing/singulo.dm:2 PROCESSING_SUBSYSTEM_DEF(singuloprocess) (the var block is generated)
public partial class datum_controller_subsystem_processing_singuloprocess : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(singuloprocess) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSsinguloprocess)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/singulo.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSsinguloprocess != this)
        {
            if (SSsinguloprocess is datum_controller_subsystem_processing_singuloprocess)
            {
                Recover();
                qdel(SSsinguloprocess);
            }
            SSsinguloprocess = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(singuloprocess) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/singulo.dm:2
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/singulo.dm:2  PROCESSING_SUBSYSTEM_DEF(singuloprocess): GLOBAL_REAL(SSsinguloprocess, /datum/controller/subsystem/processing/singuloprocess)
    internal datum_controller_subsystem_processing_singuloprocess SSsinguloprocess;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_singuloprocess SSsinguloprocess => ref TgState.Current.SSsinguloprocess;
}
