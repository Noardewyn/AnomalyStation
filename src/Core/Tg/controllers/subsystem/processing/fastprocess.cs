// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/fastprocess.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/controllers/subsystem/processing/fastprocess.dm:1 PROCESSING_SUBSYSTEM_DEF(fastprocess)
[DmPath("/datum/controller/subsystem/processing/fastprocess")]
public partial class datum_controller_subsystem_processing_fastprocess : datum_controller_subsystem_processing
{
    // tg: code/controllers/subsystem/processing/fastprocess.dm:2
    public datum_controller_subsystem_processing_fastprocess()
    {
        name = "Fast Processing";
        wait = 0.2f * SECONDS;
        stat_tag = "FP";
    }

    // The New() that PROCESSING_SUBSYSTEM_DEF(fastprocess) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSfastprocess)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/fastprocess.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSfastprocess != this)
        {
            if (SSfastprocess is datum_controller_subsystem_processing_fastprocess)
            {
                Recover();
                qdel(SSfastprocess);
            }
            SSfastprocess = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(fastprocess) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/fastprocess.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/fastprocess.dm:1  PROCESSING_SUBSYSTEM_DEF(fastprocess): GLOBAL_REAL(SSfastprocess, /datum/controller/subsystem/processing/fastprocess)
    internal datum_controller_subsystem_processing_fastprocess SSfastprocess;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_fastprocess SSfastprocess => ref TgState.Current.SSfastprocess;
}
