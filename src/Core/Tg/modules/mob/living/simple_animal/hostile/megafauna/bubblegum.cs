// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the tongue's frenching achievement (the achievements are not ported). The var blocks are generated.

/*

BUBBLEGUM

Bubblegum spawns randomly wherever a lavaland creature is able to spawn. It is the most powerful slaughter demon in existence.
Bubblegum's footsteps are heralded by shaking booms, proving its tremendous size.

It acts as a melee creature, chasing down and attacking its target while also using different attacks to augment its power

It leaves blood trails behind wherever it goes, its clones do as well.
It tries to strike at its target through any bloodpools under them; if it fails to do that.
If it does warp it will enter an enraged state, becoming immune to all projectiles, becoming much faster, and dealing damage and knockback to anything that gets in the cloud around it.
It may summon clones charging from all sides, one of these charges being bubblegum himself.
It can charge at its target, and also heavily damaging anything directly hit in the charge.
If at half health it will start to charge from all sides with clones.

When Bubblegum dies, it leaves behind a H.E.C.K. mining suit as well as a chest that can contain three things:
A. A bottle that, when activated, drives everyone nearby into a frenzy
B. A contract that marks for death the chosen target
C. A spellblade that can slice off limbs at range

Difficulty: Hard

*/

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:30
[DmPath("/mob/living/simple_animal/hostile/megafauna/bubblegum")]
public partial class mob_living_simple_animal_hostile_megafauna_bubblegum : mob_living_simple_animal_hostile_megafauna
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:1-3 (file-local defines)
    private bool BUBBLEGUM_SMASH => health <= maxHealth * 0.5f; // angery
    private bool BUBBLEGUM_CAN_ENRAGE => enrage_till + (enrage_time * 2) <= world.time;
    private bool BUBBLEGUM_IS_ENRAGED => enrage_till > world.time;

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:88
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_FLOATING_ANIM, INNATE_TRAIT);
        triple_charge = Dm.@new<datum_action_cooldown_mob_cooldown_charge_triple_charge>(this);
        hallucination_charge = Dm.@new<datum_action_cooldown_mob_cooldown_charge_hallucination_charge>(this);
        hallucination_charge_surround = Dm.@new<datum_action_cooldown_mob_cooldown_charge_hallucination_charge_hallucination_surround>(this);
        blood_warp = Dm.@new<datum_action_cooldown_mob_cooldown_blood_warp>(this);
        Waiting.NoSleep(triple_charge.Grant(this));
        Waiting.NoSleep(hallucination_charge.Grant(this));
        Waiting.NoSleep(hallucination_charge_surround.Grant(this));
        Waiting.NoSleep(blood_warp.Grant(this));
        hallucination_charge.spawn_blood = TRUE;
        hallucination_charge_surround.spawn_blood = TRUE;
        RegisterSignal(this, COMSIG_BLOOD_WARP, nameof(blood_enrage));
        RegisterSignal(this, COMSIG_FINISHED_CHARGE, nameof(after_charge));
        if (Dm.truthy(spawn_blood))
        {
            Waiting.NoSleep(AddComponent(typeof(datum_component_blood_walk),
                Dm.named("blood_type", typeof(obj_effect_decal_cleanable_blood_bubblegum)),
                Dm.named("sound_played", Dm.resource("sound/effects/meteorimpact.ogg")),
                Dm.named("sound_volume", 200)));
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:109
    public override object Destroy(float? force = null)
    {
        triple_charge = null;
        hallucination_charge = null;
        hallucination_charge_surround = null;
        blood_warp = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:116
    public override void update_cooldowns(DmList cooldown_updates = null, float? ignore_staggered = null)
    {
        base.update_cooldowns(cooldown_updates, ignore_staggered);
        if (Dm.truthy(cooldown_updates[COOLDOWN_UPDATE_SET_ENRAGE]))
            enrage_till = world.time + Dm.num(cooldown_updates[COOLDOWN_UPDATE_SET_ENRAGE]);
        if (Dm.truthy(cooldown_updates[COOLDOWN_UPDATE_ADD_ENRAGE]))
            enrage_till += Dm.num(cooldown_updates[COOLDOWN_UPDATE_ADD_ENRAGE]);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:123
    public override async DmTask OpenFire(atom A = null)
    {
        if (client != null)
            return;

        if (!Dm.truthy(try_bloodattack()) || prob(25 + anger_modifier))
            await blood_warp.Trigger(null, null, target);

        if (!BUBBLEGUM_SMASH)
            await triple_charge.Trigger(null, null, target);
        else if (prob(50 + anger_modifier))
            await hallucination_charge.Trigger(null, null, target);
        else
            await hallucination_charge_surround.Trigger(null, null, target);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:137
    public virtual object get_mobs_on_blood()
    {
        DmList targets = ListTargets() as DmList;
        DmList dot = Dm.list();
        foreach (mob_living L in Dm.each<mob_living>(targets))
        {
            DmList bloodpool = get_bloodcrawlable_pools(get_turf(L), 0) as DmList;
            if (Dm.truthy(bloodpool.len) && (!Dm.truthy(faction_check_atom(L)) || L.stat == DEAD))
                Dm.append(ref dot, L);
        }
        return dot;
    }

    /**
     * Attack by override for bubblegum
     *
     * This is used to award the frenching achievement for hitting bubblegum with a tongue
     */
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:155
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby(attacking_item, user, modifiers, attack_modifiers);
        if (attacking_item is obj_item_organ_tongue)
            Dm.Deferred("code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:158", "attackby: user.client?.give_award(/datum/award/achievement/jobs/frenching, user) (the achievements are not ported)");
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:160
    public virtual object try_bloodattack()
    {
        DmList targets = get_mobs_on_blood() as DmList;
        if (Dm.truthy(targets.len))
        {
            Waiting.Detach(bloodattack(targets, Dm.num(prob(50)))); // INVOKE_ASYNC
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:167
    public virtual async DmTask bloodattack(DmList targets = null, object handedness = null)
    {
        mob_living target_one = pick_n_take(targets) as mob_living;
        turf target_one_turf = get_turf(target_one);
        mob_living target_two = null;
        if (Dm.truthy(targets.len))
        {
            target_two = pick_n_take(targets) as mob_living;
            turf target_two_turf = get_turf(target_two);
            if (IS_UNCONSCIOUS_OR_CRIT(target_two) || prob(10))
                await bloodgrab(target_two_turf, handedness);
            else
                await bloodsmack(target_two_turf, handedness);
        }

        if (target_one != null)
        {
            DmList pools = get_bloodcrawlable_pools(get_turf(target_one), 0) as DmList;
            if (Dm.truthy(pools.len))
            {
                target_one_turf = get_turf(target_one);
                if (target_one_turf != null)
                {
                    if (IS_UNCONSCIOUS_OR_CRIT(target_one) || prob(10))
                        await bloodgrab(target_one_turf, Dm.num(!Dm.truthy(handedness)));
                    else
                        await bloodsmack(target_one_turf, Dm.num(!Dm.truthy(handedness)));
                }
            }
        }

        if (target_two == null && target_one != null)
        {
            DmList poolstwo = get_bloodcrawlable_pools(get_turf(target_one), 0) as DmList;
            if (Dm.truthy(poolstwo.len))
            {
                target_one_turf = get_turf(target_one);
                if (target_one_turf != null)
                {
                    if (IS_UNCONSCIOUS_OR_CRIT(target_one) || prob(10))
                        await bloodgrab(target_one_turf, handedness);
                    else
                        await bloodsmack(target_one_turf, handedness);
                }
            }
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:199
    public virtual async DmTask bloodsmack(turf T = null, object handedness = null)
    {
        if (Dm.truthy(handedness))
            Dm.@new<obj_effect_temp_visual_bubblegum_hands_rightsmack>(T);
        else
            Dm.@new<obj_effect_temp_visual_bubblegum_hands_leftsmack>(T);
        // SLEEP_CHECK_DEATH(4, src) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(4);
        if (QDELETED(this) || stat == DEAD)
            return;
        foreach (mob_living L in Dm.each<mob_living>(T))
        {
            if (!Dm.truthy(faction_check_atom(L)))
            {
                to_chat(L, span_userdanger(I18n.T("[] rends you!", this)));
                playsound(T, attack_sound, 100, TRUE, -1);
                object limb_to_hit = L.get_bodypart(L.get_random_valid_zone(even_weights: TRUE) as string);
                await L.apply_damage(10, BRUTE, limb_to_hit, Dm.num(L.run_armor_check(limb_to_hit, MELEE, null, null, armour_penetration)), wound_bonus: CANT_WOUND);
            }
        }
        // SLEEP_CHECK_DEATH(3, src), expanded inline
        await sleep(3);
        if (QDELETED(this) || stat == DEAD)
            return;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:213
    public virtual async DmTask bloodgrab(turf T = null, object handedness = null)
    {
        if (Dm.truthy(handedness))
        {
            Dm.@new<obj_effect_temp_visual_bubblegum_hands_rightpaw>(T);
            Dm.@new<obj_effect_temp_visual_bubblegum_hands_rightthumb>(T);
        }
        else
        {
            Dm.@new<obj_effect_temp_visual_bubblegum_hands_leftpaw>(T);
            Dm.@new<obj_effect_temp_visual_bubblegum_hands_leftthumb>(T);
        }
        // SLEEP_CHECK_DEATH(6, src), expanded inline
        await sleep(6);
        if (QDELETED(this) || stat == DEAD)
            return;
        foreach (mob_living L in Dm.each<mob_living>(T))
        {
            if (!Dm.truthy(faction_check_atom(L)))
            {
                if (IS_UNCONSCIOUS_OR_CRIT(L))
                {
                    to_chat(L, span_userdanger(I18n.T("[] drags you through the blood!", this)));
                    playsound(T, Dm.resource("sound/effects/magic/enter_blood.ogg"), 100, TRUE, -1);
                    turf targetturf = get_step(this, dir);
                    await L.forceMove(targetturf);
                    playsound(targetturf, Dm.resource("sound/effects/magic/exit_blood.ogg"), 100, TRUE, -1);
                    _addtimer(Dm.@new<datum_callback>(this, nameof(devour), L), 0.2f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm", line: 230);
                }
            }
        }
        // SLEEP_CHECK_DEATH(1, src), expanded inline
        await sleep(1);
        if (QDELETED(this) || stat == DEAD)
            return;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:233
    public override async DmTask<object> devour(mob_living victim = null)
    {
        object dot = await base.devour(victim);
        if (Dm.eq(dot, TRUE)) // a corpse was devoured
        {
            // bubblegum bubblegum in a dish, how many corpses do you wish?
            Dm.@new<obj_item_food_bubblegum_bubblegum>(loc);
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:238
    public virtual object be_aggressive()
    {
        if (BUBBLEGUM_IS_ENRAGED)
            return TRUE;
        return Dm.num(isliving(target) && HAS_TRAIT(target, TRAIT_INCAPACITATED));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:243
    public virtual object get_retreat_distance()
    {
        return Dm.truthy(be_aggressive()) ? null : Dm.initial(this, (mob_living_simple_animal_hostile p) => p.retreat_distance);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:246
    public virtual object get_minimum_distance()
    {
        return Dm.truthy(be_aggressive()) ? 1 : Dm.initial(this, (mob_living_simple_animal_hostile p) => p.minimum_distance);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:249
    public virtual void update_approach()
    {
        retreat_distance = get_retreat_distance();
        minimum_distance = Dm.num(get_minimum_distance());
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:253
    [SignalHandler]
    public virtual object blood_enrage()
    {
        if (!BUBBLEGUM_CAN_ENRAGE)
            return FALSE;
        enrage_till = world.time + enrage_time;
        update_approach();
        Waiting.Detach(change_move_delay(3.75f)); // INVOKE_ASYNC
        Waiting.NoSleep(add_atom_colour(COLOR_BUBBLEGUM_RED, TEMPORARY_COLOUR_PRIORITY));
        datum_callback cb = Dm.@new<datum_callback>(this, nameof(blood_enrage_end));
        _addtimer(cb, enrage_time, file: "code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm", line: 262);
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:264
    [SignalHandler]
    public virtual void after_charge()
    {
        try_bloodattack();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:268
    public virtual async DmTask blood_enrage_end()
    {
        update_approach();
        await change_move_delay();
        await remove_atom_colour(TEMPORARY_COLOUR_PRIORITY, COLOR_BUBBLEGUM_RED);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:273
    public virtual async DmTask change_move_delay(object newmove = null)
    {
        newmove ??= Dm.initial(this, (mob_living_simple_animal_hostile p) => p.move_to_delay);
        move_to_delay = Dm.num(newmove);
        await set_varspeed(move_to_delay);
        handle_automated_action(); // need to recheck movement otherwise move_to_delay won't update until the next checking aka will be wrong speed for a bit
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:278
    public override async DmTask on_damage_loss_changed(object amount = null, object updating_health = null, object forced = null, object damage_type = null)
    {
        await base.on_damage_loss_changed(amount, updating_health, forced, damage_type);
        anger_modifier = clamp(((maxHealth - health) / 60), 0, 20);
        enrage_time = Dm.num(Dm.initial(this, (mob_living_simple_animal_hostile_megafauna_bubblegum p) => p.enrage_time)) * clamp(anger_modifier / 20, 0.5f, 1);
        if (hallucination_charge != null)
            hallucination_charge.enraged = Dm.num(BUBBLEGUM_SMASH);
        if (Dm.num(amount) > 0 && prob(25))
        {
            obj_effect_decal_cleanable_blood_gibs_bubblegum gibs = Dm.@new<obj_effect_decal_cleanable_blood_gibs_bubblegum>(loc);
            if (prob(40))
                await step(gibs, Dm.num(pick(GLOB.cardinals)));
            else
                await gibs.setDir(pick(GLOB.cardinals));
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:291
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        if (!Dm.truthy(gibbed) && health > 0 && Dm.truthy(true_spawn) && !Dm.truthy(Dm.bitand(flags_1, ADMIN_SPAWNED_1)))
            SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_BUBBLEGUM] = TRUE;
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:296
    public override async DmTask<object> AttackingTarget(atom attacked_target = null)
    {
        object dot = await base.AttackingTarget(attacked_target);
        if (Dm.truthy(dot))
            recovery_time = world.time + 20; // can only attack melee once every 2 seconds but rapid_melee gives higher priority
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:301
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        if (BUBBLEGUM_IS_ENRAGED)
        {
            visible_message(span_danger(I18n.T("[] deflects the []! [] can't be hit with ranged weapons while enraged!", this, hitting_projectile, p_They())), span_userdanger(I18n.T("You deflect the projectile!")));
            playsound(this, SFX_BULLET_MISS, 300, TRUE);
            return BULLET_ACT_BLOCK;
        }
        return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:308
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.num(severity) <= EXPLODE_LIGHT)
            return FALSE;

        severity = EXPLODE_LIGHT; // puny mortals
        return base.ex_act(severity, target, __extra);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:315
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        update_approach();
        return await base.Move(newloc, direction, glide_size_override, update_dir);
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:319
public partial class mob_living_simple_animal_hostile_megafauna_bubblegum_hallucination : mob_living_simple_animal_hostile_megafauna_bubblegum
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:335
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        toggle_ai(AI_OFF);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:339
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(spawn_blood))
            Dm.@new<obj_effect_decal_cleanable_blood>(get_turf(this));
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:344
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        return null;
    }

    // Prevents damage, period. These are hallucinations/afterimages/whatever and should delete on their own.
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:348
    public override object can_adjust_brute_loss(object amount = null, object forced = null, object required_bodytype = null)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:351
    public override object can_adjust_fire_loss(object amount = null, object forced = null, object required_bodytype = null)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:354
    public override object can_adjust_tox_loss(object amount = null, object forced = null, float? required_biotype = null)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:357
    public override object can_adjust_oxy_loss(object amount = null, object forced = null, object required_biotype = null)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:360
    public override async DmTask OpenFire(atom A = null)
    {
        return;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:363
    public override async DmTask<object> AttackingTarget(atom attacked_target = null)
    {
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:366
    public override object try_bloodattack()
    {
        return null;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:369
public partial class obj_effect_decal_cleanable_blood_bubblegum : obj_effect_decal_cleanable_blood
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:372
    public override object can_bloodcrawl_in()
    {
        return TRUE;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:375
public partial class obj_effect_decal_cleanable_blood_gibs_bubblegum : obj_effect_decal_cleanable_blood_gibs
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/bubblegum.dm:381
    public override object can_bloodcrawl_in()
    {
        return TRUE;
    }
}
