// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_materials.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The vars material_flags, material_modifier and material_slots are
// generated (D111); custom_materials stays here (wave 3, the atoms package).

// tg: code/game/atom/atom_materials.dm:1
public partial class atom : datum
{
    /// The custom materials this atom is made of, used by a lot of things like furniture, walls, and floors (if I finish the functionality, that is.)
    /// The list referenced by this var can be shared by multiple objects and should not be directly modified. Instead, use [set_custom_materials][/atom/proc/set_custom_materials].
    // tg: code/game/atom/atom_materials.dm:4
    public DmList custom_materials;

    /// Sets the custom materials for an atom. This is what you want to call, since most of the ones below are mainly internal.
    // tg: code/game/atom/atom_materials.dm:13
    [ShouldNotOverride]
    public virtual async DmTask set_custom_materials(DmList materials = null, float? multiplier = null)
    {
        multiplier ??= 1;
        // Easy way to know no changes are being made.
        if ((custom_materials == materials) && multiplier == 1)
            return;

        float replace_mats = length(materials);
        if (Dm.truthy(length(custom_materials)))
            await remove_material_effects(replace_mats);

        if (!Dm.truthy(replace_mats))
            return;

        await initialize_materials(materials, multiplier);
    }

    /**
     * The second part of set_custom_materials(), which handles applying the new materials
     * It is a separate proc because Initialize calls may make use of this since they should've no prior materials to remove.
     */
    // tg: code/game/atom/atom_materials.dm:32
    [ShouldNotOverride]
    public virtual async DmTask initialize_materials(DmList materials = null, float? multiplier = null)
    {
        multiplier ??= 1;
        if (multiplier != 1)
        {
            materials = materials.Copy(); //avoid editing the original list since it may be cached somewhere (likely in the materials subsystem).
            foreach (object current_material in Dm.each(materials))
                materials[current_material] = Dm.num(materials[current_material]) * Dm.num(multiplier);
        }

        //Let's be sure that there are absolutely no materials in the list that aren't positive.
        DmList nonpos_mats = null;
        foreach (object mat in Dm.each(materials.Copy()))
        {
            if (Dm.num(materials[mat]) <= 0)
            {
                LAZYADD(ref nonpos_mats, Dm.text("[] = []", mat, materials[mat]));
                materials.Remove(mat);
            }
        }
        if (Dm.truthy(length(nonpos_mats)))
        {
            stack_trace(Dm.text("materials with non-positive values found in []: []", type, english_list(nonpos_mats, and_text: ", ")), "code/game/atom/atom_materials.dm", 46);
            if (!Dm.truthy(length(materials)))
                return;
        }

        sortTim(materials, "/proc/" + nameof(GlobalProcs.cmp_numeric_dsc), associative: TRUE);
        await apply_material_effects(materials);
    }

