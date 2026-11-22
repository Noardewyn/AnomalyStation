// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/limbgrower.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package MD3, D333's station order: the medical machines): the limb grower that grows the auto-unlocking
// limb grower techweb's limbs and organs from its synthflesh and chemicals, the designs imported from the limb design disks
// (designs/limbgrower_designs.dm, var-only), the hacked designs (the synthetic arm blade) when emagged, its Limbgrower window, its
// parts and plumbing; and the fully upgraded kind with every limb design. As the fabricators (DV384), it offers no design whose
// item is still inert. The var block is generated (D111).

// tg: code/game/machinery/limbgrower.dm:3
public partial class obj_machinery_limbgrower : obj_machinery
{
    // tg: code/game/machinery/limbgrower.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        create_reagents(100, OPENCONTAINER);
        GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_limbgrower)] ??= Dm.@new<datum_techweb_autounlocking_limbgrower>();
        stored_research = GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_limbgrower)] as datum_techweb_autounlocking;
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_demand)));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        register_context();
        return dot;
    }

    // tg: code/game/machinery/limbgrower.dm:48
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (held_item == null)
            return NONE;

        object sw__1 = held_item.tool_behaviour;
        if (Dm.eq(sw__1, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] panel", Dm.truthy(panel_open) ? "Close" : "Open");
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        else if (Dm.eq(sw__1, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[]chor", Dm.truthy(anchored) ? "Unan" : "An");
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        if (istype(held_item, typeof(obj_item_disk_design_disk_limbs)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Load limb designs";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    /// Emagging a limbgrower allows you to build synthetic armblades.
    // tg: code/game/machinery/limbgrower.dm:66
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        await update_static_data(user);
        balloon_alert(user, I18n.T("illegal limb production enabled"));
        return TRUE;
    }

    // tg: code/game/machinery/limbgrower.dm:75
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Limbgrower");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/limbgrower.dm:82
    public override object ui_state(mob user = null) => GLOB.physical_state;

    // tg: code/game/machinery/limbgrower.dm:85
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        datum_reagents holder = reagents as datum_reagents;

        foreach (datum_reagent reagent_id in Dm.each<datum_reagent>(holder.reagent_list))
        {
            DmList reagent_data = Dm.list(
                Dm.kv("reagent_name", reagent_id.name),
                Dm.kv("reagent_amount", reagent_id.volume),
                Dm.kv("reagent_type", reagent_id.GetType()));
            Dm.append_at(data, "reagents", Dm.list(reagent_data));
        }

        data["total_reagents"] = holder.total_volume;
        data["max_reagents"] = holder.maximum_volume;
        data["busy"] = busy;

        return data;
    }

    // tg: code/game/machinery/limbgrower.dm:101
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["categories"] = Dm.list();

        DmList species_categories = categories.Copy();
        foreach (object species in Dm.each(species_categories.Copy()))
            species_categories[species] = Dm.list();

        DmList available_nodes = stored_research.researched_designs.Copy();
        if (Dm.truthy(LAZYLEN(imported_designs)))
            Dm.combine(ref available_nodes, imported_designs);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            Dm.combine(ref available_nodes, stored_research.hacked_designs);

        foreach (object design_path in Dm.each(available_nodes))
        {
            if (SSresearch.techweb_designs[design_path] is not datum_design limb_design)
                continue;
            if (limb_design.builds_inert_item()) // DV384: an inert item is not offered
                continue;
            foreach (object found_category in Dm.each(species_categories))
            {
                if (Dm.@in(found_category, limb_design.category))
                    Dm.append_at(species_categories, found_category, limb_design);
            }
        }

        foreach ((object category, object category_designs) in Dm.pairs(species_categories))
        {
            DmList category_data = Dm.list(
                Dm.kv("name", category),
                Dm.kv("designs", Dm.list()));
            foreach (datum_design found_design in Dm.each<datum_design>(category_designs as DmList))
            {
                DmList all_reagents = Dm.list();
                foreach ((object _reagent_path, object reagent_amount) in Dm.pairs(found_design.reagents_list))
                {
                    Dm.append(ref all_reagents, Dm.list(Dm.list(
                        Dm.kv("name", Dm.initial(_reagent_path as Type, (datum_reagent r) => r.name)),
                        Dm.kv("amount", Dm.num(reagent_amount) * production_coefficient))));
                }

                Dm.append_at(category_data, "designs", Dm.list(Dm.list(
                    Dm.kv("parent_category", category),
                    Dm.kv("name", found_design.name),
                    Dm.kv("path", found_design.GetType()),
                    Dm.kv("needed_reagents", all_reagents))));
            }

            Dm.append_at(data, "categories", Dm.list(category_data));
        }

        return data;
    }

    // tg: code/game/machinery/limbgrower.dm:145
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        foreach (obj_item_reagent_containers_cup our_beaker in Dm.each<obj_item_reagent_containers_cup>(component_parts))
            await (reagents as datum_reagents).trans_to(our_beaker, Dm.num((our_beaker.reagents as datum_reagents).maximum_volume));
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/game/machinery/limbgrower.dm:150
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (check_busy(user))
            return ITEM_INTERACT_BLOCKING;

        if (istype(tool, typeof(obj_item_disk_design_disk_limbs)))
        {
            user.visible_message(span_notice(I18n.T("[] begins to load \\the [] in \\the []...", user, tool, this)),
                span_notice(I18n.T("You begin to load designs from \\the []...", tool)),
                span_hear(I18n.T("You hear the clatter of a floppy drive.")));
            busy = TRUE;
            var limb_design_disk = (obj_item_disk_design_disk_limbs)tool;
            if (Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            {
                foreach (object found_design in Dm.each(limb_design_disk.blueprints))
                    LAZYSET(ref imported_designs, found_design, TRUE);
                await update_static_data(user);
            }
            busy = FALSE;
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/machinery/limbgrower.dm:170
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        if (check_busy(user))
            return ITEM_INTERACT_BLOCKING;

        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/limbgrower.dm:176
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (check_busy(user))
            return ITEM_INTERACT_BLOCKING;

        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/limbgrower.dm:182
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (check_busy(user))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(await default_unfasten_wrench(user, tool)))
            return ITEM_INTERACT_SUCCESS;
        return dot;
    }

    // tg: code/game/machinery/limbgrower.dm:190
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(busy))
            icon_state = Dm.text("[]_idleon", base_icon_state);
        else if (Dm.truthy(panel_open))
            icon_state = Dm.text("[]_panelopen", base_icon_state);
        else
            icon_state = Dm.text("[]_idleoff", base_icon_state);
        return dot;
    }

    // tg: code/game/machinery/limbgrower.dm:199
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (check_busy(usr))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "empty_reagent"))
        {
            await (reagents as datum_reagents).del_reagent(Dm.text2path(Dm.str(@params["reagent_type"])));
            return TRUE;
        }
        if (Dm.eq(sw__1, "make_limb"))
        {
            Type design_path = Dm.text2path(Dm.str(@params["design_path"]));
            object temp_category = @params["active_tab"];
            if (design_path == null)
                return dot;
            if (!Dm.truthy(stored_research.researched_designs[design_path]) && !Dm.truthy(stored_research.hacked_designs[design_path]) && !Dm.truthy(imported_designs?[design_path]))
                return dot;
            if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && Dm.truthy(stored_research.hacked_designs[design_path]))
                return dot;
            if (!Dm.@in(temp_category, categories))
                return dot;
            if (SSresearch.techweb_designs[design_path] is not datum_design design || design.builds_inert_item()) // DV384
                return dot;
            being_built = design;
            // All the reagents we're using to make our organ.
            DmList consumed_reagents_list = LAZYCOPY(being_built.reagents_list) as DmList;
            /// The amount of power we're going to use, based on how much reagent we use.
            float power = 0;

            foreach ((object required_reagent, object required_amount) in Dm.pairs(consumed_reagents_list))
            {
                consumed_reagents_list[required_reagent] = Dm.num(consumed_reagents_list[required_reagent]) * production_coefficient;
                if (!Dm.truthy((reagents as datum_reagents).has_reagent(required_reagent, Dm.num(required_amount))))
                {
                    audible_message(span_notice(I18n.T("[] buzzes.", this)));
                    playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
                    return dot;
                }

                power = max(active_power_usage, (power + Dm.num(required_amount)));
            }

            busy = TRUE;
            await use_energy(power);
            update_appearance();
            flick("limbgrower_fill", this);
            selected_category = Dm.str(temp_category);
            _addtimer(Dm.@new<datum_callback>(this, nameof(build_item), consumed_reagents_list), production_speed * production_coefficient, file: "code/game/machinery/limbgrower.dm", line: 246);
            return TRUE;
        }
        return dot;
    }

    /*
     * The process of beginning to build a limb or organ.
     * Goes through and sanity checks that we actually have enough reagent to build our item.
     * Then, remove those reagents from our reagents datum.
     *
     * After the reagents are handled, we can proceede with making the limb or organ. (Limbs are handled in a separate proc)
     *
     * modified_consumed_reagents_list - the list of reagents we will consume on build, modified by the production coefficient.
     */
    // tg: code/game/machinery/limbgrower.dm:259
    public virtual async DmTask build_item(DmList modified_consumed_reagents_list = null)
    {
        datum_reagents holder = reagents as datum_reagents;
        foreach ((object required_reagent, object required_amount) in Dm.pairs(modified_consumed_reagents_list))
        {
            if (!Dm.truthy(holder.has_reagent(required_reagent, Dm.num(required_amount))))
            {
                audible_message(span_notice(I18n.T("The [] buzzes.", this)));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
                break;
            }

            await holder.remove_reagent(required_reagent, Dm.num(required_amount));
        }

        Type built_typepath = being_built.build_path;
        if (ispath(built_typepath, typeof(obj_item_bodypart)))
            await build_limb(create_buildpath());
        else
        {
            //Just build whatever it is
            Dm.@new(built_typepath, loc);
        }

        busy = FALSE;
        update_appearance();
        flick("limbgrower_unfill", this);
    }

    /*
     * The process of putting together a limb.
     * This is called from after we remove the reagents, so this proc is just initializing the limb type.
     *
     * This proc handles skin / mutant color, greyscaling, names and descriptions, and various other limb creation steps.
     *
     * buildpath - the path of the bodypart we're building.
     */
    // tg: code/game/machinery/limbgrower.dm:290
    public virtual async DmTask build_limb(Type buildpath = null)
    {
        /// The limb we're making with our buildpath, so we can edit it.
        //i need to create a body part manually using a set icon (otherwise it doesn't appear)
        var limb = (obj_item_bodypart)Dm.@new(buildpath, loc);
        limb.name = I18n.T("\\improper synthetic [] []", selected_category, limb.plaintext_zone);
        limb.limb_id = selected_category;
        limb.species_color = "#62A262";
        await limb.update_icon_dropped();
    }

    ///Returns a valid limb typepath based on the selected option
    // tg: code/game/machinery/limbgrower.dm:300
    public virtual Type create_buildpath()
    {
        if (selected_category == SPECIES_HUMAN) // Humans use the parent type
            return being_built.build_path;
        else
            return Dm.text2path(Dm.text("[]/[]", being_built.build_path, selected_category));
    }

    // tg: code/game/machinery/limbgrower.dm:306
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        datum_reagents holder = reagents as datum_reagents;
        holder.maximum_volume = 0;
        foreach (obj_item_reagent_containers_cup our_beaker in Dm.each<obj_item_reagent_containers_cup>(component_parts))
        {
            holder.maximum_volume = Dm.num(holder.maximum_volume) + Dm.num(our_beaker.volume);
            await (our_beaker.reagents as datum_reagents).trans_to(this, Dm.num((our_beaker.reagents as datum_reagents).total_volume));
        }
        production_coefficient = 1.25f;
        foreach (datum_stock_part_servo our_servo in Dm.each<datum_stock_part_servo>(component_parts))
            production_coefficient -= Dm.num(our_servo.tier) * 0.25f;
        production_coefficient = clamp(production_coefficient, 0, 1); // coefficient goes from 1 -> 0.75 -> 0.5 -> 0.25
        return dot;
    }

    // tg: code/game/machinery/limbgrower.dm:317
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Storing up to <b>[]u</b> of reagents.<br>Reagent consumption rate at <b>[]%</b>.", (reagents as datum_reagents).maximum_volume, production_coefficient * 100)));
        return dot;
    }

    /**
     * Check if the limb grower is currently busy.
     *
     * user - user initiating the check.
     *
     * returns the value of src.busy.
     */
    // tg: code/game/machinery/limbgrower.dm:329
    public virtual bool check_busy(mob user = null)
    {
        bool dot = Dm.truthy(busy);
        if (dot)
            to_chat(user, span_warning(I18n.T("The limb grower is busy. Please wait for completion of previous operation.")));
        return dot;
    }

    /*
     * Checks our reagent list to see if a design can be built.
     *
     * limb_design - the design we're checking for buildability.
     *
     * returns TRUE if we have enough reagent to build it. Returns FALSE if we do not.
     */
    // tg: code/game/machinery/limbgrower.dm:341
    public virtual bool can_build(datum_design limb_design = null)
    {
        foreach ((object required_reagent, object required_amount) in Dm.pairs(limb_design.reagents_list))
        {
            if (!Dm.truthy((reagents as datum_reagents).has_reagent(required_reagent, Dm.num(required_amount) * production_coefficient)))
                return false;
        }
        return true;
    }
}

// tg: code/game/machinery/limbgrower.dm:347
public partial class obj_machinery_limbgrower_fullupgrade : obj_machinery_limbgrower
{
    // tg: code/game/machinery/limbgrower.dm:351
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach ((object design_path, object _design) in Dm.pairs(SSresearch.techweb_designs))
        {
            var found_design = (datum_design)_design;
            if (Dm.truthy(Dm.bitand(found_design.build_type, LIMBGROWER)) && !Dm.@in(RND_CATEGORY_HACKED, found_design.category))
                LAZYSET(ref imported_designs, design_path, TRUE);
        }
        return dot;
    }
}
