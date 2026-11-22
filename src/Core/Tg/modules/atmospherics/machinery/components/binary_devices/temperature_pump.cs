// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice D172 (the temperature pump, which the rapid pipe dispenser dispenses): the whole file except the circuit USB port
// (circuits), the screentip context (M9) and the investigate logs (M10), which are deferred leaves (generated live-partial stub:
// add_context). The window is tg's AtmosTempPump interface. /obj/item/circuit_component/atmos_temperature_pump stays inert
// (circuits). The var block is generated.

// tg: code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm:1
public partial class obj_machinery_atmospherics_components_binary_temperature_pump : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm:16
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddComponent(/datum/component/usb_port, typecacheof(list(/obj/item/circuit_component/atmos_temperature_pump), only_root_path = TRUE)): circuits
        Dm.Deferred("code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm:18", "Initialize: AddComponent(/datum/component/usb_port): the circuit USB port (circuits)");
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context of the held tool
        Dm.Deferred("code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm:19", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm:27
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

    // tg: code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm:35
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (heat_transfer_rate == max_heat_transfer_rate)
            return CLICK_ACTION_BLOCKING;

        heat_transfer_rate = max_heat_transfer_rate;
        investigate_log(Dm.text("was set to []% by []", heat_transfer_rate, key_name(user)), INVESTIGATE_ATMOS);
        balloon_alert(user, Dm.text("transfer rate set to []%", heat_transfer_rate));
        update_appearance(UPDATE_ICON);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm:45
    public override void update_icon_nopipes(params object[] __extra)
    {
        string new_icon_state = Dm.text("tpump_[]-[]", Dm.truthy(on) && Dm.truthy(is_operational) ? "on" : "off", set_overlay_offset(piping_layer));
        string old_icon_state = icon_state;
        icon_state = new_icon_state;
        if (new_icon_state != old_icon_state)
            update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm:52
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        if (!Dm.truthy(on) || !Dm.truthy(is_operational))
            return dot.Value;

        datum_gas_mixture air_input = airs[1] as datum_gas_mixture;
        datum_gas_mixture air_output = airs[2] as datum_gas_mixture;

        if (QUANTIZE(air_input.total_moles()) == 0 || QUANTIZE(air_output.total_moles()) == 0) // Don't transfer if there's no gas
            return dot.Value;
        datum_gas_mixture remove_input = air_input.remove_ratio(0.9f) as datum_gas_mixture;
        datum_gas_mixture remove_output = air_output.remove_ratio(0.9f) as datum_gas_mixture;

        float coolant_temperature_delta = remove_input.temperature - remove_output.temperature;

        if (coolant_temperature_delta > 0)
        {
            float input_capacity = Dm.num(remove_input.heat_capacity());
            float output_capacity = Dm.num(remove_output.heat_capacity());

            float cooling_heat_amount = (heat_transfer_rate * 0.01f) * CALCULATE_CONDUCTION_ENERGY(coolant_temperature_delta, output_capacity, input_capacity);
            remove_output.temperature = max(remove_output.temperature + (cooling_heat_amount / output_capacity), TCMB);
            remove_input.temperature = max(remove_input.temperature - (cooling_heat_amount / input_capacity), TCMB);
            update_parents();
        }

        float power_usage = 200;

        air_input.merge(remove_input);
        air_output.merge(remove_output);

        if (power_usage != 0)
            await use_energy(power_usage);
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm:83
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AtmosTempPump", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm:89
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["on"] = on;
        data["rate"] = round(heat_transfer_rate);
        data["max_heat_transfer_rate"] = round(max_heat_transfer_rate);
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/temperature_pump.dm:96
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
                rate = max_heat_transfer_rate;
                dot = TRUE;
            }
            else if (text2num(rate) != null)
            {
                rate = text2num(rate);
                dot = TRUE;
            }
            if (Dm.truthy(dot))
            {
                heat_transfer_rate = clamp(Dm.num(rate), 0, max_heat_transfer_rate);
                investigate_log(Dm.text("was set to []% by []", heat_transfer_rate, key_name(usr)), INVESTIGATE_ATMOS);
            }
        }
        update_appearance(UPDATE_ICON);
        return dot;
    }
}