    ///proc responsible for applying material effects when setting materials.
    // tg: code/game/atom/atom_materials.dm:54
    [ShouldCallParent]
    public virtual async DmTask apply_material_effects(DmList materials = null)
    {
        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_EFFECTS)))
        {
            DmList material_effects = await get_material_effects_list(materials) as DmList;
            await finalize_material_effects(material_effects);
        }

        custom_materials = await SSmaterials.get_material_set_cache(materials) as DmList;
    }

    /// Proc responsible for removing material effects when setting materials.
    // tg: code/game/atom/atom_materials.dm:64
    [ShouldCallParent]
    public virtual async DmTask remove_material_effects(float? replace_mats = null)
    {
        replace_mats ??= TRUE;
        //Only runs if custom materials existed at first and affected src.
        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_EFFECTS)))
        {
            DmList material_effects = await get_material_effects_list(custom_materials) as DmList;
            await finalize_remove_material_effects(material_effects);
        }

        if (!Dm.truthy(replace_mats))
            custom_materials = null;
    }

    // tg: code/game/atom/atom_materials.dm:74
    [ShouldNotOverride, DmPrivate]
    public virtual async DmTask<object> get_material_effects_list(DmList materials = null)
    {
        DmList material_effects = Dm.list();
        float index = 1;
        foreach (object current_material in Dm.each(materials))
        {
            datum_material material = await SSmaterials.get_material(current_material) as datum_material;
            material_effects[material] = Dm.list(
                Dm.kv(MATERIAL_LIST_OPTIMAL_AMOUNT, OPTIMAL_COST(Dm.num(materials[current_material]) * material_modifier)),
                Dm.kv(MATERIAL_LIST_MULTIPLIER, get_material_multiplier(material, materials, index))
            );
            index++;
        }

        if (Dm.truthy(material_slots))
            await configure_material_slots(material_effects);

        return material_effects;
    }

    /// Add MATERIAL_LIST_SLOTS entries to material effects
    // tg: code/game/atom/atom_materials.dm:93
    public virtual async DmTask configure_material_slots(DmList material_effects = null)
    {
        for (float slot_index = 1, slot_index__end = length(material_slots); slot_index <= slot_index__end; slot_index++)
        {
            object slot_type = material_slots[slot_index];
            // DEVIATION(DV25): a slot type SSmaterials did not create (generic.dm's weapon head and handle, the spear's and the mace's: not
            // translated) gives no slot effects, and its material applies as on an atom without slots (the chaplain's whip nullrod)
            if (SSmaterials.material_slots[slot_type] == null)
                continue;
            datum_material material = null;
            // The slot has a specific material assigned to it
            if (Dm.truthy(material_slots[slot_type]))
            {
                material = await SSmaterials.get_material(material_slots[slot_type]) as datum_material;
                // Slots were unset, abort
                if (!Dm.truthy(material_effects[material]))
                    continue;
            }
            else if (slot_index <= length(material_effects)) // Otherwise, go by index
                material = material_effects[slot_index] as datum_material;
            if (!Dm.truthy(material))
                continue;
            DmList effects_list = material_effects[material] as DmList;
            if (!Dm.truthy(effects_list[MATERIAL_LIST_SLOTS]))
                effects_list[MATERIAL_LIST_SLOTS] = Dm.list();
            DmList effects_slots = effects_list[MATERIAL_LIST_SLOTS] as DmList;
            effects_slots[slot_type] = TRUE;
        }
    }

    // tg: code/game/atom/atom_materials.dm:113
    public virtual async DmTask set_material_slot(object slot_type = null, object new_material = null)
    {
        if (Dm.truthy(material_slots[slot_type]))
        {
            datum_material_slot slot = SSmaterials.material_slots[slot_type] as datum_material_slot;
            datum_material material = await SSmaterials.get_material(material_slots[slot_type]) as datum_material;
            // Not present/initialized
            if (Dm.truthy(material) && Dm.truthy(custom_materials[material]))
            {
                DmList materials_slots = get_slots_of_material(material) as DmList;
                float slot_sum = 0;
                if (length(materials_slots) > 1)
                {
                    foreach (object other_slot_type in Dm.each(materials_slots))
                    {
                        datum_material_slot other_slot = SSmaterials.material_slots[other_slot_type] as datum_material_slot;
                        slot_sum += other_slot.material_amount;
                    }
                }
                await slot.on_removed(this, material, Dm.num(custom_materials[material]) * (slot_sum > 0 ? slot.material_amount / slot_sum : 1), get_material_multiplier(material, custom_materials, custom_materials.Find(material)));
            }
        }


        // Don't store materials directly, only their IDs
        if (istype(new_material, typeof(datum_material)))
        {
            datum_material as_material = new_material as datum_material;
            material_slots[slot_type] = as_material.id;
        }
        else
            material_slots[slot_type] = new_material;

        datum_material_slot slot__1 = SSmaterials.material_slots[slot_type] as datum_material_slot;
        datum_material material__1 = istype(new_material, typeof(datum_material)) ? new_material as datum_material : await SSmaterials.get_material(new_material) as datum_material;
        if (!Dm.truthy(material__1) || !Dm.truthy(custom_materials[material__1]))
            return;
        DmList materials_slots__1 = get_slots_of_material(material__1) as DmList;
        float slot_sum__1 = 0;
        if (length(materials_slots__1) > 1)
        {
            foreach (object other_slot_type in Dm.each(materials_slots__1))
            {
                datum_material_slot other_slot = SSmaterials.material_slots[other_slot_type] as datum_material_slot;
                slot_sum__1 += other_slot.material_amount;
            }
        }
        await slot__1.on_applied(this, material__1, Dm.num(custom_materials[material__1]) * (slot_sum__1 > 0 ? slot__1.material_amount / slot_sum__1 : 1), get_material_multiplier(material__1, custom_materials, custom_materials.Find(material__1)));
    }

    // tg: code/game/atom/atom_materials.dm:147
    public virtual async DmTask set_material_slots(DmList new_slots = null)
    {
        if (Dm.truthy(length(material_slots)))
        {
            foreach (object slot_type in Dm.each(material_slots))
            {
                object material_id = material_slots[slot_type];
                datum_material_slot slot = SSmaterials.material_slots[slot_type] as datum_material_slot;
                datum_material material = await SSmaterials.get_material(material_id) as datum_material;
                // Not present/initialized
                if (!Dm.truthy(material) || !Dm.truthy(custom_materials[material]))
                    continue;
                DmList materials_slots = get_slots_of_material(material) as DmList;
                float slot_sum = 0;
                if (length(materials_slots) > 1)
                {
                    foreach (object other_slot_type in Dm.each(materials_slots))
                    {
                        datum_material_slot other_slot = SSmaterials.material_slots[other_slot_type] as datum_material_slot;
                        slot_sum += other_slot.material_amount;
                    }
                }
                await slot.on_removed(this, material, Dm.num(custom_materials[material]) * (slot_sum > 0 ? slot.material_amount / slot_sum : 1), get_material_multiplier(material, custom_materials, custom_materials.Find(material)));
            }
        }

        if (!Dm.truthy(length(new_slots)))
        {
            material_slots = null;
            return;
        }

        material_slots = new_slots.Copy();
        foreach (object slot_type in Dm.each(material_slots))
        {
            object material_id = material_slots[slot_type];
            datum_material_slot slot = SSmaterials.material_slots[slot_type] as datum_material_slot;
            datum_material material = await SSmaterials.get_material(material_id) as datum_material;
            if (!Dm.truthy(material) || !Dm.truthy(custom_materials[material]))
                continue;
            DmList materials_slots = get_slots_of_material(material) as DmList;
            float slot_sum = 0;
            if (length(materials_slots) > 1)
            {
                foreach (object other_slot_type in Dm.each(materials_slots))
                {
                    datum_material_slot other_slot = SSmaterials.material_slots[other_slot_type] as datum_material_slot;
                    slot_sum += other_slot.material_amount;
                }
            }
            await slot.on_applied(this, material, Dm.num(custom_materials[material]) * (slot_sum > 0 ? slot.material_amount / slot_sum : 1), get_material_multiplier(material, custom_materials, custom_materials.Find(material)));
        }
    }

    /**
     * A proc that can be used to selectively control the stat changes and effects from a material without affecting the others.
     *
     * For example, we can have items made of two different materials, with the primary contributing a good 1.2 multiplier
     * and the second a meager 0.3.
     *
     * The GET_MATERIAL_MODIFIER macro will handles some modifications where the minimum should be 1 if above 1 and the maximum
     * be 1 if below 1. Just don't return negative values.
     */
    // tg: code/game/atom/atom_materials.dm:190
    public virtual object get_material_multiplier(datum_material custom_material = null, DmList materials = null, object index = null)
    {
        if (!Dm.truthy(length(material_slots)))
            return 1f / length(materials);
        // Slots usually account for multipliers in their own behaviors, so unless overriden it should just be 1
        foreach (object slot_type in Dm.each(material_slots))
        {
            if (Dm.eq(material_slots[slot_type], custom_material.id))
                return 1;
        }
        return 1f / length(materials);
    }

    ///Called by apply_material_effects(). It ACTUALLY handles applying effects common to all atoms (depending on material flags)
    // tg: code/game/atom/atom_materials.dm:200
    [ShouldCallParent]
    public virtual async DmTask finalize_material_effects(DmList materials = null)
    {
        float total_alpha = 0;
        DmList colors = Dm.list();
        float mat_length = length(materials);
        datum_material main_material = materials[1] as datum_material; //the material with the highest amount (after calculations)
        object main_mat_amount = ((DmList)materials[main_material])[MATERIAL_LIST_OPTIMAL_AMOUNT];
        object main_mat_mult = ((DmList)materials[main_material])[MATERIAL_LIST_MULTIPLIER];
        float do_main_material = TRUE;
        foreach (datum_material custom_material in Dm.each<datum_material>(materials))
        {
            DmList deets = materials[custom_material] as DmList;
            object mat_amount = deets[MATERIAL_LIST_OPTIMAL_AMOUNT];
            object multiplier = deets[MATERIAL_LIST_MULTIPLIER];

            float do_effects = TRUE;
            float from_slot = FALSE;
            if (material_slots != null && Dm.truthy(length(deets[MATERIAL_LIST_SLOTS])))
            {
                from_slot = TRUE;
                float slot_sum = 0;
                // A material is in multiple slots, we need to cut it up between them
                if (length(deets[MATERIAL_LIST_SLOTS]) > 1)
                {
                    foreach (object slot_type in Dm.each(deets[MATERIAL_LIST_SLOTS]))
                    {
                        datum_material_slot slot = SSmaterials.material_slots[slot_type] as datum_material_slot;
                        slot_sum += slot.material_amount;
                    }
                }

                foreach (object slot_type in Dm.each(deets[MATERIAL_LIST_SLOTS]))
                {
                    datum_material_slot slot = SSmaterials.material_slots[slot_type] as datum_material_slot;
                    float slot_amt = Dm.num(mat_amount);
                    if (slot_sum > 0)
                        slot_amt *= slot.material_amount / slot_sum;
                    do_effects = Dm.bitand(do_effects, Dm.num(await slot.on_applied(this, custom_material, slot_amt, multiplier)));
                }

                if (!Dm.truthy(do_effects) && custom_material == main_material)
                    do_main_material = FALSE;
            }

            if (Dm.truthy(do_effects))
                await apply_single_mat_effect(custom_material, mat_amount, multiplier);
            await custom_material.on_applied(this, mat_amount, multiplier, from_slot: from_slot);

            //Prevent changing things with pre-set colors, to keep colored toolboxes their looks for example
            if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_COLOR | MATERIAL_GREYSCALE)))
            {
                gather_material_color(custom_material, colors, mat_amount, multicolor: Dm.num(mat_length > 1));
                float added_alpha = Dm.num(custom_material.alpha) * (Dm.num(custom_material.alpha) / 255);
                total_alpha += GET_MATERIAL_MODIFIER(added_alpha, multiplier);
            }
        }

        if (Dm.truthy(do_main_material))
            await apply_main_material_effects(main_material, main_mat_amount, main_mat_mult);

        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_COLOR | MATERIAL_GREYSCALE)))
        {
            float previous_alpha = alpha;
            alpha *= (total_alpha / length(materials)) / 255;

            if (alpha < previous_alpha * 0.9f)
                opacity = FALSE;

            if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_GREYSCALE)))
            {
                object config_path = get_material_greyscale_config(main_material.type, greyscale_config);
                //Make sure that we've no less than the expected amount
                //expected_colors is zero for paths, the value is assigned when reading the json files.
                datum_greyscale_config config = SSgreyscale.configurations[Dm.text("[]", Dm.truthy(config_path) ? config_path : greyscale_config)] as datum_greyscale_config;
                float colors_len = length(colors);
                // DEVIATION(DV19): the server does not load the GAGS configurations (the client composes GAGS icons, D104), so
                // config is null here and tg's padding of the colour list to the configuration's expected_colors (:261-265)
                // is skipped; the client pads it when it composes the icon.
                if (config != null && config.expected_colors > colors_len)
                {
                    DmList filled_colors = colors.Copy();
                    for (float index = colors_len, index__end = config.expected_colors - 1; index <= index__end; index++)
                        Dm.append(ref filled_colors, pick(colors));
                    colors = filled_colors;
                }
                await set_greyscale(colors, config_path);
            }
            else if (Dm.truthy(length(colors)))
                await mix_material_colors(colors);
        }

        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_ADD_PREFIX)))
        {
            object prefixes = await get_material_prefixes(materials);
            name = I18n.T("[] []", prefixes, name);
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_FINALIZE_MATERIAL_EFFECTS]))
            this._SendSignal(COMSIG_ATOM_FINALIZE_MATERIAL_EFFECTS, Dm.list(this, materials, main_material));
    }

    /**
     * A proc used by both finalize_material_effects() and finalize_remove_material_effects() to get the colors
     * that will later be applied to or removed from the atom
     */
    // tg: code/game/atom/atom_materials.dm:280
    [ShouldCallParent]
    public virtual void gather_material_color(datum_material material = null, DmList colors = null, object amount = null, float? multicolor = null)
    {
        multicolor ??= FALSE;
        if (!Dm.truthy(material.color)) //the material has no color. Nevermind
            return;
        object color_to_add = material.color;
        bool istext = Dm.istext(color_to_add);
        if (istext)
        {
            if (Dm.num(material.alpha) != 255)
                color_to_add = Dm.text("[][]", color_to_add, num2hex(material.alpha, 2));
        }
        else
        {
            if (Dm.truthy(multicolor) || Dm.truthy(Dm.bitand(material_flags, MATERIAL_GREYSCALE)))
            {
                object or__1 = material.greyscale_color;
                color_to_add = Dm.truthy(or__1) ? or__1 : color_matrix2color_hex(material.color as DmList);
                if (Dm.truthy(material.greyscale_color))
                    color_to_add = Dm.text("[][]", color_to_add, num2hex(material.alpha, 2));
            }
            else
            {
                color_to_add = color_to_full_rgba_matrix(color_to_add);
                ((DmList)color_to_add)[20] = Dm.num(((DmList)color_to_add)[20]) * (Dm.num(material.alpha) / 255); // multiply the constant alpha of the color matrix
            }
        }

        Dm.append_at(colors, color_to_add, amount);
    }

    /// Manages mixing, adding or removing the material colors from the atom in absence of the MATERIAL_GREYSCALE flag.
    // tg: code/game/atom/atom_materials.dm:301
    [ShouldNotOverride]
    public virtual async DmTask mix_material_colors(DmList colors = null, float? remove = null)
    {
        remove ??= FALSE;
        float color_len = length(colors);
        if (!Dm.truthy(color_len))
            return;
        object mixcolor = colors[1];
        float amount_divisor = Dm.num(colors[mixcolor]);
        for (float i = 2, i__end = length(colors); i <= i__end; i++)
        {
            object color_to_add = colors[i];
            if (islist(color_to_add))
                color_to_add = color_matrix2color_hex(color_to_add as DmList);
            float mix_amount = Dm.num(colors[color_to_add]);
            amount_divisor += mix_amount;
            mixcolor = BlendRGB(mixcolor, color_to_add, mix_amount / amount_divisor);
        }
        if (Dm.truthy(remove))
            await remove_atom_colour(FIXED_COLOUR_PRIORITY, mixcolor);
        else
            await add_atom_colour(mixcolor, FIXED_COLOUR_PRIORITY);
    }

    ///Returns the prefixes to attach to the atom when setting materials, from a list argument.
    // tg: code/game/atom/atom_materials.dm:321
    public virtual async DmTask<object> get_material_prefixes(DmList materials = null)
    {
        DmList mat_names = Dm.list();
        foreach (datum_material material in Dm.each<datum_material>(materials))
            Dm.combine(ref mat_names, material.name);
        return mat_names.Join("-");
    }

    ///Returns a string like "plasma, paper and glass" from a list of materials
    // tg: code/game/atom/atom_materials.dm:328
    public virtual object get_material_english_list(DmList materials = null)
    {
        DmList mat_names = Dm.list();
        foreach (datum_material material in Dm.each<datum_material>(materials))
            Dm.append(ref mat_names, material.name);
        return english_list(mat_names);
    }

    ///Searches for a subtype of config_type that is to be used in its place for specific materials (like shimmering gold for cleric maces)
    // tg: code/game/atom/atom_materials.dm:335
    [ShouldNotOverride]
    public virtual object get_material_greyscale_config(object mat_type = null, object config_type = null)
    {
        if (!Dm.truthy(config_type))
            return null;
        foreach (Type path in Dm.each<Type>(subtypesof(config_type)))
        {
            if (Dm.eq(mat_type, Dm.initial(path, (datum_greyscale_config c) => c.material_skin)))
                return path;
        }
        return null;
    }

    ///Apply material effects of a single material.
    // tg: code/game/atom/atom_materials.dm:344
    [ShouldCallParent]
    public virtual async DmTask apply_single_mat_effect(datum_material material = null, object amount = null, object multiplier = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SINGLE_MATERIAL_EFFECT_APPLY]))
            this._SendSignal(COMSIG_ATOM_SINGLE_MATERIAL_EFFECT_APPLY, Dm.list(this, material, amount, multiplier));

        // Derived and not optional, so this needs to be on base and not in the property code itself
        object beauty_modifier = material.get_property(MATERIAL_BEAUTY);
        if (Dm.truthy(beauty_modifier))
        {
            await AddElement(typeof(datum_element_beauty), Dm.mul(beauty_modifier, amount));
            if (Dm.ge(beauty_modifier, 0.15f) && HAS_TRAIT(this, TRAIT_FISHING_BAIT))
                await AddElement(typeof(datum_element_shiny_bait));
        }

        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_AFFECT_STATISTICS)) && Dm.truthy(uses_integrity))
            await change_material_integrity(material, amount, multiplier);
    }

    ///A proc for material effects that only the main material (which the atom's primarly composed of) should apply.
    // tg: code/game/atom/atom_materials.dm:359
    [ShouldCallParent]
    public virtual async DmTask apply_main_material_effects(datum_material main_material = null, object amount = null, object multiplier = null)
    {
        if (Dm.truthy(main_material.texture_layer_icon_state) && Dm.truthy(Dm.bitand(material_flags, MATERIAL_COLOR)))
        {
            ADD_KEEP_TOGETHER(this, MATERIAL_SOURCE(main_material));
            add_filter(Dm.text("material_texture_[]", main_material.name), 1, layering_filter(icon: main_material.cached_texture_filter_icon, blend_mode: BLEND_INSET_OVERLAY) as DmList);
        }

        await main_material.on_main_applied(this, amount, multiplier);
    }

    ///Called by remove_material_effects(). It ACTUALLY handles removing effects common to all atoms (depending on material flags)
    // tg: code/game/atom/atom_materials.dm:369
    public virtual async DmTask finalize_remove_material_effects(DmList materials = null)
    {
        DmList colors = Dm.list();
        datum_material main_material = await get_master_material() as datum_material;
        float mat_length = length(materials);
        object main_mat_amount = ((DmList)materials[main_material])[MATERIAL_LIST_OPTIMAL_AMOUNT];
        object main_mat_mult = ((DmList)materials[main_material])[MATERIAL_LIST_MULTIPLIER];
        float do_main_material = TRUE;
        foreach (datum_material custom_material in Dm.each<datum_material>(materials))
        {
            DmList deets = materials[custom_material] as DmList;
            object mat_amount = deets[MATERIAL_LIST_OPTIMAL_AMOUNT];
            object multiplier = deets[MATERIAL_LIST_MULTIPLIER];

            float do_effects = TRUE;
            float from_slot = FALSE;
            if (material_slots != null && Dm.truthy(length(deets[MATERIAL_LIST_SLOTS])))
            {
                from_slot = TRUE;
                float slot_sum = 0;
                // A material is in multiple slots, we need to cut it up between them
                if (length(deets[MATERIAL_LIST_SLOTS]) > 1)
                {
                    foreach (object slot_type in Dm.each(deets[MATERIAL_LIST_SLOTS]))
                    {
                        datum_material_slot slot = SSmaterials.material_slots[slot_type] as datum_material_slot;
                        slot_sum += slot.material_amount;
                    }
                }

                foreach (object slot_type in Dm.each(deets[MATERIAL_LIST_SLOTS]))
                {
                    datum_material_slot slot = SSmaterials.material_slots[slot_type] as datum_material_slot;
                    float slot_amt = Dm.num(mat_amount);
                    if (slot_sum > 0)
                        slot_amt *= slot.material_amount / slot_sum;
                    do_effects = Dm.bitand(do_effects, Dm.num(await slot.on_removed(this, custom_material, mat_amount, multiplier)));
                }

                if (!Dm.truthy(do_effects) && custom_material == main_material)
                    do_main_material = FALSE;
            }

            if (Dm.truthy(do_effects))
                await remove_single_mat_effect(custom_material, mat_amount, multiplier);
            custom_material.on_removed(this, mat_amount, multiplier, from_slot: from_slot);

            if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_COLOR)))
                gather_material_color(custom_material, colors, mat_amount, multicolor: Dm.num(mat_length > 1));
        }

        if (Dm.truthy(do_main_material))
            await remove_main_material_effects(main_material, main_mat_amount, main_mat_mult);

        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_GREYSCALE | MATERIAL_COLOR)))
        {
            if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_COLOR)))
                await mix_material_colors(colors, remove: TRUE);
            else
                await set_greyscale(Dm.initial(this, (atom a) => a.greyscale_colors), Dm.initial(this, (atom a) => a.greyscale_config));
            alpha = Dm.initial(this, (atom a) => a.alpha);
            opacity = Dm.initial(this, (atom a) => a.opacity);
        }

        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_ADD_PREFIX)))
            name = Dm.initial(this, (atom a) => a.name);

        // Ensure that we restore armor zero'd out by zero multipliers, as we don't have anything to go off other than our initial values
        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_AFFECT_STATISTICS)) && Dm.truthy(uses_integrity) && Dm.truthy(Dm.initial(this, (atom a) => a.armor_type)))
        {
            datum_armor inital_armor = get_armor_by_type(Dm.initial(this, (atom a) => a.armor_type)) as datum_armor;
            foreach (object armor_id in Dm.each(datum_armor.ARMOR_LIST_ALL__local()))
            {
                object initial_rating = inital_armor.get_rating(armor_id);
                if (Dm.eq(get_armor_rating(armor_id), 0) && !Dm.eq(initial_rating, 0))
                    set_armor_rating(armor_id, initial_rating);
            }
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_FINALIZE_REMOVE_MATERIAL_EFFECTS]))
            this._SendSignal(COMSIG_ATOM_FINALIZE_REMOVE_MATERIAL_EFFECTS, Dm.list(this, materials, main_material));
    }

    ///Remove material effects of a single material.
    // tg: code/game/atom/atom_materials.dm:434
    [ShouldCallParent]
    public virtual async DmTask remove_single_mat_effect(datum_material material = null, object amount = null, object multiplier = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SINGLE_MATERIAL_EFFECT_REMOVE]))
            this._SendSignal(COMSIG_ATOM_SINGLE_MATERIAL_EFFECT_REMOVE, Dm.list(this, material, amount, multiplier));

        object beauty_modifier = material.get_property(MATERIAL_BEAUTY);
        if (Dm.truthy(beauty_modifier))
        {
            RemoveElement(typeof(datum_element_beauty), Dm.mul(beauty_modifier, amount));
            if (Dm.ge(beauty_modifier, 0.15f) && HAS_TRAIT(this, TRAIT_FISHING_BAIT))
                RemoveElement(typeof(datum_element_shiny_bait));
        }

        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_AFFECT_STATISTICS)) && Dm.truthy(uses_integrity))
            await change_material_integrity(material, amount, multiplier, removing: TRUE);
    }

    ///A proc to remove the material effects previously applied by the (ex-)main material
    // tg: code/game/atom/atom_materials.dm:448
    [ShouldCallParent]
    public virtual async DmTask remove_main_material_effects(datum_material main_material = null, object amount = null, object multipier = null)
    {
        if (Dm.truthy(main_material.texture_layer_icon_state))
        {
            remove_filter(Dm.text("material_texture_[]", main_material.name));
            REMOVE_KEEP_TOGETHER(this, MATERIAL_SOURCE(main_material));
        }
        main_material.on_main_removed(this, amount, multipier);
    }

    ///Remove the old effects, change the material_modifier variable, and then reapply all the effects.
    // tg: code/game/atom/atom_materials.dm:456
    [ShouldNotOverride]
    public virtual async DmTask change_material_modifier(object new_value = null)
    {
        await remove_material_effects();
        material_modifier = Dm.num(new_value);
        await apply_material_effects(custom_materials);
    }

    ///For enabling and disabling material effects from an item (mainly VV)
    // tg: code/game/atom/atom_materials.dm:463
    [ShouldNotOverride]
    public virtual async DmTask toggle_material_flags(object new_flags = null)
    {
        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_EFFECTS)) && !Dm.truthy(Dm.bitand(new_flags, MATERIAL_EFFECTS)))
            await remove_material_effects();
        else if (!Dm.truthy(Dm.bitand(material_flags, MATERIAL_EFFECTS)) && Dm.truthy(Dm.bitand(new_flags, MATERIAL_EFFECTS)))
            await apply_material_effects();
        material_flags = Dm.num(new_flags);
    }

    /// Applies changes to integrity and armor from a material
    // tg: code/game/atom/atom_materials.dm:472
    public virtual async DmTask change_material_integrity(datum_material material = null, object amount = null, object multiplier = null, float? removing = null)
    {
        removing ??= FALSE;
        object base_modifier = material.get_property(MATERIAL_INTEGRITY);
        float integrity_mod = GET_MATERIAL_MODIFIER(base_modifier, multiplier);
        float integrity_change = Dm.truthy(removing) ? floor(max_integrity / integrity_mod) : ceil(max_integrity * integrity_mod);
        await modify_max_integrity(integrity_change);
        DmList armor_mods = material.get_armor_modifiers(multiplier) as DmList;
        // Invert if we're removing our material
        if (Dm.truthy(removing))
        {
            foreach (object armor_type__1 in Dm.each(armor_mods.Copy()))
            {
                object value = armor_mods[armor_type__1];
                if (!Dm.eq(value, 0)) // Needs to be restored to initial values in finalize effects, sorry
                    armor_mods[armor_type__1] = 1 / Dm.num(value);
            }
        }
        set_armor((get_armor() as datum_armor).generate_new_with_multipliers(armor_mods));
    }

    /// Tries to fetch a material matching a specific slot
    // tg: code/game/atom/atom_materials.dm:486
    public virtual async DmTask<object> get_material_from_slot(object slot_type = null)
    {
        object mat_type = material_slots?[slot_type];
        if (Dm.truthy(mat_type))
            return await SSmaterials.get_material(mat_type);
        return null;
    }

    /// Fetches a copy of all material slots.
    // tg: code/game/atom/atom_materials.dm:492
    public virtual object get_material_slots()
    {
        return material_slots?.Copy();
    }

    /// Returns TRUE if this atom utilizes material slots
    // tg: code/game/atom/atom_materials.dm:496
    public virtual object has_material_slots()
    {
        return Dm.num(Dm.truthy(length(material_slots)));
    }

    /// Lists all slots in which a material is present
    // tg: code/game/atom/atom_materials.dm:500
    public virtual object get_slots_of_material(datum_material material = null)
    {
        DmList dot = Dm.list();
        foreach (object slot_type in Dm.each(material_slots))
        {
            object material_id = material_slots[slot_type];
            // DM precedence: (material_id == istype(material)) ? material.id : material (tg's own expression, rules 4.11)
            if (Dm.truthy(Dm.eq(material_id, Dm.num(istype(material, typeof(datum_material)))) ? material.id : material))
                Dm.append(ref dot, slot_type);
        }
        return dot;
    }

    /**
     * Returns the material composition of the atom.
     *
     * Used when recycling items, specifically to turn alloys back into their component mats.
     *
     * Exists because I'd need to add a way to un-alloy alloys or otherwise deal
     * with people converting the entire stations material supply into alloys.
     *
     * Arguments:
     * - flags: A set of flags determining how exactly the materials are broken down.
     */
    // tg: code/game/atom/atom_materials.dm:518
    public virtual async DmTask<object> get_material_composition(object flags = null)
    {
        DmList dot = Dm.list();

        DmList cached_materials = custom_materials;
        foreach (object mat in Dm.each(cached_materials))
        {
            datum_material material = await SSmaterials.get_material(mat) as datum_material;
            DmList material_comp = await material.return_composition(Dm.num(cached_materials[mat]), flags) as DmList;
            foreach (object comp_mat in Dm.each(material_comp))
                Dm.append_at(dot, comp_mat, material_comp[comp_mat]);
        }
        return dot;
    }

    /**
     * Fetches a list of all of the materials this object has of the desired type. Returns null if there is no valid materials of the type
     *
     * Arguments:
     * - [required_material][/datum/material]: The type of material we are checking for
     * - mat_amount: The minimum required amount of material
     */
    // tg: code/game/atom/atom_materials.dm:535
    // M5 slice 2 (Appendix A): required_material holds a type (tg's callers pass /datum/material/x), read as a Type for the istype.
    public virtual async DmTask<object> has_material_type(object required_material = null, float? mat_amount = null)
    {
        mat_amount ??= 0;
        DmList cached_materials = custom_materials;
        if (!Dm.truthy(length(cached_materials)))
            return null;

        DmList materials_of_type = null;
        foreach (object current_material in Dm.each(cached_materials))
        {
            if (Dm.num(cached_materials[current_material]) < mat_amount)
                continue;
            datum_material material = await SSmaterials.get_material(current_material) as datum_material;
            if (!istype(material, required_material as Type))
                continue;
            LAZYSET(ref materials_of_type, material, cached_materials[current_material]);
        }

        return materials_of_type;
    }

    /// Gets the most common material in the object.
    // tg: code/game/atom/atom_materials.dm:552
    public virtual async DmTask<object> get_master_material()
    {
        return Dm.truthy(length(custom_materials)) ? await SSmaterials.get_material(custom_materials[1]) : null; //materials are sorted by amount, the first is always the main one
    }

    /// Gets the total amount of materials in this atom.
    // tg: code/game/atom/atom_materials.dm:556
    public virtual object get_custom_material_amount()
    {
        return custom_materials == null ? 0 : counterlist_sum(custom_materials);
    }

    /**
     * Returns a list with the sum of the materials of the source and its contents for each mat type.
     * If contents_type is set, only consider contained objects of that type (plus src).
     * Any object that has any of the traits in skip_traits will also be skipped.
     */
    // tg: code/game/atom/atom_materials.dm:564
    public virtual object get_contents_custom_materials(object contents_type = null, object skip_traits = null)
    {
        DmList contained = (ispath(contents_type) ? get_all_contents_type(contents_type) : get_all_contents()) as DmList;
        if (Dm.truthy(skip_traits) && !islist(skip_traits))
            skip_traits = Dm.list(skip_traits);
        DmList all_mats = Dm.list();
        foreach (object atom__it in Dm.each(contained))
        {
            atom atom = atom__it as atom;
            if (!Dm.truthy(length(atom.custom_materials)))
                continue;
            if (Dm.truthy(skip_traits))
            {
                float skip = FALSE;
                foreach (object trait in Dm.each(skip_traits))
                {
                    if (HAS_TRAIT(atom, trait))
                    {
                        skip = TRUE;
                        break;
                    }
                }
                if (Dm.truthy(skip))
                    continue;
            }
            foreach (object material in Dm.each(atom.custom_materials))
                Dm.append_at(all_mats, material, atom.custom_materials[material]);
        }
        return all_mats;
    }

    /// A simple proc that iterates through each material that the object is made of and spawns some stacks based on their amount and associated sheet/ore type.
    // tg: code/game/atom/atom_materials.dm:585
    public virtual async DmTask drop_custom_materials(float? multiplier = null)
    {
        multiplier ??= 1;
        foreach (datum_material material in Dm.each<datum_material>(custom_materials))
        {
            Type or__1 = material.sheet_type;
            Type stack_type = or__1 ?? material.ore_type;
            if (stack_type == null)
                continue;
            float amount_to_spawn = FLOOR(Dm.num(custom_materials[material]) / SHEET_MATERIAL_AMOUNT * Dm.num(multiplier), 1);
            if (amount_to_spawn > 0)
                Dm.@new(stack_type, loc, amount_to_spawn);
        }
    }

    /// Compares the materials of two items to see if they're roughly the same. Primarily used in crafting and processing unit tests.
    // tg: code/game/atom/atom_materials.dm:607
    public virtual object compare_materials(atom target = null)
    {
        if (custom_materials == target.custom_materials) // SSmaterials caches the combinations so we don't have to run more complex checks
            return TRUE;
        if (length(custom_materials) != length(target.custom_materials))
            return FALSE;
        foreach (object mat in Dm.each(custom_materials))
        {
            object enemy_amount = target.custom_materials[mat];
            if (!Dm.truthy(enemy_amount)) //we couldn't find said material, early return so we won't perform a division by zero
                return FALSE;
            float ratio_difference = abs((Dm.num(custom_materials[mat]) / Dm.num(enemy_amount)) - 1);
            if (ratio_difference > COMPARISION_ACCEPTABLE_MATERIAL_DEVIATION)
                return FALSE;
        }
        return TRUE;
    }

    /**
     * A bit of leeway when comparing the amount of material of two items.
     * This was made to test the material composition of items spawned via crafting/processable component and an items of the same type spawned
     * via other means, since small portion of materials can be lost when rounding down values to the nearest integers and we can't do much about it.
     * (eg. a slab of meat worth 100 mat points is cut in three cutlets, each 33, with the remaining 1 percent lost to rounding)
     *
     * right now it's 3 points per 100 units of a material.
     *
     */
    // tg: code/game/atom/atom_materials.dm:604  #define COMPARISION_ACCEPTABLE_MATERIAL_DEVIATION 0.03 (a file-local define, rules 11.4)
    internal const float COMPARISION_ACCEPTABLE_MATERIAL_DEVIATION = 0.03f;

    /**
     * Returns a string with the materials and their respective amounts written in a way that reflects how it's displayed in the code
     * (eg. [list(/datum/material/meat = 100, /datum/material/plastic = 10)]). Also used in several unit tests.
     * Not to be confused with get_material_english_list()
     * Arguments:
     * * as_sheets: returns the text in terms of sheets, e.g "[list(/datum/material/titanium = SHEET_MATERIAL_AMOUNT * 2)]"
     */
    // tg: code/game/atom/atom_materials.dm:628
    public virtual object transcribe_materials_list(DmList mats_list = null, float? as_sheets = null)
    {
        as_sheets ??= TRUE;
        if (!Dm.truthy(mats_list))
        {
            if (!Dm.truthy(custom_materials))
                return "null";
            mats_list = custom_materials;
        }
        string text = "list(";
        float index = 1;
        float mats_len = length(mats_list);
        foreach (datum_material mat in Dm.each<datum_material>(mats_list))
        {
            string amount_string = Dm.truthy(as_sheets) ? (string)transcribe_mat_value_as_sheet(mats_list[mat]) : Dm.text("[]", mats_list[mat]);
            text += Dm.text("[] = ", mat.type) + amount_string;
            if (index < mats_len)
                text += ", ";
            index++;
        }
        text += ")";
        return text;
    }
}

