// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/typelists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G2 (D291): GLOB.typelists and the release build's typelist (the mineral floors share their icon lists by type). Not
// here: the TESTING build's tallying variant (:11-27) and tallytypelistsavings, a debug report.

public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/typelists.dm:1  GLOBAL_LIST_EMPTY(typelists)
    public DmList typelists;

    // tg: code/__HELPERS/typelists.dm:1  (GLOBAL_MANAGED(typelists, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltypelists()
    {
        typelists = Dm.list();
        Dm.append(ref gvars_datum_init_order, "typelists");
    }
}

public partial class datum
{
    // tg: code/__HELPERS/typelists.dm:5
    public virtual object typelist(object key = null, DmList values = null)
    {
        values ??= Dm.list();
        DmList mytypelist = (GLOB.typelists[type] ?? (GLOB.typelists[type] = Dm.list())) as DmList;
        return mytypelist[key] ?? (mytypelist[key] = values.Copy());
    }
}
