// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/component_printer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file: the component printer (the circuit component designs of its techweb, printed from its
// materials or the ore silo, its parts' efficiency, the recycling of components, the link to a circuit for the editor's remote
// printing), the debug component printer and the module duplicator (it saves a module or a circuit and prints it again, with
// its scan and print animations and its ping). DV384: a design whose item is not live is not offered. The ComponentPrinter
// window's design icons are the research_designs sheet the client draws (DV44). The var blocks are generated (D111).

/// Component printer, creates components for integrated circuits.
// tg: code/modules/wiremod/core/component_printer.dm:2
public partial class obj_machinery_component_printer : obj_machinery
{
    // tg: code/modules/wiremod/core/component_printer.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        materials = Dm.@new<datum_remote_materials>(this, mapload);
        return dot;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:28
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link))) && techweb == null) /* CONFIG_GET(flag/no_default_techweb_link) */
            CONNECT_TO_RND_SERVER_ROUNDSTART(ref techweb, this);
        if (techweb != null)
            on_connected_techweb();
    }

    // tg: code/modules/wiremod/core/component_printer.dm:35
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref materials);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/core/component_printer.dm:39
    public virtual void connect_techweb(datum_techweb new_techweb = null)
    {
        if (techweb != null)
            UnregisterSignal(techweb, Dm.list(COMSIG_TECHWEB_ADD_DESIGN, COMSIG_TECHWEB_REMOVE_DESIGN));
        techweb = new_techweb;
        if (techweb != null)
            on_connected_techweb();
    }

    // tg: code/modules/wiremod/core/component_printer.dm:46
    public virtual void on_connected_techweb()
    {
        foreach (object researched_design_path in Dm.each(techweb.researched_designs))
        {
            datum_design design = SSresearch.techweb_designs[researched_design_path] as datum_design;
            if (!Dm.truthy(Dm.bitand(design.build_type, COMPONENT_PRINTER)) || !ispath(design.build_path, typeof(obj_item_circuit_component)))
                continue;
            // DEVIATION(DV384): a component whose type is not live is not offered
            if (design.builds_inert_item())
                continue;

            current_unlocked_designs[design.build_path] = researched_design_path;
        }

        RegisterSignal(techweb, COMSIG_TECHWEB_ADD_DESIGN, nameof(on_research));
        RegisterSignal(techweb, COMSIG_TECHWEB_REMOVE_DESIGN, nameof(on_removed));
    }

    // tg: code/modules/wiremod/core/component_printer.dm:57
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multitool = tool as obj_item_multitool;
        if (!QDELETED(multitool.buffer) && istype(multitool.buffer, typeof(datum_techweb)))
            connect_techweb(multitool.buffer as datum_techweb);
        return TRUE;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:62
    [SignalHandler]
    public virtual void on_research(datum source = null, datum_design added_design = null, object custom = null)
    {
        if (!Dm.truthy(Dm.bitand(added_design.build_type, COMPONENT_PRINTER)) || !ispath(added_design.build_path, typeof(obj_item_circuit_component)))
            return;
        // DEVIATION(DV384): a component whose type is not live is not offered
        if (added_design.builds_inert_item())
            return;
        current_unlocked_designs[added_design.build_path] = added_design.type;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:68
    [SignalHandler]
    public virtual void on_removed(datum source = null, datum_design added_design = null, object custom = null)
    {
        if (!Dm.truthy(Dm.bitand(added_design.build_type, COMPONENT_PRINTER)) || !ispath(added_design.build_path, typeof(obj_item_circuit_component)))
            return;
        Dm.remove(current_unlocked_designs, added_design.build_path);
    }

    // tg: code/modules/wiremod/core/component_printer.dm:74
    public override async DmTask<object> base_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        //to allow quick recycling of circuits
        if (istype(tool, typeof(obj_item_circuit_component)))
        {
            object amount_inserted = await materials.insert_item(tool, user_data: ID_DATA(user));

            if (Dm.truthy(amount_inserted))
                to_chat(user, span_notice(I18n.T("[] worth [] sheets of material was consumed by []", tool, Dm.num(amount_inserted) / SHEET_MATERIAL_AMOUNT, this)));
            else
                to_chat(user, span_warning(I18n.T("[] was rejected by []", tool, this)));

            return Dm.num(amount_inserted) > 0 ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_FAILURE;
        }

        return await base.base_item_interaction(user, tool, modifiers);
    }

    // tg: code/modules/wiremod/core/component_printer.dm:88
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ComponentPrinter", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:94
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/sheetmaterials), get_asset_datum(/datum/asset/spritesheet_batched/research_designs)):
        // the asset transport is the client's (DV44); the client draws the materials and the designs from its own sheets
        return Dm.list();
    }

    // tg: code/modules/wiremod/core/component_printer.dm:100
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        if (materials != null)
        {
            float total_storage = 0;

            foreach (datum_stock_part_matter_bin bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
                total_storage += Dm.num(bin.tier) * (37.5f * SHEET_MATERIAL_AMOUNT);

            materials.set_local_size(total_storage);
        }

        efficiency_coeff = 0;

        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
        {
            ///we do -1 because normal manipulators rating of 1 gives us 1-1=0 i.e no decrement in cost
            efficiency_coeff += Dm.num(servo.tier) - 1;
        }

        ///linear interpolation between full cost i.e 1 & 1/8th the cost i.e 0.125
        ///we do it in 6 steps because maximum rating of 2 manipulators is 8 but -1 gives us 6
        efficiency_coeff = 1.0f + ((0.125f - 1.0f) * (efficiency_coeff / 6));

        await update_static_data_for_all_viewers();
        return dot;
    }

    /**
     * typepath - the type path of the component to be printed
     * user_data - data in the form rendered by ID_DATA(user), for print logging, see the proc on SSid_access
    */
    // tg: code/modules/wiremod/core/component_printer.dm:127
    public virtual async DmTask<object> print_component(object typepath = null, DmList user_data = null)
    {
        object design_path = current_unlocked_designs[typepath];
        datum_design design = SSresearch.techweb_designs[design_path] as datum_design;

        if (!Dm.truthy(Dm.bitand(design.build_type, COMPONENT_PRINTER)))
            return null;

        if (!materials.can_use_resource(user_data: user_data))
            return null;

        if (!await materials.mat_container.has_materials(design.materials, efficiency_coeff))
            return null;

        await materials.use_materials(design.materials, efficiency_coeff, 1, "processed", Dm.text("[]", design.name), user_data);
        return await design.create_result(drop_location() as atom);
    }

    // tg: code/modules/wiremod/core/component_printer.dm:143
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        DmList user_data = ID_DATA(usr);

        switch (action)
        {
            case "print":
            {
                object design_path = text2path(@params["design_path"] as string);
                if (!Dm.truthy(techweb.researched_designs[design_path]))
                    return null;

                datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
                if (!istype(design, typeof(datum_design)))
                    return null;

                if (!Dm.truthy(Dm.bitand(design.build_type, COMPONENT_PRINTER)))
                    return null;

                // DEVIATION(DV384): a component whose type is not live is not printed
                if (design.builds_inert_item())
                    return null;

                if (!materials.can_use_resource(user_data: user_data))
                    return null;

                if (!await materials.mat_container.has_materials(design.materials, efficiency_coeff))
                {
                    await say(I18n.T("Not enough materials."));
                    return null;
                }

                balloon_alert_to_viewers(I18n.T("printed []", design.name));

                await materials.use_materials(design.materials, efficiency_coeff, 1, "processed", Dm.text("[]", design.name), user_data);
                atom printed_design = await design.create_result(drop_location() as atom) as atom;
                printed_design.pixel_x = printed_design.base_pixel_x + rand(-5, 5);
                printed_design.pixel_y = printed_design.base_pixel_y + rand(-5, 5);
                return TRUE;
            }
            case "remove_mat":
            {
                datum_material material = Dm.locate_ref(@params["ref"]) as datum_material;
                object amount = text2num(@params["amount"]);
                if (!Dm.truthy(amount))
                    return null;
                // SAFETY: eject_sheets checks for valid mats
                await materials.eject_sheets(material_ref: material, eject_amount: amount, user_data: user_data);
                return TRUE;
            }
        }
        return null;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:186
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["materials"] = await materials.mat_container.ui_data();
        return data;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:191
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = await materials.mat_container.ui_static_data() as DmList;

        DmList designs = Dm.list();

        // the research_designs spritesheet's 32x32 size (DV44: the client draws every design from its own "design32x32" sheet, as the
        // fabricators do), so every icon is the design's asset id with no size prefix

        // for (var/datum/design/component/component_design_type as anything in subtypesof(/datum/design/component))
        foreach (object researched_design_path in Dm.each(techweb.researched_designs))
        {
            datum_design design = SSresearch.techweb_designs[researched_design_path] as datum_design;
            if (!Dm.truthy(Dm.bitand(design.build_type, COMPONENT_PRINTER)))
                continue;
            // DEVIATION(DV384): a design whose item is not live is not offered
            if (design.builds_inert_item())
                continue;

            DmList cost = Dm.list();
            foreach ((object _mat, object amount) in Dm.pairs(design.materials))
            {
                datum_material mat = _mat as datum_material;
                cost[mat.name] = OPTIMAL_COST(Dm.num(amount) * efficiency_coeff);
            }

            designs[researched_design_path] = Dm.list(
                Dm.kv("name", design.name),
                Dm.kv("desc", design.desc),
                Dm.kv("cost", cost),
                Dm.kv("path", researched_design_path),
                Dm.kv("categories", design.category),
                Dm.kv("icon", design.asset_id));
        }

        data["designs"] = designs;

        return data;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:223
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        obj_item_integrated_circuit circuit = null;
        if (istype(tool, typeof(obj_item_integrated_circuit)))
            circuit = tool as obj_item_integrated_circuit;
        else if (istype(tool, typeof(obj_item_circuit_component_module)))
        {
            obj_item_circuit_component_module module = tool as obj_item_circuit_component_module;
            circuit = module.internal_circuit;
        }

        if (circuit == null)
            return NONE;

        circuit.linked_component_printer = WEAKREF(this) as datum_weakref;
        await circuit.update_static_data_for_all_viewers();
        balloon_alert(user, I18n.T("successfully linked to the integrated circuit"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:242
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/wiremod/core/component_printer.dm:245
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/wiremod/core/component_printer.dm:248
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]-o", base_icon_state) : Dm.text("[]-idle", base_icon_state);
        return dot;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:252
    public virtual object get_material_cost_data(DmList materials = null, object efficiency_coeff = null)
    {
        DmList data = Dm.list();

        foreach (object material_type in Dm.each(materials))
            data[Dm.initial(material_type, (datum_material m) => m.name)] = Dm.num(materials[material_type]) * Dm.num(efficiency_coeff);

        return data;
    }
}

