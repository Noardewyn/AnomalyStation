// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/markets/market_items/local_goods.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the goods placed on the black market by the station's LTSRBT. The var block is generated (D111).

///A special category for goods placed on the market by station by someone with the LTSRBT.
// tg: code/modules/cargo/markets/market_items/local_goods.dm:2
public partial class datum_market_item_local_good : datum_market_item
{
    // tg: code/modules/cargo/markets/market_items/local_goods.dm:10
    [DmParams("thing", "seller")]
    public override async DmTask New(params object[] __extra)
    {
        object thing = Dm.arg(__extra, 0);
        var seller = Dm.arg(__extra, 1) as datum_bank_account;
        await base.New();
        set_item(thing);
        this.seller = seller;
        if (seller != null)
            RegisterSignal(seller, COMSIG_QDELETING, nameof(delete_reference));
    }

    // tg: code/modules/cargo/markets/market_items/local_goods.dm:17
    public override async DmTask<object> buy(obj_item_market_uplink uplink = null, mob buyer = null, object shipping_method = null, object legal_status = null)
    {
        object dot = await base.buy(uplink, buyer, shipping_method, legal_status);
        if (Dm.truthy(dot) && seller != null)
            seller.adjust_money(round(Dm.num(price) * (1 - MARKET_WITHHOLDING_TAX)), I18n.T("Market: Item Sold"));
        // QDEL_IN(src, 10 MINUTES) (__DEFINES/qdel.dm), expanded inline: this category cannot hold more than 40 items at a time, so we need to clear sold items.
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), WEAKREF(this)), 10 * MINUTES, file: "code/modules/cargo/markets/market_items/local_goods.dm", line: 21);
        return dot;
    }

    // tg: code/modules/cargo/markets/market_items/local_goods.dm:23
    public virtual void delete_reference(datum source = null)
    {
        seller = null;
    }
}
