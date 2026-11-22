// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/machinery/_production.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD1, D333: the fabricators): the R&D production machine every protolathe, circuit imprinter and
// techfab derives from: the designs of its linked techweb that its department and build type allow, its materials through
// /datum/remote_materials (the ore silo, or its own storage), the Fabricator window, printing with its animation, sound and timing,
// and its parts' efficiency. DV384: a design whose item is inert is not offered (datum_design.builds_inert_item). The var block is
// generated (D111).

// tg: code/modules/research/machinery/_production.dm:1
public partial class obj_machinery_rnd_production : obj_machinery_rnd
{
    // tg: code/modules/research/machinery/_production.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        print_sound = Dm.@new<datum_looping_sound_lathe_print>(this, FALSE);
        materials = Dm.@new<datum_remote_materials>(
            this,
            mapload,
            Dm.named("mat_container_signals", Dm.list(
                Dm.kv(COMSIG_MATCONTAINER_ITEM_CONSUMED, nameof(obj_machinery_rnd_production.local_material_insert))
            ))
        );

        object dot = base.Initialize(mapload, __extra);

        cached_designs = Dm.list();

        RegisterSignal(this, COMSIG_SILO_ITEM_CONSUMED, nameof(obj_machinery_rnd_production.silo_material_insert));

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_payment),
            0,
            SSeconomy?.get_dep_account(payment_department), // SSeconomy?: a test harness world that makes machines without Master has none (rules 10.6)
            PAYMENT_CLINICAL,
            TRUE
        ));

        Waiting.NoSleep(update_icon(UPDATE_OVERLAYS));
        return dot;
    }

    // tg: code/modules/research/machinery/_production.dm:55
    public override object Destroy(float? force = null)
    {
        cached_designs = null;
        QDEL_NULL(ref print_sound);
        QDEL_NULL(ref materials);
        return base.Destroy(force);
    }

    // tg: code/modules/research/machinery/_production.dm:61
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (!Dm.truthy(stripe_color))
            return dot;

        mutable_appearance stripe = mutable_appearance(Dm.resource("icons/obj/machines/research.dmi"), Dm.text("protolathe_stripe[]", Dm.truthy(panel_open) ? "_t" : "")) as mutable_appearance;
        stripe.color = stripe_color;
        Dm.append(ref dot, stripe);
        return dot;
    }

    // tg: code/modules/research/machinery/_production.dm:71
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(in_range(user, this)) && !isobserver(user))
            return dot;

        Dm.append(ref dot, span_notice(I18n.T("Material usage cost at <b>[]%</b>.", efficiency_coeff * 100)));
        Dm.append(ref dot, span_notice(I18n.T("Build time at <b>[]%</b>.", efficiency_coeff * 100)));
        if (Dm.truthy(drop_direction))
        {
            Dm.append(ref dot, span_notice(I18n.T("Currently configured to drop printed objects <b>[]</b>.", dir2text(drop_direction))));
            Dm.append(ref dot, span_notice(I18n.T("[] to reset.", EXAMINE_HINT("Alt-click"))));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("[] towards a direction (while next to it) to change drop direction.", EXAMINE_HINT("Drag"))));
        return dot;
    }

    // tg: code/modules/research/machinery/_production.dm:84
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (Dm.truthy(drop_direction))
        {
            context[SCREENTIP_CONTEXT_ALT_LMB] = "Reset Drop";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/research/machinery/_production.dm:90
    public override async DmTask connect_techweb(datum_techweb new_techweb = null)
    {
        if (stored_research != null)
            UnregisterSignal(stored_research, Dm.list(COMSIG_TECHWEB_ADD_DESIGN, COMSIG_TECHWEB_REMOVE_DESIGN));
        await base.connect_techweb(new_techweb);
    }

    // tg: code/modules/research/machinery/_production.dm:95
    public override async DmTask on_connected_techweb()
    {
        await base.on_connected_techweb();
        RegisterSignals(
            stored_research,
            Dm.list(COMSIG_TECHWEB_ADD_DESIGN, COMSIG_TECHWEB_REMOVE_DESIGN),
            nameof(obj_machinery_rnd_production.on_techweb_update)
        );
        await update_designs();
    }

    /// Updates the list of designs this fabricator can print.
    // tg: code/modules/research/machinery/_production.dm:105
    public virtual async DmTask update_designs()
    {
        techweb_updating = FALSE;

        float previous_design_count = length(cached_designs);

        cached_designs.Cut();

        foreach (object design_path in Dm.each(stored_research.researched_designs))
        {
            datum_design design = SSresearch.techweb_designs[design_path] as datum_design;

            // DEVIATION(DV384): a design whose built item is inert (a prop) is not offered; tg offers every design its techweb has
            // researched that the machine's department and build type allow.
            if ((allowed_department_flags == null || Dm.truthy(Dm.bitand(design.departmental_flags, allowed_department_flags))) && Dm.truthy(Dm.bitand(design.build_type, allowed_buildtypes)) && !design.builds_inert_item())
                Dm.combine(ref cached_designs, design);
        }

        float design_delta = length(cached_designs) - previous_design_count;

        if (design_delta > 0)
        {
            await say(I18n.T("Received [] new design[].", design_delta, design_delta == 1 ? "" : "s"));
            playsound(this, "sound/machines/beep/twobeep_high.ogg", 50, TRUE);
        }

        await update_static_data_for_all_viewers();
    }

    // tg: code/modules/research/machinery/_production.dm:127
    [SignalHandler]
    public virtual void on_techweb_update()
    {
        if (!Dm.truthy(techweb_updating)) //so we batch these updates together
        {
            techweb_updating = TRUE;
            _addtimer(Dm.@new<datum_callback>(this, nameof(update_designs)), 2 * SECONDS, file: "code/modules/research/machinery/_production.dm", line: 132);
        }
    }

    ///When materials are instered via silo link
    // tg: code/modules/research/machinery/_production.dm:135
    [SignalHandler]
    public virtual void silo_material_insert(obj_machinery_rnd machine = null, object container = null, obj_item item_inserted = null, object last_inserted_id = null, DmList mats_consumed = null, object amount_inserted = null)
    {
        Waiting.NoSleep(process_item(item_inserted, mats_consumed, amount_inserted));
    }

    /**
     * Consumes power for the item inserted either into silo or local storage.
     * Arguments
     *
     * * obj/item/item_inserted - the item to process
     * * list/mats_consumed - list of mats consumed
     * * amount_inserted - amount of material actually processed
     */
    // tg: code/modules/research/machinery/_production.dm:148
    public virtual async DmTask process_item(obj_item item_inserted = null, DmList mats_consumed = null, object amount_inserted = null)
    {
        //we use initial(active_power_usage) because higher tier parts will have higher active usage but we have no benifit from it
        if (Dm.truthy(await directly_use_energy(ROUND_UP((Dm.num(amount_inserted) / (MAX_STACK_SIZE * SHEET_MATERIAL_AMOUNT)) * 0.4f * Dm.initial(GetType(), (obj_machinery m) => m.active_power_usage)))))
        {
            datum_material highest_mat_ref = null;

            float highest_mat = 0;
            foreach (datum_material mat in Dm.each_cast<datum_material>(mats_consumed))
            {
                float present_mat = Dm.num(mats_consumed[mat]);
                if (present_mat > highest_mat)
                {
                    highest_mat = present_mat;
                    highest_mat_ref = mat;
                }
            }

            flick_animation(highest_mat_ref);
        }
    }

    /**
     * Plays an visual animation when materials are inserted
     * Arguments
     *
     * * mat - the material ref we are trying to animate on the machine
     */
    // tg: code/modules/research/machinery/_production.dm:169
    public virtual void flick_animation(datum_material mat_ref = null)
    {
        //first play the insertion animation
        flick_overlay_view(material_insertion_animation(mat_ref), 1 * SECONDS);

        //now play the progress bar animation
        flick_overlay_view(mutable_appearance(Dm.resource("icons/obj/machines/research.dmi"), "protolathe_progress"), 1 * SECONDS);
    }

    ///When materials are instered into local storage
    // tg: code/modules/research/machinery/_production.dm:180
    [SignalHandler]
    public virtual void local_material_insert(object container = null, obj_item item_inserted = null, object last_inserted_id = null, DmList mats_consumed = null, object amount_inserted = null, atom context = null)
    {
        Waiting.NoSleep(process_item(item_inserted, mats_consumed, amount_inserted));
    }

    // tg: code/modules/research/machinery/_production.dm:185
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        float total_storage = 0;
        foreach (datum_stock_part_matter_bin bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            total_storage += Dm.num(bin.tier) * 37.5f * SHEET_MATERIAL_AMOUNT;
        materials.set_local_size(total_storage);

        efficiency_coeff = Dm.num(compute_efficiency());

        await update_static_data_for_all_viewers();
        return dot;
    }

    ///Computes this machines cost efficiency based on the available parts
    // tg: code/modules/research/machinery/_production.dm:198
    public virtual object compute_efficiency()
    {
        float efficiency = 1.2f;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            efficiency -= Dm.num(servo.tier) * 0.1f;

        return efficiency;
    }

    /**
     * The cost efficiency for an particular design
     * Arguments
     *
     * * path - the design path to check for
     */
    // tg: code/modules/research/machinery/_production.dm:213
    public virtual object build_efficiency(datum_design design = null)
    {
        if (ispath(design.build_path, typeof(obj_item_stack)) || Dm.truthy(design.fixed_cost_efficiency))
            return 1;
        return efficiency_coeff;
    }

    // tg: code/modules/research/machinery/_production.dm:221
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/sheetmaterials), get_asset_datum(/datum/asset/spritesheet_batched/research_designs)):
        // the asset transport is the client's (DV44); the Fabricator window draws the materials and the designs' pictures from its own
        // copy of tg's .dmi files.
        return Dm.list();
    }

    // tg: code/modules/research/machinery/_production.dm:227
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Fabricator");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/research/machinery/_production.dm:233
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = await materials.mat_container.ui_static_data() as DmList;

        DmList designs = Dm.list();

        // the research_designs spritesheet's 32x32 size (DV44: the client draws every design from its own "design32x32" sheet, as the
        // exosuit fabricator's), so every icon is the design's asset id with no size prefix

        foreach (datum_design design in Dm.each<datum_design>(cached_designs))
        {
            DmList cost = Dm.list();

            object coefficient = build_efficiency(design);
            foreach ((object _material, object amount) in Dm.pairs(design.materials))
            {
                datum_material material = _material as datum_material;
                cost[material.name] = OPTIMAL_COST(Dm.num(amount) * Dm.num(coefficient));
            }

            designs[design.type] = Dm.list(
                Dm.kv("name", design.name),
                Dm.kv("desc", design.get_description()),
                Dm.kv("cost", cost),
                Dm.kv("path", design.type),
                Dm.kv("categories", design.category),
                Dm.kv("icon", design.asset_id)
            );
        }

        data["designs"] = designs;
        data["fabName"] = name;

        return data;
    }

    // tg: code/modules/research/machinery/_production.dm:264
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["materials"] = await materials.mat_container.ui_data();
        data["onHold"] = Dm.num(materials.on_hold());
        data["busy"] = busy;
        data["materialMaximum"] = materials.local_size;
        data["queue"] = Dm.list();

        return data;
    }

    // tg: code/modules/research/machinery/_production.dm:275
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "remove_mat":
            {
                datum_material material = Dm.locate_ref(@params["ref"]) as datum_material;
                if (material == null)
                    return null;

                object amount = text2num(@params["amount"]);
                if (!Dm.truthy(amount))
                    return null;

                //we use initial(active_power_usage) because higher tier parts will have higher active usage but we have no benifit from it
                if (!Dm.truthy(await directly_use_energy(ROUND_UP((Dm.num(amount) / MAX_STACK_SIZE) * 0.4f * Dm.initial(GetType(), (obj_machinery m) => m.active_power_usage)))))
                {
                    await say(I18n.T("No power to dispense sheets"));
                    return null;
                }

                await materials.eject_sheets(material_ref: material, eject_amount: amount, user_data: ID_DATA(usr));
                return TRUE;
            }

            case "build":
            {
                if (Dm.truthy(busy))
                {
                    await say(I18n.T("Warning: fabricator is busy!"));
                    return null;
                }

                //validate design
                Type design_path = Dm.text2path(Dm.str(@params["design_path"]));
                if (design_path == null)
                    return null;

                if (!Dm.truthy(stored_research.researched_designs[design_path]))
                    return null;

                datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
                if (design == null)
                    return FALSE;

                if (!(allowed_department_flags == null || Dm.truthy(Dm.bitand(design.departmental_flags, allowed_department_flags))))
                {
                    await say(I18n.T("This fabricator does not have the necessary keys to decrypt this design."));
                    return FALSE;
                }
                if (Dm.truthy(design.build_type) && !Dm.truthy(Dm.bitand(design.build_type, allowed_buildtypes)))
                {
                    await say(I18n.T("This fabricator does not have the necessary manipulation systems for this design."));
                    return FALSE;
                }
                // DEVIATION(DV384): the window does not offer a design whose item is inert (update_designs), nor does the act print one.
                if (design.builds_inert_item())
                    return FALSE;

                //validate print quantity
                object print_quantity = text2num(@params["amount"]);
                if (print_quantity == null)
                    return null;
                print_quantity = clamp(Dm.num(print_quantity), 1, 50);

                //efficiency for this design, stacks use exact materials
                object coefficient = build_efficiency(design);

                //check for materials
                if (!materials.can_use_resource(user_data: ID_DATA(usr)))
                    return null;
                if (!await materials.mat_container.has_materials(design.materials, Dm.num(coefficient), Dm.num(print_quantity)))
                {
                    await say(I18n.T("Not enough materials to complete prototype[].", Dm.num(print_quantity) > 1 ? "s" : ""));
                    return FALSE;
                }

                //compute power & time to print 1 item
                float charge_per_item = ROUND_UP((values_sum(design.materials) / (MAX_STACK_SIZE * SHEET_MATERIAL_AMOUNT)) * Dm.num(coefficient) * active_power_usage);
                float build_time_per_item = Dm.pow(design.construction_time * design.lathe_time_factor * efficiency_coeff, 0.8f);

                //start production
                busy = TRUE;
                SStgui.update_uis(this);
                print_sound.start();
                update_appearance();
                turf target_location = null;
                if (Dm.truthy(drop_direction))
                {
                    target_location = get_step(this, drop_direction);
                    if (isclosedturf(target_location))
                        target_location = get_turf(this);
                }
                else
                    target_location = get_turf(this);
                _addtimer(Dm.@new<datum_callback>(this, nameof(do_make_item), design, print_quantity, build_time_per_item, coefficient, charge_per_item, target_location, ID_DATA(usr)), build_time_per_item, file: "code/modules/research/machinery/_production.dm", line: 354);

                return TRUE;
            }
        }
        return dot;
    }

    /**
     * Callback for start_making, actually makes the item
     * Arguments
     *
     * * datum/design/design - the design we are trying to print
     * * items_remaining - the number of designs left out to print
     * * build_time_per_item - the time taken to print 1 item
     * * material_cost_coefficient - the cost efficiency to print 1 design
     * * charge_per_item - the amount of power to print 1 item
     * * turf/target - the location to drop the printed item on
     * * user_data - ID_DATA(user), see the proc on SSid_access, served for logging
    */
    // tg: code/modules/research/machinery/_production.dm:370
    public virtual async DmTask do_make_item(datum_design design = null, object items_remaining = null, object build_time_per_item = null, object material_cost_coefficient = null, object charge_per_item = null, turf target = null, DmList user_data = null)
    {
        if (!Dm.truthy(items_remaining)) // how
        {
            finalize_build();
            return;
        }

        if (!Dm.truthy(is_operational))
        {
            await say(I18n.T("Unable to continue production, power failure."));
            finalize_build();
            return;
        }

        if (!Dm.truthy(await directly_use_energy(charge_per_item))) // provide the wait time until lathe is ready
        {
            area my_area = get_area(this) as area;
            obj_machinery_power_apc my_apc = my_area.apc;
            if (!QDELETED(my_apc))
            {
                object charging_wait = my_apc.time_to_charge(charge_per_item);
                if (charging_wait != null)
                    await say(I18n.T("Unable to continue production, APC overload. Wait [] and try again.", DisplayTimeText(charging_wait, round_seconds_to: 1)));
                else
                    await say(I18n.T("Unable to continue production, power grid overload."));
            }
            else
                await say(I18n.T("Unable to continue production, no APC in area."));
            finalize_build();
            return;
        }

        if (!materials.can_use_resource(user_data: user_data))
        {
            finalize_build();
            return;
        }

        bool is_stack = ispath(design.build_path, typeof(obj_item_stack));

        if (!await materials.mat_container.has_materials(design.materials, Dm.num(material_cost_coefficient), is_stack ? Dm.num(items_remaining) : 1))
        {
            await say(I18n.T("Unable to continue production, missing materials."));
            finalize_build();
            return;
        }
        await materials.use_materials(design.materials, Dm.num(material_cost_coefficient), is_stack ? Dm.num(items_remaining) : 1, "processed", Dm.text("[]", design.name), user_data: user_data);

        atom_movable created = null;
        float number_to_make = 1;
        if (is_stack)
        {
            Type stack_item = Dm.initial(design, (datum_design d) => d.build_path); // var/obj/item/stack/stack_item = initial(design.build_path): a typepath (rules 3.2, V3)
            float max_stack_amount = Dm.initial(stack_item, (obj_item_stack s) => s.max_amount);
            number_to_make = Dm.num(Dm.initial(stack_item, (obj_item_stack s) => s.amount)) * Dm.num(items_remaining);
            while (number_to_make > max_stack_amount)
            {
                created = await design.create_result(target, design.materials, amount: max_stack_amount) as atom_movable;
                if (isitem(created))
                {
                    created.pixel_x = created.base_pixel_x + rand(-6, 6);
                    created.pixel_y = created.base_pixel_y + rand(-6, 6);
                }
                number_to_make -= max_stack_amount;
            }
        }

        created = await design.create_result(target, design.materials, amount: number_to_make) as atom_movable;
        if (!Dm.eq(design.inherit_materials, DESIGN_DONT_INHERIT_MATS))
            await design.transfer_materials(design.materials, material_cost_coefficient, created);

        if (isitem(created))
        {
            created.pixel_x = created.base_pixel_x + rand(-6, 6);
            created.pixel_y = created.base_pixel_y + rand(-6, 6);
        }
        SSblackbox.record_feedback("nested tally", "lathe_printed_items", 1, Dm.list(Dm.text("[]", type), Dm.text("[]", created.type)));

        if (is_stack)
            items_remaining = 0;
        else
            items_remaining = Dm.num(items_remaining) - 1;

        if (!Dm.truthy(items_remaining))
        {
            finalize_build();
            return;
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(do_make_item), design, items_remaining, build_time_per_item, material_cost_coefficient, charge_per_item, target, user_data), Dm.num(build_time_per_item), file: "code/modules/research/machinery/_production.dm", line: 446);
    }

    /// Resets the busy flag
    /// Called at the end of do_make_item's timer loop
    // tg: code/modules/research/machinery/_production.dm:450
    public virtual void finalize_build()
    {
        print_sound.stop();
        busy = FALSE;
        SStgui.update_uis(this);
        update_appearance();
    }

    // tg: code/modules/research/machinery/_production.dm:457
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        // isAdminGhostAI(user): no mob is an admin ghost AI yet (is_helpers.cs's GAP: the admins' rights are M10's)
        if (!Dm.truthy(await can_interact(user)) || !HAS_SILICON_ACCESS(user) && !Dm.truthy(await Adjacent(user)))
            return null;
        if (Dm.truthy(busy))
        {
            balloon_alert(user, I18n.T("busy printing!"));
            return null;
        }
        float direction = get_dir(this, over_location as atom);
        if (!Dm.truthy(direction))
            return null;
        drop_direction = direction;
        balloon_alert(user, I18n.T("dropping []", dir2text(drop_direction)));
        return null;
    }

    // tg: code/modules/research/machinery/_production.dm:469
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (drop_direction == 0)
            return CLICK_ACTION_BLOCKING;
        if (Dm.truthy(busy))
        {
            balloon_alert(user, I18n.T("busy printing!"));
            return CLICK_ACTION_BLOCKING;
        }
        balloon_alert(user, I18n.T("drop direction reset"));
        drop_direction = 0;
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/research/machinery/_production.dm:479
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(busy) && Dm.truthy(production_animation))
            icon_state = Dm.str(production_animation);
        else if (!Dm.truthy(panel_open)) // use what is set by parent if panel is open
            icon_state = Dm.truthy(base_icon_state) ? Dm.str(base_icon_state) : Dm.initial(GetType(), (atom a) => a.icon_state);
        return dot;
    }
}
