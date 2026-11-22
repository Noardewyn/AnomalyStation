// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/obj.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/controllers/subsystem/processing/obj.dm:1 PROCESSING_SUBSYSTEM_DEF(obj)
[DmPath("/datum/controller/subsystem/processing/obj")]
public partial class datum_controller_subsystem_processing_obj : datum_controller_subsystem_processing
{
    // tg: code/controllers/subsystem/processing/obj.dm:2
    public datum_controller_subsystem_processing_obj()
    {
        name = "Objects";
        priority = FIRE_PRIORITY_OBJ;
        ss_flags = SS_NO_INIT;
        wait = 2 * SECONDS;
    }

    // The New() that PROCESSING_SUBSYSTEM_DEF(obj) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSobj)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/obj.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSobj != this)
        {
            if (SSobj is datum_controller_subsystem_processing_obj)
            {
                Recover();
                qdel(SSobj);
            }
            SSobj = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(obj) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/obj.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/obj.dm:1  PROCESSING_SUBSYSTEM_DEF(obj): GLOBAL_REAL(SSobj, /datum/controller/subsystem/processing/obj)
    internal datum_controller_subsystem_processing_obj SSobj;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_obj SSobj => ref TgState.Current.SSobj;
}
