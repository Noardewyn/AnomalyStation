// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/portable/pump.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the portable air pumps): the pictures, process_atmos (the pump between its air and the room or
// a held tank), unregister_holding, the EMP (M7) and the Lil' Pump's init. Generated live-partial stubs: on_deconstruction (M5) and
// replace_tank (the hand-held tanks, M4a). Package E3 (docs/ai/specs/engineering.md): the tgui window (ui_interact, ui_data, ui_act;
// the pump is switched on there); its investigate logs and admin messages are deferred leaves (admin tooling, M10). The var blocks are generated.

// tg: code/modules/atmospherics/machinery/portable/pump.dm:1
public partial class obj_machinery_portable_atmospherics_pump : obj_machinery_portable_atmospherics
{
    // tg: code/modules/atmospherics/machinery/portable/pump.dm:20
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[]", Dm.initial(this, (atom p) => p.icon_state), on);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/portable/pump.dm:24
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (holding != null)
            Dm.append(ref dot, "siphon-open");
        if (connected_port != null)
            Dm.append(ref dot, "siphon-connector");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/pump.dm:31
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(seconds_per_tick, dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(object seconds_per_tick, DotBox<object> dot)
    {
        if (Dm.truthy(await take_atmos_damage()))
        {
            excited = TRUE;
            return base.process_atmos(seconds_per_tick);
        }
        if (!Dm.truthy(on))
            return base.process_atmos(seconds_per_tick);

        excited = TRUE;

        turf local_turf = get_turf(this);

        datum_gas_mixture sending;
        datum_gas_mixture receiving;

        if (holding != null) //Work with tank when inserted, otherwise - with area
        {
            sending = (Dm.eq(direction, PUMP_IN) ? await holding.return_air() : air_contents) as datum_gas_mixture;
            receiving = (Dm.eq(direction, PUMP_IN) ? air_contents : await holding.return_air()) as datum_gas_mixture;
        }
        else
        {
            sending = (Dm.eq(direction, PUMP_IN) ? await local_turf.return_air() : air_contents) as datum_gas_mixture;
            receiving = (Dm.eq(direction, PUMP_IN) ? air_contents : await local_turf.return_air()) as datum_gas_mixture;
        }

        if (Dm.truthy(sending.pump_gas_to(receiving, target_pressure)) && holding == null)
            air_update_turf(FALSE, FALSE); // Update the environment if needed.

        return base.process_atmos(seconds_per_tick);
    }

    // tg: code/modules/atmospherics/machinery/portable/pump.dm:170
    public override void unregister_holding()
    {
        on = FALSE;
        base.unregister_holding();
    }

    // tg: code/modules/atmospherics/machinery/portable/pump.dm:58
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (!Dm.truthy(is_operational))
            return dot;
        if (prob(50 / Dm.num(severity)))
        {
            on = !Dm.truthy(on) ? TRUE : FALSE;
            if (Dm.truthy(on))
                SSair.start_processing_machine(this);
        }
        if (prob(100 / Dm.num(severity)))
            direction = PUMP_OUT;
        target_pressure = rand(0, 100 * ONE_ATMOSPHERE);
        update_appearance();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/pump.dm:73
    public override async DmTask<object> replace_tank(mob_living user = null, object close_valve = null, obj_item_tank new_tank = null)
    {
        object dot = await base.replace_tank(user, close_valve, new_tank);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(close_valve))
        {
            if (Dm.truthy(on))
            {
                on = FALSE;
                update_appearance();
            }
        }
        else if (Dm.truthy(on) && holding != null && Dm.eq(direction, PUMP_OUT))
            user.investigate_log(Dm.text("started a transfer into [].", holding), INVESTIGATE_ATMOS);
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/portable/pump.dm:174
public partial class obj_machinery_portable_atmospherics_pump_lil_pump : obj_machinery_portable_atmospherics_pump
{
    // tg: code/modules/atmospherics/machinery/portable/pump.dm:177
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //25% chance to occur
        if (prob(25))
        {
            name = "Liler' Pump";
            desc = "When a Lil' Pump and a portable air pump love each other very much.";
            matrix lil_pump = Dm.matrix();
            lil_pump.Scale(0.8f);
            this.transform = lil_pump;
        }
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/portable/pump.dm:1
public partial class obj_machinery_portable_atmospherics_pump : obj_machinery_portable_atmospherics
{
    // tg: code/modules/atmospherics/machinery/portable/pump.dm:15  (M5 slice 7)
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        turf local_turf = get_turf(this) as turf;
        await local_turf.assume_air(air_contents);
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/atmospherics/machinery/portable/pump.dm:84
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "PortablePump", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/portable/pump.dm:90
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list(
            Dm.kv("on", on),
            Dm.kv("direction", direction),
            Dm.kv("connected", Dm.truthy(connected_port) ? TRUE : FALSE),
            Dm.kv("pressure", round(Dm.num(air_contents.return_pressure()))),
            Dm.kv("targetPressure", round(target_pressure)),
            Dm.kv("defaultPressure", round(PUMP_DEFAULT_PRESSURE)),
            Dm.kv("minPressure", round(PUMP_MIN_PRESSURE)),
            Dm.kv("maxPressure", round(PUMP_MAX_PRESSURE)),
            Dm.kv("hasHypernobCrystal", Dm.truthy(nob_crystal_inserted) ? TRUE : FALSE),
            Dm.kv("reactionSuppressionEnabled", Dm.truthy(suppress_reactions) ? TRUE : FALSE)
        );

        if (holding != null)
        {
            datum_gas_mixture holding_mix = await holding.return_air() as datum_gas_mixture;
            data["holding"] = Dm.list(
                Dm.kv("name", holding.name),
                Dm.kv("pressure", round(Dm.num(holding_mix.return_pressure())))
            );
        }
        else
            data["holding"] = null;
        return data;
    }

    // tg: code/modules/atmospherics/machinery/portable/pump.dm:112
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "power":
            {
                on = Dm.truthy(on) ? FALSE : TRUE;
                if (Dm.truthy(on))
                    SSair.start_processing_machine(this);
                if (Dm.truthy(on) && holding == null)
                {
                    float plasma_moles = Dm.num(air_contents.moles[typeof(datum_gas_plasma)]);
                    float n2o_moles = Dm.num(air_contents.moles[typeof(datum_gas_nitrous_oxide)]);
                    if (n2o_moles != 0 || plasma_moles != 0)
                    {
                        message_admins(Dm.text("[] turned on a pump that contains [][][] at []", ADMIN_LOOKUPFLW(usr), n2o_moles != 0 ? "N2O" : "", n2o_moles != 0 && plasma_moles != 0 ? " & " : "", plasma_moles != 0 ? "Plasma" : "", ADMIN_VERBOSEJMP(this)));
                        log_admin(Dm.text("[] turned on a pump that contains [][][] at []", key_name(usr), n2o_moles != 0 ? "N2O" : "", n2o_moles != 0 && plasma_moles != 0 ? " & " : "", plasma_moles != 0 ? "Plasma" : "", AREACOORD(this)));
                    }
                }
                else if (Dm.truthy(on) && Dm.eq(direction, PUMP_OUT))
                {
                    usr.investigate_log(Dm.text("started a transfer into [].", holding), INVESTIGATE_ATMOS);
                }
                dot = TRUE;
                break;
            }

            case "direction":
                if (Dm.eq(direction, PUMP_OUT))
                    direction = PUMP_IN;
                else
                {
                    if (Dm.truthy(on) && holding != null)
                    {
                        usr.investigate_log(Dm.text("started a transfer into [].", holding), INVESTIGATE_ATMOS);
                    }
                    direction = PUMP_OUT;
                }
                dot = TRUE;
                break;

            case "pressure":
            {
                object pressure = @params["pressure"];
                if (Dm.eq(pressure, "reset"))
                {
                    pressure = PUMP_DEFAULT_PRESSURE;
                    dot = TRUE;
                }
                else if (Dm.eq(pressure, "min"))
                {
                    pressure = PUMP_MIN_PRESSURE;
                    dot = TRUE;
                }
                else if (Dm.eq(pressure, "max"))
                {
                    pressure = PUMP_MAX_PRESSURE;
                    dot = TRUE;
                }
                else if (text2num(pressure) != null)
                {
                    pressure = text2num(pressure);
                    dot = TRUE;
                }
                if (Dm.truthy(dot))
                {
                    target_pressure = Dm.clamp(Dm.num(pressure), PUMP_MIN_PRESSURE, PUMP_MAX_PRESSURE);
                    investigate_log(Dm.text("was set to [] kPa by [].", target_pressure, key_name(usr)), INVESTIGATE_ATMOS);
                }
                break;
            }

            case "eject":
                if (holding != null)
                {
                    await replace_tank(World.Current.usr as mob_living, FALSE);
                    dot = TRUE;
                }
                break;

            case "reaction_suppression":
                if (!Dm.truthy(nob_crystal_inserted))
                {
                    stack_trace(Dm.text("[] tried to toggle reaction suppression on a pump without a noblium crystal inside, possible href exploit attempt.", World.Current.usr), "code/modules/atmospherics/machinery/portable/pump.dm", 161);
                    return dot;
                }
                suppress_reactions = Dm.truthy(suppress_reactions) ? FALSE : TRUE;
                SSair.start_processing_machine(this);
                message_admins(Dm.text("[] turned [] the [] reaction suppression.", ADMIN_LOOKUPFLW(usr), Dm.truthy(suppress_reactions) ? "on" : "off", this));
                usr.investigate_log(Dm.text("turned [] the [] reaction suppression.", Dm.truthy(suppress_reactions) ? "on" : "off", this), INVESTIGATE_ATMOS);
                dot = TRUE;
                break;
        }

        update_appearance();
        return dot;
    }
}
