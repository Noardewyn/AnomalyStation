// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmodes/gizlectric.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the electric gizmo mode and its battery (a tenth of a standard battery): it draws power from a cell in view or
// charges itself a little for free, and spends it on an EMP, a tesla discharge, charging a cell in view or a defibrillating pulse
// that revives the dead around it, with their beams, sounds and the yellow ring. The var blocks are generated (D111).

/// Suck power and shoot it out again
// tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:2
public partial class datum_gizmodes_electric : datum_gizmodes
{
    // tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:31
    public override async DmTask activate(atom_movable holder = null)
    {
        if (power == null)
            power = Dm.@new<obj_item_stock_parts_power_store_battery_gizmo>(holder);

        await base.activate(holder);
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:42
public partial class datum_gizpulse_electric : datum_gizpulse
{
    /// Get the total charge
    // tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:42
    public virtual object get_power(atom_movable holder = null, datum_gizmodes master = null)
    {
        if (istype(master, typeof(datum_gizmodes_electric)))
        {
            datum_gizmodes_electric electromaster = master as datum_gizmodes_electric;
            return electromaster.power.charge__proc();
        }
        return 0;
    }

    /// Use some charge
    // tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:49
    public virtual async DmTask<object> use_power(object amount = null, atom_movable holder = null, datum_gizmodes master = null)
    {
        if (istype(master, typeof(datum_gizmodes_electric)))
        {
            datum_gizmodes_electric electromaster = master as datum_gizmodes_electric;
            return await electromaster.power.use(amount);
        }
        return FALSE;
    }
}

/// Do an EMP blast
// tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:56
public partial class datum_gizpulse_electric_emp : datum_gizpulse_electric
{
    /// Do an EMP blast using the cell of our gizmode
    // tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:61
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        float charge = Dm.num(get_power(holder, master));
        if (charge < min_power)
            return;
        /// Max charge is 1MJ, standard cell charge is 10kJ. A full charge EMP is 1000 / 10 = 100. 100 / 15 is 6 tiles of light emp range
        await empulse(get_turf(holder), floor(charge / STANDARD_CELL_CHARGE / 40), floor(charge / STANDARD_CELL_CHARGE / 15), holder);
        await use_power(charge, holder, master);
    }
}

/// Shoot our current charge away as lightning
// tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:70
public partial class datum_gizpulse_electric_discharge : datum_gizpulse_electric
{
    // tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:70
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        object charge = get_power(holder, master);
        if (Dm.truthy(charge))
            return;

        await tesla_zap(holder, power: charge, zap_flags: ZAP_GIZMO_FLAGS);
        await use_power(charge, holder, master);
    }
}

/// Look for an object with a cell, and CHARGE IT!!!
// tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:79
public partial class datum_gizpulse_electric_charge : datum_gizpulse_electric
{
    // tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:79
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        if (!istype(master, typeof(datum_gizmodes_electric)))
            return;

        datum_gizmodes_electric electromaster = master as datum_gizmodes_electric;

        foreach (atom_movable power_source in Dm.each<atom_movable>(oview(4, holder)))
        {
            if (!Dm.truthy(power_source.get_cell()))
                continue;

            obj_item_stock_parts_power_store cell = power_source.get_cell() as obj_item_stock_parts_power_store;

            float charge = Dm.num(electromaster.power.charge__proc()) - Dm.num(cell.used_charge());
            if (charge <= 0)
                continue;

            await cell.give(charge);
            await electromaster.power.use(charge);

            holder.Beam(power_source, icon_state: "g_beam", time: 5);
            playsound(power_source, "sound/effects/magic/ethereal_exit.ogg", 40);
            return;
        }
    }
}

/// Revive people in a radius like the revival surgery
// tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:103
public partial class datum_gizpulse_electric_revive : datum_gizpulse_electric
{
    // tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:107
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        if (!istype(master, typeof(datum_gizmodes_electric)))
            return;

        datum_gizmodes_electric electromaster = master as datum_gizmodes_electric;

        if (Dm.num(electromaster.power.charge__proc()) < defib_cost)
            return;

        await electromaster.power.use(defib_cost);

        foreach (mob_living dead in Dm.each<mob_living>(orange(holder, 3)))
        {
            if (dead.stat == DEAD)
            {
                if (Dm.truthy(await dead.revive(excess_healing: 5)))
                {
                    if (iscarbon(dead)) //still want it to work for simple/basicmobs too
                    {
                        mob_living_carbon dead_but_carbon = dead as mob_living_carbon;
                        dead_but_carbon.set_heartattack(FALSE);
                    }
                    await dead.adjust_oxy_loss(-200);
                }
                _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), dead, Dm.resource("sound/machines/defib/defib_zap.ogg"), 60), 3 * SECONDS, file: "code/modules/research/gizmo/gizmodes/gizlectric.dm", line: 125);
            }
        }

        Dm.@new<obj_effect_temp_visual_circle_wave>(get_turf(holder), COLOR_YELLOW);
        playsound(holder, "sound/machines/defib/defib_charge.ogg", 80);
    }
}

/// Look for the nearest power-containing object and suck the power out
// tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:131
public partial class datum_gizpulse_electric_draw : datum_gizpulse_electric
{
    // tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:131
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        if (!istype(master, typeof(datum_gizmodes_electric)))
            return;

        datum_gizmodes_electric electromaster = master as datum_gizmodes_electric;

        foreach (atom_movable power_source in Dm.each<atom_movable>(oview(4, holder)))
        {
            if (!Dm.truthy(power_source.get_cell()))
                continue;

            obj_item_stock_parts_power_store cell = power_source.get_cell() as obj_item_stock_parts_power_store;

            float charge = Dm.num(cell.charge__proc()) - Dm.num(electromaster.power.used_charge());
            if (charge <= 0)
                continue;

            await cell.use(charge);
            await electromaster.power.give(charge);

            holder.Beam(power_source, icon_state: "r_beam", time: 5);
            playsound(power_source, "sound/effects/magic/ethereal_exit.ogg", 40);
            return;
        }
    }
}

/// Give a bit of charge, FOR FREE
// tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:155
public partial class datum_gizpulse_electric_passive_charge : datum_gizpulse_electric
{
    // tg: code/modules/research/gizmo/gizmodes/gizlectric.dm:159
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        if (!istype(master, typeof(datum_gizmodes_electric)))
            return;

        datum_gizmodes_electric electromaster = master as datum_gizmodes_electric;
        await electromaster.power.give(recharge);

        playsound(holder, "sound/effects/magic/charge.ogg", 50, TRUE);
    }
}
