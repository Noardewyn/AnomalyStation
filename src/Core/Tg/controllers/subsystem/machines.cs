// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/machines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P3. The var block is generated (D111); this part adds the procs. M7 slice 1: the power networks (makepowernets, the
// powernets' reset in fire, setup_template_powernets). M7 slice 3 (the APC): the APC steps of fire (early_process,
// charge_channel per channel, late_process).

// tg: code/controllers/subsystem/machines.dm:1 SUBSYSTEM_DEF(machines)
public partial class datum_controller_subsystem_machines : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(machines) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSmachines)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/machines.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSmachines != this)
        {
            if (SSmachines is datum_controller_subsystem_machines)
            {
                Recover();
                qdel(SSmachines);
            }
            SSmachines = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/machines.dm:32
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        makepowernets();
        await fire();
        return SS_INIT_SUCCESS;
    }

    /// Registers a machine with the machine subsystem; should only be called by the machine itself during its creation.
    // tg: code/controllers/subsystem/machines.dm:38
    public virtual void register_machine(obj_machinery machine = null)
    {
        // LAZYADD(machines_by_type[machine.type], machine): the list element through a temp (the macro assigns it, rules 11.1)
        DmList t__1 = machines_by_type[machine.type] as DmList;
        LAZYADD(ref t__1, machine);
        machines_by_type[machine.type] = t__1;
        Dm.combine(ref all_machines, machine);
    }

    /// Removes a machine from the machine subsystem; should only be called by the machine itself inside Destroy.
    // tg: code/controllers/subsystem/machines.dm:43
    public virtual void unregister_machine(obj_machinery machine = null)
    {
        DmList existing = machines_by_type[machine.type] as DmList;
        Dm.remove(ref existing, machine);
        if (!Dm.truthy(length(existing)))
            Dm.remove(ref machines_by_type, machine.type);
        Dm.remove(ref all_machines, machine);
    }

    /// Gets a list of all machines that are either the passed type or a subtype.
    // tg: code/controllers/subsystem/machines.dm:51
    public virtual object get_machines_by_type_and_subtypes(object machine_type = null)
    {
        if (!ispath(machine_type))
            machine_type = ((datum)machine_type).type;
        if (!ispath(machine_type, typeof(obj_machinery)))
            throw Dm.CRASH(Dm.text("called get_machines_by_type_and_subtypes with a non-machine type []", machine_type), "code/controllers/subsystem/machines.dm", 55);
        DmList machines = Dm.list();
        foreach (object next_type in Dm.each(typesof(machine_type as Type)))
        {
            DmList found_machines = machines_by_type[next_type] as DmList;
            if (Dm.truthy(found_machines))
                Dm.append(ref machines, found_machines);
        }
        return machines;
    }


    /// Gets a list of all machines that are the exact passed type.
    // tg: code/controllers/subsystem/machines.dm:65
    public virtual object get_machines_by_type(object machine_type = null)
    {
        if (!ispath(machine_type))
            machine_type = ((datum)machine_type).type;
        if (!ispath(machine_type, typeof(obj_machinery)))
            throw Dm.CRASH(Dm.text("called get_machines_by_type with a non-machine type []", machine_type), "code/controllers/subsystem/machines.dm", 69);

        DmList machines = machines_by_type[machine_type] as DmList;
        return (machines?.Copy() is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.list());
    }

    // tg: code/controllers/subsystem/machines.dm:74
    public virtual object get_all_machines()
    {
        return all_machines.Copy();
    }

    // tg: code/controllers/subsystem/machines.dm:77
    public virtual void makepowernets()
    {
        foreach (datum_powernet power_network in Dm.each_cast<datum_powernet>(powernets))
            qdel(power_network);
        powernets.Cut();

        foreach (obj_structure_cable power_cable in Dm.each_cast<obj_structure_cable>(GLOB.cable_list))
        {
            if (!Dm.truthy(power_cable.powernet))
            {
                datum_powernet new_powernet = Dm.@new<datum_powernet>();
                new_powernet.add_cable(power_cable);
                propagate_network(power_cable, power_cable.powernet);
            }
        }
    }

    // tg: code/controllers/subsystem/machines.dm:88
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/machines.dm:88", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/machines.dm:92
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
        {
            foreach (datum_powernet powernet in Dm.each_cast<datum_powernet>(powernets))
                await powernet.reset(); //reset the power state.
            current_part = SSMACHINES_MACHINES_EARLY;
            this.currentrun = processing_early.Copy();
        }

        //Processing machines that get the priority power draw
        if (current_part == SSMACHINES_MACHINES_EARLY)
        {
            //cache for sanic speed (lists are references anyways)
            DmList currentrun = this.currentrun;
            while (Dm.truthy(currentrun.len))
            {
                obj_machinery thing = currentrun[currentrun.len] as obj_machinery;
                currentrun.len--;
                bool kill__1 = QDELETED(thing);
                if (!kill__1)
                {
                    // RULE-GAP: rules 9.2 has no form for a boundary call inside an || condition: its value goes to the temp t__1.
                    object t__1 = null;
                    try { t__1 = thing.process_early(wait * 0.1f); }
                    catch (Exception e) { World.Current.ReportRuntime(e); t__1 = null; } // isolation boundary (translation-rules.md 9.2)
                    kill__1 = Dm.eq(t__1, PROCESS_KILL);
                }
                if (kill__1)
                {
                    Dm.remove(ref processing_early, thing);
                    Dm.mask(ref thing.datum_flags, Dm.bitnot(DF_ISPROCESSING));
                }
                if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                    return;
            }
            current_part = Dm.num(apc_steps[1]);
            this.currentrun = processing_apcs.Copy();
        }

        //Processing APCs
        while (Dm.@in(current_part, apc_steps))
        {
            //cache for sanic speed (lists are references anyways)
            DmList currentrun = this.currentrun;
            while (Dm.truthy(currentrun.len))
            {
                obj_machinery_power_apc apc = currentrun[currentrun.len] as obj_machinery_power_apc;
                currentrun.len--;
                if (QDELETED(apc))
                {
                    Dm.remove(ref processing_apcs, apc);
                    Dm.mask(ref apc.datum_flags, Dm.bitnot(DF_ISPROCESSING));
                }
                float sw__1 = current_part;
                if (sw__1 == SSMACHINES_APCS_EARLY)
                    await apc.early_process(); // the argument wait * 0.1 is dropped: early_process declares none (rules 7.3)
                else if (sw__1 == SSMACHINES_APCS_LATE)
                {
                    await apc.charge_channel(null, wait * 0.1f);
                    apc.late_process(wait * 0.1f);
                }
                else
                    await apc.charge_channel(current_part, wait * 0.1f);
                if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                    return;
            }
            float next_index = apc_steps.Find(current_part) + 1;
            if (next_index > apc_steps.len)
            {
                current_part = SSMACHINES_MACHINES;
                this.currentrun = processing.Copy();
                break;
            }
            current_part = Dm.num(apc_steps[next_index]);
            this.currentrun = processing_apcs.Copy();
        }

        //Processing all machines
        if (current_part == SSMACHINES_MACHINES)
        {
            //cache for sanic speed (lists are references anyways)
            DmList currentrun = this.currentrun;
            while (Dm.truthy(currentrun.len))
            {
                // tg types it /obj/machinery, but DM calls process() on whatever the list holds: the stove component processes here
                // (stove_component.dm:93, gaps H2), so the port holds it as the datum that declares process
                datum thing = currentrun[currentrun.len] as datum;
                currentrun.len--;
                bool kill__2 = QDELETED(thing);
                if (!kill__2)
                {
                    // RULE-GAP: rules 9.2 has no form for a boundary call inside an || condition: its value goes to the temp t__2.
                    object t__2 = null;
                    try { t__2 = thing.process(wait * 0.1f); }
                    catch (Exception e) { World.Current.ReportRuntime(e); t__2 = null; } // isolation boundary (translation-rules.md 9.2)
                    kill__2 = Dm.eq(t__2, PROCESS_KILL);
                }
                if (kill__2)
                {
                    Dm.remove(ref processing, thing);
                    Dm.mask(ref thing.datum_flags, Dm.bitnot(DF_ISPROCESSING));
                }
                if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                    return;
            }
            current_part = SSMACHINES_MACHINES_LATE;
            this.currentrun = processing_late.Copy();
        }

        //Processing machines that record the power usage statistics
        if (current_part == SSMACHINES_MACHINES_LATE)
        {
            //cache for sanic speed (lists are references anyways)
            DmList currentrun = this.currentrun;
            while (Dm.truthy(currentrun.len))
            {
                obj_machinery thing = currentrun[currentrun.len] as obj_machinery;
                currentrun.len--;
                bool kill__3 = QDELETED(thing);
                if (!kill__3)
                {
                    // RULE-GAP: rules 9.2 has no form for a boundary call inside an || condition: its value goes to the temp t__3.
                    object t__3 = null;
                    try { t__3 = thing.process_late(); } // the argument wait * 0.1 is dropped: process_late declares none (rules 7.3)
                    catch (Exception e) { World.Current.ReportRuntime(e); t__3 = null; } // isolation boundary (translation-rules.md 9.2)
                    kill__3 = Dm.eq(t__3, PROCESS_KILL);
                }
                if (kill__3)
                {
                    Dm.remove(ref processing_late, thing);
                    Dm.mask(ref thing.datum_flags, Dm.bitnot(DF_ISPROCESSING));
                }
                if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                    return;
            }
        }
    }

    // tg: code/controllers/subsystem/machines.dm:170
    public virtual void setup_template_powernets(DmList cables = null)
    {
        obj_structure_cable PC = null;
        for (float A = 1, A__end = cables.len; A <= A__end; A++)
        {
            PC = cables[A] as obj_structure_cable;
            if (!Dm.truthy(PC.powernet))
            {
                datum_powernet NewPN = Dm.@new<datum_powernet>();
                NewPN.add_cable(PC);
                propagate_network(PC, PC.powernet);
            }
        }
    }

    // tg: code/controllers/subsystem/machines.dm:179
    public override void Recover()
    {
        if (islist(SSmachines.processing))
            processing = SSmachines.processing;
        if (islist(SSmachines.powernets))
            powernets = SSmachines.powernets;
        if (islist(SSmachines.all_machines))
            all_machines = SSmachines.all_machines;
        if (islist(SSmachines.machines_by_type))
            machines_by_type = SSmachines.machines_by_type;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/machines.dm:1  SUBSYSTEM_DEF(machines): GLOBAL_REAL(SSmachines, /datum/controller/subsystem/machines)
    internal datum_controller_subsystem_machines SSmachines;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_machines SSmachines => ref TgState.Current.SSmachines;
}
