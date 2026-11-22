// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/embedded_controller/airlock_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the engineering package E8, D287): the incinerators' airlock consoles. The console links its doors, dual-port
// vent and airlock sensor by their id tags (GLOB.objects_by_id_tag) and cycles the chamber on SSmachines: the AirlockController
// window's cycle, close and abort buttons and the sensor's button set the target state. The var blocks are generated.

// tg: code/game/machinery/embedded_controller/airlock_controller.dm:8
public partial class obj_machinery_airlock_controller : obj_machinery
{
    //States for airlock_control
    // tg: code/game/machinery/embedded_controller/airlock_controller.dm:2-6  the file-local #defines (#undef at :312-316)
    private const string AIRLOCK_STATE_INOPEN = "inopen";
    private const string AIRLOCK_STATE_PRESSURIZE = "pressurize";
    private const string AIRLOCK_STATE_CLOSED = "closed";
    private const string AIRLOCK_STATE_DEPRESSURIZE = "depressurize";
    private const string AIRLOCK_STATE_OUTOPEN = "outopen";

    // tg: code/game/machinery/embedded_controller/airlock_controller.dm:37
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        // the locals are object (rules 3.4): a mapper's tag may name something else, which tg reports and still references
        object interior_door = GLOB.objects_by_id_tag[interior_door_tag];
        if (interior_door_tag != null && !istype(interior_door, typeof(obj_machinery_door)))
            stack_trace(Dm.text("interior_door_tag is set to [], which is not a door ([])", interior_door_tag, Dm.truthy(interior_door) ? interior_door : "null"), "code/game/machinery/embedded_controller/airlock_controller.dm", 42);
        interior_door_ref = WEAKREF(interior_door as datum) as datum_weakref;

        object exterior_door = GLOB.objects_by_id_tag[exterior_door_tag];
        if (exterior_door_tag != null && !istype(exterior_door, typeof(obj_machinery_door)))
            stack_trace(Dm.text("exterior_door_tag is set to [], which is not a door ([])", exterior_door_tag, Dm.truthy(exterior_door) ? exterior_door : "null"), "code/game/machinery/embedded_controller/airlock_controller.dm", 47);
        exterior_door_ref = WEAKREF(exterior_door as datum) as datum_weakref;

        object pump = GLOB.objects_by_id_tag[airpump_tag];
        if (airpump_tag != null && !istype(pump, typeof(obj_machinery_atmospherics_components_binary_dp_vent_pump)))
            stack_trace(Dm.text("airpump_tag is set to [], which is not a pump ([])", airpump_tag, Dm.truthy(pump) ? pump : "null"), "code/game/machinery/embedded_controller/airlock_controller.dm", 52);
        pump_ref = WEAKREF(pump as datum) as datum_weakref;

