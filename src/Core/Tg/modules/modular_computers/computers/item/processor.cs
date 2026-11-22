// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/processor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the engineering package E2, D287): the processing unit a modular computer console holds, which runs NtOS with
// the console's power and passes its icon updates to the console. The var block is generated (D111).

// Held by /obj/machinery/modular_computer to reduce amount of copy-pasted code.
//TODO: REFACTOR THIS SPAGHETTI CODE
// tg: code/modules/modular_computers/computers/item/processor.dm:3
public partial class obj_item_modular_computer_processor : obj_item_modular_computer
{
    // tg: code/modules/modular_computers/computers/item/processor.dm:16
    public override void UpdateDisplay()
    {
        base.UpdateDisplay();
        //update our name to match the computer's
        name = machinery_computer.name;
    }

    // tg: code/modules/modular_computers/computers/item/processor.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!(loc is obj_machinery_modular_computer))
            throw Dm.CRASH(Dm.text("A non '/obj/machinery/modular_computer' had a [] initialized in it!", this), "code/modules/modular_computers/computers/item/processor.dm", 23);

        // Obtain reference to machinery computer
        physical = loc as obj;
        machinery_computer = loc as obj_machinery_modular_computer;
        machinery_computer.cpu = this;
        hardware_flag = machinery_computer.hardware_flag;
        steel_sheet_cost = machinery_computer.steel_sheet_cost;
        max_idle_programs = machinery_computer.max_idle_programs;
        update_integrity(machinery_computer.get_integrity());
        max_integrity = machinery_computer.max_integrity;
        integrity_failure = machinery_computer.integrity_failure;
        base_active_power_usage = machinery_computer.base_active_power_usage;
        base_idle_power_usage = machinery_computer.base_idle_power_usage;
        machinery_computer.RegisterSignal(this, COMSIG_ATOM_UPDATED_ICON, nameof(obj_machinery_modular_computer.relay_icon_update)); //when we update_icon, also update the computer
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/modular_computers/computers/item/processor.dm:40
    public override object Destroy(float? force = null)
    {
        if (machinery_computer != null && machinery_computer.cpu == this)
        {
            machinery_computer.cpu = null;
            machinery_computer.UnregisterSignal(this, COMSIG_ATOM_UPDATED_ICON);
        }
        machinery_computer = null;
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/computers/item/processor.dm:47
    public override object use_energy(object amount = null, object check_programs = null)
    {
        amount ??= 0;
        check_programs ??= TRUE;
        obj_machinery machine_holder = physical as obj_machinery;
        if (Dm.truthy(machine_holder.powered__proc()))
        {
            Waiting.NoSleep(machine_holder.use_energy(amount));
            return TRUE;
        }
        return base.use_energy(amount, check_programs);
    }

    // tg: code/modules/modular_computers/computers/item/processor.dm:54
    public override void relay_qdel()
    {
        qdel(machinery_computer);
    }

    // tg: code/modules/modular_computers/computers/item/processor.dm:57
    public override object get_messenger_ending()
    {
        return "Sent from my Desktop";
    }
}
