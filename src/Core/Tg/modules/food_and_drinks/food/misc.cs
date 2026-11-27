// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable, make_dryable),
// the burned mess (the bad recipe, :196-262) and the butter on a stick (:392-412) came with the kitchen (gaps H2, D333); the melons'
// and fruits' juices, the sushi sheet's and the onigiri's ingredients, the pickle a coroner likes, the candied pineapple's look and
// the ink sac's splat with the restaurant. The huge mushroom slice is in _food.cs. The var blocks are generated.

// tg: code/modules/food_and_drinks/food/misc.dm:196
public partial class obj_item_food_badrecipe : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:207
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_GRILL_PROCESS, nameof(OnGrill));
        RegisterSignals(this, Dm.list(COMSIG_ITEM_GRILLED_RESULT, COMSIG_ITEM_BAKED_RESULT, COMSIG_ITEM_MICROWAVE_COOKED_RESULT, COMSIG_OBJ_DECOMPOSITION_RESULT), nameof(convert_to_bad_food));
        if (stink_particles != null)
            add_shared_particles(stink_particles);
        return dot;
    }

    ///Prevents grilling burnt shit from well, burning.
    // tg: code/modules/food_and_drinks/food/misc.dm:215
    [SignalHandler]
    public virtual object OnGrill()
    {
        return COMPONENT_HANDLED_GRILLING;
    }

    /**
     * The bad food reagent is cleared when cooked rather than just spawned and the reagents of the item this is from are transferred to this instead,
     * So we want to convert most of the consumable reagents into bad food, which is what makes the burned mess a bad thing to eat, taste aside.
     */
    // tg: code/modules/food_and_drinks/food/misc.dm:223
    [SignalHandler]
    public virtual void convert_to_bad_food(atom source = null)
    {
        datum_reagents reagents = (datum_reagents)this.reagents;
        float bad_food_amount = 0;
        foreach (datum_reagent_consumable food_reagent in Dm.each<datum_reagent_consumable>(reagents.reagent_list))
        {
            float amount_to_remove = Dm.num(food_reagent.volume) * rand(6, 8) * 0.1f; //around 60% to 80% of the volume is to be converted.
            food_reagent.volume = Dm.num(food_reagent.volume) - amount_to_remove;
            bad_food_amount += amount_to_remove;
        }
        Waiting.NoSleep(reagents.update_total());
        Waiting.NoSleep(reagents.add_reagent(typeof(datum_reagent_toxin_bad_food), bad_food_amount, reagtemp: reagents.chem_temp));
    }

    // tg: code/modules/food_and_drinks/food/misc.dm:233
    public override object Destroy(float? force = null)
    {
        if (stink_particles != null)
            remove_shared_particles(stink_particles);
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/food/misc.dm:239
    public override async DmTask make_grillable()
    {
        return;
    }

    // tg: code/modules/food_and_drinks/food/misc.dm:241
    public override async DmTask make_bakeable()
    {
        return;
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:381
public partial class obj_item_food_butter : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:392
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(can_stick))
            dot.Add(span_notice(I18n.T("If you had a rod you could make <b>butter on a stick</b>.")));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/misc.dm:397
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stack_rods)) || !Dm.truthy(can_stick))
            return NONE;

        obj_item_stack_rods rods = tool as obj_item_stack_rods;
        if (!Dm.truthy(await rods.use(1)))//borgs can still fail this if they have no metal
        {
            to_chat(user, span_warning(I18n.T("You do not have enough iron to put [] on a stick!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You stick the rod into the stick of butter.")));
        await user.temporarilyRemoveItemFromInventory(this);
        obj_item_food_butter_on_a_stick new_item = Dm.@new<obj_item_food_butter_on_a_stick>(drop_location());
        if (Dm.truthy(await new_item.IsReachableBy(user)))
            await user.put_in_hands(new_item);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/food/misc.dm:423
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_butterslice), 3, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_moussaka : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:727
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_moussaka_slice), 4, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_raw_pita_bread : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:777
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_pita_bread), rand(15 * SECONDS, 30 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/misc.dm:780
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_pita_bread), rand(15 * SECONDS, 30 * SECONDS), TRUE, TRUE);
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:254
public partial class obj_item_food_badrecipe_moldy_bacteria : obj_item_food_badrecipe_moldy
{
    // tg: code/modules/food_and_drinks/food/misc.dm:259
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_MOLD, CELL_VIRUS_TABLE_GENERIC, rand(2, 4), 25));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:3
public partial class obj_item_food_watermelonslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:18
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_watermelonjuice);
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:21
public partial class obj_item_food_watermelonmush : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:36
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_watermelonjuice);
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:39
public partial class obj_item_food_holymelonslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:54
    public override object juice_typepath()
    {
        return typeof(datum_reagent_water_holywater);
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:57
public partial class obj_item_food_holymelonmush : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:72
    public override object juice_typepath()
    {
        return typeof(datum_reagent_water_holywater);
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:75
public partial class obj_item_food_barrelmelonslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:90
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_ethanol_beer);
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:93
public partial class obj_item_food_barrelmelonmush : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:108
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_ethanol_beer);
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:111
public partial class obj_item_food_appleslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:126
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_applejuice);
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:446
public partial class obj_item_food_pineappleslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:454
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_pineapplejuice);
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:544
public partial class obj_item_food_seaweedsheet : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:556
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), typeof(obj_item_food_sushi_empty), CUSTOM_INGREDIENT_ICON_FILL, Dm.named("max_ingredients", 6)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:587
public partial class obj_item_food_onigiri : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:601
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), typeof(obj_item_food_onigiri_empty), CUSTOM_INGREDIENT_ICON_NOCHANGE, Dm.named("max_ingredients", 4)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:628
public partial class obj_item_food_pickle : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:643
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_pickle);
    }

    // tg: code/modules/food_and_drinks/food/misc.dm:646
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("check_liked", Dm.@new<datum_callback>(this, nameof(check_liked))));
    }

    // tg: code/modules/food_and_drinks/food/misc.dm:650
    public virtual object check_liked(mob_living_carbon_human consumer = null)
    {
        obj_item_organ_liver liver = consumer.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (!HAS_TRAIT(consumer, TRAIT_AGEUSIA) && liver != null && HAS_TRAIT(liver, TRAIT_CORONER_METABOLISM))
            return FOOD_LIKED;
        return null;
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:744
public partial class obj_item_food_candied_pineapple : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:759
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("[]_[]", base_icon_state, rand(1, 3));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:852
public partial class obj_item_food_ink_sac : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:860
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_splat),
            Dm.named("memory_type", typeof(datum_memory_witnessed_inking)),
            Dm.named("smudge_type", typeof(obj_effect_decal_cleanable_food_squid_ink)),
            Dm.named("moodlet_type", typeof(datum_mood_event_inked)),
            Dm.named("splat_color", COLOR_NEARLY_ALL_BLACK),
            Dm.named("hit_callback", Dm.@new<datum_callback>(this, nameof(blind_em)))
        ));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/misc.dm:870
    public virtual async DmTask blind_em(mob_living victim = null, object can_splat_on = null)
    {
        if (Dm.truthy(can_splat_on))
        {
            await victim.adjust_temp_blindness_up_to(2.5f * SECONDS, 3 * SECONDS);
            await victim.adjust_confusion_up_to(2.5f * SECONDS, 3 * SECONDS);
        }
        victim.visible_message(span_warning(I18n.T("[] is inked by []!", victim, this)), span_userdanger(I18n.T("You've been inked by []!", this)));
        playsound(victim, SFX_DESECRATION, 50, TRUE);
    }
}