        object sensor = GLOB.objects_by_id_tag[sensor_tag];
        if (sensor_tag != null && !istype(sensor, typeof(obj_machinery_airlock_sensor)))
            stack_trace(Dm.text("sensor_tag is set to [], which is not a sensor ([])", sensor_tag, Dm.truthy(sensor) ? sensor : "null"), "code/game/machinery/embedded_controller/airlock_controller.dm", 57);
        sensor_ref = WEAKREF(sensor as datum) as datum_weakref;
    }

    // tg: code/game/machinery/embedded_controller/airlock_controller.dm:60
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AirlockController", this);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/embedded_controller/airlock_controller.dm:66
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48): the doors' secure_open and secure_close sleep
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        bool process_again = true;
        while (process_again)
        {
            process_again = false;
            switch (state)
            {
                case AIRLOCK_STATE_INOPEN:
                    if (target_state != state)
                    {
                        var interior_airlock = interior_door_ref.resolve() as obj_machinery_door_airlock;
                        if (interior_airlock == null)
                            continue;

                        if (Dm.truthy(interior_airlock.density))
                        {
                            state = AIRLOCK_STATE_CLOSED;
                            process_again = true;
                        }
                        else
                            await interior_airlock.secure_close();
                    }
                    else
                    {
                        var pump = pump_ref.resolve() as obj_machinery_atmospherics_components_binary_dp_vent_pump;

                        if (Dm.truthy(pump?.on))
                        {
                            pump.on = FALSE;
                            pump.update_appearance(UPDATE_ICON);
                        }
                    }
                    break;

                case AIRLOCK_STATE_PRESSURIZE:
                    if (target_state == AIRLOCK_STATE_INOPEN)
                    {
                        object sensor_pressure = await this.sensor_pressure();
                        if (sensor_pressure == null)
                            continue;

                        if (Dm.num(sensor_pressure) >= ONE_ATMOSPHERE * 0.95f)
                        {
                            var interior_airlock = interior_door_ref.resolve() as obj_machinery_door_airlock;
                            if (interior_airlock == null)
                                continue;

                            if (Dm.truthy(interior_airlock.density))
                                await interior_airlock.secure_open();
                            else
                            {
                                state = AIRLOCK_STATE_INOPEN;
                                process_again = true;
                            }
                        }
                        else
                        {
                            var pump = pump_ref.resolve() as obj_machinery_atmospherics_components_binary_dp_vent_pump;
                            if (pump == null)
                                continue;

                            if (pump.pump_direction == ATMOS_DIRECTION_SIPHONING)
                            {
                                Dm.combine(ref pump.pressure_checks, ATMOS_EXTERNAL_BOUND);
                                pump.pump_direction = ATMOS_DIRECTION_RELEASING;
                            }
                            else if (!Dm.truthy(pump.on))
                            {
                                pump.on = TRUE;
                                pump.update_appearance(UPDATE_ICON);
                            }
                        }
                    }
                    else
                    {
                        state = AIRLOCK_STATE_CLOSED;
                        process_again = true;
                    }
                    break;

                case AIRLOCK_STATE_CLOSED:
                    if (target_state == AIRLOCK_STATE_OUTOPEN)
                    {
                        var interior_airlock = interior_door_ref.resolve() as obj_machinery_door_airlock;
                        if (interior_airlock == null)
                            continue;

                        if (Dm.truthy(interior_airlock.density))
                        {
                            state = AIRLOCK_STATE_DEPRESSURIZE;
                            process_again = true;
                        }
                        else
                            await interior_airlock.secure_close();
                    }
                    else if (target_state == AIRLOCK_STATE_INOPEN)
                    {
                        var exterior_airlock = exterior_door_ref.resolve() as obj_machinery_door_airlock;
                        if (exterior_airlock == null)
                            continue;

                        if (Dm.truthy(exterior_airlock.density))
                        {
                            state = AIRLOCK_STATE_PRESSURIZE;
                            process_again = true;
                        }
                        else
                            await exterior_airlock.secure_close();
                    }
                    else
                    {
                        var pump = pump_ref.resolve() as obj_machinery_atmospherics_components_binary_dp_vent_pump;
                        if (pump == null)
                            continue;

                        if (!Dm.truthy(pump.on))
                        {
                            pump.on = TRUE;
                            pump.update_appearance(UPDATE_ICON);
                        }
                    }
                    break;

                case AIRLOCK_STATE_DEPRESSURIZE:
                {
                    float target_pressure = ONE_ATMOSPHERE * 0.05f;
                    if (Dm.truthy(sanitize_external))
                        target_pressure = ONE_ATMOSPHERE * 0.01f;

                    object sensor_pressure = await this.sensor_pressure();
                    if (sensor_pressure == null)
                        continue;

                    if (Dm.num(sensor_pressure) <= target_pressure)
                    {
                        if (target_state == AIRLOCK_STATE_OUTOPEN)
                        {
                            var exterior_airlock = exterior_door_ref.resolve() as obj_machinery_door_airlock;
                            if (exterior_airlock == null)
                                continue;

                            if (Dm.truthy(exterior_airlock.density))
                                await exterior_airlock.secure_open();
                            else
                                state = AIRLOCK_STATE_OUTOPEN;
                        }
                        else
                        {
                            state = AIRLOCK_STATE_CLOSED;
                            process_again = true;
                        }
                    }
                    else if ((target_state != AIRLOCK_STATE_OUTOPEN) && !Dm.truthy(sanitize_external))
                    {
                        state = AIRLOCK_STATE_CLOSED;
                        process_again = true;
                    }
                    else
                    {
                        var pump = pump_ref.resolve() as obj_machinery_atmospherics_components_binary_dp_vent_pump;
                        if (pump == null)
                            continue;

                        if (pump.pump_direction == ATMOS_DIRECTION_RELEASING)
                        {
                            Dm.mask(ref pump.pressure_checks, Dm.bitnot(ATMOS_EXTERNAL_BOUND));
                            pump.pump_direction = ATMOS_DIRECTION_SIPHONING;
                        }
                        else if (!Dm.truthy(pump.on))
                        {
                            pump.on = TRUE;
                            pump.update_appearance(UPDATE_ICON);
                        }
                    }
                    break;
                }

                case AIRLOCK_STATE_OUTOPEN: //state 2
                    if (target_state != AIRLOCK_STATE_OUTOPEN)
                    {
                        var exterior_airlock = exterior_door_ref.resolve() as obj_machinery_door_airlock;
                        if (exterior_airlock == null)
                            continue;

                        if (Dm.truthy(exterior_airlock.density))
                        {
                            if (Dm.truthy(sanitize_external))
                            {
                                state = AIRLOCK_STATE_DEPRESSURIZE;
                                process_again = true;
                            }
                            else
                            {
                                state = AIRLOCK_STATE_CLOSED;
                                process_again = true;
                            }
                        }
                        else
                            await exterior_airlock.secure_close();
                    }
                    else
                    {
                        var pump = pump_ref.resolve() as obj_machinery_atmospherics_components_binary_dp_vent_pump;
                        if (pump == null)
                            continue;

                        if (Dm.truthy(pump.on))
                        {
                            pump.on = FALSE;
                            pump.update_appearance(UPDATE_ICON);
                        }
                    }
                    break;
            }
        }

        processing = Dm.num(state != target_state);

        update_appearance();
        SStgui.update_uis(this);
        return null;
    }

    // tg: code/game/machinery/embedded_controller/airlock_controller.dm:216
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["airlockState"] = state;

        object sensor_pressure = await this.sensor_pressure();
        data["sensorPressure"] = sensor_pressure == null ? "----" : round(Dm.num(sensor_pressure), 0.1f);

        var interior_airlock = interior_door_ref.resolve() as obj_machinery_door_airlock;
        if (interior_airlock == null)
            data["interiorStatus"] = "----";
        else
            data["interiorStatus"] = Dm.truthy(interior_airlock.density) ? "closed" : "open";

        var exterior_airlock = exterior_door_ref.resolve() as obj_machinery_door_airlock;
        if (exterior_airlock == null)
            data["exteriorStatus"] = "----";
        else
            data["exteriorStatus"] = Dm.truthy(exterior_airlock.density) ? "closed" : "open";

        var pump = pump_ref.resolve() as obj_machinery_atmospherics_components_binary_dp_vent_pump;
        float? sw__1 = pump?.pump_direction;
        if (sw__1 == null)
            data["pumpStatus"] = "----";
        else if (sw__1 == ATMOS_DIRECTION_RELEASING)
            data["pumpStatus"] = "release";
        else if (sw__1 == ATMOS_DIRECTION_SIPHONING)
            data["pumpStatus"] = "siphon";

        return data;
    }

    // tg: code/game/machinery/embedded_controller/airlock_controller.dm:247
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "cycleClosed"))
            target_state = AIRLOCK_STATE_CLOSED;
        else if (Dm.eq(action, "cycleExterior"))
            target_state = AIRLOCK_STATE_OUTOPEN;
        else if (Dm.eq(action, "cycleInterior"))
            target_state = AIRLOCK_STATE_INOPEN;
        else if (Dm.eq(action, "abort"))
            target_state = AIRLOCK_STATE_CLOSED;

        return TRUE;
    }

    /// Starts an airlock cycle
    // tg: code/game/machinery/embedded_controller/airlock_controller.dm:265
    public virtual void cycle()
    {
        if (state == AIRLOCK_STATE_INOPEN || state == AIRLOCK_STATE_PRESSURIZE)
            target_state = AIRLOCK_STATE_OUTOPEN;
        else
            target_state = AIRLOCK_STATE_INOPEN;
    }

    /// Returns the pressure over the pump, or null if it is deleted
    // tg: code/game/machinery/embedded_controller/airlock_controller.dm:272
    public virtual async DmTask<object> sensor_pressure()
    {
        var sensor = sensor_ref.resolve() as obj_machinery_airlock_sensor;
        if (sensor != null && !Dm.truthy(sensor.on))
            return last_pressure;

        var air = (sensor == null ? null : await sensor.return_air()) as datum_gas_mixture;
        last_pressure = air?.return_pressure();
        return last_pressure;
    }

    // tg: code/game/machinery/embedded_controller/airlock_controller.dm:308
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[]", base_icon_state, Dm.truthy(processing) ? "process" : "standby");
        return await base.update_icon_state(__extra);
    }
}
