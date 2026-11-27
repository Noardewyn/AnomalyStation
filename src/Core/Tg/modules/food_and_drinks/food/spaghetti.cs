// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/spaghetti.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable, make_dryable)
// came with the kitchen (gaps H2, D333); the carbonara's Italian speech with the restaurant. The var blocks are generated.

public partial class obj_item_food_spaghetti_raw : obj_item_food_spaghetti
{
    // tg: code/modules/food_and_drinks/food/spaghetti.dm:29
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_spaghetti_boiledspaghetti), rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/spaghetti.dm:32
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), typeof(obj_item_food_spaghetti_boiledspaghetti));
    }
}

public partial class obj_item_food_spaghetti : obj_item_food
{
    // Why are you putting cooked spaghetti in your pockets?
    // tg: code/modules/food_and_drinks/food/spaghetti.dm:14
    public override async DmTask make_microwaveable()
    {
        DmList display_message = Dm.list(
            span_notice(I18n.T("Something wet falls out of their pocket and hits the ground. Is that... []?", name)),
            span_warning(I18n.T("Oh shit! All your pocket [] fell out!", name)));
        await AddComponent(typeof(datum_component_spill), display_message, Dm.resource("sound/effects/splat.ogg"), typeof(datum_memory_lost_spaghetti));

        await base.make_microwaveable();
        return;
    }
}

// Gaps H2 (D333): the dough, bread, cake and pie bases' storage and custom-food holders (food_storage, ingredients_holder).

// tg: code/modules/food_and_drinks/food/spaghetti.dm:35
public partial class obj_item_food_spaghetti_boiledspaghetti : obj_item_food_spaghetti
{
    // tg: code/modules/food_and_drinks/food/spaghetti.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), null, CUSTOM_INGREDIENT_ICON_SCATTER, Dm.named("max_ingredients", 6)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/spaghetti.dm:262
public partial class obj_item_food_spaghetti_carbonara : obj_item_food_spaghetti
{
    // tg: code/modules/food_and_drinks/food/spaghetti.dm:276
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_love_food_buff), typeof(datum_status_effect_food_speech_italian)));
        return dot;
    }
}
