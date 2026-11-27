// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/*

The Hierophant

The Hierophant spawns in its arena, which makes fighting it challenging but not impossible.

The text this boss speaks is ROT4, use ROT22 to decode

Cross Blasts and the AoE burst gain additional range as Hierophant loses health, while Chasers gain additional speed.

When Hierophant dies, it stops trying to murder you and shrinks into a small form, which, while much weaker, is still quite effective.
- The smaller club can place a teleport beacon, allowing the user to teleport themself and their allies to the beacon.

Difficulty: Hard

*/

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:38
public partial class mob_living_simple_animal_hostile_megafauna_hierophant : mob_living_simple_animal_hostile_megafauna
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:102
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        spawned_beacon_ref = WEAKREF(Dm.@new<obj_effect_hierophant>(loc)) as datum_weakref;
        Waiting.NoSleep(AddComponent(typeof(datum_component_boss_music), Dm.resource("sound/music/boss/hiero_boss.ogg"), COMSIG_HOSTILE_FOUND_TARGET)); // change to COMSIG_AI_BLACKBOARD_KEY_SET(BB_CURRENT_TARGET) in basic conversion
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:107
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref spawned_beacon_ref);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:139
    public override void update_cooldowns(DmList cooldown_updates = null, float? ignore_staggered = null)
    {
        ignore_staggered ??= FALSE;
        base.update_cooldowns(cooldown_updates, ignore_staggered);
        if (Dm.truthy(cooldown_updates[COOLDOWN_UPDATE_SET_CHASER]))
            chaser_cooldown = world.time + Dm.num(cooldown_updates[COOLDOWN_UPDATE_SET_CHASER]); // COOLDOWN_START
        if (Dm.truthy(cooldown_updates[COOLDOWN_UPDATE_ADD_CHASER]))
            chaser_cooldown += Dm.num(cooldown_updates[COOLDOWN_UPDATE_ADD_CHASER]);
        if (Dm.truthy(cooldown_updates[COOLDOWN_UPDATE_SET_ARENA]))
            arena_cooldown = world.time + Dm.num(cooldown_updates[COOLDOWN_UPDATE_SET_ARENA]); // COOLDOWN_START
        if (Dm.truthy(cooldown_updates[COOLDOWN_UPDATE_ADD_ARENA]))
            arena_cooldown += Dm.num(cooldown_updates[COOLDOWN_UPDATE_ADD_ARENA]);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:150
    public override async DmTask OpenFire(atom A = null)
    {
        if (Dm.truthy(blinking))
            return;

        calculate_rage();
        float blink_counter = 1 + round(anger_modifier * 0.08f);
        float cross_counter = 1 + round(anger_modifier * 0.12f);

        arena_trap(target as mob);
        update_cooldowns(Dm.list(Dm.kv(COOLDOWN_UPDATE_SET_RANGED, max(0.5f * SECONDS, ranged_cooldown_time - anger_modifier * 0.75f))), ignore_staggered: TRUE); //scale cooldown lower with high anger.

        float target_slowness = 0;
        mob_living L = null;
        if (isliving(target))
        {
            L = (mob_living)target;
            target_slowness += Dm.num(L.cached_multiplicative_slowdown);
        }
        if (client != null)
            target_slowness += 1;

        target_slowness = max(target_slowness, 1);
        chaser_speed = max(1, (3 - anger_modifier * 0.04f) + ((target_slowness - 1) * 0.5f));

        if (client != null)
        {
            switch (Dm.num(chosen_attack))
            {
                case 1:
                    await blink(target as mob);
                    break;
                case 2:
                    await chaser_swarm(blink_counter, target_slowness, cross_counter);
                    break;
                case 3:
                    await cross_blast_spam(blink_counter, target_slowness, cross_counter);
                    break;
                case 4:
                    await blink_spam(blink_counter, target_slowness, cross_counter);
                    break;
            }
            return;
        }

        if (L?.stat == DEAD && !Dm.truthy(blinking) && get_dist(this, L) > 2)
        {
            await blink(L);
            return;
        }

        if (prob(anger_modifier * 0.75f)) //major ranged attack
        {
            DmList possibilities = Dm.list();
            if (cross_counter > 1)
                Dm.append(ref possibilities, "cross_blast_spam");
            if (get_dist(this, target) > 2)
                Dm.append(ref possibilities, "blink_spam");
            if (chaser_cooldown <= world.time) // COOLDOWN_FINISHED(src, chaser_cooldown)
            {
                if (prob(anger_modifier * 2))
                    possibilities = Dm.list("chaser_swarm");
                else
                    Dm.append(ref possibilities, "chaser_swarm");
            }
            if (Dm.truthy(possibilities.len))
            {
                switch (pick(possibilities) as string)
                {
                    case "blink_spam": //blink either once or multiple times.
                        await blink_spam(blink_counter, target_slowness, cross_counter);
                        break;
                    case "cross_blast_spam": //fire a lot of cross blasts at a target.
                        await cross_blast_spam(blink_counter, target_slowness, cross_counter);
                        break;
                    case "chaser_swarm": //fire four fucking chasers at a target and their friends.
                        await chaser_swarm(blink_counter, target_slowness, cross_counter);
                        break;
                }
                return;
            }
        }

        if (chaser_cooldown <= world.time) //if chasers are off cooldown, fire some!
        {
            obj_effect_temp_visual_hierophant_chaser C = Dm.@new<obj_effect_temp_visual_hierophant_chaser>(loc, this, target, chaser_speed, FALSE);
            update_cooldowns(Dm.list(Dm.kv(COOLDOWN_UPDATE_SET_CHASER, chaser_cooldown_time)));
            if ((prob(anger_modifier) || Dm.truthy(await target.Adjacent(this))) && target != this)
            {
                obj_effect_temp_visual_hierophant_chaser OC = Dm.@new<obj_effect_temp_visual_hierophant_chaser>(loc, this, target, chaser_speed * 1.5f, FALSE);
                OC.moving = 4;
                OC.moving_dir = pick(Dm.sub(GLOB.cardinals, C.moving_dir));
            }
        }
        else if (prob(10 + (anger_modifier * 0.5f)) && get_dist(this, target) > 2)
            await blink(target as mob);
        else if (prob(70 - anger_modifier)) //a cross blast of some type
        {
            if (prob(anger_modifier * (2 / target_slowness)) && health < maxHealth * 0.5f) //we're super angry do it at all dirs
                Waiting.Detach(blasts(target as mob, GLOB.alldirs)); // INVOKE_ASYNC
            else if (prob(60))
                Waiting.Detach(blasts(target as mob, GLOB.cardinals)); // INVOKE_ASYNC
            else
                Waiting.Detach(blasts(target as mob, GLOB.diagonals)); // INVOKE_ASYNC
        }
        else //just release a burst of power
            Waiting.Detach(burst(get_turf(this))); // INVOKE_ASYNC
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:230
    public virtual async DmTask blink_spam(object blink_counter = null, object target_slowness = null, object cross_counter = null)
    {
        update_cooldowns(Dm.list(Dm.kv(COOLDOWN_UPDATE_SET_RANGED, max(0.5f * SECONDS, major_attack_cooldown - anger_modifier * 0.75f))));
        if (health < maxHealth * 0.5f && Dm.num(blink_counter) > 1)
        {
            visible_message(span_hierophant(I18n.T("\"Mx ampp rsx iwgeti.\"")));
            object oldcolor = color;
            animate(this, Dm.named("color", "#660099"), Dm.named("time", 6));
            await sleep(6); // SLEEP_CHECK_DEATH(6, src)
            if (QDELETED(this) || stat == DEAD)
                return;
            while (!QDELETED(target) && Dm.truthy(blink_counter))
            {
                if (loc == target.loc || loc == target) //we're on the same tile as them after about a second we can stop now
                    break;
                blink_counter = Dm.num(blink_counter) - 1;
                blinking = FALSE;
                await blink(target as mob);
                blinking = TRUE;
                await sleep(4 + Dm.num(target_slowness)); // SLEEP_CHECK_DEATH(4 + target_slowness, src)
                if (QDELETED(this) || stat == DEAD)
                    return;
            }
            animate(this, Dm.named("color", oldcolor), Dm.named("time", 8));
            _addtimer(Dm.@new<datum_callback>(this, nameof(atom.update_atom_colour)), 0.8f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm", line: 246);
            await sleep(8); // SLEEP_CHECK_DEATH(8, src)
            if (QDELETED(this) || stat == DEAD)
                return;
            blinking = FALSE;
        }
        else
            await blink(target as mob);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:252
    public virtual async DmTask cross_blast_spam(object blink_counter = null, object target_slowness = null, object cross_counter = null)
    {
        update_cooldowns(Dm.list(Dm.kv(COOLDOWN_UPDATE_SET_RANGED, max(0.5f * SECONDS, major_attack_cooldown - anger_modifier * 0.75f))));
        visible_message(span_hierophant(I18n.T("\"Piezi mx rsalivi xs vyr.\"")));
        blinking = TRUE;
        object oldcolor = color;
        animate(this, Dm.named("color", "#660099"), Dm.named("time", 6));
        await sleep(6); // SLEEP_CHECK_DEATH(6, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        while (!QDELETED(target) && Dm.truthy(cross_counter))
        {
            cross_counter = Dm.num(cross_counter) - 1;
            if (prob(60))
                Waiting.Detach(blasts(target as mob, GLOB.cardinals)); // INVOKE_ASYNC
            else
                Waiting.Detach(blasts(target as mob, GLOB.diagonals)); // INVOKE_ASYNC
            await sleep(6 + Dm.num(target_slowness)); // SLEEP_CHECK_DEATH(6 + target_slowness, src)
            if (QDELETED(this) || stat == DEAD)
                return;
        }
        animate(this, Dm.named("color", oldcolor), Dm.named("time", 8));
        _addtimer(Dm.@new<datum_callback>(this, nameof(atom.update_atom_colour)), 0.8f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm", line: 267);
        await sleep(8); // SLEEP_CHECK_DEATH(8, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        blinking = FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:272
    public virtual async DmTask chaser_swarm(object blink_counter = null, object target_slowness = null, object cross_counter = null)
    {
        update_cooldowns(Dm.list(Dm.kv(COOLDOWN_UPDATE_SET_RANGED, max(0.5f * SECONDS, major_attack_cooldown - anger_modifier * 0.75f))));
        visible_message(span_hierophant(I18n.T("\"Mx gerrsx lmhi.\"")));
        blinking = TRUE;
        object oldcolor = color;
        animate(this, Dm.named("color", "#660099"), Dm.named("time", 6));
        await sleep(6); // SLEEP_CHECK_DEATH(6, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        DmList targets = ListTargets() as DmList;
        DmList cardinal_copy = GLOB.cardinals.Copy();
        while (Dm.truthy(targets.len) && Dm.truthy(cardinal_copy.len))
        {
            object pickedtarget = pick(targets);
            if (targets.len >= cardinal_copy.len)
                pickedtarget = pick_n_take(targets);
            if (!(pickedtarget is mob_living picked_living) || picked_living.stat == DEAD)
            {
                pickedtarget = target;
                if (QDELETED(pickedtarget) || (pickedtarget is mob_living main_living && main_living.stat == DEAD))
                    break; //main target is dead and we're out of living targets, cancel out
            }
            obj_effect_temp_visual_hierophant_chaser C = Dm.@new<obj_effect_temp_visual_hierophant_chaser>(loc, this, pickedtarget, chaser_speed, FALSE);
            C.moving = 3;
            C.moving_dir = pick_n_take(cardinal_copy);
            await sleep(8 + Dm.num(target_slowness)); // SLEEP_CHECK_DEATH(8 + target_slowness, src)
            if (QDELETED(this) || stat == DEAD)
                return;
        }
        update_cooldowns(Dm.list(Dm.kv(COOLDOWN_UPDATE_SET_CHASER, chaser_cooldown_time)));
        animate(this, Dm.named("color", oldcolor), Dm.named("time", 8));
        _addtimer(Dm.@new<datum_callback>(this, nameof(atom.update_atom_colour)), 0.8f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm", line: 294);
        await sleep(8); // SLEEP_CHECK_DEATH(8, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        blinking = FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:299
    public virtual async DmTask blasts(mob victim = null, DmList directions = null) //fires cross blasts with a delay
    {
        directions ??= GLOB.cardinals;
        turf T = get_turf(victim);
        if (T == null)
            return;
        if (directions == GLOB.cardinals)
            Dm.@new<obj_effect_temp_visual_hierophant_telegraph_cardinal>(T, this);
        else if (directions == GLOB.diagonals)
            Dm.@new<obj_effect_temp_visual_hierophant_telegraph_diagonal>(T, this);
        else
            Dm.@new<obj_effect_temp_visual_hierophant_telegraph>(T, this);
        playsound(T, Dm.resource("sound/effects/bin/bin_close.ogg"), 75, TRUE);
        await sleep(2); // SLEEP_CHECK_DEATH(2, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging>(T, this, FALSE);
        foreach (object d in Dm.each(directions))
            blast_wall(T, d); // INVOKE_ASYNC; blast_wall never sleeps
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:315
    public virtual void blast_wall(turf T = null, object set_dir = null) //make a wall of blasts beam_range tiles long
    {
        float range = beam_range;
        turf previousturf = T;
        turf J = get_step(previousturf, Dm.num(set_dir));
        for (int i = 1; i <= range; i++)
        {
            Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging>(J, this, FALSE);
            previousturf = J;
            J = get_step(previousturf, Dm.num(set_dir));
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:324
    public virtual void arena_trap(mob victim = null) //trap a target in an arena
    {
        turf T = get_turf(victim);
        if (victim == null || victim.stat == DEAD || T == null || !(arena_cooldown <= world.time))
            return;
        if ((istype(get_area(T), typeof(area_ruin_unpowered_hierophant)) || istype(get_area(this), typeof(area_ruin_unpowered_hierophant))) && victim != this)
            return;
        update_cooldowns(Dm.list(Dm.kv(COOLDOWN_UPDATE_SET_ARENA, arena_cooldown_time)));
        foreach (object d in Dm.each(GLOB.cardinals))
            Waiting.Detach(arena_squares(T, d)); // INVOKE_ASYNC
        foreach (object t in Dm.each(RANGE_TURFS(11, T)))
        {
            if (t != null && get_dist(t, T) == 11 && !istype(t, typeof(turf_closed_indestructible_riveted_hierophant)))
            {
                Dm.@new<obj_effect_temp_visual_hierophant_wall>(t, this);
                Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging>(t, this, FALSE);
            }
        }
        if (get_dist(this, T) >= 11) //hey you're out of range I need to get closer to you!
            Waiting.Detach(blink(null, T)); // INVOKE_ASYNC(src, PROC_REF(blink), T)
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:340
    public virtual async DmTask arena_squares(turf T = null, object set_dir = null) //make a fancy effect extending from the arena target
    {
        turf previousturf = T;
        turf J = get_step(previousturf, Dm.num(set_dir));
        for (int i = 1; i <= 10; i++)
        {
            obj_effect_temp_visual_hierophant_squares HS = Dm.@new<obj_effect_temp_visual_hierophant_squares>(J);
            await HS.setDir(set_dir);
            previousturf = J;
            J = get_step(previousturf, Dm.num(set_dir));
            await sleep(0.5f); // SLEEP_CHECK_DEATH(0.5, src)
            if (QDELETED(this) || stat == DEAD)
                return;
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:350
    public virtual DmTask blink(mob victim = null) => blink(victim, victim);

    // The arena trap and the trip home pass a turf or the beacon where tg's blink takes a mob; the body only reads its turf, so
    // the second parameter carries the atom as tg passes it.
    private async DmTask blink(mob victim, atom victim_atom) //blink to a target
    {
        if (Dm.truthy(blinking) || victim_atom == null)
            return;
        turf T = get_turf(victim_atom);
        turf source = get_turf(this);
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph>(T, this);
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph>(source, this);
        playsound(T, Dm.resource("sound/effects/magic/wand_teleport.ogg"), 80, TRUE);
        playsound(source, Dm.resource("sound/machines/airlock/airlockopen.ogg"), 80, TRUE);
        blinking = TRUE;
        await sleep(2); //short delay before we start... // SLEEP_CHECK_DEATH(2, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph_teleport>(T, this);
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph_teleport>(source, this);
        foreach (object t in Dm.each(RANGE_TURFS(1, T)))
        {
            obj_effect_temp_visual_hierophant_blast_damaging B = Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging>(t, this, FALSE);
            B.damage = 30;
        }
        foreach (object t in Dm.each(RANGE_TURFS(1, source)))
        {
            obj_effect_temp_visual_hierophant_blast_damaging B = Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging>(t, this, FALSE);
            B.damage = 30;
        }
        animate(this, Dm.named("alpha", 0), Dm.named("time", 2), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT))); //fade out
        await sleep(1); // SLEEP_CHECK_DEATH(1, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        visible_message(span_hierophant_warning(I18n.T("[] fades out!", this)));
        ADD_TRAIT(this, TRAIT_UNDENSE, VANISHING_TRAIT);
        await sleep(2); // SLEEP_CHECK_DEATH(2, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        await forceMove(T);
        await sleep(1); // SLEEP_CHECK_DEATH(1, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        animate(this, Dm.named("alpha", 255), Dm.named("time", 2), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN))); //fade IN
        await sleep(1); // SLEEP_CHECK_DEATH(1, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        REMOVE_TRAIT(this, TRAIT_UNDENSE, VANISHING_TRAIT);
        visible_message(span_hierophant_warning(I18n.T("[] fades in!", this)));
        await sleep(1); //at this point the blasts we made detonate // SLEEP_CHECK_DEATH(1, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        blinking = FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:383
    public virtual async DmTask melee_blast(mob victim = null) //make a 3x3 blast around a target
    {
        if (victim == null)
            return;
        turf T = get_turf(victim);
        if (T == null)
            return;
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph>(T, this);
        playsound(T, Dm.resource("sound/effects/bin/bin_close.ogg"), 75, TRUE);
        await sleep(2); // SLEEP_CHECK_DEATH(2, src)
        if (QDELETED(this) || stat == DEAD)
            return;
        foreach (object t in Dm.each(RANGE_TURFS(1, T)))
            Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging>(t, this, FALSE);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:409
    public virtual async DmTask burst(turf original = null, object spread_speed = null)
    {
        await hierophant_burst(this, original, burst_range, spread_speed);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:412  (the earlier of two definitions)
    public object GiveTarget__d0(object new_target = null)
    {
        object dot = base.GiveTarget(new_target);
        if (new_target != null)
        {
            deltimer(respawn_timer_id);
            respawn_timer_id = null;
            return dot;
        }
        if (Dm.truthy(respawn_timer_id) || client != null || spawned_beacon_ref == null)
            return dot;
        respawn_timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(send_me_home)), 30 * SECONDS, Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME), file: "code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm", line: 420);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:422
    public virtual async DmTask send_me_home()
    {
        respawn_timer_id = null;
        obj_effect_hierophant beacon = spawned_beacon_ref?.resolve() as obj_effect_hierophant;
        if (beacon == null || client != null)
            return;
        sitting_at_center = TRUE;
        visible_message(span_hierophant_warning(I18n.T("\"Vixyvrmrk xs fewi...\"")));
        await blink(null, beacon);
        await adjust_brute_loss(min((health - maxHealth) * 0.5f, -250)); //heal for 50% of our missing health, minimum 10% of maximum health
        wander = FALSE;
        if (health > maxHealth * 0.9f)
            visible_message(span_hierophant(I18n.T("\"Vitemvw gsqtpixi. Stivexmrk ex qebmqyq ijjmgmirgc.\"")));
        else
            visible_message(span_hierophant(I18n.T("\"Vitemvw gsqtpixi. Stivexmsrep ijjmgmirgc gsqtvsqmwih.\"")));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:437
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (health > 0 || stat == DEAD)
            return null;

        await set_stat(DEAD);
        blinking = TRUE; //we do a fancy animation, release a huge burst(), and leave our staff.
        visible_message(span_hierophant(I18n.T("\"Mrmxmexmrk wipj-hiwxvygx wiuyirgi...\"")));
        visible_message(span_hierophant_warning(I18n.T("[] shrinks, releasing a massive burst of energy!", this)));
        Waiting.Detach(hierophant_burst(null, get_turf(this), 10)); // INVOKE_ASYNC(GLOBAL_PROC, ...)
        await set_stat(STABLE); // deathgasp won't run if dead, stupid
        await base.death(gibbed, __extra);
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:449
    public override void celebrate_kill(mob_living L = null)
    {
        visible_message(span_hierophant_warning(I18n.T("\"[]\"", pick(kill_phrases))));
        visible_message(span_hierophant_warning(I18n.T("[] absorbs []'s life force!", this, L)), span_userdanger(I18n.T("You absorb []'s life force, restoring your health!", L)));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:453
    public override object CanAttack(object the_target = null)
    {
        object dot = base.CanAttack(the_target);
        if (istype(the_target, typeof(mob_living_basic_mining_legion_brood))) //ignore temporary targets in favor of more permanent targets
            return FALSE;
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:458
    public override object GiveTarget(object new_target = null)
    {
        bool targets_the_same = new_target == target;
        object dot = GiveTarget__d0(new_target);
        if (Dm.truthy(dot) && target != null && !targets_the_same)
        {
            visible_message(span_hierophant_warning(I18n.T("\"[]\"", pick(target_phrases))));
            obj_effect_hierophant beacon = spawned_beacon_ref?.resolve() as obj_effect_hierophant;
            if (beacon != null && loc == beacon.loc && Dm.truthy(sitting_at_center))
                arena_trap(this);
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:467
    public override async DmTask on_damage_loss_changed(object amount = null, object updating_health = null, object forced = null, object damage_type = null)
    {
        if (Dm.num(amount) > 0 && !Dm.truthy(blinking))
        {
            wander = TRUE;
            sitting_at_center = FALSE;
        }
        await base.on_damage_loss_changed(amount, updating_health, forced, damage_type);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:473
    public override async DmTask<object> AttackingTarget(atom attacked_target = null)
    {
        if (!Dm.truthy(blinking))
        {
            if (target != null && isliving(target))
            {
                mob_living L = (mob_living)target;
                if (L.stat != DEAD)
                {
                    if (ranged_cooldown <= world.time)
                    {
                        calculate_rage();
                        update_cooldowns(Dm.list(Dm.kv(COOLDOWN_UPDATE_SET_RANGED, max(0.5f * SECONDS, ranged_cooldown_time - anger_modifier * 0.75f))), ignore_staggered: TRUE);
                        Waiting.Detach(burst(get_turf(this))); // INVOKE_ASYNC
                    }
                    else
                    {
                        burst_range = 3;
                        Waiting.Detach(burst(get_turf(this), 0.25f)); //melee attacks on living mobs cause it to release a fast burst if on cooldown // INVOKE_ASYNC
                    }
                    await OpenFire();
                    if (Dm.num(L.health) <= Dm.num(L.dead_threshold) && HAS_TRAIT(L, TRAIT_NODEATH)) //Nope, it still kills yall
                        await devour(L);
                }
                else
                    await devour(L);
            }
            else
                return await base.AttackingTarget(attacked_target);
        }
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:493
    public override async DmTask DestroySurroundings()
    {
        if (!Dm.truthy(blinking))
            await base.DestroySurroundings();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:497
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = null;
        if (!Dm.truthy(blinking))
            dot = await base.Move(newloc, direction, glide_size_override, update_dir);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:501
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (!IS_UNCONSCIOUS_OR_CRIT(this) && Dm.truthy(dot))
        {
            obj_effect_temp_visual_hierophant_squares HS = Dm.@new<obj_effect_temp_visual_hierophant_squares>(old_loc);
            await HS.setDir(movement_dir);
            playsound(this, Dm.resource("sound/vehicles/mecha/mechmove04.ogg"), 80, TRUE, -4);
            if (target != null)
                arena_trap(target as mob);
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:510
    public override object Goto(object target = null, object delay = null, object minimum_distance = null)
    {
        wander = TRUE;
        if (!Dm.truthy(blinking))
            base.Goto(target, delay, minimum_distance);
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:515
    public virtual void calculate_rage() //how angry we are overall
    {
        sitting_at_center = FALSE; //oh hey we're doing SOMETHING, clearly we might need to heal if we recall
        anger_modifier = clamp(((maxHealth - health) / 42), 0, 50);
        burst_range = Dm.num(Dm.initial(this, (mob_living_simple_animal_hostile_megafauna_hierophant p) => p.burst_range)) + round(anger_modifier * 0.08f);
        beam_range = Dm.num(Dm.initial(this, (mob_living_simple_animal_hostile_megafauna_hierophant p) => p.beam_range)) + round(anger_modifier * 0.12f);
    }
}

public static partial class GlobalProcs
{
    //expanding square
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:396
    public static async DmTask hierophant_burst(mob caster = null, turf original = null, object burst_range = null, object spread_speed = null)
    {
        float spread = spread_speed == null ? 0.5f : Dm.num(spread_speed);
        playsound(original, Dm.resource("sound/machines/airlock/airlockopen.ogg"), 750, TRUE);
        float last_dist = 0;
        foreach (object t in Dm.each(spiral_range_turfs(burst_range, original)))
        {
            turf T = t as turf;
            if (T == null)
                continue;
            float dist = get_dist(original, T);
            if (dist > last_dist)
            {
                last_dist = dist;
                await sleep(1 + min(Dm.num(burst_range) - last_dist, 12) * spread); //gets faster as it gets further out
            }
            Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging>(T, caster, FALSE);
        }
    }
}

//Hierophant overlays
// tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:522
public partial class obj_effect_temp_visual_hierophant : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:528
    [DmParams("mapload", "new_caster")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        var new_caster = Dm.arg(__extra, 0) as mob_living;
        object dot = base.Initialize(mapload, __extra);
        if (new_caster != null)
            caster = new_caster;
        return dot;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:534
public partial class obj_effect_temp_visual_hierophant_squares : obj_effect_temp_visual_hierophant
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:539
    [DmParams("mapload", "new_caster")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (ismineralturf(loc))
        {
            turf_closed_mineral M = (turf_closed_mineral)loc;
            Waiting.NoSleep(M.gets_drilled(caster));
        }
        return dot;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:546
public partial class obj_effect_temp_visual_hierophant_wall : obj_effect_temp_visual_hierophant //smoothing and pooling were not friends, but pooling is dead.
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:556
    [DmParams("mapload", "new_caster")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
        {
            QUEUE_SMOOTH_NEIGHBORS(this);
            QUEUE_SMOOTH(this);
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:562
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH_NEIGHBORS(this);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:567
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (QDELETED(caster))
            return FALSE;
        if (mover == caster.pulledby)
            return dot;
        if (isprojectile(mover))
        {
            obj_projectile proj = (obj_projectile)mover;
            if (proj.firer == caster)
                return dot;
        }
        if (mover != caster)
            return FALSE;
        return dot;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:581
public partial class obj_effect_temp_visual_hierophant_chaser : obj_effect_temp_visual_hierophant //a hierophant's chaser. follows target around, moving and producing a blast every speed deciseconds.
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:596
    [DmParams("mapload", "new_caster", "new_target", "new_speed", "is_friendly_fire")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_target = Dm.arg(__extra, 1);
        object new_speed = Dm.arg(__extra, 2);
        object is_friendly_fire = Dm.arg(__extra, 3);
        object dot = base.Initialize(mapload, __extra);
        target = new_target as mob_living;
        friendly_fire_check = Dm.num(is_friendly_fire);
        if (Dm.truthy(new_speed))
            speed = Dm.num(new_speed);
        _addtimer(Dm.@new<datum_callback>(this, nameof(seek_target)), 0.1f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm", line: 602);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:604
    public virtual object get_target_dir()
    {
        object dot = get_cardinal_dir(this, targetturf);
        if ((!Dm.eq(dot, previous_moving_dir) && Dm.eq(dot, more_previouser_moving_dir)) || Dm.num(dot) == 0) //we're alternating, recalculate
        {
            DmList cardinal_copy = GLOB.cardinals.Copy();
            Dm.remove(ref cardinal_copy, more_previouser_moving_dir);
            dot = pick(cardinal_copy);
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:611
    public virtual async DmTask seek_target()
    {
        if (!Dm.truthy(currently_seeking))
        {
            currently_seeking = TRUE;
            targetturf = get_turf(target);
            while (target != null && !QDELETED(this) && Dm.truthy(currently_seeking) && Dm.truthy(x) && Dm.truthy(y) && targetturf != null) //can this target actually be sook out
            {
                if (!Dm.truthy(moving)) //we're out of tiles to move, find more and where the target is!
                {
                    more_previouser_moving_dir = previous_moving_dir;
                    previous_moving_dir = moving_dir;
                    moving_dir = get_target_dir();
                    float standard_target_dir = get_cardinal_dir(this, targetturf);
                    if ((!Dm.eq(standard_target_dir, previous_moving_dir) && Dm.eq(standard_target_dir, more_previouser_moving_dir)) || standard_target_dir == 0)
                        moving = 1; //we would be repeating, only move a tile before checking
                    else
                        moving = standard_moving_before_recalc;
                }
                if (Dm.truthy(moving)) //move in the dir we're moving in right now
                {
                    turf T = get_turf(this);
                    for (int i = 1; i <= tiles_per_step; i++)
                    {
                        turf maybe_new_turf = get_step(T, Dm.num(moving_dir));
                        if (maybe_new_turf != null)
                            T = maybe_new_turf;
                        else
                            break;
                    }
                    await forceMove(T);
                    make_blast(); //make a blast, too
                    moving = Dm.num(moving) - 1;
                    await sleep(speed);
                }
                targetturf = get_turf(target);
            }
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:639
    public virtual void make_blast()
    {
        obj_effect_temp_visual_hierophant_blast_damaging B = Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging>(loc, caster, friendly_fire_check);
        B.damage = damage;
        B.monster_damage_boost = monster_damage_boost;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:670
public partial class obj_effect_temp_visual_hierophant_blast_damaging : obj_effect_temp_visual_hierophant_blast
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:680
    [DmParams("mapload", "new_caster", "friendly_fire")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_caster = Dm.arg(__extra, 0);
        object friendly_fire = Dm.arg(__extra, 1);
        object dot = base.Initialize(mapload, __extra);
        friendly_fire_check = Dm.num(friendly_fire);
        if (Dm.truthy(new_caster))
            Dm.append(ref hit_things, new_caster);
        if (ismineralturf(loc)) //drill mineral turfs
        {
            turf_closed_mineral M = (turf_closed_mineral)loc;
            Waiting.NoSleep(M.gets_drilled(caster));
        }
        Waiting.Detach(blast()); // INVOKE_ASYNC
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:689  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_effect_temp_visual_hierophant_blast_damaging__Initialize__loc_connections;

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:694
    public virtual async DmTask blast()
    {
        turf T = get_turf(this);
        if (T == null)
            return;
        playsound(T, Dm.resource("sound/effects/magic/blind.ogg"), 65, TRUE, -5); //make a sound
        await sleep(0.6f * SECONDS); //wait a little
        bursting = TRUE;
        await do_damage(T); //do damage and mark us as bursting
        await sleep(0.13f * SECONDS); //slightly forgiving; the burst animation is 1.5 deciseconds
        bursting = FALSE; //we no longer damage crossers
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:705
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        if (Dm.truthy(bursting))
            Waiting.NoSleep(do_damage(get_turf(this)));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:710
    public virtual async DmTask do_damage(turf T = null)
    {
        if (!Dm.truthy(damage))
            return;
        foreach (mob_living L in Dm.each<mob_living>(Dm.sub(T.contents, hit_things))) //find and damage mobs...
        {
            Dm.append(ref hit_things, L);
            if ((Dm.truthy(friendly_fire_check) && Dm.truthy(caster?.faction_check_atom(L))) || L.stat == DEAD)
                continue;
            if (L.client != null)
                flash_color(L.client, "#660099", 1);
            playsound(L, Dm.resource("sound/items/weapons/sear.ogg"), 50, TRUE, -4);
            to_chat(L, span_userdanger(I18n.T("You're struck by a []!", name)));
            object limb_to_hit = L.get_bodypart(L.get_random_valid_zone(even_weights: TRUE) as string);
            // tg passes its "penetrated" text one slot late, into silent, so the check is silent and the text is lost; kept.
            object armor = L.run_armor_check(limb_to_hit, MELEE, I18n.T("Your armor absorbs []!", this), I18n.T("Your armor blocks part of []!", this), FALSE, 50, TRUE);
            await L.apply_damage(damage, BURN, limb_to_hit, Dm.num(armor), wound_bonus: CANT_WOUND);
            if (ishostile(L))
            {
                mob_living_simple_animal_hostile H = (mob_living_simple_animal_hostile)L; //mobs find and damage you...
                if (!IS_UNCONSCIOUS_OR_CRIT(H) && H.target == null && !Dm.eq(H.AIStatus, AI_OFF) && H.client == null)
                {
                    if (!QDELETED(caster))
                    {
                        if (get_dist(H, caster) <= Dm.num(H.aggro_vision_range))
                            H.FindTarget(Dm.list(caster));
                        else
                            H.Goto(get_turf(caster), H.move_to_delay, 3);
                    }
                }
            }
            if (Dm.truthy(monster_damage_boost) && (ismegafauna(L) || istype(L, typeof(mob_living_simple_animal_hostile_asteroid))))
                await L.adjust_brute_loss(damage);
            if (caster != null)
                log_combat(caster, L, Dm.text("struck with a []", name));
        }
        foreach (obj_vehicle_sealed_mecha M in Dm.each<obj_vehicle_sealed_mecha>(Dm.sub(T.contents, hit_things))) //also damage mechs.
        {
            Dm.append(ref hit_things, M);
            foreach (object O in Dm.each(M.occupants))
            {
                mob_living occupant = O as mob_living;
                if (Dm.truthy(friendly_fire_check) && Dm.truthy(caster?.faction_check_atom(occupant)))
                    continue;
                to_chat(occupant, span_userdanger(I18n.T("Your [] is struck by a []!", M.name, name)));
                playsound(M, Dm.resource("sound/items/weapons/sear.ogg"), 50, TRUE, -4);
                await M.take_damage(damage, BURN, 0, 0);
            }
        }
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:747
public partial class obj_effect_temp_visual_hierophant_blast_visual : obj_effect_temp_visual_hierophant_blast
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:754
    [DmParams("mapload", "new_caster")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        turf src_turf = get_turf(this);
        playsound(src_turf, Dm.resource("sound/effects/magic/blind.ogg"), 65, TRUE, -5);
        return dot;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:760
public partial class obj_effect_hierophant : obj_effect
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:768
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_hierophant_club)))
            return await base.item_interaction(user, tool, modifiers, __extra);

        obj_item_hierophant_club club = (obj_item_hierophant_club)tool;
        if (club.beacon != this)
        {
            to_chat(user, span_hierophant_warning(I18n.T("You touch the beacon with the club, but nothing happens.")));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You start removing your hierophant beacon...")));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
            return ITEM_INTERACT_BLOCKING;

        playsound(this, Dm.resource("sound/effects/magic/blind.ogg"), 100, TRUE, -4);
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph_teleport>(get_turf(this), user);
        to_chat(user, span_hierophant_warning(I18n.T("You collect [], reattaching it to the club!", this)));
        club.beacon = null;
        club.update_appearance(UPDATE_ICON_STATE);
        await user.update_mob_action_buttons();
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/hierophant.dm:689  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_effect_temp_visual_hierophant_blast_damaging__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_temp_visual_hierophant_blast_damaging.on_entered)));
}
