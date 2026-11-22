// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/meatdish.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable,
// make_processable, make_dryable), the fish fillets (:23-168), the sashimi, the nuggets' shapes, the clown's meat and the fried
// chicken. The var blocks are generated (D111).

public partial class obj_item_food_fishmeat_moonfish : obj_item_food_fishmeat
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:114
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_grilled_moonfish), rand(40 * SECONDS, 50 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_fishmeat_octopus : obj_item_food_fishmeat
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:153
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_grilled_octopus), rand(15 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_vegetariansushiroll : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:231
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_vegetariansushislice), 4, Dm.named("screentip_verb", "Chop"));
    }
}

public partial class obj_item_food_spicyfiletsushiroll : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:262
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_spicyfiletsushislice), 4, Dm.named("screentip_verb", "Chop"));
    }
}

public partial class obj_item_food_sushi_empty : obj_item_food_sushi
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:289
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_sushislice_empty), 4, Dm.named("screentip_verb", "Chop"));
    }
}

public partial class obj_item_food_tempeh : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:361
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_tempehslice), 4, 5 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_spiderleg : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:416
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_boiledspiderleg), rand(50 * SECONDS, 60 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_raw_meatball : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:484
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), meatball_type, rand(30 * SECONDS, 40 * SECONDS), TRUE);
    }

    // tg: code/modules/food_and_drinks/food/meatdish.dm:487
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_ROLLINGPIN, patty_type, 1, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Flatten"), Dm.named("sound_to_play", SFX_ROLLING_PIN_ROLLING));
    }
}

public partial class obj_item_food_raw_patty : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:561
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), patty_type, rand(30 * SECONDS, 40 * SECONDS), TRUE);
    }
}

public partial class obj_item_food_raw_sausage : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:638
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_sausage), rand(60 * SECONDS, 75 * SECONDS), TRUE);
    }
}

public partial class obj_item_food_sausage : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:660
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_salami), 6, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_sausage_american), 1, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_sausage_american : obj_item_food_sausage
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:669
    public override async DmTask make_processable()
    {
        return;
    }
}

public partial class obj_item_food_rawkhinkali : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:702
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_khinkali), rand(50 * SECONDS, 60 * SECONDS), TRUE);
    }
}

public partial class obj_item_food_beef_wellington : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:1072
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_beef_wellington_slice), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_korta_wellington : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:1107
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_korta_wellington_slice), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_roast_dinner : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:1142
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_roast_slice), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_roast_dinner_lizzy : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:1177
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_roast_slice_lizzy), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_roast_dinner_tofu : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:1211
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_roast_slice_tofu), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_raw_meatloaf : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:1261
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_meatloaf), rand(30 * SECONDS, 40 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_meatloaf : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:1280
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_meatloaf_slice), 4, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_futomaki_sushi_roll : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:1364
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_futomaki_sushi_slice), 4, Dm.named("screentip_verb", "Chop"));
    }
}

public partial class obj_item_food_philadelphia_sushi_roll : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:1395
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_philadelphia_sushi_slice), 4, Dm.named("screentip_verb", "Chop"));
    }
}

// Gaps H2 (D333): the fish fillets' names from what they were cut from, the quality fillets and the carp fillet's swab.

// tg: code/modules/food_and_drinks/food/meatdish.dm:23
public partial class obj_item_food_fishmeat : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_FOOD_DONT_INHERIT_NAME_FROM_PROCESSED, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/meatdish.dm:44
    public override async DmTask OnCreatedFromProcessing(mob_living user = null, obj_item work_tool = null, DmList chosen_option = null, atom original_atom = null)
    {
        await base.OnCreatedFromProcessing(user, work_tool, chosen_option, original_atom);
        name = I18n.T("[]", replacetext(fillet_name, "%NAME", original_atom.name));
        Dm.mask(ref material_flags, Dm.bitnot(MATERIAL_ADD_PREFIX)); //don't double down on material prefixes
    }
}

// tg: code/modules/food_and_drinks/food/meatdish.dm:49
public partial class obj_item_food_fishmeat_quality : obj_item_food_fishmeat
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_quality_food_ingredient), FOOD_COMPLEXITY_1));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatdish.dm:63
public partial class obj_item_food_fishmeat_salmon : obj_item_food_fishmeat
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:75
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_quality_food_ingredient), FOOD_COMPLEXITY_1));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatdish.dm:79
public partial class obj_item_food_fishmeat_carp : obj_item_food_fishmeat
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:90
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(cell_line))
            Waiting.NoSleep(AddElement(typeof(datum_element_swabable), cell_line, CELL_VIRUS_TABLE_GENERIC_MOB));
        return dot;
    }
}

///carp fillet, but without the toxin. Used by baby carps (fish item), which have a trait that handles the toxin already.
// tg: code/modules/food_and_drinks/food/meatdish.dm:102
public partial class obj_item_food_fishmeat_carp_no_tox : obj_item_food_fishmeat_carp
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:104
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.remove(ref food_reagents, typeof(datum_reagent_toxin_carpotoxin));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/food_and_drinks/food/meatdish.dm:156
public partial class obj_item_food_fishmeat_fish_tail : obj_item_food_fishmeat
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:168
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_quality_food_ingredient), FOOD_COMPLEXITY_1));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatdish.dm:785
public partial class obj_item_food_sashimi : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:802
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CARP, CELL_VIRUS_TABLE_GENERIC_MOB));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatdish.dm:806
public partial class obj_item_food_nugget : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:826
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        string shape = Dm.str(Dm.pick("lump", "star", "lizard", "corgi"));
        desc = I18n.T("A [] nugget vaguely shaped like a [].", meat_source, shape);
        icon_state = Dm.text("nugget_[]", shape);
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatdish.dm:891
public partial class obj_item_food_meatclown : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:907
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_slippery), 3 * SECONDS));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/meatdish.dm:1019
public partial class obj_item_food_fried_chicken : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/meatdish.dm:1032
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(50))
            icon_state = "fried_chicken2";
        return dot;
    }
}
