// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/moods.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file, the subsystem that processes the mood datums (/datum/mood/process). The var block is generated (D111).

// tg: code/controllers/subsystem/moods.dm:1 PROCESSING_SUBSYSTEM_DEF(mood) (the var block is generated)
public partial class datum_controller_subsystem_processing_mood : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(mood) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSmood)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/moods.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSmood != this)
        {
            if (SSmood is datum_controller_subsystem_processing_mood)
            {
                Recover();
                qdel(SSmood);
            }
            SSmood = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(mood) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/moods.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/moods.dm:1  PROCESSING_SUBSYSTEM_DEF(mood): GLOBAL_REAL(SSmood, /datum/controller/subsystem/processing/acid)
    internal datum_controller_subsystem_processing_mood SSmood;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_mood SSmood => ref TgState.Current.SSmood;
}
