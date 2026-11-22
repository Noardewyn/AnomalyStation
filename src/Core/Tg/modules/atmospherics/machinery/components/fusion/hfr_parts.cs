// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Package E12b (docs/ai/specs/engineering.md): the whole file, the eight parts around the core (the fuel, moderator and waste ports, the
// interface and the corners), the interface's Hypertorus window and the boxes that deploy the machine. The two examines are translated (X1). Deferred leaves: the emissive glow of the active parts (DEVIATION DV12) and the recipe change's investigate log (admin
// tooling, not-translated.md 92). The var blocks are generated.

/**
 * This file contain the eight parts surrounding the main core, those are: fuel input, moderator input, waste output, interface and the corners
 * The file also contain the guicode of the machine
 */
// tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:5
public partial class obj_machinery_atmospherics_components_unary_hypertorus : obj_machinery_atmospherics_components_unary
{
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:26
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        initialize_directions = dir;
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:30
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("[] can be rotated by first opening the panel with a screwdriver and then using a wrench on it.", this)));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:34
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return Dm.truthy(fusion_started) ? NONE : default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:37
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        return await default_change_direction_wrench(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:40
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(cracked))
            return FALSE;
        if (Dm.truthy(user.combat_mode))
            return FALSE;
        balloon_alert(user, I18n.T("repairing..."));
        if (Dm.truthy(await tool.use_tool(this, user, 10 * SECONDS, volume: 30)))
        {
            balloon_alert(user, I18n.T("repaired"));
            cracked = FALSE;
            update_appearance(UPDATE_ICON);
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:51
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await crowbar_deconstruction_act(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:54
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[]_open", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = (string)base_icon_state;
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:61
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(cracked))
            Dm.append(ref dot, image(icon, icon_state: "crack", dir: this.dir));
        if (Dm.truthy(active))
        {
            Dm.append(ref dot, Dm.text("[]_active", base_icon_state));
            // DEVIATION(DV12): . += emissive_appearance(icon, "[base_icon_state]_active", src, alpha = src.alpha), the glow on the
            // emissive plane, is not created (lighting planes and emissives are not ported).
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:69
    public override void update_layer()
    {
        return;
    }
}

/*
* Interface and corners
*/
// tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:96
public partial class obj_machinery_hypertorus : obj_machinery
{
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:111
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("[] can be rotated by first opening the panel with a screwdriver and then using a wrench on it.", this)));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:115
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return Dm.truthy(fusion_started) ? NONE : default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:118
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        return await default_change_direction_wrench(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:121
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:124
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[]_open", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = (string)base_icon_state;
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:131
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(active))
        {
            Dm.append(ref dot, Dm.text("[]_active", base_icon_state));
            // DEVIATION(DV12): . += emissive_appearance(icon, "[base_icon_state]_active", src, alpha = src.alpha) is not created.
        }
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:137
public partial class obj_machinery_hypertorus_interface : obj_machinery_hypertorus
{
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:148
    public override object Destroy(float? force = null)
    {
        if (connected_core != null)
            connected_core = null;
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:153
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        turf T = get_step(this, REVERSE_DIR(dir)) as turf;
        obj_machinery_atmospherics_components_unary_hypertorus_core centre = Dm.locate<obj_machinery_atmospherics_components_unary_hypertorus_core>(T);

        if (centre == null || !Dm.truthy(centre.check_part_connectivity()))
        {
            to_chat(user, span_notice(I18n.T("Check all parts and then try again.")));
            return TRUE;
        }

        connected_core = centre;
        connected_core.activate(user);
        if (!Dm.truthy(activated))
        {
            Dm.@new<obj_item_paper_guides_jobs_atmos_hypertorus>(loc);
            activated = TRUE;
        }

        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:170
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (Dm.truthy(active))
        {
            ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
            if (!Dm.truthy(ui))
            {
                ui = Dm.@new<datum_tgui>(user, this, "Hypertorus", name);
                await ui.open();
            }
        }
        else
        {
            to_chat(user, span_notice(I18n.T("Activate the machine first by using a multitool on the interface.")));
            // ui.close(): ui is null when the window is not open yet (a runtime in tg, "Cannot execute null.close()", at the proc's last line)
            if (ui != null)
                ui.close();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:180
    public virtual object gas_list_to_gasid_list(DmList gas_list = null)
    {
        DmList gasid_list = Dm.list();
        foreach (Type gas_type in Dm.each<Type>(gas_list))
            Dm.append(gasid_list, Dm.initial(gas_type, (datum_gas gas) => gas.id));
        return gasid_list;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:189
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["base_max_temperature"] = FUSION_MAXIMUM_TEMPERATURE;
        DmList selectable_fuel = Dm.list(Dm.list(Dm.kv("name", "Nothing"), Dm.kv("id", null)));
        data["selectable_fuel"] = selectable_fuel;
        foreach (object path in Dm.each(GLOB.hfr_fuels_list))
        {
            datum_hfr_fuel recipe = GLOB.hfr_fuels_list[path] as datum_hfr_fuel;

            Dm.append(selectable_fuel, Dm.list(Dm.list(
                Dm.kv("name", recipe.name),
                Dm.kv("id", recipe.id),
                Dm.kv("requirements", gas_list_to_gasid_list(recipe.requirements)),
                Dm.kv("fusion_byproducts", gas_list_to_gasid_list(recipe.primary_products)),
                Dm.kv("product_gases", gas_list_to_gasid_list(recipe.secondary_products)),
                Dm.kv("recipe_cooling_multiplier", recipe.negative_temperature_multiplier),
                Dm.kv("recipe_heating_multiplier", recipe.positive_temperature_multiplier),
                Dm.kv("energy_loss_multiplier", recipe.energy_concentration_multiplier),
                Dm.kv("fuel_consumption_multiplier", recipe.fuel_consumption_multiplier),
                Dm.kv("gas_production_multiplier", recipe.gas_production_multiplier),
                Dm.kv("temperature_multiplier", recipe.temperature_change_multiplier)
            )));
        }
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:211
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        if (connected_core.selected_fuel != null)
            data["selected"] = connected_core.selected_fuel.id;
        else
            data["selected"] = "";

        //Internal Fusion gases
        DmList fusion_gasdata = Dm.list();
        if (Dm.truthy(connected_core.internal_fusion.total_moles()))
        {
            foreach (Type gas in Dm.each<Type>(connected_core.internal_fusion.moles))
                fusion_gasdata.Add(Dm.list(Dm.list(
                    Dm.kv("id", Dm.initial(gas, (datum_gas g) => g.id)),
                    Dm.kv("amount", round(Dm.num(connected_core.internal_fusion.moles[gas]), 0.01f))
                )));
        }
        else
        {
            foreach (Type gas in Dm.each<Type>(connected_core.internal_fusion.moles))
                fusion_gasdata.Add(Dm.list(Dm.list(
                    Dm.kv("id", Dm.initial(gas, (datum_gas g) => g.id)),
                    Dm.kv("amount", 0)
                )));
        }
        //Moderator gases
        DmList moderator_gasdata = Dm.list();
        if (Dm.truthy(connected_core.moderator_internal.total_moles()))
        {
            foreach (Type gas in Dm.each<Type>(connected_core.moderator_internal.moles))
                moderator_gasdata.Add(Dm.list(Dm.list(
                    Dm.kv("id", Dm.initial(gas, (datum_gas g) => g.id)),
                    Dm.kv("amount", round(Dm.num(connected_core.moderator_internal.moles[gas]), 0.01f))
                )));
        }
        else
        {
            foreach (Type gas in Dm.each<Type>(connected_core.moderator_internal.moles))
                moderator_gasdata.Add(Dm.list(Dm.list(
                    Dm.kv("id", Dm.initial(gas, (datum_gas g) => g.id)),
                    Dm.kv("amount", 0)
                )));
        }

        data["fusion_gases"] = fusion_gasdata;
        data["moderator_gases"] = moderator_gasdata;

        data["energy_level"] = connected_core.energy;
        data["heat_limiter_modifier"] = connected_core.heat_limiter_modifier;
        data["heat_output_min"] = connected_core.heat_output_min;
        data["heat_output_max"] = connected_core.heat_output_max;
        data["heat_output"] = connected_core.heat_output;
        data["instability"] = connected_core.instability;

        data["heating_conductor"] = connected_core.heating_conductor;
        data["magnetic_constrictor"] = connected_core.magnetic_constrictor;
        data["fuel_injection_rate"] = connected_core.fuel_injection_rate;
        data["moderator_injection_rate"] = connected_core.moderator_injection_rate;
        data["current_damper"] = connected_core.current_damper;

        data["power_level"] = connected_core.power_level;
        data["apc_energy"] = connected_core.get_area_cell_percent();
        data["iron_content"] = connected_core.iron_content;
        data["integrity"] = connected_core.get_integrity_percent();

        data["start_power"] = connected_core.start_power;
        data["start_cooling"] = connected_core.start_cooling;
        data["start_fuel"] = connected_core.start_fuel;
        data["start_moderator"] = connected_core.start_moderator;

        data["internal_fusion_temperature"] = connected_core.fusion_temperature;
        data["moderator_internal_temperature"] = connected_core.moderator_temperature;
        data["internal_output_temperature"] = connected_core.output_temperature;
        data["internal_coolant_temperature"] = connected_core.coolant_temperature;

        data["internal_fusion_temperature_archived"] = connected_core.fusion_temperature_archived;
        data["moderator_internal_temperature_archived"] = connected_core.moderator_temperature_archived;
        data["internal_output_temperature_archived"] = connected_core.output_temperature_archived;
        data["internal_coolant_temperature_archived"] = connected_core.coolant_temperature_archived;
        data["temperature_period"] = connected_core.temperature_period;

        data["waste_remove"] = connected_core.waste_remove;
        DmList filter_types = Dm.list();
        data["filter_types"] = filter_types;
        DmList cached_gas_info = GLOB.meta_gas_info;
        foreach (object path in Dm.each(cached_gas_info[META_GAS_ID]))
            Dm.append(filter_types, Dm.list(Dm.list(
                Dm.kv("gas_id", ((DmList)cached_gas_info[META_GAS_ID])[path]),
                Dm.kv("gas_name", ((DmList)cached_gas_info[META_GAS_NAME])[path]),
                Dm.kv("enabled", Dm.@in(path, connected_core.moderator_scrubbing) ? TRUE : FALSE)
            )));

        data["cooling_volume"] = ((datum_gas_mixture)connected_core.airs[1]).volume;
        data["mod_filtering_rate"] = connected_core.moderator_filtering_rate;

        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:304
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "start_power":
                connected_core.start_power = Dm.truthy(connected_core.start_power) ? FALSE : TRUE;
                connected_core.update_use_power(Dm.truthy(connected_core.start_power) ? ACTIVE_POWER_USE : IDLE_POWER_USE);
                dot = TRUE;
                break;
            case "start_cooling":
                connected_core.start_cooling = Dm.truthy(connected_core.start_cooling) ? FALSE : TRUE;
                dot = TRUE;
                break;
            case "start_fuel":
                connected_core.start_fuel = Dm.truthy(connected_core.start_fuel) ? FALSE : TRUE;
                dot = TRUE;
                break;
            case "start_moderator":
                connected_core.start_moderator = Dm.truthy(connected_core.start_moderator) ? FALSE : TRUE;
                dot = TRUE;
                break;
            case "heating_conductor":
            {
                float? heating_conductor = text2num(@params["heating_conductor"]);
                if (heating_conductor != null)
                {
                    connected_core.heating_conductor = clamp(heating_conductor.Value, 50, 500);
                    dot = TRUE;
                }
                break;
            }
            case "magnetic_constrictor":
            {
                float? magnetic_constrictor = text2num(@params["magnetic_constrictor"]);
                if (magnetic_constrictor != null)
                {
                    connected_core.magnetic_constrictor = clamp(magnetic_constrictor.Value, 50, 1000);
                    dot = TRUE;
                }
                break;
            }
            case "fuel_injection_rate":
            {
                float? fuel_injection_rate = text2num(@params["fuel_injection_rate"]);
                if (fuel_injection_rate != null)
                {
                    connected_core.fuel_injection_rate = clamp(fuel_injection_rate.Value, 0.5f, 150);
                    dot = TRUE;
                }
                break;
            }
            case "moderator_injection_rate":
            {
                float? moderator_injection_rate = text2num(@params["moderator_injection_rate"]);
                if (moderator_injection_rate != null)
                {
                    connected_core.moderator_injection_rate = clamp(moderator_injection_rate.Value, 0.5f, 150);
                    dot = TRUE;
                }
                break;
            }
            case "current_damper":
            {
                float? current_damper = text2num(@params["current_damper"]);
                if (current_damper != null)
                {
                    connected_core.current_damper = clamp(current_damper.Value, 0, 1000);
                    dot = TRUE;
                }
                break;
            }
            case "waste_remove":
                connected_core.waste_remove = Dm.truthy(connected_core.waste_remove) ? FALSE : TRUE;
                dot = TRUE;
                break;
            case "filter":
                Dm.bitxor_assign(connected_core.moderator_scrubbing, gas_id2path(@params["mode"]));
                dot = TRUE;
                break;
            case "mod_filtering_rate":
            {
                float? mod_filtering_rate = text2num(@params["mod_filtering_rate"]);
                if (mod_filtering_rate != null)
                {
                    connected_core.moderator_filtering_rate = clamp(mod_filtering_rate.Value, 5, 200);
                    dot = TRUE;
                }
                break;
            }
            case "fuel":
            {
                connected_core.selected_fuel = null;
                object fuel_mix = "nothing";
                datum_hfr_fuel fuel = null;
                if (!Dm.eq(@params["mode"], ""))
                    fuel = GLOB.hfr_fuels_list[@params["mode"]] as datum_hfr_fuel;
                if (fuel != null)
                {
                    connected_core.selected_fuel = fuel;
                    fuel_mix = fuel.name;
                }
                if (Dm.truthy(connected_core.internal_fusion.total_moles()))
                    await connected_core.dump_gases();
                connected_core.update_parents(); //prevent the machine from stopping because of the recipe change and the pipenet not updating
                connected_core.linked_input.update_parents();
                connected_core.linked_output.update_parents();
                connected_core.linked_moderator.update_parents();
                investigate_log(Dm.text("was set to recipe [] by []", Dm.truthy(fuel_mix) ? fuel_mix : "null", key_name(usr)), INVESTIGATE_ATMOS);
                dot = TRUE;
                break;
            }
            case "cooling_volume":
            {
                float? cooling_volume = text2num(@params["cooling_volume"]);
                if (cooling_volume != null)
                {
                    ((datum_gas_mixture)connected_core.airs[1]).volume = clamp(cooling_volume.Value, 50, 2000);
                    dot = TRUE;
                }
                break;
            }
        }
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:455
public partial class obj_item_hfr_box_core : obj_item_hfr_box
{
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:462
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        DmList parts = Dm.list();
        foreach (obj_item_hfr_box box in Dm.each<obj_item_hfr_box>(orange(1, this)))
        {
            float direction = get_dir(this, box);
            if (box.box_type == "corner")
            {
                if (Dm.truthy(ISDIAGONALDIR(direction)))
                {
                    switch (direction)
                    {
                        case NORTHEAST:
                            direction = EAST;
                            break;
                        case SOUTHEAST:
                            direction = SOUTH;
                            break;
                        case SOUTHWEST:
                            direction = WEST;
                            break;
                        case NORTHWEST:
                            direction = NORTH;
                            break;
                    }
                    box.dir = direction;
                    Dm.combine(parts, box);
                }
                continue;
            }
            if (box.box_type == "body")
            {
                if (Dm.@in(direction, GLOB.cardinals))
                {
                    box.dir = direction;
                    Dm.combine(parts, box);
                }
                continue;
            }
        }
        if (parts.len == 8)
            build_reactor(parts);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_parts.dm:490
    public virtual void build_reactor(DmList parts = null)
    {
        foreach (obj_item_hfr_box box in Dm.each<obj_item_hfr_box>(parts))
        {
            if (box.box_type == "corner")
            {
                obj_machinery_hypertorus_corner corner = Dm.@new(box.part_path, box.loc) as obj_machinery_hypertorus_corner;
                corner.dir = box.dir;
                qdel(box);
                continue;
            }
            if (box.box_type == "body")
            {
                object location = get_turf(box);
                if (box.part_path != typeof(obj_machinery_hypertorus_interface))
                {
                    obj_machinery_atmospherics_components_unary_hypertorus part = Dm.@new(box.part_path, location, TRUE, box.dir) as obj_machinery_atmospherics_components_unary_hypertorus;
                    part.dir = box.dir;
                }
                else
                {
                    obj_machinery_hypertorus_interface part = Dm.@new(box.part_path, location) as obj_machinery_hypertorus_interface;
                    part.dir = box.dir;
                }
                qdel(box);
                continue;
            }
        }

        Dm.@new<obj_machinery_atmospherics_components_unary_hypertorus_core>(loc, TRUE);
        qdel(this);
    }
}
