// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/blackbox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: an inert stub subsystem (lead decision W3 G2), owned by the mc (A) package. It keeps tg's [DmPath], name,
// init_stage and dependencies, so that Master/New creates it and the dependency graph of the subsystems that name it
// resolves (controllers/master.dm:361-363), but neither initializes nor fires.
// STUB(M10): translated in M10 (round statistics; its SQL part never, rules 13).

// tg: code/controllers/subsystem/blackbox.dm:1 SUBSYSTEM_DEF(blackbox)
[DmPath("/datum/controller/subsystem/blackbox")]
public partial class datum_controller_subsystem_blackbox : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/blackbox.dm:2
    public datum_controller_subsystem_blackbox()
    {
        name = "Blackbox";
        // STUB(M10): SS_NO_INIT | SS_NO_FIRE instead of tg's flags (none; wait 6000, runlevels GAME|POSTGAME) until the stub is translated.
        ss_flags = Dm.bitor(SS_NO_INIT, SS_NO_FIRE);
    }

    // The New() that SUBSYSTEM_DEF(blackbox) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSblackbox) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/blackbox.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSblackbox != this)
        {
            if (SSblackbox is datum_controller_subsystem_blackbox)
            {
                Recover();
                qdel(SSblackbox);
            }
            SSblackbox = this;
        }
        PreInit();
    }

    /**
     * Records feedback (statistics) for the round; the Master Controller records every subsystem's init time
     * (controllers/master.dm:508).
     */
    // tg: code/controllers/subsystem/blackbox.dm:231
    public virtual void record_feedback(object key_type = null, object key = null, object increment = null, object data = null, object overwrite = null)
    {
        Dm.Deferred("code/controllers/subsystem/blackbox.dm:231", "record_feedback: round statistics (M10+); nothing in M0 reads them");
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/blackbox.dm:1  SUBSYSTEM_DEF(blackbox): GLOBAL_REAL(SSblackbox, /datum/controller/subsystem/blackbox)
    internal datum_controller_subsystem_blackbox SSblackbox;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_blackbox SSblackbox => ref TgState.Current.SSblackbox;
}
