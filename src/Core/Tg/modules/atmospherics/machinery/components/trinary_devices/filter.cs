// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/trinary_devices/filter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the gas filter): the init, the ctrl and alt clicks, the pictures, process_atmos and can_unwrench.
// Deferred leaves: the screentip context (M9) and the investigate logs (M10). Generated live-partial stub: add_context.
// The tgui window (ui_interact, ui_data, ui_act) is translated (package E1, with the AtmosFilter window). The var blocks are generated.

// tg: code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:1
public partial class obj_machinery_atmospherics_components_trinary_filter : obj_machinery_atmospherics_components_trinary
{
    // tg: code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:18
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context of the held tool
        Dm.Deferred("code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:18", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:28
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        if (Dm.truthy(is_operational))
        {
            set_on(Dm.truthy(on) ? FALSE : TRUE);
            balloon_alert(user, Dm.text("turned []", Dm.truthy(on) ? "on" : "off"));
            investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(user)), INVESTIGATE_ATMOS);
            return CLICK_ACTION_SUCCESS;
        }
        return CLICK_ACTION_BLOCKING;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:36
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (transfer_rate == MAX_TRANSFER_RATE)
            return CLICK_ACTION_BLOCKING;

        transfer_rate = MAX_TRANSFER_RATE;
        investigate_log(Dm.text("was set to [] L/s by []", transfer_rate, key_name(user)), INVESTIGATE_ATMOS);
        balloon_alert(user, Dm.text("volume output set to [] L/s", transfer_rate));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:45
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            if (!Dm.truthy(Dm.bitand(direction, initialize_directions)))
                continue;

            Dm.append(ref dot, get_pipe_image(icon, "cap", direction, pipe_color, Dm.num(piping_layer), TRUE));
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:53
    public override void update_icon_nopipes(params object[] __extra)
    {
        bool on_state = Dm.truthy(on) && Dm.truthy(nodes[1]) && Dm.truthy(nodes[2]) && Dm.truthy(nodes[3]) && Dm.truthy(is_operational);
        icon_state = Dm.text("filter_[]-[][]", on_state ? "on" : "off", set_overlay_offset(piping_layer), Dm.truthy(flipped) ? "_f" : "");
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:57
    public override object process_atmos(object seconds_per_tick = null)
    {
        base.process_atmos(seconds_per_tick);
        if (!Dm.truthy(on) || !(Dm.truthy(nodes[1]) && Dm.truthy(nodes[2]) && Dm.truthy(nodes[3])) || !Dm.truthy(is_operational))
            return null;

        //Early return
        datum_gas_mixture air1 = airs[1] as datum_gas_mixture;
        if (air1 == null || air1.temperature <= 0)
            return null;

        datum_gas_mixture air2 = airs[2] as datum_gas_mixture;
        datum_gas_mixture air3 = airs[3] as datum_gas_mixture;

        float transfer_ratio = transfer_rate / Dm.num(air1.volume);
        if (transfer_ratio <= 0)
            return null;

        // Attempt to transfer the gas.

        // If the main output is full, we try to send filtered output to the side port (air2).
        // If the side output is full, we try to send the non-filtered gases to the main output port (air3).
        // Any gas that can't be moved due to its destination being too full is sent back to the input (air1).

        bool side_output_full = Dm.num(air2.return_pressure()) >= MAX_OUTPUT_PRESSURE;
        bool main_output_full = Dm.num(air3.return_pressure()) >= MAX_OUTPUT_PRESSURE;

        // If both output ports are full, there's nothing we can do. Don't bother removing anything from the input.
        if (side_output_full && main_output_full)
            return null;

        datum_gas_mixture removed = air1.remove_ratio(transfer_ratio) as datum_gas_mixture;

        if (removed == null || !Dm.truthy(removed.total_moles()))
            return null;

        float filtering = TRUE;
        if (!Dm.truthy(filter_type.len))
            filtering = FALSE;

        // Process if we have a filter set.
        // If no filter is set, we just try to forward everything to air3 to avoid gas being outright lost.
        if (Dm.truthy(filtering))
        {
            datum_gas_mixture filtered_out = Dm.@new<datum_gas_mixture>();

            foreach (object gas in Dm.each(Dm.bitand(removed.moles, filter_type)))
            {
                datum_gas_mixture removing = removed.remove_specific_ratio(gas, 1) as datum_gas_mixture;
                if (removing != null)
                    filtered_out.merge(removing);
            }
            // Send things to the side output if we can, return them to the input if we can't.
            // This means that other gases continue to flow to the main output if the side output is blocked.
            if (side_output_full)
                air1.merge(filtered_out);
            else
                air2.merge(filtered_out);
            // Make sure we don't send any now-empty gas entries to the main output
            removed.garbage_collect();
        }

        // Send things to the main output if we can, return them to the input if we can't.
        // This lets filtered gases continue to flow to the side output in a manner consistent with the main output behavior.
        if (main_output_full)
            air1.merge(removed);
        else
            air3.merge(removed);

        update_parents();
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:124
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AtmosFilter", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:130
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["on"] = on;
        data["rate"] = round(Dm.num(transfer_rate));
        data["max_rate"] = round(MAX_TRANSFER_RATE);

        DmList filter_types = Dm.list();
        data["filter_types"] = filter_types;
        DmList cached_gas_info = GLOB.meta_gas_info;
        foreach (object path in Dm.each(cached_gas_info[META_GAS_ID]))
            Dm.append(filter_types, Dm.list(Dm.list(
                Dm.kv("gas_id", ((DmList)cached_gas_info[META_GAS_ID])[path]),
                Dm.kv("enabled", Dm.@in(path, filter_type) ? TRUE : FALSE)
            )));

        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:143
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
                transfer_rate = clamp(Dm.num(rate), 0, MAX_TRANSFER_RATE);
                investigate_log(Dm.text("was set to [] L/s by []", transfer_rate, key_name(usr)), INVESTIGATE_ATMOS);
            }
        }
        else if (Dm.eq(action, "toggle_filter"))
        {
            if (!Dm.truthy(gas_id2path(@params["val"])))
                return TRUE;
            Dm.bitxor_assign(filter_type, gas_id2path(@params["val"]));
            string change;
            if (Dm.@in(gas_id2path(@params["val"]), filter_type))
                change = "added";
            else
                change = "removed";
            object gas_name = ((DmList)GLOB.meta_gas_info[META_GAS_NAME])[gas_id2path(@params["val"])];
            usr.investigate_log(Dm.text("[] [] from the filter type.", change, gas_name), INVESTIGATE_ATMOS);
            dot = TRUE;
        }
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/filter.dm:177
    public override object can_unwrench__proc(mob user = null)
    {
        object dot = base.can_unwrench__proc(user);
        if (Dm.truthy(dot) && Dm.truthy(on) && Dm.truthy(is_operational))
        {
            balloon_alert(user, "turn off!");
            return FALSE;
        }
        return dot;
    }
}
