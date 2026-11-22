// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/manufacturing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16 (the manufacturing machines): the whole file. The var block is generated (D111).

// tg: code/controllers/subsystem/processing/manufacturing.dm:1 PROCESSING_SUBSYSTEM_DEF(manufacturing)
public partial class datum_controller_subsystem_processing_manufacturing : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(manufacturing) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSmanufacturing)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/manufacturing.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSmanufacturing != this)
        {
            if (SSmanufacturing is datum_controller_subsystem_processing_manufacturing)
            {
                Recover();
                qdel(SSmanufacturing);
            }
            SSmanufacturing = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(manufacturing) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/manufacturing.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/manufacturing.dm:1  PROCESSING_SUBSYSTEM_DEF(manufacturing): GLOBAL_REAL(SSmanufacturing, /datum/controller/subsystem/processing/manufacturing)
    internal datum_controller_subsystem_processing_manufacturing SSmanufacturing;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_manufacturing SSmanufacturing => ref TgState.Current.SSmanufacturing;
}
