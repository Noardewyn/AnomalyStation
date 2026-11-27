// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/ranged_attacks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3; the watcher trophy's delay since Lavaland's fauna). A shared leaf: C4's and C5's ranged creatures use it too.

/**
 * Configurable ranged attack for basic mobs.
 */
// tg: code/datums/components/ranged_attacks.dm:4
public partial class datum_component_ranged_attacks : datum_component
{
    // tg: code/datums/components/ranged_attacks.dm:20
    [DmParams("casing_type", "projectile_type", "projectile_sound", "burst_shots", "burst_intervals", "cooldown_time")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        Type casing_type = Dm.arg<Type>(__extra, 0);
        Type projectile_type = Dm.arg<Type>(__extra, 1);
        object projectile_sound = Dm.arg(__extra, 2) ?? Dm.resource("sound/items/weapons/gun/pistol/shot.ogg");
        object burst_shots = Dm.arg(__extra, 3);
        object burst_intervals = Dm.arg(__extra, 4) ?? 0.2f * SECONDS;
        object cooldown_time = Dm.arg(__extra, 5) ?? 3 * SECONDS;
        await base.Initialize(__extra);
        if (!isbasicmob(parent))
            return COMPONENT_INCOMPATIBLE;

        this.casing_type = casing_type;
        this.projectile_sound = projectile_sound;
        this.projectile_type = projectile_type;
        this.cooldown_time = cooldown_time;

        if (casing_type != null && projectile_type != null)
            throw Dm.CRASH(Dm.text("Set both casing type and projectile type in []'s ranged attacks component! uhoh! stinky!", parent), "code/datums/components/ranged_attacks.dm", 39);
        if (casing_type == null && projectile_type == null)
            throw Dm.CRASH(Dm.text("Set neither casing type nor projectile type in []'s ranged attacks component! What are they supposed to be attacking with, air?", parent), "code/datums/components/ranged_attacks.dm", 41);
        if (Dm.num(burst_shots) <= 1)
            return null;
        this.burst_shots = burst_shots;
        this.burst_intervals = burst_intervals;
        return null;
    }

