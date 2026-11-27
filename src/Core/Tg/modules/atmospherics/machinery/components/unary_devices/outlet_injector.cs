// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the air injectors): the init (the id tag), the ctrl and alt clicks, the picture, process_atmos and
// can_unwrench. Deferred leaves: the tool screentips and the screentip context (M9) and the investigate logs (M10).
// Generated live-partial stub: add_context (M9). The tgui window (ui_interact, ui_data, ui_act) is tg's AtmosPump.
// The air sensor link (multitool_act) is translated (package E5). The var block is generated.

// tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:1
public partial class obj_machinery_atmospherics_components_unary_outlet_injector : obj_machinery_atmospherics_components_unary
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:21
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (id_tag == null)
            id_tag = assign_random_name();
        object dot = base.Initialize(mapload, __extra);
        // var/static/list/tool_screentips (TOOL_MULTITOOL "Log to link later with air sensor") and
        // AddElement(/datum/element/contextual_screentip_tools, tool_screentips), register_context(): screentips (M9)
        Dm.Deferred("code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:24", "Initialize: AddElement(/datum/element/contextual_screentip_tools, tool_screentips) and register_context(): screentips (M10+)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:42
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You can link it with an air sensor using a multitool.")));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:46  (package E5: the air sensor link)
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multi_tool = (obj_item_multitool)tool;
        if (multi_tool.buffer is obj_machinery_air_sensor sensor)
        {
            multi_tool.set_buffer(this);
            await sensor.multitool_act(user, multi_tool);
            return ITEM_INTERACT_SUCCESS;
        }

        balloon_alert(user, I18n.T("injector saved in buffer"));
        multi_tool.set_buffer(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:57
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        if (Dm.truthy(is_operational))
        {
            set_on(Dm.truthy(on) ? FALSE : TRUE);
            balloon_alert(user, Dm.text("turned []", Dm.truthy(on) ? "on" : "off"));
            investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(user)), INVESTIGATE_ATMOS);
            return CLICK_ACTION_BLOCKING;
        }
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:65
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (volume_rate == MAX_TRANSFER_RATE)
            return CLICK_ACTION_BLOCKING;

        volume_rate = MAX_TRANSFER_RATE;
        investigate_log(Dm.text("was set to [] L/s by []", volume_rate, key_name(user)), INVESTIGATE_ATMOS);
        balloon_alert(user, Dm.text("volume output set to [] L/s", volume_rate));
        update_appearance(UPDATE_ICON);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:75
    public override void update_icon_nopipes(params object[] __extra)
    {
        cut_overlays();
        if (Dm.truthy(underfloor_state))
        {
            // everything is already shifted so don't shift the cap
            image cap = get_pipe_image(icon, "inje_cap", initialize_directions, pipe_color) as image;
            cap.appearance_flags = Dm.bitor(cap.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
            add_overlay(cap);
        }
        else
            PIPING_LAYER_SHIFT(this, PIPING_LAYER_DEFAULT);

        if (!Dm.truthy(nodes[1]) || !Dm.truthy(on) || !Dm.truthy(is_operational))
            icon_state = "inje_off";
        else
            icon_state = "inje_on";
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:90
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(seconds_per_tick, dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(object seconds_per_tick, DotBox<object> dot)
    {
        base.process_atmos(seconds_per_tick);

        if (!Dm.truthy(on) || !Dm.truthy(is_operational))
            return dot.Value;

        turf location = get_turf(loc);
        if (isclosedturf(location))
            return dot.Value;

        datum_gas_mixture air_contents = airs[1] as datum_gas_mixture;

        if (air_contents.temperature > 0)
        {
            float transfer_moles = (Dm.num(air_contents.return_pressure()) * volume_rate) / (air_contents.temperature * R_IDEAL_GAS_EQUATION);

            if (!Dm.truthy(transfer_moles))
                return dot.Value;

            datum_gas_mixture removed = air_contents.remove(transfer_moles) as datum_gas_mixture;

            AirflowSeam.Vent(this, removed, null, outward: true); // DEVIATION(DV34): the clients' vent streams
            await location.assume_air(removed);

            update_parents();
        }
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:113
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AtmosPump", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:119
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["on"] = on;
        data["rate"] = round(volume_rate);
        data["max_rate"] = round(MAX_TRANSFER_RATE);
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:126
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "power"))
        {
            set_on(Dm.truthy(on) ? FALSE : TRUE);
            investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(usr)), INVESTIGATE_ATMOS);
            dot = TRUE;
        }
        else if (Dm.eq(action, "rate"))
        {
            object rate = @params["rate"];
            if (Dm.eq(rate, "max"))
            {
                rate = MAX_TRANSFER_RATE;
                dot = TRUE;
            }
            else if (text2num(rate) != null)
            {
                rate = text2num(rate);
                dot = TRUE;
            }
            if (Dm.truthy(dot))
            {
                volume_rate = clamp(Dm.num(rate), 0, MAX_TRANSFER_RATE);
                investigate_log(Dm.text("was set to [] L/s by []", volume_rate, key_name(usr)), INVESTIGATE_ATMOS);
            }
        }
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/outlet_injector.dm:149
    public override object can_unwrench__proc(mob user = null)
    {
        object dot = base.can_unwrench__proc(user);
        if (Dm.truthy(dot) && Dm.truthy(on) && Dm.truthy(is_operational))
        {
            to_chat(user, span_warning(Dm.text("You cannot unwrench [], turn it off first!", this)));
            return FALSE;
        }
        return dot;
    }
}