// tg: code/modules/wiremod/core/component_printer.dm:260
public partial class obj_machinery_debug_component_printer : obj_machinery
{
    // tg: code/modules/wiremod/core/component_printer.dm:270
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        all_circuit_designs = Dm.list();

        foreach ((object design_path, object _design) in Dm.pairs(SSresearch.techweb_designs))
        {
            datum_design design = _design as datum_design;
            if (!Dm.truthy(Dm.bitand(design.build_type, COMPONENT_PRINTER)) || !ispath(design.build_path))
                continue;
            all_circuit_designs[design.build_path] = Dm.list(
                Dm.kv("id", design.build_path),
                Dm.kv("categories", design.category),
                Dm.kv("cost", design.materials),
                Dm.kv("desc", design.desc),
                Dm.kv("name", design.name));
        }

        foreach (Type component_path in Dm.each<Type>(subtypesof(typeof(obj_item_circuit_component))))
        {
            if (Dm.truthy(all_circuit_designs[component_path]))
                continue;

            DmList categories;
            if (Dm.truthy(Dm.bitand(Dm.initial(component_path, (obj_item_circuit_component c) => c.circuit_flags), CIRCUIT_FLAG_ADMIN)))
                categories = Dm.list("Admin");
            else
                categories = Dm.list("Inaccessible");

            all_circuit_designs[component_path] = Dm.list(
                Dm.kv("id", component_path), // component_path.type: the typepath itself (a path's .type is its path)
                Dm.kv("categories", categories),
                Dm.kv("cost", Dm.list()),
                Dm.kv("desc", Dm.initial(component_path, (obj_item_circuit_component c) => c.desc)),
                Dm.kv("name", Dm.initial(component_path, (obj_item_circuit_component c) => c.display_name)));
        }
        return dot;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:304
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ComponentPrinter", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:310
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // the material and design sheets: the asset transport is the client's (DV44)
        return Dm.list();
    }

    // tg: code/modules/wiremod/core/component_printer.dm:316
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "print":
            {
                object build_path = text2path(@params["design_path"] as string);
                if (!Dm.truthy(build_path))
                    return null;

                DmList design = all_circuit_designs[build_path] as DmList;
                if (!Dm.truthy(design))
                    return null;

                balloon_alert_to_viewers(I18n.T("printed []", design["name"]));
                atom printed_design = await Dm.new_async((Type)build_path, drop_location()) as atom;
                printed_design.pixel_x = printed_design.base_pixel_x + rand(-5, 5);
                printed_design.pixel_y = printed_design.base_pixel_y + rand(-5, 5);
                return TRUE;
            }
        }
        return null;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:337
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["debug"] = TRUE;
        data["designs"] = all_circuit_designs;
        data["materials"] = Dm.list();
        data["SHEET_MATERIAL_AMOUNT"] = SHEET_MATERIAL_AMOUNT;

        return data;
    }
}

