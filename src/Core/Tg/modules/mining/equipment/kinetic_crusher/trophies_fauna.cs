// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*!
 * Contains crusher trophies you can obtain from regular fauna
 */

//watcher
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:6
public partial class obj_item_crusher_trophy_watcher_wing : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:16
    public override string effect_desc()
    {
        return I18n.T("mark detonation to prevent certain creatures from using certain attacks for <b>[]</b> second\\s", bonus_value * 0.1f);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:19
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        if (target is not mob_living_simple_animal_hostile) // ishostile
            return dot;
        mob_living_simple_animal_hostile hostile_animal = target as mob_living_simple_animal_hostile;
        if (!Dm.truthy(hostile_animal.ranged))
            return dot;
        if (hostile_animal.ranged_cooldown >= world.time) //briefly delay ranged attacks
            hostile_animal.ranged_cooldown += bonus_value;
        else
            hostile_animal.ranged_cooldown = bonus_value + world.time;
        return dot;
    }
}

//magmawing watcher
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:32
public partial class obj_item_crusher_trophy_magma_wing : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:42
    public override object Destroy(float? force = null)
    {
        marked_targets.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:46
    public override string effect_desc()
    {
        return I18n.T("attacks of marked enemies to cause mark detonation");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:49
    public override async DmTask<object> remove_from(obj_item_kinetic_crusher crusher = null, mob_living user = null)
    {
        object dot = await base.remove_from(crusher, user);
        foreach (mob_living mark_on in Dm.each<mob_living>(marked_targets))
            UnregisterSignal(mark_on, Dm.list(COMSIG_LIVING_STATUS_REMOVED, COMSIG_HOSTILE_POST_ATTACKINGTARGET, COMSIG_QDELETING));
        marked_targets.Cut();
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:55
    public override void on_mark_applied(mob_living target = null, mob_living user = null, datum_status_effect crusher_mark = null)
    {
        base.on_mark_applied(target, user, crusher_mark);
        // Shouldn't happen, but just in case
        if (Dm.truthy(marked_targets[target]))
            return;
        marked_targets[target] = TRUE;
        RegisterSignal(target, COMSIG_LIVING_STATUS_REMOVED, nameof(on_status_removed));
        RegisterSignal(target, COMSIG_HOSTILE_POST_ATTACKINGTARGET, nameof(on_hostile_attacking));
        RegisterSignal(target, COMSIG_QDELETING, nameof(on_target_deleted));
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:65
    [SignalHandler]
    public virtual void on_status_removed(mob_living source = null, datum_status_effect removed = null)
    {
        if (removed is datum_status_effect_crusher_mark)
        {
            UnregisterSignal(source, Dm.list(COMSIG_LIVING_STATUS_REMOVED, COMSIG_HOSTILE_POST_ATTACKINGTARGET, COMSIG_QDELETING));
            Dm.remove(ref marked_targets, source);
        }
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:72
    [SignalHandler]
    public virtual void on_target_deleted(mob_living source = null)
    {
        UnregisterSignal(source, Dm.list(COMSIG_LIVING_STATUS_REMOVED, COMSIG_HOSTILE_POST_ATTACKINGTARGET, COMSIG_QDELETING));
        Dm.remove(ref marked_targets, source);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:78
    [SignalHandler]
    public virtual void on_hostile_attacking(mob_living source = null, atom target = null, object result = null)
    {
        if (!isliving(target) || loc is not obj_item_kinetic_crusher)
            return;

        mob_living living_target = target as mob_living;
        datum_status_effect_crusher_mark crusher_mark = source.has_status_effect(typeof(datum_status_effect_crusher_mark)) as datum_status_effect_crusher_mark;
        mob_living crusher_user = null;
        obj_item_kinetic_crusher used_crusher = loc as obj_item_kinetic_crusher;
        // Count hits on crusher wielder as detonations caused by them
        if (living_target.get_active_held_item() == used_crusher)
            crusher_user = living_target;
        if (crusher_mark != null)
        {
            playsound(source, used_crusher.backstab_sound, 100, TRUE); // Give feedback that a detonation has occured
            Waiting.NoSleep(crusher_mark.detonate(used_crusher, crusher_user, melee_hit: FALSE));
        }
    }
}

//icewing watcher
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:96
public partial class obj_item_crusher_trophy_ice_wing : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:105
    public override string effect_desc()
    {
        return I18n.T("user to backstep [] tile\\s when detonating a mark", bonus_value);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:108
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        if (user == null)
            return dot;
        object step_dir = get_dir(target, user);
        for (int i = 1; i <= bonus_value; i++)
        {
            turf_open behind_user = get_step(user, Dm.num(step_dir)) as turf_open;
            if (behind_user == null || Dm.truthy(await behind_user.is_blocked_turf(source_atom: user)))
                return dot;
            // Don't backstep into chasms, lava or fire
            if (ischasm(behind_user) || islava(behind_user) || Dm.locate<obj_effect_hotspot>(behind_user) != null)
                return dot;
            // Don't try further moves if you fail once
            if (!Dm.truthy(await user.Move(behind_user, step_dir, FALSE, FALSE)))
                return dot;
        }
        return dot;
    }
}

//legion
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:125
public partial class obj_item_crusher_trophy_legion_skull : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:134
    public override string effect_desc()
    {
        return I18n.T("a kinetic crusher to recharge <b>[]</b> second\\s faster", bonus_value * 0.1f);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:137
    public override async DmTask<object> add_to(obj_item_kinetic_crusher pkc = null, mob_living user = null)
    {
        object dot = await base.add_to(pkc, user);
        if (Dm.truthy(dot))
            pkc.charge_time -= bonus_value;
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:142
    public override async DmTask<object> remove_from(obj_item_kinetic_crusher pkc = null, mob_living user = null)
    {
        object dot = await base.remove_from(pkc, user);
        if (Dm.truthy(dot))
            pkc.charge_time += bonus_value;
        return dot;
    }
}

// Goliath - Increases damage as your health decreases.
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:148
public partial class obj_item_crusher_trophy_goliath_tentacle : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:160
    public override string effect_desc()
    {
        return I18n.T("mark detonation to do <b>[]</b> more damage for every <b>[]</b> health you are missing", bonus_value, 1 / missing_health_ratio);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:163
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        if (user == null)
            return dot;
        float missing_health = user.maxHealth - user.health;
        missing_health *= missing_health_ratio; //bonus is active at all times, even if you're above 90 health
        missing_health *= bonus_value; //multiply the remaining amount by bonus_value
        if (missing_health > 0)
            return missing_health; //and do that much damage
        return dot;
    }
}

// Lobstrosity - Rebukes targets, increasing their click cooldown.
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:174
public partial class obj_item_crusher_trophy_lobster_claw : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:184
    public override string effect_desc()
    {
        return I18n.T("mark detonation to briefly rebuke the target for [] second[]", bonus_value, bonus_value > 1 ? "s" : "");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:187
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        await target.apply_status_effect(typeof(datum_status_effect_rebuked), bonus_value * SECONDS);
        return dot;
    }
}

// Brimdemon - makes a funny sound, the most essential trophy out of all
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:192
public partial class obj_item_crusher_trophy_brimdemon_fang : obj_item_crusher_trophy
{
    /// Cartoon punching vfx
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:200 (var/static/list/comic_phrases)
    public static DmList comic_phrases => TgState.Current.obj_item_crusher_trophy_brimdemon_fang__comic_phrases ??= Dm.list("BOOM", "BANG", "KABLOW", "KAPOW", "OUCH", "BAM", "KAPOW", "WHAM", "POW", "KABOOM");

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:202
    public override string effect_desc()
    {
        return I18n.T("mark detonation to create visual and audiosensory effects at the target");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:205
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        target.loc.balloon_alert_to_viewers(I18n.T("[]!", Dm.pick(comic_phrases)));
        playsound(target, Dm.resource("sound/mobs/non-humanoids/brimdemon/brimdemon_crush.ogg"), 100);
        return dot;
    }
}

// Bileworm
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:211
public partial class obj_item_crusher_trophy_bileworm_spewlet : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:222
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ability = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_dir_shots_spewlet>();
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:226
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref ability);
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:230
    public override async DmTask<object> add_to(obj_item_kinetic_crusher crusher = null, mob_living user = null)
    {
        object dot = await base.add_to(crusher, user);
        if (Dm.truthy(dot))
            await crusher.add_item_action(ability);
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:235
    public override async DmTask<object> remove_from(obj_item_kinetic_crusher crusher = null, mob_living user = null)
    {
        object dot = await base.remove_from(crusher, user);
        crusher.remove_item_action(ability);
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:239
    public override string effect_desc()
    {
        return I18n.T("mark detonation launches projectiles in cardinal directions on a 10 second cooldown. Also gives you an AOE when mining minerals");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:242
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        //ability itself handles cooldowns.
        await ability.InterceptClickOn(user, null, target);
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:247
    public override async DmTask on_projectile_hit_mineral(turf_closed_mineral mineral = null, mob_living user = null)
    {
        foreach (turf_closed_mineral mineral_turf in Dm.each<turf_closed_mineral>(Dm.sub(RANGE_TURFS(1, mineral), mineral)))
            await mineral_turf.drill_aoe(user, 0.2f);
    }
}

//yes this is a /mob_cooldown subtype being added to an item. I can't recommend you do what I'm doing
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:252
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_dir_shots_spewlet : datum_action_cooldown_mob_cooldown_projectile_attack_dir_shots
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:259
    public override async DmTask New(params object[] __extra)
    {
        firing_directions = GLOB.cardinals.Copy();
        await base.New(__extra);
    }
}

// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:263
public partial class obj_projectile_bileworm_acid : obj_projectile
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:276
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_parriable_projectile)));
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:280
    public override async DmTask<object> prehit_pierce(atom target = null)
    {
        if (!isliving(target))
            return await base.prehit_pierce(target);
        mob_living as_living = target as mob_living;
        // Only hit hostile things, or mining mobs if we have no firer (somehow)
        if (firer != null)
        {
            if (Dm.truthy((firer as mob)?.faction_check_atom(as_living)))
                return PROJECTILE_DELETE_WITHOUT_HITTING;
            return await base.prehit_pierce(target);
        }
        if (Dm.truthy(Dm.bitand(as_living.mob_biotypes, MOB_MINING)))
            return await base.prehit_pierce(target);
        return PROJECTILE_DELETE_WITHOUT_HITTING;
    }
}

