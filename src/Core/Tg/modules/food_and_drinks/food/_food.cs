// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/_food.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps S1 (D291): the whole file. The kitchen's links (the grill and the oven, the microwave) came with gaps H2 (D333); the venue
// price came with the restaurant; the decomposition of food left on the floor (decomposition.dm) stays a Dm.Deferred leaf; its germs
// came with the diseases (gaps VI1). The var block is generated.

///Abstract class to allow us to easily create all the generic "normal" food without too much copy pasta of adding more components
// tg: code/modules/food_and_drinks/food/_food.dm:2
[DmPath("/obj/item/food")]
public partial class obj_item_food : obj_item
{
    // tg: code/modules/food_and_drinks/food/_food.dm:68
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(food_reagents))
            food_reagents = string_assoc_list(food_reagents) as DmList;

        ///This has to be done before set_custom_materials is called at atom level
        if (Dm.truthy(custom_materials))
        {
            object main_mat_type = null;
            float mat_amount = 0;
            foreach (object mat_type in Dm.each(custom_materials))
            {
                if (Dm.num(custom_materials[mat_type]) > mat_amount)
                    main_mat_type = mat_type;
            }
            LAZYADD(ref intrinsic_food_materials, main_mat_type);
        }
        if (Dm.truthy(intrinsic_food_materials))
            intrinsic_food_materials = typecacheof(intrinsic_food_materials) as DmList;

        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(tastes))
            tastes = string_assoc_list(tastes) as DmList;
        if (Dm.truthy(eatverbs))
            eatverbs = string_list(eatverbs) as DmList;
        if (Dm.truthy(venue_value))
            Waiting.NoSleep(AddElement(typeof(datum_element_venue_price), venue_value));

        Waiting.NoSleep(make_edible());
        Waiting.NoSleep(make_processable());
        Waiting.NoSleep(make_leave_trash());
        Waiting.NoSleep(make_germ_sensitive(mapload));

        Waiting.NoSleep(make_grillable());
        Waiting.NoSleep(make_bakeable());
        Waiting.NoSleep(make_microwaveable());
        Waiting.NoSleep(make_dryable());

        if (HAS_TRAIT(this, TRAIT_GRILLABLE))
            RegisterSignal(this, COMSIG_ITEM_GRILLED, nameof(food_grilled));
        if (HAS_TRAIT(this, TRAIT_BAKEABLE))
            RegisterSignal(this, COMSIG_ITEM_BAKED, nameof(food_baked));
        if (HAS_TRAIT(this, TRAIT_MICROWAVABLE))
            RegisterSignal(this, COMSIG_ITEM_MICROWAVE_COOKED, nameof(food_microwaved));
        if (HAS_TRAIT(this, TRAIT_DRYABLE))
            RegisterSignal(this, COMSIG_ITEM_DRIED, nameof(food_dried));

        RegisterSignal(this, SIGNAL_USED_IN_FOOD_PROCESSOR, nameof(used_in_food_processor));

        ADD_TRAIT(this, TRAIT_FISHING_BAIT, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/_food.dm:115
    public override async DmTask apply_material_effects(DmList materials = null)
    {
        if (!HAS_TRAIT(this, TRAIT_INGREDIENTS_HOLDER)) //ingredients holder handle prefixes and colors differently
        {
            object main_material = materials[1]; //The list is sorted by amount so the first of the list is the main mat
            if (!Dm.truthy(is_type_in_typecache(main_material, intrinsic_food_materials)))
                material_flags = Dm.bitor(material_flags, Dm.bitor(Dm.bitor(MATERIAL_EFFECTS, MATERIAL_AFFECT_STATISTICS), Dm.bitor(MATERIAL_ADD_PREFIX, MATERIAL_COLOR)));
        }
        else
        {
            //food items with the ingredients holders component are still affected by the materials stats and effects wise.
            material_flags = Dm.bitor(material_flags, Dm.bitor(MATERIAL_EFFECTS, MATERIAL_AFFECT_STATISTICS));
        }
        await base.apply_material_effects(materials);
    }

    // tg: code/modules/food_and_drinks/food/_food.dm:125
    public override async DmTask remove_material_effects(float? replace_mats = null)
    {
        replace_mats ??= TRUE;
        await base.remove_material_effects(replace_mats);
        material_flags = Dm.bitand(material_flags, Dm.bitnot(Dm.bitor(Dm.bitor(MATERIAL_EFFECTS, MATERIAL_AFFECT_STATISTICS), Dm.bitor(MATERIAL_ADD_PREFIX, MATERIAL_COLOR))));
    }

    ///This proc adds the edible component, overwrite this if you for some reason want to change some specific args like callbacks.
    // tg: code/modules/food_and_drinks/food/_food.dm:130
    public virtual async DmTask make_edible()
    {
        await AddComponentFrom(
            SOURCE_EDIBLE_INNATE,
            typeof(datum_component_edible),
            Dm.named("initial_reagents", food_reagents),
            Dm.named("food_flags", food_flags),
            Dm.named("foodtypes", foodtypes),
            Dm.named("volume", max_volume),
            Dm.named("eat_time", eat_time),
            Dm.named("tastes", tastes),
            Dm.named("eatverbs", eatverbs),
            Dm.named("bite_consumption", bite_consumption),
            Dm.named("junkiness", junkiness),
            Dm.named("reagent_purity", starting_reagent_purity),
            Dm.named("handmade_complexity", crafting_complexity)
        );
    }

    // tg: code/modules/food_and_drinks/food/_food.dm:147
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        foreach (obj_item item in Dm.each_cast<obj_item>(components)) // parent proc assumes machinery or structures in components are used, so we should be fine to assume only items from here
        {
            if (!(item is obj_item_food food_component))
                continue;
            LAZYADD(ref intrinsic_food_materials, food_component.intrinsic_food_materials);
        }
        mob_living user = crafter as mob_living;
        if (user is mob_living && user.mind != null)
            ADD_TRAIT(this, TRAIT_HANDMADE, REF(user.mind));

        if (!(current_recipe is datum_crafting_recipe_food food_recipe))
            return;

        float made_with_food = FALSE;
        float final_foodtypes = food_recipe.added_foodtypes;
        foreach (obj_item ingredient in Dm.each_cast<obj_item>(components))
        {
            datum_component_edible edible = ingredient.GetComponent(typeof(datum_component_edible)) as datum_component_edible;
            if (edible == null)
                continue;
            made_with_food = TRUE;
            object ingredient_foodtypes = edible.foodtypes_by_source?[SOURCE_EDIBLE_INNATE];
            final_foodtypes = Dm.bitor(final_foodtypes, Dm.num(ingredient_foodtypes));
        }
        if (!Dm.truthy(made_with_food))
            return;
        final_foodtypes = Dm.bitand(final_foodtypes, Dm.bitnot(food_recipe.removed_foodtypes));
        ///Update the foodtypes
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("foodtypes", final_foodtypes));
    }

    // tg: code/modules/food_and_drinks/food/_food.dm:178
    public override async DmTask OnCreatedFromProcessing(mob_living user = null, obj_item work_tool = null, DmList chosen_option = null, atom original_atom = null)
    {
        await base.OnCreatedFromProcessing(user, work_tool, chosen_option, original_atom);
        if (!(original_atom is obj_item_food original_food))
            return;
        if (Dm.truthy(original_food.intrinsic_food_materials))
            LAZYADD(ref intrinsic_food_materials, original_food.intrinsic_food_materials);

        ///Update the foodtypes
        datum_component_edible edible = original_food.GetComponent(typeof(datum_component_edible)) as datum_component_edible;
        object og_food_types = edible.foodtypes_by_source[SOURCE_EDIBLE_INNATE];
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("foodtypes", og_food_types));
    }

    ///This proc handles processable elements, overwrite this if you want to add behavior such as slicing, forking, spooning, whatever, to turn the item into something else
    // tg: code/modules/food_and_drinks/food/_food.dm:192
    public virtual async DmTask make_processable()
    {
        return;
    }

    ///This proc handles grillable components, overwrite if you want different grill results etc.
    // tg: code/modules/food_and_drinks/food/_food.dm:196
    public virtual async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_badrecipe), rand(20 * SECONDS, 30 * SECONDS), FALSE);
    }

    ///This proc handles bakeable components, overwrite if you want different bake results etc.
    // tg: code/modules/food_and_drinks/food/_food.dm:201
    public virtual async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_badrecipe), rand(25 * SECONDS, 40 * SECONDS), FALSE);
    }

    /// This proc handles the microwavable element. Overwrite if you want special microwave results.
    /// By default, all food is microwavable. However, they will be microwaved into a bad recipe (burnt mess).
    // tg: code/modules/food_and_drinks/food/_food.dm:207
    public virtual async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), typeof(obj_item_food_badrecipe), Dm.named("bad_recipe", TRUE));
    }

    /// This proc handles the dryable element. Overwrite if you want special drying rack results.
    // tg: code/modules/food_and_drinks/food/_food.dm:211
    public virtual async DmTask make_dryable()
    {
        return;
    }

    ///This proc handles trash components, overwrite this if you want the object to spawn trash
    // tg: code/modules/food_and_drinks/food/_food.dm:215
    public virtual async DmTask make_leave_trash()
    {
        if (trash_type != null)
            await AddElement(typeof(datum_element_food_trash), trash_type);
    }

    ///This proc makes things infective and decomposing when they stay on the floor for too long.
    ///Set preserved_food to TRUE to make it never decompose.
    ///Set decomp_req_handle to TRUE to only make it decompose when someone picks it up.
    ///Requires /datum/component/germ_sensitive to detect exposure
    // tg: code/modules/food_and_drinks/food/_food.dm:224
    public virtual async DmTask make_germ_sensitive(object mapload = null)
    {
        if (trash_type != null)
            return; // You don't eat the package and it protects from decomposing
        await AddComponent(typeof(datum_component_germ_sensitive), mapload); // gaps VI1
        if (!Dm.truthy(preserved_food))
            // AddComponent(/datum/component/decomposition, mapload, decomp_req_handle, ...): the mold, ants and stink lines of food left
            // on the floor (datums/components/food/decomposition.dm) are not ported; the germs are (gaps VI1)
            Dm.Deferred("code/modules/food_and_drinks/food/_food.dm:228", "make_germ_sensitive: the decomposition of food left on the floor (datums/components/food/decomposition.dm: mold, ants, stink particles)");
    }

    // tg: code/modules/food_and_drinks/food/_food.dm:231
    [SignalHandler]
    public virtual void food_baked(datum source = null, obj_item_food baked_result = null)
    {
        Waiting.NoSleep(pass_down_foodtypes(baked_result));
    }

    // tg: code/modules/food_and_drinks/food/_food.dm:235
    [SignalHandler]
    public virtual void food_microwaved(datum source = null, obj_item_food result = null)
    {
        Waiting.NoSleep(pass_down_foodtypes(result));
    }

    // tg: code/modules/food_and_drinks/food/_food.dm:239
    [SignalHandler]
    public virtual void food_grilled(datum source = null, obj_item_food grill_result = null)
    {
        Waiting.NoSleep(pass_down_foodtypes(grill_result));
    }

    // tg: code/modules/food_and_drinks/food/_food.dm:243
    [SignalHandler]
    public virtual void food_dried(datum source = null, obj_item_food result = null)
    {
        if (this != result)
            Waiting.NoSleep(pass_down_foodtypes(result));
    }

    // tg: code/modules/food_and_drinks/food/_food.dm:248
    [SignalHandler]
    public virtual void used_in_food_processor(datum source = null, obj_item_food result = null, datum_food_processor_process recipe = null)
    {
        Waiting.NoSleep(pass_down_foodtypes(result, recipe.added_foodtypes, recipe.removed_foodtypes));
    }

    /**
     * Proc called when the food is cooked (and the result is obviously still food)
     *
     * This proc handles passing down the foodtype flags of the source to the result, usually because it has been cooked.
     * This way we can preserve the food types of the ingredients this dish is made of. Crafting and processing are handled separately.
     */
    // tg: code/modules/food_and_drinks/food/_food.dm:263
    public virtual async DmTask pass_down_foodtypes(obj_item_food result = null, object added_foodtypes = null, object removed_foodtypes = null)
    {
        added_foodtypes ??= foodtypes_added_when_cooked;
        removed_foodtypes ??= foodtypes_removed_when_cooked;
        if (!(result is obj_item_food))
            return;

        datum_component_edible edible = GetComponent(typeof(datum_component_edible)) as datum_component_edible;
        float inherited_foodtypes = Dm.num(edible.foodtypes_by_source?[SOURCE_EDIBLE_INNATE]);

        inherited_foodtypes = Dm.bitand(inherited_foodtypes, Dm.bitnot(Dm.num(removed_foodtypes)));
        inherited_foodtypes = Dm.bitor(inherited_foodtypes, Dm.num(added_foodtypes));

        await result.AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("foodtypes", inherited_foodtypes));
    }
}

// tg: code/modules/food_and_drinks/food/misc.dm:129
public partial class obj_item_food_hugemushroomslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/misc.dm:141
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_WALKING_MUSHROOM, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        return dot;
    }
}