/// Module duplicator, allows you to save and recreate module components.
// tg: code/modules/wiremod/core/component_printer.dm:348
public partial class obj_machinery_module_duplicator : obj_machinery
{
    // tg: code/modules/wiremod/core/component_printer.dm:366
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        materials = Dm.@new<datum_remote_materials>(this, mapload);
        return dot;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:371
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref materials);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/core/component_printer.dm:375
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ComponentPrinter", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:381
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // the material and design sheets: the asset transport is the client's (DV44)
        return Dm.list();
    }

    // tg: code/modules/wiremod/core/component_printer.dm:387
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        if (materials != null)
        {
            float total_storage = 0;

            foreach (datum_stock_part_matter_bin bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
                total_storage += Dm.num(bin.tier) * (37.5f * SHEET_MATERIAL_AMOUNT);

            materials.set_local_size(total_storage);
        }

        efficiency_coeff = 0;

        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
        {
            ///we do -1 because normal manipulators rating of 1 gives us 1-1=0 i.e no decrement in cost
            efficiency_coeff += Dm.num(servo.tier) - 1;
        }

        ///linear interpolation between full cost i.e 1 & 1/8th the cost i.e 0.125
        ///we do it in 6 steps because maximum rating of 2 manipulators is 8 but -1 gives us 6
        efficiency_coeff = 1.0f + ((0.125f - 1.0f) * (efficiency_coeff / 6));

        await update_static_data_for_all_viewers();
        return dot;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:410
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        DmList user_data = ID_DATA(usr);

        switch (action)
        {
            case "print":
            {
                float design_id = Dm.num(text2num(@params["design_path"]));

                if (design_id < 1 || design_id > length(scanned_designs))
                    return TRUE;

                DmList design = scanned_designs[design_id] as DmList;

                if (!materials.can_use_resource(user_data: user_data))
                    return TRUE;

                if (!await materials.mat_container.has_materials(design["materials"] as DmList, efficiency_coeff))
                {
                    await say(I18n.T("Not enough materials."));
                    return TRUE;
                }

                // tg passes the design's materials as the log's name (component_printer.dm:430)
                await materials.use_materials(design["materials"] as DmList, efficiency_coeff, 1, Dm.str(design["name"]), Dm.text("[]", design["materials"]), user_data: user_data);
                print_module(design);
                balloon_alert_to_viewers(I18n.T("printed []", design["name"]));
                break;
            }
            case "remove_mat":
            {
                datum_material material = Dm.locate_ref(@params["ref"]) as datum_material;
                object amount = text2num(@params["amount"]);
                // SAFETY: eject_sheets checks for valid mats
                await materials.eject_sheets(material, amount, user_data: ID_DATA(usr));
                break;
            }
        }

        return TRUE;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:443
    public virtual void print_module(DmList design = null)
    {
        Dm.flick("module-fab-print", this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_module_print), design), 1.6f * SECONDS, file: "code/modules/wiremod/core/component_printer.dm", line: 445);
    }

    // tg: code/modules/wiremod/core/component_printer.dm:447
    public virtual async DmTask finish_module_print(DmList design = null)
    {
        atom_movable created_atom;
        if (Dm.truthy(design["integrated_circuit"]))
        {
            obj_item_integrated_circuit circuit = await Dm.new_async<obj_item_integrated_circuit>(drop_location());
            DmList errors = Dm.list();
            await circuit.load_circuit_data(design["dupe_data"], errors);
            if (Dm.truthy(length(errors)))
                stack_trace(Dm.text("Error loading user saved circuit [].", errors.Join(", ")), "code/modules/wiremod/core/component_printer.dm", 453);
            created_atom = circuit;
        }
        else
        {
            obj_item_circuit_component_module module = await Dm.new_async<obj_item_circuit_component_module>(drop_location());
            await module.load_data_from_list(design["dupe_data"] as DmList);
            created_atom = module;
        }
        created_atom.pixel_x = created_atom.base_pixel_x + rand(-5, 5);
        created_atom.pixel_y = created_atom.base_pixel_y + rand(-5, 5);
    }

    // tg: code/modules/wiremod/core/component_printer.dm:463
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        DmList data = Dm.list();

        if (istype(tool, typeof(obj_item_circuit_component_module)))
        {
            obj_item_circuit_component_module module = tool as obj_item_circuit_component_module;
            if (HAS_TRAIT(module, TRAIT_CIRCUIT_UNDUPABLE))
            {
                balloon_alert(user, I18n.T("integrated circuit cannot be saved!"));
                return ITEM_INTERACT_BLOCKING;
            }

            DmList dupe_data = Dm.list();
            data["dupe_data"] = dupe_data;
            module.save_data_to_list(dupe_data);

            data["name"] = module.display_name;
            data["desc"] = I18n.T("A module that has been loaded in by [].", user);
            data["materials"] = Dm.list(Dm.kv(await SSmaterials.get_material(typeof(datum_material_glass)), module.circuit_size * cost_per_component));
        }
        else if (istype(tool, typeof(obj_item_integrated_circuit)))
        {
            obj_item_integrated_circuit integrated_circuit = tool as obj_item_integrated_circuit;
            if (HAS_TRAIT(integrated_circuit, TRAIT_CIRCUIT_UNDUPABLE))
            {
                balloon_alert(user, I18n.T("integrated circuit cannot be saved!"));
                return ITEM_INTERACT_BLOCKING;
            }

            data["dupe_data"] = integrated_circuit.convert_to_json();

            data["name"] = integrated_circuit.display_name;
            data["desc"] = I18n.T("An integrated circuit that has been loaded in by [].", user);

            datum_design_integrated_circuit circuit_design = SSresearch.techweb_designs[typeof(datum_design_integrated_circuit)] as datum_design_integrated_circuit;
            DmList materials = Dm.list(Dm.kv(await SSmaterials.get_material(typeof(datum_material_glass)), integrated_circuit.current_size * cost_per_component));
            foreach ((object material_type, object amount) in Dm.pairs(circuit_design.materials))
                materials[material_type] = Dm.add(materials[material_type], amount);

            data["materials"] = materials;
            data["integrated_circuit"] = TRUE;
        }

        if (!Dm.truthy(length(data)))
            return NONE;

        if (!Dm.truthy(data["name"]))
        {
            balloon_alert(user, I18n.T("it needs a name!"));
            return ITEM_INTERACT_BLOCKING;
        }

        foreach (DmList component_data in Dm.each_cast<DmList>(scanned_designs))
        {
            if (Dm.eq(component_data["name"], data["name"]))
            {
                balloon_alert(user, I18n.T("name already exists!"));
                return ITEM_INTERACT_BLOCKING;
            }
        }

        Dm.flick("module-fab-scan", this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_module_scan), user, data), 1.4f * SECONDS, file: "code/modules/wiremod/core/component_printer.dm", line: 510);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:513
    public virtual async DmTask finish_module_scan(mob user = null, object data = null)
    {
        Dm.append(scanned_designs, Dm.list(data));

        balloon_alert(user, I18n.T("module has been saved."));
        playsound(this, "sound/machines/ping.ogg", 50);

        await update_static_data_for_all_viewers();
    }

    // tg: code/modules/wiremod/core/component_printer.dm:521
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["materials"] = await materials.mat_container.ui_data();
        return data;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:526
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = await materials.mat_container.ui_static_data() as DmList;

        DmList designs = Dm.list();

        float index = 1;
        foreach (DmList design in Dm.each_cast<DmList>(scanned_designs))
        {
            DmList cost = Dm.list();
            DmList materials = design["materials"] as DmList;
            foreach (datum_material mat in Dm.each<datum_material>(materials))
                cost[mat.name] = OPTIMAL_COST(Dm.num(materials[mat]) * efficiency_coeff);

            designs[Dm.text("[]", index)] = Dm.list(
                Dm.kv("name", design["name"]),
                Dm.kv("desc", design["desc"]),
                Dm.kv("cost", cost),
                Dm.kv("id", Dm.text("[]", index)),
                Dm.kv("icon", "integrated_circuit"),
                Dm.kv("categories", Dm.list("/Saved Circuits")));
            index++;
        }

        data["designs"] = designs;

        return data;
    }

    // tg: code/modules/wiremod/core/component_printer.dm:553
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/wiremod/core/component_printer.dm:556
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/wiremod/core/component_printer.dm:559
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]-o", base_icon_state) : Dm.text("[]-idle", base_icon_state);
        return dot;
    }
}
