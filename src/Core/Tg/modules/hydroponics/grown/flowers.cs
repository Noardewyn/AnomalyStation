// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/flowers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/flowers.dm:254
public partial class obj_item_grown_novaflower : obj_item_grown
{
    // tg: code/modules/hydroponics/grown/flowers.dm:273
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_capsaicin), 0), Dm.kv(typeof(datum_reagent_consumable_condensedcapsaicin), 0));
    }
}

// tg: code/modules/hydroponics/grown/flowers.dm:298
public partial class obj_item_food_grown_flower_rose : obj_item_food_grown_flower
{
    // tg: code/modules/hydroponics/grown/flowers.dm:312
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_MASK)))
        {
            worn_icon_state = Dm.text("[]_mouth", base_icon_state);
            await user.update_worn_mask();
        }
        else
        {
            worn_icon_state = base_icon_state;
            await user.update_worn_head();
        }
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_grown_flower_sunflower : obj_item_food_grown_flower
{
    // tg: code/modules/hydroponics/grown/flowers.dm:207
    public override async DmTask make_dryable()
    {
        await AddElement(typeof(datum_element_dryable), typeof(obj_item_food_semki_healthy)); //yum
    }
}
