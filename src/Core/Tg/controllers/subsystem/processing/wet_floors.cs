// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/wet_floors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 3 (wet floors): the whole file. The var block is generated (D111).

// tg: code/controllers/subsystem/processing/wet_floors.dm:1 PROCESSING_SUBSYSTEM_DEF(wet_floors)
public partial class datum_controller_subsystem_processing_wet_floors : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(wet_floors) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSwet_floors)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/wet_floors.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSwet_floors != this)
        {
            if (SSwet_floors is datum_controller_subsystem_processing_wet_floors)
            {
                Recover();
                qdel(SSwet_floors);
            }
            SSwet_floors = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(wet_floors) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/wet_floors.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/wet_floors.dm:1  PROCESSING_SUBSYSTEM_DEF(wet_floors): GLOBAL_REAL(SSwet_floors, /datum/controller/subsystem/processing/wet_floors)
    internal datum_controller_subsystem_processing_wet_floors SSwet_floors;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_wet_floors SSwet_floors => ref TgState.Current.SSwet_floors;
}
