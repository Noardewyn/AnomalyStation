// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/fish.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file; the fish's own price is fishing's (a fish is inert until then). The var blocks are generated.

// tg: code/modules/cargo/exports/fish.dm:1
public partial class datum_export_fish : datum_export
{
    // tg: code/modules/cargo/exports/fish.dm:7
    public override object get_base_cost(obj exported_item = null)
    {
        Dm.NotTranslated("code/modules/cargo/exports/fish.dm:8", "get_base_cost: fish.get_export_price(..()), a fish's own export price (fishing, 0/787)");
        return base.get_base_cost(exported_item);
    }
}
