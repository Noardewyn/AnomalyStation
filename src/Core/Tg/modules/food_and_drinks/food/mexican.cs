// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/mexican.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable, make_dryable)
// came with the kitchen (gaps H2, D333); the hard taco shell's ingredients with the restaurant. The var blocks are generated.

public partial class obj_item_food_tortilla : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/mexican.dm:15
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_hard_taco_shell), rand(15 * SECONDS, 30 * SECONDS), TRUE, TRUE);
    }
}

// tg: code/modules/food_and_drinks/food/mexican.dm:253
public partial class obj_item_food_hard_taco_shell : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/mexican.dm:264
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), typeof(obj_item_food_hard_taco_shell_empty), CUSTOM_INGREDIENT_ICON_FILL, Dm.named("max_ingredients", 6)));
        return dot;
    }
}
