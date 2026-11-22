// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/floodlight.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the frame built from iron with its wiring and light tube, and the floodlight that draws straight from the cable
// under it. The var blocks are generated.

// tg: code/modules/power/floodlight.dm:6
public partial class obj_structure_floodlight_frame : obj_structure
{
    // tg: code/modules/power/floodlight.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/modules/power/floodlight.dm:21
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        string message = null;
        if (Dm.eq(state, FLOODLIGHT_NEEDS_WIRES))
        {
            if (istype(held_item, typeof(obj_item_stack_cable_coil)))
                message = "Add cable";
            else if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
                message = "Dismantle frame";
        }
        else if (Dm.eq(state, FLOODLIGHT_NEEDS_SECURING))
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
                message = "Secure cable";
            else if (Dm.eq(held_item.tool_behaviour, TOOL_WIRECUTTER))
                message = "Cut cable";
        }
        else if (Dm.eq(state, FLOODLIGHT_NEEDS_LIGHTS))
        {
            if (istype(held_item, typeof(obj_item_light_tube)))
                message = "Add light";
            else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
                message = "Unscrew cable";
        }

        if (message == null)
            return NONE;
        context[SCREENTIP_CONTEXT_LMB] = message;
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/power/floodlight.dm:55
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.eq(state, FLOODLIGHT_NEEDS_WIRES))
        {
            Dm.append(ref dot, span_notice(I18n.T("It can be wired with [].", EXAMINE_HINT(I18n.T("5 cable pieces")))));
            Dm.append(ref dot, span_notice(I18n.T("The frame can be deconstructed by [].", EXAMINE_HINT(I18n.T("unwrenching")))));
        }
        else if (Dm.eq(state, FLOODLIGHT_NEEDS_SECURING))
        {
            Dm.append(ref dot, span_notice(I18n.T("The cable needs to be [] on to the frame.", EXAMINE_HINT(I18n.T("screwed")))));
            Dm.append(ref dot, span_notice(I18n.T("The hanging cable could be [] apart.", EXAMINE_HINT(I18n.T("cut")))));
        }
        else if (Dm.eq(state, FLOODLIGHT_NEEDS_LIGHTS))
        {
            Dm.append(ref dot, span_notice(I18n.T("It needs a [] to finish it.", EXAMINE_HINT(I18n.T("light tube")))));
            Dm.append(ref dot, span_notice(I18n.T("The cable could be [] from the frame.", EXAMINE_HINT(I18n.T("unscrewed")))));
        }
        return dot;
    }

    // tg: code/modules/power/floodlight.dm:67
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item O = null)
    {
        await base.screwdriver_act(user, O);
        if (Dm.eq(state, FLOODLIGHT_NEEDS_SECURING))
        {
            icon_state = "floodlight_c3";
            state = FLOODLIGHT_NEEDS_LIGHTS;
            return ITEM_INTERACT_SUCCESS;
        }
        else if (Dm.eq(state, FLOODLIGHT_NEEDS_LIGHTS))
        {
            icon_state = "floodlight_c2";
            state = FLOODLIGHT_NEEDS_SECURING;
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/power/floodlight.dm:79
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(state, FLOODLIGHT_NEEDS_WIRES))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("deconstructing..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 30, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        Dm.@new<obj_item_stack_sheet_iron>(loc, 5);
        qdel(this);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/floodlight.dm:91
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(state, FLOODLIGHT_NEEDS_SECURING))
            return ITEM_INTERACT_BLOCKING;

        icon_state = "floodlight_c1";
        state = FLOODLIGHT_NEEDS_WIRES;
        Dm.@new<obj_item_stack_cable_coil>(loc, 5);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/floodlight.dm:101
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_stack_cable_coil)) && Dm.eq(state, FLOODLIGHT_NEEDS_WIRES))
        {
            obj_item_stack coil = tool as obj_item_stack;
            if (!Dm.truthy(await coil.use(5)))
            {
                balloon_alert(user, I18n.T("need 5 cable pieces!"));
                return ITEM_INTERACT_BLOCKING;
            }

            icon_state = "floodlight_c2";
            state = FLOODLIGHT_NEEDS_SECURING;
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_light_tube)))
        {
            if (!Dm.eq(state, FLOODLIGHT_NEEDS_LIGHTS))
            {
                balloon_alert(user, I18n.T("construction not completed!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.eq((tool as obj_item_light_tube).status, LIGHT_BROKEN)) // light tube broken.
            {
                balloon_alert(user, I18n.T("light tube is broken!"));
                return ITEM_INTERACT_BLOCKING;
            }

            Dm.@new<obj_machinery_power_floodlight>(loc);
            qdel(this);
            qdel(tool);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }
}

// tg: code/modules/power/floodlight.dm:136
public partial class obj_machinery_power_floodlight : obj_machinery_power
{
    // tg: code/modules/power/floodlight.dm:1
    private const int FLOODLIGHT_OFF = 1;
    // tg: code/modules/power/floodlight.dm:2
    private const int FLOODLIGHT_LOW = 2;
    // tg: code/modules/power/floodlight.dm:3
    private const int FLOODLIGHT_MED = 3;
    // tg: code/modules/power/floodlight.dm:4
    private const int FLOODLIGHT_HIGH = 4;

    // tg: code/modules/power/floodlight.dm:156
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_OBJ_PAINTED, nameof(on_color_change)); //update light color when color changes
        register_context();
        if (Dm.truthy(mapload))
        {
            Waiting.NoSleep(set_anchored(TRUE));
            connect_to_network();
        }
        return dot;
    }

    // tg: code/modules/power/floodlight.dm:164
    [SignalHandler]
    public virtual void on_color_change(obj_machinery_power flood_light = null, mob user = null, obj_item_toy_crayon_spraycan spraycan = null, object is_dark_color = null)
    {
        if (!Dm.truthy(spraycan.actually_paints))
            return;

        if (setting > FLOODLIGHT_OFF)
            Waiting.NoSleep(update_light_state());
    }

    // tg: code/modules/power/floodlight.dm:172
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(this, COMSIG_OBJ_PAINTED);
        return base.Destroy(force);
    }

    /// change light color during operation
    // tg: code/modules/power/floodlight.dm:177
    public virtual async DmTask update_light_state()
    {
        object light_color = NONSENSICAL_VALUE;
        if (color != null)
            light_color = color;
        if (Dm.truthy(cached_color_filter))
            light_color = apply_matrix_to_color(COLOR_WHITE, cached_color_filter["color"] as DmList, Dm.truthy(cached_color_filter["space"]) ? Dm.num(cached_color_filter["space"]) : COLORSPACE_RGB);
        await set_light(light_setting_list[setting], light_power, light_color);
    }

    // tg: code/modules/power/floodlight.dm:185
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
        {
            if (Dm.truthy(panel_open))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Remove Light";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            return NONE;
        }

        string message = null;
        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            message = "Open Panel";
        else if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
            message = Dm.truthy(anchored) ? "Unsecure light" : "Secure light";

        if (message == null)
            return NONE;
        context[SCREENTIP_CONTEXT_LMB] = message;
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/power/floodlight.dm:209
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It needs to be wrenched on top of a wire.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("It's at power level [].", setting)));
        if (Dm.truthy(panel_open))
        {
            Dm.append(ref dot, span_notice(I18n.T("Its maintenance hatch is open but can be [] closed.", EXAMINE_HINT(I18n.T("screwed")))));
            Dm.append(ref dot, span_notice(I18n.T("You can remove the light tube by [].", EXAMINE_HINT(I18n.T("hand")))));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("Its maintenance hatch can be [] open.", EXAMINE_HINT(I18n.T("screwed")))));
        return dot;
    }

    // tg: code/modules/power/floodlight.dm:221
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        turf T = get_turf(this);
        obj_structure_cable C = Dm.locate<obj_structure_cable>(T);
        if (C == null && Dm.truthy(powernet))
            disconnect_from_network();
        if (setting > FLOODLIGHT_OFF) //If on
        {
            if (Dm.truthy(avail(active_power_usage)))
                add_load(active_power_usage);
            else
                await change_setting(FLOODLIGHT_OFF);
        }
        else if (Dm.truthy(avail(idle_power_usage)))
            add_load(idle_power_usage);
        return null;
    }

    // tg: code/modules/power/floodlight.dm:234
    public virtual async DmTask<object> change_setting(object newval = null, mob user = null)
    {
        if ((Dm.num(newval) < FLOODLIGHT_OFF) || (Dm.num(newval) > light_setting_list.len))
            return null;

        setting = Dm.num(newval);
        active_power_usage = Dm.num(light_setting_list[setting]) * light_power_coefficient;
        if (!Dm.truthy(avail(active_power_usage)) && setting > FLOODLIGHT_OFF)
            return await change_setting(setting - 1);
        await update_light_state();
        string setting_text = "";
        if (setting > FLOODLIGHT_OFF)
            icon_state = Dm.text("[]_on", Dm.initial(this, (obj_machinery_power_floodlight p) => p.icon_state));
        else
            icon_state = Dm.initial(this, (obj_machinery_power_floodlight p) => p.icon_state);
        object sw__1 = setting;
        if (Dm.eq(sw__1, FLOODLIGHT_OFF))
            setting_text = I18n.T("OFF");
        else if (Dm.eq(sw__1, FLOODLIGHT_LOW))
            setting_text = I18n.T("low power");
        else if (Dm.eq(sw__1, FLOODLIGHT_MED))
            setting_text = I18n.T("standard lighting");
        else if (Dm.eq(sw__1, FLOODLIGHT_HIGH))
            setting_text = I18n.T("high power");
        if (Dm.truthy(user))
            to_chat(user, span_notice(I18n.T("You set [] to [].", this, setting_text)));
        return null;
    }

    // tg: code/modules/power/floodlight.dm:260
    public override async DmTask<object> cable_layer_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("unanchor first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.cable_layer_act(user, tool);
    }

    // tg: code/modules/power/floodlight.dm:266
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await default_unfasten_wrench(user, tool);
        await change_setting(FLOODLIGHT_OFF);
        if (Dm.truthy(anchored))
            connect_to_network();
        else
            disconnect_from_network();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/floodlight.dm:275
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(panel_open))
        {
            panel_open = FALSE;
            balloon_alert(user, I18n.T("closed panel"));
            return ITEM_INTERACT_SUCCESS;
        }
        await change_setting(FLOODLIGHT_OFF);
        panel_open = TRUE;
        balloon_alert(user, I18n.T("opened panel"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/floodlight.dm:285
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(panel_open))
        {
            obj_structure_floodlight_frame floodlight_frame = Dm.@new<obj_structure_floodlight_frame>(loc);
            floodlight_frame.state = FLOODLIGHT_NEEDS_LIGHTS;

            obj_item_light_tube light_tube = Dm.@new<obj_item_light_tube>(loc);
            await user.put_in_active_hand(light_tube);

            qdel(this);
        }

        float current = Dm.num(setting);
        if (current == FLOODLIGHT_OFF)
            current = light_setting_list.len;
        else
            current--;
        await change_setting(current, user);
        return dot;
    }

    // tg: code/modules/power/floodlight.dm:305
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        return await attack_hand(user);
    }

    // tg: code/modules/power/floodlight.dm:308
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return await attack_hand(user);
    }

    // tg: code/modules/power/floodlight.dm:311
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        await base.on_saboteur(source, disrupt_duration);
        await atom_break(ENERGY); // technically,
        return TRUE;
    }

    // tg: code/modules/power/floodlight.dm:316
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(dot))
            return dot;
        playsound(loc, "sound/effects/glass/glassbr3.ogg", 100, TRUE);

        obj_structure_floodlight_frame floodlight_frame = Dm.@new<obj_structure_floodlight_frame>(loc);
        floodlight_frame.state = FLOODLIGHT_NEEDS_LIGHTS;
        obj_item_light_tube our_light = Dm.@new<obj_item_light_tube>(loc);
        await our_light.shatter();

        qdel(this);
        return dot;
    }

    // tg: code/modules/power/floodlight.dm:329
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        playsound(this, "sound/effects/glass/glasshit.ogg", 75, TRUE);
        return null;
    }
}
