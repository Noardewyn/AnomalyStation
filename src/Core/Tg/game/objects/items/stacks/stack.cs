// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/stack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file, its tgui window included (StackCrafting, D148, protocol 16). The var
// block is generated (D111). Deferred leaves: the golem food component (golems), the crafting investigate log (M10); grind_atom came with the grinder (gaps P5).

/* Stack type objects!
 * Contains:
 * Stacks
 * Recipe datum
 * Recipe list datum
 */

/*
 * Stacks
 */

// tg: code/game/objects/items/stacks/stack.dm:12
[DmPath("/obj/item/stack")]
public partial class obj_item_stack : obj_item
{
    // tg: code/game/objects/items/stacks/stack.dm:68
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_amount = Dm.arg(__extra, 0);
        float? merge = Dm.arg<float?>(__extra, 1);
        DmList mat_override = Dm.arg<DmList>(__extra, 2);
        float? mat_amt = Dm.arg<float?>(__extra, 3);
        new_amount ??= amount;
        merge ??= TRUE;
        mat_amt ??= 1;
        object dot = null;
        amount = Dm.num_or_null(new_amount);
        if (Dm.num(amount) <= 0)
        {
            stack_trace(Dm.text("invalid amount []!", amount), "code/game/objects/items/stacks/stack.dm", 71);
            return INITIALIZE_HINT_QDEL;
        }
        while (Dm.num(amount) > max_amount)
        {
            amount = Dm.num(amount) - max_amount;
            Dm.@new(type, loc, max_amount, FALSE, mat_override, mat_amt);
        }
        if (merge_type == null)
            merge_type = type;

        dot = base.Initialize(mapload, Dm.forward(__extra, new_amount, merge, mat_override, mat_amt));

        if (Dm.truthy(merge))
            dot = INITIALIZE_HINT_LATELOAD;

        if (Dm.truthy(LAZYLEN(mat_override)))
            mats_per_unit = mat_override;
        if (Dm.truthy(LAZYLEN(mats_per_unit)))
        {
            mats_per_unit = Waiting.NoSleep(SSmaterials.get_material_set_cache(mats_per_unit, mat_amt)) as DmList;
            Waiting.NoSleep(initialize_materials(mats_per_unit, amount));
        }

        recipes = (get_main_recipes() as DmList).Copy();
        if (material_type != null)
        {
            datum_material our_mat = Waiting.NoSleep(SSmaterials.get_material(material_type)) as datum_material; //First/main material
            if (Dm.truthy(Dm.bitand(our_mat.mat_flags, MATERIAL_BASIC_RECIPES)))
                Dm.combine(ref recipes, SSmaterials.base_stack_recipes.Copy());
            if (Dm.truthy(Dm.bitand(our_mat.mat_flags, MATERIAL_CLASS_RIGID)))
                Dm.combine(ref recipes, SSmaterials.rigid_stack_recipes.Copy());
        }

        update_weight();
        update_appearance();

