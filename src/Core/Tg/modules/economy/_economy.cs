// Translated from tgstation (snapshot 1d99bd7f): code/modules/economy/_economy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the whole file (an item's money value, none but the money's own; the coins, cash and holochips that override it are
// inert with the economy).

public partial class obj_item
{
    // tg: code/modules/economy/_economy.dm:2
    public virtual object get_item_credit_value()
    {
        return null;
    }
}
