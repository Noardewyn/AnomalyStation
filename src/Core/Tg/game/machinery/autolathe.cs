// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/autolathe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD1, D333: the fabricators): the autolathe, with the designs of the autolathe's auto-unlocking techweb
// (its hacked ones while it is hacked, and those a design disk imports), its own material container, the Autolathe window, printing
// with its animation, sound and timing, its parts, its wires (datums/wires/autolathe.dm) and its shock. DV384: a design whose item is
// inert is not offered (datum_design.builds_inert_item). The var block is generated (D111).

// tg: code/game/machinery/autolathe.dm:1
public partial class obj_machinery_autolathe : obj_machinery
{
    // tg: code/game/machinery/autolathe.dm:36
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        print_sound = Dm.@new<datum_looping_sound_lathe_print>(this, FALSE);
        materials = Dm.@new<datum_material_container>(
            this,
            SSmaterials.flat_materials,
            0,
            Dm.bitor(MATCONTAINER_EXAMINE, MATCONTAINER_ACCEPT_ALLOYS),
            Dm.named("container_signals", Dm.list(Dm.kv(COMSIG_MATCONTAINER_ITEM_CONSUMED, nameof(obj_machinery_autolathe.AfterMaterialInsert))))
        );
        object dot = base.Initialize(mapload, __extra);