public static partial class GlobalProcs
{
    // tg: code/game/atom/atom_materials.dm:645
    public static object transcribe_mat_value_as_sheet(object value = null)
    {
        float amount = Dm.num(sheets_from_value(value));
        float sw__1 = amount;
        if (Dm.in_to(sw__1, 0, 0.09f))
            return "SMALL_MATERIAL_AMOUNT * " + num2text(amount * 10);
        else if (sw__1 == 0.1f)
            return "SMALL_MATERIAL_AMOUNT";
        else if (Dm.in_to(sw__1, 0.11f, 0.49f))
            return "SMALL_MATERIAL_AMOUNT * " + num2text(amount * 10);
        else if (sw__1 == 0.5f)
            return "HALF_SHEET_MATERIAL_AMOUNT";
        else if (sw__1 == 1)
            return "SHEET_MATERIAL_AMOUNT";
        else
            return "SHEET_MATERIAL_AMOUNT * " + num2text(amount);
    }

    /// Convert a raw material amount into
    /// "SHEET_MATERIAL_AMOUNT", or "* N", with rounding rules.
    // tg: code/game/atom/atom_materials.dm:663
    public static object sheets_from_value(object value = null, float? sheet_amount = null)
    {
        sheet_amount ??= SHEET_MATERIAL_AMOUNT;
        if (!Dm.truthy(value))
            return 0;

        // If value is small, do NOT try rounding to nearest 0 or 5. percentage error becomes huge.
        float final_value = 0;

        if (Dm.num(value) < sheet_amount)
        {
            // Use exact amount for small-value materials (0.1, 0.25, 0.55, etc)
            final_value = Dm.num(value);
        }
        else
        {
            // Large values: round to nearest 0 or 5
            float nearest5_value = round(Dm.num(value) / 5) * 5;
            float max_error = Dm.num(value) * atom.COMPARISION_ACCEPTABLE_MATERIAL_DEVIATION; // 3%
            if (abs(nearest5_value - Dm.num(value)) <= max_error)
                final_value = nearest5_value;
            else
                final_value = Dm.num(value);
        }

        float final_sheet_multiplier = final_value / Dm.num(sheet_amount);
        return final_sheet_multiplier;
    }
}
