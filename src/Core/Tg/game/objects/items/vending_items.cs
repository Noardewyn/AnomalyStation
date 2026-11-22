// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/vending_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the whole file, the vending machine refill canister (its subtypes are in each vendor's file, var-only). The var block is
// generated.

// tg: code/game/objects/items/vending_items.dm:4
public partial class obj_item_vending_refill : obj_item
{
    // tg: code/game/objects/items/vending_items.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = I18n.T("\\improper [] restocking unit", machine_name);
        return dot;
    }

    // tg: code/game/objects/items/vending_items.dm:39
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        object num = get_part_rating();
        if (!Dm.truthy(num))
            Dm.append(ref dot, span_notice("It's empty!"));
        else if (Dm.eq(num, INFINITY))
            Dm.append(ref dot, span_notice("It's full of supplies!"));
        else
            Dm.append(ref dot, span_notice(I18n.T("It can restock [] item\\s.", num)));
        return dot;
    }

    // tg: code/game/objects/items/vending_items.dm:50
    public override object get_part_rating()
    {
        float dot = 0;
        //first time needs to be filled by the vending machine
        if (!Dm.truthy(products))
            return INFINITY;

        foreach (object key in Dm.each(products))
            dot += Dm.num(products[key]);
        foreach (object key in Dm.each(contraband))
            dot += Dm.num(contraband[key]);
        foreach (object key in Dm.each(premium))
            dot += Dm.num(premium[key]);
        return dot;
    }
}
