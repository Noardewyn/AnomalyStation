// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/qdel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    ///the underscores are to encourage people not to use this directly.
    // tg: code/__HELPERS/qdel.dm:3
    public static void ______qdel_list_wrapper(DmList L = null)
    {
        QDEL_LIST(L);
    }
}
