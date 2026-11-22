// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/markets/market_items/stolen_goods.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the black market's fenced goods. The spies' bounties put them on the market
// (spy_bounty.dm:228); the spy is not ported, so nothing makes one yet. The var block is generated (D111).

///A special category for goods stolen by spies for their bounties.
// tg: code/modules/cargo/markets/market_items/stolen_goods.dm:2
public partial class datum_market_item_stolen_good : datum_market_item
{
    // tg: code/modules/cargo/markets/market_items/stolen_goods.dm:9
    [DmParams("thing", "thing_price")]
    public override async DmTask New(params object[] __extra)
    {
        var thing = Dm.arg(__extra, 0) as atom_movable;
        object thing_price = Dm.arg(__extra, 1);
        await base.New();
        set_item(thing);
        name = I18n.T("Stolen []", thing.name);
        desc = I18n.T("A [], stolen from somewhere on the station. Whoever owned it probably wouldn't be happy to see it here.", thing.name);
        price = thing_price;
    }
}