// demonic watcher
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:294
public partial class obj_item_crusher_trophy_ice_demon_cube : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:305
    public override string effect_desc()
    {
        return I18n.T("mark detonation to unleash demonic ice clones upon the target");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:308
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        if (target == null || !(summon_cooldown <= world.time)) // COOLDOWN_FINISHED(src, summon_cooldown)
            return dot;
        for (int i = 1; i <= summon_amount; i++)
        {
            turf drop_off = await find_dropoff_turf(target, user);
            mob_living_basic_mining_demon_afterimage_crusher friend = Dm.@new<mob_living_basic_mining_demon_afterimage_crusher>(drop_off);
            friend.set_faction(Dm.list(FACTION_NEUTRAL));
            await friend.befriend(user);
            (friend.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_CURRENT_TARGET, target);
        }
        summon_cooldown = world.time + 30 * SECONDS; // COOLDOWN_START(src, summon_cooldown, 30 SECONDS)
        return dot;
    }

    ///try to make them spawn all around the target to surround him
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:321
    public virtual async DmTask<turf> find_dropoff_turf(mob_living target = null, mob_living user = null)
    {
        DmList turfs_list = get_adjacent_open_turfs(target) as DmList;
        foreach (turf possible_turf in Dm.each<turf>(turfs_list))
        {
            if (Dm.truthy(await possible_turf.is_blocked_turf()))
                continue;
            return possible_turf;
        }
        return get_turf(user);
    }
}

