// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice D172 (the pressure gate, which the rapid pipe dispenser dispenses): the whole file except the screentip context (M9),
//  and the investigate logs (M10), which are deferred leaves (generated live-partial stubs:
// add_context; the ventcrawling relaymove since gaps X5). The window is tg's AtmosPump interface. The var block is generated.

// tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:1
public partial class obj_machinery_atmospherics_components_binary_pressure_valve : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:17
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context of the held tool
        Dm.Deferred("code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:19", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:27
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

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:35
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (target_pressure == MAX_OUTPUT_PRESSURE)
            return CLICK_ACTION_BLOCKING;

        target_pressure = MAX_OUTPUT_PRESSURE;
        investigate_log(Dm.text("was set to [] kPa by []", target_pressure, key_name(user)), INVESTIGATE_ATMOS);
        balloon_alert(user, Dm.text("target pressure set to [] kPa", target_pressure));
        update_appearance(UPDATE_ICON);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:45
    public override void update_icon_nopipes(params object[] __extra)
    {
        string new_icon_state;
        if (Dm.truthy(on) && Dm.truthy(is_operational) && Dm.truthy(is_gas_flowing))
            new_icon_state = Dm.text("pvalve_flow-[]", set_overlay_offset(piping_layer));
        else if (Dm.truthy(on) && Dm.truthy(is_operational) && !Dm.truthy(is_gas_flowing))
            new_icon_state = Dm.text("pvalve_on-[]", set_overlay_offset(piping_layer));
        else
            new_icon_state = Dm.text("pvalve_off-[]", set_overlay_offset(piping_layer));

        string old_icon_state = icon_state;
        icon_state = new_icon_state;
        if (new_icon_state != old_icon_state)
            update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:59
    public override object process_atmos(object seconds_per_tick = null)
    {
        if (!Dm.truthy(on) || !Dm.truthy(is_operational))
            return null;

        datum_gas_mixture input_air = airs[1] as datum_gas_mixture;
        datum_gas_mixture output_air = airs[2] as datum_gas_mixture;
        datum_gas_mixture output_pipenet_air = (parents[2] as datum_pipeline).air;

        if (Dm.num(input_air.return_pressure()) > target_pressure)
        {
            if (Dm.truthy(input_air.release_gas_to(output_air, input_air.return_pressure(), output_pipenet_air: output_pipenet_air)))
            {
                update_parents();
                is_gas_flowing = TRUE;
            }
        }
        else
            is_gas_flowing = FALSE;
        update_icon_nopipes();
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:81
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

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:87
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["on"] = on;
        data["pressure"] = round(Dm.num(target_pressure));
        data["max_pressure"] = round(ONE_ATMOSPHERE * 100);
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:94
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "power"))
        {
            on = Dm.truthy(on) ? FALSE : TRUE;
            investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(usr)), INVESTIGATE_ATMOS);
            dot = TRUE;
        }
        else if (Dm.eq(action, "pressure"))
        {
            object pressure = @params["pressure"];
            if (Dm.eq(pressure, "max"))
            {
                pressure = ONE_ATMOSPHERE * 100;
                dot = TRUE;
            }
            else if (text2num(pressure) != null)
            {
                pressure = text2num(pressure);
                dot = TRUE;
            }
            if (Dm.truthy(dot))
            {
                target_pressure = clamp(Dm.num(pressure), 0, ONE_ATMOSPHERE * 100);
                investigate_log(Dm.text("was set to [] kPa by []", target_pressure, key_name(usr)), INVESTIGATE_ATMOS);
            }
        }
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:116
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

// tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:76  (gaps X5: ventcrawling)
public partial class obj_machinery_atmospherics_components_binary_pressure_valve : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/pressure_valve.dm:76
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (!Dm.truthy(on) || !Dm.eq(direction, dir))
            return null;
        return await base.relaymove(user, direction);
    }
}
