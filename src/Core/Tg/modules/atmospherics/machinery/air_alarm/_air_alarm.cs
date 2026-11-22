// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the air alarms): the init (the thresholds, the area, the filtering mode, the turf's exposure), the
// warning loop (process), Destroy, the environment check (check_danger: the danger level and the area's atmos alarm),
// select_mode, the area relationship, update_name, the pictures and the light, the map helpers' procs (unlock, the
// accesses, the cold room, kitchen and no-check thresholds) and the air sensor link (its multitool_act: package E5). Deferred leaves: the wires
// (/datum/wires/airalarm: hacking, M7), the circuit USB port (circuits), the screentips (M9), the spoken warning (say:
// speech, M9), the emissives (DEVIATION DV12) and the mapping log lines (AREACOORD, M10). Generated live-partial stubs:
// the map export (M10), add_context (M9) and air_alarm_interact.dm's emag proc (M10+); its tool and
// construction procs are translated in air_alarm_interact.cs (M5 slice 7). The tgui window (ui_status, ui_interact,
// ui_static_data, ui_data, ui_act; tg's AirAlarm interface, godot/scripts/Shared/Tgui/Interfaces/AirAlarm.cs) and the
// interface lock are translated (the air alarm window, 2026-10-16). The var block is generated.

// tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:1  #define AIRALARM_WARNING_COOLDOWN (10 SECONDS) (file-local, #undef at :765)

// tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:3
public partial class obj_machinery_airalarm : obj_machinery
{
    private const float AIRALARM_WARNING_COOLDOWN = 10 * SECONDS;

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:41  var/static/list/atmos_connections (rules 10.6: a TgState field)
    public static ref DmList atmos_connections => ref TgState.Current.obj_machinery_airalarm__atmos_connections;

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:93
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // set_wires(new /datum/wires/airalarm(src)): the air alarm's wires (hacking, M7)
        set_wires(Dm.@new<datum_wires_airalarm>(this));

        if (!Dm.truthy(mapload))
        {
            buildstage = AIR_ALARM_BUILD_NO_CIRCUIT;
            set_panel_open(TRUE);
        }

        if (Dm.eq(name, Dm.initial(this, (atom p) => p.name)))
            name = Dm.text("[] Air Alarm", get_area_name(this));

        tlv_collection = Dm.list();
        tlv_collection["pressure"] = Dm.@new<datum_tlv_pressure>();
        tlv_collection["temperature"] = Dm.@new<datum_tlv_temperature>();

        DmList cached_gas_info = GLOB.meta_gas_info;
        foreach (Type gas_path in Dm.each_cast<Type>(cached_gas_info[META_GAS_ID]))
        {
            if (ispath(gas_path, typeof(datum_gas_oxygen)))
                tlv_collection[gas_path] = Dm.@new<datum_tlv_oxygen>();
            else if (ispath(gas_path, typeof(datum_gas_carbon_dioxide)))
                tlv_collection[gas_path] = Dm.@new<datum_tlv_carbon_dioxide>();
            else if (Dm.truthy(((DmList)cached_gas_info[META_GAS_DANGER])[gas_path]))
                tlv_collection[gas_path] = Dm.@new<datum_tlv_dangerous>();
            else
                tlv_collection[gas_path] = Dm.@new<datum_tlv_no_checks>();
        }

