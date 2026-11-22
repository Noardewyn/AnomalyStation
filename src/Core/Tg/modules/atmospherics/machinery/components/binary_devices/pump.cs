// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/binary_devices/pump.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the pressure pump): the init, the ctrl and alt clicks (on/off, maximum pressure), the picture,
// process_atmos and can_unwrench. Deferred leaves: the circuit USB port (circuits), the screentip context (M9) and the
// investigate logs (M10). Generated live-partial stub: add_context (M9). The M7 supermatter slice, part 2: the tgui window (ui_interact, ui_data, ui_act). /obj/item/circuit_component/atmos_pump stays inert (circuits). The var block is generated.

// Every cycle, the pump uses the air in air_in to try and make air_out the perfect pressure.
//
// node1, air1, network1 correspond to input
// node2, air2, network2 correspond to output
//
// Thus, the two variables affect pump operation are set in New():
//   air1.volume
//     This is the volume of gas available to the pump that may be transferred to the output
//   air2.volume
//     Higher quantities of this cause more air to be perfected later
//     but overall network volume is also increased as this increases...

// tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:13
public partial class obj_machinery_atmospherics_components_binary_pump : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:26
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddComponent(/datum/component/usb_port, typecacheof(list(/obj/item/circuit_component/atmos_pump), only_root_path = TRUE)): circuits
        Dm.Deferred("code/modules/atmospherics/machinery/components/binary_devices/pump.dm:29", "Initialize: AddComponent(/datum/component/usb_port): the circuit USB port (circuits)");
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context of the held tool
        Dm.Deferred("code/modules/atmospherics/machinery/components/binary_devices/pump.dm:30", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:37
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

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:45
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (target_pressure == MAX_OUTPUT_PRESSURE)
            return CLICK_ACTION_BLOCKING;

        target_pressure = MAX_OUTPUT_PRESSURE;
        investigate_log(Dm.text("was set to [] kPa by []", target_pressure, key_name(user)), INVESTIGATE_ATMOS);
        balloon_alert(user, Dm.text("pressure output set to [] kPa", target_pressure));
        update_appearance(UPDATE_ICON);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:55
    public override void update_icon_nopipes(params object[] __extra)
    {
        icon_state = (Dm.truthy(on) && Dm.truthy(is_operational)) ? Dm.text("pump_on-[]", set_overlay_offset(piping_layer)) : Dm.text("pump_off-[]", set_overlay_offset(piping_layer));
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:58
    public override object process_atmos(object seconds_per_tick = null)
    {
        if (!Dm.truthy(on) || !Dm.truthy(is_operational))
            return null;

        datum_gas_mixture input_air = airs[1] as datum_gas_mixture;
        datum_gas_mixture output_air = airs[2] as datum_gas_mixture;
        datum_gas_mixture output_pipenet_air = (parents[2] as datum_pipeline).air;

        if (Dm.truthy(input_air.pump_gas_to(output_air, target_pressure, output_pipenet_air: output_pipenet_air)))
            update_parents();
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:69  (the M7 supermatter slice, part 2: the engine loop's pumps)
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

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:75
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["on"] = on;
        data["pressure"] = round(Dm.num(target_pressure));
        data["max_pressure"] = round(MAX_OUTPUT_PRESSURE);
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:82
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
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:104
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
