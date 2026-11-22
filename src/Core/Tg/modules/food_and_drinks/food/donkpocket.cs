// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/donkpocket.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable,
// make_dryable) translated (gaps package H2, D333); the rest of the file is generated or waits for its package.

public partial class obj_item_food_donkpocket : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/donkpocket.dm:28
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), warm_type, rand(baking_time_short, baking_time_long), positive_result, TRUE, Dm.list(Dm.kv(typeof(datum_reagent_medicine_omnizine), omnizine_to_add)));
    }

    // tg: code/modules/food_and_drinks/food/donkpocket.dm:31
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), warm_type, string_assoc_list(Dm.list(Dm.kv(typeof(datum_reagent_medicine_omnizine), omnizine_to_add))), Dm.named("bad_recipe", Dm.num(!Dm.truthy(positive_result))));
    }
}

public partial class obj_item_food_donkpocket_honk : obj_item_food_donkpocket
{
    // tg: code/modules/food_and_drinks/food/donkpocket.dm:207  var/static/list/honk_added_reagents (rules 10.6)
    public static DmList honk_added_reagents => TgState.Current.obj_item_food_donkpocket_honk__honk_added_reagents ??= Dm.list(
        Dm.kv(typeof(datum_reagent_medicine_omnizine), 2),
        Dm.kv(typeof(datum_reagent_consumable_laughter), 6)
    );

    // tg: code/modules/food_and_drinks/food/donkpocket.dm:213
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), warm_type, rand(baking_time_short, baking_time_long), positive_result, TRUE, honk_added_reagents);
    }

    // tg: code/modules/food_and_drinks/food/donkpocket.dm:216
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), warm_type, honk_added_reagents, positive_result);
    }
}

public partial class obj_item_food_donkpocket_gondola : obj_item_food_donkpocket
{
    // tg: code/modules/food_and_drinks/food/donkpocket.dm:275  var/static/list/gondola_added_reagents (rules 10.6)
    public static DmList gondola_added_reagents => TgState.Current.obj_item_food_donkpocket_gondola__gondola_added_reagents ??= Dm.list(
        Dm.kv(typeof(datum_reagent_medicine_omnizine), 2),
        Dm.kv(typeof(datum_reagent_gondola_mutation_toxin), 5)
    );

    // tg: code/modules/food_and_drinks/food/donkpocket.dm:280
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), warm_type, rand(baking_time_short, baking_time_long), positive_result, TRUE, gondola_added_reagents);
    }

    // tg: code/modules/food_and_drinks/food/donkpocket.dm:283
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), warm_type, gondola_added_reagents, positive_result);
    }
}

public partial class obj_item_food_donkpocket_deluxe : obj_item_food_donkpocket
{
    // tg: code/modules/food_and_drinks/food/donkpocket.dm:315  var/static/list/deluxe_added_reagents (rules 10.6)
    public static DmList deluxe_added_reagents => TgState.Current.obj_item_food_donkpocket_deluxe__deluxe_added_reagents ??= Dm.list(
        Dm.kv(typeof(datum_reagent_medicine_omnizine), 8)
    );

    // tg: code/modules/food_and_drinks/food/donkpocket.dm:320
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), warm_type, rand(baking_time_short, baking_time_long), positive_result, TRUE, deluxe_added_reagents);
    }

    // tg: code/modules/food_and_drinks/food/donkpocket.dm:323
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), warm_type, deluxe_added_reagents, positive_result);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/food/donkpocket.dm:207, :275, :315  the donk pockets' var/static added reagents
    internal DmList obj_item_food_donkpocket_honk__honk_added_reagents;
    internal DmList obj_item_food_donkpocket_gondola__gondola_added_reagents;
    internal DmList obj_item_food_donkpocket_deluxe__deluxe_added_reagents;
}