        my_area = connected_sensor != null ? get_area(connected_sensor) as area : get_area(this) as area;
        alarm_manager = Dm.@new<datum_alarm_handler>(this);
        select_mode(this, typeof(datum_air_alarm_mode_filtering), should_apply: FALSE);

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), atmos_connections));
        // AddComponent(/datum/component/usb_port, typecacheof(list(the air alarm's circuit components)), only_root_path = TRUE): circuits
        Dm.Deferred("code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:123", "Initialize: AddComponent(/datum/component/usb_port): the circuit USB port (circuits)");

        Dm.append(ref GLOB.air_alarms, this);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context of the held tool
        Dm.Deferred("code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:135", "Initialize: register_context(): screentip contexts (M10+)");
        check_enviroment();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:139
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!(warning_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return null;

        Waiting.Detach(speak(warning_message)); // the slot's set waitfor = FALSE (processing.dm:48): speak may sleep
        warning_cooldown = world.time + AIRALARM_WARNING_COOLDOWN; // COOLDOWN_START (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        return null;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:146
    public override object Destroy(float? force = null)
    {
        if (my_area != null)
            my_area = null;
        if (connected_sensor != null)
        {
            UnregisterSignal(connected_sensor, COMSIG_QDELETING);
            UnregisterSignal(connected_sensor.loc, COMSIG_TURF_EXPOSE);
            connected_sensor.connected_airalarm = null;
            connected_sensor = null;
        }

        QDEL_NULL(ref alarm_manager);
        Dm.remove(ref GLOB.air_alarms, this);
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:159
    public virtual void check_enviroment()
    {
        turf our_turf = connected_sensor != null ? get_turf(connected_sensor) : get_turf(this);
        datum_gas_mixture environment = Waiting.NoSleep(our_turf.return_air()) as datum_gas_mixture;
        if (environment == null)
            return;
        check_danger(our_turf, environment, environment.temperature);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:166
    public virtual object get_enviroment()
    {
        turf our_turf = connected_sensor != null ? get_turf(connected_sensor) : get_turf(this);
        return Waiting.NoSleep(our_turf.return_air());
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:170
    public override object power_change()
    {
        check_enviroment();
        return base.power_change();
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:174
    public override void on_enter_area(datum source = null, area area_to_register = null)
    {
        //were already registered to an area. exit from here first before entering into an new area
        if (my_area != null)
            return;
        base.on_enter_area(source, area_to_register);

        my_area = connected_sensor != null ? get_area(connected_sensor) as area : area_to_register;
        update_appearance();
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:183
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = Dm.text("[] Air Alarm", get_area_name(my_area));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:187
    public override void on_exit_area(datum source = null, area area_to_unregister = null)
    {
        //we cannot unregister from an area we never registered to in the first place
        if (my_area != area_to_unregister)
            return;
        base.on_exit_area(source, area_to_unregister);

        my_area = connected_sensor != null ? get_area(connected_sensor) as area : null;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:195
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        float sw__1 = buildstage;
        if (sw__1 == AIR_ALARM_BUILD_NO_CIRCUIT)
            Dm.append(ref dot, span_notice(I18n.T("It is missing air alarm electronics.")));
        else if (sw__1 == AIR_ALARM_BUILD_NO_WIRES)
            Dm.append(ref dot, span_notice(I18n.T("It is missing wiring.")));
        else if (sw__1 == AIR_ALARM_BUILD_COMPLETE)
            Dm.append(ref dot, span_notice(I18n.T("Right-click to [] the interface.", Dm.truthy(locked) ? I18n.T("unlock") : I18n.T("lock"))));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:205
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        // HAS_SILICON_ACCESS(user) (__DEFINES/is_helpers.dm:178); isAdminGhostAI is not declared (no admin ghosts yet, as _machinery.cs)
        if (HAS_TRAIT(user, TRAIT_SILICON_ACCESS) && Dm.truthy(aidisabled))
            to_chat(user, I18n.T("AI control has been disabled."));
        else if (!Dm.truthy(shorted))
            return await base.ui_status(user, state);
        return UI_CLOSE;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:212  (package E5: the air sensor link)
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);

        if (tool is not obj_item_multitool multi_tool || Dm.truthy(locked))
            return dot;

        if (multi_tool.buffer is obj_machinery_air_sensor sensor)
        {
            if (!Dm.truthy(allow_link_change))
            {
                balloon_alert(user, I18n.T("linking disabled"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (connected_sensor != null || sensor.connected_airalarm != null)
            {
                balloon_alert(user, I18n.T("sensor already connected!"));
                return ITEM_INTERACT_BLOCKING;
            }

            connect_sensor(sensor);
            balloon_alert(user, I18n.T("connected sensor"));
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:232
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AirAlarm", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:238
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["thresholdTypeMap"] = Dm.list(
            Dm.kv("warning_min", TLV_VAR_WARNING_MIN),
            Dm.kv("hazard_min", TLV_VAR_HAZARD_MIN),
            Dm.kv("warning_max", TLV_VAR_WARNING_MAX),
            Dm.kv("hazard_max", TLV_VAR_HAZARD_MAX),
            Dm.kv("all", TLV_VAR_ALL)
        );
        return data;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:249
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        bool silicon = HAS_TRAIT(user, TRAIT_SILICON_ACCESS); // HAS_SILICON_ACCESS(user), as ui_status

        data["locked"] = locked;
        data["siliconUser"] = silicon ? TRUE : FALSE;
        data["emagged"] = Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? 1 : 0;
        data["dangerLevel"] = danger_level;
        data["atmosAlarm"] = Dm.truthy(area_danger) ? TRUE : FALSE;
        data["fireAlarm"] = my_area.fire;
        data["faultStatus"] = my_area.fault_status;
        data["faultLocation"] = my_area.fault_location;
        data["sensor"] = connected_sensor != null ? TRUE : FALSE;
        data["allowLinkChange"] = allow_link_change;

        datum_gas_mixture environment = get_enviroment() as datum_gas_mixture;
        float total_moles = Dm.num(environment.total_moles());
        float temp = environment.temperature;
        float pressure = Dm.num(environment.return_pressure());

        DmList envData = Dm.list();
        data["envData"] = envData;
        if (connected_sensor != null)
            Dm.append(envData, Dm.list(Dm.list(
                Dm.kv("name", "Linked area"),
                Dm.kv("value", my_area.name)
            )));
        Dm.append(envData, Dm.list(Dm.list(
            Dm.kv("name", "Pressure"),
            Dm.kv("value", Dm.text("[] kPa", round(pressure, 0.01f))),
            Dm.kv("danger", ((datum_tlv)tlv_collection["pressure"]).check_value(pressure))
        )));
        Dm.append(envData, Dm.list(Dm.list(
            Dm.kv("name", "Temperature"),
            Dm.kv("value", Dm.text("[] Kelvin / [] Celcius", round(temp, 0.01f), round(temp, 0.01f) - T0C)),
            Dm.kv("danger", ((datum_tlv)tlv_collection["temperature"]).check_value(temp))
        )));
        if (Dm.truthy(total_moles))
        {
            DmList gas_names = (DmList)GLOB.meta_gas_info[META_GAS_NAME];
            foreach ((object gas_path, object moles_value) in Dm.pairs(environment.moles))
            {
                float moles = Dm.num(moles_value);
                float portion = moles / total_moles;
                Dm.append(envData, Dm.list(Dm.list(
                    Dm.kv("name", gas_names[gas_path]),
                    Dm.kv("value", Dm.text("[] moles / [] % / [] kPa", round(moles, 0.01f), round(100 * portion, 0.01f), round(portion * pressure, 0.01f))),
                    Dm.kv("danger", ((datum_tlv)tlv_collection[gas_path]).check_value(portion * pressure))
                )));
            }
        }

        DmList tlvSettings = Dm.list();
        data["tlvSettings"] = tlvSettings;
        foreach (object threshold in Dm.each(tlv_collection))
        {
            datum_tlv tlv = (datum_tlv)tlv_collection[threshold];
            DmList singular_tlv = Dm.list();
            if (Dm.eq(threshold, "pressure"))
            {
                singular_tlv["name"] = "Pressure";
                singular_tlv["unit"] = "kPa";
            }
            else if (Dm.eq(threshold, "temperature"))
            {
                singular_tlv["name"] = "Temperature";
                singular_tlv["unit"] = "K";
            }
            else
            {
                singular_tlv["name"] = ((DmList)GLOB.meta_gas_info[META_GAS_NAME])[threshold];
                singular_tlv["unit"] = "kPa";
            }
            singular_tlv["id"] = threshold;
            singular_tlv["warning_min"] = tlv.warning_min;
            singular_tlv["hazard_min"] = tlv.hazard_min;
            singular_tlv["warning_max"] = tlv.warning_max;
            singular_tlv["hazard_max"] = tlv.hazard_max;
            Dm.append(tlvSettings, Dm.list(singular_tlv));
        }

        if (!Dm.truthy(locked) || silicon)
        {
            DmList vents = Dm.list();
            data["vents"] = vents;
            foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(my_area.air_vents))
                Dm.append(vents, Dm.list(Dm.list(
                    Dm.kv("refID", REF(vent)),
                    Dm.kv("long_name", sanitize(vent.name)),
                    Dm.kv("power", vent.on),
                    Dm.kv("overclock", vent.fan_overclocked),
                    Dm.kv("integrity", vent.get_integrity_percentage()),
                    Dm.kv("checks", vent.pressure_checks),
                    Dm.kv("excheck", Dm.bitand(vent.pressure_checks, ATMOS_EXTERNAL_BOUND)),
                    Dm.kv("incheck", Dm.bitand(vent.pressure_checks, ATMOS_INTERNAL_BOUND)),
                    Dm.kv("direction", vent.pump_direction),
                    Dm.kv("external", vent.external_pressure_bound),
                    Dm.kv("internal", vent.internal_pressure_bound),
                    Dm.kv("extdefault", vent.external_pressure_bound == ONE_ATMOSPHERE ? TRUE : FALSE),
                    Dm.kv("intdefault", vent.internal_pressure_bound == 0 ? TRUE : FALSE)
                )));
            DmList scrubbers = Dm.list();
            data["scrubbers"] = scrubbers;
            foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(my_area.air_scrubbers))
            {
                DmList filter_types = Dm.list();
                DmList cached_gas_info = GLOB.meta_gas_info;
                foreach (object path in Dm.each(cached_gas_info[META_GAS_ID]))
                    Dm.append(filter_types, Dm.list(Dm.list(
                        Dm.kv("gas_id", ((DmList)cached_gas_info[META_GAS_ID])[path]),
                        Dm.kv("gas_name", ((DmList)cached_gas_info[META_GAS_NAME])[path]),
                        Dm.kv("enabled", Dm.@in(path, scrubber.filter_types) ? TRUE : FALSE)
                    )));
                Dm.append(scrubbers, Dm.list(Dm.list(
                    Dm.kv("refID", REF(scrubber)),
                    Dm.kv("long_name", sanitize(scrubber.name)),
                    Dm.kv("power", scrubber.on),
                    Dm.kv("scrubbing", scrubber.scrubbing),
                    Dm.kv("widenet", scrubber.widenet),
                    Dm.kv("filter_types", filter_types)
                )));
            }

            data["selectedModePath"] = selected_mode.GetType();
            DmList modes = Dm.list();
            data["modes"] = modes;
            foreach (object mode_path in Dm.each(GLOB.air_alarm_modes))
            {
                datum_air_alarm_mode mode = (datum_air_alarm_mode)GLOB.air_alarm_modes[mode_path];
                if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && Dm.truthy(mode.emag))
                    continue;
                Dm.append(modes, Dm.list(Dm.list(
                    Dm.kv("name", mode.name),
                    Dm.kv("desc", mode.desc),
                    Dm.kv("danger", mode.danger),
                    Dm.kv("path", mode.GetType())
                )));
            }

            // forgive me holy father
            data["panicSiphonPath"] = typeof(datum_air_alarm_mode_panic_siphon);
            data["filteringPath"] = typeof(datum_air_alarm_mode_filtering);
        }

        return data;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:365
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);

        if (Dm.truthy(dot) || buildstage != AIR_ALARM_BUILD_COMPLETE)
            return dot;
        mob user = ui.user;
        bool silicon = HAS_TRAIT(user, TRAIT_SILICON_ACCESS); // HAS_SILICON_ACCESS(user), as ui_status
        if ((Dm.truthy(locked) && !silicon) || (silicon && Dm.truthy(aidisabled)))
            return dot;

        area area = (connected_sensor != null ? get_area(connected_sensor) : get_area(this)) as area;

        // ASSERT(!isnull(area)) (__DEFINES/assert.dm:4), expanded inline (rules 11.1)
        if (!(area != null))
            throw Dm.CRASH(assertion_message("code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm", 376, "!isnull(area)"), "code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm", 376);

        object @ref = @params["ref"];
        obj_machinery_atmospherics_components_unary_vent_pump vent = null;
        obj_machinery_atmospherics_components_unary_vent_scrubber scrubber = null;
        if (@ref != null)
        {
            // locate(ref) in area.air_scrubbers, locate(ref) in area.air_vents
            object located = Dm.locate_ref(@ref);
            scrubber = Dm.@in(located, area.air_scrubbers) ? located as obj_machinery_atmospherics_components_unary_vent_scrubber : null;
            vent = Dm.@in(located, area.air_vents) ? located as obj_machinery_atmospherics_components_unary_vent_pump : null;
        }

        switch (action)
        {
            case "power":
            {
                obj_machinery_atmospherics_components powering = (obj_machinery_atmospherics_components)vent ?? scrubber;
                powering.on = Dm.truthy(@params["val"]) ? TRUE : FALSE;
                powering.atmos_conditions_changed();
                powering.update_appearance(UPDATE_ICON);
                break;
            }

            case "overclock":
                if (vent == null)
                    return TRUE;
                vent.toggle_overclock(source: key_name(user));
                vent.update_appearance(UPDATE_ICON);
                return TRUE;

            case "direction":
            {
                if (vent == null)
                    return TRUE;

                object value = @params["val"];

                if (Dm.eq(value, ATMOS_DIRECTION_SIPHONING) || Dm.eq(value, ATMOS_DIRECTION_RELEASING))
                {
                    vent.pump_direction = Dm.num(value);
                    vent.update_appearance(UPDATE_ICON);
                }
                break;
            }
            case "incheck":
            {
                if (vent == null)
                    return TRUE;

                float new_checks = Dm.clamp(Dm.bitxor(text2num(@params["val"]) is float v && v != 0 ? v : 0, ATMOS_INTERNAL_BOUND), NONE, ATMOS_BOUND_MAX);
                vent.pressure_checks = new_checks;
                vent.update_appearance(UPDATE_ICON);
                break;
            }
            case "excheck":
            {
                if (vent == null)
                    return TRUE;

                float new_checks = Dm.clamp(Dm.bitxor(text2num(@params["val"]) is float v && v != 0 ? v : 0, ATMOS_EXTERNAL_BOUND), NONE, ATMOS_BOUND_MAX);
                vent.pressure_checks = new_checks;
                vent.update_appearance(UPDATE_ICON);
                break;
            }
            case "set_internal_pressure":
            {
                if (vent == null)
                    return TRUE;

                float old_pressure = vent.internal_pressure_bound;
                float new_pressure = Dm.clamp(Dm.num(text2num(@params["value"])), 0, ATMOS_PUMP_MAX_PRESSURE);
                vent.internal_pressure_bound = new_pressure;
                if (old_pressure != new_pressure) // vent.investigate_log("internal pressure was set to ..."): the investigate logs (admin tooling, M10)
                    vent.investigate_log(Dm.text("internal pressure was set to [] by []", new_pressure, key_name(user)), INVESTIGATE_ATMOS);
                break;
            }
            case "reset_internal_pressure":
                if (vent == null)
                    return TRUE;

                if (vent.internal_pressure_bound != 0)
                {
                    vent.internal_pressure_bound = 0;
                    vent.investigate_log(Dm.text("internal pressure was reset by []", key_name(user)), INVESTIGATE_ATMOS);
                }
                break;
            case "set_external_pressure":
            {
                if (vent == null)
                    return TRUE;

                float old_pressure = vent.external_pressure_bound;
                float new_pressure = Dm.clamp(Dm.num(text2num(@params["value"])), 0, ATMOS_PUMP_MAX_PRESSURE);

                if (old_pressure == new_pressure)
                    return TRUE;

                vent.external_pressure_bound = new_pressure;
                vent.investigate_log(Dm.text("external pressure was set to [] by []", new_pressure, key_name(user)), INVESTIGATE_ATMOS);
                vent.update_appearance(UPDATE_ICON);
                break;
            }
            case "reset_external_pressure":
                if (vent == null)
                    return TRUE;

                if (vent.external_pressure_bound == ATMOS_PUMP_MAX_PRESSURE)
                    return TRUE;

                vent.external_pressure_bound = ATMOS_PUMP_MAX_PRESSURE;
                vent.investigate_log(Dm.text("internal pressure was reset by []", key_name(user)), INVESTIGATE_ATMOS);
                vent.update_appearance(UPDATE_ICON);
                break;
            case "scrubbing":
                if (scrubber == null)
                    return TRUE;

                scrubber.set_scrubbing(Dm.truthy(@params["val"]) ? TRUE : FALSE, user);
                break;
            case "widenet":
                if (scrubber == null)
                    return TRUE;

                scrubber.set_widenet(Dm.truthy(@params["val"]) ? TRUE : FALSE);
                break;
            case "toggle_filter":
                if (scrubber == null)
                    return TRUE;

                scrubber.toggle_filters(@params["val"]);
                break;
            case "mode":
                select_mode(user, text2path(@params["mode"] as string));
                investigate_log(Dm.text("was turned to [] mode by []", selected_mode.name, key_name(user)), INVESTIGATE_ATMOS);
                break;

            case "set_threshold":
            {
                object threshold = (object)text2path(@params["threshold"] as string) ?? @params["threshold"];
                datum_tlv tlv = tlv_collection[threshold] as datum_tlv;
                if (tlv == null)
                    return dot;
                object threshold_type = @params["threshold_type"];
                object value = @params["value"];
                tlv.set_value(threshold_type, value);
                investigate_log(Dm.text("threshold value for []:[] was set to [] by []", threshold, threshold_type, value, key_name(user)), INVESTIGATE_ATMOS);

                check_enviroment();
                break;
            }

            case "reset_threshold":
            {
                object threshold = (object)text2path(@params["threshold"] as string) ?? @params["threshold"];
                datum_tlv tlv = tlv_collection[threshold] as datum_tlv;
                if (tlv == null)
                    return dot;
                object threshold_type = @params["threshold_type"];
                tlv.reset_value(threshold_type);
                investigate_log(Dm.text("threshold value for []:[] was reset by []", threshold, threshold_type, key_name(user)), INVESTIGATE_ATMOS);

                check_enviroment();
                break;
            }

            case "alarm":
                if (Dm.truthy(alarm_manager.send_alarm(ALARM_ATMOS)))
                    danger_level = AIR_ALARM_ALERT_HAZARD;
                break;

            case "reset":
                if (Dm.truthy(alarm_manager.clear_alarm(ALARM_ATMOS)))
                    danger_level = AIR_ALARM_ALERT_NONE;
                break;

            case "disconnect_sensor":
                if (Dm.truthy(allow_link_change))
                    disconnect_sensor();
                break;

            case "lock":
                await togglelock(user as mob_living);
                return TRUE;
        }

        update_appearance();

        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:523
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);

        if (Dm.truthy(panel_open) || Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || Dm.truthy(shorted))
        {
            Waiting.NoSleep(set_light(0));
            return dot;
        }

        string color;
        if (danger_level == AIR_ALARM_ALERT_HAZARD)
            color = "#FF0022"; // red
        else if (danger_level == AIR_ALARM_ALERT_WARNING || Dm.truthy(area_danger))
            color = "#FFAA00"; // yellow
        else
            color = "#00FFCC"; // teal

        Waiting.NoSleep(set_light(1.5f, 1, color));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:540
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            float sw__1 = buildstage;
            if (sw__1 == AIR_ALARM_BUILD_COMPLETE)
                icon_state = "alarmx";
            else if (sw__1 == AIR_ALARM_BUILD_NO_WIRES)
                icon_state = "alarm_b2";
            else if (sw__1 == AIR_ALARM_BUILD_NO_CIRCUIT)
                icon_state = "alarm_b1";
            return await base.update_icon_state(__extra);
        }

        icon_state = connected_sensor == null ? "alarmp" : "alarmp_remote";
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:554
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(panel_open) || Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || Dm.truthy(shorted))
            return dot;

        string state;
        if (danger_level == AIR_ALARM_ALERT_HAZARD)
            state = "alarm1";
        else if (danger_level == AIR_ALARM_ALERT_WARNING || Dm.truthy(area_danger))
            state = "alarm2";
        else
            state = "alarm0";

        Dm.append(ref dot, mutable_appearance(icon, state));
        // DEVIATION(DV12): emissive_appearance(icon, state, src) (__HELPERS/lighting.dm:2), the screen's glow on the emissive
        // plane, is not created (lighting planes and emissives are not ported).
        Dm.Deferred("code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:570", "update_overlays: emissive_appearance of the alarm's screen (DEVIATION DV12)");
        return dot;
    }

    /// Check the current air and update our danger level.
    /// [/obj/machinery/airalarm/var/danger_level]
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:573
    [SignalHandler]
    public virtual void check_danger(turf location = null, datum_gas_mixture environment = null, object exposed_temperature = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || Dm.truthy(shorted))
            return;

        if (environment == null)
            return;

        float old_danger = danger_level;
        danger_level = AIR_ALARM_ALERT_NONE;
        float old_area_danger = area_danger;
        area_danger = Dm.num(my_area.active_alarms[ALARM_ATMOS]);

        float total_moles = Dm.num(environment.total_moles());
        float pressure = Dm.num(environment.return_pressure());
        float temp = Dm.num(environment.return_temperature());

        danger_level = max(danger_level, Dm.num(((datum_tlv)tlv_collection["pressure"]).check_value(pressure)));
        danger_level = max(danger_level, Dm.num(((datum_tlv)tlv_collection["temperature"]).check_value(temp)));
        if (Dm.truthy(total_moles))
        {
            DmList cached_gas_info = GLOB.meta_gas_info;
            foreach (Type gas_path in Dm.each_cast<Type>(cached_gas_info[META_GAS_ID]))
            {
                float moles = (environment.moles[gas_path] is var or__1 && Dm.truthy(or__1)) ? Dm.num(or__1) : 0;
                danger_level = max(danger_level, Dm.num(((datum_tlv)tlv_collection[gas_path]).check_value(pressure * moles / total_moles)));
            }
        }

        if (Dm.truthy(danger_level))
        {
            alarm_manager.send_alarm(ALARM_ATMOS);
            datum_tlv pressure_tlv = (datum_tlv)tlv_collection["pressure"];
            datum_tlv temperature_tlv = (datum_tlv)tlv_collection["temperature"];
            bool is_high_pressure = pressure_tlv.hazard_max != TLV_VALUE_IGNORE && pressure >= pressure_tlv.hazard_max;
            bool is_high_temp = temperature_tlv.hazard_max != TLV_VALUE_IGNORE && temp >= temperature_tlv.hazard_max;
            bool is_low_pressure = pressure_tlv.hazard_min != TLV_VALUE_IGNORE && pressure <= pressure_tlv.hazard_min;
            bool is_low_temp = temperature_tlv.hazard_min != TLV_VALUE_IGNORE && temp <= temperature_tlv.hazard_min;

            if (is_low_pressure && is_low_temp)
            {
                warning_message = "Danger! Low pressure and temperature detected.";
                return;
            }
            if (is_low_pressure && is_high_temp)
            {
                warning_message = "Danger! Low pressure and high temperature detected.";
                return;
            }
            if (is_high_pressure && is_high_temp)
            {
                warning_message = "Danger! High pressure and temperature detected.";
                return;
            }
            if (is_high_pressure && is_low_temp)
            {
                warning_message = "Danger! High pressure and low temperature detected.";
                return;
            }
            if (is_low_pressure)
            {
                warning_message = "Danger! Low pressure detected.";
                return;
            }
            if (is_high_pressure)
            {
                warning_message = "Danger! High pressure detected.";
                return;
            }
            if (is_low_temp)
            {
                warning_message = "Danger! Low temperature detected.";
                return;
            }
            if (is_high_temp)
            {
                warning_message = "Danger! High temperature detected.";
                return;
            }
            else
                warning_message = null;
        }
        else
        {
            alarm_manager.clear_alarm(ALARM_ATMOS);
            warning_message = null;
        }

        if (old_danger != danger_level || old_area_danger != area_danger)
            update_appearance();

        selected_mode.replace(my_area, pressure);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:641
    // (mode_path is object, not tg's declared /datum/air_alarm_mode: it holds a typepath, rules 3.5 V3)
    public virtual void select_mode(atom source = null, object mode_path = null, float? should_apply = null)
    {
        should_apply ??= TRUE;
        datum_air_alarm_mode new_mode = GLOB.air_alarm_modes[mode_path] as datum_air_alarm_mode;
        if (new_mode == null)
            return;
        if (Dm.truthy(new_mode.emag) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return;
        selected_mode = new_mode;
        if (Dm.truthy(should_apply))
            selected_mode.apply(my_area);
        if (Dm.truthy(_listen_lookup?[COMSIG_AIRALARM_UPDATE_MODE])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            _SendSignal(COMSIG_AIRALARM_UPDATE_MODE, Dm.list(this, source));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:654
    public virtual async DmTask speak(object warning_message = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            return;
        if (!Dm.truthy(speaker_enabled))
            return;
        if (!Dm.truthy(warning_message))
            return;

        await say(warning_message);
    }

    /// Used for unlocked air alarm helper, which unlocks the air alarm.
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:665
    public virtual void unlock()
    {
        locked = FALSE;
    }

    /// Used for syndicate_access air alarm helper, which sets air alarm's required access to syndicate_access.
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:669
    public virtual void give_syndicate_access()
    {
        req_access = Dm.list(ACCESS_SYNDICATE);
    }

    ///Used for away_general_access air alarm helper, which set air alarm's required access to away_general_access.
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:673
    public virtual void give_away_general_access()
    {
        req_access = Dm.list(ACCESS_AWAY_GENERAL);
    }

    ///Used for engine_access air alarm helper, which set air alarm's required access to away_general_access.
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:677
    public virtual void give_engine_access()
    {
        name = "engine air alarm";
        locked = FALSE;
        req_access = null;
        req_one_access = Dm.list(ACCESS_ATMOSPHERICS, ACCESS_ENGINEERING);
    }

    ///Used for mixingchamber_access air alarm helper, which set air alarm's required access to away_general_access.
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:684
    public virtual void give_mixingchamber_access()
    {
        name = "chamber air alarm";
        locked = FALSE;
        req_access = null;
        req_one_access = Dm.list(ACCESS_ATMOSPHERICS, ACCESS_ORDNANCE);
    }

    ///Used for all_access air alarm helper, which set air alarm's required access to null.
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:691
    public virtual void give_all_access()
    {
        name = "all-access air alarm";
        desc = "This particular atmos control unit appears to have no access restrictions.";
        locked = FALSE;
        req_access = null;
        req_one_access = null;
    }

    ///Used for air alarm cold room tlv helper, which sets cold room temperature and pressure alarm thresholds
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:699
    public virtual void set_tlv_cold_room()
    {
        tlv_collection["temperature"] = Dm.@new<datum_tlv_cold_room_temperature>();
        tlv_collection["pressure"] = Dm.@new<datum_tlv_cold_room_pressure>();
    }

    ///Used for air alarm kitchen tlv helper, which ensures that kitchen air alarm doesn't trigger from cold room air
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:704
    public virtual void set_tlv_kitchen()
    {
        tlv_collection["temperature"] = Dm.@new<datum_tlv_kitchen_temperature>();
    }

    ///Used for air alarm no tlv helper, which removes alarm thresholds
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:708
    public virtual void set_tlv_no_checks()
    {
        tlv_collection["temperature"] = Dm.@new<datum_tlv_no_checks>();
        tlv_collection["pressure"] = Dm.@new<datum_tlv_no_checks>();

        foreach (object gas_path in Dm.each(GLOB.meta_gas_info[META_GAS_ID]))
            tlv_collection[gas_path] = Dm.@new<datum_tlv_no_checks>();
    }

    ///Used for air alarm link helper, which connects air alarm to a sensor with corresponding chamber_id
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:716
    public virtual void setup_chamber_link()
    {
        obj_machinery_air_sensor sensor = null;
        foreach (obj_machinery_air_sensor target in Dm.each_cast<obj_machinery_air_sensor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_air_sensor))))
        {
            if (target.z == z && Dm.eq(target.chamber_id, air_sensor_chamber_id))
            {
                sensor = target;
                break;
            }
        }
        if (sensor == null)
        {
            // log_mapping("[src] at [AREACOORD(src)] tried to connect to a sensor, but no sensor with chamber_id:[air_sensor_chamber_id] found!")
            Dm.Deferred("code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:722", "setup_chamber_link: the mapping log of a missing sensor (AREACOORD, M10+)");
            return;
        }
        if (connected_sensor != null)
        {
            // log_mapping("[src] at [AREACOORD(src)] tried to connect to more than one sensor!")
            Dm.Deferred("code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:725", "setup_chamber_link: the mapping log of a second sensor (AREACOORD, M10+)");
            return;
        }
        connect_sensor(sensor);
    }

    ///Used to connect air alarm with a sensor
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:731
    public virtual void connect_sensor(obj_machinery_air_sensor sensor = null)
    {
        sensor.connected_airalarm = this;
        connected_sensor = sensor;

        RegisterSignal(connected_sensor, COMSIG_QDELETING, nameof(disconnect_sensor));

        // Transfer signal from air alarm to sensor
        UnregisterSignal(loc, COMSIG_TURF_EXPOSE);
        RegisterSignal(connected_sensor.loc, COMSIG_TURF_EXPOSE, nameof(check_danger), @override: TRUE);

        my_area = get_area(connected_sensor) as area;

        check_enviroment();

        update_appearance();
        update_name();
    }

    ///Used to reset the air alarm to default configuration after disconnecting from air sensor
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:749
    public virtual void disconnect_sensor()
    {
        UnregisterSignal(connected_sensor, COMSIG_QDELETING);

        // Transfer signal from sensor to air alarm
        UnregisterSignal(connected_sensor.loc, COMSIG_TURF_EXPOSE);
        RegisterSignal(loc, COMSIG_TURF_EXPOSE, nameof(check_danger), @override: TRUE);

        connected_sensor.connected_airalarm = null;
        connected_sensor = null;
        my_area = get_area(this) as area;

        check_enviroment();

        update_appearance();
        update_name();
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:83  GLOBAL_LIST_EMPTY_TYPED(air_alarms, /obj/machinery/airalarm)
    public DmList air_alarms;

    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:83  (GLOBAL_MANAGED(air_alarms, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalair_alarms()
    {
        air_alarms = Dm.list();
        Dm.append(ref gvars_datum_init_order, "air_alarms");
    }
}

public sealed partial class TgState
{
    // tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:41  /obj/machinery/airalarm/var/static/list/atmos_connections = list(COMSIG_TURF_EXPOSE = PROC_REF(check_danger))
    internal DmList obj_machinery_airalarm__atmos_connections = Dm.list(
        Dm.kv(COMSIG_TURF_EXPOSE, nameof(obj_machinery_airalarm.check_danger)));
}
