// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/achievements.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: gaps A1: GLOB.commendations (the medals' round-end report); the achievements' lists come with the achievements.

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/_globalvars/lists/achievements.dm:1  GLOBAL_LIST_EMPTY(commendations)
    public DmList commendations;

    // tg: code/_globalvars/lists/achievements.dm:1  (GLOBAL_MANAGED(commendations, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcommendations()
    {
        commendations = Dm.list();
        Dm.append(ref gvars_datum_init_order, "commendations");
    }
}
