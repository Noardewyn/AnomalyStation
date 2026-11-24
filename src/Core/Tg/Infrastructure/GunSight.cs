using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tg;

/// <summary>
/// DEVIATION(DV96): a shooter's crosshair, what the own mob's crosshair shows of its gun, sent to its client (protocol 92,
/// GunSight): the half angle of the cone the held gun's shots scatter in at most, and a count of the mob's shots that hit a living thing.
/// </summary>
public static class GunSight
{
    // tg: code/modules/projectiles/gun.dm:1  #define DUALWIELD_PENALTY_EXTRA_MULTIPLIER 1.4 (every random spread roll is scaled by it)
    private const float SpreadRollMultiplier = 1.4f;

    /// <summary>
    /// The half angle in degrees of the held gun's widest shot, null without a gun in the active hand. tg's rolls (gun.dm process_fire,
    /// _firing.dm fire_casing): the gun's spread gives a deviation of at most 0.5 x 1.4 x spread; a casing with variance replaces it by at most
    /// 0.5 x variance (a pellet's). Times the server's share (<see cref="ShotSpread"/>), as <see cref="ShotGeometry.Scatter"/> takes it.
    /// The dual wield penalty (gun.dm:426-433, in combat mode each other light gun held adds the gun's <c>dual_wield_spread</c>) widens it too.
    /// shortcut: the signals' bonus spread (COMSIG_MOB_FIRED_GUN) is not shown; add it once a ported effect adds a large one.
    /// </summary>
    public static float? ConeOf(mob user)
    {
        if (user?.loc is obj_vehicle_sealed_mecha mech && mech.gc_destroyed == null)
            return MechConeOf(mech);
        if (user == null || user.gc_destroyed != null || user.get_active_held_item() is not obj_item_gun gun || gun.gc_destroyed != null)
            return null;
        float rolled = 0.5f * SpreadRollMultiplier * (gun.spread + DualWieldBonus(user, gun));
        if (gun.chambered is obj_item_ammo_casing casing && casing.variance > 0)
            rolled = 0.5f * casing.variance;
        return Math.Clamp(rolled * ShotSpread.ScaleOf(World.CurrentOrNull), 0f, 90f);
    }

    // tg: gun.dm:426-433, without can_trigger_gun's checks (a pin, a hand that can pull), which the shot itself makes
    private static float DualWieldBonus(mob user, obj_item_gun gun)
    {
        if (user is not mob_living living || !Dm.truthy(living.combat_mode) || HAS_TRAIT(living, TRAIT_NO_GUN_AKIMBO))
            return 0;
        float bonus = 0;
        foreach (obj_item_gun other in Dm.each<obj_item_gun>(living.held_items))
        {
            if (other != gun && other.weapon_weight < WEAPON_MEDIUM)
                bonus += gun.dual_wield_spread;
        }
        return bonus;
    }

    /// <summary>
    /// A pilot's crosshair (DV361): the wider cone of the mech's two arms' weapons, null when neither arm has one. tg's weapon action
    /// (weapons.dm:52): a shot turns by <c>round((rand(0, 1) - 0.5) * variance)</c> or a fan's <c>(n / count - 0.5) * variance</c>, at most
    /// half the variance.
    /// </summary>
    private static float? MechConeOf(obj_vehicle_sealed_mecha mech)
    {
        float? cone = null;
        foreach (object arm in new[] { mech.equip_by_category[MECHA_L_ARM], mech.equip_by_category[MECHA_R_ARM] })
        {
            if (arm is obj_item_mecha_parts_mecha_equipment_weapon weapon && weapon.gc_destroyed == null)
                cone = Math.Max(cone ?? 0f, 0.5f * weapon.variance);
        }
        return cone is float rolled ? Math.Clamp(rolled * ShotSpread.ScaleOf(World.CurrentOrNull), 0f, 90f) : null;
    }

    /// <summary>
    /// A projectile of <paramref name="p"/>'s firer hit <paramref name="victim"/>: the firer's crosshair marks it (a mech's shot: its pilots', tg's
    /// firer is the chassis, weapons.dm:72).
    /// </summary>
    public static void NoteHit(obj_projectile p, mob_living victim)
    {
        if (p?.firer is mob shooter && shooter != victim)
            shooter.dm_shots_landed++;
        else if (p?.firer is obj_vehicle_sealed_mecha mech)
        {
            foreach (mob pilot in Dm.each<mob>(mech.return_controllers_with_flag(VEHICLE_CONTROL_EQUIPMENT)))
            {
                if (pilot != victim)
                    pilot.dm_shots_landed++;
            }
        }
    }
}

public partial class mob
{
    /// <summary>DV96: how many of this mob's projectiles hit a living thing (<see cref="GunSight"/>; it wraps on the wire).</summary>
    public int dm_shots_landed;
}
