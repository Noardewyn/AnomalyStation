// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/loot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D298): PARTIAL, the trade contract chip only (it unlocks a special supply pack at the supply console). The exploration
// drone's loot generators and the other loot wait for the exodrone's package. The var block is generated.

// tg: code/modules/explorer_drone/loot.dm:119
public partial class obj_item_trade_chip : obj_item
{
    // tg: code/modules/explorer_drone/loot.dm:127
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object pack_type = Dm.arg(__extra, 0);
        if (Dm.truthy(pack_type))
        {
            unlocked_pack_type = pack_type;
            name += Dm.text("- []", Dm.initial((Type)pack_type, (datum_supply_pack p) => p.name));
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/loot.dm:134
    public virtual void try_to_unlock_contract(mob user = null)
    {
        var pack_singleton = SSshuttle.supply_packs[unlocked_pack_type] as datum_supply_pack;
        if (!Dm.truthy(unlocked_pack_type) || pack_singleton == null || !Dm.truthy(Dm.bitand(pack_singleton.order_flags, ORDER_SPECIAL)))
        {
            to_chat(user, span_danger(I18n.T("This chip is invalid!")));
            return;
        }
        pack_singleton.order_flags = Dm.num(Dm.bitor(pack_singleton.order_flags, ORDER_SPECIAL_ENABLED));
        to_chat(user, span_notice(I18n.T("Contract accepted into nanotrasen supply database.")));
        qdel(this);
    }
}