    // tg: code/datums/components/ranged_attacks.dm:47
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        ADD_TRAIT(parent, TRAIT_SUBTREE_REQUIRED_OPERATIONAL_DATUM, type);
        RegisterSignal(parent, COMSIG_MOB_ATTACK_RANGED, nameof(fire_ranged_attack));
        // COMSIG_MOB_TROPHY_ACTIVATED(TROPHY_WATCHER) (__DEFINES/dcs/signals/signals_mob/signals_mob_main.dm:294), inline
        RegisterSignal(parent, "COMSIG_MOB_TROPHY_ACTIVATED" + TROPHY_WATCHER, nameof(disable_attack));
        RegisterSignal(parent, COMSIG_LIVING_STATUS_APPLIED, nameof(on_status_applied));
        RegisterSignal(parent, COMSIG_LIVING_STATUS_REMOVED, nameof(on_status_removed));
    }

    // tg: code/datums/components/ranged_attacks.dm:55
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, Dm.list(COMSIG_MOB_ATTACK_RANGED, "COMSIG_MOB_TROPHY_ACTIVATED" + TROPHY_WATCHER, COMSIG_LIVING_STATUS_APPLIED, COMSIG_LIVING_STATUS_REMOVED));
        REMOVE_TRAIT(parent, TRAIT_SUBTREE_REQUIRED_OPERATIONAL_DATUM, type);
    }

    // tg: code/datums/components/ranged_attacks.dm:60
    [SignalHandler]
    public virtual void fire_ranged_attack(mob_living_basic firer = null, atom target = null, object modifiers = null)
    {
        if (!(fire_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
            return;
        if (Dm.truthy(Dm.bitand(Dm.truthy(firer._listen_lookup?[COMSIG_BASICMOB_PRE_ATTACK_RANGED]) ? firer._SendSignal(COMSIG_BASICMOB_PRE_ATTACK_RANGED, Dm.list(firer, target, modifiers)) : NONE, COMPONENT_CANCEL_RANGED_ATTACK)))
            return;
        fire_cooldown = world.time + Dm.num(cooldown_time); // COOLDOWN_START, inline
        Waiting.Detach(async_fire_ranged_attack(firer, target, modifiers));
        if (burst_shots == null)
            return;
        for (int i = 1; i <= Dm.num(burst_shots) - 1; i++)
            _addtimer(Dm.@new<datum_callback>(this, nameof(async_fire_ranged_attack), firer, target, modifiers), i * Dm.num(burst_intervals), file: "code/datums/components/ranged_attacks.dm", line: 71);
    }

    /// Actually fire the damn thing
    // tg: code/datums/components/ranged_attacks.dm:74
    public virtual async DmTask async_fire_ranged_attack(mob_living_basic firer = null, atom target = null, object modifiers = null)
    {
        firer.face_atom(target);
        if (projectile_type != null)
        {
            await firer.fire_projectile(projectile_type, target, projectile_sound);
            if (Dm.truthy(parent._listen_lookup?[COMSIG_BASICMOB_POST_ATTACK_RANGED]))
                parent._SendSignal(COMSIG_BASICMOB_POST_ATTACK_RANGED, Dm.list(parent, target, modifiers));
            return;
        }
        playsound(firer, projectile_sound, 100, TRUE);
        turf startloc = get_turf(firer);
        obj_item_ammo_casing casing = await Dm.new_async(casing_type, startloc) as obj_item_ammo_casing;
        object target_zone;
        if (target is mob target_mob)
            target_zone = target_mob.get_random_valid_zone();
        else
            target_zone = ran_zone();
        await casing.fire_casing(target, firer, null, null, null, target_zone, 0, firer);
        casing.update_appearance();
        Dm.Deferred("code/datums/components/ranged_attacks.dm:96", "async_fire_ranged_attack: the spent casing fades away (fade_into_nothing, a casing-firing creature)");
        if (Dm.truthy(parent._listen_lookup?[COMSIG_BASICMOB_POST_ATTACK_RANGED]))
            parent._SendSignal(COMSIG_BASICMOB_POST_ATTACK_RANGED, Dm.list(parent, target, modifiers));
    }

    /// Increase CD when rebuked
    // tg: code/datums/components/ranged_attacks.dm:104
    [SignalHandler]
    public virtual void on_status_applied(mob_living source = null, datum_status_effect effect = null)
    {
        if (effect is not datum_status_effect_rebuked rebuked)
            return;
        if (!(fire_cooldown <= world.time))
            fire_cooldown += Dm.num(cooldown_time) * (Dm.num(rebuked.cd_increase) - 1); // COOLDOWN_INCREMENT, inline
        cooldown_time = Dm.num(cooldown_time) * Dm.num(rebuked.cd_increase);
    }

    /// Reduce CD back when the rebuked status runs out
    // tg: code/datums/components/ranged_attacks.dm:113
    [SignalHandler]
    public virtual void on_status_removed(mob_living source = null, datum_status_effect effect = null)
    {
        if (effect is not datum_status_effect_rebuked rebuked)
            return;
        cooldown_time = Dm.num(cooldown_time) / Dm.num(rebuked.cd_increase);
    }

    /// Delay the attack when hit by a watcher trophy detonation
    // tg: code/datums/components/ranged_attacks.dm:95
    [SignalHandler]
    public virtual void disable_attack(mob source = null, obj_item_crusher_trophy used_trophy = null, mob_living user = null)
    {
        float stun_duration = used_trophy.bonus_value * 0.1f * SECONDS;
        // COOLDOWN_INCREMENT(src, fire_cooldown, stun_duration) (__DEFINES/cooldowns.dm:122), inline
        if (Dm.num(fire_cooldown) <= world.time)
        {
            fire_cooldown = world.time + stun_duration;
            return;
        }
        fire_cooldown = Dm.num(fire_cooldown) + stun_duration;
    }
}