        if (Dm.truthy(is_path_in_list(merge_type, GLOB.golem_stack_food_directory)))
        {
            float processing_bonus = 1;
            if (Dm.truthy(ispath(merge_type, typeof(obj_item_stack_sheet))))
                processing_bonus = GOLEMFOOD_PREPARED_MEAL;
            Waiting.NoSleep(AddComponent(typeof(datum_component_golem_food), Dm.named("golem_food_key", merge_type), Dm.named("food_multiplier", processing_bonus)));
        }
        return dot;
    }

    // tg: code/game/objects/items/stacks/stack.dm:107
    public override void LateInitialize(params object[] __extra)
    {
        Waiting.NoSleep(merge_with_loc());
    }

    // tg: code/game/objects/items/stacks/stack.dm:110
    public override object Destroy(float? force = null)
    {
        mats_per_unit = null;
        if (source != null)
        {
            // source holds the energy storage's typepath until a cyborg's model swaps in its datum (robot_model.dm:102-103); a
            // typepath's linked_modules reads its initial null, so the LAZYREMOVE removes nothing there
            if (source is datum_robot_energy_storage source_storage)
                LAZYREMOVE(ref source_storage.linked_modules, this);
            source = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/stacks/stack.dm:117
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        maptext = (ismob(loc) || (loc as atom)?.atom_storage != null) ? MAPTEXT(Dm.text("<font color='white'>[]</font>", get_amount())) : "";
        return dot;
    }

    // tg: code/game/objects/items/stacks/stack.dm:121
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if ((!Dm.truthy(throwing) || throwing.target_turf == loc) && old_loc != loc && Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
            await merge_with_loc(merge_into_ourselves: Dm.num(pulledby != null));
        if (ismob(loc) || ismob(old_loc) || (loc as atom)?.atom_storage != null || old_loc?.atom_storage != null)
            update_appearance(UPDATE_NAME);
        return dot;
    }

    ///Called to lazily update the materials of the item whenever the used or if more is added
    // tg: code/game/objects/items/stacks/stack.dm:129
    public virtual async DmTask update_custom_materials()
    {
        if (Dm.truthy(length(mats_per_unit)))
            await set_custom_materials(mats_per_unit, amount);
    }

    // tg: code/game/objects/items/stacks/stack.dm:133
    public virtual object find_other_stack(DmList already_found = null, float? merge_into_ourselves = null)
    {
        merge_into_ourselves ??= FALSE;
        if (QDELETED(this) || loc == null)
            return null;
        foreach (obj_item_stack item_stack in Dm.each<obj_item_stack>(loc))
        {
            if (item_stack == this || QDELING(item_stack) || (Dm.num(item_stack.amount) >= item_stack.max_amount))
                continue;
            if (!Dm.truthy(Dm.bitand(item_stack.flags_1, INITIALIZED_1)))
                continue;
            object stack_ref = REF(item_stack);
            if (Dm.truthy(already_found[stack_ref]))
                continue;
            if (Dm.truthy(Dm.truthy(merge_into_ourselves) ? item_stack.can_merge(this) : can_merge(item_stack)))
            {
                already_found[stack_ref] = TRUE;
                return item_stack;
            }
        }
        return null;
    }

    /// Tries to merge the stack with everything on the same tile.
    // tg: code/game/objects/items/stacks/stack.dm:149
    public virtual async DmTask<object> merge_with_loc(float? merge_into_ourselves = null)
    {
        merge_into_ourselves ??= FALSE;
        DmList already_found = Dm.list(); // change to alist whenever dreamchecker and such finally supports that
        obj_item_stack other_stack = find_other_stack(already_found, merge_into_ourselves) as obj_item_stack;
        float sanity = max_amount; // just in case
        while (Dm.truthy(other_stack) && sanity > 0)
        {
            sanity--;
            if (!Dm.truthy(merge_into_ourselves))
            {
                if (Dm.truthy(await merge(other_stack)))
                    return FALSE;
            }
            else if (Dm.truthy(await other_stack.merge(this)) && !QDELETED(other_stack))
                return FALSE;
            other_stack = find_other_stack(already_found, TRUE) as obj_item_stack;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/stacks/stack.dm:163
    public override async DmTask apply_material_effects(DmList materials = null)
    {
        await base.apply_material_effects(materials);
        if (Dm.truthy(amount))
            mats_per_unit = await SSmaterials.get_material_set_cache(materials, 1 / Dm.num(amount)) as DmList;
    }

    // tg: code/game/objects/items/stacks/stack.dm:168
    public override object blend_requirements(atom_movable grinder = null, mob_living user = null)
    {
        if (!Dm.truthy(is_cyborg))
            return TRUE;
        if (Dm.truthy(user))
            to_chat(user, span_warning(I18n.T("[] is too integrated into your chassis and can't be ground up!", this)));
        return FALSE;
    }

    // tg: code/game/objects/items/stacks/stack.dm:175
    public override async DmTask<object> grind_atom(datum_reagents target_holder = null, mob user = null)
    {
        float current_amount = Dm.num(get_amount());
        if (current_amount <= 0 || QDELETED(this)) //just to get rid of this 0 amount/deleted stack we return success
            return TRUE;

        DmList grind_reagents = await grind_results() as DmList;

        if (reagents != null)
            await (reagents as datum_reagents).trans_to(target_holder, (reagents as datum_reagents).total_volume, transferred_by: user);
        float available_volume = target_holder.maximum_volume - target_holder.total_volume;

        //compute total volume of reagents that will be occupied by grind_results
        float total_volume = 0;
        foreach (object reagent in Dm.each(grind_reagents))
            total_volume += Dm.num(grind_reagents[reagent]);

        //compute number of pieces(or sheets) from available_volume
        float available_amount = (float)Math.Ceiling(current_amount * Dm.min(1, available_volume / total_volume));
        if (available_amount <= 0)
            return FALSE;

        //Now transfer the grind results scaled by available_amount
        foreach (object reagent in Dm.each(grind_reagents))
            grind_reagents[reagent] = Dm.num(grind_reagents[reagent]) * available_amount;
        await target_holder.add_reagent_list(grind_reagents);

        /**
         * use available_amount of sheets/pieces, return TRUE only if all sheets/pieces of this stack were used
         * we don't delete this stack when it reaches 0 because we expect the all in one grinder, etc to delete
         * this stack if grinding was successful
         */
        await use(available_amount, FALSE); // check = FALSE
        return available_amount == current_amount ? TRUE : FALSE;
    }

    // tg: code/game/objects/items/stacks/stack.dm:209
    [ShouldCallParent]
    public virtual object get_main_recipes()
    {
        return Dm.list(); //empty list
    }

    // tg: code/game/objects/items/stacks/stack.dm:215
    public virtual object update_weight()
    {
        return update_weight_class(get_weight_from_size(amount));
    }

    // tg: code/game/objects/items/stacks/stack.dm:218
    public virtual object get_weight_from_size(object stack_amount = null)
    {
        if (Dm.num(stack_amount) <= (max_amount * (1f / 3)))
            return clamp(full_w_class - 2, WEIGHT_CLASS_TINY, full_w_class);
        if (Dm.num(stack_amount) <= (max_amount * (2f / 3)))
            return clamp(full_w_class - 1, WEIGHT_CLASS_TINY, full_w_class);
        return full_w_class;
    }

    // tg: code/game/objects/items/stacks/stack.dm:225
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(novariants))
            return await base.update_icon_state(__extra);
        object or__1 = post_init_icon_state;
        object base_state = Dm.truthy(or__1) ? or__1 : Dm.initial(this, (atom a) => a.icon_state);
        if (Dm.num(amount) <= (max_amount * (1f / 3)))
        {
            icon_state = Dm.text("[]", base_state);
            return await base.update_icon_state(__extra);
        }
        if (Dm.num(amount) <= (max_amount * (2f / 3)))
        {
            icon_state = Dm.text("[]_2", base_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[]_3", base_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/stacks/stack.dm:237
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(is_cyborg))
            return dot;
        if (Dm.truthy(singular_name))
        {
            if (Dm.num(get_amount()) > 1)
                Dm.append(ref dot, I18n.T("There are [] []\\s in the stack.", get_amount(), singular_name));
            else
                Dm.append(ref dot, I18n.T("There is [] [] in the stack.", get_amount(), singular_name));
        }
        else if (Dm.num(get_amount()) > 1)
            Dm.append(ref dot, I18n.T("There are [] in the stack.", get_amount()));
        else
            Dm.append(ref dot, I18n.T("There is [] in the stack.", get_amount()));
        Dm.append(ref dot, span_notice(I18n.T("<b>Right-click</b> with an empty hand to take a custom amount.")));
        return dot;
    }

    // tg: code/game/objects/items/stacks/stack.dm:252
    public virtual object get_amount()
    {
        if (Dm.truthy(is_cyborg))
            return round(Dm.num((source as datum_robot_energy_storage)?.energy) / cost);
        return amount;
    }

    /// Gets the table type we make, accounting for potential exceptions.
    // tg: code/game/objects/items/stacks/stack.dm:259
    public virtual object get_table_type()
    {
        if (ispath(table_type, typeof(obj_structure_table_greyscale)) && material_type == null)
            return null; // This table type breaks without a material type.
        return table_type;
    }

    /**
     * Builds all recipes in a given recipe list and returns an association list containing them
     *
     * Arguments:
     * * recipe_to_iterate - The list of recipes we are using to build recipes
     */
    // tg: code/game/objects/items/stacks/stack.dm:270
    public virtual object recursively_build_recipes(DmList recipe_to_iterate = null)
    {
        DmList L = Dm.list();
        foreach (object recipe in Dm.each(recipe_to_iterate))
        {
            if (istype(recipe, typeof(datum_stack_recipe_list)))
            {
                datum_stack_recipe_list R = recipe as datum_stack_recipe_list;
                L[Dm.text("[]", R.title)] = recursively_build_recipes(R.recipes);
            }
            if (istype(recipe, typeof(datum_stack_recipe)))
            {
                datum_stack_recipe R = recipe as datum_stack_recipe;
                L[Dm.text("[]", R.title)] = build_recipe(R);
            }
        }
        return L;
    }

    /**
     * Returns a list of properties of a given recipe
     *
     * Arguments:
     * * R - The stack recipe we are using to get a list of properties
     */
    // tg: code/game/objects/items/stacks/stack.dm:287
    public virtual object build_recipe(datum_stack_recipe R = null)
    {
        DmList data = Dm.list();
        Type result = R.result_type;

        data["ref"] = text_ref(R);
        data["req_amount"] = R.req_amount;
        data["res_amount"] = R.res_amount;
        data["max_res_amount"] = R.max_res_amount;
        data["icon"] = Dm.initial(result, (atom a) => a.icon);
        data["icon_state"] = Dm.initial(result, (atom a) => a.icon_state);

        // DmIcon cannot paint images. So, if we have grayscale sprite, we need ready base64 image.
        if (Dm.truthy(R.result_image))
            data["image"] = R.result_image;

        return data;
    }

    /**
     * Checks if the recipe is valid to be used
     *
     * Arguments:
     * * R - The stack recipe we are checking if it is valid
     * * recipe_list - The list of recipes we are using to check the given recipe
     */
    // tg: code/game/objects/items/stacks/stack.dm:311
    public virtual object is_valid_recipe(datum_stack_recipe R = null, DmList recipe_list = null)
    {
        foreach (object S in Dm.each(recipe_list))
        {
            if (S == R)
                return TRUE;
            if (istype(S, typeof(datum_stack_recipe_list)))
            {
                datum_stack_recipe_list L = S as datum_stack_recipe_list;
                if (Dm.truthy(is_valid_recipe(R, L.recipes)))
                    return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/game/objects/items/stacks/stack.dm:321
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(use_radial))
            await show_construction_radial(user);
        else
            await ui_interact(user);
        return null;
    }

    // tg: code/game/objects/items/stacks/stack.dm:327
    public override object ui_state(mob user = null)
    {
        return GLOB.hands_state;
    }

    // tg: code/game/objects/items/stacks/stack.dm:330
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "StackCrafting", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/stacks/stack.dm:336
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["amount"] = get_amount();
        return data;
    }

    // tg: code/game/objects/items/stacks/stack.dm:341
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["recipes"] = recursively_build_recipes(recipes);
        return data;
    }

    // tg: code/game/objects/items/stacks/stack.dm:346
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "make":
            {
                datum_stack_recipe recipe = locate_ref(@params["ref"]) as datum_stack_recipe;
                float? multiplier = text2num(@params["multiplier"]);

                return await make_item(usr, recipe, multiplier);
            }
        }
        return dot;
    }

    /// The key / title for a radial option that shows the entire list of buildables (uses the old menu)
    // tg: code/game/objects/items/stacks/stack.dm:359  #define FULL_LIST "view full list" (a file-local define, rules 11.4)
    private const string FULL_LIST = "view full list";

    /// Shows a radial consisting of every radial recipe we have in our list.
    // tg: code/game/objects/items/stacks/stack.dm:362
    public virtual async DmTask show_construction_radial(mob builder = null)
    {
        DmList options = Dm.list();
        DmList titles_to_recipes = Dm.list();

        foreach (datum_stack_recipe_radial recipe in Dm.each<datum_stack_recipe_radial>(recipes))
        {
            datum_radial_menu_choice option = Dm.@new<datum_radial_menu_choice>();
            // option.image = image(icon = initial(recipe.result_type.icon), icon_state = initial(recipe.result_type.icon_state))
            // (:368-371): GAP(types): /datum/radial_menu_choice/var/image is generated as a Type (radial.cs extract_image), so the
            // choice cannot hold its picture; only runed metal (cult, M10+) has radial recipes.
            Dm.NotTranslated("code/game/objects/items/stacks/stack.dm:368", "show_construction_radial: a radial recipe's choice image (/datum/radial_menu_choice/var/image is generated as a Type, GAP(types); runed metal only, M10+)");

            if (Dm.truthy(recipe.desc))
                option.info = recipe.desc;

            options[recipe.title] = option;
            titles_to_recipes[recipe.title] = recipe;
        }

        // After everything's been added to the radial, add an option
        // that lets the user see the whole list of buildables
        options[FULL_LIST] = Dm.image(
            icon: Dm.resource("icons/hud/radial.dmi"),
            icon_state: "radial_full_list"
        );

        object selection = await show_radial_menu(
            user: builder,
            anchor: builder,
            choices: options,
            custom_check: Dm.@new<datum_callback>(this, nameof(radial_check), builder),
            radius: radial_radius,
            tooltips: TRUE
        );

        if (!Dm.truthy(selection))
            return;
        // Run normal UI interact if we wanna see the full list
        if (Dm.eq(selection, FULL_LIST))
        {
            await ui_interact(builder);
            return;
        }

        // Otherwise go straight to building
        datum_stack_recipe picked_recipe = titles_to_recipes[selection] as datum_stack_recipe;
        if (!istype(picked_recipe, typeof(datum_stack_recipe)))
            return;

        await make_item(builder, picked_recipe, 1);
    }

    /// Used as a callback for radial building.
    // tg: code/game/objects/items/stacks/stack.dm:410
    public virtual object radial_check(mob builder = null)
    {
        if (QDELETED(builder) || QDELETED(this))
            return FALSE;
        if (Dm.truthy(builder.incapacitated))
            return FALSE;
        if (!Dm.truthy(builder.is_holding(this)))
            return FALSE;
        return TRUE;
    }

    /// Makes the item with the given recipe.
    // tg: code/game/objects/items/stacks/stack.dm:422
    public virtual async DmTask<object> make_item(mob builder = null, datum_stack_recipe recipe = null, float? multiplier = null)
    {
        multiplier ??= 1;
        if (Dm.num(get_amount()) < 1 && !Dm.truthy(is_cyborg)) //sanity check as this shouldn't happen
        {
            qdel(this);
            return null;
        }
        if (!Dm.truthy(is_valid_recipe(recipe, recipes))) //href exploit protection
            return null;
        if (!Dm.truthy(multiplier) || multiplier < 1 || !IS_FINITE(multiplier)) //href exploit protection
        {
            stack_trace(Dm.text("Invalid multiplier value in stack creation [], [] is likely attempting an exploit", multiplier, usr), "code/game/objects/items/stacks/stack.dm", 429);
            return null;
        }
        if (!Dm.truthy(building_checks(builder, recipe, multiplier)))
            return null;
        if (Dm.truthy(recipe.time))
        {
            float adjusted_time = 0;
            builder.balloon_alert(builder, I18n.T("building..."));
            builder.visible_message(
                span_notice(I18n.T("[] starts building \\a [].", builder, recipe.title)),
                span_notice(I18n.T("You start building \\a []...", recipe.title))
            );
            if (HAS_TRAIT(builder, recipe.trait_booster))
                adjusted_time = (Dm.num(recipe.time) * recipe.trait_modifier);
            else
                adjusted_time = Dm.num(recipe.time);
            if (!Dm.truthy(await do_after(builder, adjusted_time, target: builder)))
            {
                builder.balloon_alert(builder, I18n.T("interrupted!"));
                return null;
            }
            if (!Dm.truthy(building_checks(builder, recipe, multiplier)))
                return null;
        }

        atom created = null;
        if (recipe.max_res_amount > 1) // Is it a stack?
        {
            created = Dm.@new(recipe.result_type, build_location(builder), recipe.res_amount * Dm.num(multiplier)) as atom;
            builder.balloon_alert(builder, I18n.T("built items"));
        }

        else if (ispath(recipe.result_type, typeof(turf)))
        {
            turf covered_turf = build_location(builder) as turf;
            if (!isturf(covered_turf))
                return null;
            created = await covered_turf.place_on_top(recipe.result_type, flags: CHANGETURF_INHERIT_AIR) as atom;
            builder.balloon_alert(builder, I18n.T("placed []", ispath(recipe.result_type, typeof(turf_open)) ? I18n.T("floor") : I18n.T("wall")));
        }

        else
        {
            created = Dm.@new(recipe.result_type, build_location(builder)) as atom;
            builder.balloon_alert(builder, I18n.T("built item"));
        }

        // split the material and use it for the craft
        obj_item_stack used_stack = await split_stack(recipe.req_amount * Dm.num(multiplier)) as obj_item_stack;
        if (ismovable(created))
            await created.setDir(builder.dir);
        await created.on_craft_completion(Dm.list(used_stack), null, builder);

        builder.investigate_log(Dm.text("crafted []", recipe.title), INVESTIGATE_CRAFTING);

        // Apply mat datums
        if (Dm.truthy(LAZYLEN(used_stack.mats_per_unit)) && !Dm.truthy(Dm.bitand(recipe.crafting_flags, CRAFT_NO_MATERIALS)))
        {
            DmList result_mats = used_stack.mats_per_unit.Copy();
            foreach (object mat in Dm.each(recipe.removed_mats))
            {
                object to_remove = recipe.removed_mats[mat];
                datum_material ref_mat = locate_type(mat as Type, result_mats) as datum_material;
                if (!Dm.truthy(ref_mat))
                    continue;
                if (Dm.num(result_mats[ref_mat]) < Dm.num(to_remove))
                    result_mats.Remove(ref_mat);
                else
                    result_mats[ref_mat] = Dm.num(result_mats[ref_mat]) - Dm.num(to_remove);
            }

            if (isstack(created))
            {
                obj_item_stack crafted_stack = created as obj_item_stack;
                if (Dm.truthy(crafted_stack.amount)) // If our stack's been emptied, it means another stack's already eaten it, and that stack'll deal with materials
                {
                    if (recipe.res_amount > 0 && recipe.req_amount != recipe.res_amount)
                    {
                        float scale = recipe.req_amount / recipe.res_amount;
                        foreach (object mat in Dm.each(result_mats))
                            result_mats[mat] = Dm.num(result_mats[mat]) * scale;
                    }
                    crafted_stack.mats_per_unit = await SSmaterials.get_material_set_cache(result_mats) as DmList;
                    await crafted_stack.update_custom_materials();
                }
            }
            else
                await created.set_custom_materials(result_mats, recipe.req_amount * Dm.num(multiplier));
        }

        qdel(used_stack); //you've outlived your purpose

        // We could be qdeleted - like if it's a stack and has already been merged
        if (QDELETED(created))
            return TRUE;

        // Add fingerprints first, otherwise created might already be deleted because of stack merging
        // tg: code/game/objects/items/stacks/stack.dm:506
        created.add_fingerprint(builder);
        if (isitem(created))
            await builder.put_in_hands(created as obj_item);

        //BubbleWrap - so newly formed boxes are empty
        if (istype(created, typeof(obj_item_storage)))
        {
            foreach (obj_item thing in Dm.each<obj_item>(created))
                qdel(thing);
        }
        //BubbleWrap END

        return TRUE;
    }

    /// <summary>
    /// DEVIATION(DV65): whether a recipe's result blocks its whole tile: its type's initial <c>density</c> (a closed turf, a
    /// girder, a table), unless it is a border object (<c>ON_BORDER_1</c>: a directional window).
    /// </summary>
    public static bool dense_result(datum_stack_recipe recipe)
    {
        if (recipe?.result_type == null)
            return false;
        if (!Dm.truthy(Dm.initial(recipe.result_type, (atom p) => p.density)))
            return false;
        return !Dm.truthy(Dm.bitand(Dm.initial(recipe.result_type, (atom p) => p.flags_1), ON_BORDER_1));
    }

    /// <summary>
    /// DEVIATION(DV65): where a recipe is built. tg builds on the builder's <c>drop_location()</c>; in first person the
    /// player builds where the crosshair aims: the turf of the last map thing it was over (<c>client.dm_aim_turf</c>), when
    /// that is the builder's turf or an open turf next to it that the builder can reach (tg's <c>Adjacent</c>: not through
    /// a window or a wall corner). Otherwise, and for a builder inside something, tg's own location.
    /// </summary>
    public atom build_location(mob builder)
    {
        atom own = builder?.drop_location() as atom;
        if (own is not turf here || builder.client?.dm_aim_turf is not turf aimed || aimed.gc_destroyed != null)
            return own;
        if (aimed == here)
            return here;
        if (aimed.z != here.z)
            return own;
        // A turf farther than one step is built on the open neighbour toward it: the crosshair reaches far, the builder's hands do not.
        if (get_dist(here, aimed) > 1 && get_step(here, get_dir(here, aimed)) is turf stepped)
            aimed = stepped;
        if (isclosedturf(aimed) || !Dm.truthy(Waiting.NoSleep(aimed.Adjacent(builder))))
            return own;
        return aimed;
    }

    /// Checks if we can build here, validly.
    // tg: code/game/objects/items/stacks/stack.dm:529
    public virtual object building_checks(mob builder = null, datum_stack_recipe recipe = null, object multiplier = null)
    {
        if (Dm.num(get_amount()) < recipe.req_amount * Dm.num(multiplier))
        {
            builder.balloon_alert(builder, I18n.T("not enough material!"));
            return FALSE;
        }
        turf dest_turf = get_turf(build_location(builder)); // DEVIATION(DV65): where the builder aims, else tg's own turf

        // DEVIATION(DV65): nothing dense is built on the tile the builder stands on (the owner, 2026-10-18): a wall, a girder,
        // a table or a door frame would shut them in. A floor and what can be walked through can; so can a border object (a
        // directional window), which closes one side of its tile only.
        if (dest_turf != null && dest_turf == get_turf(builder) && dense_result(recipe))
        {
            builder.balloon_alert(builder, I18n.T("not where you stand!"));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(recipe.crafting_flags, CRAFT_ONE_PER_TURF)) && Dm.truthy(locate_type(recipe.result_type, dest_turf)))
        {
            builder.balloon_alert(builder, I18n.T("already one here!"));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(recipe.crafting_flags, CRAFT_CHECK_DIRECTION)))
        {
            if (!Dm.truthy(valid_build_direction(dest_turf, builder.dir, is_fulltile: Dm.bitand(recipe.crafting_flags, CRAFT_IS_FULLTILE))))
            {
                builder.balloon_alert(builder, I18n.T("won't fit here!"));
                return FALSE;
            }
        }

        if (Dm.truthy(Dm.bitand(recipe.crafting_flags, CRAFT_ON_SOLID_GROUND)))
        {
            if (isclosedturf(dest_turf))
            {
                builder.balloon_alert(builder, I18n.T("cannot be made on a wall!"));
                return FALSE;
            }

            if (Dm.truthy(is_type_in_typecache(dest_turf, GLOB.turfs_without_ground)))
            {
                if (!Dm.truthy(locate_type(typeof(obj_structure_thermoplastic), dest_turf))) // for tram construction
                {
                    builder.balloon_alert(builder, I18n.T("must be made on solid ground!"));
                    return FALSE;
                }
            }
        }

        if (Dm.truthy(Dm.bitand(recipe.crafting_flags, CRAFT_CHECK_DENSITY)))
        {
            foreach (obj @object in Dm.each<obj>(dest_turf))
            {
                if (Dm.truthy(@object.density) && !Dm.truthy(Dm.bitand(@object.obj_flags, IGNORE_DENSITY)) || Dm.truthy(Dm.bitand(@object.obj_flags, BLOCKS_CONSTRUCTION)))
                {
                    builder.balloon_alert(builder, I18n.T("something is in the way!"));
                    return FALSE;
                }
            }
        }

        if (Dm.truthy(Dm.bitand(recipe.placement_checks, STACK_CHECK_CARDINALS)))
        {
            turf nearby_turf = null;
            foreach (object direction in Dm.each(GLOB.cardinals))
            {
                nearby_turf = get_step(dest_turf, Dm.num(direction));
                if (Dm.truthy(locate_type(recipe.result_type, nearby_turf)))
                {
                    to_chat(builder, span_warning(I18n.T("\\The [] must not be built directly adjacent to another!", recipe.title)));
                    builder.balloon_alert(builder, I18n.T("can't be adjacent to another!"));
                    return FALSE;
                }
            }
        }

        if (Dm.truthy(Dm.bitand(recipe.placement_checks, STACK_CHECK_ADJACENT)))
        {
            if (Dm.truthy(locate_type(recipe.result_type, Builtins.range(1, dest_turf))))
            {
                builder.balloon_alert(builder, I18n.T("can't be near another!"));
                return FALSE;
            }
        }

        if (Dm.truthy(Dm.bitand(recipe.placement_checks, STACK_CHECK_TRAM_FORBIDDEN)))
        {
            if (Dm.truthy(locate_transport<obj_structure_transport_linear_tram>(dest_turf)) || Dm.truthy(locate_type(typeof(obj_structure_thermoplastic), dest_turf)))
            {
                builder.balloon_alert(builder, I18n.T("can't be on tram!"));
                return FALSE;
            }
        }

        if (Dm.truthy(Dm.bitand(recipe.placement_checks, STACK_CHECK_TRAM_EXCLUSIVE)))
        {
            if (!Dm.truthy(locate_transport<obj_structure_transport_linear_tram>(dest_turf)))
            {
                builder.balloon_alert(builder, I18n.T("must be made on a tram!"));
                return FALSE;
            }
        }

        return TRUE;
    }

    // tg: code/game/objects/items/stacks/stack.dm:586
    [DmParams("used", "transfer", "check")]
    public override async DmTask<object> use(object used = null, params object[] __extra) // return 0 = borked; return 1 = had enough
    {
        float? transfer = Dm.arg<float?>(__extra, 0);
        float? check = Dm.arg<float?>(__extra, 1);
        transfer ??= FALSE;
        check ??= TRUE;
        if (Dm.truthy(check) && Dm.truthy(is_zero_amount(delete_if_zero: TRUE)))
            return FALSE;
        if (Dm.truthy(is_cyborg))
        {
            datum_robot_energy_storage storage = source as datum_robot_energy_storage;
            if (Dm.truthy(storage.use_charge(Dm.num(used) * cost)))
            {
                //this will include us
                foreach (obj_item_stack modules in Dm.each_cast<obj_item_stack>(storage.linked_modules))
                    modules.update_appearance(UPDATE_NAME);
                return TRUE;
            }
            return FALSE;
        }
        if (Dm.num(amount) < Dm.num(used))
            return FALSE;
        amount = Dm.num(amount) - Dm.num(used);
        if (!Dm.truthy(is_zero_amount(delete_if_zero: check)))
        {
            await update_custom_materials();
            update_appearance();
            update_weight();
        }
        return TRUE;
    }

    // tg: code/game/objects/items/stacks/stack.dm:605
    public override object tool_use_check(mob_living user = null, object amount = null, object heat_required = null)
    {
        if (Dm.num(get_amount()) < Dm.num(amount))
        {
            // general balloon alert that says they don't have enough
            user.balloon_alert(user, I18n.T("not enough material!"));
            // then a more specific message about how much they need and what they need specifically
            if (Dm.truthy(singular_name))
            {
                if (Dm.num(amount) > 1)
                    to_chat(user, span_warning(I18n.T("You need at least [] []\\s to do this!", amount, singular_name)));
                else
                    to_chat(user, span_warning(I18n.T("You need at least [] [] to do this!", amount, singular_name)));
            }
            else
                to_chat(user, span_warning(I18n.T("You need at least [] to do this!", amount)));

            return FALSE;
        }

        return TRUE;
    }

    /**
     * Returns TRUE if the item stack is the equivalent of a 0 amount item.
     *
     * Also deletes the item if delete_if_zero is TRUE and the stack does not have
     * is_cyborg set to true.
     */
    // tg: code/game/objects/items/stacks/stack.dm:628
    public virtual object is_zero_amount(float? delete_if_zero = null)
    {
        delete_if_zero ??= TRUE;
        if (Dm.truthy(is_cyborg))
            return Dm.num((source as datum_robot_energy_storage).energy) < cost ? TRUE : FALSE;
        if (Dm.num(amount) < 1)
        {
            if (Dm.truthy(delete_if_zero))
                qdel(this);
            return TRUE;
        }
        return FALSE;
    }

    /** Adds some number of units to this stack.
     *
     * Arguments:
     * - _amount: The number of units to add to this stack.
     */
    // tg: code/game/objects/items/stacks/stack.dm:642
    public virtual async DmTask add(object _amount = null)
    {
        if (Dm.truthy(is_cyborg))
            (source as datum_robot_energy_storage).add_charge(Dm.num(_amount) * cost);
        else
            amount = Dm.num(amount) + Dm.num(_amount);
        await update_custom_materials();
        update_appearance();
        update_weight();
    }

    /** Checks whether this stack can merge itself into another stack.
     *
     * Arguments:
     * - [check][/obj/item/stack]: The stack to check for mergeability.
     * - [inhand][boolean]: Whether or not the stack to check should act like it's in a mob's hand.
     */
    // tg: code/game/objects/items/stacks/stack.dm:657
    public virtual object can_merge(obj_item_stack check = null, float? inhand = null)
    {
        inhand ??= FALSE;
        // We don't only use istype here, since that will match subtypes, and stack things that shouldn't stack
        if (!istype(check, merge_type) || check.merge_type != merge_type)
            return FALSE;
        if (!Dm.equiv(mats_per_unit, check.mats_per_unit)) // ~! in case of lists this operator checks only keys, but not values
            return FALSE;
        if (Dm.truthy(is_cyborg)) // No merging cyborg stacks into other stacks
            return FALSE;
        if (ismob(loc) && !Dm.truthy(inhand) && !HAS_TRAIT(loc, TRAIT_MOB_MERGE_STACKS)) // no merging with items that are on the mob
            return FALSE;
        if (istype(loc, typeof(obj_machinery))) // no merging items in machines that aren't both in componentparts
        {
            obj_machinery machine = loc as obj_machinery;
            if (!Dm.@in(this, machine.component_parts) || !Dm.@in(check, machine.component_parts))
                return FALSE;
        }
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_STACK_CAN_MERGE]) ? (object)NONE : this._SendSignal(COMSIG_STACK_CAN_MERGE, Dm.list(this, check, inhand))), CANCEL_STACK_MERGE)))
            return FALSE;
        return TRUE;
    }

    /**
     * Merges as much of src into target_stack as possible. If present, the limit arg overrides target_stack.max_amount for transfer.
     *
     * This calls use() without check = FALSE, preventing the item from qdeling itself if it reaches 0 stack size.
     *
     * As a result, this proc can leave behind a 0 amount stack.
     */
    // tg: code/game/objects/items/stacks/stack.dm:682
    public virtual async DmTask<object> merge_without_del(obj_item_stack target_stack = null, object limit = null)
    {
        // Cover edge cases where multiple stacks are being merged together and haven't been deleted properly.
        // Also cover edge case where a stack is being merged into itself, which is supposedly possible.
        if (QDELETED(target_stack))
            throw CRASH("Stack merge attempted on qdeleted target stack.", "code/game/objects/items/stacks/stack.dm", 686);
        if (QDELETED(this))
            throw CRASH("Stack merge attempted on qdeleted source stack.", "code/game/objects/items/stacks/stack.dm", 688);
        if (target_stack == this)
            throw CRASH("Stack attempted to merge into itself.", "code/game/objects/items/stacks/stack.dm", 690);

        float transfer = Dm.num(get_amount());
        if (Dm.truthy(target_stack.is_cyborg))
        {
            datum_robot_energy_storage target_storage = target_stack.source as datum_robot_energy_storage;
            transfer = min(transfer, round((target_storage.max_energy - Dm.num(target_storage.energy)) / target_stack.cost));
        }
        else
        {
            transfer = min(transfer, (Dm.truthy(limit) ? Dm.num(limit) : target_stack.max_amount) - Dm.num(target_stack.amount));
            // Ensure that we're not bloating the target stack to the point where it falls out of storage
            if ((target_stack.loc as atom)?.atom_storage != null)
            {
                datum_storage target_storage = (target_stack.loc as atom)?.atom_storage;
                float cur_size = target_stack.w_class;
                float new_size = Dm.num(target_stack.get_weight_from_size(Dm.num(target_stack.amount) + transfer));
                float real_new_size = new_size;
                float real_cur_size = cur_size;

                // Ensure that we don't end up with two mergeable stacks if our own size gets reduced enough from the merge and we share the space
                if (!Dm.truthy(is_cyborg) && loc == target_stack.loc)
                {
                    real_cur_size += w_class;
                    if (Dm.num(amount) > transfer)
                        real_new_size += Dm.num(get_weight_from_size(Dm.num(amount) - transfer));
                }

                // If total size changed, check for overflows
                if (new_size > cur_size)
                {
                    float size_limit = max(new_size - target_storage.max_specific_storage, Dm.num(target_storage.get_total_weight()) + real_new_size - real_cur_size - target_storage.max_total_storage);
                    // If we're over the stack limit the storage container can support, reduce the transferred amount
                    // to the nearest size threshold, then by a third of the target stack per excess size
                    if (size_limit > 0)
                    {
                        float to_threshold = FLOOR(Dm.num(target_stack.amount) + transfer, floor(target_stack.max_amount / 3));
                        transfer = clamp(to_threshold - floor(target_stack.max_amount / 3) * (size_limit - 1) - Dm.num(target_stack.amount), 0, transfer);
                    }
                }
            }
        }

        if (!Dm.truthy(transfer))
            return null;

        if (Dm.truthy(pulledby) && Dm.truthy(is_zero_amount(delete_if_zero: FALSE)))
            await pulledby.start_pulling(target_stack);

        await target_stack.copy_evidences(this);
        await use(transfer, TRUE, FALSE);
        await target_stack.add(transfer);
        if (target_stack.mats_per_unit != mats_per_unit) // We get the average value of mats_per_unit between two stacks getting merged
        {
            DmList temp_mats_list = Dm.list(); // mats_per_unit is passed by ref into this coil, and that same ref is used in other places. If we didn't make a new list here we'd end up contaminating those other places, which leads to batshit behavior
            foreach (object mat_type in Dm.each(target_stack.mats_per_unit))
                temp_mats_list[mat_type] = (Dm.num(target_stack.mats_per_unit[mat_type]) * (Dm.num(target_stack.amount) - transfer) + Dm.num(mats_per_unit?[mat_type]) * transfer) / Dm.num(target_stack.amount);
            target_stack.mats_per_unit = temp_mats_list;
        }
        return transfer;
    }

    /**
     * Merges as much of src into target_stack as possible. If present, the limit arg overrides target_stack.max_amount for transfer.
     *
     * This proc deletes src if the remaining amount after the transfer is 0.
     */
    // tg: code/game/objects/items/stacks/stack.dm:741
    public virtual async DmTask<object> merge(obj_item_stack target_stack = null, object limit = null)
    {
        object dot = await merge_without_del(target_stack, limit);
        is_zero_amount(delete_if_zero: TRUE);
        return dot;
    }

    // tg: code/game/objects/items/stacks/stack.dm:745
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) before tg's first line, as atom/hitby's (atom_act.cs): an inert stack
        // subtype's own hitby is not run as this one.
        if (InertSeam.MissingOverride(this, "hitby", throwingdatum?.get_thrower() as mob))
            return null;
        if (Dm.truthy(can_merge(hitting_atom as obj_item_stack, inhand: TRUE)))
            await merge(hitting_atom as obj_item_stack);
        return await base.hitby(hitting_atom, skipcatch, hitpush, blocked, throwingdatum);
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/game/objects/items/stacks/stack.dm:751
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        // DEVIATION(DV27): the interaction guard (D132) before tg's first line: an inert stack subtype's own attack_hand (sticky
        // tape, mesh, bluespace crystal, ...) is not run as this one.
        if (InertSeam.MissingOverride(this, "attack_hand", user))
            return null;
        object dot = null;
        if (user.get_inactive_held_item() == this)
        {
            if (Dm.truthy(is_zero_amount(delete_if_zero: TRUE)))
                return dot;
            return await split_n_take(user, 1);
        }
        else
            dot = await base.attack_hand(user, modifiers, __extra);
        return dot;
    }

    // tg: code/game/objects/items/stacks/stack.dm:759
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132), as atom/attack_hand_secondary's.
        if (InertSeam.MissingOverride(this, "attack_hand_secondary", user))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;

        if (Dm.truthy(is_cyborg) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        if (Dm.truthy(is_zero_amount(delete_if_zero: TRUE)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        object max = get_amount();
        object stackmaterial = await tgui_input_number(user, I18n.T("How many sheets do you wish to take out of this stack?"), I18n.T("Stack Split"), max_value: max);
        if (!Dm.truthy(stackmaterial) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await usr.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        await split_n_take(user, stackmaterial);
        to_chat(user, span_notice(I18n.T("You take [] sheets out of the stack.", stackmaterial)));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/stacks/stack.dm:518  (gaps SK1, D333)
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        if (Dm.eq(var_name, nameof(amount)))
        {
            await add(clamp(var_value, 1 - Dm.num(amount), max_amount - Dm.num(amount))); //there must always be one.
            return TRUE;
        }
        else if (Dm.eq(var_name, nameof(max_amount)))
        {
            max_amount = Dm.num(max(var_value, 1));
            await add((max_amount < Dm.num(amount)) ? (max_amount - Dm.num(amount)) : 0); //update icon, weight, ect
            return TRUE;
        }
        return await base.vv_edit_var(var_name, var_value);
    }

    /** Splits the stack into two stacks, returns the new stack.
     *
     * Arguments:
     * - amount: The number of units to split from this stack.
     */
    // tg: code/game/objects/items/stacks/stack.dm:781
    public virtual async DmTask<object> split_stack(object amount = null)
    {
        if (!Dm.truthy(await use(amount, TRUE, FALSE)))
            return null;
        obj_item_stack new_stack = Dm.@new(type, null, amount, FALSE, mats_per_unit) as obj_item_stack;
        await new_stack.copy_evidences(this);
        if ((loc as atom)?.atom_storage != null)
            await ((atom)loc).atom_storage.refresh_views();
        is_zero_amount(delete_if_zero: TRUE);
        return new_stack;
    }

    /**
     * Splits amount items from stack, attempts to place new stack in user's hands.
     * Returns the new stack.
     * Arguments:
     * * [user][/mob] - Mob performing the split, non-nullable
     * * amount - Number of units to split from this stack
     */
    // tg: code/game/objects/items/stacks/stack.dm:797
    public virtual async DmTask<object> split_n_take(mob user = null, object amount = null)
    {
        if (!Dm.truthy(user))
            return null;
        // tg: code/game/objects/items/stacks/stack.dm:800
        add_fingerprint(user);
        obj_item_stack new_stack = await split_stack(amount) as obj_item_stack;
        if (new_stack == null)
            return null;
        // tg: code/game/objects/items/stacks/stack.dm:804
        new_stack.add_fingerprint(user);
        await user.put_in_hands(new_stack, merge_stacks: FALSE);
        return new_stack;
    }

    // tg: code/game/objects/items/stacks/stack.dm:808
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(can_merge(tool as obj_item_stack, inhand: TRUE)))
            return NONE;
        obj_item_stack overtaking_stack = tool as obj_item_stack;
        if (!Dm.truthy(await merge(overtaking_stack)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("Your [] stack now contains [] []\\s.", overtaking_stack.name, overtaking_stack.get_amount(), overtaking_stack.singular_name)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/stacks/stack.dm:817
    public virtual async DmTask copy_evidences(obj_item_stack from = null)
    {
        // tg: code/game/objects/items/stacks/stack.dm:818
        Waiting.NoSleep(add_blood_DNA(GET_ATOM_BLOOD_DNA(from)));
        add_fingerprint_list(GET_ATOM_FINGERPRINTS(from));
        add_hiddenprint_list(GET_ATOM_HIDDENPRINTS(from));
        fingerprintslast = from.fingerprintslast;
        //TODO bloody overlay
    }
}
