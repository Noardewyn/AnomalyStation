// Translated from tgstation (snapshot 1d99bd7f): code/game/gamemodes/events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    /**
     * Causes a power failure across the station.
     *
     * All SMESs and APCs will be fully drained, and all areas will power down.
     *
     * The drain is permanent (that is, it won't automatically come back after some time like the grid check event),
     * but the crew themselves can return power via the engine, solars, or other means of creating power.
     */
    // tg: code/game/gamemodes/events.dm:9
    public static async DmTask power_failure()
    {
        priority_announce(Dm.text("Abnormal activity detected in []'s powernet. As a precautionary measure, the station's power will be shut off for an indeterminate duration.", station_name()), "Critical Power Failure", ANNOUNCER_POWEROFF);
        foreach (obj_machinery_power_smes smes in Dm.each<obj_machinery_power_smes>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_smes))))
        {
            if (istype(get_area(smes), typeof(area_station_ai)) || !Dm.truthy(is_station_level(smes.z)))
                continue;
            smes.charge = 0; // tg's own: a SMES's charge lives in its cells (smes.dm:15 is only the initial one), so they stay full and this bank just stops
            smes.output_level = 0;
            smes.output_attempt = FALSE;
            smes.update_appearance();
            smes.power_change();
        }

        foreach (area station_area in Dm.each<area>(GLOB.areas))
        {
            if (!Dm.truthy(station_area.z) || !Dm.truthy(is_station_level(station_area.z)))
                continue;
            if (!Dm.truthy(station_area.requires_power) || Dm.truthy(station_area.always_unpowered))
                continue;
            if (HAS_TRAIT(station_area, TRAIT_AREA_BLOCK_POWER_FAIL))
                continue;

            station_area.power_light = FALSE;
            station_area.power_equip = FALSE;
            station_area.power_environ = FALSE;
            station_area.power_change();
        }

        foreach (obj_machinery_power_apc C in Dm.each<obj_machinery_power_apc>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))))
        {
            if (C.cell != null && Dm.truthy(is_station_level(C.z)))
            {
                area A = C.area;
                if (HAS_TRAIT(A, TRAIT_AREA_BLOCK_POWER_FAIL))
                    continue;

                C.cell.charge = 0;
            }
        }
    }

    /**
     * Restores power to all rooms on the station.
     *
     * Magically fills ALL APCs and SMESs to capacity, and restores power to depowered areas.
     */
    // tg: code/game/gamemodes/events.dm:47
    public static async DmTask power_restore()
    {
        priority_announce(Dm.text("Power has been restored to []. We apologize for the inconvenience.", station_name()), "Power Systems Nominal", ANNOUNCER_POWERON);
        foreach (obj_machinery_power_apc C in Dm.each<obj_machinery_power_apc>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))))
        {
            if (C.cell != null && Dm.truthy(is_station_level(C.z)))
            {
                C.cell.charge = C.cell.maxcharge;
                C.failure_timer = 0; // COOLDOWN_RESET(C, failure_timer)
            }
        }
        await recharge_station_smes();

        foreach (area station_area in Dm.each<area>(GLOB.areas))
        {
            if (!Dm.truthy(station_area.z) || !Dm.truthy(is_station_level(station_area.z)))
                continue;
            if (!Dm.truthy(station_area.requires_power) || Dm.truthy(station_area.always_unpowered))
                continue;
            if (istype(station_area, typeof(area_shuttle)))
                continue;
            station_area.power_light = TRUE;
            station_area.power_equip = TRUE;
            station_area.power_environ = TRUE;
            station_area.power_change();
        }
    }

    /**
     * A quicker version of [/proc/power_restore] that only handles recharging SMESs.
     *
     * This will also repower an entire station - it is not instantaneous like power restore,
     * but it is faster performance-wise as it only handles SMES units.
     *
     * Great as a less magical / more IC way to return power to a sapped station.
     */
    // tg: code/game/gamemodes/events.dm:83
    public static async DmTask power_restore_quick()
    {
        priority_announce(Dm.text("All SMESs on [] have been recharged. We apologize for the inconvenience.", station_name()), "Power Systems Nominal", ANNOUNCER_POWERON);
        await recharge_station_smes();
    }

    // the loop tg writes out twice (events.dm:55-62, :85-92)
    private static async DmTask recharge_station_smes()
    {
        foreach (obj_machinery_power_smes smes in Dm.each<obj_machinery_power_smes>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_smes))))
        {
            if (!Dm.truthy(is_station_level(smes.z)))
                continue;
            await smes.adjust_charge(INFINITY);
            smes.output_level = smes.output_level_max;
            smes.output_attempt = TRUE;
            smes.update_appearance();
            smes.power_change();
        }
    }
}
