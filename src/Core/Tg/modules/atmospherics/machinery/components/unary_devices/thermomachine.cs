// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 4 (the engine's cooling loop): the whole file, the freezer and the heater (their heat transfer, the power use through
// the APC, the tool acts, the construction from the board and the parts, the picture, the window). Its USB port takes the USB
// circuit component (wiremod/components/usb/thermomachine.dm). The investigate logs and the emissive glow stay deferred
// leaves (admin tooling, M10; DEVIATION(DV12)), each with a row in
// docs/ai/specs/not-translated.md. The var block is generated.

// tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:1  #define THERMOMACHINE_POWER_CONVERSION 0.01 (file-local, #undef at :396)

// tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:4
public partial class obj_machinery_atmospherics_components_unary_thermomachine : obj_machinery_atmospherics_components_unary
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:1
    private const float THERMOMACHINE_POWER_CONVERSION = 0.01f;

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:43
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_ICON);
        if (Dm.truthy(interactive))
        {
            Waiting.NoSleep(AddComponent(typeof(datum_component_usb_port), typecacheof(Dm.list(typeof(obj_item_circuit_component_thermomachine)), only_root_path: TRUE)));
        }
        register_context();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:50
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        context[SCREENTIP_CONTEXT_CTRL_LMB] = Dm.text("Turn [on ? \"off\" : \"on\"]", Dm.truthy(on) ? "off" : "on");
        context[SCREENTIP_CONTEXT_ALT_LMB] = "Cycle temperature";
        if (held_item == null)
            return CONTEXTUAL_SCREENTIP_SET;
        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] panel", Dm.truthy(panel_open) ? "Close" : "Open");
        else if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Rotate";
            context[SCREENTIP_CONTEXT_RMB] = Dm.text("[]chor", Dm.truthy(anchored) ? "Unan" : "An");
        }
        else if (Dm.eq(held_item.tool_behaviour, TOOL_MULTITOOL))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Change piping layer";
            context[SCREENTIP_CONTEXT_RMB] = "Change piping color";
        }
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:67
    public override object is_connectable(obj_machinery_atmospherics target = null, object given_layer = null, params object[] __extra)
    {
        if (!Dm.truthy(anchored))
            return FALSE;
        return base.is_connectable(target, given_layer, __extra);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:72
    [DmParams("user", "obj_color", "set_layer")]
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        object obj_color = Dm.arg(__extra, 0);
        object set_layer = Dm.arg(__extra, 1);
        obj_item_circuitboard_machine_thermomachine board = circuit_value as obj_item_circuitboard_machine_thermomachine; // GAP(types): the board instance is circuit_value
        if (board != null)
        {
            piping_layer = board.pipe_layer;
            set_layer = piping_layer;
        }

        await base.on_construction(user, Dm.forward(__extra, obj_color, set_layer)); //Skipping the rest of on_construction() would be a bad idea so we clean up after it instead.

        if (Dm.truthy(check_pipe_on_turf()))
        {
            await set_anchored(FALSE);
            set_panel_open(TRUE);
            balloon_alert(user, I18n.T("the port is already in use!"));
        }
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:85
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float calculated_bin_rating = 0;
        foreach (datum_stock_part_matter_bin bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            calculated_bin_rating += Dm.num(bin.tier);
        heat_capacity = 5000 * Dm.pow(calculated_bin_rating - 1, 2);

        float calculated_laser_rating = 0;
        foreach (datum_stock_part_micro_laser laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            calculated_laser_rating += Dm.num(laser.tier);
        min_temperature = max(T0C - (base_cooling + calculated_laser_rating * 15), TCMB); //73.15K with T1 stock parts
        max_temperature = T20C + (base_heating * calculated_laser_rating); //573.15K with T1 stock parts
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:98
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        string colors_to_use = "";
        float sw__1 = Dm.num(target_temperature);
        if (Dm.in_to(sw__1, BODYTEMP_HEAT_WARNING_3, INFINITY))
            colors_to_use = COLOR_RED;
        else if (Dm.in_to(sw__1, BODYTEMP_HEAT_WARNING_2, BODYTEMP_HEAT_WARNING_3))
            colors_to_use = COLOR_ORANGE;
        else if (Dm.in_to(sw__1, BODYTEMP_HEAT_WARNING_1, BODYTEMP_HEAT_WARNING_2))
            colors_to_use = COLOR_YELLOW;
        else if (Dm.in_to(sw__1, BODYTEMP_COLD_WARNING_1, BODYTEMP_HEAT_WARNING_1))
            colors_to_use = COLOR_VIBRANT_LIME;
        else if (Dm.in_to(sw__1, BODYTEMP_COLD_WARNING_2, BODYTEMP_COLD_WARNING_1))
            colors_to_use = COLOR_CYAN;
        else if (Dm.in_to(sw__1, BODYTEMP_COLD_WARNING_3, BODYTEMP_COLD_WARNING_2))
            colors_to_use = COLOR_BLUE;
        else
            colors_to_use = COLOR_VIOLET;

        if (!Dm.eq(greyscale_colors, colors_to_use))
            await set_greyscale(colors: colors_to_use);

        if (Dm.truthy(panel_open))
        {
            icon_state = "thermo-open";
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(on) && Dm.truthy(is_operational))
        {
            icon_state = "thermo_1";
            return await base.update_icon_state(__extra);
        }
        icon_state = "thermo_base";
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:128
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        image pipe = get_pipe_image(Dm.resource("icons/obj/machines/atmospherics/thermomachine.dmi"), "pipe", dir, SELECT_ATMOS_NODE_COLOR(this, nodes[1] as obj_machinery_atmospherics), Dm.num(piping_layer)) as image;
        pipe.appearance_flags = Dm.bitor(pipe.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
        Dm.append(ref dot, pipe);

        if (Dm.truthy(on) && Dm.truthy(is_operational))
        {
            // this is cursed but both these emissive_appearance() are needed
            // one gives emissives to mapload machinery that are already on
            // the other gives emissives when updates happen (on/off/pressure change/etc.)
            // . += emissive_appearance(icon, "thermo-emissive", src, alpha = src.alpha) and the add_overlay of the same: DEVIATION(DV12), the glow on the emissive plane
            Dm.Deferred("code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:138", "update_overlays: emissive_appearance of the lit screen (DEVIATION DV12)");
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:141
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("With the panel open:")));
        Dm.append(ref dot, span_notice(I18n.T(" -Use a wrench with left-click to rotate [] and right-click to unanchor it.", this)));
        Dm.append(ref dot, span_notice(I18n.T(" -Use a multitool with left-click to change the piping layer and right-click to change the piping color.")));
        Dm.append(ref dot, span_notice(I18n.T(" -[] to cycle between temperaure ranges.", EXAMINE_HINT(I18n.T("AltClick")))));
        Dm.append(ref dot, span_notice(I18n.T(" -[] to toggle on/off.", EXAMINE_HINT(I18n.T("CtrlClick")))));
        Dm.append(ref dot, span_notice(I18n.T("The thermostat is set to []K ([]C).", target_temperature, (T0C - Dm.num(target_temperature)) * -1)));

        if (in_range(user, this) || isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("Heat capacity at <b>[] Joules per Kelvin</b>.", heat_capacity)));
            Dm.append(ref dot, span_notice(I18n.T("Temperature range <b>[]K - []K ([]C - []C)</b>.", min_temperature, max_temperature, (T0C - min_temperature) * -1, (T0C - max_temperature) * -1)));
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:154
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("close panel!"));
            return CLICK_ACTION_BLOCKING;
        }

        if (Dm.eq(target_temperature, T20C))
            target_temperature = max_temperature;
        else if (Dm.eq(target_temperature, max_temperature))
            target_temperature = min_temperature;
        else
            target_temperature = T20C;

        investigate_log(Dm.text("was set to [] K by []", target_temperature, key_name(user)), INVESTIGATE_ATMOS);
        balloon_alert(user, I18n.T("temperature reset to [] K", target_temperature));
        update_appearance(UPDATE_ICON);
        return CLICK_ACTION_SUCCESS;
    }

    /// Performs heat calculation for the freezer.
    /// We just equalize the gasmix with an object at temp = var/target_temperature and heat cap = var/heat_capacity
    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:173
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        if (!Dm.truthy(on))
            return dot.Value;

        turf local_turf = get_turf(this) as turf;

        if (!Dm.truthy(is_operational) || local_turf == null)
        {
            set_on(FALSE);
            return dot.Value;
        }

        // The gas we want to cool/heat
        datum_gas_mixture port = airs[1] as datum_gas_mixture;

        if (!Dm.truthy(port.total_moles())) // Nothing to cool? go home lad
            return dot.Value;

        float port_capacity = Dm.num(port.heat_capacity());

        // The difference between target and what we need to heat/cool. Positive if heating, negative if cooling.
        float temperature_target_delta = Dm.num(target_temperature) - port.temperature;

        // We perfectly can do W1+W2 / C1+C2 here but this lets us count the power easily.
        float heat_amount = CALCULATE_CONDUCTION_ENERGY(temperature_target_delta, port_capacity, heat_capacity);

        port.temperature = max(((port.temperature * port_capacity) + heat_amount) / port_capacity, TCMB);

        heat_amount = min(abs(heat_amount), 1e8f) * THERMOMACHINE_POWER_CONVERSION;

        // This produces a nice curve that scales decently well for really hot stuff, and is nice to not fusion. It'll do
        float power_usage = idle_power_usage + Dm.pow(heat_amount * 0.05f, 1.05f - (5e7f * 0.16f / max(heat_amount, 5e7f)));

        await use_energy(power_usage);
        update_parents();
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:207
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(on))
        {
            balloon_alert(user, I18n.T("turn off!"));
            return ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("anchor!"));
            return ITEM_INTERACT_SUCCESS;
        }

        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:217
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        return await default_change_direction_wrench(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:220
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await crowbar_deconstruction_act(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:223
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open panel!"));
            return ITEM_INTERACT_SUCCESS;
        }
        piping_layer = (Dm.num(piping_layer) >= PIPING_LAYER_MAX) ? PIPING_LAYER_MIN : (Dm.num(piping_layer) + 1);
        to_chat(user, span_notice(I18n.T("You change the circuitboard to layer [].", piping_layer)));
        if (Dm.truthy(anchored))
            reconnect_nodes();
        update_appearance(UPDATE_ICON);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:234
    public override async DmTask<object> multitool_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open panel!"));
            return ITEM_INTERACT_SUCCESS;
        }
        color_index = (color_index >= GLOB.pipe_paint_colors.len) ? (color_index = 1) : (color_index = 1 + color_index);
        set_pipe_color(GLOB.pipe_paint_colors[GLOB.pipe_paint_colors[color_index]]);
        visible_message(span_notice(I18n.T("[] set []'s pipe color to [].", user, this, GLOB.pipe_color_name[pipe_color])), ignored_mobs: user);
        to_chat(user, span_notice(I18n.T("You set []'s pipe color to [].", this, GLOB.pipe_color_name[pipe_color])));
        if (Dm.truthy(anchored))
            reconnect_nodes();
        update_appearance(UPDATE_ICON);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:247
    public virtual object check_pipe_on_turf()
    {
        foreach (obj_machinery_atmospherics device in Dm.each<obj_machinery_atmospherics>(get_turf(this)))
        {
            if (device == this)
                continue;
            if (Dm.eq(device.piping_layer, piping_layer))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:255
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open panel!"));
            return ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(anchored) && Dm.truthy(check_pipe_on_turf()))
        {
            visible_message(span_warning(I18n.T("A pipe is hogging the port. Remove the obstruction or change the machine piping layer.")));
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(await default_unfasten_wrench(user, tool)))
        {
            change_pipe_connection(Dm.not(anchored));
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:267
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (Dm.truthy(interactive))
            return await base.ui_status(user, state);
        return UI_CLOSE;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:272
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (Dm.truthy(panel_open))
            return null;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ThermoMachine", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:280
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["on"] = on;

        data["min"] = min_temperature;
        data["max"] = max_temperature;
        data["target"] = target_temperature;
        data["initial"] = Dm.initial(this, (obj_machinery_atmospherics_components_unary_thermomachine t) => t.target_temperature);

        datum_gas_mixture port = airs[1] as datum_gas_mixture;
        data["temperature"] = port.temperature;
        data["pressure"] = port.return_pressure();
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:294
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "power":
                set_on(Dm.truthy(on) ? FALSE : TRUE);
                update_use_power(Dm.truthy(on) ? ACTIVE_POWER_USE : IDLE_POWER_USE);
                investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(usr)), INVESTIGATE_ATMOS);
                dot = TRUE;
                break;

            case "target":
            {
                object target = @params["target"];
                float? adjust = text2num(@params["adjust"]);
                if (Dm.eq(target, "input"))
                {
                    target = await input(usr, I18n.T("Set new target ([]-[] K):", min_temperature, max_temperature), name, target_temperature); // as num|null
                    if (target != null)
                        dot = TRUE;
                }
                else if (Dm.truthy(adjust))
                {
                    target = Dm.num(target_temperature) + Dm.num(adjust);
                    dot = TRUE;
                }
                else if (text2num(target) != null)
                {
                    target = text2num(target);
                    dot = TRUE;
                }
                if (Dm.truthy(dot))
                {
                    target_temperature = clamp(Dm.num(target), min_temperature, max_temperature);
                    investigate_log(Dm.text("was set to [] K by []", target_temperature, key_name(usr)), INVESTIGATE_ATMOS);
                }
                break;
            }
        }

        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:324
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        if (!Dm.truthy(anchored))
            return NONE;
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("close panel!"));
            return CLICK_ACTION_BLOCKING;
        }
        if (!Dm.truthy(is_operational))
            return CLICK_ACTION_BLOCKING;

        set_on(Dm.truthy(on) ? FALSE : TRUE);
        balloon_alert(user, I18n.T("turned []", Dm.truthy(on) ? "on" : "off"));
        investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(user)), INVESTIGATE_ATMOS);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:338
    public override void update_layer()
    {
        return;
    }
}

// tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:357
public partial class obj_machinery_atmospherics_components_unary_thermomachine_freezer_on : obj_machinery_atmospherics_components_unary_thermomachine_freezer
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:363
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(target_temperature, Dm.initial(this, (obj_machinery_atmospherics_components_unary_thermomachine t) => t.target_temperature)))
            target_temperature = min_temperature;
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:371
public partial class obj_machinery_atmospherics_components_unary_thermomachine_freezer_on_coldroom : obj_machinery_atmospherics_components_unary_thermomachine_freezer_on
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm:376
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        target_temperature = COLD_ROOM_TEMP;
        return dot;
    }
}
