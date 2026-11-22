// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/powermonitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//normal computer version is located in code\modules\power\monitor.dm, /obj/machinery/computer/monitor
// The whole file (gaps S4b, D327): AmpCheck, the engineering console's power monitor, with its NtosPowerMonitor window (the power
// monitoring console's PowerMonitor content). The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/powermonitor.dm:3
public partial class datum_computer_file_program_power_monitor : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/powermonitor.dm:27
    public override async DmTask<object> on_start(mob_living user = null)
    {
        object dot = await base.on_start(user);
        search();
        history["supply"] = Dm.list();
        history["demand"] = Dm.list();
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/powermonitor.dm:34
    public override object process_tick(object seconds_per_tick = null)
    {
        if (!Dm.truthy(get_powernet()))
            search();
        else
            record();
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/powermonitor.dm:40  keep in sync with /obj/machinery/computer/monitor's version
    public virtual void search()
    {
        turf T = get_turf(computer) as turf;
        attached_wire_ref = WEAKREF(Dm.locate<obj_structure_cable>(T)) as datum_weakref;
        if (attached_wire_ref != null)
            return;
        area A = get_area(computer) as area; //if the computer isn't directly connected to a wire, attempt to find the APC powering it to pull its powernet instead
        if (A == null)
            return;
        obj_machinery_power_apc local_apc = A.apc;
        if (local_apc == null)
            return;
        if (local_apc.terminal == null) //this really shouldn't happen without badminnery.
            local_apc = null;
        local_apc_ref = WEAKREF(local_apc) as datum_weakref;
    }

    // tg: code/modules/modular_computers/file_system/programs/powermonitor.dm:55  keep in sync with /obj/machinery/computer/monitor's version
    public virtual object get_powernet()
    {
        obj_structure_cable attached_wire = attached_wire_ref?.resolve() as obj_structure_cable;
        obj_machinery_power_apc local_apc = local_apc_ref?.resolve() as obj_machinery_power_apc;
        if (attached_wire != null || local_apc?.terminal != null)
            return attached_wire != null ? attached_wire.powernet : local_apc.terminal.powernet;
        return FALSE;
    }

    // tg: code/modules/modular_computers/file_system/programs/powermonitor.dm:62  keep in sync with /obj/machinery/computer/monitor's version
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

    // tg: code/modules/modular_computers/file_system/programs/powermonitor.dm:79
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        datum_powernet connected_powernet = get_powernet() as datum_powernet;
        data["stored"] = record_size;
        data["interval"] = record_interval / 10;
        data["attached"] = connected_powernet != null ? TRUE : FALSE;
        if (connected_powernet != null)
        {
            data["supply"] = display_power(connected_powernet.avail);
            data["demand"] = display_power(connected_powernet.load);
        }
        data["history"] = history;

        data["areas"] = Dm.list();
        if (connected_powernet != null)
        {
            foreach (obj_machinery_power_terminal term in Dm.each<obj_machinery_power_terminal>(connected_powernet.nodes))
            {
                obj_machinery_power_apc A = term.master as obj_machinery_power_apc;
                if (A != null)
                {
                    DmList areas = data["areas"] as DmList;
                    Dm.append(ref areas, Dm.list(Dm.list(
                        Dm.kv("name", A.area.name),
                        Dm.kv("charge", A.cell != null ? A.cell.percent() : 0),
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
