// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/equipment/tools/air_tank.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///Mech air tank module
// tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:2
public partial class obj_item_mecha_parts_mecha_equipment_air_tank : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        internal_tank = Dm.@new<obj_machinery_portable_atmospherics_canister_mecha>(this);
        internal_tank.air_contents.volume = volume;
        internal_tank.maximum_pressure = maximum_pressure;
        if (Dm.truthy(start_full))
        {
            internal_tank.air_contents.set_temperature(T20C);
            internal_tank.air_contents.set_gas(typeof(datum_gas_oxygen), maximum_pressure * volume / (R_IDEAL_GAS_EQUATION * Dm.num(internal_tank.air_contents.temperature)));
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:41
    public override object Destroy(float? force = null)
    {
        if (chassis != null)
            UnregisterSignal(chassis, COMSIG_MOVABLE_PRE_MOVE);
        STOP_PROCESSING(SSobj, this);
        qdel(internal_tank);
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:48
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        await base.attach(new_mecha, attach_right);
        START_PROCESSING(SSobj, this);
        RegisterSignal(new_mecha, COMSIG_MOVABLE_PRE_MOVE, nameof(disconnect_air));
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:53
    public override async DmTask detach(atom moveto = null)
    {
        disconnect_air();
        if (Dm.truthy(tank_pump_active))
            tank_pump_active = FALSE;
        UnregisterSignal(chassis, COMSIG_MOVABLE_PRE_MOVE);
        STOP_PROCESSING(SSobj, this);
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:60
    public override async DmTask set_active(object active__1 = null)
    {
        await base.set_active(active__1);
        datum_action action = Dm.locate<datum_action_vehicle_sealed_mecha_mech_toggle_cabin_seal>(usr?.actions);
        if (action == null)
            return;
        if (Dm.truthy(active__1))
            action.button_icon_state = Dm.text("mech_cabin_[]", Dm.truthy(chassis.cabin_sealed) ? "pressurized" : "open");
        else
            action.button_icon_state = Dm.text("mech_cabin_[]", Dm.truthy(chassis.cabin_sealed) ? "closed" : "open");
        await action.build_all_button_icons();
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:71
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (chassis == null)
            return null;
        Waiting.Detach(process_cabin_pressure());
        Waiting.Detach(process_pump());
        return null;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:77
    public virtual async DmTask process_cabin_pressure()
    {
        if (!Dm.truthy(chassis.cabin_sealed) || !Dm.truthy(active))
            return;
        datum_gas_mixture external_air = await chassis.loc.return_air() as datum_gas_mixture;
        datum_gas_mixture tank_air = await internal_tank.return_air() as datum_gas_mixture;
        datum_gas_mixture cabin_air = chassis.cabin_air;
        float release_pressure = Dm.num(internal_tank.release_pressure);
        if (Dm.num(cabin_air.return_pressure()) < release_pressure)
            tank_air.release_gas_to(cabin_air, release_pressure);
        if (Dm.truthy(cabin_air.has_gas(typeof(datum_gas_carbon_dioxide))))
            cabin_air.pump_gas_to(external_air, PUMP_MAX_PRESSURE, typeof(datum_gas_carbon_dioxide));
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:89
    public virtual async DmTask process_pump()
    {
        if (!Dm.truthy(tank_pump_active))
            return;
        turf local_turf = get_turf(chassis);
        datum_gas_mixture sending = (Dm.eq(tank_pump_direction, PUMP_IN) ? await local_turf.return_air() : internal_tank.air_contents) as datum_gas_mixture;
        datum_gas_mixture receiving = (Dm.eq(tank_pump_direction, PUMP_IN) ? internal_tank.air_contents : await local_turf.return_air()) as datum_gas_mixture;
        if (Dm.truthy(sending.pump_gas_to(receiving, tank_pump_pressure)))
            air_update_turf(FALSE, FALSE);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:98
    public virtual void disconnect_air()
    {
        if (connected_port != null && Dm.truthy(Waiting.NoSleep(internal_tank.disconnect())))
        {
            to_chat(chassis.occupants, span_warning(I18n.T("Air port connection has been severed!")));
            log_message("Lost connection to gas port.", LOG_MECHA);
        }
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:104
    public override DmList get_snowflake_data()
    {
        datum_gas_mixture tank_air = Waiting.NoSleep(internal_tank.return_air()) as datum_gas_mixture;
        return Dm.list(
            Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_AIR_TANK),
            Dm.kv("auto_pressurize_on_seal", auto_pressurize_on_seal),
            Dm.kv("port_connected", Dm.truthy(internal_tank?.connected_port) ? TRUE : FALSE),
            Dm.kv("tank_release_pressure", round(Dm.num(internal_tank.release_pressure))),
            Dm.kv("tank_release_pressure_min", CAN_MIN_RELEASE_PRESSURE),
            Dm.kv("tank_release_pressure_max", CAN_MAX_RELEASE_PRESSURE),
            Dm.kv("tank_pump_active", tank_pump_active),
            Dm.kv("tank_pump_direction", tank_pump_direction),
            Dm.kv("tank_pump_pressure", round(tank_pump_pressure)),
            Dm.kv("tank_pump_pressure_min", PUMP_MIN_PRESSURE),
            Dm.kv("tank_pump_pressure_max", Math.Min(PUMP_MAX_PRESSURE, Dm.num(internal_tank.maximum_pressure))),
            Dm.kv("tank_air", gas_mixture_parser(tank_air, "tank")),
            Dm.kv("cabin_air", gas_mixture_parser(chassis.cabin_air, "cabin")));
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/air_tank.dm:122
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        string act = action as string;
        switch (act)
        {
            case "set_cabin_pressure":
            {
                object new_pressure = text2num(@params["new_pressure"]);
                internal_tank.release_pressure = Math.Clamp(Dm.num(round(Dm.num(new_pressure))), CAN_MIN_RELEASE_PRESSURE, CAN_MAX_RELEASE_PRESSURE);
                return TRUE;
            }
            case "toggle_port":
            {
                if (Dm.truthy(internal_tank.connected_port))
                {
                    if (Dm.truthy(await internal_tank.disconnect()))
                    {
                        to_chat(chassis.occupants, span_notice(I18n.T("Disconnected from the air system port.")));
                        log_message("Disconnected from gas port.", LOG_MECHA);
                        return TRUE;
                    }
                    to_chat(chassis.occupants, span_warning(I18n.T("Unable to disconnect from the air system port!")));
                    return null;
                }
                obj_machinery_atmospherics_components_unary_portables_connector possible_port = Dm.locate<obj_machinery_atmospherics_components_unary_portables_connector>(chassis.loc);
                if (Dm.truthy(await internal_tank.connect(possible_port)))
                {
                    to_chat(chassis.occupants, span_notice(I18n.T("Connected to the air system port.")));
                    log_message("Connected to gas port.", LOG_MECHA);
                    return TRUE;
                }
                to_chat(chassis.occupants, span_warning(I18n.T("Unable to connect with air system port!")));
                return FALSE;
            }
            case "toggle_auto_pressurize":
                auto_pressurize_on_seal = Dm.truthy(auto_pressurize_on_seal) ? 0 : 1;
                return TRUE;
            case "toggle_tank_pump":
                tank_pump_active = Dm.truthy(tank_pump_active) ? 0 : 1;
                return TRUE;
            case "toggle_tank_pump_direction":
                tank_pump_direction = Dm.truthy(tank_pump_direction) ? 0 : 1;
                return TRUE;
            case "set_tank_pump_pressure":
            {
                object new_pressure = text2num(@params["new_pressure"]);
                tank_pump_pressure = Math.Clamp(Dm.num(round(Dm.num(new_pressure))), PUMP_MIN_PRESSURE, Math.Min(PUMP_MAX_PRESSURE, Dm.num(internal_tank.maximum_pressure)));
                return TRUE;
            }
        }
        return FALSE;
    }
}
