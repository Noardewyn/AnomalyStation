// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/ai_controllers_low_priority.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The vars are generated.

/// Plans background controllers that are active when unwatched but not critical
// tg: code/controllers/subsystem/ai_controllers_low_priority.dm:2 AI_CONTROLLER_SUBSYSTEM_DEF(low_priority_ai_controllers)
public partial class datum_controller_subsystem_ai_controllers_low_priority_ai_controllers : datum_controller_subsystem_ai_controllers
{
    // tg: code/__DEFINES/MC.dm:139 (AI_CONTROLLER_SUBSYSTEM_DEF's New: it calls no parent)
    public override async DmTask New(params object[] __extra)
    {
        if (SSlow_priority_ai_controllers != this)
        {
            if (SSlow_priority_ai_controllers is datum_controller_subsystem_ai_controllers_low_priority_ai_controllers)
            {
                Recover();
                qdel(SSlow_priority_ai_controllers);
            }
            SSlow_priority_ai_controllers = this;
        }
        PreInit();
    }

    // tg: code/__DEFINES/MC.dm:144 (the macro's fire(), ..() for the profiler)
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra);
    }
}
