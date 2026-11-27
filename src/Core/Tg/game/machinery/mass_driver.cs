// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/mass_driver.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the mass driver throws what lies on it to the edge of the map in its direction (the ordnance range's, the
// chapel's, the disposals' and the shack's drivers), with its launch flick, its wires (wires/mass_driver.cs), its servos' range
// and its EMP misfire. The var block is generated (D111).

// tg: code/game/machinery/mass_driver.dm:1
public partial class obj_machinery_mass_driver : obj_machinery
{
    // tg: code/game/machinery/mass_driver.dm:14
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        wires = Dm.@new<datum_wires_mass_driver>(this);
        return dot;
    }

    // tg: code/game/machinery/mass_driver.dm:31
    public override object Destroy(float? force = null)
    {
        foreach (obj_machinery_computer_pod control in Dm.each_cast<obj_machinery_computer_pod>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_pod))))
        {
            if (Dm.eq(control.id, id))
                control.connected = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/machinery/mass_driver.dm:37
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        await DmTask.CompletedTask;
        id = Dm.text("[]_[]", port.shuttle_id, id);
        return null;
    }

    // tg: code/game/machinery/mass_driver.dm:40
    public virtual async DmTask drive(object amount = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))) || Dm.truthy(panel_open))
            return;
        await use_energy(power_per_obj);
        float O_limit = 0;
        atom target = get_edge_target_turf(this, dir) as atom;
        foreach (atom_movable O in Dm.each<atom_movable>(loc))
        {
            if (!Dm.truthy(O.anchored) || ismecha(O)) //Mechs need their launch platforms.
            {
                if (ismob(O) && !isliving(O))
                    continue;
                O_limit++;
                if (O_limit >= 20)
                {
                    audible_message(span_notice(I18n.T("[] lets out a screech, it doesn't seem to be able to handle the load.", this)));
                    break;
                }
                await use_energy(power_per_obj);
                await O.throw_at(target, drive_range * Dm.num(power), power);
            }
        }
        flick(Dm.text("[]1", base_icon_state), this);
    }

    // tg: code/game/machinery/mass_driver.dm:58
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]_o", base_icon_state) : (string)base_icon_state;
        return dot;
    }

    // tg: code/game/machinery/mass_driver.dm:62
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(is_wire_tool(tool)) && Dm.truthy(panel_open))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/mass_driver.dm:69
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/mass_driver.dm:72
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        return await default_change_direction_wrench(user, tool);
    }

    // tg: code/game/machinery/mass_driver.dm:75
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/mass_driver.dm:78
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        foreach (datum_stock_part_servo new_servo in Dm.each<datum_stock_part_servo>(component_parts))
            drive_range += Dm.num(new_servo.tier) * 10;
        return dot;
    }

    // tg: code/game/machinery/mass_driver.dm:83
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))) || Dm.truthy(panel_open))
            return dot;
        await drive();
        return dot;
    }
}
