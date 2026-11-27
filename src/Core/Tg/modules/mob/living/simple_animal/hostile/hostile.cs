// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/hostile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:1
public partial class mob_living_simple_animal_hostile : mob_living_simple_animal
{
    // GET_TARGETS_FROM(src) (__DEFINES/mobs.dm:739), expanded inline at each use (rules 11.1)
    private atom GET_TARGETS_FROM() => targets_from != null ? get_targets_from() as atom : this;

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:92
    public override object Destroy(float? force = null)
    {
        //We can't use losetarget here because fucking cursed blobs override it to do nothing the motherfuckers
        GiveTarget(null);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:97
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = base.Life(seconds_per_tick, __extra);
        if (!Dm.truthy(dot)) //dead
            GLOB.move_manager.stop_looping(this);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:102  (set waitfor = FALSE inherited, waiting-model 5.4)
    public override object handle_automated_action()
    {
        return Waiting.WaitforFalse(handle_automated_action__body(), null);
    }

    private async DmTask<object> handle_automated_action__body()
    {
        if (Dm.eq(AIStatus, AI_OFF) || QDELETED(this))
            return FALSE;
        DmList possible_targets = ListTargets() as DmList; //we look around for potential targets and make it a list for later use.

        if (Dm.truthy(environment_smash))
            await EscapeConfinement();

        if (Dm.truthy(AICanContinue(possible_targets)))
        {
            atom target_from = GET_TARGETS_FROM();
            if (!QDELETED(target) && !Dm.truthy(await target_from.Adjacent(target)))
                await DestroyPathToTarget();
            if (!Dm.truthy(await MoveToTarget(possible_targets))) //if we lose our target
            {
                if (Dm.truthy(AIShouldSleep(possible_targets))) // we try to acquire a new one
                    toggle_ai(AI_IDLE); // otherwise we go idle
            }
        }
        return 1;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:119
    public override object handle_automated_movement()
    {
        object dot = base.handle_automated_movement();
        if (Dm.truthy(dodging) && target != null && Dm.truthy(in_melee) && isturf(loc) && isturf(target.loc))
        {
            datum_callback cb = Dm.@new<datum_callback>(this, nameof(sidestep));
            if (sidestep_per_cycle > 1) //For more than one just spread them equally - this could changed to some sensible distribution later
            {
                float sidestep_delay = Dm.num(SSnpcpool?.wait) / sidestep_per_cycle;
                for (float i = 1; i <= sidestep_per_cycle; i++)
                    _addtimer(cb, (i - 1) * sidestep_delay, file: "code/modules/mob/living/simple_animal/hostile/hostile.dm", line: 125);
            }
            else //Otherwise randomize it to make the players guessing.
                _addtimer(cb, rand(1, Dm.num(SSnpcpool?.wait)), file: "code/modules/mob/living/simple_animal/hostile/hostile.dm", line: 128);
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:130
    public override async DmTask update_stamina()
    {
        await base.update_stamina();
        move_to_delay = Dm.num(Dm.initial(this, (mob_living_simple_animal_hostile p) => p.move_to_delay)) + (Dm.num(staminaloss) * 0.06f);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:134  var/static/list/cardinal_sidestep_directions, diagonal_sidestep_directions
    private static DmList cardinal_sidestep_directions => TgState.Current.mob_living_simple_animal_hostile__cardinal_sidestep_directions ??= Dm.list(-90, -45, 0, 45, 90);
    private static DmList diagonal_sidestep_directions => TgState.Current.mob_living_simple_animal_hostile__diagonal_sidestep_directions ??= Dm.list(-45, 0, 45);

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:134
    public virtual async DmTask sidestep()
    {
        if (target == null || !isturf(target.loc) || !isturf(loc) || stat == DEAD)
            return;
        float target_dir = get_dir(this, target);

        float chosen_dir = 0;
        if (Dm.truthy(Dm.bitand(target_dir, target_dir - 1)))
            chosen_dir = Dm.num(pick(diagonal_sidestep_directions));
        else
            chosen_dir = Dm.num(pick(cardinal_sidestep_directions));
        if (Dm.truthy(chosen_dir))
        {
            chosen_dir = turn(target_dir, chosen_dir);
            await Move(get_step(this, chosen_dir));
            face_atom(target); //Looks better if they keep looking at you when dodging
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:151
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!IS_UNCONSCIOUS_OR_CRIT(this) && target == null && !Dm.eq(AIStatus, AI_OFF) && client == null && user != null)
            FindTarget(Dm.list(user));
        return await base.attacked_by(attacking_item, user, modifiers, attack_modifiers);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:156
    public override async DmTask<object> electrocute_act(object shock_damage = null, object source = null, float? siemens_coeff = null, float? flags = null, params object[] __extra)
    {
        if (!IS_UNCONSCIOUS_OR_CRIT(this) && target == null && !Dm.eq(AIStatus, AI_OFF) && client == null && isatom(source)) // strings are sometimes used in electrocute_act()
            FindTarget(Dm.list(source));
        return await base.electrocute_act(shock_damage, source, siemens_coeff, flags, __extra);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:161
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        obj_projectile proj = hitting_projectile;
        if (!IS_UNCONSCIOUS_OR_CRIT(this) && target == null && !Dm.eq(AIStatus, AI_OFF) && client == null)
        {
            if (proj.firer != null && get_dist(this, proj.firer) <= aggro_vision_range)
                FindTarget(Dm.list(proj.firer));
            Goto(proj.starting, move_to_delay, 3);
        }
        return await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
    }

    //////////////HOSTILE MOB TARGETING AND AGGRESSION////////////

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:174  var/static/hostile_machines
    private static DmList hostile_machines => TgState.Current.mob_living_simple_animal_hostile__hostile_machines ??= typecacheof(Dm.list(typeof(obj_machinery_porta_turret), typeof(obj_vehicle_sealed_mecha))) as DmList;

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:170
    public virtual object ListTargets() //Step 1, find out what we can see
    {
        object dot;
        atom target_from = GET_TARGETS_FROM();
        if (!Dm.truthy(search_objects))
        {
            dot = Dm.sub(hearers(vision_range, target_from), this); //Remove self, so we don't suicide

            foreach (object HM in Dm.each(typecache_filter_list(range(vision_range, target_from), hostile_machines)))
            {
                if (Dm.truthy(can_see(target_from, HM as atom, vision_range)))
                    Dm.append(ref dot, HM);
            }
        }
        else
            dot = oview(vision_range, target_from);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:183
    public virtual object FindTarget(DmList possible_targets = null) //Step 2, filter down possible targets to things we actually care about
    {
        if (QDELETED(this))
            return null;
        DmList all_potential_targets = Dm.list();

        if (possible_targets == null)
            possible_targets = ListTargets() as DmList;

        foreach (object pos_targ__it in Dm.each(possible_targets))
        {
            atom pos_targ = (atom)pos_targ__it;
            if (Dm.truthy(Found(pos_targ))) //Just in case people want to override targeting
            {
                all_potential_targets = Dm.list(pos_targ);
                break;
            }

            if (isitem(pos_targ) && ismob(pos_targ.loc)) //If source is from an item, check the holder of it.
            {
                if (Dm.truthy(CanAttack(pos_targ.loc)))
                    Dm.append(ref all_potential_targets, pos_targ.loc);
            }
            else
            {
                if (Dm.truthy(CanAttack(pos_targ)))
                    Dm.append(ref all_potential_targets, pos_targ);
            }
        }

        object found_target = PickTarget(all_potential_targets);
        GiveTarget(found_target);
        return found_target; //We now have a target
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:208
    public virtual object PossibleThreats()
    {
        DmList dot = Dm.list();
        foreach (object pos_targ in Dm.each(ListTargets()))
        {
            atom A = pos_targ as atom;
            if (Dm.truthy(Found(A)))
            {
                dot = Dm.list(A);
                break;
            }
            if (Dm.truthy(CanAttack(A)))
            {
                Dm.append(ref dot, A);
                continue;
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:221
    public virtual object Found(atom A = null) //This is here as a potential override to pick a specific target if available
    {
        if (QDELETED(A))
            return FALSE;
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:226
    public virtual object PickTarget(DmList Targets = null) //Step 3, pick amongst the possible, attackable targets
    {
        if (target != null) //If we already have a target, but are told to pick again, calculate the lowest distance between all possible, and pick from the lowest distance targets
        {
            atom target_from = GET_TARGETS_FROM();
            foreach (object pos_targ in Dm.each(Targets))
            {
                atom A = pos_targ as atom;
                float target_dist = get_dist(target_from, target);
                float possible_target_distance = get_dist(target_from, A);
                if (target_dist < possible_target_distance)
                    Targets.Remove(A);
            }
        }
        if (!Dm.truthy(Targets.len)) //We didnt find nothin!
            return null;
        object chosen_target = pick(Targets); //Pick the remaining targets (if any) at random
        return chosen_target;
    }

    // Please do not add one-off mob AIs here, but override this function for your mob
    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:241
    public override object CanAttack(object the_target = null) //Can we actually attack a possible target?
    {
        if (!isatom(the_target))
        {
            stack_trace(Dm.text("Invalid target in CanAttack(): []", the_target), "code/modules/mob/living/simple_animal/hostile/hostile.dm", 243);
            return FALSE;
        }

        if (isturf(the_target) || QDELETED(the_target) || QDELETED(this)) // bail out on invalids
            return FALSE;

        if (ismob(the_target)) //Target is in godmode, ignore it.
        {
            mob mob = the_target as mob;
            if (HAS_TRAIT(mob, TRAIT_GODMODE))
                return FALSE;
        }

        if (see_invisible < ((atom)the_target).invisibility) //Target's invisible to us, forget it
            return FALSE;
        if (search_objects < 2)
        {
            if (isliving(the_target))
            {
                mob_living L = the_target as mob_living;
                bool faction_check = Dm.truthy(faction_check_atom(L));
                if (Dm.truthy(L.has_status_effect(typeof(datum_status_effect_shapechange_mob))))
                    faction_check = false;
                if (Dm.truthy(robust_searching))
                {
                    if (faction_check && !Dm.truthy(attack_same))
                        return FALSE;
                    if (L.stat > stat_attack)
                        return FALSE;
                    if (Dm.@in(L, friends))
                        return FALSE;
                }
                else
                {
                    if ((faction_check && !Dm.truthy(attack_same)) || IS_UNCONSCIOUS_OR_CRIT(L))
                        return FALSE;
                }
                return TRUE;
            }

            if (ismecha(the_target))
            {
                obj_vehicle_sealed_mecha M = the_target as obj_vehicle_sealed_mecha;
                foreach (object occupant in Dm.each(M.occupants))
                {
                    if (Dm.truthy(CanAttack(occupant)))
                        return TRUE;
                }
            }

            if (the_target is obj_machinery_porta_turret)
            {
                obj_machinery_porta_turret P = the_target as obj_machinery_porta_turret;
                if (Dm.truthy(P.in_faction(this))) //Don't attack if the turret is in the same faction
                    return FALSE;
                if (Dm.truthy(P.has_cover) && !Dm.truthy(P.raised)) //Don't attack invincible turrets
                    return FALSE;
                if (Dm.truthy(Dm.bitand(P.machine_stat, BROKEN))) //Or turrets that are already broken
                    return FALSE;
                return TRUE;
            }
        }

        return FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:292
    public virtual object GiveTarget(object new_target = null) //Step 4, give us our selected target
    {
        add_target(new_target);
        LosePatience();
        if (!QDELETED(target))
        {
            GainPatience();
            Aggro();
            return TRUE;
        }
        return null;
    }

    //What we do after closing in
    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:301
    public virtual async DmTask MeleeAction(float? patience = null)
    {
        patience ??= TRUE;
        if (rapid_melee > 1)
        {
            datum_callback cb = Dm.@new<datum_callback>(this, nameof(CheckAndAttack));
            float delay = Dm.num(SSnpcpool?.wait) / rapid_melee;
            for (float i = 1; i <= rapid_melee; i++)
                _addtimer(cb, (i - 1) * delay, file: "code/modules/mob/living/simple_animal/hostile/hostile.dm", line: 306);
        }
        else
            await AttackingTarget(target);
        if (Dm.truthy(patience))
            GainPatience();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:312
    public virtual async DmTask CheckAndAttack()
    {
        atom target_from = GET_TARGETS_FROM();
        if (target != null && isturf(target_from.loc) && Dm.truthy(await target.Adjacent(target_from)) && !Dm.truthy(incapacitated))
            await AttackingTarget(target);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:317
    public virtual async DmTask<object> MoveToTarget(DmList possible_targets = null) //Step 5, handle movement between us and our target
    {
        stop_automated_movement = 1;
        if (target == null || !Dm.truthy(CanAttack(target)))
        {
            LoseTarget();
            return 0;
        }
        atom target_from = GET_TARGETS_FROM();
        if (Dm.@in(target, possible_targets))
        {
            turf T = get_turf(this);
            if (target.z != T.z)
            {
                LoseTarget();
                return 0;
            }
            float target_distance = get_dist(target_from, target);
            if (Dm.truthy(ranged)) //We ranged? Shoot at em
            {
                if (!Dm.truthy(await target.Adjacent(target_from)) && ranged_cooldown <= world.time) //But make sure they're not in range for a melee attack and our range attack is off cooldown
                    await OpenFire(target);
            }
            if (!Dm.truthy(await Process_Spacemove(0))) //Drifting
            {
                GLOB.move_manager.stop_looping(this);
                return 1;
            }
            if (retreat_distance != null) //If we have a retreat distance, check if we need to run from our target
            {
                if (target_distance <= Dm.num(retreat_distance)) //If target's closer than our retreat distance, run
                    GLOB.move_manager.move_away(this, target, retreat_distance, move_to_delay, flags: MOVEMENT_LOOP_IGNORE_GLIDE);
                else
                    Goto(target, move_to_delay, minimum_distance); //Otherwise, get to our minimum distance so we chase them
            }
            else
                Goto(target, move_to_delay, minimum_distance);
            if (target != null)
            {
                if (isturf(target_from.loc) && Dm.truthy(await target.Adjacent(target_from))) //If they're next to us, attack
                    await MeleeAction();
                else
                {
                    if (rapid_melee > 1 && target_distance <= melee_queue_distance)
                        await MeleeAction(FALSE);
                    in_melee = FALSE; //If we're just preparing to strike do not enter sidestep mode
                }
                return 1;
            }
            return 0;
        }
        if (Dm.truthy(environment_smash))
        {
            if (target.loc != null && get_dist(target_from, target.loc) <= vision_range) //We can't see our target, but he's in our vision range still
            {
                if (Dm.truthy(ranged_ignores_vision) && ranged_cooldown <= world.time) //we can't see our target... but we can fire at them!
                    await OpenFire(target);
                if (environment_smash >= ENVIRONMENT_SMASH_WALLS) //If we're capable of smashing through walls, forget about vision completely after finding our target
                {
                    Goto(target, move_to_delay, minimum_distance);
                    await FindHidden();
                    return 1;
                }
                else
                {
                    if (Dm.truthy(await FindHidden()))
                        return 1;
                }
            }
        }
        LoseTarget();
        return 0;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:365
    public override object Goto(object target = null, object delay = null, object minimum_distance = null)
    {
        if (Dm.truthy(prevent_goto_movement))
            return FALSE;
        if (target == this.target)
            approaching_target = TRUE;
        else
            approaching_target = FALSE;
        GLOB.move_manager.move_to(this, target, minimum_distance, delay, flags: MOVEMENT_LOOP_IGNORE_GLIDE);
        return TRUE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:375
    public override void toggle_ai_on_damage(params object[] __extra)
    {
        base.toggle_ai_on_damage(__extra);
        if (search_objects >= 3)
            return;
        if (Dm.truthy(search_objects)) //Turn off item searching and ignore whatever item we were looking at, we're more concerned with fight or flight
        {
            LoseTarget();
            LoseSearchObjects();
        }
        if (!Dm.eq(AIStatus, AI_ON) && !Dm.eq(AIStatus, AI_OFF))
        {
            toggle_ai(AI_ON);
            FindTarget();
        }
        else if (target != null && prob(40)) //No more pulling a mob forever and having a second player attack it, it can switch targets now if it finds a more suitable one
            FindTarget();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:388
    public virtual async DmTask<object> AttackingTarget(atom attacked_target = null)
    {
        in_melee = TRUE;
        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_HOSTILE_PRE_ATTACKINGTARGET, Dm.list(this, attacked_target)), COMPONENT_HOSTILE_NO_ATTACK)))
            return FALSE; //but more importantly return before attack_animal called
        object result = await attacked_target.attack_animal(this);
        SEND_SIGNAL(COMSIG_HOSTILE_POST_ATTACKINGTARGET, Dm.list(this, attacked_target, result));
        return result;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:396
    public virtual void Aggro()
    {
        vision_range = aggro_vision_range;
        if (target != null && Dm.truthy(emote_taunt.len) && prob(taunt_chance))
        {
            manual_emote(I18n.T("[] at [].", pick(emote_taunt), target));
            taunt_chance = max(taunt_chance - 7, 2);
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:403
    public virtual void LoseAggro()
    {
        stop_automated_movement = 0;
        vision_range = Dm.num(Dm.initial(this, (mob_living_simple_animal_hostile p) => p.vision_range));
        taunt_chance = Dm.num(Dm.initial(this, (mob_living_simple_animal_hostile p) => p.taunt_chance));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:408
    public virtual void LoseTarget()
    {
        GiveTarget(null);
        approaching_target = FALSE;
        in_melee = FALSE;
        GLOB.move_manager.stop_looping(this);
        LoseAggro();
    }

    //////////////END HOSTILE MOB TARGETING AND AGGRESSION////////////

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:417
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        LoseTarget();
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:421
    public virtual void summon_backup(object distance = null, object exact_faction_match = null)
    {
        do_alert_animation();
        playsound(loc, Dm.resource("sound/machines/chime.ogg"), 50, TRUE, -1);
        atom target_from = GET_TARGETS_FROM();
        foreach (mob_living_simple_animal_hostile M in Dm.each<mob_living_simple_animal_hostile>(oview(distance, target_from)))
        {
            if (Dm.truthy(faction_check_atom(M, TRUE)))
            {
                if (Dm.eq(M.AIStatus, AI_OFF))
                    return;
                else
                    M.Goto(this, M.move_to_delay, M.minimum_distance);
            }
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:432
    public virtual object CheckFriendlyFire(atom A = null)
    {
        if (Dm.truthy(check_friendly_fire))
        {
            foreach (turf T in Dm.each<turf>(get_line(this, A))) // Not 100% reliable but this is faster than simulating actual trajectory
            {
                foreach (mob_living L in Dm.each<mob_living>(T))
                {
                    if (L == this || L == A)
                        continue;
                    if (Dm.truthy(faction_check_atom(L)) && !Dm.truthy(attack_same))
                        return TRUE;
                }
            }
        }
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:441
    public virtual async DmTask OpenFire(atom A = null)
    {
        if (Dm.truthy(CheckFriendlyFire(A)))
            return;
        if (!Dm.truthy(Dm.bitand(simple_mob_flags, SILENCE_RANGED_MESSAGE)))
            visible_message(span_danger(I18n.T("<b>[]</b> [] at []!", this, ranged_message, A)));

        if (rapid > 1)
        {
            datum_callback cb = Dm.@new<datum_callback>(this, nameof(Shoot), A);
            for (float i = 1; i <= rapid; i++)
                _addtimer(cb, (i - 1) * rapid_fire_delay, file: "code/modules/mob/living/simple_animal/hostile/hostile.dm", line: 451);
        }
        else
            await Shoot(A);
        ranged_cooldown = world.time + ranged_cooldown_time;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:457
    public virtual async DmTask Shoot(atom targeted_atom = null)
    {
        atom target_from = GET_TARGETS_FROM();
        if (QDELETED(targeted_atom) || targeted_atom == target_from.loc || targeted_atom == target_from)
            return;
        turf startloc = get_turf(target_from);
        face_atom(targeted_atom);
        if (casingtype != null)
        {
            obj_item_ammo_casing casing = Dm.@new(casingtype, startloc) as obj_item_ammo_casing;
            playsound(this, projectilesound, 100, TRUE);
            object targeted_zone;
            if (ismob(targeted_atom))
            {
                mob targeted_mob = targeted_atom as mob;
                targeted_zone = targeted_mob.get_random_valid_zone();
            }
            else
                targeted_zone = ran_zone();
            await casing.fire_casing(targeted_atom, this, null, null, null, targeted_zone, 0, this);
            return;
        }
        if (projectiletype != null)
        {
            await fire_projectile(projectiletype, targeted_atom, projectilesound);
            if (!Dm.eq(AIStatus, AI_ON)) //Don't want mindless mobs to have their movement screwed up firing in space
                await newtonian_move(get_angle(targeted_atom, target_from));
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:480
    public virtual object CanSmashTurfs(turf T = null)
    {
        return Dm.num(iswallturf(T) || ismineralturf(T));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:484
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        if (Dm.truthy(dodging) && Dm.truthy(approaching_target) && prob(dodge_prob) && moving_diagonally == 0 && isturf(loc) && isturf(newloc))
            return await dodge(newloc, direction);
        else
            return await base.Move(newloc, direction, glide_size_override, update_dir);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:490
    public virtual async DmTask<object> dodge(object moving_to = null, object move_direction = null)
    {
        //Assuming we move towards the target we want to swerve toward them to get closer
        float cdir = turn(Dm.num(move_direction), 45);
        float ccdir = turn(Dm.num(move_direction), -45);
        dodging = FALSE;
        object dot = await Move(get_step(loc, Dm.num(pick(cdir, ccdir))));
        if (!Dm.truthy(dot)) //Can't dodge there so we just carry on
            dot = await Move(moving_to as atom, move_direction);
        dodging = TRUE;
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:500
    public virtual async DmTask DestroyObjectsInDirection(object direction = null)
    {
        atom target_from = GET_TARGETS_FROM();
        turf T = get_step(target_from, Dm.num(direction));
        if (QDELETED(T))
            return;
        if (Dm.truthy(await T.Adjacent(target_from)))
        {
            if (Dm.truthy(CanSmashTurfs(T)))
            {
                await T.attack_animal(this);
                return;
            }
        }
        foreach (obj O in Dm.each<obj>(T.contents))
        {
            if (!Dm.truthy(await O.Adjacent(target_from)))
                continue;
            if ((ismachinery(O) || isstructure(O)) && Dm.truthy(O.density) && environment_smash >= ENVIRONMENT_SMASH_STRUCTURES && !Dm.truthy(O.IsObscured()))
            {
                await O.attack_animal(this);
                return;
            }
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:516
    public virtual async DmTask DestroyPathToTarget()
    {
        if (Dm.truthy(environment_smash))
        {
            await EscapeConfinement();
            atom target_from = GET_TARGETS_FROM();
            float dir_to_target = get_dir(target_from, target);
            DmList dir_list = Dm.list();
            if (Dm.truthy(ISDIAGONALDIR(dir_to_target))) //it's diagonal, so we need two directions to hit
            {
                foreach (object direction in Dm.each(GLOB.cardinals))
                {
                    if (Dm.truthy(Dm.bitand(direction, dir_to_target)))
                        Dm.append(ref dir_list, direction);
                }
            }
            else
                Dm.append(ref dir_list, dir_to_target);
            foreach (object direction in Dm.each(dir_list)) //now we hit all of the directions we got in this fashion, since it's the only directions we should actually need
                await DestroyObjectsInDirection(direction);
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:532
    public virtual async DmTask DestroySurroundings() // for use with megafauna destroying everything around them
    {
        if (Dm.truthy(environment_smash))
        {
            await EscapeConfinement();
            foreach (object dir in Dm.each(GLOB.cardinals))
                await DestroyObjectsInDirection(dir);
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:539
    public virtual async DmTask EscapeConfinement()
    {
        atom target_from = GET_TARGETS_FROM();
        if (buckled != null)
            await buckled.attack_animal(this);
        if (!isturf(target_from.loc) && target_from.loc != null) //Did someone put us in something?
        {
            atom A = target_from.loc;
            await A.attack_animal(this); //Bang on it till we get out
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:547
    public virtual async DmTask<object> FindHidden()
    {
        if (target == null)
            return FALSE;
        if (target.loc is obj_structure_closet || target.loc is obj_machinery_disposal || target.loc is obj_machinery_sleeper)
        {
            atom A = target.loc;
            atom target_from = GET_TARGETS_FROM();
            Goto(A, move_to_delay, minimum_distance);
            if (Dm.truthy(await A.Adjacent(target_from)))
                await A.attack_animal(this);
            return 1;
        }
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:559
    public override async DmTask<object> RangedAttack(atom A = null, object modifiers = null) //Player firing
    {
        if (Dm.truthy(ranged) && ranged_cooldown <= world.time)
        {
            GiveTarget(A);
            await OpenFire(A);
        }
        return await base.RangedAttack(A, modifiers);
    }

    ////// AI Status ///////
    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:567
    public virtual object AICanContinue(DmList possible_targets = null)
    {
        if (QDELETED(this))
            return FALSE;
        if (Dm.eq(AIStatus, AI_ON))
            return TRUE;
        else if (Dm.eq(AIStatus, AI_IDLE))
        {
            if (Dm.truthy(FindTarget(possible_targets)))
            {
                toggle_ai(AI_ON); //Wake up for more than one Life() cycle.
                return TRUE;
            }
            else
                return FALSE;
        }
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:580
    public virtual object AIShouldSleep(DmList possible_targets = null)
    {
        return Dm.num(!Dm.truthy(FindTarget(possible_targets)));
    }

    //These two procs handle losing our target if we've failed to attack them for
    //more than lose_patience_timeout deciseconds, which probably means we're stuck
    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:586
    public virtual void GainPatience()
    {
        if (Dm.truthy(lose_patience_timeout))
        {
            LosePatience();
            if (!QDELETED(this))
                lose_patience_timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(LoseTarget)), lose_patience_timeout, TIMER_STOPPABLE, file: "code/modules/mob/living/simple_animal/hostile/hostile.dm", line: 590);
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:593
    public virtual void LosePatience()
    {
        deltimer(lose_patience_timer_id);
    }

    //These two procs handle losing and regaining search_objects when attacked by a mob
    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:598
    public virtual void LoseSearchObjects()
    {
        search_objects = 0;
        deltimer(search_objects_timer_id);
        search_objects_timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(RegainSearchObjects)), search_objects_regain_time, TIMER_STOPPABLE, file: "code/modules/mob/living/simple_animal/hostile/hostile.dm", line: 601);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:604
    public virtual void RegainSearchObjects(object value = null)
    {
        if (!Dm.truthy(value))
            value = Dm.initial(this, (mob_living_simple_animal_hostile p) => p.search_objects);
        search_objects = Dm.num(value);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:609
    public virtual object ListTargetsLazy(object _Z = null) //Step 1, find out what we can see
    {
        DmList dot = Dm.list();
        foreach (object I in Dm.each(datum_controller_subsystem_mobs.clients_by_zlevel[_Z]))
        {
            mob M = I as mob;
            if (get_dist(M, this) < vision_range)
            {
                if (isturf(M.loc))
                    Dm.append(ref dot, M);
                else if (Dm.@in(M.loc.type, hostile_machines))
                    Dm.append(ref dot, M.loc);
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:620
    public virtual object get_targets_from()
    {
        atom target_from = targets_from.resolve() as atom;
        if (target_from == null)
        {
            targets_from = null;
            return this;
        }
        return target_from;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:627
    [SignalHandler]
    public virtual void handle_target_del(datum source = null)
    {
        UnregisterSignal(target, COMSIG_QDELETING);
        target = null;
        LoseTarget();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:633
    [SignalHandler]
    public virtual void handle_friend_del(datum source = null)
    {
        UnregisterSignal(source, COMSIG_QDELETING);
        friends.Remove(source);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:638
    public virtual void add_target(object new_target = null)
    {
        SEND_SIGNAL(COMSIG_HOSTILE_FOUND_TARGET, Dm.list(this, new_target));
        if (target != null)
            UnregisterSignal(target, COMSIG_QDELETING);
        target = new_target as atom;
        if (target != null)
            RegisterSignal(target, COMSIG_QDELETING, nameof(handle_target_del));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:646
    public override async DmTask<object> befriend(mob_living new_friend = null)
    {
        object dot = await base.befriend(new_friend);
        if (!Dm.truthy(dot))
            return dot;
        Dm.append(ref friends, new_friend);
        RegisterSignal(new_friend, COMSIG_QDELETING, nameof(handle_friend_del));
        // SET_FACTION_AND_ALLIES_FROM(this, new_friend) (__DEFINES/mobfactions.dm:119), inline
        set_faction(new_friend.get_faction());
        set_allies(LAZYLISTDUPLICATE(new_friend.allies));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:654
    public override async DmTask lazarus_revive(mob_living reviver = null, object malfunctioning = null)
    {
        await base.lazarus_revive(reviver, malfunctioning);
        if (Dm.truthy(malfunctioning))
        {
            robust_searching = TRUE; // enables friends list check
            return;
        }
        robust_searching = Dm.num(Dm.initial(this, (mob_living_simple_animal_hostile p) => p.robust_searching));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/simple_animal/hostile/hostile.dm:139-140, :174 (var/static, rules 10.6)
    internal DmList mob_living_simple_animal_hostile__cardinal_sidestep_directions;
    internal DmList mob_living_simple_animal_hostile__diagonal_sidestep_directions;
    internal DmList mob_living_simple_animal_hostile__hostile_machines;
}
