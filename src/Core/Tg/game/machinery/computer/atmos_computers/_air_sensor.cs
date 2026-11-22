// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/atmos_computers/_air_sensor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Package E5 (the atmospherics control consoles): the whole file. Deferred leaves: the multitool screentips of the sensor and
// the item's screentip context (examine and the context tips: D264). Generated live-partial stubs: both examines and the
// item's add_context (D264). The var blocks are generated.

/// Gas tank air sensor.
/// These always hook to monitors, be mindful of them
// tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:9
public partial class obj_machinery_air_sensor : obj_machinery
{
    ///Indicator for inlet port
    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:2  #define INLET 1 (file-local, #undef at :262)
    private const int INLET = 1;
    ///Indicator for outlet port
    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:4  #define OUTLET 2 (file-local, #undef at :263)
    private const int OUTLET = 2;

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:27
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        id_tag = assign_random_name();

        // var/static/list/multitool_tips (TOOL_MULTITOOL: "Link logged injectors/vents", "Reset all I/O ports") and
        // AddElement(/datum/element/contextual_screentip_tools, multitool_tips): screentips (D264)
        Dm.Deferred("code/game/machinery/computer/atmos_computers/_air_sensor.dm:30", "Initialize: AddElement(/datum/element/contextual_screentip_tools, multitool_tips): screentips (not now, by the owner: D264)");

        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:40
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        //auto connect to any inlet & outlet devices within a 4 diameter radius from this sensor
        foreach (obj_machinery_atmospherics_components_unary device in Dm.each<obj_machinery_atmospherics_components_unary>(oview(4, this)))
        {
            if (Dm.truthy(inlet_id) && Dm.truthy(outlet_id))
                break;
            configure(device);
        }
    }

    /**
     * Connects an injector or an vent pump to this air sensor. Must be on the same z level as sensor
     * return what type of port was configured or NONE for no op
     *
     * Arguments
     * * obj/machinery/atmospherics/components/unary/port - the device we are trying to connect to this sensor
     * * reconfigure - if TRUE it will override existing ports if they are already registered
    */
    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:57
    public virtual object configure(obj_machinery_atmospherics_components_unary port = null, float? reconfigure = null)
    {
        reconfigure ??= FALSE;
        // PRIVATE_PROC(TRUE)
        if (port == null || port.z != z)
            return NONE;

        if (port is obj_machinery_atmospherics_components_unary_outlet_injector input)
        {
            if (!Dm.truthy(reconfigure) && Dm.truthy(inlet_id))
                return INLET;
            //only configure non maploaded injectors cause they already have a preset config
            if (input.GetType() == typeof(obj_machinery_atmospherics_components_unary_outlet_injector))
                input.volume_rate = MAX_TRANSFER_RATE;
            inlet_id = input.id_tag;
            return INLET;
        }
        else if (port is obj_machinery_atmospherics_components_unary_vent_pump output)
        {
            if (!Dm.truthy(reconfigure) && Dm.truthy(outlet_id))
                return OUTLET;
            //only configure non maploaded vent pumps cause they already have a preset config
            if (output.GetType() == typeof(obj_machinery_atmospherics_components_unary_vent_pump))
            {
                //so its no longer controlled by air alarm
                output.disconnect_from_area();
                //configuration copied from /obj/machinery/atmospherics/components/unary/vent_pump/siphon but with max pressure
                output.pump_direction = ATMOS_DIRECTION_SIPHONING;
                output.pressure_checks = ATMOS_INTERNAL_BOUND;
                output.internal_pressure_bound = MAX_OUTPUT_PRESSURE;
                output.external_pressure_bound = 0;
            }
            //finally assign it to this sensor
            outlet_id = output.id_tag;
            return OUTLET;
        }
        return NONE;
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:90
    public override object Destroy(float? force = null)
    {
        reset();
        return base.Destroy(force);
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:94
    public override async DmTask<object> return_air()
    {
        if (!Dm.truthy(on))
            return null;
        object dot = await base.return_air();
        await use_energy(active_power_usage); //use power for analyzing gases
        return dot;
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:100
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        //update appearance according to power state
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            if (Dm.truthy(on))
            {
                on = FALSE;
                update_appearance();
            }
        }
        else if (!Dm.truthy(on))
        {
            on = TRUE;
            update_appearance();
        }
        return null;
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:110  examine: the generated live-partial stub (D264)

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:110
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Use a multitool to link it to an injector, vent, or air alarm, or reset its ports.")));
        Dm.append(ref dot, span_notice(I18n.T("Click with hand to turn it off.")));
        return dot;
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:115
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);

        //switched off version of this air sensor but still anchored to the ground
        obj_item_air_sensor sensor = Dm.@new<obj_item_air_sensor>(drop_location());
        await sensor.set_anchored(TRUE);
        sensor.balloon_alert(user, I18n.T("sensor turned off"));

        //delete self
        qdel(this);
        return dot;
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:126
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("gsensor[]", on);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:130
    public virtual void reset()
    {
        inlet_id = null;
        outlet_id = null;
        if (connected_airalarm != null)
        {
            connected_airalarm.disconnect_sensor();
            // if air alarm and sensor were linked at roundstart we allow them to link to new devices
            connected_airalarm.allow_link_change = TRUE;
            connected_airalarm = null;
        }
    }

    ///right click with multi tool to disconnect everything
    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:140
    public override async DmTask<object> multitool_act_secondary(mob_living user = null, obj_item tool = null)
    {
        reset();
        balloon_alert(user, I18n.T("ports reset"));
        return TRUE;
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:145
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multi_tool = (obj_item_multitool)tool;
        await base.multitool_act(user, multi_tool);

        object type = configure(multi_tool.buffer as obj_machinery_atmospherics_components_unary, TRUE);
        if (Dm.eq(type, INLET) || Dm.eq(type, OUTLET))
        {
            string port = I18n.T("[] port", Dm.eq(type, INLET) ? I18n.T("input") : I18n.T("output"));
            user.balloon_alert(user, I18n.T("[] configured", port));
            to_chat(user, span_notice(I18n.T("[] has connected [] to its [].", this, multi_tool.buffer, port)));
        }
        to_chat(user, span_notice(I18n.T("[] has been added to multitool buffer.", this)));
        multi_tool.set_buffer(this);

        return ITEM_INTERACT_SUCCESS;
    }
}

