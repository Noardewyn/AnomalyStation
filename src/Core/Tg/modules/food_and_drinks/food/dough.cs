// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/dough.dm
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

public partial class obj_item_food_dough : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/dough.dm:13
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_bread_plain), rand(30 * SECONDS, 45 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/dough.dm:17
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_ROLLINGPIN, typeof(obj_item_food_flatdough), 1, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Flatten"), Dm.named("sound_to_play", SFX_ROLLING_PIN_ROLLING));
    }
}

public partial class obj_item_food_flatdough : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/dough.dm:30
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizzabread), rand(30 * SECONDS, 45 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/dough.dm:34
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_doughslice), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_doughslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/dough.dm:62
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_bun), rand(20 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/dough.dm:65
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_bait_doughball), 5, 3 * SECONDS, Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_cakebatter : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/dough.dm:93
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_cake_plain), rand(70 * SECONDS, 90 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/dough.dm:96
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_ROLLINGPIN, typeof(obj_item_food_piedough), 1, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Flatten"), Dm.named("sound_to_play", SFX_ROLLING_PIN_ROLLING));
    }
}

public partial class obj_item_food_cakebatter_vegan : obj_item_food_cakebatter
{
    // tg: code/modules/food_and_drinks/food/dough.dm:104
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_cake_plain_vegan), rand(70 * SECONDS, 90 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_piedough : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/dough.dm:117
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pie_plain), rand(30 * SECONDS, 45 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/dough.dm:120
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_rawpastrybase), 6, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_rawpastrybase : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/dough.dm:134
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pastrybase), rand(20 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

// Gaps H2 (D333): the dough, bread, cake and pie bases' storage and custom-food holders (food_storage, ingredients_holder).

// tg: code/modules/food_and_drinks/food/dough.dm:37
public partial class obj_item_food_pizzabread : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/dough.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), typeof(obj_item_food_pizza_custom), CUSTOM_INGREDIENT_ICON_SCATTER, Dm.named("max_ingredients", 12)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/dough.dm:68
public partial class obj_item_food_bun : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/dough.dm:79
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), typeof(obj_item_food_burger_empty), CUSTOM_INGREDIENT_ICON_STACKPLUSTOP));
        return dot;
    }
}
