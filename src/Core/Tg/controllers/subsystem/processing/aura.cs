// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/aura.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the subsystem that ticks the healing and damage auras. The var block is generated.

/// The subsystem used to tick auras ([/datum/component/aura_healing] and [/datum/component/damage_aura]).
// tg: code/controllers/subsystem/processing/aura.dm:2 PROCESSING_SUBSYSTEM_DEF(aura)
public partial class datum_controller_subsystem_processing_aura : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(aura) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSaura)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/aura.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSaura != this)
        {
            if (SSaura is datum_controller_subsystem_processing_aura)
            {
                Recover();
                qdel(SSaura);
            }
            SSaura = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(aura) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/aura.dm:2
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/aura.dm:2  PROCESSING_SUBSYSTEM_DEF(aura): GLOBAL_REAL(SSaura, /datum/controller/subsystem/processing/aura)
    internal datum_controller_subsystem_processing_aura SSaura;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_aura SSaura => ref TgState.Current.SSaura;
}
