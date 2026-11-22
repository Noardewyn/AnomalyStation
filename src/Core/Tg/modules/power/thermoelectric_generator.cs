// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/thermoelectric_generator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering package E11: the whole file. The thermoelectric generator: it finds a hot and a cold circulator on its two sides,
// exchanges the heat of the gas they pass (tg's 65% efficiency) into its power, which it adds to the cable a tenth at a time, shows
// the output level and the loops' pressure on its overlays, and opens tg's ThermoElectricGenerator window; the panel, the anchoring,
// the multitool's circulator refresh, the rotation and the deconstruction. The var block is generated.

// tg: code/modules/power/thermoelectric_generator.dm:3
public partial class obj_machinery_power_thermoelectric_generator : obj_machinery_power
{
    // tg: code/modules/power/thermoelectric_generator.dm:1  #define TEG_EFFICIENCY 0.65 (file-local)
    internal const float TEG_EFFICIENCY = 0.65f;

    // tg: code/modules/power/thermoelectric_generator.dm:32
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        find_circulators();
        connect_to_network();
        SSair.start_processing_machine(this);
        update_appearance();
        return dot;
    }

    // tg: code/modules/power/thermoelectric_generator.dm:40
    public override object Destroy(float? force = null)
    {
        force ??= FALSE;
        null_circulators();
        SSair.stop_processing_machine(this);
        return base.Destroy(force);
    }

    // tg: code/modules/power/thermoelectric_generator.dm:45
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        null_circulators();
        return null;
    }

    // tg: code/modules/power/thermoelectric_generator.dm:48
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return dot;

        float level = min(round(lastgenlev / 100000), 11);
        if (Dm.truthy(level))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/machines/engine/other.dmi"), Dm.text("[]-op[]", base_icon_state, level)));
        if (hot_circ != null && cold_circ != null)
            Dm.append(ref dot, Dm.text("[]-oc[]", base_icon_state, last_pressure_overlay));
        return dot;
    }

    // tg: code/modules/power/thermoelectric_generator.dm:59
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open the panel!"));
            return null;
        }
        await set_anchored(Dm.truthy(anchored) ? FALSE : TRUE);
        tool.play_tool_sound(this);
        if (Dm.truthy(anchored))
            connect_to_network();
        else
            null_circulators();
        balloon_alert(user, Dm.truthy(anchored) ? I18n.T("secure") : I18n.T("unsecure"));
        return TRUE;
    }

    // tg: code/modules/power/thermoelectric_generator.dm:72
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        if (!Dm.truthy(anchored))
            return dot;
        find_circulators();
        balloon_alert(user, I18n.T("circulators updated"));
        return TRUE;
    }

    // tg: code/modules/power/thermoelectric_generator.dm:80
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("anchor it down!"));
            return null;
        }
        toggle_panel_open();
        tool.play_tool_sound(this);
        balloon_alert(user, I18n.T("panel []", Dm.truthy(panel_open) ? I18n.T("open") : I18n.T("closed")));
        return TRUE;
    }

    // tg: code/modules/power/thermoelectric_generator.dm:89
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/power/thermoelectric_generator.dm:92
    [DmParams()]
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        //Setting this number higher just makes the change in power output slower, it doesnt actualy reduce power output cause **math**
        float power_output = round(lastgen / 10);
        add_avail(power_output);
        lastgenlev = power_output;
        lastgen -= power_output;
        return null;
    }

    // tg: code/modules/power/thermoelectric_generator.dm:99
    [DmParams()]
    public override object process_atmos(object seconds_per_tick = null)
    {
        if (cold_circ == null || hot_circ == null)
            return null;
        if (powernet == null)
            return null;

        datum_gas_mixture cold_air = cold_circ.return_transfer_air() as datum_gas_mixture;
        datum_gas_mixture hot_air = hot_circ.return_transfer_air() as datum_gas_mixture;
        if (cold_air != null && hot_air != null)
        {
            float cold_air_heat_capacity = Dm.num(cold_air.heat_capacity());
            float hot_air_heat_capacity = Dm.num(hot_air.heat_capacity());
            float delta_temperature = hot_air.temperature - cold_air.temperature;
            if (delta_temperature > 0 && cold_air_heat_capacity > 0 && hot_air_heat_capacity > 0)
            {
                float efficiency = TEG_EFFICIENCY;
                float energy_transfer = delta_temperature * hot_air_heat_capacity * cold_air_heat_capacity / (hot_air_heat_capacity + cold_air_heat_capacity);
                float heat = energy_transfer * (1 - efficiency);
                lastgen += energy_transfer * efficiency;
                hot_air.temperature = hot_air.temperature - energy_transfer / hot_air_heat_capacity;
                cold_air.temperature = cold_air.temperature + heat / cold_air_heat_capacity;
            }
        }

        if (hot_air != null)
        {
            datum_gas_mixture hot_circ_air1 = hot_circ.airs[1] as datum_gas_mixture;
            hot_circ_air1.merge(hot_air);
        }

        if (cold_air != null)
        {
            datum_gas_mixture cold_circ_air1 = cold_circ.airs[1] as datum_gas_mixture;
            cold_circ_air1.merge(cold_air);
        }

        string current_pressure = Dm.text("[][]", cold_circ?.last_pressure_delta > 0 ? "1" : "0", hot_circ?.last_pressure_delta > 0 ? "1" : "0");
        if (current_pressure != last_pressure_overlay)
            //this requires an update to overlays.
            last_pressure_overlay = current_pressure;

        update_appearance(UPDATE_ICON);
        return null;
    }

    // tg: code/modules/power/thermoelectric_generator.dm:134
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ThermoElectricGenerator", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/power/thermoelectric_generator.dm:140
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["error_message"] = null;
        if (powernet == null)
        {
            data["error_message"] = I18n.T("Unable to connect to the power network!");
            return data;
        }
        if (cold_circ == null && hot_circ == null)
        {
            data["error_message"] = I18n.T("Unable to locate any parts! Multitool the machine to sync to nearby parts.");
            return data;
        }
        if (cold_circ == null)
        {
            data["error_message"] = I18n.T("Unable to locate cold circulator!");
            return data;
        }
        if (hot_circ == null)
        {
            data["error_message"] = I18n.T("Unable to locate hot circulator!");
            return data;
        }

        datum_gas_mixture cold_circ_air1 = cold_circ.airs[1] as datum_gas_mixture;
        datum_gas_mixture cold_circ_air2 = cold_circ.airs[2] as datum_gas_mixture;

        datum_gas_mixture hot_circ_air1 = hot_circ.airs[1] as datum_gas_mixture;
        datum_gas_mixture hot_circ_air2 = hot_circ.airs[2] as datum_gas_mixture;

        data["last_power_output"] = display_power(lastgenlev);

        DmList cold_data = Dm.list();
        cold_data["temperature_inlet"] = round(cold_circ_air2.temperature, 0.1f);
        cold_data["temperature_outlet"] = round(cold_circ_air1.temperature, 0.1f);
        cold_data["pressure_inlet"] = round(Dm.num(cold_circ_air2.return_pressure()), 0.1f);
        cold_data["pressure_outlet"] = round(Dm.num(cold_circ_air1.return_pressure()), 0.1f);
        data["cold_data"] = Dm.list(cold_data);

        DmList hot_data = Dm.list();
        hot_data["temperature_inlet"] = round(hot_circ_air2.temperature, 0.1f);
        hot_data["temperature_outlet"] = round(hot_circ_air1.temperature, 0.1f);
        hot_data["pressure_inlet"] = round(Dm.num(hot_circ_air2.return_pressure()), 0.1f);
        hot_data["pressure_outlet"] = round(Dm.num(hot_circ_air1.return_pressure()), 0.1f);
        data["hot_data"] = Dm.list(hot_data);

        return data;
    }

    ///Finds and connects nearby valid circulators to the machine, nulling out previous ones.
    // tg: code/modules/power/thermoelectric_generator.dm:181
    public virtual void find_circulators()
    {
        null_circulators();
        DmList valid_circulators = Dm.list();

        if (Dm.truthy(Dm.bitand(dir, Dm.bitor(NORTH, SOUTH))))
        {
            obj_machinery_atmospherics_components_binary_circulator east_circulator = Dm.locate<obj_machinery_atmospherics_components_binary_circulator>(get_step(this, EAST));
            if (east_circulator != null && east_circulator.dir == WEST)
                Dm.append(valid_circulators, east_circulator);
            obj_machinery_atmospherics_components_binary_circulator west_circulator = Dm.locate<obj_machinery_atmospherics_components_binary_circulator>(get_step(this, WEST));
            if (west_circulator != null && west_circulator.dir == EAST)
                Dm.append(valid_circulators, west_circulator);
        }
        else
        {
            obj_machinery_atmospherics_components_binary_circulator north_circulator = Dm.locate<obj_machinery_atmospherics_components_binary_circulator>(get_step(this, NORTH));
            if (north_circulator != null && north_circulator.dir == SOUTH)
                Dm.append(valid_circulators, north_circulator);
            obj_machinery_atmospherics_components_binary_circulator south_circulator = Dm.locate<obj_machinery_atmospherics_components_binary_circulator>(get_step(this, SOUTH));
            if (south_circulator != null && south_circulator.dir == NORTH)
                Dm.append(valid_circulators, south_circulator);
        }

        if (!Dm.truthy(valid_circulators.len))
            return;

        foreach (obj_machinery_atmospherics_components_binary_circulator circulators in Dm.each_cast<obj_machinery_atmospherics_components_binary_circulator>(valid_circulators))
        {
            if (Dm.eq(circulators.mode, CIRCULATOR_COLD) && cold_circ == null)
            {
                cold_circ = circulators;
                circulators.generator = this;
                continue;
            }
            if (Dm.eq(circulators.mode, CIRCULATOR_HOT) && hot_circ == null)
            {
                hot_circ = circulators;
                circulators.generator = this;
            }
        }
    }

    ///Removes hot and cold circulators from the generator, nulling them.
    // tg: code/modules/power/thermoelectric_generator.dm:213
    public virtual void null_circulators()
    {
        if (hot_circ != null)
        {
            hot_circ.generator = null;
            hot_circ = null;
        }
        if (cold_circ != null)
        {
            cold_circ.generator = null;
            cold_circ = null;
        }
    }
}
