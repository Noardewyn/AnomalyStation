// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/pizza.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the file whole but the whole pizza's screentip (add_context, a live-partial row). The whole pizza's own procs came
// with the syndicate campaign G2 (D316: the pizza box and its bomb hold one): slicing it with a knife, taking the slices out by
// hand, cutting it apart; the kitchen (gaps package H2, D333) brought the baking of the raw pizzas, the margherita slice's
// ingredients, the pizzeria slice, the moldy slice and Arnold's pizza.

// tg: code/modules/food_and_drinks/food/pizza.dm:2
public partial class obj_item_food_pizza : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        register_context();
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/pizza.dm:47
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (slice_type == null || !Dm.truthy(sliced))
            return dot;
        dot.Add(span_notice(I18n.T("You can slice this to make it possible to take out slices with an empty hand!")));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/pizza.dm:53
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = NONE;
        if (slice_type == null || !Dm.@in(tool.tool_behaviour, cutting_tools))
            return dot;
        if (!Dm.truthy(sliced))
        {
            slice(user, tool);
            return ITEM_INTERACT_SUCCESS;
        }
        user.visible_message(span_notice(I18n.T("[] seperates [] into individual slices with [].", user, this, tool)));
        await cut_apart();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/food/pizza.dm:64
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = NONE;
        if (slice_type == null || !Dm.@in(tool.tool_behaviour, cutting_tools))
            return dot;
        visible_message(span_notice(I18n.T("[] seperates [] into individual slices with [].", user, this, tool)));
        await cut_apart();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/food/pizza.dm:72
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(sliced))
            return dot;
        user.visible_message(span_notice(I18n.T("[] takes a slice of [].", user, this)), span_notice(I18n.T("You take a slice of [].", this)));
        await produce_slice(user);
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/pizza.dm:79
    public virtual object get_slices_filter() //to not repeat code
    {
        return alpha_mask_filter(icon: icon(Dm.resource("icons/obj/food/pizza.dmi"), Dm.text("[]slices", slices_left)));
    }

    /// slices this pizza. all arguments optional.
    // tg: code/modules/food_and_drinks/food/pizza.dm:83
    public virtual void slice(mob user = null, obj_item tool = null)
    {
        if (Dm.truthy(sliced))
            return;
        tool?.play_tool_sound(this);
        sliced = TRUE;
        user?.visible_message(span_notice(I18n.T("[] cuts [] into 6 slices with [].", user, this, tool)));
        interaction_flags_item = Dm.num(Dm.bitand(interaction_flags_item, Dm.bitnot(INTERACT_ITEM_ATTACK_HAND_PICKUP)));
    }

    // tg: code/modules/food_and_drinks/food/pizza.dm:91
    public virtual async DmTask cut_apart()
    {
        float __end = slices_left;
        for (float _ = 1; _ <= __end; _++)
            await produce_slice(no_update: TRUE);
    }

    /// make a slice and give it to user. no_update means no filter work is done. user is optional
    // tg: code/modules/food_and_drinks/food/pizza.dm:96
    public virtual async DmTask produce_slice(mob user = null, float? no_update = null)
    {
        no_update ??= FALSE;
        turf our_turf = get_turf(this) as turf;
        obj_item_food_pizzaslice slice = Dm.@new(slice_type, our_turf) as obj_item_food_pizzaslice;
        if (HAS_TRAIT(this, TRAIT_FOOD_SILVER))
            ADD_TRAIT(slice, TRAIT_FOOD_SILVER, INNATE_TRAIT);
        if (HAS_TRAIT(this, TRAIT_HANDMADE))
            ADD_TRAIT(slice, TRAIT_HANDMADE, (GET_TRAIT_SOURCES(this, TRAIT_HANDMADE) as DmList)[1]); // wack thing to inherit first source
        slice.pixel_x += rand(-6, 6);
        slice.pixel_y += rand(-6, 6);
        if (user != null)
            await user.put_in_active_hand(slice);

        await (slice.reagents as datum_reagents).remove_all();
        await (reagents as datum_reagents).trans_to(slice, amount: (reagents as datum_reagents).total_volume / slices_left, no_react: TRUE);
        if (Dm.truthy(_listen_lookup?[COMSIG_PIZZA_SLICE_TAKEN]))
            _SendSignal(COMSIG_PIZZA_SLICE_TAKEN, Dm.list(this, user, slice));

        slices_left--;
        if (slices_left <= 0)
        {
            qdel(this);
            return;
        }
        if (Dm.truthy(no_update))
            return;
        remove_filter("pizzaslices");
        add_filter("pizzaslices", 1, get_slices_filter() as DmList);
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_pizza_margherita_raw : obj_item_food_pizza_margherita
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:168
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_margherita), rand(70 * SECONDS, 80 * SECONDS), TRUE, TRUE);
    }
}

