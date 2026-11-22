// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/fire_burning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 1 (the burning component): the whole file. The var block is generated (D111).

/// The subsystem used to tick [/datum/component/burning] instances.
// tg: code/controllers/subsystem/processing/fire_burning.dm:2 PROCESSING_SUBSYSTEM_DEF(burning) (the var block is generated)
public partial class datum_controller_subsystem_processing_burning : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(burning) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSburning)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/fire_burning.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSburning != this)
        {
            if (SSburning is datum_controller_subsystem_processing_burning)
            {
                Recover();
                qdel(SSburning);
            }
            SSburning = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(burning) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/fire_burning.dm:2
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/fire_burning.dm:2  PROCESSING_SUBSYSTEM_DEF(burning): GLOBAL_REAL(SSburning, /datum/controller/subsystem/processing/burning)
    internal datum_controller_subsystem_processing_burning SSburning;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_burning SSburning => ref TgState.Current.SSburning;
}
