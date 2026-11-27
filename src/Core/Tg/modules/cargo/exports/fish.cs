// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/fish.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file, the fish priced by their own export price. The var blocks are generated.

// tg: code/modules/cargo/exports/fish.dm:1
public partial class datum_export_fish : datum_export
{
    // tg: code/modules/cargo/exports/fish.dm:7
    public override object get_base_cost(obj exported_item = null)
    {
        obj_item_fish fish = (obj_item_fish)exported_item;
        return fish.get_export_price(base.get_base_cost(exported_item));
    }
}
