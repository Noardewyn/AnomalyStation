// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/lizard.dm
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

public partial class obj_item_food_raw_tiziran_sausage : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/lizard.dm:21
    public override async DmTask make_dryable()
    {
        await AddElement(typeof(datum_element_dryable), typeof(obj_item_food_tiziran_sausage));
    }
}

public partial class obj_item_food_raw_headcheese : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/lizard.dm:55
    public override async DmTask make_dryable()
    {
        await AddElement(typeof(datum_element_dryable), typeof(obj_item_food_headcheese));
    }
}

public partial class obj_item_food_headcheese : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/lizard.dm:73
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_headcheese_slice), 5, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_rootdough : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/lizard.dm:447
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), bread_type, rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/lizard.dm:450
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_ROLLINGPIN, flat_type, 1, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Flatten"), Dm.named("sound_to_play", SFX_ROLLING_PIN_ROLLING));
    }
}

public partial class obj_item_food_flatrootdough : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/lizard.dm:471
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, process_type, 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }

    // tg: code/modules/food_and_drinks/food/lizard.dm:474
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), grill_type, rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_rootdoughslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/lizard.dm:498
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_spaghetti_nizaya), 1, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }

    // tg: code/modules/food_and_drinks/food/lizard.dm:501
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_rootroll), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}
