// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/martian.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable,
// make_dryable) and the kamaboko's design (:76) translated (gaps package H2, D333); the rest of the file is generated or waits for its
// package.

public partial class obj_item_food_bread_reispan : obj_item_food_bread
{
    // tg: code/modules/food_and_drinks/food/martian.dm:211
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_breadslice_reispan), 5, 3 * SECONDS, Dm.named("table_required", TRUE));
    }
}

public partial class obj_item_food_cake_spekkoek : obj_item_food_cake
{
    // tg: code/modules/food_and_drinks/food/martian.dm:1088
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_cakeslice_spekkoek), 5, 3 * SECONDS, Dm.named("table_required", TRUE));
    }
}

public partial class obj_item_food_kebab_raw_ballpark_tsukune : obj_item_food_kebab
{
    // tg: code/modules/food_and_drinks/food/martian.dm:1357
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_kebab_ballpark_tsukune), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_surimi : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/martian.dm:59
    public override async DmTask make_dryable()
    {
        await AddElement(typeof(datum_element_dryable), typeof(obj_item_food_kamaboko));
    }
}

// tg: code/modules/food_and_drinks/food/martian.dm:62
public partial class obj_item_food_kamaboko : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/martian.dm:76
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        string design = Dm.str(Dm.pick("smiling", "spiral", "star", "sunrise"));
        name = I18n.T("[] kamaboko", design);
        icon_state = Dm.text("kamaboko_[]", design);
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/martian.dm:82
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_kamaboko_slice), 4, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_rice_dough : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/martian.dm:142
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_bread_reispan), rand(30 * SECONDS, 45 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/martian.dm:145
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_spaghetti_rawnoodles), 6, 3 * SECONDS, Dm.named("table_required", TRUE));
    }
}

public partial class obj_item_food_raw_ballpark_pretzel : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/martian.dm:1322
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_ballpark_pretzel), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/martian.dm:1325
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_ballpark_pretzel), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}
