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

// tg: code/modules/food_and_drinks/food/lizard.dm:130
public partial class obj_item_food_moonfish_eggs : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/lizard.dm:145
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //Moonfish can lay eggs inside aquariums (unaffected by breeding, so think of them as unfertilizard)
        RegisterSignal(this, COMSIG_AQUARIUM_CONTENT_GENERATE_APPEARANCE, nameof(generate_aquarium_appearance));
        RegisterSignal(this, COMSIG_AQUARIUM_CONTENT_RANDOMIZE_POSITION, nameof(randomize_aquarium_position));
        // AddComponent(/datum/component/aquarium_content): the aquariums come with the fishing (the component is inert until then)
        Dm.Deferred("code/modules/food_and_drinks/food/lizard.dm:150", "Initialize: AddComponent(/datum/component/aquarium_content), the eggs put in an aquarium (the fishing's aquariums)");
        RegisterSignal(this, COMSIG_MOVABLE_GET_AQUARIUM_BEAUTY, nameof(get_aquarium_beauty));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/lizard.dm:153
    [SignalHandler]
    public virtual void generate_aquarium_appearance(datum source = null, obj_effect_aquarium visual = null)
    {
        visual.icon = icon;
        visual.icon_state = "moonfish_eggs_aquarium";
        visual.layer_mode = AQUARIUM_LAYER_MODE_BOTTOM;
    }

    // tg: code/modules/food_and_drinks/food/lizard.dm:159
    [SignalHandler]
    public virtual void randomize_aquarium_position(datum source = null, obj_structure_aquarium current_aquarium = null, obj_effect_aquarium visual = null)
    {
        float sprite_width = 5;
        float sprite_height = 4;
        float pw_min = Dm.num(visual.aquarium_zone_min_pw);
        float pw_max = Dm.num(visual.aquarium_zone_max_pw) - sprite_width;
        float pz_min = Dm.num(visual.aquarium_zone_min_pz) - sprite_height;

        visual.pixel_w = rand(pw_min, pw_max);
        visual.pixel_z = pz_min + rand(-1, 1);
    }

    // tg: code/modules/food_and_drinks/food/lizard.dm:170
    [SignalHandler]
    public virtual void get_aquarium_beauty(datum source = null, DmList beauty_holder = null)
    {
        Dm.append(beauty_holder, 100); //moonfish eggs are kinda eye candy
    }
}

// tg: code/modules/food_and_drinks/food/lizard.dm:537
public partial class obj_item_food_bread_root : obj_item_food_bread
{
    // tg: code/modules/food_and_drinks/food/lizard.dm:549
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), typeof(obj_item_food_bread_empty), CUSTOM_INGREDIENT_ICON_FILL, Dm.named("max_ingredients", 8)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/lizard.dm:557
public partial class obj_item_food_breadslice_root : obj_item_food_breadslice
{
    // tg: code/modules/food_and_drinks/food/lizard.dm:567
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), null, CUSTOM_INGREDIENT_ICON_STACK));
        return dot;
    }
}
