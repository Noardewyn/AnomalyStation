// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/cereals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/cereals.dm:18
public partial class obj_item_food_grown_wheat : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/cereals.dm:30
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_flour), 0));
    }
}

// tg: code/modules/hydroponics/grown/cereals.dm:44
public partial class obj_item_food_grown_oat : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/cereals.dm:54
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_flour), 0));
    }
}

// tg: code/modules/hydroponics/grown/cereals.dm:72
public partial class obj_item_food_grown_rice : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/cereals.dm:82
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_rice), 0));
    }
}

// tg: code/modules/hydroponics/grown/cereals.dm:96
public partial class obj_item_food_grown_meatwheat : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/cereals.dm:108
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_flour), 0), Dm.kv(typeof(datum_reagent_blood), 0));
    }

    // tg: code/modules/hydroponics/grown/cereals.dm:111
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        user.visible_message(span_notice(I18n.T("[] crushes [] into meat.", user, this)), span_notice(I18n.T("You crush [] into something that resembles meat.", this)));
        playsound(user, Dm.resource("sound/effects/blob/blobattack.ogg"), 50, TRUE);
        obj_item_food_meat_slab_meatwheat meaties = Dm.@new<obj_item_food_meat_slab_meatwheat>((object)null);
        (meaties.reagents as datum_reagents)?.set_all_reagents_purity(Dm.num(((obj_item_seeds)seed).get_reagent_purity()));
        qdel(this);
        await user.put_in_hands(meaties);
        return TRUE;
    }
}
