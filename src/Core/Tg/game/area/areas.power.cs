// Translated from tgstation (snapshot 1d99bd7f): code/game/area/areas.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the power procs of areas.dm (:492-567, in tg's order) are here; the rest of the file is game/area/areas.cs.
// The M3 contracts step moved them out of areas.cs unchanged, so that M3 P3 (machinery and power) and M3 P2 (the area's
// contents and the map load) own one file each (docs/ai/specs/m3-ownership.md).

// tg: code/game/area/areas.dm:492  (interleaved types, rules 1.9)
public partial class area : atom
{
    /**
     * Returns int 1 or 0 if the area has power for the given channel
     *
     * evaluates a mixture of variables mappers can set, requires_power, always_unpowered and then
     * per channel power_equip, power_light, power_environ
     */
    // tg: code/game/area/areas.dm:492
    public virtual object powered(object chan = null) // return true if the area has power to given channel
    {
        if (!Dm.truthy(requires_power))
            return TRUE;
        if (Dm.truthy(always_unpowered))
            return FALSE;
        object sw__1 = chan;
        if (Dm.eq(sw__1, AREA_USAGE_EQUIP))
            return power_equip;
        else if (Dm.eq(sw__1, AREA_USAGE_LIGHT))
            return power_light;
        else if (Dm.eq(sw__1, AREA_USAGE_ENVIRON))
            return power_environ;

        return FALSE;
    }
}

// tg: code/game/area/areas.dm:511  (interleaved types, rules 1.9)
public partial class area_space : area
{
    /**
     * Space is not powered ever, so this returns false
     */
    // tg: code/game/area/areas.dm:511
    public override object powered(object chan = null) //Nope.avi
    {
        return FALSE;
    }
}

// tg: code/game/area/areas.dm:519  (interleaved types, rules 1.9)
public partial class area : atom
{
    /**
     * Called when the area power status changes
     *
     * Updates the area icon, calls power change on all machinees in the area, and sends the `COMSIG_AREA_POWER_CHANGE` signal.
     */
    // tg: code/game/area/areas.dm:519
    public virtual void power_change()
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_AREA_POWER_CHANGE]))
            this._SendSignal(COMSIG_AREA_POWER_CHANGE, Dm.list(this));
        update_appearance();
    }


    /**
     * Add a static amount of power load to an area. The value is assumed as the watt.
     *
     * Possible channels
     * *AREA_USAGE_STATIC_EQUIP
     * *AREA_USAGE_STATIC_LIGHT
     * *AREA_USAGE_STATIC_ENVIRON
     */
    // tg: code/game/area/areas.dm:532
    public virtual void addStaticPower(object value = null, object powerchannel = null)
    {
        value = power_to_energy(value);
        object sw__1 = powerchannel;
        if (Dm.in_to(sw__1, AREA_USAGE_STATIC_START, AREA_USAGE_STATIC_END))
            Dm.append_at(energy_usage, powerchannel, value);
    }

    /**
     * Remove a static amount of power load to an area. The value is assumed as the watt.
     *
     * Possible channels
     * *AREA_USAGE_STATIC_EQUIP
     * *AREA_USAGE_STATIC_LIGHT
     * *AREA_USAGE_STATIC_ENVIRON
     */
    // tg: code/game/area/areas.dm:546
    public virtual void removeStaticPower(object value = null, object powerchannel = null)
    {
        value = power_to_energy(value);
        object sw__1 = powerchannel;
        if (Dm.in_to(sw__1, AREA_USAGE_STATIC_START, AREA_USAGE_STATIC_END))
            Dm.remove_at(energy_usage, powerchannel, value);
    }

    /**
     * Clear all non-static power usage in area
     *
     * Clears all power used for the dynamic equipment, light and environment channels
     */
    // tg: code/game/area/areas.dm:557
    public virtual void clear_usage()
    {
        energy_usage[AREA_USAGE_EQUIP] = 0;
        energy_usage[AREA_USAGE_LIGHT] = 0;
        energy_usage[AREA_USAGE_ENVIRON] = 0;
        energy_usage[AREA_USAGE_APC_CHARGE] = 0;
    }


    /**
     * Add a power value amount to the stored used_x variables
     */
    // tg: code/game/area/areas.dm:567
    public virtual object use_energy(object amount = null, object chan = null)
    {
        object sw__1 = chan;
        if (Dm.in_to(sw__1, AREA_USAGE_STATIC_START, AREA_USAGE_STATIC_END))
            return null;
        else
            Dm.append_at(energy_usage, chan, amount);
        return null;
    }
}
