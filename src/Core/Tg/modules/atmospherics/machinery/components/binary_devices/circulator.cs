// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/binary_devices/circulator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering package E11: the whole file. The thermoelectric generator's circulator (heat exchanger): it passes half the pressure
// difference between its input and output pipes to its generator each atmos tick, shows its anchor, panel, fan, flow and mode
// display overlays, connects to pipes on its two sides when anchored, and its multitool switches it between hot and cold. Not here:
// the flow's emissive glow (DV12). The var block is generated.

//node2, air2, network2 correspond to input
//node1, air1, network1 correspond to output

// tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:4
public partial class obj_machinery_atmospherics_components_binary_circulator : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:21
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:29
    public override object Destroy(float? force = null)
    {
        force ??= FALSE;
        if (generator != null)
            disconnectFromGenerator();
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:34
    public virtual object return_transfer_air()
    {
        datum_gas_mixture air1 = airs[1] as datum_gas_mixture;
        datum_gas_mixture air2 = airs[2] as datum_gas_mixture;

        float output_starting_pressure = Dm.num(air1.return_pressure());
        float input_starting_pressure = Dm.num(air2.return_pressure());

        if (output_starting_pressure >= input_starting_pressure - 10)
        {
            //Need at least 10 KPa difference to overcome friction in the mechanism
            last_pressure_delta = 0;
            return null;
        }

        //Calculate necessary moles to transfer using PV = nRT
        if (air2.temperature <= 0)
        {
            last_pressure_delta = 0;
            return null;
        }
        float pressure_delta = (input_starting_pressure - output_starting_pressure) / 2;
        float transfer_moles = (pressure_delta * Dm.num(air1.volume)) / (air2.temperature * R_IDEAL_GAS_EQUATION);
        last_pressure_delta = pressure_delta;
        //Actually transfer the gas
        datum_gas_mixture removed = air2.remove(transfer_moles) as datum_gas_mixture;
        update_parents();
        return removed;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:59
    [DmParams()]
    public override object process_atmos(object seconds_per_tick = null)
    {
        update_appearance(UPDATE_ICON);
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:62
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        cut_overlays();
        if (Dm.truthy(anchored))
            add_overlay("circ_anchor");
        add_overlay(Dm.text("panel_[]", panel_open));

        if (!Dm.truthy(is_operational))
        {
            add_overlay(Dm.text("fan_[]", mode));
            add_overlay("flow");
            add_overlay("display");
            return dot;
        }

        add_overlay("flow_on");
        // DEVIATION(DV12): add_overlay(emissive_appearance(icon, "flow_on-emissive", src, alpha = src.alpha)), the glow on the emissive plane, is not created

        add_overlay(Dm.text("display_[]", mode));
        if (last_pressure_delta > 0)
            add_overlay(Dm.text("fan_[]_[]", mode, Dm.num(last_pressure_delta > ONE_ATMOSPHERE)));
        else
            add_overlay(Dm.text("fan_[]", mode));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:84
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open the panel!"));
            return null;
        }
        await set_anchored(Dm.truthy(anchored) ? FALSE : TRUE);
        tool.play_tool_sound(this);
        if (generator != null)
            disconnectFromGenerator();
        balloon_alert(user, Dm.truthy(anchored) ? I18n.T("secure") : I18n.T("unsecure"));

        obj_machinery_atmospherics node1 = nodes[1] as obj_machinery_atmospherics;
        obj_machinery_atmospherics node2 = nodes[2] as obj_machinery_atmospherics;

        if (node1 != null)
        {
            node1.disconnect(this);
            nodes[1] = null;
            if (Dm.truthy(parents[1]))
                nullify_pipenet(parents[1] as datum_pipeline);
        }

        if (node2 != null)
        {
            node2.disconnect(this);
            nodes[2] = null;
            if (Dm.truthy(parents[2]))
                nullify_pipenet(parents[2] as datum_pipeline);
        }

        if (Dm.truthy(anchored))
        {
            set_init_directions();
            atmos_init();
            node1 = nodes[1] as obj_machinery_atmospherics;
            if (node1 != null)
            {
                node1.atmos_init();
                node1.add_member(this);
            }
            node2 = nodes[2] as obj_machinery_atmospherics;
            if (node2 != null)
            {
                node2.atmos_init();
                node2.add_member(this);
            }
            SSair.add_to_rebuild_queue(this);
        }

        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:124
    [DmParams()]
    public override void set_init_directions(object init_dir = null)
    {
        float sw = dir;
        if (sw == NORTH || sw == SOUTH)
            initialize_directions = Dm.bitor(EAST, WEST);
        else if (sw == EAST || sw == WEST)
            initialize_directions = Dm.bitor(NORTH, SOUTH);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:131
    public override object get_node_connects()
    {
        if (Dm.truthy(flipped))
            return Dm.list(turn(dir, 270), turn(dir, 90));
        return Dm.list(turn(dir, 90), turn(dir, 270));
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:136
    public override object can_be_node(obj_machinery_atmospherics target = null)
    {
        if (Dm.truthy(anchored))
            return base.can_be_node(target);
        return FALSE;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:141
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (generator != null)
            disconnectFromGenerator();
        mode = Dm.truthy(mode) ? FALSE : TRUE;
        balloon_alert(user, I18n.T("set to []", Dm.truthy(mode) ? I18n.T("cold") : I18n.T("hot")));
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:148
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

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:157
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await default_deconstruction_crowbar(user, tool)))
            return TRUE;
        return await base.crowbar_act(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:162
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (generator != null)
            disconnectFromGenerator();
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:167
    public virtual void disconnectFromGenerator()
    {
        if (Dm.truthy(mode))
            generator.cold_circ = null;
        else
            generator.hot_circ = null;
        generator.update_appearance(UPDATE_ICON);
        generator = null;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/circulator.dm:175
    public override void set_piping_layer(object new_layer = null)
    {
        base.set_piping_layer(new_layer);
        pixel_x = 0;
        pixel_y = 0;
    }
}
