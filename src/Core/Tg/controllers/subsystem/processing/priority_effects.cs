// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/priority_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/controllers/subsystem/processing/priority_effects.dm:1 PROCESSING_SUBSYSTEM_DEF(priority_effects)
[DmPath("/datum/controller/subsystem/processing/priority_effects")]
public partial class datum_controller_subsystem_processing_priority_effects : datum_controller_subsystem_processing
{
    // tg: code/controllers/subsystem/processing/priority_effects.dm:2-6
    public datum_controller_subsystem_processing_priority_effects()
    {
        name = "Priority Status Effects";
        ss_flags = Dm.bitor(SS_KEEP_TIMING, SS_NO_INIT);
        wait = 0.2f * SECONDS; // Same as SSfastprocess, but can be anything, assuming you refactor all high-priority status effect intervals and durations to be a multiple of it.
        priority = FIRE_PRIORITY_PRIORITY_EFFECTS;
        stat_tag = "PEFF";
    }

    // The New() that PROCESSING_SUBSYSTEM_DEF(priority_effects) defines (__DEFINES/MC.dm:116-119), with
    // NEW_SS_GLOBAL(SSpriority_effects) (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/priority_effects.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSpriority_effects != this)
        {
            if (SSpriority_effects is datum_controller_subsystem_processing_priority_effects)
            {
                Recover();
                qdel(SSpriority_effects);
            }
            SSpriority_effects = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(priority_effects) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/priority_effects.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/priority_effects.dm:1  PROCESSING_SUBSYSTEM_DEF(priority_effects): GLOBAL_REAL(SSpriority_effects, ...)
    internal datum_controller_subsystem_processing_priority_effects SSpriority_effects;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_priority_effects SSpriority_effects => ref TgState.Current.SSpriority_effects;
}
