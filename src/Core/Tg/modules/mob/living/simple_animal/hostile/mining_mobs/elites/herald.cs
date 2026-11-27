// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the attack actions are var-only). The var blocks are generated.

/**
 * # Herald
 *
 * A slow-moving projectile user with a few tricks up its sleeve.  Less unga-bunga than Colossus, with more cleverness in its fighting style.
 * As its health gets lower, the amount of projectiles fired per-attack increases.
 * Its attacks are as follows:
 * - Fires three projectiles in a given direction.
 * - Fires a spread in every cardinal and diagonal direction at once, then does it again after a bit.
 * - Shoots a single, golden bolt.  Wherever it lands, the herald will be teleported to the location.
 * - Spawns a mirror which reflects projectiles directly at the target.
 * Herald is a more concentrated variation of the Colossus fight, having less projectiles overall, but more focused attacks.
 */
// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:19
public partial class mob_living_simple_animal_hostile_asteroid_elite_herald : mob_living_simple_animal_hostile_asteroid_elite
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:1-4 (file-local #defines)
    private const float HERALD_TRISHOT = 1;
    private const float HERALD_DIRECTIONALSHOT = 2;
    private const float HERALD_TELESHOT = 3;
    private const float HERALD_MIRROR = 4;

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:57
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        if (!Dm.truthy(is_mirror))
            _addtimer(Dm.@new<datum_callback>(this, nameof(become_ghost)), 0.8f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm", line: 60);
        if (my_mirror != null)
            qdel(my_mirror);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:64
    public virtual void become_ghost()
    {
        icon_state = "herald_ghost";
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:67
    public override async DmTask send_speech(object message_raw = null, float? message_range = null, atom_movable source = null, object bubble_type = null, DmList spans = null, object message_language = null, DmList message_mods = null, object forced = null, object tts_message = null, DmList tts_filter = null)
    {
        await base.send_speech(message_raw, message_range, source, bubble_type, spans, message_language, message_mods, forced, tts_message, tts_filter);
        if (IS_UNCONSCIOUS_OR_CRIT(this))
            return;
        playsound(this, Dm.resource("sound/effects/magic/clockwork/invoke_general.ogg"), 20, TRUE);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:97
    public override async DmTask OpenFire(atom A = null)
    {
        if (client != null)
        {
            if (chosen_attack == HERALD_TRISHOT)
            {
                herald_trishot(target);
                if (my_mirror != null)
                    my_mirror.herald_trishot(target);
            }
            else if (chosen_attack == HERALD_DIRECTIONALSHOT)
            {
                herald_directionalshot();
                if (my_mirror != null)
                    my_mirror.herald_directionalshot();
            }
            else if (chosen_attack == HERALD_TELESHOT)
            {
                herald_teleshot(target);
                if (my_mirror != null)
                    my_mirror.herald_teleshot(target);
            }
            else if (chosen_attack == HERALD_MIRROR)
                await herald_mirror();
            return;
        }
        float aiattack = rand(1, 4);
        if (aiattack == HERALD_TRISHOT)
        {
            herald_trishot(target);
            if (my_mirror != null)
                my_mirror.herald_trishot(target);
        }
        else if (aiattack == HERALD_DIRECTIONALSHOT)
        {
            herald_directionalshot();
            if (my_mirror != null)
                my_mirror.herald_directionalshot();
        }
        else if (aiattack == HERALD_TELESHOT)
        {
            herald_teleshot(target);
            if (my_mirror != null)
                my_mirror.herald_teleshot(target);
        }
        else if (aiattack == HERALD_MIRROR)
            await herald_mirror();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:132
    public virtual void shoot_projectile(turf marker = null, object set_angle = null, object is_teleshot = null, object is_trishot = null)
    {
        turf startloc = get_turf(this);
        obj_projectile_herald H = null;
        if (!Dm.truthy(is_teleshot))
            H = Dm.@new<obj_projectile_herald>(startloc);
        else
            H = Dm.@new<obj_projectile_herald_teleshot>(startloc);
        Waiting.NoSleep(H.aim_projectile(marker, startloc));
        H.firer = this;
        if (target != null)
            H.original = target;
        Waiting.NoSleep(H.fire(set_angle));
        if (Dm.truthy(is_trishot))
        {
            shoot_projectile(marker, Dm.num(set_angle) + 15, FALSE, FALSE);
            shoot_projectile(marker, Dm.num(set_angle) - 15, FALSE, FALSE);
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:148
    public virtual void herald_trishot(object target = null)
    {
        ranged_cooldown = world.time + 30;
        playsound(get_turf(this), Dm.resource("sound/effects/magic/clockwork/invoke_general.ogg"), 20, TRUE);
        turf target_turf = get_turf(target as atom);
        object angle_to_target = get_angle(this, target_turf);
        shoot_projectile(target_turf, angle_to_target, FALSE, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(shoot_projectile), target_turf, angle_to_target, FALSE, TRUE), 0.2f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm", line: 156);
        _addtimer(Dm.@new<datum_callback>(this, nameof(shoot_projectile), target_turf, angle_to_target, FALSE, TRUE), 0.4f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm", line: 157);
        if (Dm.num(health) < Dm.num(maxHealth) * 0.5f && !Dm.truthy(is_mirror))
        {
            playsound(get_turf(this), Dm.resource("sound/effects/magic/clockwork/invoke_general.ogg"), 20, TRUE);
            _addtimer(Dm.@new<datum_callback>(this, nameof(shoot_projectile), target_turf, angle_to_target, FALSE, TRUE), 1 * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm", line: 160);
            _addtimer(Dm.@new<datum_callback>(this, nameof(shoot_projectile), target_turf, angle_to_target, FALSE, TRUE), 1.2f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm", line: 161);
            _addtimer(Dm.@new<datum_callback>(this, nameof(shoot_projectile), target_turf, angle_to_target, FALSE, TRUE), 1.4f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm", line: 162);
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:162
    public virtual void herald_circleshot(object offset = null)
    {
        // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:165 (var/static/list/directional_shot_angles)
        DmList directional_shot_angles = TgState.Current.mob_living_simple_animal_hostile_asteroid_elite_herald__herald_circleshot__directional_shot_angles ??= Dm.list(0, 45, 90, 135, 180, 225, 270, 315);
        foreach (object i in Dm.each(directional_shot_angles))
            shoot_projectile(get_turf(this), Dm.num(i) + Dm.num(offset), FALSE, FALSE);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:167
    public virtual void unenrage()
    {
        if (stat == DEAD || Dm.truthy(is_mirror))
            return;
        icon_state = "herald";
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:172
    public virtual void herald_directionalshot()
    {
        ranged_cooldown = world.time + 3 * SECONDS;
        if (!Dm.truthy(is_mirror))
            icon_state = "herald_enraged";
        playsound(get_turf(this), Dm.resource("sound/effects/magic/clockwork/invoke_general.ogg"), 20, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(herald_circleshot), 0), 0.5f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm", line: 179);
        if (Dm.num(health) < Dm.num(maxHealth) * 0.5f && !Dm.truthy(is_mirror))
        {
            playsound(get_turf(this), Dm.resource("sound/effects/magic/clockwork/invoke_general.ogg"), 20, TRUE);
            _addtimer(Dm.@new<datum_callback>(this, nameof(herald_circleshot), 22.5f), 1.5f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm", line: 182);
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(unenrage)), 2 * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm", line: 183);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:183
    public virtual void herald_teleshot(object target = null)
    {
        ranged_cooldown = world.time + 30;
        playsound(get_turf(this), Dm.resource("sound/effects/magic/clockwork/invoke_general.ogg"), 20, TRUE);
        turf target_turf = get_turf(target as atom);
        object angle_to_target = get_angle(this, target_turf);
        shoot_projectile(target_turf, angle_to_target, TRUE, FALSE);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:190
    public virtual async DmTask herald_mirror()
    {
        ranged_cooldown = world.time + 4 * SECONDS;
        playsound(get_turf(this), Dm.resource("sound/effects/magic/clockwork/invoke_general.ogg"), 20, TRUE);
        if (my_mirror != null)
        {
            qdel(my_mirror);
            my_mirror = null;
        }
        mob_living_simple_animal_hostile_asteroid_elite_herald_mirror new_mirror = Dm.@new<mob_living_simple_animal_hostile_asteroid_elite_herald_mirror>(loc);
        my_mirror = new_mirror;
        my_mirror.my_master = this;
        // SET_FACTION_AND_ALLIES_FROM(my_mirror, src) (__DEFINES/mobfactions.dm:119), inline
        my_mirror.set_faction(get_faction());
        my_mirror.set_allies(LAZYLISTDUPLICATE(allies));
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:201
public partial class mob_living_simple_animal_hostile_asteroid_elite_herald_mirror : mob_living_simple_animal_hostile_asteroid_elite_herald
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:217
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        toggle_ai(AI_OFF);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:222
    public override object Destroy(float? force = null)
    {
        if (my_master != null)
            my_master.my_mirror = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:227
public partial class obj_projectile_herald : obj_projectile
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:236
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_parriable_projectile)));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:240
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        if (ismob(target) && ismob(firer))
        {
            mob_living mob_target = target as mob_living;
            if (Dm.truthy(mob_target.faction_check_atom(firer as atom_movable)))
                damage = 0;
        }

        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (ismineralturf(target))
        {
            turf_closed_mineral rock_target = target as turf_closed_mineral;
            await rock_target.gets_drilled();
        }
        return dot;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:251
public partial class obj_projectile_herald_teleshot : obj_projectile_herald
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:256
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!QDELETED(firer))
            await ((atom_movable)firer).forceMove(get_turf(this));
        return dot;
    }
}

//Herald's loot: Cloak of the Prophet

// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:263
public partial class obj_item_clothing_neck_cloak_herald_cloak : obj_item_clothing_neck_cloak
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:271
    public virtual void reactionshot(mob_living_carbon owner = null)
    {
        // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:273 (var/static/list/directional_shot_angles)
        DmList directional_shot_angles = TgState.Current.obj_item_clothing_neck_cloak_herald_cloak__reactionshot__directional_shot_angles ??= Dm.list(0, 45, 90, 135, 180, 225, 270, 315);
        foreach (object i in Dm.each(directional_shot_angles))
            shoot_projectile(get_turf(owner), i, owner);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:276
    public virtual void shoot_projectile(turf marker = null, object set_angle = null, mob_living_carbon owner = null)
    {
        turf startloc = get_turf(owner);
        obj_projectile_herald H = null;
        H = Dm.@new<obj_projectile_herald>(startloc);
        Waiting.NoSleep(H.aim_projectile(marker, startloc));
        H.firer = owner;
        Waiting.NoSleep(H.fire(set_angle));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:284
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        object dot = await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
        if (prob(hit_reaction_chance))
            return dot;
        owner.visible_message(span_danger(I18n.T("[]'s [] emits a loud noise as [] is struck!", owner, this, owner)));
        // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:290 (var/static/list/directional_shot_angles, unused here)
        playsound(get_turf(owner), Dm.resource("sound/effects/magic/clockwork/invoke_general.ogg"), 20, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(reactionshot), owner), 1 * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm", line: 292);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:165 (var/static/list/directional_shot_angles)
    internal DmList mob_living_simple_animal_hostile_asteroid_elite_herald__herald_circleshot__directional_shot_angles;
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/herald.dm:273 (var/static/list/directional_shot_angles)
    internal DmList obj_item_clothing_neck_cloak_herald_cloak__reactionshot__directional_shot_angles;
}
