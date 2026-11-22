// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/cake.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable,
// make_dryable) and the energy cake's sword (:314-323) translated (gaps package H2, D333); the rest of the file is generated or waits for its
// package.

public partial class obj_item_food_cake_birthday : obj_item_food_cake
{
    // tg: code/modules/food_and_drinks/food/cake.dm:278
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), typeof(obj_item_clothing_head_utility_hardhat_cakehat));
    }
}

// tg: code/modules/food_and_drinks/food/cake.dm:294
public partial class obj_item_food_cake_birthday_energy : obj_item_food_cake_birthday
{
    // tg: code/modules/food_and_drinks/food/cake.dm:311
    public override async DmTask make_microwaveable() //super sekriter club
    {
        await AddElement(typeof(datum_element_microwavable), typeof(obj_item_clothing_head_utility_hardhat_cakehat_energycake));
    }

    // tg: code/modules/food_and_drinks/food/cake.dm:314
    public virtual async DmTask energy_bite(mob_living user = null)
    {
        to_chat(user, I18n.T("<font color='red' size='5'>As you eat the cake, you accidentally hurt yourself on the embedded energy sword!</font>"));
        await user.apply_damage(30, BRUTE, BODY_ZONE_HEAD);
        playsound(user, Dm.resource("sound/items/weapons/blade1.ogg"), 5, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/cake.dm:319
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (HAS_TRAIT(user, TRAIT_PACIFISM) && target_mob != user) //Prevents pacifists from attacking others directly
            return dot;
        // tg passes user as a second argument that energy_bite does not declare (DM drops it)
        await energy_bite(target_mob);
        return dot;
    }
}

public partial class obj_item_food_cake : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/cake.dm:22
    public override async DmTask make_processable()
    {
        if (slice_type != null)
            await AddElement(typeof(datum_element_processable), TOOL_KNIFE, slice_type, yield, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

// Gaps H2 (D333): the dough, bread, cake and pie bases' storage and custom-food holders (food_storage, ingredients_holder).

// tg: code/modules/food_and_drinks/food/cake.dm:1
public partial class obj_item_food_cake : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/cake.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_food_storage)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/cake.dm:38
public partial class obj_item_food_cake_plain : obj_item_food_cake
{
    // tg: code/modules/food_and_drinks/food/cake.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), cake_holder, CUSTOM_INGREDIENT_ICON_FILL, Dm.named("max_ingredients", 16)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/cake.dm:84
public partial class obj_item_food_cakeslice_empty : obj_item_food_cakeslice
{
    // tg: code/modules/food_and_drinks/food/cake.dm:90
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), null, CUSTOM_INGREDIENT_ICON_FILL, Dm.named("max_ingredients", 16)));
        return dot;
    }
}
