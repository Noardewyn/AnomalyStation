// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/acid.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI1c, D326). The var block is generated (D111).

/// The subsystem used to tick [/datum/component/acid] instances.
// tg: code/controllers/subsystem/processing/acid.dm:2 PROCESSING_SUBSYSTEM_DEF(acid) (the var block is generated)
public partial class datum_controller_subsystem_processing_acid : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(acid) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSacid)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/acid.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSacid != this)
        {
            if (SSacid is datum_controller_subsystem_processing_acid)
            {
                Recover();
                qdel(SSacid);
            }
            SSacid = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(acid) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/acid.dm:2
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/acid.dm:2  PROCESSING_SUBSYSTEM_DEF(acid): GLOBAL_REAL(SSacid, /datum/controller/subsystem/processing/acid)
    internal datum_controller_subsystem_processing_acid SSacid;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_acid SSacid => ref TgState.Current.SSacid;
}
