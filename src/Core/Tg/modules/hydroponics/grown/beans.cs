// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/beans.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/beans.dm:21
public partial class obj_item_food_grown_soybeans : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/beans.dm:30
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_soymilk);
    }
}

// tg: code/modules/hydroponics/grown/beans.dm:47
public partial class obj_item_food_grown_koibeans : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/beans.dm:56
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        user.visible_message(span_notice(I18n.T("[] crushes [] into a slab of carplike meat.", user, this)), span_notice(I18n.T("You crush [] into something that resembles a slab of carplike meat.", this)));
        playsound(user, Dm.resource("sound/effects/blob/blobattack.ogg"), 50, TRUE);
        obj_item_food_fishmeat_carp_imitation fishie = Dm.@new<obj_item_food_fishmeat_carp_imitation>((object)null);
        (fishie.reagents as datum_reagents)?.set_all_reagents_purity(Dm.num(((obj_item_seeds)seed).get_reagent_purity()));
        qdel(this);
        await user.put_in_hands(fishie);
        return TRUE;
    }
}

// tg: code/modules/hydroponics/grown/beans.dm:80
public partial class obj_item_food_grown_butterbeans : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/beans.dm:88
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        user.visible_message(span_notice(I18n.T("[] crushes [] into a pat of butter.", user, this)), span_notice(I18n.T("You crush [] into something that resembles butter.", this)));
        playsound(user, Dm.resource("sound/effects/blob/blobattack.ogg"), 50, TRUE);
        obj_item_food_butterslice butties = Dm.@new<obj_item_food_butterslice>((object)null);
        (butties.reagents as datum_reagents)?.set_all_reagents_purity(Dm.num(((obj_item_seeds)seed).get_reagent_purity()));
        qdel(this);
        await user.put_in_hands(butties);
        return TRUE;
    }
}
