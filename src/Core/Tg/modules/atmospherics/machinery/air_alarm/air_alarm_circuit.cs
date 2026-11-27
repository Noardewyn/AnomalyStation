// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the air alarm's circuit components, which its USB port adds to a plugged circuit: the general one (the alarm and
// the mode), the core control of a gas's limits and readings, and the scrubber and vent controls, each with duplicates for more gases,
// scrubbers or vents. The var blocks are generated.

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:1
public partial class obj_item_circuit_component_air_alarm_general : obj_item_circuit_component
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:22  var/static/list/options_map
    public static ref DmList options_map => ref TgState.Current.obj_item_circuit_component_air_alarm_general__options_map;

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:24
    public override void populate_options()
    {
        if (options_map == null)
        {
            options_map = Dm.list();
            foreach (object mode_path in Dm.each(GLOB.air_alarm_modes))
            {
                datum_air_alarm_mode mode = GLOB.air_alarm_modes[mode_path] as datum_air_alarm_mode;
                if (!Dm.truthy(mode.emag))
                    options_map[mode.name] = mode.type;
            }
        }
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:32
    public override async DmTask populate_ports()
    {
        mode = add_option_port("Mode", options_map, order: 1) as datum_port_input_option;
        set_mode = add_input_port("Set Mode", PORT_TYPE_SIGNAL, trigger: nameof(set_mode__proc)) as datum_port_input;
        enable_fire_alarm = add_input_port("Enable Alarm", PORT_TYPE_SIGNAL, trigger: nameof(trigger_alarm)) as datum_port_input;
        disable_fire_alarm = add_input_port("Disable Alarm", PORT_TYPE_SIGNAL, trigger: nameof(trigger_alarm)) as datum_port_input;

        fire_alarm_enabled = add_output_port("Alarm Enabled", PORT_TYPE_NUMBER) as datum_port_output;
        current_mode = add_output_port("Current Mode", PORT_TYPE_STRING) as datum_port_output;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:41
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_airalarm)))
        {
            connected_alarm = shell as obj_machinery_airalarm;
            RegisterSignal(connected_alarm.alarm_manager, COMSIG_ALARM_TRIGGERED, nameof(on_alarm_triggered));
            RegisterSignal(connected_alarm.alarm_manager, COMSIG_ALARM_CLEARED, nameof(on_alarm_cleared));
            RegisterSignal(shell, COMSIG_AIRALARM_UPDATE_MODE, nameof(on_mode_updated));
            current_mode.set_value(connected_alarm.selected_mode.name);
        }
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:50
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        if (connected_alarm != null)
        {
            UnregisterSignal(connected_alarm.alarm_manager, Dm.list(
                COMSIG_ALARM_TRIGGERED,
                COMSIG_ALARM_CLEARED));
        }
        connected_alarm = null;

        UnregisterSignal(shell, Dm.list(
            COMSIG_AIRALARM_UPDATE_MODE));
        return base.unregister_usb_parent(shell);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:63
    [SignalHandler]
    public virtual void on_mode_updated(obj_machinery_airalarm alarm = null, datum signal_source = null)
    {
        current_mode.set_value(alarm.selected_mode.name);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:67
    [SignalHandler]
    public virtual void on_alarm_triggered(datum source = null, object alarm_type = null, area location = null)
    {
        if (Dm.eq(alarm_type, ALARM_ATMOS))
            fire_alarm_enabled.set_output(TRUE);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:72
    [SignalHandler]
    public virtual void on_alarm_cleared(datum source = null, object alarm_type = null, area location = null)
    {
        if (Dm.eq(alarm_type, ALARM_ATMOS))
            fire_alarm_enabled.set_output(FALSE);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:78
    [ShouldNotSleep]
    public virtual void trigger_alarm(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        if (port == enable_fire_alarm)
        {
            if (Dm.truthy(connected_alarm.alarm_manager.send_alarm(ALARM_ATMOS)))
                connected_alarm.danger_level = AIR_ALARM_ALERT_HAZARD;
        }
        else
        {
            if (Dm.truthy(connected_alarm.alarm_manager.clear_alarm(ALARM_ATMOS)))
                connected_alarm.danger_level = AIR_ALARM_ALERT_NONE;
        }
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:90
    [ShouldNotSleep]
    public virtual void set_mode__proc(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        if (!Dm.truthy(mode.value))
            return;

        connected_alarm.select_mode(parent.get_creator(), options_map[mode.value]);
        connected_alarm.investigate_log(Dm.text("was turned to [] by []", connected_alarm.selected_mode.name, parent.get_creator()), INVESTIGATE_ATMOS);
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:101
public partial class obj_item_circuit_component_air_alarm : obj_item_circuit_component
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:130
    public override async DmTask ui_perform_action(mob user = null, object action = null)
    {
        if (length(alarm_duplicates) >= max_alarm_duplicates)
            return;

        if (Dm.eq(action, "add_new_component"))
        {
            obj_item_circuit_component_air_alarm component = Dm.@new<obj_item_circuit_component_air_alarm_duplicate>(parent);
            await parent.add_component(component);
            RegisterSignal(component, COMSIG_QDELETING, nameof(on_duplicate_removed));
            component.connected_alarm = connected_alarm;
            Dm.append(alarm_duplicates, component);
        }
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:141
    [SignalHandler]
    public virtual void on_duplicate_removed(datum source = null)
    {
        Dm.remove(alarm_duplicates, source);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:145
    public override async DmTask populate_ports()
    {
        min_2 = add_input_port("Hazard Minimum", PORT_TYPE_NUMBER, trigger: null) as datum_port_input;
        min_1 = add_input_port("Warning Minimum", PORT_TYPE_NUMBER, trigger: null) as datum_port_input;
        max_1 = add_input_port("Warning Maximum", PORT_TYPE_NUMBER, trigger: null) as datum_port_input;
        max_2 = add_input_port("Hazard Maximum", PORT_TYPE_NUMBER, trigger: null) as datum_port_input;
        set_data = add_input_port("Set Limits", PORT_TYPE_SIGNAL, trigger: nameof(set_limits)) as datum_port_input;
        request_data = add_input_port("Request Data", PORT_TYPE_SIGNAL) as datum_port_input;

        pressure = add_output_port("Pressure", PORT_TYPE_NUMBER) as datum_port_output;
        temperature = add_output_port("Temperature", PORT_TYPE_NUMBER) as datum_port_output;
        gas_amount = add_output_port("Chosen Gas Amount", PORT_TYPE_NUMBER) as datum_port_output;
        update_received = add_output_port("Update Received", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:159  var/static/list/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_air_alarm__populate_options__component_options;

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:158
    public override void populate_options()
    {
        if (populate_options__component_options == null)
        {
            populate_options__component_options = Dm.list(
                Dm.kv("Pressure", "pressure"),
                Dm.kv("Temperature", "temperature"));

            DmList cached_gas_info = GLOB.meta_gas_info;
            foreach (object gas_id in Dm.each(cached_gas_info[META_GAS_ID]))
                populate_options__component_options[((DmList)cached_gas_info[META_GAS_NAME])[gas_id]] = gas_id2path(gas_id);
        }

        air_alarm_options = add_option_port("Air Alarm Options", populate_options__component_options) as datum_port_input_option;
        options_map = populate_options__component_options;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:189
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        QDEL_LIST(alarm_duplicates);
        base.removed_from(removed_from);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:193
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_airalarm)))
            connected_alarm = shell as obj_machinery_airalarm;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:198
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        connected_alarm = null;
        foreach (obj_item_circuit_component_air_alarm alarm in Dm.each_cast<obj_item_circuit_component_air_alarm>(alarm_duplicates))
            alarm.connected_alarm = null;
        return base.unregister_usb_parent(shell);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:204
    [ShouldNotSleep]
    public virtual void set_limits()
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        object current_option = air_alarm_options.value;

        if (!Dm.truthy(current_option))
            return;

        datum_tlv settings = connected_alarm.tlv_collection[options_map[current_option]] as datum_tlv;
        if (min_2.value != null)
            settings.hazard_min = Dm.num(min_2.value);
        if (min_1.value != null)
            settings.warning_min = Dm.num(min_1.value);
        if (max_1.value != null)
            settings.warning_max = Dm.num(max_1.value);
        if (max_2.value != null)
            settings.hazard_max = Dm.num(max_2.value);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:224
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return null;

        object current_option = air_alarm_options.value;

        datum_gas_mixture environment = connected_alarm.get_enviroment() as datum_gas_mixture;
        pressure.set_output(round(Dm.num(environment.return_pressure())));
        temperature.set_output(round(environment.temperature));
        if (ispath(options_map[current_option]))
            gas_amount.set_output(round(Dm.num(environment.moles[options_map[current_option]])));

        update_received.set_output(COMPONENT_SIGNAL);
        return null;
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:174
public partial class obj_item_circuit_component_air_alarm_duplicate : obj_item_circuit_component_air_alarm
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:180
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        if (!QDELING(this))
            qdel(this);
        base.removed_from(removed_from);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:185
    public override object Destroy(float? force = null)
    {
        connected_alarm = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:238
public partial class obj_item_circuit_component_air_alarm_scrubbers : obj_item_circuit_component
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:280  var/static/list/filtering_map
    public static ref DmList filtering_map => ref TgState.Current.obj_item_circuit_component_air_alarm_scrubbers__filtering_map;

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:285
    public override async DmTask ui_perform_action(mob user = null, object action = null)
    {
        if (length(scrubber_duplicates) >= max_scrubber_duplicates)
            return;

        if (Dm.eq(action, "add_new_component"))
        {
            obj_item_circuit_component_air_alarm_scrubbers component = Dm.@new<obj_item_circuit_component_air_alarm_scrubbers_duplicate>(parent);
            await parent.add_component(component);
            RegisterSignal(component, COMSIG_QDELETING, nameof(on_duplicate_removed));
            component.connected_alarm = connected_alarm;
            component.scrubbers.possible_options = extract_id_tags(connected_alarm.my_area.air_scrubbers) as DmList;
            Dm.append(scrubber_duplicates, component);
        }
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:297
    [SignalHandler]
    public virtual void on_duplicate_removed(datum source = null)
    {
        Dm.remove(scrubber_duplicates, source);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:301
    public override void populate_options()
    {
        scrubbers = add_option_port("Scrubber", null) as datum_port_input_option;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:304
    public override async DmTask populate_ports()
    {
        gas_filter = add_input_port("Gas To Filter", PORT_TYPE_LIST(PORT_TYPE_STRING), trigger: null) as datum_port_input;
        set_gas_filter = add_input_port("Set Filter", PORT_TYPE_SIGNAL, trigger: nameof(set_gas_to_filter)) as datum_port_input;
        enable_extended_range = add_input_port("Enable Extra Range", PORT_TYPE_SIGNAL, trigger: nameof(toggle_range)) as datum_port_input;
        disable_extended_range = add_input_port("Disable Extra Range", PORT_TYPE_SIGNAL, trigger: nameof(toggle_range)) as datum_port_input;
        enable_siphon = add_input_port("Enable Siphon", PORT_TYPE_SIGNAL, trigger: nameof(toggle_siphon)) as datum_port_input;
        disable_siphon = add_input_port("Disable Siphon", PORT_TYPE_SIGNAL, trigger: nameof(toggle_siphon)) as datum_port_input;
        enable = add_input_port("Enable", PORT_TYPE_SIGNAL, trigger: nameof(toggle_scrubber)) as datum_port_input;
        disable = add_input_port("Disable", PORT_TYPE_SIGNAL, trigger: nameof(toggle_scrubber)) as datum_port_input;
        request_update = add_input_port("Request Data", PORT_TYPE_SIGNAL, trigger: nameof(update_data)) as datum_port_input;

        enabled = add_output_port("Enabled", PORT_TYPE_BOOLEAN) as datum_port_output;
        is_siphoning = add_output_port("Siphoning", PORT_TYPE_BOOLEAN) as datum_port_output;
        filtering = add_output_port("Filtered Gases", PORT_TYPE_LIST(PORT_TYPE_STRING)) as datum_port_output;
        update_received = add_output_port("Update Received", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:334
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        QDEL_LIST(scrubber_duplicates);
        base.removed_from(removed_from);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:338
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_airalarm)))
        {
            connected_alarm = shell as obj_machinery_airalarm;
            scrubbers.possible_options = extract_id_tags(connected_alarm.my_area.air_scrubbers) as DmList;
        }
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:344
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        connected_alarm = null;
        scrubbers.possible_options = null;
        foreach (obj_item_circuit_component_air_alarm_scrubbers scrubber in Dm.each_cast<obj_item_circuit_component_air_alarm_scrubbers>(scrubber_duplicates))
            scrubber.connected_alarm = null;
        return base.unregister_usb_parent(shell);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:353  var/static/list/meta_data
    private static ref DmList get_ui_notices__meta_data => ref TgState.Current.obj_item_circuit_component_air_alarm_scrubbers__get_ui_notices__meta_data;

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:351
    public override object get_ui_notices()
    {
        object dot = base.get_ui_notices();
        if (length(get_ui_notices__meta_data) == 0)
        {
            DmList cached_gas_info = GLOB.meta_gas_info;
            foreach (object typepath in Dm.each(cached_gas_info[META_GAS_ID]))
                Dm.append(get_ui_notices__meta_data, ((DmList)cached_gas_info[META_GAS_ID])[typepath]);
        }
        Dm.append(ref dot, create_table_notices(get_ui_notices__meta_data, column_name: "Gas", column_name_plural: "Gases"));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:360
    [ShouldNotSleep]
    public virtual void set_gas_to_filter(datum_port_input port = null)
    {
        Waiting.DetachSync(() => set_gas_filter_async(port));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:364
    public virtual void set_gas_filter_async(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        DmList valid_filters = Dm.list();
        foreach (object info in Dm.each(gas_filter.value))
        {
            object gas_type = gas_id2path(info);
            if (!Dm.truthy(gas_type))
                continue;
            Dm.append(valid_filters, gas_type);
        }

        obj_machinery_atmospherics_components_unary_vent_scrubber scrubber = find_by_id_tag(connected_alarm.my_area.air_scrubbers, scrubbers.value) as obj_machinery_atmospherics_components_unary_vent_scrubber;
        if (scrubber == null)
            return;

        scrubber.filter_types = valid_filters;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:381
    [ShouldNotSleep]
    public virtual void toggle_scrubber(datum_port_input port = null)
    {
        Waiting.DetachSync(() => toggle_scrubber_async(port));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:385
    public virtual void toggle_scrubber_async(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        // var/scrubber_id = scrubbers.value
        obj_machinery_atmospherics_components_unary_vent_scrubber scrubber = find_by_id_tag(connected_alarm.my_area.air_scrubbers, scrubbers.value) as obj_machinery_atmospherics_components_unary_vent_scrubber;
        if (scrubber == null)
            return;

        scrubber.on = Dm.num(port == enable);
        scrubber.update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:397
    [ShouldNotSleep]
    public virtual void toggle_range(datum_port_input port = null)
    {
        Waiting.DetachSync(() => toggle_range_async(port));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:401
    public virtual void toggle_range_async(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        obj_machinery_atmospherics_components_unary_vent_scrubber scrubber = find_by_id_tag(connected_alarm.my_area.air_scrubbers, scrubbers.value) as obj_machinery_atmospherics_components_unary_vent_scrubber;
        if (scrubber == null)
            return;

        scrubber.widenet = Dm.num(port == enable_extended_range);
        scrubber.update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:412
    [ShouldNotSleep]
    public virtual void toggle_siphon(datum_port_input port = null)
    {
        Waiting.DetachSync(() => toggle_siphon_async(port));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:416
    public virtual void toggle_siphon_async(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        obj_machinery_atmospherics_components_unary_vent_scrubber scrubber = find_by_id_tag(connected_alarm.my_area.air_scrubbers, scrubbers.value) as obj_machinery_atmospherics_components_unary_vent_scrubber;
        if (scrubber == null)
            return;

        scrubber.scrubbing = Dm.num(port != enable_siphon);
        scrubber.update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:427
    [ShouldNotSleep]
    public virtual void update_data()
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        obj_machinery_atmospherics_components_unary_vent_scrubber scrubber = find_by_id_tag(connected_alarm.my_area.air_scrubbers, scrubbers.value) as obj_machinery_atmospherics_components_unary_vent_scrubber;
        if (scrubber == null)
            return;

        enabled.set_value(scrubber.on);
        is_siphoning.set_value(Dm.num(scrubber.scrubbing == ATMOS_DIRECTION_SCRUBBING));

        DmList filtered = Dm.list();

        foreach (object gas_type in Dm.each(scrubber.filter_types))
            Dm.append(filtered, Dm.initial(gas_type as Type, (datum_gas g) => g.id));

        filtering.set_value(filtered);

        update_received.set_value(COMPONENT_SIGNAL);
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:320
public partial class obj_item_circuit_component_air_alarm_scrubbers_duplicate : obj_item_circuit_component_air_alarm_scrubbers
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:325
    public override object Destroy(float? force = null)
    {
        connected_alarm = null;
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:329
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        if (!QDELING(this))
            qdel(this);
        base.removed_from(removed_from);
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:448
public partial class obj_item_circuit_component_air_alarm_vents : obj_item_circuit_component
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:500  var/static/list/filtering_map
    public static ref DmList filtering_map => ref TgState.Current.obj_item_circuit_component_air_alarm_vents__filtering_map;

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:505
    public override async DmTask ui_perform_action(mob user = null, object action = null)
    {
        if (length(vent_duplicates) >= max_vent_duplicates)
            return;

        if (Dm.eq(action, "add_new_component"))
        {
            obj_item_circuit_component_air_alarm_vents component = Dm.@new<obj_item_circuit_component_air_alarm_vents_duplicate>(parent);
            await parent.add_component(component);
            RegisterSignal(component, COMSIG_QDELETING, nameof(on_duplicate_removed));
            Dm.append(vent_duplicates, component);
            component.connected_alarm = connected_alarm;
            component.vents.possible_options = extract_id_tags(connected_alarm.my_area.air_vents) as DmList;
        }
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:517
    [SignalHandler]
    public virtual void on_duplicate_removed(datum source = null)
    {
        Dm.remove(vent_duplicates, source);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:521
    public override void populate_options()
    {
        vents = add_option_port("Vent", null) as datum_port_input_option;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:524
    public override async DmTask populate_ports()
    {
        external_pressure = add_input_port("External Pressure", PORT_TYPE_NUMBER, trigger: nameof(set_external_pressure)) as datum_port_input;
        internal_pressure = add_input_port("Internal Pressure", PORT_TYPE_NUMBER, trigger: nameof(set_internal_pressure)) as datum_port_input;

        enable_external = add_input_port("Enable External", PORT_TYPE_SIGNAL, trigger: nameof(toggle_external)) as datum_port_input;
        disable_external = add_input_port("Disable External", PORT_TYPE_SIGNAL, trigger: nameof(toggle_external)) as datum_port_input;
        enable_internal = add_input_port("Enable Internal", PORT_TYPE_SIGNAL, trigger: nameof(toggle_internal)) as datum_port_input;
        disable_internal = add_input_port("Disable Internal", PORT_TYPE_SIGNAL, trigger: nameof(toggle_internal)) as datum_port_input;

        enable_siphon = add_input_port("Enable Siphon", PORT_TYPE_SIGNAL, trigger: nameof(toggle_siphon)) as datum_port_input;
        disable_siphon = add_input_port("Disable Siphon", PORT_TYPE_SIGNAL, trigger: nameof(toggle_siphon)) as datum_port_input;
        enable = add_input_port("Enable", PORT_TYPE_SIGNAL, trigger: nameof(toggle_vent)) as datum_port_input;
        disable = add_input_port("Disable", PORT_TYPE_SIGNAL, trigger: nameof(toggle_vent)) as datum_port_input;
        request_update = add_input_port("Request Data", PORT_TYPE_SIGNAL, trigger: nameof(update_data)) as datum_port_input;

        enabled = add_output_port("Enabled", PORT_TYPE_BOOLEAN) as datum_port_output;
        is_siphoning = add_output_port("Siphoning", PORT_TYPE_BOOLEAN) as datum_port_output;
        external_on = add_output_port("External On", PORT_TYPE_BOOLEAN) as datum_port_output;
        internal_on = add_output_port("Internal On", PORT_TYPE_BOOLEAN) as datum_port_output;
        current_external_pressure = add_output_port("External Pressure", PORT_TYPE_NUMBER) as datum_port_output;
        current_internal_pressure = add_output_port("Internal Pressure", PORT_TYPE_NUMBER) as datum_port_output;
        update_received = add_output_port("Update Received", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:562
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        QDEL_LIST(vent_duplicates);
        base.removed_from(removed_from);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:566
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_airalarm)))
        {
            connected_alarm = shell as obj_machinery_airalarm;
            vents.possible_options = extract_id_tags(connected_alarm.my_area.air_vents) as DmList;
        }
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:572
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        connected_alarm = null;
        vents.possible_options = null;
        foreach (obj_item_circuit_component_air_alarm_vents vent in Dm.each_cast<obj_item_circuit_component_air_alarm_vents>(vent_duplicates))
            vent.connected_alarm = null;
        return base.unregister_usb_parent(shell);
    }

    private obj_machinery_atmospherics_components_unary_vent_pump find_vent()
        => find_by_id_tag(connected_alarm.my_area.air_vents, vents.value) as obj_machinery_atmospherics_components_unary_vent_pump;

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:579
    [ShouldNotSleep]
    public virtual void toggle_vent(datum_port_input port = null)
    {
        Waiting.DetachSync(() => toggle_vent_async(port));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:583
    public virtual void toggle_vent_async(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        obj_machinery_atmospherics_components_unary_vent_pump vent = find_vent();
        if (vent == null)
            return;

        vent.on = Dm.num(port == enable);
        vent.update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:594
    [ShouldNotSleep]
    public virtual void toggle_external(datum_port_input port = null)
    {
        Waiting.DetachSync(() => toggle_external_async(port));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:598
    public virtual void toggle_external_async(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        obj_machinery_atmospherics_components_unary_vent_pump vent = find_vent();
        if (vent == null)
            return;

        if (port == enable_external)
            vent.pressure_checks = Dm.bitor(vent.pressure_checks, ATMOS_EXTERNAL_BOUND);
        else
            vent.pressure_checks = Dm.bitand(vent.pressure_checks, Dm.bitnot(ATMOS_EXTERNAL_BOUND));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:611
    [ShouldNotSleep]
    public virtual void toggle_internal(datum_port_input port = null)
    {
        Waiting.DetachSync(() => toggle_internal_async(port));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:615
    public virtual void toggle_internal_async(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        obj_machinery_atmospherics_components_unary_vent_pump vent = find_vent();
        if (vent == null)
            return;

        if (port == enable_internal)
            vent.pressure_checks = Dm.bitor(vent.pressure_checks, ATMOS_INTERNAL_BOUND);
        else
            vent.pressure_checks = Dm.bitand(vent.pressure_checks, Dm.bitnot(ATMOS_INTERNAL_BOUND));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:628
    [ShouldNotSleep]
    public virtual void set_internal_pressure(datum_port_input port = null)
    {
        Waiting.DetachSync(() => set_internal_pressure_async(port));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:632
    public virtual void set_internal_pressure_async(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        obj_machinery_atmospherics_components_unary_vent_pump vent = find_vent();
        if (vent == null)
            return;

        vent.internal_pressure_bound = clamp(Dm.num(internal_pressure.value), 0, ATMOS_PUMP_MAX_PRESSURE);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:642
    [ShouldNotSleep]
    public virtual void set_external_pressure(datum_port_input port = null)
    {
        Waiting.DetachSync(() => set_external_pressure_async(port));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:646
    public virtual void set_external_pressure_async(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        obj_machinery_atmospherics_components_unary_vent_pump vent = find_vent();
        if (vent == null)
            return;

        // tg writes the external pressure input into the internal bound; kept as tg has it
        vent.internal_pressure_bound = clamp(Dm.num(external_pressure.value), 0, ATMOS_PUMP_MAX_PRESSURE);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:656
    [ShouldNotSleep]
    public virtual void toggle_siphon(datum_port_input port = null)
    {
        Waiting.DetachSync(() => toggle_siphon_async(port));
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:660
    public virtual void toggle_siphon_async(datum_port_input port = null)
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        obj_machinery_atmospherics_components_unary_vent_pump vent = find_vent();
        if (vent == null)
            return;

        vent.pump_direction = port == enable_siphon ? ATMOS_DIRECTION_SIPHONING : ATMOS_DIRECTION_RELEASING;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:670
    [ShouldNotSleep]
    public virtual void update_data()
    {
        if (connected_alarm == null || Dm.truthy(connected_alarm.locked))
            return;

        obj_machinery_atmospherics_components_unary_vent_pump vent = find_vent();
        if (vent == null)
            return;

        enabled.set_value(vent.on);
        is_siphoning.set_value(Dm.num(vent.pump_direction == ATMOS_DIRECTION_SIPHONING));
        internal_on.set_value(Dm.not(Dm.not(Dm.bitand(vent.pressure_checks, ATMOS_INTERNAL_BOUND))));
        current_internal_pressure.set_value(vent.internal_pressure_bound);
        external_on.set_value(Dm.not(Dm.not(Dm.bitand(vent.pressure_checks, ATMOS_EXTERNAL_BOUND))));
        current_external_pressure.set_value(vent.external_pressure_bound);
        update_received.set_value(COMPONENT_SIGNAL);
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:547
public partial class obj_item_circuit_component_air_alarm_vents_duplicate : obj_item_circuit_component_air_alarm_vents
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:553
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        if (!QDELING(this))
            qdel(this);
        base.removed_from(removed_from);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:558
    public override object Destroy(float? force = null)
    {
        connected_alarm = null;
        return base.Destroy(force);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:22
    internal DmList obj_item_circuit_component_air_alarm_general__options_map;
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:159  (var/static/list/component_options in /obj/item/circuit_component/air_alarm/populate_options)
    internal DmList obj_item_circuit_component_air_alarm__populate_options__component_options;
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:280
    internal DmList obj_item_circuit_component_air_alarm_scrubbers__filtering_map = Dm.list();
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:353  (var/static/list/meta_data in /obj/item/circuit_component/air_alarm_scrubbers/get_ui_notices)
    internal DmList obj_item_circuit_component_air_alarm_scrubbers__get_ui_notices__meta_data = Dm.list();
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_circuit.dm:500
    internal DmList obj_item_circuit_component_air_alarm_vents__filtering_map = Dm.list();
}