// Wolf
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:331
public partial class obj_item_crusher_trophy_wolf_ear : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:338
    public override string effect_desc()
    {
        return I18n.T("mark detonation to gain a slight speed boost temporarily");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:341
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        await user.apply_status_effect(typeof(datum_status_effect_speed_boost), 1 * SECONDS);
        return dot;
    }
}

// Polar bear - If you're hurt, you attack twice when you detonate a mark
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:346
public partial class obj_item_crusher_trophy_bear_paw : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:353
    public override string effect_desc()
    {
        return I18n.T("mark detonation to attack twice if you are below half your life");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:356
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        if (user.health / user.maxHealth > 0.5f)
            return dot;
        obj_item weapon = user.get_active_held_item() as obj_item;
        if (weapon != null)
            _addtimer(Dm.@new<datum_callback>(weapon, nameof(obj_item.melee_attack_chain), user, target), 0.1f * SECONDS, file: "code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm", line: 362);
        return dot;
    }
}

// Raptor - Your shots now go through your allied mobs. You monster.
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:365
public partial class obj_item_crusher_trophy_raptor_feather : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:373
    public override string effect_desc()
    {
        return I18n.T("your shots to go through your allies");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:376
    public override void on_projectile_fire(obj_projectile_destabilizer marker = null, mob_living user = null)
    {
        marker.ignore_allies = TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_fauna.dm:200 (var/static/list/comic_phrases)
    internal DmList obj_item_crusher_trophy_brimdemon_fang__comic_phrases;
}
