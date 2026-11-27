// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/**
 * COLOSSUS
 *
 *The colossus spawns randomly wherever a lavaland creature is able to spawn. It is powerful, ancient, and extremely deadly.
 *The colossus has a degree of sentience, proving this in speech during its attacks.
 *
 *It acts as a melee creature, chasing down and attacking its target while also using different attacks to augment its power that increase as it takes damage.
 */
// tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:30
[DmPath("/mob/living/simple_animal/hostile/megafauna/colossus")]
public partial class mob_living_simple_animal_hostile_megafauna_colossus : mob_living_simple_animal_hostile_megafauna
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:1  #define COLOSSUS_ENRAGED (health <= maxHealth / 3) (file-local)
    private bool COLOSSUS_ENRAGED => health <= maxHealth / 3;

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:73
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_FLOATING_ANIM, INNATE_TRAIT); //we don't want this guy to float, messes up his animations.
        spiral_shots = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_spiral_shots_colossus>(this);
        random_shots = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_random_aoe_colossus>(this);
        shotgun_blast = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_shotgun_blast_colossus>(this);
        dir_shots = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_dir_shots_alternating_colossus>(this);
        colossus_final = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_colossus_final>(this);
        Waiting.NoSleep(spiral_shots.Grant(this));
        Waiting.NoSleep(random_shots.Grant(this));
        Waiting.NoSleep(shotgun_blast.Grant(this));
        Waiting.NoSleep(dir_shots.Grant(this));
        Waiting.NoSleep(colossus_final.Grant(this));
        RegisterSignal(this, COMSIG_MOB_ABILITY_STARTED, nameof(start_attack));
        RegisterSignal(this, COMSIG_MOB_ABILITY_FINISHED, nameof(finished_attack));
        Waiting.NoSleep(AddElement(typeof(datum_element_projectile_shield)));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:90
    public override object Destroy(float? force = null)
    {
        RemoveElement(typeof(datum_element_projectile_shield));
        spiral_shots = null;
        random_shots = null;
        shotgun_blast = null;
        dir_shots = null;
        colossus_final = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:99
    public override async DmTask OpenFire(atom A = null)
    {
        anger_modifier = clamp(((maxHealth - health) / 40), 0, 20);

        if (client != null)
            return;

        if (Dm.truthy(enrage(target as mob_living)))
        {
            if (move_to_delay == Dm.num(Dm.initial(this, (mob_living_simple_animal_hostile p) => p.move_to_delay)))
                visible_message(span_colossus(I18n.T("\"<b>You can't dodge.</b>\"")));
            ranged_cooldown = world.time + 3 * SECONDS;
            telegraph();
            dir_shots.fire_in_directions(this, target, GLOB.alldirs);
            move_to_delay = 3;
            return;
        }
        else
            move_to_delay = Dm.num(Dm.initial(this, (mob_living_simple_animal_hostile p) => p.move_to_delay));

        if (health <= maxHealth / 10 && Dm.truthy(final_available))
        {
            final_available = FALSE;
            await colossus_final.Trigger(null, null, target);
        }
        else if (prob(20 + anger_modifier)) //Major attack
            await spiral_shots.Trigger(null, null, target);
        else if (prob(20))
            await random_shots.Trigger(null, null, target);
        else
        {
            if (prob(60 + anger_modifier))
                await shotgun_blast.Trigger(null, null, target);
            else
                await dir_shots.Trigger(null, null, target);
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:127
    public virtual void telegraph()
    {
        foreach (object viewer__it in Dm.each(viewers(10, this)))
        {
            mob viewer = (mob)viewer__it;
            if (viewer.client != null)
            {
                flash_color(viewer.client, "#C80000", 1);
                shake_camera(viewer, 4, 3);
            }
        }
        playsound(this, Dm.resource("sound/effects/magic/clockwork/narsie_attack.ogg"), 200, TRUE);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:134
    [SignalHandler]
    public virtual void start_attack(mob_living owner = null, datum_action_cooldown activated = null)
    {
        if (activated == spiral_shots)
        {
            spiral_shots.enraged = Dm.num(COLOSSUS_ENRAGED);
            telegraph();
            icon_state = "eva_attack";
            Waiting.Detach(say(I18n.T("Judgement."), null, Dm.list("colossus", "yell"))); // INVOKE_ASYNC
        }
        else if (activated == random_shots)
            Waiting.Detach(say(I18n.T("Wrath."), null, Dm.list("colossus", "yell"))); // INVOKE_ASYNC
        else if (activated == shotgun_blast)
            Waiting.Detach(say(I18n.T("Retribution."), null, Dm.list("colossus", "yell"))); // INVOKE_ASYNC
        else if (activated == dir_shots)
            Waiting.Detach(say(I18n.T("Lament."), null, Dm.list("colossus", "yell"))); // INVOKE_ASYNC
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:149
    [SignalHandler]
    public virtual void finished_attack(mob_living owner = null, datum_action_cooldown finished = null)
    {
        if (finished == spiral_shots)
            icon_state = Dm.initial(this, (atom p) => p.icon_state);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:154
    public virtual object enrage(mob_living victim = null)
    {
        if (!ishuman(victim))
            return FALSE;
        if (Dm.truthy(victim.has_status_effect(typeof(datum_status_effect_golem_gold))))
            return TRUE;

        return Dm.num(LAZYACCESS(victim.martial_arts, 1) is datum_martial_art_the_sleeping_carp); // GET_ACTIVE_MARTIAL_ART (__DEFINES/combat.dm:353), inline
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:162
public partial class obj_effect_temp_visual_at_shield : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:177
    [DmParams("mapload", "new_target")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_target = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        target = new_target;
        Waiting.Detach(orbit(target as atom, 0, FALSE, 0, 0, FALSE, TRUE)); // INVOKE_ASYNC
        return dot;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:182
public partial class obj_projectile_colossus : obj_projectile
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:193
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_parriable_projectile)));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:197
    public override async DmTask<object> can_hit_target(atom target = null, float? direct_target = null, float? ignore_loc = null, float? cross_failed = null)
    {
        direct_target ??= FALSE;
        ignore_loc ??= FALSE;
        cross_failed ??= FALSE;
        if (isliving(target) && target != firer)
            direct_target = TRUE;
        return await base.can_hit_target(target, direct_target, ignore_loc, cross_failed);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/colossus.dm:202
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (isliving(target))
        {
            mob_living dust_mob = target as mob_living;
            if (dust_mob.stat == DEAD)
            {
                dust_mob.investigate_log("has been dusted by a death bolt (colossus).", INVESTIGATE_DEATHS);
                await dust_mob.dust();
            }
            return dot;
        }
        if (!Dm.truthy(explode_hit_objects) || target is obj_vehicle_sealed)
            return dot;
        if (isturf(target) || isobj(target))
        {
            if (isobj(target))
                Dm.append(ref SSexplosions.med_mov_atom, target);
            else
                Dm.append(ref SSexplosions.medturf, target);
        }
        return dot;
    }
}
