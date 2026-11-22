// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/blood_drying.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 1 (blood in the world: the drying of the blood decals): the whole file. The var block is generated (D111).

/// Used as a low priority background system to handling the drying of blood on the ground
// tg: code/controllers/subsystem/blood_drying.dm:6 PROCESSING_SUBSYSTEM_DEF(blood_drying) (the var block is generated)
public partial class datum_controller_subsystem_processing_blood_drying : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(blood_drying) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSblood_drying)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/blood_drying.dm:6
    public override async DmTask New(params object[] __extra)
    {
        if (SSblood_drying != this)
        {
            if (SSblood_drying is datum_controller_subsystem_processing_blood_drying)
            {
                Recover();
                qdel(SSblood_drying);
            }
            SSblood_drying = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(blood_drying) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/blood_drying.dm:6
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/blood_drying.dm:6  PROCESSING_SUBSYSTEM_DEF(blood_drying): GLOBAL_REAL(SSblood_drying, /datum/controller/subsystem/processing/blood_drying)
    internal datum_controller_subsystem_processing_blood_drying SSblood_drying;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_blood_drying SSblood_drying => ref TgState.Current.SSblood_drying;
}
