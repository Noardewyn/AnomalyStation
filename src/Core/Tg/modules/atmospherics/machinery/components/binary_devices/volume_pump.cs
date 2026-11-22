// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the volume pump): the init, the ctrl and alt clicks, the picture, process_atmos and can_unwrench.
// Deferred leaves: the circuit USB port (circuits), the screentip context (M9) and the investigate logs (M10). Generated
// live-partial stubs: add_context (M9) and the multitool
// overclock (multitool_act, M4a). /obj/item/circuit_component/atmos_volume_pump stays inert (circuits). The var block is
// generated.

// Every cycle, the pump uses the air in air_in to try and move a specific volume of gas into air_out.
//
// node1, air1, network1 corresponds to input
// node2, air2, network2 corresponds to output
//
// Thus, the two variables affect pump operation are set in New():
//   air1.volume
//     This is the volume of gas available to the pump that may be transferred to the output
//   air2.volume
//     Higher quantities of this cause more air to be perfected later
//     but overall network volume is also increased as this increases...

// tg: code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:13
public partial class obj_machinery_atmospherics_components_binary_volume_pump : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:30
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddComponent(/datum/component/usb_port, typecacheof(list(/obj/item/circuit_component/atmos_volume_pump), only_root_path = TRUE)): circuits
        Dm.Deferred("code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:34", "Initialize: AddComponent(/datum/component/usb_port): the circuit USB port (circuits)");
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context of the held tool
        Dm.Deferred("code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:39", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:39
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        if (Dm.truthy(await can_interact(user)))
        {
            set_on(Dm.truthy(on) ? FALSE : TRUE);
            balloon_alert(user, Dm.text("turned []", Dm.truthy(on) ? "on" : "off"));
            investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(user)), INVESTIGATE_ATMOS);
            return CLICK_ACTION_SUCCESS;
        }
        return CLICK_ACTION_BLOCKING;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:47
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (transfer_rate == MAX_TRANSFER_RATE)
            return CLICK_ACTION_BLOCKING;

        transfer_rate = MAX_TRANSFER_RATE;
        investigate_log(Dm.text("was set to [] L/s by []", transfer_rate, key_name(user)), INVESTIGATE_ATMOS);
        balloon_alert(user, Dm.text("volume output set to [] L/s", transfer_rate));
        update_appearance(UPDATE_ICON);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:57
    public override void update_icon_nopipes(params object[] __extra)
    {
        icon_state = Dm.truthy(on) && Dm.truthy(is_operational) ? Dm.text("volpump_on-[]", set_overlay_offset(piping_layer)) : Dm.text("volpump_off-[]", set_overlay_offset(piping_layer));
        float altlayeroverlay = FALSE;
        if (Dm.eq(set_overlay_offset(piping_layer), 2))
            altlayeroverlay = TRUE;
        overclock_overlay = mutable_appearance(Dm.resource("icons/obj/machines/atmospherics/binary_devices.dmi"), Dm.text("vpumpoverclock[]", Dm.truthy(altlayeroverlay) ? "2" : "")) as mutable_appearance;
        if (Dm.truthy(overclocked) && Dm.truthy(on) && Dm.truthy(is_operational))
            add_overlay(overclock_overlay);
        else
            cut_overlay(overclock_overlay);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:68
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        if (!Dm.truthy(on) || !Dm.truthy(is_operational))
            return dot.Value;

        datum_gas_mixture air1 = airs[1] as datum_gas_mixture;
        datum_gas_mixture air2 = airs[2] as datum_gas_mixture;

        // Pump mechanism just won't do anything if the pressure is too high/too low unless you overclock it.

        float input_starting_pressure = Dm.num(air1.return_pressure());
        float output_starting_pressure = Dm.num(air2.return_pressure());

        // Requires being able to leak air in order to overclock.
        if (Dm.truthy(overclocked))
        {
            turf turf = loc as turf;
            if (isclosedturf(turf))
            {
                balloon_alert_to_viewers("jammed!");
                overclocked = FALSE;
                update_appearance(UPDATE_ICON);
            }
        }

        if ((input_starting_pressure < VOLUME_PUMP_MINIMUM_OUTPUT_PRESSURE) || ((output_starting_pressure > VOLUME_PUMP_MAX_OUTPUT_PRESSURE)) && !Dm.truthy(overclocked))
            return dot.Value;

        float transfer_ratio = transfer_rate / Dm.num(air1.volume);

        datum_gas_mixture removed = air1.remove_ratio(transfer_ratio) as datum_gas_mixture;

        if (!Dm.truthy(removed.total_moles()))
            return dot.Value;

        if (Dm.truthy(overclocked)) //Some of the gas from the mixture leaks to the environment when overclocked
        {
            turf_open T = loc as turf_open;
            if (istype(T, typeof(turf_open)))
            {
                datum_gas_mixture leaked = removed.remove_ratio(VOLUME_PUMP_LEAK_AMOUNT) as datum_gas_mixture;
                await T.assume_air(leaked);
            }
        }

        air2.merge(removed);

        update_parents();
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:108
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Its pressure limits could be []abled with a <b>multitool</b>.", Dm.truthy(overclocked) ? I18n.T("en") : I18n.T("dis"))));
        if (Dm.truthy(overclocked))
            Dm.append(ref dot, I18n.T("Its warning light is on[]", Dm.truthy(on) ? I18n.T(" and it's spewing gas!") : "."));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:122  (the M7 supermatter slice, part 2: the window the pumps share)
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

    // tg: code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:128
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["on"] = on;
        data["rate"] = round(Dm.num(transfer_rate));
        data["max_rate"] = round(MAX_TRANSFER_RATE);
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:135
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
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/volume_pump.dm:157
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
