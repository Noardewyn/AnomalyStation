// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/projectileattack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The base attack (gaps package C4): rapid fire, the shotgun and the other megafauna's variants stay inert with their hosts.

// tg: code/datums/actions/mobs/projectileattack.dm:2
public partial class datum_action_cooldown_mob_cooldown_projectile_attack : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/projectileattack.dm:22
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        RegisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE, nameof(on_move), @override: TRUE);
        await attack_sequence(owner as mob_living, target_atom);
        UnregisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:31
    [SignalHandler]
    public virtual object on_move(atom source = null, atom new_loc = null)
    {
        if (!Dm.truthy(can_move))
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
        return null;
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:36
    public virtual async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        shoot_projectile(firer, target, null, firer, rand(-default_projectile_spread, default_projectile_spread), null);
        return null;
    }

    // tg: code/datums/actions/mobs/projectileattack.dm:39
    public virtual obj_projectile shoot_projectile(atom origin = null, atom target = null, object set_angle = null, mob firer = null, object projectile_spread = null, object speed_multiplier = null, object override_projectile_type = null, object override_homing = null)
    {
        turf startloc = get_turf(origin) as turf;
        turf endloc = get_turf(target) as turf;
        if (startloc == null || endloc == null)
            return null;
        obj_projectile our_projectile;
        if (override_projectile_type != null)
            our_projectile = Dm.@new((Type)override_projectile_type, startloc) as obj_projectile;
        else
            our_projectile = Dm.@new(projectile_type, startloc) as obj_projectile;
        if (speed_multiplier is not float)
            speed_multiplier = projectile_speed_multiplier;
        our_projectile.speed = Dm.num(our_projectile.speed) * Dm.num(speed_multiplier);
        Waiting.NoSleep(our_projectile.aim_projectile(endloc, startloc, null, Dm.num(projectile_spread)));
        our_projectile.firer = firer;
        if (target != null)
            our_projectile.original = target;
        if ((override_homing == null && Dm.truthy(has_homing)) || Dm.truthy(override_homing))
        {
            our_projectile.homing_turn_speed = homing_turn_speed;
            our_projectile.set_homing_target(target);
        }
        if (set_angle is float)
        {
            Waiting.NoSleep(our_projectile.fire(set_angle));
            return our_projectile;
        }
        Waiting.NoSleep(our_projectile.fire());
        return our_projectile;
    }
}