        set_wires(Dm.@new<datum_wires_autolathe>(this));
        GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_autolathe)] ??= Dm.@new<datum_techweb_autounlocking_autolathe>();
        stored_research = GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_autolathe)] as datum_techweb_autounlocking;

        register_context();
        return dot;
    }

    // tg: code/game/machinery/autolathe.dm:53
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref print_sound);
        QDEL_NULL(ref materials);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/autolathe.dm:58
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(in_range(user, this)) && !isobserver(user))
            return dot;

        Dm.append(ref dot, span_notice(I18n.T("Material usage cost at <b>[]%</b>.", creation_efficiency * 100)));
        if (Dm.truthy(drop_direction))
        {
            Dm.append(ref dot, span_notice(I18n.T("Currently configured to drop printed objects <b>[]</b>.", dir2text(drop_direction))));
            Dm.append(ref dot, span_notice(I18n.T("[] to reset.", EXAMINE_HINT("Alt-click"))));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("[] towards a direction (while next to it) to change drop direction.", EXAMINE_HINT("Drag"))));

        Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT("screwed"), Dm.truthy(panel_open) ? "closed" : "open")));
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("The machine can be [] apart.", EXAMINE_HINT("pried"))));
        return dot;
    }

    // tg: code/game/machinery/autolathe.dm:74
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (Dm.truthy(drop_direction))
        {
            context[SCREENTIP_CONTEXT_ALT_LMB] = "Reset Drop";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item == null)
            return NONE;

        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] Panel", Dm.truthy(panel_open) ? "Close" : "Open");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.truthy(panel_open) && Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return null;
    }

    // tg: code/game/machinery/autolathe.dm:90
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/autolathe.dm:93
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/autolathe.dm:96
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(busy) ? Dm.text("[]_n", base_icon_state) : Dm.truthy(panel_open) ? Dm.text("[]_t", base_icon_state) : Dm.str(base_icon_state);
        return dot;
    }

    // tg: code/game/machinery/autolathe.dm:100
    [SignalHandler]
    public virtual void AfterMaterialInsert(object container = null, obj_item item_inserted = null, object last_inserted_id = null, object mats_consumed = null, object amount_inserted = null, atom context = null)
    {
        //we use initial(active_power_usage) because higher tier parts will have higher active usage but we have no benefit from it
        if (Dm.truthy(Waiting.NoSleep(directly_use_energy(ROUND_UP((Dm.num(amount_inserted) / (MAX_STACK_SIZE * SHEET_MATERIAL_AMOUNT)) * 0.4f * Dm.initial(GetType(), (obj_machinery m) => m.active_power_usage))))))
        {
            flick_overlay_view(mutable_appearance(Dm.resource("icons/obj/machines/lathes.dmi"), "autolathe_mat"), 1 * SECONDS);

            datum_material highest_mat_ref = null;
            float highest_mat = 0;
            foreach (datum_material mat in Dm.each_cast<datum_material>(mats_consumed))
            {
                float present_mat = Dm.num(((DmList)mats_consumed)[mat]);
                if (present_mat > highest_mat)
                {
                    highest_mat = present_mat;
                    highest_mat_ref = mat;
                }
            }

            flick_overlay_view(material_insertion_animation(highest_mat_ref), 1 * SECONDS);
        }
    }

    // tg: code/game/machinery/autolathe.dm:117
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!Dm.truthy(is_operational))
            return null;

        if (Dm.truthy(shocked) && !Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            await shock__proc(user as mob_living, 50);
            return null;
        }

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;

        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Autolathe");
            await ui.open();
        }
        return null;
    }

    /**
     * Converts all the designs supported by this autolathe into UI data
     * Arguments
     *
     * * list/designs - the list of techweb designs we are trying to send to the UI
     */
    // tg: code/game/machinery/autolathe.dm:137
    public virtual async DmTask<object> handle_designs(DmList designs = null)
    {
        DmList output = Dm.list();

        // the research_designs spritesheet's 32x32 size (DV44: the client draws every design from its own "design32x32" sheet, as the
        // exosuit fabricator's), so every icon is the design's asset id with no size prefix

        foreach (object design_path in Dm.each(designs))
        {
            datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
            if (design.make_reagent != null)
                continue;
            // DEVIATION(DV384): a design whose built item is inert (a prop) is not offered; tg offers every design of the techweb.
            if (design.builds_inert_item())
                continue;

            //compute cost & maximum number of printable items
            float coeff = ispath(design.build_path, typeof(obj_item_stack)) ? 1 : creation_efficiency;
            DmList cost = Dm.list();
            float custom_materials = FALSE;
            // for(var/datum/material/mat as anything in design.materials) with istype(mat): the keys are materials or requirement types (rules 6.5)
            foreach (object mat in Dm.each(design.materials))
            {
                object mat_cost = design.materials[mat];
                float design_cost = OPTIMAL_COST(Dm.num(mat_cost) * coeff);
                if (mat is datum_material material)
                {
                    cost[material.name] = design_cost;
                    custom_materials = FALSE;
                    continue;
                }

                datum_material_requirement requirement = null;
                if (ispath(mat, typeof(datum_material_slot)))
                {
                    datum_material_slot slot = SSmaterials.material_slots[mat] as datum_material_slot;
                    requirement = SSmaterials.requirements[slot.requirement_type] as datum_material_requirement;
                }
                else
                    requirement = SSmaterials.requirements[mat] as datum_material_requirement;

                if (requirement == null)
                {
                    stack_trace(Dm.text("Design [] has an invalid material requirement: []", design, mat), "code/game/machinery/autolathe.dm", 170);
                    continue;
                }

                cost[requirement.get_description()] = design_cost;
                custom_materials = TRUE;
            }

            //create & send ui data
            DmList design_data = Dm.list(
                Dm.kv("name", design.name),
                Dm.kv("desc", design.get_description()),
                Dm.kv("cost", cost),
                Dm.kv("path", design_path),
                Dm.kv("categories", design.category),
                Dm.kv("icon", design.asset_id),
                Dm.kv("customMaterials", custom_materials)
            );

            Dm.append(ref output, Dm.list(design_data));
        }

        await DmTask.CompletedTask;
        return output;
    }

    // tg: code/game/machinery/autolathe.dm:192
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = await materials.ui_static_data() as DmList;

        data["designs"] = await handle_designs(stored_research.researched_designs);
        if (Dm.truthy(LAZYLEN(imported_designs)))
            Dm.append_at(data, "designs", await handle_designs(imported_designs));
        if (Dm.truthy(hacked))
            Dm.append_at(data, "designs", await handle_designs(stored_research.hacked_designs));

        return data;
    }

    // tg: code/game/machinery/autolathe.dm:203
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/sheetmaterials), get_asset_datum(/datum/asset/spritesheet_batched/research_designs)):
        // the asset transport is the client's (DV44); the Autolathe window draws the designs' pictures from its own copy of tg's .dmi files.
        return Dm.list();
    }

    // tg: code/game/machinery/autolathe.dm:209
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["materials"] = await materials.ui_data();
        data["materialtotal"] = await materials.total_amount();
        data["materialsmax"] = materials.max_amount;
        data["active"] = busy;

        return data;
    }

    // tg: code/game/machinery/autolathe.dm:219
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "eject"))
        {
            datum_material material = Dm.locate_ref(@params["ref"]) as datum_material;
            if (material == null)
                return dot;

            object amount = text2num(@params["amount"]);
            if (amount == null)
                return dot;

            //we use initial(active_power_usage) because higher tier parts will have higher active usage but we have no benefit from it
            if (!Dm.truthy(await directly_use_energy(ROUND_UP((Dm.num(amount) / MAX_STACK_SIZE) * 0.4f * Dm.initial(GetType(), (obj_machinery m) => m.active_power_usage)))))
            {
                await say(I18n.T("No power to dispense sheets"));
                return dot;
            }

            await materials.retrieve_stack(amount, material);
            return TRUE;
        }

        //sanity checks to start printing
        if (!Dm.eq(action, "make"))
        {
            stack_trace(Dm.text("unknown autolathe ui_act: []", action), "code/game/machinery/autolathe.dm", 243);
            return dot;
        }

        if (Dm.truthy(disabled))
        {
            await say(I18n.T("Unable to print, voltage mismatch in internal wiring."));
            return dot;
        }

        if (Dm.truthy(busy))
        {
            await say(I18n.T("Currently printing."));
            return dot;
        }

        //validate design
        Type design_path = Dm.text2path(Dm.str(@params["design_path"]));
        if (design_path == null)
            return dot;
        object valid_design = stored_research.researched_designs[design_path];
        if (!Dm.truthy(valid_design))
            valid_design = stored_research.hacked_designs[design_path];
        if (!Dm.truthy(valid_design))
            valid_design = imported_designs?[design_path];
        if (!Dm.truthy(valid_design))
            return dot;
        datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
        if (design == null)
        {
            stack_trace(Dm.text("Autolathe ui_act() got passed an invalid design: \\[[]\\] and somehow made it past all checks", design_path), "code/game/machinery/autolathe.dm", 265);
            return dot;
        }
        if (!Dm.truthy(Dm.bitand(design.build_type, AUTOLATHE)))
        {
            await say(I18n.T("This fabricator does not have the necessary keys to decrypt this design."));
            return dot;
        }
        // DEVIATION(DV384): the window does not offer a design whose item is inert (handle_designs), nor does the act print one.
        if (design.builds_inert_item())
            return dot;

        //validate print quantity
        object build_count = text2num(@params["multiplier"]);
        if (build_count == null)
            return dot;
        build_count = clamp(Dm.num(build_count), 1, 50);

        // Check for materials required. For custom material items decode their required materials
        DmList materials_needed = Dm.list();
        DmList slots_chosen = null;
        float mat_choice = FALSE;
        foreach ((object material__it, object amount_needed) in Dm.pairs(design.materials))
        {
            object material = material__it; // the body reassigns the loop var (rules 6.5)
            if (!ispath(material, typeof(datum_material_requirement)) && !ispath(material, typeof(datum_material_slot))) // Material requirement
            {
                if (!istype(material, typeof(datum_material)))
                    throw CRASH(Dm.text("Autolathe ui_act got passed an invalid material id: []", material), "code/game/machinery/autolathe.dm", 284);
                Dm.append_at(materials_needed, material, amount_needed);
                continue;
            }

            datum_material_slot slot = null;
            if (ispath(material, typeof(datum_material_slot)))
            {
                slot = SSmaterials.material_slots[material] as datum_material_slot;
                material = slot.requirement_type;
            }

            DmList choices = Dm.list();
            foreach (datum_material valid_candidate in Dm.each_cast<datum_material>(SSmaterials.get_materials_by_req(material)))
            {
                if (await materials.get_material_amount(valid_candidate) >= (Dm.num(amount_needed) + Dm.num(materials_needed[valid_candidate])))
                    choices[valid_candidate.name] = valid_candidate;
            }

            if (!Dm.truthy(length(choices)))
            {
                await say(I18n.T("No valid materials with applicable amounts detected for design."));
                return dot;
            }

            object chosen = await tgui_input_list(
                ui.user,
                I18n.T("Select the material to use[]", slot != null ? I18n.T(" for []", LOWER_TEXT(slot.name)) : ""),
                I18n.T("Material Selection"),
                sort_list(choices) as DmList
            );
            if (chosen == null || QDELING(this))
                return dot; // user cancelled

            material = choices[chosen];
            if (slot != null)
            {
                datum_material proper_mat = material as datum_material;
                LAZYSET(ref slots_chosen, slot.type, proper_mat.id);
            }

            if (material == null)
                throw CRASH(Dm.text("A player chose an invalid custom material in autolathe ui_act: []", material), "code/game/machinery/autolathe.dm", 317);
            Dm.append_at(materials_needed, material, amount_needed);
        }

        //checks for available materials
        float material_cost_coefficient = (ispath(design.build_path, typeof(obj_item_stack)) || Dm.truthy(design.fixed_cost_efficiency)) ? 1 : creation_efficiency;
        if (!await materials.has_materials(materials_needed, material_cost_coefficient, Dm.num(build_count)))
        {
            await say(I18n.T("Not enough materials to begin production."));
            return dot;
        }

        //compute power & time to print 1 item
        float charge_per_item = 0;
        foreach ((object material, object amount) in Dm.pairs(design.materials))
            charge_per_item += Dm.num(amount);

        charge_per_item = ROUND_UP((charge_per_item / (MAX_STACK_SIZE * SHEET_MATERIAL_AMOUNT)) * material_cost_coefficient * active_power_usage);
        float build_time_per_item = Dm.pow(design.construction_time * design.lathe_time_factor, 0.8f);

        //do the printing sequentially
        busy = TRUE;
        update_appearance();
        SStgui.update_uis(this);
        // play this after all checks passed individually for each item.
        print_sound.start();
        turf target_location = null;
        if (Dm.truthy(drop_direction))
        {
            target_location = get_step(this, drop_direction);
            if (isclosedturf(target_location))
                target_location = get_turf(this);
        }
        else
            target_location = get_turf(this);

        //give achievement for using unique material
        if (Dm.truthy(mat_choice))
        {
            foreach (datum_material material in Dm.each<datum_material>(materials_needed))
            {
                if (!istype(material, typeof(datum_material_glass)) && !istype(material, typeof(datum_material_iron)))
                {
                    Dm.Deferred("code/game/machinery/autolathe.dm:352", "ui_act: ui.user.client.give_award(/datum/award/achievement/misc/getting_an_upgrade, ui.user): the achievements (D332)");
                    break;
                }
            }
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(do_make_item), design, build_count, build_time_per_item, material_cost_coefficient, charge_per_item, materials_needed, target_location, slots_chosen), build_time_per_item, file: "code/game/machinery/autolathe.dm", line: 354);

        return TRUE;
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
     * * list/materials_needed - the list of materials to print 1 item
     * * turf/target - the location to drop the printed item on
    */
    // tg: code/game/machinery/autolathe.dm:370
    public virtual async DmTask do_make_item(datum_design design = null, object items_remaining = null, object build_time_per_item = null, object material_cost_coefficient = null, object charge_per_item = null, DmList materials_needed = null, turf target = null, DmList slots_chosen = null)
    {
        if (Dm.num(items_remaining) <= 0) // how
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

        bool is_stack = ispath(design.build_path, typeof(obj_item_stack));
        if (!await materials.has_materials(materials_needed, Dm.num(material_cost_coefficient), is_stack ? Dm.num(items_remaining) : 1))
        {
            await say(I18n.T("Unable to continue production, missing materials."));
            finalize_build();
            return;
        }
        await materials.use_materials(materials_needed, Dm.num(material_cost_coefficient), is_stack ? Dm.num(items_remaining) : 1);

        atom_movable created = null;
        float number_to_make = 1;
        if (is_stack)
        {
            Type stack_item = Dm.initial(design, (datum_design d) => d.build_path); // var/obj/item/stack/stack_item = initial(design.build_path): a typepath (rules 3.2, V3)
            float max_stack_amount = Dm.initial(stack_item, (obj_item_stack s) => s.max_amount);
            number_to_make = Dm.num(Dm.initial(stack_item, (obj_item_stack s) => s.amount)) * Dm.num(items_remaining);
            while (number_to_make > max_stack_amount)
            {
                created = await design.create_result(target, materials_needed, amount: max_stack_amount) as atom_movable;
                if (isitem(created))
                {
                    created.pixel_x = created.base_pixel_x + rand(-6, 6);
                    created.pixel_y = created.base_pixel_y + rand(-6, 6);
                }
                number_to_make -= max_stack_amount;
            }
        }
        created = await design.create_result(target, materials_needed, amount: number_to_make) as atom_movable;
        if (Dm.truthy(length(slots_chosen)))
            await created.set_material_slots(slots_chosen);
        if (!Dm.eq(design.inherit_materials, DESIGN_DONT_INHERIT_MATS))
            await design.transfer_materials(materials_needed, material_cost_coefficient, created);

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

        if (Dm.num(items_remaining) <= 0)
        {
            finalize_build();
            return;
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(do_make_item), design, items_remaining, build_time_per_item, material_cost_coefficient, charge_per_item, materials_needed, target, slots_chosen), Dm.num(build_time_per_item), file: "code/game/machinery/autolathe.dm", line: 434);
    }

    /**
     * Resets the icon state and busy flag
     * Called at the end of do_make_item's timer loop
    */
    // tg: code/game/machinery/autolathe.dm:440
    public virtual void finalize_build()
    {
        print_sound.stop();
        busy = FALSE;
        update_appearance();
        SStgui.update_uis(this);
    }

    // tg: code/game/machinery/autolathe.dm:447
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        // isAdminGhostAI(user): no mob is an admin ghost AI yet (is_helpers.cs's GAP: the admins' rights are M10's)
        if (!Dm.truthy(await can_interact(user)) || !HAS_SILICON_ACCESS(user) && !Dm.truthy(await Adjacent(user)))
            return null;
        if (Dm.truthy(busy))
        {
            balloon_alert(user, I18n.T("printing started!"));
            return null;
        }
        float direction = get_dir(this, over_location as atom);
        if (!Dm.truthy(direction))
            return null;
        drop_direction = direction;
        balloon_alert(user, I18n.T("dropping []", dir2text(drop_direction)));
        return null;
    }

    // tg: code/game/machinery/autolathe.dm:459
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(drop_direction))
            return CLICK_ACTION_BLOCKING;
        if (Dm.truthy(busy))
        {
            balloon_alert(user, I18n.T("busy printing!"));
            return CLICK_ACTION_SUCCESS;
        }
        balloon_alert(user, I18n.T("drop direction reset"));
        drop_direction = 0;
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/machinery/autolathe.dm:469
    public override async DmTask<object> base_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode))
            return await base.base_item_interaction(user, tool, modifiers);

        if (Dm.truthy(busy))
        {
            balloon_alert(user, I18n.T("it's busy!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(panel_open) && Dm.truthy(is_wire_tool(tool)))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(machine_stat))
            return await base.base_item_interaction(user, tool, modifiers);

        if (!istype(tool, typeof(obj_item_disk_design_disk)))
            return await base.base_item_interaction(user, tool, modifiers);

        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("close the panel first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] begins to load \\the [] in \\the []...", user, tool, this)), blind_message: span_hear(I18n.T("You hear the chatter of a floppy drive.")));
        balloon_alert(user, I18n.T("uploading design..."));
        busy = TRUE;

        if (!Dm.truthy(await do_after(user, 1.5f * SECONDS, target: this)))
        {
            busy = FALSE;
            await update_static_data_for_all_viewers();
            balloon_alert(user, I18n.T("interrupted!"));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_disk_design_disk disky = (obj_item_disk_design_disk)tool;
        DmList not_imported = null;
        foreach (object design_path in Dm.each(disky.blueprints))
        {
            datum_design disk_design = SSresearch.techweb_designs[design_path] as datum_design;
            if (Dm.truthy(Dm.bitand(disk_design.build_type, AUTOLATHE)))
                LAZYSET(ref imported_designs, design_path, TRUE);
            else
                LAZYADD(ref not_imported, disk_design.name);
        }

        if (not_imported != null)
            to_chat(user, span_warning(I18n.T("The following design[] couldn't be imported: []", LAZYLEN(not_imported) > 1 ? "s" : "", english_list(not_imported))));

        busy = FALSE;
        await update_static_data_for_all_viewers();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/autolathe.dm:517
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float mat_capacity = 0;
        foreach (datum_stock_part_matter_bin new_matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            mat_capacity += Dm.num(new_matter_bin.tier) * (37.5f * SHEET_MATERIAL_AMOUNT);
        materials.max_amount = mat_capacity;

        float efficiency = 1.8f;
        foreach (datum_stock_part_servo new_servo in Dm.each<datum_stock_part_servo>(component_parts))
            efficiency -= Dm.num(new_servo.tier) * 0.2f;
        creation_efficiency = max(1, round(efficiency, 0.1f)); // creation_efficiency goes 1.6 -> 1.4 -> 1.2 -> 1 per level of servo efficiency
        return dot;
    }

    /**
     * Cut a wire in the autolathe
     * Arguments
     *
     * * wire - the wire we are trying to cut
     */
    // tg: code/game/machinery/autolathe.dm:535
    public virtual async DmTask reset(object wire = null)
    {
        object sw__1 = wire;
        if (Dm.eq(sw__1, WIRE_HACK))
        {
            if (!Dm.truthy(wires.is_cut(wire)))
                await adjust_hacked(FALSE);
        }
        else if (Dm.eq(sw__1, WIRE_SHOCK))
        {
            if (!Dm.truthy(wires.is_cut(wire)))
                shocked = FALSE;
        }
        else if (Dm.eq(sw__1, WIRE_DISABLE))
        {
            if (!Dm.truthy(wires.is_cut(wire)))
                disabled = FALSE;
        }
    }

    // tg: code/game/machinery/autolathe.dm:547
    public override async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER)))) // unpowered, no shock
            return FALSE;
        if (siemens_coeff == null)
            siemens_coeff = 0.7f;
        return await base.shock__proc(shocking, chance, shock_source, siemens_coeff);
    }

    /**
     * Is the autolathe hacked. Allowing us to acess hidden designs
     * Arguments
     *
     * state - TRUE/FALSE for is the autolathe hacked
     */
    // tg: code/game/machinery/autolathe.dm:560
    public virtual async DmTask adjust_hacked(object state = null)
    {
        hacked = Dm.num(state);
        await update_static_data_for_all_viewers();
    }
}

// tg: code/game/machinery/autolathe.dm:564
public partial class obj_machinery_autolathe_hacked : obj_machinery_autolathe
{
    // tg: code/game/machinery/autolathe.dm:564
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(adjust_hacked(TRUE));
        return dot;
    }
}
