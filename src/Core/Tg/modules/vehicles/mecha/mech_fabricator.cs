// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mech_fabricator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): the exosuit fabricator, whole, with its ExosuitFabricator window. It links to the station's science techweb
// (an R&D server's at round start since gaps RD2, else tg's fallback web) and its materials to the ore silo. DV384: designs that would build an
// inert item, and the mechs' parts (the mechs are not this campaign), are not offered. The var block is generated (D111).

// tg: code/modules/vehicles/mecha/mech_fabricator.dm:1
public partial class obj_machinery_mecha_part_fabricator : obj_machinery
{
    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:60
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        print_sound = Dm.@new<datum_looping_sound_lathe_print>(this, FALSE);
        rmat = Dm.@new<datum_remote_materials>(this, Dm.truthy(mapload) && Dm.truthy(link_on_init) ? TRUE : FALSE);
        cached_designs = Dm.list();
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:66
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref rmat);
        QDEL_NULL(ref print_sound);
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:71
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link))) && stored_research == null) /* CONFIG_GET(flag/no_default_techweb_link) */
            CONNECT_TO_RND_SERVER_ROUNDSTART(ref stored_research, this);
        if (stored_research != null)
            await on_connected_techweb();
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:78
    public virtual async DmTask connect_techweb(datum_techweb new_techweb = null)
    {
        if (stored_research != null)
            UnregisterSignal(stored_research, Dm.list(COMSIG_TECHWEB_ADD_DESIGN, COMSIG_TECHWEB_REMOVE_DESIGN));
        stored_research = new_techweb;
        if (stored_research != null)
            await on_connected_techweb();
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:85
    public virtual async DmTask on_connected_techweb()
    {
        RegisterSignals(
            stored_research,
            Dm.list(COMSIG_TECHWEB_ADD_DESIGN, COMSIG_TECHWEB_REMOVE_DESIGN),
            nameof(on_techweb_update)
        );
        await update_menu_tech();
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:93
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object buffer = (tool as obj_item_multitool)?.buffer;
        if (!QDELETED(buffer) && buffer is datum_techweb techweb)
            await connect_techweb(techweb);
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:98
    public virtual void on_techweb_update(params object[] __extra)
    {
        // We're probably going to get more than one update (design) at a time, so batch
        // them together.
        _addtimer(Dm.@new<datum_callback>(this, nameof(update_menu_tech)), 2 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/vehicles/mecha/mech_fabricator.dm", line: 103);
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:105
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float T = 0;

        //maximum stocking amount (default 300000, 600000 at T4)
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            T += Dm.num(matter_bin.tier);
        rmat.set_local_size((100 * SHEET_MATERIAL_AMOUNT) + (T * (25 * SHEET_MATERIAL_AMOUNT)));

        //resources adjustment coefficient (1 -> 0.85 -> 0.7 -> 0.55)
        T = 1.15f;
        foreach (datum_stock_part_micro_laser micro_laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            T -= Dm.num(micro_laser.tier) * 0.15f;
        component_coeff = T;

        //building time adjustment coefficient (1 -> 0.8 -> 0.6)
        T = -1;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            T += Dm.num(servo.tier);
        float initial_time_coeff = Dm.initial(GetType(), (obj_machinery_mecha_part_fabricator f) => f.time_coeff);
        time_coeff = round(initial_time_coeff - (initial_time_coeff * T) / 5, 0.01f);

        // Adjust the build time of any item currently being built.
        if (being_built != null)
        {
            float last_const_time = build_finish - build_start;
            float new_const_time = get_construction_time_w_coeff(Dm.initial(being_built.GetType(), (datum_design d) => d.construction_time));
            float const_time_left = build_finish - world.time;
            float new_build_time = (new_const_time / last_const_time) * const_time_left;
            build_finish = world.time + new_build_time;
        }

        await update_static_data_for_all_viewers();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:136
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Storing up to <b>[]</b> material units.<br>Material consumption at <b>[]%</b>.<br>Build time reduced by <b>[]%</b>.", rmat.local_size, component_coeff * 100, 100 - time_coeff * 100)));
            Dm.append(ref dot, span_notice(I18n.T("Currently configured to drop printed objects <b>[]</b>.", dir2text(drop_direction))));
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:142
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        // isAdminGhostAI(user): no mob is an admin ghost AI yet (is_helpers.cs's GAP: the admins' rights are M10's)
        if (!Dm.truthy(await can_interact(user)) || !HAS_SILICON_ACCESS(user) && !Dm.truthy(await Adjacent(user)))
            return null;
        if (being_built != null)
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

    /**
     * Updates the `final_sets` and `buildable_parts` for the current mecha fabricator.
     */
    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:157
    public virtual async DmTask update_menu_tech()
    {
        float previous_design_count = length(cached_designs);

        cached_designs.Cut();
        foreach (object design_path in Dm.each(stored_research.researched_designs))
        {
            datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
            if (Dm.truthy(Dm.bitand(design.build_type, MECHFAB)) && offers_design(design))
                cached_designs = Dm.bitor(cached_designs, design_path) as DmList;
        }

        float design_delta = length(cached_designs) - previous_design_count;

        if (design_delta > 0)
        {
            await say(I18n.T("Received [] new design[].", design_delta, design_delta == 1 ? "" : "s"));
            playsound(this, "sound/machines/beep/twobeep_high.ogg", 50, TRUE);
        }

        await update_static_data_for_all_viewers();
    }

    /// DEVIATION(DV384): a design whose item is inert (its own procs wait for their system; the MODsuit core's parts, plating and
    /// general modules are offered since gaps MO1, the department modules since gaps MO2; the fabricators' shared check,
    /// datum_design.builds_inert_item) or a mech's part (the mechs are inert) is not offered; tg's techweb offers every MECHFAB design
    /// it has researched.
    public static bool offers_design(datum_design design)
    {
        Type build_path = design.build_path;
        if (build_path == null)
            return true;
        return !design.builds_inert_item() && !ispath(build_path, typeof(obj_item_mecha_parts));
    }

    /**
     * Intended to be called when an item starts printing.
     *
     * Adds the overlay to show the fab working and sets active power usage settings.
     */
    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:179
    public virtual void on_start_printing()
    {
        add_overlay(Dm.text("[]-active", base_icon_state));
        update_use_power(ACTIVE_POWER_USE);
        print_sound.start();
    }

    /**
     * Intended to be called when the exofab has stopped working and is no longer printing items.
     *
     * Removes the overlay to show the fab working and sets idle power usage settings. Additionally resets the description and turns off queue processing.
     */
    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:189
    public virtual void on_finish_printing()
    {
        cut_overlay(Dm.text("[]-active", base_icon_state));
        update_use_power(IDLE_POWER_USE);
        desc = Dm.initial(GetType(), (atom a) => a.desc);
        process_queue = FALSE;
        print_sound.stop();
    }

    /**
     * Attempts to build the next item in the build queue.
     *
     * Returns FALSE if either there are no more parts to build or the next part is not buildable.
     * Returns TRUE if the next part has started building.
     * * verbose - Whether the machine should use say() procs. Set to FALSE to disable the machine saying reasons for failure to build.
     */
    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:203
    public virtual async DmTask<bool> build_next_in_queue(float? verbose = null)
    {
        verbose ??= TRUE;
        if (!Dm.truthy(length(queue)))
            return false;

        DmList queue_record = queue[1] as DmList;
        datum_design D = queue_record["design"] as datum_design;
        DmList user_data = queue_record["user"] as DmList;
        if (await build_part(D, verbose, user_data))
        {
            remove_from_queue(1);
            return true;
        }

        return false;
    }

    /**
     * Starts the build process for a given design datum.
     *
     * Returns FALSE if the procedure fails. Returns TRUE when being_built is set.
     * Uses materials.
     * * D - Design datum to attempt to print.
     * * verbose - Whether the machine should use say() procs. Set to FALSE to disable the machine saying reasons for failure to build.
     * * user_data - ID_DATA(user), see the proc on SSid_access
     */
    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:225
    public virtual async DmTask<bool> build_part(datum_design D = null, float? verbose = null, DmList user_data = null)
    {
        verbose ??= TRUE;
        if (D == null || Dm.truthy(LAZYLEN(D.reagents_list)))
            return false;

        datum_material_container materials = rmat.mat_container;
        if (materials == null)
        {
            if (Dm.truthy(verbose))
                await say(I18n.T("No access to material storage, please contact the quartermaster."));
            return false;
        }
        if (!rmat.can_use_resource(user_data: user_data))
            return false;
        if (!await materials.has_materials(D.materials, component_coeff))
        {
            if (Dm.truthy(verbose))
                await say(I18n.T("Not enough resources. Processing stopped."));
            return false;
        }

        await rmat.use_materials(D.materials, component_coeff, 1, "processed", Dm.text("[]", D.name), user_data);
        being_built = D;
        build_finish = world.time + get_construction_time_w_coeff(Dm.initial(D.GetType(), (datum_design d) => d.construction_time));
        build_start = world.time;
        desc = I18n.T("It's building \\a [].", D.name);

        return true;
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:249
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        // If there's a stored part to dispense due to an obstruction, try to dispense it.
        if (stored_part != null)
        {
            turf exit = get_step(this, drop_direction);
            if (Dm.truthy(exit.density))
                return TRUE;

            Waiting.NoSleep(say(I18n.T("Obstruction cleared. The fabrication of [] is now complete.", stored_part)));
            Waiting.NoSleep(stored_part.forceMove(exit));
            stored_part = null;
        }

        // If there's nothing being built, try to build something
        if (being_built == null)
        {
            // If we're not processing the queue anymore or there's nothing to build, end processing.
            if (!Dm.truthy(process_queue) || !Waiting.NoSleep(build_next_in_queue()))
            {
                on_finish_printing();
                end_processing();
                return TRUE;
            }
            on_start_printing();
        }

        // If there's an item being built, check if it is complete.
        if (being_built != null && build_finish < world.time)
        {
            // Then attempt to dispense it and if appropriate build the next item.
            Waiting.NoSleep(dispense_built_part(being_built));
            if (Dm.truthy(process_queue))
                Waiting.NoSleep(build_next_in_queue(FALSE));
            return TRUE;
        }
        return null;
    }

    /**
     * Dispenses a part to the tile infront of the Exosuit Fab.
     *
     * Returns FALSE is the machine cannot dispense the part on the appropriate turf.
     * Return TRUE if the part was successfully dispensed.
     * * dispensed_design - Design datum to attempt to dispense.
     */
    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:284
    public virtual async DmTask<bool> dispense_built_part(datum_design dispensed_design = null)
    {
        obj_item built_part = await dispensed_design.create_result(this) as obj_item;
        SSblackbox.record_feedback("nested tally", "lathe_printed_items", 1, Dm.list(Dm.text("[]", type), Dm.text("[]", built_part.type)));

        being_built = null;

        turf exit = get_step(this, drop_direction);
        if (Dm.truthy(exit.density))
        {
            await say(I18n.T("Error! The part outlet is obstructed."));
            desc = I18n.T("It's trying to dispense the fabricated [], but the part outlet is obstructed.", dispensed_design.name);
            stored_part = built_part;
            return false;
        }

        await say(I18n.T("The fabrication of [] is now complete.", built_part));
        await built_part.forceMove(exit);

        top_job_id += 1;

        return true;
    }

    /**
     * Adds a datum design to the build queue.
     *
     * Returns TRUE if successful and FALSE if the design was not added to the queue.
     * * D - Datum design to add to the queue.
     * user_data - user data in the form rendered by ID_DATA(user), see the proc on SSidaccess
     */
    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:311
    public virtual bool add_to_queue(datum_design D = null, DmList user_data = null)
    {
        if (queue == null)
            queue = Dm.list();

        if (D != null)
        {
            // queue[++queue.len] = alist("design" = D, "user" = user_data)
            queue.len += 1;
            queue[queue.len] = Dm.list(Dm.kv("design", D), Dm.kv("user", user_data));
            return true;
        }

        return false;
    }

    /**
     * Removes datum design from the build queue based on index.
     *
     * Returns TRUE if successful and FALSE if a design was not removed from the queue.
     * * index - Index in the build queue of the element to remove.
     */
    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:327
    public virtual bool remove_from_queue(object index = null)
    {
        if (!isnum(index) || !(Dm.num(index) == round(Dm.num(index))) /* ISINTEGER */ || queue == null || (Dm.num(index) < 1 || Dm.num(index) > length(queue)))
            return false;
        float i = Dm.num(index);
        queue.Cut(i, i + 1);
        return true;
    }

    /**
     * Calculates the coefficient-modified build time of a design.
     *
     * Returns coefficient-modified build time of a given design.
     * * D - Design datum to calculate the modified build time of.
     * * roundto - Rounding value for round() proc
     */
    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:340
    public virtual float get_construction_time_w_coeff(object construction_time = null, float? roundto = null) //aran
    {
        roundto ??= 1;
        return round(Dm.num(construction_time) * time_coeff, roundto.Value);
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:343
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/sheetmaterials), get_asset_datum(/datum/asset/spritesheet_batched/research_designs)):
        // the asset transport is the client's (DV44); the ExosuitFabricator window draws the materials and the designs' pictures from
        // its own copy of tg's .dmi files.
        return Dm.list();
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:349
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ExosuitFabricator");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:355
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = await rmat.mat_container.ui_static_data() as DmList;

        DmList designs = Dm.list();

        // the research_designs spritesheet's 32x32 size (DV44: the client draws every design from its own "design32x32" sheet, as the
        // biogenerator's), so every icon is the design's asset id with no size prefix

        foreach (object design_path in Dm.each(cached_designs))
        {
            datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
            DmList cost = Dm.list();
            DmList materials = design.materials;
            foreach (datum_material mat in Dm.each<datum_material>(materials))
                cost[mat.name] = OPTIMAL_COST(Dm.num(materials[mat]) * component_coeff);

            designs[design.type] = Dm.list(
                Dm.kv("name", design.name),
                Dm.kv("desc", design.get_description()),
                Dm.kv("cost", cost),
                Dm.kv("path", design.type),
                Dm.kv("categories", design.category),
                Dm.kv("icon", design.asset_id),
                Dm.kv("constructionTime", get_construction_time_w_coeff(design.construction_time))
            );
        }

        data["designs"] = designs;

        return data;
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:385
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["materials"] = await rmat.mat_container.ui_data();
        data["queue"] = Dm.list();
        data["processing"] = process_queue;

        DmList queue_data = data["queue"] as DmList;
        if (being_built != null)
        {
            Dm.append(ref queue_data, Dm.list(Dm.list(
                Dm.kv("jobId", top_job_id),
                Dm.kv("designPath", being_built.type),
                Dm.kv("processing", TRUE),
                Dm.kv("timeLeft", build_finish - world.time)
            )));
        }

        float offset = 0;

        foreach (DmList queue_item in Dm.each<DmList>(queue))
        {
            offset += 1;
            datum_design design = queue_item["design"] as datum_design;

            Dm.append(ref queue_data, Dm.list(Dm.list(
                Dm.kv("jobId", top_job_id + offset),
                Dm.kv("designPath", design.type),
                Dm.kv("processing", FALSE),
                Dm.kv("timeLeft", get_construction_time_w_coeff(design.construction_time) / 10)
            )));
        }

        return data;
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:415
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "build":
            {
                if (!rmat.can_use_resource(user_data: ID_DATA(usr)))
                    return null;

                if (@params["designs"] is not DmList designs)
                    return null;

                foreach (object design_entry in Dm.each(designs))
                {
                    object design_path = Dm.text2path(Dm.str(design_entry));
                    if (!Dm.truthy(stored_research.researched_designs[design_path]))
                        continue;

                    if (SSresearch.techweb_designs[design_path] is not datum_design design)
                        continue;

                    if (!Dm.truthy(Dm.bitand(design.build_type, MECHFAB)))
                        continue;

                    add_to_queue(design, ID_DATA(usr));
                }

                if (Dm.truthy(@params["now"]))
                {
                    if (Dm.truthy(process_queue))
                        return null;

                    process_queue = TRUE;

                    if (being_built == null)
                        begin_processing();
                }

                return TRUE;
            }

            case "del_queue_part":
            {
                // Delete a specific from the queue
                object index = text2num(@params["index"]);
                remove_from_queue(index);
                return TRUE;
            }

            case "clear_queue":
                // Delete everything from queue
                queue.Cut();
                return TRUE;

            case "build_queue":
                // Build everything in queue
                if (Dm.truthy(process_queue))
                    return null;

                process_queue = TRUE;
                if (being_built == null)
                    begin_processing();
                return TRUE;

            case "stop_queue":
                // Pause queue building. Also known as stop.
                process_queue = FALSE;
                return TRUE;

            case "remove_mat":
            {
                datum_material material = Dm.locate_ref(@params["ref"]) as datum_material; // locate(params["ref"]): the material's REF
                object amount = text2num(@params["amount"]);
                // SAFETY: eject_sheets checks for valid mats
                await rmat.eject_sheets(material, Dm.num(amount), user_data: ID_DATA(usr));
                return TRUE;
            }
        }
        return null;
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:487
    public virtual void AfterMaterialInsert(object item_inserted = null, object id_inserted = null, object amount_inserted = null)
    {
        datum_material M = id_inserted as datum_material;
        add_overlay(Dm.text("[]-load-[]", base_icon_state, M.name));
        _addtimer(Dm.@new<datum_callback>(this, nameof(cut_overlay), Dm.text("[]-load-[]", base_icon_state, M.name)), 1 * SECONDS, file: "code/modules/vehicles/mecha/mech_fabricator.dm", line: 490);
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:492
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]-o", base_icon_state) : Dm.text("[]-idle", base_icon_state);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:496
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (being_built != null)
        {
            to_chat(user, span_warning(I18n.T("\\The [] is currently processing! Please wait until completion.", this)));
            return NONE;
        }

        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/vehicles/mecha/mech_fabricator.dm:503
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (being_built != null)
        {
            to_chat(user, span_warning(I18n.T("\\The [] is currently processing! Please wait until completion.", this)));
            return NONE;
        }

        return await default_deconstruction_crowbar(user, tool);
    }
}
