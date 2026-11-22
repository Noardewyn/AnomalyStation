// Translated from tgstation (snapshot 1d99bd7f): code/datums/view.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: holds the [DmPath] of a type that the M1 contracts' var blocks and signatures name (rules 2.3: the earliest .dme
// file that declares it), written by the M1 contracts step, and the procs the client layer names (M1 P1: Login's
// client.view_size?.resetToDefault(), login.dm:111); the file is translated with the client's view (M2).

// tg: code/datums/view.dm:3
[DmPath("/datum/view_data")]
public partial class datum_view_data : datum
{
    // tg: code/datums/view.dm:67
    public virtual void resetToDefault()
    {
        Dm.NotTranslated("code/datums/view.dm:67", "resetToDefault: the client's view size (the view, M2); client/New does not make a view_data in M1 (deferred)");
    }
}
