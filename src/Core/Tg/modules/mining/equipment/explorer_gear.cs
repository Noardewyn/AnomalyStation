// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/explorer_gear.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the explorer's gas mask, folded in the miner's survival box. The explorer suits (a vendor's, the mining points) stay inert with the console (P4).

// tg: code/modules/mining/equipment/explorer_gear.dm:60
public partial class obj_item_clothing_mask_gas_explorer : obj_item_clothing_mask_gas
{
    // tg: code/modules/mining/equipment/explorer_gear.dm:84
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        return await adjust_visor(user as mob_living);
    }

    // tg: code/modules/mining/equipment/explorer_gear.dm:87
    public override void visor_toggling(params object[] __extra)
    {
        base.visor_toggling(__extra);
        // adjusted = out of the way = smaller = can fit in boxes
        update_weight_class(Dm.truthy(up) ? WEIGHT_CLASS_SMALL : WEIGHT_CLASS_NORMAL);
    }

    // tg: code/modules/mining/equipment/explorer_gear.dm:92
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        inhand_icon_state = Dm.text("[][]", Dm.initial(type, (obj_item_clothing_mask_gas_explorer m) => m.inhand_icon_state), Dm.truthy(up) ? "_up" : "");
        return dot;
    }

    // tg: code/modules/mining/equipment/explorer_gear.dm:96
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(up) || Dm.eq(w_class, WEIGHT_CLASS_SMALL))
            return dot;
        Dm.append(ref dot, span_notice(I18n.T("You could fit this into a box if you adjusted it.")));
        return dot;
    }
}

// tg: code/modules/mining/equipment/explorer_gear.dm:102
public partial class obj_item_clothing_mask_gas_explorer_folded : obj_item_clothing_mask_gas_explorer
{
    // tg: code/modules/mining/equipment/explorer_gear.dm:105
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        visor_toggling();
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/clothing/gloves/color.dm:46  var/static/list/slapcraft_recipe_list
    internal DmList obj_item_clothing_gloves_fingerless__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_gripperoffbrand));
}