// tg: code/modules/food_and_drinks/food/pizza.dm:180
public partial class obj_item_food_pizzaslice_margherita : obj_item_food_pizzaslice
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:188
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), null, CUSTOM_INGREDIENT_ICON_FILL, Dm.named("max_ingredients", 12)));
        return dot;
    }
}

public partial class obj_item_food_pizza_meat_raw : obj_item_food_pizza_meat
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:214
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_meat), rand(70 * SECONDS, 80 * SECONDS), TRUE, TRUE);
    }
}

//Reward for pizzeria bitrunning domain; not really meant to be eaten but functionality as food still exists for remote tasters and absurdly determined persons.
// tg: code/modules/food_and_drinks/food/pizza.dm:225
public partial class obj_item_food_pizzaslice_meat_pizzeria : obj_item_food_pizzaslice_meat
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:236
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_FOOD_CONSUMED, nameof(on_consumed));
        return dot;
    }

    /// Little bit of flavor for the absurdly persistent pizza eater.
    // tg: code/modules/food_and_drinks/food/pizza.dm:241
    [SignalHandler]
    public virtual object on_consumed(datum source = null, mob_living eater = null, mob feeder = null)
    {
        to_chat(eater, span_notice(I18n.T("You finally finish eating that piece of trash, and your stomach pains reflect that. Surely there was a better use for this slice of pizza...")));

        return NONE;
    }
}

public partial class obj_item_food_pizza_mushroom_raw : obj_item_food_pizza_mushroom
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:269
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_mushroom), rand(70 * SECONDS, 80 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_pizza_vegetable_raw : obj_item_food_pizza_vegetable
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:303
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_vegetable), rand(70 * SECONDS, 80 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_pizza_donkpocket_raw : obj_item_food_pizza_donkpocket
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:338
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_donkpocket), rand(70 * SECONDS, 80 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_pizza_dank_raw : obj_item_food_pizza_dank
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:371
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_dank), rand(70 * SECONDS, 80 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_pizza_sassysage_raw : obj_item_food_pizza_sassysage
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:404
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_sassysage), rand(70 * SECONDS, 80 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_pizza_pineapple_raw : obj_item_food_pizza_pineapple
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:439
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_pineapple), rand(70 * SECONDS, 80 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_pizza_arnold_raw : obj_item_food_pizza_arnold
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:501
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_arnold), rand(70 * SECONDS, 80 * SECONDS), TRUE, TRUE);
    }
}

// tg: code/modules/food_and_drinks/food/pizza.dm:467
public partial class obj_item_food_pizzaslice_moldy_bacteria : obj_item_food_pizzaslice_moldy
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:471
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_MOLD, CELL_VIRUS_TABLE_GENERIC, rand(2, 4), 25));
        return dot;
    }
}

