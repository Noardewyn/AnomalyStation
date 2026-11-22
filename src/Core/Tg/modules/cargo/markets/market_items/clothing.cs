// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/markets/market_items/clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file; the clothing is var blocks (generated) but the spacesuit set's box.

// tg: code/modules/cargo/markets/market_items/clothing.dm:45
public partial class datum_market_item_clothing_full_spacesuit_set : datum_market_item_clothing
{
    // tg: code/modules/cargo/markets/market_items/clothing.dm:55
    public override async DmTask<object> spawn_item(object loc = null, datum_market_purchase purchase = null)
    {
        var B = await base.spawn_item(loc, purchase) as obj_item_storage_box;
        B.name = I18n.T("Spacesuit Box");
        B.desc = I18n.T("It has an NT logo on it.");
        Dm.@new<obj_item_clothing_suit_space>(B);
        Dm.@new<obj_item_clothing_head_helmet_space>(B);
        return B;
    }
}
