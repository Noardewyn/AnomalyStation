// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/moth.dm
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

public partial class obj_item_food_raw_green_lasagne : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:179
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_green_lasagne), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_green_lasagne : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:196
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_green_lasagne_slice), 6, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_raw_baked_rice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:227
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_big_baked_rice), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_big_baked_rice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:245
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_lil_baked_rice), 6, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_raw_ratatouille : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:322
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_ratatouille), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_raw_stuffed_peppers : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:368
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_stuffed_peppers), rand(10 * SECONDS, 20 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_raw_mothic_margherita : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:553
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_mothic_margherita), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_raw_mothic_firecracker : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:598
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_mothic_firecracker), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_raw_mothic_five_cheese : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:643
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_mothic_five_cheese), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_raw_mothic_white_pie : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:687
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_mothic_white_pie), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_raw_mothic_pesto : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:731
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_mothic_pesto), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_raw_mothic_garlic : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/moth.dm:775
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_mothic_garlic), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}