//fuck it, i will leave this at the food level for now.
public partial class obj_item_food : obj_item
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:505
    public virtual async DmTask try_break_off(mob_living attacker = null, mob_living user = null) //maybe i give you a pizza maybe i break off your arm
    {
        if (prob(50) || (attacker != user) || !iscarbon(user) || HAS_TRAIT(user, TRAIT_NODISMEMBER))
            return;
        obj_item_bodypart_arm left = user.get_bodypart(BODY_ZONE_L_ARM) as obj_item_bodypart_arm;
        obj_item_bodypart_arm right = user.get_bodypart(BODY_ZONE_R_ARM) as obj_item_bodypart_arm;
        object did_the_thing = (left != null ? await left.dismember() : null) is var or__1 && Dm.truthy(or__1) ? or__1 : (right != null ? await right.dismember() : null); //not all limbs can be removed, so important to check that we did. the. thing.
        if (!Dm.truthy(did_the_thing))
            return;
        to_chat(user, span_userdanger(I18n.T("Maybe I'll give you a pizza, maybe I'll break off your arm."))); //makes the reference more obvious
        user.visible_message(span_warning(I18n.T("\\The [] breaks off []'s arm!", this, user)), span_warning(I18n.T("\\The [] breaks off your arm!", this)));
        playsound(user, SFX_DESECRATION, 50, TRUE, -1);
    }

    // tg: code/modules/food_and_drinks/food/pizza.dm:517
    public virtual async DmTask<object> i_kill_you(obj_item item = null, mob_living user = null)
    {
        if (istype(item, typeof(obj_item_food_pineappleslice)))
        {
            to_chat(user, I18n.T("<font color='red' size='7'>If you want something crazy like pineapple, I'll kill you.</font>")); //this is in bigger text because it's hard to spam something that gibs you, and so that you're perfectly aware of the reason why you died
            user.investigate_log("has been gibbed by putting pineapple on an arnold pizza.", INVESTIGATE_DEATHS);
            await user.gib(DROP_ALL_REMAINS); //if you want something crazy like pineapple, i'll kill you
            return TRUE;
        }

        if (istype(item, typeof(obj_item_food_grown_mushroom)) && iscarbon(user))
        {
            to_chat(user, span_userdanger(I18n.T("So, if you want mushroom, shut up."))); //not as large as the pineapple text, because you could in theory spam it
            mob_living_carbon shutup = user as mob_living_carbon;
            await shutup.gain_trauma(typeof(datum_brain_trauma_severe_mute));
            return TRUE;
        }

        return FALSE;
    }
}

// tg: code/modules/food_and_drinks/food/pizza.dm:477
public partial class obj_item_food_pizza_arnold : obj_item_food_pizza
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:532
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        await try_break_off(target_mob, user);
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/pizza.dm:536
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await i_kill_you(tool, user)))
            return await base.item_interaction(user, tool, modifiers, __extra);

        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/food_and_drinks/food/pizza.dm:542
public partial class obj_item_food_pizzaslice_arnold : obj_item_food_pizzaslice
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:550
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        await try_break_off(target_mob, user);
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/pizza.dm:554
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await i_kill_you(tool, user)))
            return await base.item_interaction(user, tool, modifiers, __extra);

        return ITEM_INTERACT_SUCCESS;
    }
}

public partial class obj_item_food_pizza_energy_raw : obj_item_food_pizza_energy
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:594
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pizza_energy), rand(70 * SECONDS, 80 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_pizzaslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:132
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_ROLLINGPIN, typeof(obj_item_stack_sheet_pizza), 1, 1 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Flatten"), Dm.named("sound_to_play", SFX_ROLLING_PIN_ROLLING));
    }
}

public partial class obj_item_food_raw_meat_calzone : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:620
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_meat_calzone), rand(20 * SECONDS, 40 * SECONDS), TRUE, TRUE);
    }
}

public partial class obj_item_food_raw_vegetarian_calzone : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pizza.dm:651
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_vegetarian_calzone), rand(20 * SECONDS, 40 * SECONDS), TRUE, TRUE);
    }
}
