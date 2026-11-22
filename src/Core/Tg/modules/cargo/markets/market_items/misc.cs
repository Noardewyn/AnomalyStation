// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/markets/market_items/misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file; the misc goods are var blocks (generated) but the holy water's, the girlypop crate's and the
// self surgery skillchip's procs.

// tg: code/modules/cargo/markets/market_items/misc.dm:90
public partial class datum_market_item_misc_holywater : datum_market_item_misc
{
    // tg: code/modules/cargo/markets/market_items/misc.dm:100
    public override async DmTask<object> spawn_item(object loc = null, datum_market_purchase purchase = null)
    {
        if (prob(6.66f))
            item = typeof(obj_item_reagent_containers_cup_beaker_unholywater);
        else
            item = Dm.initial(this, nameof(item));
        return await base.spawn_item(loc, purchase);
    }
}

// tg: code/modules/cargo/markets/market_items/misc.dm:176
public partial class datum_market_item_misc_girlypop : datum_market_item_misc
{
    // tg: code/modules/cargo/markets/market_items/misc.dm:186
    public override async DmTask<object> spawn_item(object loc = null, datum_market_purchase purchase = null)
    {
        await base.spawn_item(loc, purchase);
        var C = Dm.@new<obj_structure_closet_crate_glitter>(loc);
        foreach (Type type in new[]
        {
            typeof(obj_item_poster_contraband_dream),
            typeof(obj_item_poster_contraband_beekind),
            typeof(obj_item_poster_contraband_heart),
            typeof(obj_item_poster_contraband_dolphin),
            typeof(obj_item_poster_contraband_principles),
            typeof(obj_item_poster_contraband_trigger),
            typeof(obj_item_poster_contraband_barbaro),
            typeof(obj_item_poster_contraband_seabiscuit),
            typeof(obj_item_poster_contraband_pharlap),
            typeof(obj_item_poster_contraband_waradmiral),
            typeof(obj_item_poster_contraband_silver),
            typeof(obj_item_poster_contraband_jovial),
            typeof(obj_item_poster_contraband_bojack),
        })
            Dm.@new(type, C);
        return C;
    }
}

// tg: code/modules/cargo/markets/market_items/misc.dm:207
public partial class datum_market_item_misc_self_surgery_skillchip : datum_market_item_misc
{
    // tg: code/modules/cargo/markets/market_items/misc.dm:220
    public override async DmTask<object> buy(obj_item_market_uplink uplink = null, mob buyer = null, object shipping_method = null, object legal_status = null)
    {
        object dot = await base.buy(uplink, buyer, shipping_method, legal_status);
        if (Dm.truthy(dot))
            availability_prob = Dm.num(availability_prob) * 0.5f;
        return dot;
    }
}