/**
 * A portable version of the /obj/machinery/air_sensor
 * Wrenching it & turning it on will convert it back to /obj/machinery/air_sensor
 * Unwelding /obj/machinery/air_sensor will turn it back to /obj/item/air_sensor
 * The logic is same as meters
 */
// tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:166
public partial class obj_item_air_sensor : obj_item
{
    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:172
    [DmParams("mapload", "inlet", "outlet")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_context(): screentip contexts (D264)
        Dm.Deferred("code/game/machinery/computer/atmos_computers/_air_sensor.dm:174", "Initialize: register_context(): screentip contexts (not now, by the owner: D264)");
        return dot;
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:176  add_context: the generated live-partial stub (D264)

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:190  examine: the generated live-partial stub (D264)

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:190
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It's [] in place", EXAMINE_HINT(I18n.T("wrenched")))));
        else
            Dm.append(ref dot, span_notice(I18n.T("It should be [] in place to turn it on.", EXAMINE_HINT(I18n.T("wrenched")))));
        Dm.append(ref dot, span_notice(I18n.T("It could be [] apart.", EXAMINE_HINT(I18n.T("welded")))));
        Dm.append(ref dot, span_notice(I18n.T("Click with hand to turn it on.")));
        return dot;
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:199
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(anchored))
            return dot;

        //List of air sensor's by name
        DmList available_sensors = Dm.list();
        foreach (object chamber_id in Dm.each(GLOB.station_gas_chambers))
        {
            //don't let it conflict with existing distro & waste moniter meter's
            if (Dm.eq(chamber_id, ATMOS_GAS_MONITOR_DISTRO))
                continue;
            if (Dm.eq(chamber_id, ATMOS_GAS_MONITOR_WASTE))
                continue;
            Dm.append(available_sensors, GLOB.station_gas_chambers[chamber_id]);
        }

        //make the choice
        object chamber_name = await tgui_input_list(user, I18n.T("Select Sensor Purpose"), I18n.T("Select Sensor ID"), available_sensors);
        if (chamber_name == null)
            return dot;

        //map chamber name back to id
        object target_chamber = null;
        foreach (object chamber_id in Dm.each(GLOB.station_gas_chambers))
        {
            if (!Dm.eq(GLOB.station_gas_chambers[chamber_id], chamber_name))
                continue;
            target_chamber = chamber_id;
            break;
        }

        //build the sensor from the subtypes of sensor's available
        DmList chamber_subtypes = TgState.Current.obj_item_air_sensor__attack_hand__chamber_subtypes ??= subtypesof(typeof(obj_machinery_air_sensor));
        foreach (Type sensor in Dm.each_cast<Type>(chamber_subtypes))
        {
            if (!Dm.eq(Dm.initial(sensor, (obj_machinery_air_sensor p) => p.chamber_id), target_chamber))
                continue;

            //make real air sensor in its place
            obj_machinery_air_sensor new_sensor = (obj_machinery_air_sensor)Dm.@new(sensor, get_turf(this));
            new_sensor.balloon_alert(user, I18n.T("sensor turned on"));
            qdel(this);

            break;
        }
        return dot;
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:242
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        return null;
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:246
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;

        ((atom)loc).balloon_alert(user, I18n.T("dismantling sensor"));
        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 30, amount: 1)))
            return ITEM_INTERACT_BLOCKING;
        ((atom)loc).balloon_alert(user, I18n.T("sensor dismanteled"));

        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:258
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_analyzer>(loc);
        Dm.@new<obj_item_stack_sheet_iron>(loc);
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/computer/atmos_computers/_air_sensor.dm:224  attack_hand's var/static/list/chamber_subtypes (a proc-level static, rules 10.6)
    internal DmList obj_item_air_sensor__attack_hand__chamber_subtypes;
}
