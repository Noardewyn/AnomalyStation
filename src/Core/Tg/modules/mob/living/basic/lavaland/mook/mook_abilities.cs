// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:1
public partial class datum_action_cooldown_mob_cooldown_mook_ability : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:5
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        if (owner == null)
            return dot;
        is_mook = owner is mob_living_basic_mining_mook ? TRUE : FALSE;
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:11
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);

        if (!Dm.truthy(dot))
            return FALSE;

        if (!Dm.truthy(is_mook))
            return TRUE;

        mob_living_basic_mining_mook mook_owner = owner as mob_living_basic_mining_mook;
        if (mook_owner.attack_state != MOOK_ATTACK_NEUTRAL)
        {
            if (Dm.truthy(feedback))
                mook_owner.balloon_alert(mook_owner, "still recovering!");
            return FALSE;
        }
        return TRUE;
    }
}

// tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:28
public partial class datum_action_cooldown_mob_cooldown_mook_ability_mook_leap : datum_action_cooldown_mob_cooldown_mook_ability
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:39
    public override async DmTask<object> Activate(atom target = null)
    {
        if (Dm.truthy(await target.IsReachableBy(owner)))
        {
            await attack_combo(target);
            StartCooldown();
            return TRUE;
        }

        if (Dm.truthy(is_mook))
        {
            mob_living_basic_mining_mook mook_owner = owner as mob_living_basic_mining_mook;
            mook_owner.change_combatant_state(state: MOOK_ATTACK_WARMUP);
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(launch_towards_target), target), wind_up_time, file: "code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm", line: 50);
        StartCooldown();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:53
    public virtual async DmTask launch_towards_target(atom target = null)
    {
        if (QDELETED(owner) || QDELETED(target))
            return;
        turf target_turf = get_turf(target) as turf;
        if (target_turf == null)
            return;
        Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(owner));
        playsound(owner, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 25, TRUE);
        playsound(owner, Dm.resource("sound/mobs/non-humanoids/mook/mook_leap_yell.ogg"), 100, TRUE);

        if (!Dm.truthy(await target_turf.is_blocked_turf()))
        {
            await owner.throw_at(target: target_turf, range: 7, speed: 1, spin: FALSE, callback: Dm.@new<datum_callback>(this, nameof(attack_combo), target));
            return;
        }

        DmList open_turfs = Dm.list();

        foreach (turf possible_turf in Dm.each<turf>(get_adjacent_open_turfs(target)))
        {
            if (Dm.truthy(await possible_turf.is_blocked_turf()))
                continue;
            Dm.append(ref open_turfs, possible_turf);
        }

        if (Dm.length(open_turfs) == 0)
            return;

        turf final_turf = get_closest_atom(typeof(turf), open_turfs, owner) as turf;
        await owner.throw_at(target: final_turf, range: 7, speed: 1, spin: FALSE, callback: Dm.@new<datum_callback>(this, nameof(attack_combo), target));
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:80
    public virtual async DmTask<object> attack_combo(atom target = null)
    {
        if (!Dm.truthy(await target.IsReachableBy(owner)))
            return FALSE;

        for (float i = 0; i <= (times_to_attack - 1); i++)
            _addtimer(Dm.@new<datum_callback>(this, nameof(attack_target), target), i * attack_interval, file: "code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm", line: 86);
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:87
    public virtual async DmTask attack_target(atom target = null)
    {
        if (!Dm.truthy(await target.IsReachableBy(owner)) || owner.stat == DEAD)
            return;
        mob_living_basic basic_owner = owner as mob_living_basic;
        await basic_owner.melee_attack(target, ignore_cooldown: TRUE);
    }
}

// tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:94
public partial class datum_action_cooldown_mob_cooldown_mook_ability_mook_jump : datum_action_cooldown_mob_cooldown_mook_ability
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:100
    public override async DmTask<object> Activate(atom target = null)
    {
        obj_effect_landmark drop_zone = Dm.locate<obj_effect_landmark_mook_village>(GLOB.landmarks_list);
        if (drop_zone != null && Dm.eq(drop_zone.z, owner.z))
        {
            turf jump_destination = get_turf(drop_zone) as turf;
            jump_to_turf(jump_destination);
            StartCooldown();
            return TRUE;
        }
        DmList potential_turfs = Dm.list();
        foreach (turf open_turf in Dm.each<turf>(oview(9, owner)))
        {
            if (!Dm.truthy(await open_turf.is_blocked_turf()))
                Dm.append(ref potential_turfs, open_turf);
        }
        if (Dm.length(potential_turfs) == 0)
            return FALSE;
        jump_to_turf(pick(potential_turfs) as turf);
        StartCooldown();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:117
    public virtual void jump_to_turf(turf target = null)
    {
        if (Dm.truthy(is_mook))
        {
            mob_living_basic_mining_mook mook_owner = owner as mob_living_basic_mining_mook;
            mook_owner.change_combatant_state(state: MOOK_ATTACK_ACTIVE);
        }
        Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(owner));
        playsound(owner, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 50, TRUE);
        animate(owner, Dm.named("pixel_z", 146), Dm.named("time", 0.5f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        _addtimer(Dm.@new<datum_callback>(this, nameof(land_on_turf), target), 0.5f * SECONDS, file: "code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm", line: 126);
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:126
    public virtual async DmTask land_on_turf(turf target = null)
    {
        await do_teleport(owner, target, precision: 3, no_effects: TRUE);
        animate(owner, Dm.named("pixel_z", -146), Dm.named("time", 0.5f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(owner));
        if (Dm.truthy(is_mook))
            _addtimer(Dm.@new<datum_callback>(owner, nameof(mob_living_basic_mining_mook.change_combatant_state), MOOK_ATTACK_NEUTRAL), 0.5f * SECONDS, file: "code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm", line: 133);
    }
}

// tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:146
public partial class obj_effect_temp_visual_mook_dust_small : obj_effect_temp_visual_mook_dust
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_abilities.dm:148
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        transform = (transform as matrix).Scale(0.5f);
        return dot;
    }
}
