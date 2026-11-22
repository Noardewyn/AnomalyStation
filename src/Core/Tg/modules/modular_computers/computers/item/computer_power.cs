// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/computer_power.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: the PDA's battery): the computer's draw from its cell, its power failure and the silicons' PDA that
// never drains (pda/silicon's check_power_override, since silicons SI2).

// tg: code/modules/modular_computers/computers/item/computer_power.dm:1 (interleaved types, rules 1.9)
public partial class obj_item_modular_computer : obj_item
{
    ///The multiplier given to the base overtime charge drain value if its flashlight is on.
    // tg: code/modules/modular_computers/computers/item/computer_power.dm:2  #define FLASHLIGHT_DRAIN_MULTIPLIER 1.1 (file-local)
    private const float FLASHLIGHT_DRAIN_MULTIPLIER = 1.1f;

    ///Draws power from its rightful source (area if its a computer, the cell otherwise)
    ///Takes into account special cases, like silicon PDAs through override, and nopower apps.
    // tg: code/modules/modular_computers/computers/item/computer_power.dm:6
    public virtual object use_energy(object amount = null, object check_programs = null)
    {
        amount ??= 0;
        check_programs ??= TRUE;
        if (Dm.truthy(check_power_override(amount)))
            return TRUE;
        if (cell == null)
            return FALSE;
        if (!Dm.truthy(amount) || Dm.truthy(Waiting.NoSleep(cell.use(amount))))
            return TRUE;
        if (!Dm.truthy(check_programs))
            return FALSE;

        Waiting.DetachSync(() => close_all_programs());
        return FALSE;
    }

    // tg: code/modules/modular_computers/computers/item/computer_power.dm:19
    public virtual object give_power(object amount = null)
    {
        if (cell != null)
            return Waiting.NoSleep(cell.give(amount));
        return 0;
    }

    ///Shuts down the computer from powerloss.
    // tg: code/modules/modular_computers/computers/item/computer_power.dm:25
    public virtual void power_failure()
    {
        if (!Dm.truthy(enabled))
            return;
        if (active_program != null)
            active_program.event_powerfailure();
        if (Dm.truthy(light_on))
            Waiting.NoSleep(set_light_on(FALSE));
        foreach (datum_computer_file_program programs in Dm.each<datum_computer_file_program>(idle_threads.Copy()))
            programs.event_powerfailure();
        shutdown_computer();
    }

    ///Takes the charge necessary from the Computer, shutting it off if it's unable to provide it.
    ///Charge depends on whether the PC is on, and what programs are running/idle on it.
    // tg: code/modules/modular_computers/computers/item/computer_power.dm:38
    public virtual object handle_power(object seconds_per_tick = null)
    {
        float power_usage = Dm.truthy(screen_on) ? base_active_power_usage : base_idle_power_usage;
        if (Dm.truthy(light_on))
            power_usage *= FLASHLIGHT_DRAIN_MULTIPLIER;
        if (active_program != null)
            power_usage += active_program.power_cell_use;
        foreach (datum_computer_file_program open_programs in Dm.each<datum_computer_file_program>(idle_threads))
        {
            if (!Dm.truthy(open_programs.power_cell_use))
                continue;
            if (Dm.@in(open_programs, idle_threads))
                power_usage += open_programs.power_cell_use / 2;
        }

        if (Dm.truthy(use_energy(power_usage * Dm.num(seconds_per_tick))))
            return TRUE;
        power_failure();
        return FALSE;
    }

    ///Returns TRUE if the PC should not be using any power, FALSE otherwise.
    // tg: code/modules/modular_computers/computers/item/computer_power.dm:56
    public virtual object check_power_override(object amount = null)
    {
        return !Dm.truthy(amount) && !Dm.truthy(cell?.charge) ? TRUE : FALSE;
    }
}

//Integrated (Silicon) tablets don't drain power, because the tablet is required to state laws, so it being disabled WILL cause problems.
// tg: code/modules/modular_computers/computers/item/computer_power.dm:60  (silicons SI2)
public partial class obj_item_modular_computer_pda_silicon : obj_item_modular_computer_pda
{
    // tg: code/modules/modular_computers/computers/item/computer_power.dm:60
    public override object check_power_override(object amount = null)
    {
        return TRUE;
    }
}
