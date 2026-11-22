// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/cheese.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * # Abstract cheese class
 *
 * Everything that is a subclass of this counts as cheese for regal rats.
 */
// tg: code/modules/food_and_drinks/food/cheese.dm:7
public partial class obj_item_food_cheese : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/cheese.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_RAT_INTERACT, nameof(on_rat_eat));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/cheese.dm:21
    [SignalHandler]
    public virtual object on_rat_eat(datum source = null, mob_living_basic_regal_rat king = null)
    {
        king.cheese_heal(this, rat_heal, span_green(Dm.text("You eat [], restoring some health.", this)));
        return COMPONENT_RAT_INTERACTED;
    }
}

// tg: code/modules/food_and_drinks/food/cheese.dm:27
public partial class obj_item_food_cheese_wedge : obj_item_food_cheese
{
    // tg: code/modules/food_and_drinks/food/cheese.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_food_storage)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/cheese.dm:44
public partial class obj_item_food_cheese_wheel : obj_item_food_cheese
{
    // tg: code/modules/food_and_drinks/food/cheese.dm:57
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_food_storage)));
        return dot;
    }

    /**
     * Whiffs away cheese that was touched by the chaos entity byond the realm. In layman's terms, deletes the cheese and throws sparks.
     * Used in wizard grand rituals' optional cheesy alternative.
     */
    // tg: code/modules/food_and_drinks/food/cheese.dm:71
    public virtual void consume_cheese()
    {
        visible_message(span_revenwarning("...and is consumed in a vortex of chaos!"));
        do_sparks(number: 1, cardinal_only: TRUE, source: get_turf(this));
        qdel(this);
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_cheese_wheel : obj_item_food_cheese
{
    // tg: code/modules/food_and_drinks/food/cheese.dm:61
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_cheese_wedge), 5, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }

    // tg: code/modules/food_and_drinks/food/cheese.dm:64
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_baked_cheese), rand(20 * SECONDS, 25 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_cheese_curd_cheese : obj_item_food_cheese
{
    // tg: code/modules/food_and_drinks/food/cheese.dm:107
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_cheese_cheese_curds), rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/cheese.dm:110
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), typeof(obj_item_food_cheese_cheese_curds));
    }
}

public partial class obj_item_food_cheese_cheese_curds : obj_item_food_cheese
{
    // tg: code/modules/food_and_drinks/food/cheese.dm:121
    public override async DmTask make_dryable()
    {
        await AddElement(typeof(datum_element_dryable), typeof(obj_item_food_cheese_firm_cheese));
    }
}

public partial class obj_item_food_cheese_firm_cheese : obj_item_food_cheese
{
    // tg: code/modules/food_and_drinks/food/cheese.dm:133
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_cheese_firm_cheese_slice), 3, 3 * SECONDS, Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_cheese_firm_cheese_slice : obj_item_food_cheese
{
    // tg: code/modules/food_and_drinks/food/cheese.dm:149
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_grilled_cheese), rand(25 * SECONDS, 35 * SECONDS), TRUE, TRUE);
    }
}

// Gaps H2 (D333): the dough, bread, cake and pie bases' storage and custom-food holders (food_storage, ingredients_holder).

// tg: code/modules/food_and_drinks/food/cheese.dm:136
public partial class obj_item_food_cheese_firm_cheese_slice : obj_item_food_cheese
{
    // tg: code/modules/food_and_drinks/food/cheese.dm:145
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_food_storage)));
        return dot;
    }
}
