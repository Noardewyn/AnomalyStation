// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/supermatter_cascade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 delamination slice (G6, D163): the whole file. The var block is generated (D111).

// tg: code/controllers/subsystem/processing/supermatter_cascade.dm:2 PROCESSING_SUBSYSTEM_DEF(supermatter_cascade) (the var block is generated)
public partial class datum_controller_subsystem_processing_supermatter_cascade : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(supermatter_cascade) defines (__DEFINES/MC.dm:116-119), with
    // NEW_SS_GLOBAL(SSsupermatter_cascade) (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/supermatter_cascade.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSsupermatter_cascade != this)
        {
            if (SSsupermatter_cascade is datum_controller_subsystem_processing_supermatter_cascade)
            {
                Recover();
                qdel(SSsupermatter_cascade);
            }
            SSsupermatter_cascade = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(supermatter_cascade) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/supermatter_cascade.dm:2
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/supermatter_cascade.dm:2  PROCESSING_SUBSYSTEM_DEF(supermatter_cascade): GLOBAL_REAL(SSsupermatter_cascade, /datum/controller/subsystem/processing/supermatter_cascade)
    internal datum_controller_subsystem_processing_supermatter_cascade SSsupermatter_cascade;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_supermatter_cascade SSsupermatter_cascade => ref TgState.Current.SSsupermatter_cascade;
}
