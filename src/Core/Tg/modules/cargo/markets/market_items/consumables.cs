// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/markets/market_items/consumables.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file; the consumables are var blocks (generated) but the donk pocket box's and the pills' picks.

// tg: code/modules/cargo/markets/market_items/consumables.dm:15
public partial class datum_market_item_consumable_donk_pocket_box : datum_market_item_consumable
{
    // tg: code/modules/cargo/markets/market_items/consumables.dm:26
    public override async DmTask<object> spawn_item(object loc = null, datum_market_purchase purchase = null)
    {
        DmList choices = TgState.Current.datum_market_item_consumable_donk_pocket_box__spawn_item__choices;
        if (choices == null)
        {
            choices = Dm.list();
            foreach (Type boxtype in Dm.each_cast<Type>(typesof(typeof(obj_item_storage_box_donkpockets))))
                choices[boxtype] = 3f;
            choices[typeof(obj_item_storage_box_donkpockets_donkpocketgondola)] = 1f;
            TgState.Current.datum_market_item_consumable_donk_pocket_box__spawn_item__choices = choices;
        }
        item = pick_weight(choices);
        return await base.spawn_item(loc, purchase);
    }
}

// tg: code/modules/cargo/markets/market_items/consumables.dm:36
public partial class datum_market_item_consumable_suspicious_pills : datum_market_item_consumable
{
    // tg: code/modules/cargo/markets/market_items/consumables.dm:47
    public override async DmTask<object> spawn_item(object loc = null, datum_market_purchase purchase = null)
    {
        item = pick(Dm.list(typeof(obj_item_storage_pill_bottle_zoom),
            typeof(obj_item_storage_pill_bottle_happy),
            typeof(obj_item_storage_pill_bottle_lsd),
            typeof(obj_item_storage_pill_bottle_aranesp),
            typeof(obj_item_storage_pill_bottle_stimulant),
            typeof(obj_item_storage_pill_bottle_maintenance_pill)));
        return await base.spawn_item(loc, purchase);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/cargo/markets/market_items/consumables.dm:27  var/static/list/choices
    internal DmList datum_market_item_consumable_donk_pocket_box__spawn_item__choices;
}
