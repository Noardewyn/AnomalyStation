// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/plumbing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111).

// tg: code/controllers/subsystem/processing/plumbing.dm:1 PROCESSING_SUBSYSTEM_DEF(plumbing) (the var block is generated)
public partial class datum_controller_subsystem_processing_plumbing : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(plumbing) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSplumbing)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/plumbing.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSplumbing != this)
        {
            if (SSplumbing is datum_controller_subsystem_processing_plumbing)
            {
                Recover();
                qdel(SSplumbing);
            }
            SSplumbing = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(plumbing) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/plumbing.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/plumbing.dm:1  PROCESSING_SUBSYSTEM_DEF(plumbing): GLOBAL_REAL(SSplumbing, /datum/controller/subsystem/processing/plumbing)
    internal datum_controller_subsystem_processing_plumbing SSplumbing;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_plumbing SSplumbing => ref TgState.Current.SSplumbing;
}
