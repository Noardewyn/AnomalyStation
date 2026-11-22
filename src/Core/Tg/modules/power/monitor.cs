// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/monitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 (the power monitor, D162): the whole file, the power monitoring console. It joins the late process queue of SSmachines, finds its
// network through a cable under it or the APC of its area, records the supply and the demand (tg's 60 points, 50 deciseconds
// apart) and lists the network's APCs in its PowerMonitor window. The modular computer program of the same name
// (modular_computers/file_system/programs/powermonitor.dm) is NTOS (modular computers, after the core version). The var block is generated.

// tg: code/modules/power/monitor.dm:4
public partial class obj_machinery_computer_monitor : obj_machinery_computer
{
    // tg: code/modules/power/monitor.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //Add to the late process queue to record the accurate power usage data
        // rules 10.6: tg's Master creates SSmachines before the map; a test harness world may lack it.
        if (SSmachines != null)
            Dm.append(ref SSmachines.processing_late, this);
        search();
        history["supply"] = Dm.list();
        history["demand"] = Dm.list();
        return dot;
    }

    // tg: code/modules/power/monitor.dm:28
    public override object Destroy(float? force = null)
    {
        if (SSmachines != null)
            Dm.remove(ref SSmachines.processing_late, this);
        return base.Destroy(force);
    }

    // tg: code/modules/power/monitor.dm:32
    public override object process_late()
    {
        if (!Dm.truthy(get_powernet()))
        {
            update_use_power(IDLE_POWER_USE);
            search();
        }
        else
        {
            update_use_power(ACTIVE_POWER_USE);
            record();
        }
        return null;
    }

    // tg: code/modules/power/monitor.dm:40  keep in sync with /obj/machinery/computer/monitor's version
    public virtual void search()
    {
        turf T = get_turf(this) as turf;
        attached_wire_ref = WEAKREF(Dm.locate<obj_structure_cable>(T)) as datum_weakref;
        if (attached_wire_ref != null)
            return;
        area A = get_area(this) as area; //if the computer isn't directly connected to a wire, attempt to find the APC powering it to pull its powernet instead
        if (A == null)
            return;
        obj_machinery_power_apc local_apc = A.apc;
        if (local_apc == null)
            return;
        if (local_apc.terminal == null) //this really shouldn't happen without badminnery.
            local_apc = null;
        local_apc_ref = WEAKREF(local_apc) as datum_weakref;
    }

    // tg: code/modules/power/monitor.dm:55  keep in sync with /datum/computer_file/program/power_monitor's version
    public virtual object get_powernet()
    {
        obj_structure_cable attached_wire = attached_wire_ref?.resolve() as obj_structure_cable;
        obj_machinery_power_apc local_apc = local_apc_ref?.resolve() as obj_machinery_power_apc;
        if (attached_wire != null || local_apc?.terminal != null)
            return attached_wire != null ? attached_wire.powernet : local_apc.terminal.powernet;
        return FALSE;
    }

    // tg: code/modules/power/monitor.dm:62  keep in sync with /datum/computer_file/program/power_monitor's version
    public virtual void record()
    {
        if (Dm.world.time >= next_record)
        {
            next_record = Dm.world.time + record_interval;

            datum_powernet connected_powernet = get_powernet() as datum_powernet;

            DmList supply = history["supply"] as DmList;
            if (connected_powernet != null)
                Dm.append(ref supply, energy_to_power(connected_powernet.avail));
            if (supply.len > record_size)
                supply.Cut(1, 2);

            DmList demand = history["demand"] as DmList;
            if (connected_powernet != null)
                Dm.append(ref demand, energy_to_power(connected_powernet.load));
            if (demand.len > record_size)
                demand.Cut(1, 2);
        }
    }

    // tg: code/modules/power/monitor.dm:80
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "PowerMonitor", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/power/monitor.dm:87
    public override async DmTask<object> ui_data(mob user = null)
    {
        datum_powernet connected_powernet = get_powernet() as datum_powernet;
        DmList data = Dm.list();
        data["stored"] = record_size;
        data["interval"] = record_interval / 10;
        data["attached"] = connected_powernet != null ? TRUE : FALSE;
        data["history"] = history;
        data["areas"] = Dm.list();

        if (connected_powernet != null)
        {
            data["supply"] = display_power(connected_powernet.avail);
            data["demand"] = display_power(connected_powernet.load);
            foreach (obj_machinery_power_terminal term in Dm.each<obj_machinery_power_terminal>(connected_powernet.nodes))
            {
                obj_machinery_power_apc A = term.master as obj_machinery_power_apc;
                if (A != null)
                {
                    object cell_charge;
                    if (A.cell == null)
                        cell_charge = 0;
                    else
                        cell_charge = A.cell.percent();
                    DmList areas = data["areas"] as DmList;
                    Dm.append(ref areas, Dm.list(Dm.list(
                        Dm.kv("name", A.area.name),
                        Dm.kv("charge", cell_charge),
                        Dm.kv("load", display_power(A.lastused_total)),
                        Dm.kv("charging", A.charging),
                        Dm.kv("eqp", A.equipment),
                        Dm.kv("lgt", A.lighting),
                        Dm.kv("env", A.environ))));
                }
            }
        }

        return data;
    }
}
