// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the gas mixer): the init, the ctrl and alt clicks, the pictures, process_atmos, adjust_node1_value
// and can_unwrench. Deferred leaves: the screentip context (M9) and the investigate logs (M10). Generated live-partial
// stub: add_context. The tgui window (ui_interact, ui_data, ui_act) is translated (package E1, with the AtmosMixer window).
// The var blocks are generated.

// tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:1
public partial class obj_machinery_atmospherics_components_trinary_mixer : obj_machinery_atmospherics_components_trinary
{
    //node 3 is the outlet, nodes 1 & 2 are intakes

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:21
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_gas_mixture air3 = airs[3] as datum_gas_mixture;
        air3.volume = 300;
        airs[3] = air3;
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context of the held tool
        Dm.Deferred("code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:26", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:34
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

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:42
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (target_pressure == MAX_OUTPUT_PRESSURE)
            return CLICK_ACTION_BLOCKING;

        target_pressure = MAX_OUTPUT_PRESSURE;
        investigate_log(Dm.text("was set to [] kPa by []", target_pressure, key_name(user)), INVESTIGATE_ATMOS);
        balloon_alert(user, Dm.text("pressure output on set to [] kPa", target_pressure));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:51
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            if (!Dm.truthy(Dm.bitand(direction, initialize_directions)))
                continue;
            image cap = get_pipe_image(icon, "cap", direction, pipe_color, Dm.num(piping_layer), TRUE) as image;
            cap.appearance_flags = Dm.bitor(cap.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
            Dm.append(ref dot, cap);
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:61
    public override void update_icon_nopipes(params object[] __extra)
    {
        bool on_state = Dm.truthy(on) && Dm.truthy(nodes[1]) && Dm.truthy(nodes[2]) && Dm.truthy(nodes[3]) && Dm.truthy(is_operational);
        icon_state = Dm.text("mixer_[]-[][]", on_state ? "on" : "off", set_overlay_offset(piping_layer), Dm.truthy(flipped) ? "_f" : "");
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:65
    public override object process_atmos(object seconds_per_tick = null)
    {
        base.process_atmos(seconds_per_tick);
        if (!Dm.truthy(on) || !(Dm.truthy(nodes[1]) && Dm.truthy(nodes[2]) && Dm.truthy(nodes[3])) && !Dm.truthy(is_operational))
            return null;

        //Get those gases, mah boiiii
        datum_gas_mixture air1 = airs[1] as datum_gas_mixture;
        datum_gas_mixture air2 = airs[2] as datum_gas_mixture;

        if (air1 == null || air2 == null)
            return null;

        datum_gas_mixture air3 = airs[3] as datum_gas_mixture;

        float output_starting_pressure = Dm.num(air3.return_pressure());

        if (output_starting_pressure >= target_pressure)
        {
            //No need to mix if target is already full!
            return null;
        }

        //Calculate necessary moles to transfer using PV=nRT
        float general_transfer = (target_pressure - output_starting_pressure) * Dm.num(air3.volume) / R_IDEAL_GAS_EQUATION;

        //Calculate combined temperature for accurate output ratio
        float combined_heat_capacity = Dm.num(air1.heat_capacity()) + Dm.num(air2.heat_capacity());
        float equalized_temperature = Dm.truthy(combined_heat_capacity) ? (Dm.num(air1.thermal_energy()) + Dm.num(air2.thermal_energy())) / combined_heat_capacity : 0;

        float transfer_moles1 = Dm.truthy(equalized_temperature) ? (node1_concentration * general_transfer) / equalized_temperature : 0;
        float transfer_moles2 = Dm.truthy(equalized_temperature) ? (node2_concentration * general_transfer) / equalized_temperature : 0;

        float air1_moles = Dm.num(air1.total_moles());
        float air2_moles = Dm.num(air2.total_moles());

        if (!Dm.truthy(node2_concentration))
        {
            if (air1.temperature <= 0)
                return null;
            transfer_moles1 = min(transfer_moles1, air1_moles);
            transfer_moles2 = 0;
        }
        else if (!Dm.truthy(node1_concentration))
        {
            if (air2.temperature <= 0)
                return null;
            transfer_moles2 = min(transfer_moles2, air2_moles);
            transfer_moles1 = 0;
        }
        else
        {
            if (air1.temperature <= 0 || air2.temperature <= 0)
                return null;
            if ((transfer_moles2 <= 0) || (transfer_moles1 <= 0))
                return null;
            if ((air1_moles < transfer_moles1) || (air2_moles < transfer_moles2))
            {
                float ratio = 0;
                ratio = min(air1_moles / transfer_moles1, air2_moles / transfer_moles2);
                transfer_moles1 *= ratio;
                transfer_moles2 *= ratio;
            }
        }

        //Actually transfer the gas

        if (Dm.truthy(transfer_moles1))
        {
            datum_gas_mixture removed1 = air1.remove(transfer_moles1) as datum_gas_mixture;
            air3.merge(removed1);
            datum_pipeline parent1 = parents[1] as datum_pipeline;
            parent1.update = TRUE;
        }

        if (Dm.truthy(transfer_moles2))
        {
            datum_gas_mixture removed2 = air2.remove(transfer_moles2) as datum_gas_mixture;
            air3.merge(removed2);
            datum_pipeline parent2 = parents[2] as datum_pipeline;
            parent2.update = TRUE;
        }

        datum_pipeline parent3 = parents[3] as datum_pipeline;
        parent3.update = TRUE;
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:136
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AtmosMixer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:142
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["on"] = on;
        data["set_pressure"] = round(Dm.num(target_pressure));
        data["max_pressure"] = round(MAX_OUTPUT_PRESSURE);
        data["node1_concentration"] = round(node1_concentration * 100, 1);
        data["node2_concentration"] = round(node2_concentration * 100, 1);
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:151
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
        else if (Dm.eq(action, "pressure"))
        {
            object pressure = @params["pressure"];
            if (Dm.eq(pressure, "max"))
            {
                pressure = MAX_OUTPUT_PRESSURE;
                dot = TRUE;
            }
            else if (text2num(pressure) != null)
            {
                pressure = text2num(pressure);
                dot = TRUE;
            }
            if (Dm.truthy(dot))
            {
                target_pressure = clamp(Dm.num(pressure), 0, MAX_OUTPUT_PRESSURE);
                investigate_log(Dm.text("was set to [] kPa by []", target_pressure, key_name(usr)), INVESTIGATE_ATMOS);
            }
        }
        else if (Dm.eq(action, "node1"))
        {
            object value = text2num(@params["concentration"]);
            adjust_node1_value(value);
            investigate_log(Dm.text("was set to [] % on node 1 by []", 100 * node1_concentration, key_name(usr)), INVESTIGATE_ATMOS);
            dot = TRUE;
        }
        else if (Dm.eq(action, "node2"))
        {
            object value = text2num(@params["concentration"]);
            adjust_node1_value(100 - Dm.num(value));
            investigate_log(Dm.text("was set to [] % on node 2 by []", 100 * node2_concentration, key_name(usr)), INVESTIGATE_ATMOS);
            dot = TRUE;
        }
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:183
    public virtual void adjust_node1_value(object newValue = null)
    {
        node1_concentration = Dm.num(newValue) / 100;
        node2_concentration = 1 - node1_concentration;
    }

    // tg: code/modules/atmospherics/machinery/components/trinary_devices/mixer.dm:187
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
