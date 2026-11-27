// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/living.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M2 P1 (the living core): the M2 subset of living.dm is translated (scope 2e "living": Initialize, Destroy, Bump and
// MobBump, PushAM, succumb, resting and getting up, lying down and standing up, health, revive, fully_heal, Move and the
// crawl angle, gravity, z registration, the body position, the floored and handsblocked conditions, set_stat and the
// limb counts); the procs outside it keep Dm.NotTranslated with their milestones ("not in M2"). Gaps RG2: do_strange_reagent_revival
// and itch.

// tg: code/modules/mob/living/living.dm:487
public partial class mob_living : mob
{
    /**
     * Updates the offsets of the passed mob according to the passed grab state and the direction between them and us
     *
     * * M - the mob to update the offsets of
     * * grab_state - the state of the grab
     * * animate - whether or not to animate the offsets
     */
    // tg: code/modules/mob/living/living.dm:487  (pulling, 2026-10-15)
    public virtual async DmTask set_pull_offsets(mob_living mob_to_set = null, float? grab_state = null, float? animate = null)
    {
        grab_state ??= GRAB_PASSIVE;
        animate ??= TRUE;
        if (Dm.truthy(mob_to_set.buckled))
            return; //don't make them change direction or offset them if they're buckled into something.
        float offset = 0;
        switch (grab_state.Value)
        {
            case GRAB_PASSIVE:
                offset = GRAB_PIXEL_SHIFT_PASSIVE;
                break;
            case GRAB_AGGRESSIVE:
                offset = GRAB_PIXEL_SHIFT_AGGRESSIVE;
                break;
            case GRAB_NECK:
                offset = GRAB_PIXEL_SHIFT_NECK;
                break;
            case GRAB_KILL:
                offset = GRAB_PIXEL_SHIFT_NECK;
                break;
        }
        await mob_to_set.setDir(get_dir(mob_to_set, this));
        float dir_filter = Dm.num(mob_to_set.dir);
        if (Dm.truthy(ISDIAGONALDIR(dir_filter)))
            dir_filter = EWCOMPONENT(dir_filter);
        switch (dir_filter)
        {
            case NORTH:
                mob_to_set.add_offsets(GRABBING_TRAIT, x_add: 0, y_add: offset, animate: animate);
                break;
            case SOUTH:
                mob_to_set.add_offsets(GRABBING_TRAIT, x_add: 0, y_add: -offset, animate: animate);
                break;
            case EAST:
                if (mob_to_set.lying_angle == LYING_ANGLE_WEST) //update the dragged dude's direction if we've turned
                    await mob_to_set.set_lying_angle(LYING_ANGLE_EAST);
                mob_to_set.add_offsets(GRABBING_TRAIT, x_add: offset, y_add: 0, animate: animate);
                break;
            case WEST:
                if (mob_to_set.lying_angle == LYING_ANGLE_EAST)
                    await mob_to_set.set_lying_angle(LYING_ANGLE_WEST);
                mob_to_set.add_offsets(GRABBING_TRAIT, x_add: -offset, y_add: 0, animate: animate);
                break;
        }
    }
}

// tg: code/modules/mob/living/living.dm:30
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living.dm:1
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (initial_size != RESIZE_DEFAULT_SIZE)
            Waiting.NoSleep(update_transform(initial_size));
        Waiting.NoSleep(AddElement(typeof(datum_element_movetype_handler)));
        Waiting.NoSleep(register_init_signals());
        if (Dm.truthy(unique_name))
            set_name();
        update_blood_status();
        update_blood_effects();
        datum_atom_hud_data_human_medical_advanced medhud = GLOB.huds[DATA_HUD_MEDICAL_ADVANCED] as datum_atom_hud_data_human_medical_advanced;
        medhud.add_atom_to_hud(this);
        datum_atom_hud_data_diagnostic diag_hud = GLOB.huds[DATA_HUD_DIAGNOSTIC] as datum_atom_hud_data_diagnostic;
        diag_hud.add_atom_to_hud(this);
        // tg: :15
        add_ally(this);
        if (!Dm.truthy(pull_force))
        {
            // GAP(lead): remove_verb (__HELPERS/verbs.dm) and /mob/living/verb/pulled are verbs, the player command layer (rules 7.10, M9-M10).
            Dm.Deferred("code/modules/mob/living/living.dm:17", "Initialize: remove_verb(src, /mob/living/verb/pulled), the Pull verb (verbs, M10+)");
        }
        Dm.append(ref GLOB.mob_living_list, this);
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this)); // tg: :19 (M10x slice C: the ghosts' orbit menu)
        // GAP(lead): /mob/living/proc/update_fov (modules/mob/living/living_fov.dm) is not declared; the field of view cone (M9).
        Dm.Deferred("code/modules/mob/living/living.dm:20", "Initialize: update_fov(), the field of view cone (living_fov.dm, M10+)");
        Waiting.NoSleep(gravity_setup());
        unconscious_appearance = get_unconscious_appearance() as image;
        if (unconscious_appearance != null)
            Dm.append(ref GLOB.unconscious_appearances, unconscious_appearance);
        return dot;
    }

    // tg: code/modules/mob/living/living.dm:26
    public override void prepare_huds()
    {
        base.prepare_huds();
        prepare_data_huds();
    }

    // tg: code/modules/mob/living/living.dm:30
    public virtual void prepare_data_huds()
    {
        med_hud_set_health();
        med_hud_set_status();
    }

    /// Returns the appearance other mobs see instead of us while unconscious
    // tg: code/modules/mob/living/living.dm:35
    public virtual object get_unconscious_appearance()
    {
        return null;
    }

    // tg: code/modules/mob/living/living.dm:38
    public override object Destroy(float? force = null)
    {
        foreach (datum_status_effect effect in Dm.each_cast<datum_status_effect>(status_effects))
        {
            // The status effect calls on_remove when its mob is deleted
            if (Dm.truthy(effect.on_remove_on_mob_delete))
                qdel(effect);

            else
                Waiting.NoSleep(effect.be_replaced());
        }

        clear_personalities(); // must be done for the personalities which process

        if (buckled != null)
            Waiting.NoSleep(buckled.unbuckle_mob(this, force: 1));

        remove_from_all_data_huds();
        Dm.remove(ref GLOB.mob_living_list, this);
        if (Dm.truthy(imaginary_group))
        {
            Dm.remove(ref imaginary_group, this);
            QDEL_LIST(imaginary_group);
        }
        QDEL_LAZYLIST(ref diseases);
        QDEL_LAZYLIST(ref quirks);
        QDEL_NULL(ref inner_armor);

        if (unconscious_appearance != null)
        {
            // Not super necessary strictly speaking but just in case
            foreach (client player in Dm.each_cast<client>(GLOB.clients))
                player?.images.Remove(unconscious_appearance);
            Dm.remove(ref GLOB.unconscious_appearances, unconscious_appearance);
            unconscious_appearance = null;
        }

        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/living.dm:69
    public override async DmTask<object> onZImpact(turf impacted_turf = null, object levels = null, float? impact_flags = null)
    {
        impact_flags ??= NONE;
        if (!isgroundlessturf(impacted_turf))
            Dm.combine(ref impact_flags, await ZImpactDamage(impacted_turf, levels));

        return await base.onZImpact(impacted_turf, levels, impact_flags);
    }

    // tg: code/modules/mob/living/living.dm:81
    public virtual async DmTask<object> ZImpactDamage(turf impacted_turf = null, object levels = null)
    {
        object dot = null;
        dot = (!Dm.truthy(this._listen_lookup?[COMSIG_LIVING_Z_IMPACT]) ? NONE : this._SendSignal(COMSIG_LIVING_Z_IMPACT, Dm.list(this, levels, impacted_turf)));
        if (Dm.truthy(Dm.bitand(dot, ZIMPACT_CANCEL_DAMAGE)))
            return dot;
        // multiplier for the damage taken from falling
        float damage_softening_multiplier = 1;

        obj_item_organ_cyberimp_chest_spine potential_spine = get_organ_slot(ORGAN_SLOT_SPINE) as obj_item_organ_cyberimp_chest_spine;
        if (potential_spine is obj_item_organ_cyberimp_chest_spine)
            damage_softening_multiplier *= potential_spine.athletics_boost_multiplier;

        // If you are incapped, you probably can't brace yourself
        bool can_help_themselves = !Dm.truthy(INCAPACITATED_IGNORING(this, INCAPABLE_RESTRAINTS));
        if (Dm.le(levels, 1) && can_help_themselves)
        {
            obj_item_organ_wings gliders = get_organ_by_type(typeof(obj_item_organ_wings)) as obj_item_organ_wings;
            if (HAS_TRAIT(this, TRAIT_FREERUNNING) || Dm.truthy(gliders?.can_soften_fall())) // the power of parkour or wings allows falling short distances unscathed
            {
                bool graceful_landing = HAS_TRAIT(this, TRAIT_CATLIKE_INSTINCT);

                if (graceful_landing)
                {
                    await add_movespeed_modifier(typeof(datum_movespeed_modifier_landed_on_feet));
                    _addtimer(Dm.@new<datum_callback>(this, nameof(mob.remove_movespeed_modifier), typeof(datum_movespeed_modifier_landed_on_feet)), Dm.num(levels) * 3 * SECONDS, file: "code/modules/mob/living/living.dm", line: 101);
                }
                else
                {
                    await Knockdown(Dm.num(levels) * 4 * SECONDS);
                    await emote("spin");
                }

                visible_message(
                    span_notice(I18n.T("[] makes a hard landing on [] but remains unharmed from the fall[].", this, impacted_turf, graceful_landing ? I18n.T(" and stays on [] feet", p_their()) : I18n.T(" by tucking in rolling into the landing"))),
                    span_notice(I18n.T("You brace for the fall. You make a hard landing on [], but remain unharmed[].", impacted_turf, graceful_landing ? I18n.T(" while landing on your feet") : I18n.T(" by tucking in and rolling into the landing")))
                );
                return Dm.bitor(dot, ZIMPACT_NO_MESSAGE);
            }
        }

        float incoming_damage = Dm.pow(Dm.num(levels) * 5, 1.5f);
        // Smaller mobs with catlike grace can ignore damage (EG: cats)
        bool small_surface_area = Dm.num(mob_size) <= MOB_SIZE_SMALL;
        bool skip_knockdown = false;
        if (HAS_TRAIT(this, TRAIT_CATLIKE_INSTINCT) && (small_surface_area || usable_legs >= 2) && Dm.eq(body_position, STANDING_UP) && can_help_themselves)
        {
            Dm.combine(ref dot, Dm.bitor(ZIMPACT_NO_MESSAGE, ZIMPACT_NO_SPIN));
            skip_knockdown = true;
            if (small_surface_area)
            {
                visible_message(
                    span_notice(I18n.T("[] makes a hard landing on [], but lands safely on [] feet!", this, impacted_turf, p_their())),
                    span_notice(I18n.T("You make a hard landing on [], but land safely on your feet!", impacted_turf))
                );
                Dm.@new<obj_effect_temp_visual_mook_dust_small>(impacted_turf);
                return dot;
            }

            incoming_damage *= 1.66f;
            await add_movespeed_modifier(typeof(datum_movespeed_modifier_landed_on_feet));
            _addtimer(Dm.@new<datum_callback>(this, nameof(mob.remove_movespeed_modifier), typeof(datum_movespeed_modifier_landed_on_feet)), Dm.num(levels) * 2 * SECONDS, file: "code/modules/mob/living/living.dm", line: 129);
            visible_message(
                span_danger(I18n.T("[] makes a hard landing on [], landing on [] feet painfully!", this, impacted_turf, p_their())),
                span_userdanger(I18n.T("You make a hard landing on [], and instinctively land on your feet - painfully!", impacted_turf))
            );
            Dm.@new<obj_effect_temp_visual_mook_dust>(impacted_turf);
        }

        if (Dm.eq(body_position, STANDING_UP))
        {
            float damage_for_each_leg = round((incoming_damage / 2) * damage_softening_multiplier);
            await apply_damage(damage_for_each_leg, BRUTE, BODY_ZONE_L_LEG, wound_bonus: -2.5f * Dm.num(levels));
            await apply_damage(damage_for_each_leg, BRUTE, BODY_ZONE_R_LEG, wound_bonus: -2.5f * Dm.num(levels));
        }
        else
            await apply_damage(incoming_damage, BRUTE, spread_damage: TRUE);

        if (!skip_knockdown)
            await Knockdown(Dm.num(levels) * 5 * SECONDS);
        return dot;
    }

    //Generic Bump(). Override MobBump() and ObjBump() instead of this.
    // tg: code/modules/mob/living/living.dm:153
    [DmParams("A")]
    public override async DmTask<object> Bump(atom A = null)
    {
        if (Dm.truthy(await base.Bump(A))) //we are thrown onto something
            return null;
        if (Dm.truthy(buckled) || Dm.truthy(now_pushing))
            return null;
        if (ismob(A))
        {
            mob M = A as mob;
            if (Dm.truthy(await MobBump(M)))
                return null;
        }
        if (isobj(A))
        {
            obj O = A as obj;
            // DM: if(ObjBump(O)) return; ObjBump's slot has no value (rules 7.4: a void slot's value is null), so the
            // test is always false.
            ObjBump(O);
        }
        if (ismovable(A))
        {
            atom_movable AM = A as atom_movable;
            if (Dm.truthy(await PushAM(AM, move_force)))
                return null;
        }
        return null;
    }

    // tg: code/modules/mob/living/living.dm:171
    [DmParams("AM")]
    public override object Bumped(atom_movable AM = null)
    {
        base.Bumped(AM);
        last_bumped = world.time;
        return null;
    }

    //Called when we bump onto a mob
    // tg: code/modules/mob/living/living.dm:176
    public virtual async DmTask<object> MobBump(mob M = null)
    {
        //No bumping/swapping/pushing others if you are on walk intent
        if (move_intent == MOVE_INTENT_WALK)
            return TRUE;

        if (Dm.truthy(Dm.bitand((!Dm.truthy(M._listen_lookup?[COMSIG_LIVING_PRE_MOB_BUMP]) ? NONE : M._SendSignal(COMSIG_LIVING_PRE_MOB_BUMP, Dm.list(M, this))), COMPONENT_LIVING_BLOCK_PRE_MOB_BUMP)))
            return TRUE;

        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_MOB_BUMP]))
            _SendSignal(COMSIG_LIVING_MOB_BUMP, Dm.list(this, M));
        if (Dm.truthy(M._listen_lookup?[COMSIG_LIVING_MOB_BUMPED]))
            M._SendSignal(COMSIG_LIVING_MOB_BUMPED, Dm.list(M, this));
        //Even if we don't push/swap places, we "touched" them, so spread fire
        await spreadFire(M as mob_living);

        if (Dm.truthy(now_pushing))
            return TRUE;

        if (isliving(M))
        {
            mob_living L = M as mob_living;
            //Also spread diseases
            foreach (object thing in Dm.each(diseases))
            {
                datum_disease D = (datum_disease)thing;
                if (Dm.truthy(Dm.bitand(D.spread_flags, DISEASE_SPREAD_CONTACT_SKIN)))
                    await L.ContactContractDisease(D); // gaps VI1
            }

            foreach (object thing in Dm.each(L.diseases))
            {
                datum_disease D = (datum_disease)thing;
                if (Dm.truthy(Dm.bitand(D.spread_flags, DISEASE_SPREAD_CONTACT_SKIN)))
                    await ContactContractDisease(D); // gaps VI1
            }

            //Should stop you pushing a restrained person out of the way
            if (Dm.truthy(L.pulledby) && L.pulledby != this && HAS_TRAIT(L, TRAIT_RESTRAINED))
            {
                if (!Dm.truthy(Dm.mod(world.time, 5)))
                    to_chat(this, span_warning(I18n.T("[] is restrained, you cannot push past.", L)));
                return TRUE;
            }

            if (Dm.truthy(L.pulling))
            {
                if (ismob(L.pulling))
                {
                    mob P = L.pulling as mob;
                    if (HAS_TRAIT(P, TRAIT_RESTRAINED))
                    {
                        if (!Dm.truthy(Dm.mod(world.time, 5)))
                            to_chat(this, span_warning(I18n.T("[] is restraining [], you cannot push past.", L, P)));
                        return TRUE;
                    }
                }
            }
        }

        if (Dm.truthy(moving_diagonally))//no mob swap during diagonal moves.
            return TRUE;

        if (!Dm.truthy(M.buckled) && !Dm.truthy(M.has_buckled_mobs()))
        {
            if (Dm.truthy(can_mobswap_with(M)))
            {
                //switch our position with M
                if (Dm.truthy(loc) && !Dm.truthy(await loc.Adjacent(M.loc)))
                    return TRUE;
                now_pushing = TRUE;
                atom oldloc = loc;
                atom oldMloc = M.loc;


                object M_passmob = Dm.bitand(M.pass_flags, PASSMOB); // we give PASSMOB to both mobs to avoid bumping other mobs during swap.
                object src_passmob = Dm.bitand(pass_flags, PASSMOB);
                Dm.combine(ref M.pass_flags, PASSMOB);
                Dm.combine(ref pass_flags, PASSMOB);

                float move_failed = FALSE;
                if (!Dm.truthy(await M.Move(oldloc)) || !Dm.truthy(await Move(oldMloc)))
                {
                    await M.forceMove(oldMloc);
                    await forceMove(oldloc);
                    move_failed = TRUE;
                }
                if (!Dm.truthy(src_passmob))
                    Dm.mask(ref pass_flags, Dm.bitnot(PASSMOB));
                if (!Dm.truthy(M_passmob))
                    Dm.mask(ref M.pass_flags, Dm.bitnot(PASSMOB));

                now_pushing = FALSE;

                if (!Dm.truthy(move_failed))
                    return TRUE;
            }
        }

        //okay, so we didn't switch. but should we push?
        //not if he's not CANPUSH of course
        if (!Dm.truthy(Dm.bitand(M.status_flags, CANPUSH)))
            return TRUE;
        if (isliving(M))
        {
            mob_living L__1 = M as mob_living;
            if (HAS_TRAIT(L__1, TRAIT_PUSHIMMUNE))
                return TRUE;
        }
        //If they're a human, and they're not in help intent, block pushing
        if (ishuman(M))
        {
            mob_living_carbon_human human = M as mob_living_carbon_human;
            if (Dm.truthy(human.combat_mode))
                return TRUE;
        }
        //if they are a cyborg, and they're alive and in combat mode, block pushing
        if (iscyborg(M))
        {
            mob_living_silicon_robot borg = (mob_living_silicon_robot)M;
            if (Dm.truthy(borg.combat_mode) && borg.stat != DEAD)
                return TRUE;
        }
        //anti-riot equipment is also anti-push
        foreach (obj_item I in Dm.each<obj_item>(M.held_items))
        {
            if (!isclothing(M))
            {
                if (prob(I.block_chance * 2))
                    return null;
            }
        }
        return null;
    }

    // tg: code/modules/mob/living/living.dm:276
    public virtual object can_mobswap_with(mob other = null)
    {
        if (HAS_TRAIT(other, TRAIT_NOMOBSWAP) || HAS_TRAIT(this, TRAIT_NOMOBSWAP))
            return FALSE;

        object they_can_move = TRUE;
        object their_combat_mode = FALSE;

        if (isliving(other))
        {
            mob_living other_living = other as mob_living;
            their_combat_mode = other_living.combat_mode;
            they_can_move = Dm.bitand(other_living.mobility_flags, MOBILITY_MOVE);
        }

        float too_strong = Dm.num(Dm.num(other.move_resist) > Dm.num(move_force));

        // They cannot move, see if we can push through them
        if (!Dm.truthy(they_can_move))
            return Dm.not(too_strong);

        // We are pulling them and can move through
        if (other.pulledby == this && !Dm.truthy(too_strong))
            return TRUE;

        // If we're in combat mode and not restrained we don't try to pass through people
        if (Dm.truthy(combat_mode) && !HAS_TRAIT(this, TRAIT_RESTRAINED))
            return FALSE;

        // Nor can we pass through non-restrained people in combat mode (or if they're restrained but still too strong for us)
        if (Dm.truthy(their_combat_mode) && (!HAS_TRAIT(other, TRAIT_RESTRAINED) || Dm.truthy(too_strong)))
            return FALSE;

        if (other.client == null || client == null)
            return TRUE;

        // If both of us are trying to move in the same direction, let the fastest one through first
        if (Dm.eq(client.intended_direction, other.client.intended_direction))
            return Dm.num(Dm.lt(cached_multiplicative_slowdown, other.cached_multiplicative_slowdown));

        // Else, sure, let us pass
        return TRUE;
    }

    //Called when we bump onto an obj
    // tg: code/modules/mob/living/living.dm:330
    public virtual void ObjBump(obj O = null)
    {
        return;
    }

    //Called when we want to push an atom/movable
    // tg: code/modules/mob/living/living.dm:334
    public virtual async DmTask<object> PushAM(atom_movable AM = null, object force = null)
    {
        force ??= move_force;
        if (Dm.truthy(now_pushing))
            return TRUE;
        if (Dm.truthy(moving_diagonally))// no pushing during diagonal moves.
            return TRUE;
        if (!Dm.truthy(client) && (Dm.num(mob_size) < MOB_SIZE_SMALL))
            return null;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(AM._listen_lookup?[COMSIG_MOVABLE_BUMP_PUSHED]) ? NONE : AM._SendSignal(COMSIG_MOVABLE_BUMP_PUSHED, Dm.list(AM, this, force))), COMPONENT_NO_PUSH)))
            return null;
        now_pushing = TRUE;
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_PUSHING_MOVABLE]))
            _SendSignal(COMSIG_LIVING_PUSHING_MOVABLE, Dm.list(this, AM));
        float dir_to_target = get_dir(this, AM);

        // If there's no dir_to_target then the player is on the same turf as the atom they're trying to push.
        // This can happen when a player is stood on the same turf as a directional window. All attempts to push
        // the window will fail as get_dir will return 0 and the player will be unable to move the window when
        // it should be pushable.
        // In this scenario, we will use the facing direction of the /mob/living attempting to push the atom as
        // a fallback.
        if (!Dm.truthy(dir_to_target))
            dir_to_target = dir;

        float push_anchored = FALSE;
        if (Dm.le(Dm.num(AM.move_resist) * MOVE_FORCE_CRUSH_RATIO, force))
        {
            if (Dm.truthy(await move_crush(AM, move_force, dir_to_target)))
                push_anchored = TRUE;
        }
        if (Dm.le(Dm.num(AM.move_resist) * MOVE_FORCE_FORCEPUSH_RATIO, force)) //trigger move_crush and/or force_push regardless of if we can push it normally
        {
            if (Dm.truthy(await force_push(AM, move_force, dir_to_target, push_anchored)))
                push_anchored = TRUE;
        }
        if (ismob(AM))
        {
            mob mob_to_push = AM as mob;
            atom_movable mob_buckle = mob_to_push.buckled;
            // If we can't pull them because of what they're buckled to, make sure we can push the thing they're buckled to instead.
            // If neither are true, we're not pushing anymore.
            if (Dm.truthy(mob_buckle) && (Dm.truthy(mob_buckle.buckle_prevents_pull) || (Dm.lt(force, Dm.num(mob_buckle.move_resist) * MOVE_FORCE_PUSH_RATIO))))
            {
                now_pushing = FALSE;
                return null;
            }
        }
        if ((Dm.truthy(AM.anchored) && !Dm.truthy(push_anchored)) || (Dm.lt(force, Dm.num(AM.move_resist) * MOVE_FORCE_PUSH_RATIO)))
        {
            now_pushing = FALSE;
            return null;
        }
        // tg: :375-380  if(istype(AM, /obj/structure/window)) { var/obj/structure/window/W = AM; if(W.fulltile) for(var/obj/structure/window/win
        //               in get_step(W, dir_to_target)) { now_pushing = FALSE; return } }
        // GAP(M3): /obj/structure/window is not declared (windows come with the map, M3), so no AM is one and the test is
        // FALSE; the block is translated with the type.
        if (pulling == AM)
            await stop_pulling();
        object current_dir = null;
        if (isliving(AM))
            current_dir = AM.dir;
        if (Dm.truthy(await AM.Move(get_step(AM.loc, dir_to_target), dir_to_target, glide_size)))
        {
            // tg: code/modules/mob/living/living.dm:385
            AM.add_fingerprint(this);
            // DEVIATION(DV535): a capsule's press that pushed walks on into the freed tile itself (its next crossing is this Move), so it
            // keeps its speed and presses again a walked tile later, at the pushed thing's glide; tg's step here stopped it and slid it there.
            // Its step is taken again through the freed tile, so the press costs it no time
            if (!LocomotionSeam.WalksOnAfterPush(this))
                await Move(get_step(loc, dir_to_target), dir_to_target);
        }
        if (Dm.truthy(current_dir))
            await AM.setDir(current_dir);
        now_pushing = FALSE;
        return null;
    }

    // tg: code/modules/mob/living/living.dm:392  (pulling, 2026-10-15)
    public override async DmTask<object> start_pulling(atom_movable pulled_atom = null, object state = null, object force = null, float? supress_message = null)
    {
        atom_movable AM = pulled_atom;
        force ??= pull_force;
        supress_message ??= FALSE;
        // tg: :393  if(!src) return FALSE: a proc's src is never null here
        // tg: :395  ASSERT(ismovable(AM), "[src] attempted to pull [AM ? "[AM], a nonmovable atom" : "a null object"]")
        if (!ismovable(AM))
            throw Dm.CRASH(assertion_message("code/modules/mob/living/living.dm", 395, "ismovable(AM)", Dm.text("[] attempted to pull []", this, Dm.truthy(AM) ? Dm.text("[], a nonmovable atom", AM) : "a null object")), "code/modules/mob/living/living.dm", 395);
        if (!Dm.truthy(AM.can_be_pulled(this, force)))
            return FALSE;
        if (Dm.truthy(throwing) || !Dm.truthy(Dm.bitand(mobility_flags, MOBILITY_PULL)))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_LIVING_TRY_PULL]) ? (object)NONE : this._SendSignal(COMSIG_LIVING_TRY_PULL, Dm.list(this, AM, force))), COMSIG_LIVING_CANCEL_PULL)))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(AM._listen_lookup?[COMSIG_LIVING_TRYING_TO_PULL]) ? (object)NONE : AM._SendSignal(COMSIG_LIVING_TRYING_TO_PULL, Dm.list(AM, this, force))), COMSIG_LIVING_CANCEL_PULL)))
            return FALSE;

        // tg: code/modules/mob/living/living.dm:406
        AM.add_fingerprint(this);

        // If we're pulling something then drop what we're currently pulling and pull this instead.
        if (Dm.truthy(pulling))
        {
            // Are we trying to pull something we are already pulling? Then just stop here, no need to continue.
            if (AM == pulling)
                return FALSE;
            await stop_pulling();
        }

        changeNext_move(CLICK_CD_GRABBING);

        if (Dm.truthy(AM.pulledby))
        {
            if (!Dm.truthy(supress_message))
            {
                AM.visible_message(span_danger(I18n.T("[] pulls [] from []'s grip.", this, AM, AM.pulledby)),
                                span_danger(I18n.T("[] pulls you from []'s grip.", this, AM.pulledby)), null, null, this);
                to_chat(this, span_notice(I18n.T("You pull [] from []'s grip!", AM, AM.pulledby)));
            }
            log_combat(AM, AM.pulledby, "pulled from", this);
            await AM.pulledby.stop_pulling(); //an object can't be pulled by two mobs at once.
        }

        await set_pulling(AM);
        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_START_PULL]))
            this._SendSignal(COMSIG_LIVING_START_PULL, Dm.list(this, AM, state, force));

        if (!Dm.truthy(supress_message))
        {
            object sound_to_play = Dm.resource("sound/items/weapons/thudswoosh.ogg");
            if (ishuman(this))
            {
                mob_living_carbon_human H = (mob_living_carbon_human)this;
                if (Dm.truthy((H.dna.species as datum_species)?.grab_sound))
                    sound_to_play = (H.dna.species as datum_species).grab_sound;
                if (HAS_TRAIT(H, TRAIT_STRONG_GRABBER))
                    sound_to_play = null;
            }
            playsound(this.loc, sound_to_play, 50, TRUE, -1);
        }
        update_pull_hud_icon();

        if (ismob(AM))
        {
            mob M = AM as mob;

            log_combat(this, M, "grabbed", addition: "passive grab");
            if (!Dm.truthy(supress_message) && !(iscarbon(AM) && HAS_TRAIT(this, TRAIT_STRONG_GRABBER)))
            {
                if (ishuman(M))
                {
                    mob_living_carbon_human grabbed_human = (mob_living_carbon_human)M;
                    bool grabbed_by_hands = (zone_selected == "l_arm" || zone_selected == "r_arm") && grabbed_human.usable_hands > 0;
                    M.visible_message(span_warning(I18n.T("[] grabs [] []!", this, M, grabbed_by_hands ? I18n.T("by their hands") : I18n.T("passively"))),
                                    span_warning(I18n.T("[] grabs you []!", this, grabbed_by_hands ? I18n.T("by your hands") : I18n.T("passively"))), null, null, this);
                    to_chat(this, span_notice(I18n.T("You grab [] []!", M, grabbed_by_hands ? I18n.T("by their hands") : I18n.T("passively"))));
                    // tg: code/modules/mob/living/living.dm:449
                    await grabbed_human.share_blood_on_touch(this as mob_living_carbon_human, grabbed_by_hands ? (object)ITEM_SLOT_GLOVES : Dm.bitor(ITEM_SLOT_ICLOTHING, ITEM_SLOT_OCLOTHING));
                }
                else
                {
                    M.visible_message(span_warning(I18n.T("[] grabs [] passively!", this, M)),
                                    span_warning(I18n.T("[] grabs you passively!", this)), null, null, this);
                    to_chat(this, span_notice(I18n.T("You grab [] passively!", M)));
                }
            }

            if (isliving(M))
            {
                mob_living L = M as mob_living;

                if (Dm.truthy(M._listen_lookup?[COMSIG_LIVING_GET_PULLED]))
                    M._SendSignal(COMSIG_LIVING_GET_PULLED, Dm.list(M, this));
                //Share diseases that are spread by touch
                foreach (object thing in Dm.each(diseases))
                {
                    datum_disease D = (datum_disease)thing;
                    if (Dm.truthy(Dm.bitand(D.spread_flags, DISEASE_SPREAD_CONTACT_SKIN)))
                        await L.ContactContractDisease(D); // gaps VI1
                }

                foreach (object thing in Dm.each(L.diseases))
                {
                    datum_disease D = (datum_disease)thing;
                    if (Dm.truthy(Dm.bitand(D.spread_flags, DISEASE_SPREAD_CONTACT_SKIN)))
                        await ContactContractDisease(D); // gaps VI1
                }

                if (iscarbon(L))
                {
                    mob_living_carbon C = L as mob_living_carbon;
                    if (HAS_TRAIT(this, TRAIT_STRONG_GRABBER))
                        await C.grabbedby(this);
                }

                await update_pull_movespeed();
            }

            await set_pull_offsets(M as mob_living, Dm.num_or_null(state));
            return TRUE;
        }
        return null;
    }

    /**
     * Removes any offsets from the passed mob that are related to being grabbed
     *
     * * M - the mob to remove the offsets from
     * * override - if TRUE, the offsets will be removed regardless of the mob's buckled state
     * otherwise we won't remove the offsets if the mob is buckled
     */
    // tg: code/modules/mob/living/living.dm:525  (pulling, 2026-10-15)
    public virtual void reset_pull_offsets(mob_living M = null, object @override = null)
    {
        if (!Dm.truthy(@override) && Dm.truthy(M.buckled))
            return;
        M.remove_offsets(GRABBING_TRAIT);
    }

    //mob verbs are a lot faster than object verbs
    //for more info on why this is not atom/pull, see examinate() in mob.dm
    // tg: code/modules/mob/living/living.dm:532  GAME_VERB_CONTEXT_RANGE(/mob/living, pulled, "Pull", "", null, /atom/movable, oview(1)):
    // the verb's body (the body proc /mob/living/proc/__gvb_pulled(list/structured_args), __DEFINES/verb.dm:58); the verb
    // itself (pulled) is below.
    public virtual async DmTask __gvb_pulled(DmList structured_args = null)
    {
        // VERB_ARG_TYPED_RANGE(thing_pulled, VERB_ARG_TYPE_MOB | VERB_ARG_TYPE_OBJ, VERB_ARG_SOURCE_VIEW, /atom/movable, 1)
        // (__DEFINES/verb.dm): the static registration of the argument for the verb panel is left out (rules 10.6, 7.10)
        object thing_pulled = structured_args?["thing_pulled"];
        if (thing_pulled is atom_movable movable && Dm.truthy(await Adjacent(movable)))
            await start_pulling(movable);
    }

    // tg: code/modules/mob/living/living.dm:532  the verb /mob/living/verb/pulled(atom/movable/__context_target in oview(1))
    // (__DEFINES/verb.dm:48-57), which tg's own code calls as a proc (base_click_ctrl). As examinate (mob.cs, rules 7.10's M4a
    // exception, D128): the set statements (the verb panel and the right-click verb menu) stay M9, and so does SSverbs; the
    // verb's INVOKE_ASYNC(SSverbs, invoke_verb, src, /mob/living/verb/pulled, args + list(VERB_ARG_CONTEXT_TARGET_KEY =
    // __context_target), usr) is a detached call of the body with the context target as thing_pulled. invoke_verb's own
    // checks (controllers/subsystem/verbs.dm:43-55): a caller without a client does nothing; the argument's range check
    // (____collect_verb_args: one tile in view) is the body's Adjacent here.
    public virtual void pulled(atom_movable __context_target = null, params object[] __extra)
    {
        if (!Dm.truthy(__context_target))
        {
            // else INVOKE_ASYNC(SSverbs, invoke_verb, src, /mob/living/verb/pulled, args, usr): the verb typed in the command
            // bar with its argument chosen by the verb manager (M9)
            Dm.NotTranslated("code/modules/mob/living/living.dm:532", "pulled: the verb without a context target (the verb manager, M10+)");
            return;
        }
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_pulled(Dm.list(Dm.kv("thing_pulled", __context_target))));
    }

    // tg: code/modules/mob/living/living.dm:537  (pulling, 2026-10-15)
    public override async DmTask stop_pulling()
    {
        if (ismob(pulling))
            reset_pull_offsets(pulling as mob_living);
        await base.stop_pulling();
        await update_pull_movespeed();
        update_pull_hud_icon();
    }

    // tg: code/modules/mob/living/living.dm:557 (the body of the succumb verb, :558-572)
    public virtual async DmTask __gvb_succumb(DmList structured_args = null)
    {
        // tg: :558  VERB_ARG(whispered, VERB_ARG_TYPE_NUM, VERB_ARG_SOURCE_INPUT) (__DEFINES/verb.dm:180): its
        // var/static/____reg_whispered = ____register_verb_arg(...) registers the argument with the verb registry, the
        // player command layer (rules 7.10, M9-M10), and is not translated.
        // rules 10.6 (settled at the M2 integration): a proc-level var/static whose initializer only registers metadata of the untranslated verb registry
        // has no rule; it is left out with this comment.
        object whispered = structured_args["whispered"];
        if (!CAN_SUCCUMB(this))
        {
            if (HAS_TRAIT(this, TRAIT_SUCCUMB_OVERRIDE))
            {
                if (Dm.truthy(whispered))
                {
                    to_chat(this, span_notice(I18n.T("Your immortal body is keeping you alive! Unless you just press the UI button.")), type: MESSAGE_TYPE_INFO);
                    return;
                }
            }
            else
            {
                to_chat(this, span_warning(I18n.T("You are unable to succumb to death! This life continues.")), type: MESSAGE_TYPE_INFO);
                return;
            }
        }
        log_message(Dm.text("Has [] with [] points of health!", Dm.truthy(whispered) ? "whispered his final words" : "succumbed to death", round(health, 0.1f)), LOG_ATTACK);
        await adjust_oxy_loss(health - dead_threshold);
        await updatehealth();
        if (!Dm.truthy(whispered))
            to_chat(this, span_notice(I18n.T("You have given up life and succumbed to death.")));
        investigate_log("has succumbed to death.", INVESTIGATE_DEATHS);
        await death();
    }

    // tg: code/modules/mob/living/living.dm:557
    // GAME_VERB_HIDDEN(/mob/living, succumb, "succumb") expands (__DEFINES/verb.dm:12-31) to the type
    // /datum/verb_metadata/mob/living/succumb (the verb registry), the verb below, which hands its arguments to
    // SSverbs.invoke_verb, and the proc __gvb_succumb with the body.
    // M8z (D244): the client's radial and the succumb alert call it. As pulled (above): INVOKE_ASYNC(SSverbs, invoke_verb, src,
    // /mob/living/verb/succumb, args, usr) is a detached call of the body; invoke_verb's own checks (controllers/subsystem/verbs.dm:43-55):
    // a caller without a client does nothing, and the one argument (VERB_ARG_SOURCE_INPUT) is the positional whispered.
    public virtual void succumb(object whispered = null, params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_succumb(Dm.list(Dm.kv("whispered", whispered))));
    }

    // Remember, anything that influences this needs to call update_incapacitated somehow when it changes
    // Most often best done in [code/modules/mob/living/init_signals.dm]
    // tg: code/modules/mob/living/living.dm:577
    [DmParams("flags")]
    public override object build_incapacitated(params object[] __extra)
    {
        object flags = Dm.arg(__extra, 0);
        // Holds a set of flags that describe how we are currently incapacitated
        object incap_status = NONE;
        if (HAS_TRAIT(this, TRAIT_INCAPACITATED))
            Dm.combine(ref incap_status, TRADITIONAL_INCAPACITATED);
        if (HAS_TRAIT(this, TRAIT_RESTRAINED))
            Dm.combine(ref incap_status, INCAPABLE_RESTRAINTS);
        if (Dm.truthy(pulledby) && pulledby.grab_state >= GRAB_AGGRESSIVE)
            Dm.combine(ref incap_status, INCAPABLE_GRAB);
        if (HAS_TRAIT(this, TRAIT_STASIS))
            Dm.combine(ref incap_status, INCAPABLE_STASIS);

        return incap_status;
    }

    // tg: code/modules/mob/living/living.dm:591
    public override object canUseStorage()
    {
        if (usable_hands <= 0)
            return FALSE;
        return TRUE;
    }

    //This proc is used for mobs which are affected by pressure to calculate the amount of pressure that actually
    //affects them once clothing is factored in. ~Errorage
    // tg: code/modules/mob/living/living.dm:599
    public virtual object calculate_affecting_pressure(object pressure = null)
    {
        return pressure;
    }

    // tg: code/modules/mob/living/living.dm:602
    public virtual object getMaxHealth()
    {
        return maxHealth;
    }

    // tg: code/modules/mob/living/living.dm:605
    public virtual void setMaxHealth(object newMaxHealth = null)
    {
        maxHealth = Dm.num(newMaxHealth);
    }

    /// Returns the health of the mob while ignoring damage of non-organic (prosthetic) limbs
    /// Used by cryo cells to not permanently imprison those with damage from prosthetics,
    /// as they cannot be healed through chemicals.
    // tg: code/modules/mob/living/living.dm:611
    public virtual object get_organic_health()
    {
        return health;
    }

    // tg: code/modules/mob/living/living.dm:616  GAME_VERB_PROC(/mob/living, mob_sleep, "Sleep", null) (__DEFINES/verb.dm:96-114): the body
    // proc /mob/living/proc/__gvb_mob_sleep(list/structured_args); the verb proc (mob_sleep) is below. M8z (D244).
    public virtual async DmTask __gvb_mob_sleep(DmList structured_args = null)
    {
        if (Dm.truthy(IsSleeping()))
        {
            to_chat(this, span_warning(I18n.T("You are already sleeping!")));
            return;
        }
        else
        {
            if (Dm.eq(await tgui_alert(usr, I18n.T("You sure you want to sleep for a while?"), I18n.T("Sleep"), Dm.list(I18n.T("Yes"), I18n.T("No"))), I18n.T("Yes")))
                await SetSleeping(400); //Short nap
        }
    }

    // tg: code/modules/mob/living/living.dm:616  the verb proc's INVOKE_ASYNC(SSverbs, invoke_verb, src, /mob/living/proc/mob_sleep, args,
    // usr), as succumb (above): a detached call of the body; a caller without a client does nothing; the verb has no arguments.
    public virtual void mob_sleep(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_mob_sleep(Dm.list()));
    }

    /**
     * Gets ID card from a mob.
     * Argument:
     * * hand_firsts - boolean that checks the hands of the mob first if TRUE.
     */
    // tg: code/modules/mob/living/living.dm:634  (M4a P5)
    public virtual object get_idcard(object hand_first = null)
    {
        object dot = null;
        if (!Dm.truthy(length(held_items))) //Early return for mobs without hands.
            return dot;
        //Check hands
        obj_item held_item = get_active_held_item() as obj_item;
        if (Dm.truthy(held_item)) //Check active hand
            dot = held_item.GetID();
        if (!Dm.truthy(dot)) //If there is no id, check the other hand
        {
            held_item = get_inactive_held_item() as obj_item;
            if (Dm.truthy(held_item))
                dot = held_item.GetID();
        }
        return dot;
    }

    /**
     * Returns the access list for this mob
     */
    // tg: code/modules/mob/living/living.dm:649  (M4a P5)
    public override object get_access()
    {
        DmList access_list = Dm.list();
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOB_RETRIEVE_ACCESS]))
            this._SendSignal(COMSIG_MOB_RETRIEVE_ACCESS, Dm.list(this, access_list));
        return access_list;
    }

    // tg: code/modules/mob/living/living.dm:654  (M4a P5)
    public virtual object get_id_in_hand()
    {
        obj_item held_item = get_active_held_item() as obj_item;
        if (!Dm.truthy(held_item))
            return null;
        return held_item.GetID();
    }

    // tg: code/modules/mob/living/living.dm:661
    public virtual object get_bank_account()
    {
        datum_bank_account account = null;
        var I = get_idcard() as obj_item_card_id;

        if (I?.registered_account != null)
        {
            account = I.registered_account;
            return account;
        }
        return null;
    }

    // tg: code/modules/mob/living/living.dm:670
    public virtual async DmTask toggle_resting()
    {
        await set_resting(Dm.not(resting), FALSE);
    }

    ///Proc to hook behavior to the change of value in the resting variable.
    // tg: code/modules/mob/living/living.dm:675
    public virtual async DmTask<object> set_resting(object new_resting = null, float? silent = null, float? instant = null)
    {
        silent ??= TRUE;
        instant ??= FALSE;
        object dot = null;
        if (!Dm.truthy(Dm.bitand(mobility_flags, MOBILITY_REST)))
            return null;
        if (Dm.eq(new_resting, resting))
            return null;

        dot = resting;
        resting = Dm.num(new_resting);
        if (Dm.truthy(new_resting))
        {
            if (body_position == LYING_DOWN)
            {
                if (!Dm.truthy(silent))
                    to_chat(this, span_notice(I18n.T("You will now try to stay lying down on the floor.")));
            }
            else if (HAS_TRAIT(this, TRAIT_FORCED_STANDING) || (Dm.truthy(buckled) && !Dm.eq(buckled.buckle_lying, NO_BUCKLE_LYING)))
            {
                if (!Dm.truthy(silent))
                    to_chat(this, span_notice(I18n.T("You will now lay down as soon as you are able to.")));
            }
            else
            {
                if (!Dm.truthy(silent))
                    to_chat(this, span_notice(I18n.T("You lay down.")));
                await set_lying_down();
            }
        }
        else
        {
            if (body_position == STANDING_UP)
            {
                if (!Dm.truthy(silent))
                    to_chat(this, span_notice(I18n.T("You will now try to remain standing up.")));
            }
            else if (HAS_TRAIT(this, TRAIT_FLOORED) || (Dm.truthy(buckled) && !Dm.eq(buckled.buckle_lying, NO_BUCKLE_LYING)))
            {
                if (!Dm.truthy(silent))
                    to_chat(this, span_notice(I18n.T("You will now stand up as soon as you are able to.")));
            }
            else
            {
                if (!Dm.truthy(silent))
                    to_chat(this, span_notice(I18n.T("You stand up.")));
                get_up(instant);
            }
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_RESTING]))
            _SendSignal(COMSIG_LIVING_RESTING, Dm.list(this, new_resting, silent, instant));
        update_resting();
        return dot;
    }

    /// Proc to append and redefine behavior to the change of the [/mob/living/var/resting] variable.
    // tg: code/modules/mob/living/living.dm:711
    public virtual void update_resting()
    {
        update_rest_hud_icon();
    }

    // tg: code/modules/mob/living/living.dm:715  set waitfor = FALSE (waiting-model.md 5.4: the wrapper and its __body)
    public virtual void get_up(float? instant = null)
    {
        Waiting.WaitforFalse(get_up__body(instant));
    }

    private async DmTask get_up__body(float? instant)
    {
        instant ??= FALSE;

        float get_up_time = 1 * SECONDS;

        object potential_spine = get_organ_slot(ORGAN_SLOT_SPINE);
        // tg: :721-722  if(istype(potential_spine)) get_up_time *= potential_spine.athletics_boost_multiplier
        // GAP(lead): /obj/item/organ/cyberimp/chest/spine (surgery/organs/internal/cyberimp, cybernetic implants, M8) is not
        // declared, so no organ is one and the test is FALSE; the line is translated with the type.

        if (!Dm.truthy(instant) && !Dm.truthy(await do_after(this, get_up_time, this, timed_action_flags: Dm.bitor(Dm.bitor(IGNORE_USER_LOC_CHANGE, IGNORE_TARGET_LOC_CHANGE), IGNORE_HELD_ITEM), extra_checks: Dm.@new<datum_callback>(this, nameof(mob_living.rest_checks_callback)), interaction_key: DOAFTER_SOURCE_GETTING_UP, cog_icon: null)))
            return;
        if (Dm.truthy(resting) || body_position == STANDING_UP || HAS_TRAIT(this, TRAIT_FLOORED))
            return;
        await set_body_position(STANDING_UP);
        await set_lying_angle(0);
    }

    // tg: code/modules/mob/living/living.dm:732
    public virtual object rest_checks_callback()
    {
        if (Dm.truthy(resting) || body_position == STANDING_UP || HAS_TRAIT(this, TRAIT_FLOORED))
            return FALSE;
        return TRUE;
    }

    /// Change the [body_position] to [LYING_DOWN] and update associated behavior.
    // tg: code/modules/mob/living/living.dm:739
    public virtual async DmTask set_lying_down(object new_lying_angle = null)
    {
        await set_body_position(LYING_DOWN);
    }

    /// Proc to append behavior related to lying down.
    // tg: code/modules/mob/living/living.dm:743
    public virtual async DmTask on_lying_down(object new_lying_angle = null)
    {
        if (layer == Dm.initial(this, (mob_living p) => p.layer)) //to avoid things like hiding larvas.
            layer = LYING_MOB_LAYER; //so mob lying always appear behind standing mobs
        add_traits(Dm.list(TRAIT_UI_BLOCKED, TRAIT_PULL_BLOCKED, TRAIT_UNDENSE), LYING_DOWN_TRAIT);
        if (HAS_TRAIT(this, TRAIT_FLOORED) && !Dm.truthy(Dm.bitand(dir, Dm.bitor(NORTH, SOUTH))))
            await setDir(pick(NORTH, SOUTH)); // We are and look helpless.
        if (Dm.truthy(rotate_on_lying))
            add_offsets(LYING_DOWN_TRAIT, y_add: PIXEL_Y_OFFSET_LYING);
    }

    /// Proc to append behavior related to lying down.
    // tg: code/modules/mob/living/living.dm:753
    public virtual async DmTask on_standing_up(params object[] __extra)
    {
        if (layer == LYING_MOB_LAYER)
            layer = Dm.initial(this, (mob_living p) => p.layer);
        remove_traits(Dm.list(TRAIT_UI_BLOCKED, TRAIT_PULL_BLOCKED, TRAIT_UNDENSE), LYING_DOWN_TRAIT);
        remove_offsets(LYING_DOWN_TRAIT);
    }

    // tg: code/modules/mob/living/living.dm:759
    public virtual void update_density()
    {
        if (HAS_TRAIT(this, TRAIT_UNDENSE))
            set_density(FALSE);
        else
            set_density(TRUE);
    }

    // tg: code/modules/mob/living/living.dm:765
    public override object update_rest_hud_icon()
    {
        object dot = null;
        dot = base.update_rest_hud_icon();
        if (!Dm.truthy(dot) || !Dm.truthy(hud_used))
            return FALSE;

        atom_movable_screen_sleep sleep_icon = hud_used.screen_objects[HUD_MOB_SLEEP] as atom_movable_screen_sleep;
        if (sleep_icon == null || HAS_TRAIT(this, TRAIT_SLEEPIMMUNE))
            return TRUE;

        if (Dm.truthy(resting) || HAS_TRAIT(this, TRAIT_FLOORED))
            sleep_icon.RemoveInvisibility(INVISIBILITY_SOURCE_SLEEP_HUD_BUTTON);
        else
            sleep_icon.SetInvisibility(INVISIBILITY_ABSTRACT, INVISIBILITY_SOURCE_SLEEP_HUD_BUTTON);
        return TRUE;
    }

    // tg: code/modules/mob/living/living.dm:799
    public virtual object can_inject(mob user = null, object target_zone = null, object injection_flags = null)
    {
        return TRUE;
    }

    // tg: code/modules/mob/living/living.dm:811
    public virtual object try_inject(mob user = null, object target_zone = null, object injection_flags = null)
    {
        return can_inject(user, target_zone, injection_flags);
    }

    // tg: code/modules/mob/living/living.dm:814 (M8 slice C)
    public override object is_injectable(params object[] __extra)
    {
        mob user = Dm.arg(__extra, 0) as mob;
        object allowmobs = Dm.arg(__extra, 1) ?? TRUE;
        return (Dm.truthy(allowmobs) && reagents != null && Dm.truthy(can_inject(user))) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/living.dm:817 (M8 slice C)
    public override object is_drawable(params object[] __extra)
    {
        mob user = Dm.arg(__extra, 0) as mob;
        object allowmobs = Dm.arg(__extra, 1) ?? TRUE;
        return (Dm.truthy(allowmobs) && reagents != null && Dm.truthy(can_inject(user))) ? TRUE : FALSE;
    }

    ///Sets the current mob's health value. Do not call directly if you don't know what you are doing, use the damage procs, instead.
    // tg: code/modules/mob/living/living.dm:822
    public virtual object set_health(object new_value = null)
    {
        object dot = null;
        dot = health;
        health = Dm.num(new_value);
        return dot;
    }

    // tg: code/modules/mob/living/living.dm:827
    public virtual async DmTask updatehealth()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;
        set_health(maxHealth - Dm.num(get_oxy_loss()) - Dm.num(get_tox_loss()) - Dm.num(get_fire_loss()) - Dm.num(get_brute_loss()));
        update_stat();
        // tg: :832-833  med_hud_set_health()  med_hud_set_status()
        // GAP(lead): /mob/living/proc/med_hud_set_health and med_hud_set_status (game/data_huds.dm:174, :179) are not
        // declared; the medical data HUD (HUD glasses, M8).
        Dm.Deferred("code/modules/mob/living/living.dm:832", "updatehealth: med_hud_set_health() and med_hud_set_status(), the medical data HUD (M8)");
        update_health_hud();
        await update_stamina();
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_HEALTH_UPDATE]))
            _SendSignal(COMSIG_LIVING_HEALTH_UPDATE, Dm.list(this));
    }

    // tg: code/modules/mob/living/living.dm:838
    public override object update_health_hud(object healthpercent = null)
    {
        float severity = 0;
        if (!Dm.truthy(healthpercent))
            healthpercent = (health / maxHealth) * 100;
        atom_movable_screen_healthdoll_living livingdoll = hud_used?.screen_objects[HUD_MOB_HEALTHDOLL] as atom_movable_screen_healthdoll_living;
        if (livingdoll is atom_movable_screen_healthdoll_living) //to really put you in the boots of a simplemob
        {
            // silicons SI6 (D326): the pAI's HUD has the living health doll
            float percent = Dm.num(healthpercent);
            if (percent >= 100)
                severity = 0;
            else if (percent >= 80)
                severity = 1;
            else if (percent >= 60)
                severity = 2;
            else if (percent >= 40)
                severity = 3;
            else if (percent >= 20)
                severity = 4;
            else if (percent >= 1)
                severity = 5;
            else
                severity = 6;
            livingdoll.icon_state = Dm.text("living[]", severity);
            if (!Dm.truthy(livingdoll.filtered))
            {
                livingdoll.filtered = TRUE;
                icon mob_mask = Dm.icon(icon, icon_state);
                if (Dm.num(get_cached_height()) > ICON_SIZE_Y || Dm.num(get_cached_width()) > ICON_SIZE_X)
                {
                    object health_doll_icon_state = Dm.truthy(health_doll_icon) ? health_doll_icon : "megasprite";
                    mob_mask = Dm.icon(Dm.resource("icons/hud/screen_gen.dmi"), health_doll_icon_state); //swap to something generic if they have no special doll
                }
                livingdoll.add_filter("mob_shape_mask", 1, alpha_mask_filter(icon: mob_mask) as DmList);
                livingdoll.add_filter("inset_drop_shadow", 2, drop_shadow_filter(size: -1) as DmList);
            }
            livingdoll.health_overlay.maptext = MAPTEXT(Dm.text("<div align='center' valign='middle' style='position:relative'>[]%</div>", round(percent, 1)));
            if (Dm.truthy(livingdoll.hovering))
                livingdoll.update_appearance(UPDATE_ICON);
        }

        if (severity > 0)
            overlay_fullscreen("brute", typeof(atom_movable_screen_fullscreen_brute), severity);
        else
            clear_fullscreen("brute");
        return null;
    }

    /**
     * Proc used to resuscitate a mob, bringing them back to life.
     *
     * Note that, even if a mob cannot be revived, the healing from this proc will still be applied.
     *
     * Arguments
     * * full_heal_flags - Optional. If supplied, [/mob/living/fully_heal] will be called with these flags before revival.
     * * excess_healing - Optional. If supplied, this number will be used to apply a bit of healing to the mob. Currently, 1 "excess healing" translates to -1 oxyloss, -1 toxloss, +2 blood, -5 to all organ damage.
     * * force_grab_ghost - We grab the ghost of the mob on revive. If TRUE, we force grab the ghost (includes suiciders). If FALSE, we do not. See [/mob/grab_ghost].
     *
     */
    // tg: code/modules/mob/living/living.dm:888
    public virtual async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        full_heal_flags ??= NONE;
        excess_healing ??= 0;
        force_grab_ghost ??= FALSE;
        object dot = null;
        if (QDELETED(this))
        {
            // Bro just like, don't ok
            return FALSE;
        }
        if (Dm.truthy(excess_healing))
        {
            await adjust_oxy_loss(-Dm.num(excess_healing), updating_health: FALSE);
            await adjust_tox_loss(-Dm.num(excess_healing), updating_health: FALSE, forced: TRUE); //slime friendly
            await updatehealth();
        }

        grab_ghost(force_grab_ghost);
        if (Dm.truthy(full_heal_flags))
            await fully_heal(full_heal_flags);

        if (stat == DEAD && Dm.truthy(can_be_revived())) //in some cases you can't revive (e.g. no brain)
        {
            REMOVE_TRAIT(this, TRAIT_DISSECTED, AUTOPSY_TRAIT);
            // tg: :903  set_suicide(FALSE)
            // GAP(lead): /mob/living/proc/set_suicide (modules/client/verbs/suicide.dm:34) is not declared; suicide is M10
            // (it only clears TRAIT_SUICIDED from the mob and its brain, which nothing in M2 sets).
            Dm.Deferred("code/modules/mob/living/living.dm:903", "revive: set_suicide(FALSE), the suicide flag (suicide, M10+)");
            await set_stat(HARD_CRIT); //the mob starts unconscious,
            await updatehealth(); //then we check if the mob should wake up.
            if (Dm.truthy(Dm.bitand(full_heal_flags, HEAL_ADMIN)))
                get_up(TRUE);
            await GhostNightVision.Remove(this); // DV174
            update_sight();
            clear_alert(ALERT_NOT_ENOUGH_OXYGEN);
            reload_fullscreen();
            dot = TRUE;
            if (Dm.truthy(excess_healing))
            {
                // tg: :912  INVOKE_ASYNC(src, PROC_REF(emote), "gasp")
                Waiting.Detach(emote("gasp")); // INVOKE_ASYNC
                log_combat(this, this, "revived");
            }
        }

        else if (Dm.truthy(Dm.bitand(full_heal_flags, HEAL_ADMIN)))
        {
            await updatehealth();
            get_up(TRUE);
        }

        // The signal is called after everything else so components can properly check the updated values
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_REVIVE]))
            _SendSignal(COMSIG_LIVING_REVIVE, Dm.list(this, full_heal_flags));
        return dot;
    }

    /**
     * Heals up the mob up to [heal_to] of the main damage types.
     * EX: If heal_to is 50, and they have 150 brute damage, they will heal 100 brute (up to 50 brute damage)
     *
     * If the target is dead, also revives them and heals their organs / restores blood.
     * If we have a [revive_message], play a visible message if the revive was successful.
     *
     * Arguments
     * * heal_to - the health threshold to heal the mob up to for each of the main damage types.
     * * revive_message - if provided, a visible message to show on a successful revive.
     *
     * Returns TRUE if the mob is alive afterwards, or FALSE if they're still dead (revive failed).
     */
    // tg: code/modules/mob/living/living.dm:936
    public virtual async DmTask<object> heal_and_revive(float? heal_to = null, object revive_message = null)
    {
        heal_to ??= 50;

        // Heal their brute and burn up to the threshold we're looking for
        float brute_to_heal = Dm.num(heal_to) - Dm.num(get_brute_loss());
        float burn_to_heal = Dm.num(heal_to) - Dm.num(get_fire_loss());
        float oxy_to_heal = Dm.num(heal_to) - Dm.num(get_oxy_loss());
        float tox_to_heal = Dm.num(heal_to) - Dm.num(get_tox_loss());
        if (brute_to_heal < 0)
            await adjust_brute_loss(brute_to_heal, updating_health: FALSE);
        if (burn_to_heal < 0)
            await adjust_fire_loss(burn_to_heal, updating_health: FALSE);
        if (oxy_to_heal < 0)
            await adjust_oxy_loss(oxy_to_heal, updating_health: FALSE);
        if (tox_to_heal < 0)
            await adjust_tox_loss(tox_to_heal, updating_health: FALSE, forced: TRUE);

        // Run updatehealth once to set health for the revival check
        await updatehealth();

        // We've given them a decent heal.
        // If they happen to be dead too, try to revive them - if possible.
        if (stat == DEAD && Dm.truthy(can_be_revived()))
        {
            // If the revive is successful, show our revival message (if present).
            if (Dm.truthy(await revive(excess_healing: 10)) && Dm.truthy(revive_message))
                visible_message(revive_message);
        }

        // Finally update health again after we're all done
        await updatehealth();

        return Dm.num(stat != DEAD);
    }

    /**
     * A grand proc used whenever this mob is, quote, "fully healed".
     * Fully healed could mean a number of things, such as "healing all the main damage types", "healing all the organs", etc
     * So, you can pass flags to specify
     *
     * See [mobs.dm] for more information on the flags
     *
     * If you ever think "hey I'm adding something and want it to be reverted on full heal",
     * consider handling it via signal instead of implementing it in this proc
     */
    // tg: code/modules/mob/living/living.dm:977
    [ShouldCallParent]
    public virtual async DmTask fully_heal(float? heal_flags = null)
    {
        heal_flags ??= HEAL_ALL;

        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_TOX)))
            await set_tox_loss(0, updating_health: FALSE, forced: TRUE);
        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_OXY)))
            await set_oxy_loss(0, updating_health: FALSE, forced: TRUE);
        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_BRUTE)))
            await set_brute_loss(0, updating_health: FALSE, forced: TRUE);
        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_BURN)))
            await set_fire_loss(0, updating_health: FALSE, forced: TRUE);
        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_STAM)))
            await set_stamina_loss(0, updating_stamina: FALSE, forced: TRUE);

        // Only aheals really do this right now, so this flag should be fine for the time being
        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_ADMIN)))
        {
            await set_nutrition(NUTRITION_LEVEL_FED + 50);
            overeatduration = 0;
            satiety = 0;
        }

        // These should be tracked by status effects
        losebreath = 0;
        set_disgust(0);
        await cure_husk();

        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_TEMP)))
            bodytemperature = Dm.num(get_body_temp_normal(apply_change: FALSE));
        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_BLOOD)))
            restore_blood();
        if (Dm.truthy(reagents) && Dm.truthy(Dm.bitand(heal_flags, HEAL_ALL_REAGENTS)))
            await ((datum_reagents)reagents).clear_reagents();

        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_ADMIN)))
            REMOVE_TRAIT(this, TRAIT_SUICIDED, REF(this));

        await updatehealth();
        stop_sound_channel(CHANNEL_HEARTBEAT); // tg: code/modules/mob/living/living.dm:1013
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_POST_FULLY_HEAL]))
            _SendSignal(COMSIG_LIVING_POST_FULLY_HEAL, Dm.list(this, heal_flags));
    }

    // tg: code/modules/mob/living/living.dm:1020
    public virtual async DmTask do_strange_reagent_revival(object healing_amount = null)
    {
        // Gaps RG2 (D339): strange reagent's revival.
        float healing = Dm.num(healing_amount);
        float brute_loss = Dm.num(get_brute_loss());
        if (Dm.truthy(brute_loss))
        {
            float brute_healing = min(healing * 0.5f, brute_loss); // 50% of the healing goes to brute
            await set_brute_loss(round(brute_loss - brute_healing, DAMAGE_PRECISION), updating_health: FALSE, forced: TRUE);
            healing = max(0, healing - brute_healing);
        }

        float fire_loss = Dm.num(get_fire_loss());
        if (Dm.truthy(fire_loss) && Dm.truthy(healing))
        {
            float fire_healing = min(healing, fire_loss); // rest of the healing goes to fire
            await set_fire_loss(round(fire_loss - fire_healing, DAMAGE_PRECISION), updating_health: TRUE, forced: TRUE);
            healing = max(0, healing - fire_healing);
        }

        await revive(NONE, excess_healing: max(healing, 0), force_grab_ghost: FALSE); // and any excess healing is passed along
    }

    /// Checks if we are actually able to ressuscitate this mob.
    /// (We don't want to revive then to have them instantly die again)
    // tg: code/modules/mob/living/living.dm:1037
    public virtual object can_be_revived()
    {
        if (HAS_TRAIT(this, TRAIT_NODEATH))
            return TRUE;
        if (health > dead_threshold)
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/living/living.dm:1044
    public virtual async DmTask update_damage_overlays()
    {
        return;
    }

    // tg: code/modules/mob/living/living.dm:1047
    [DmParams("newloc", "direct", "glide_size_override")]
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = null;
        if (lying_angle != 0)
            await lying_angle_on_movement(direct);
        if (Dm.truthy(buckled) && buckled.loc != newloc) //not updating position
        {
            if (!Dm.truthy(buckled.anchored))
            {
                buckled.moving_from_pull = moving_from_pull;
                dot = await buckled.Move(newloc, direct, glide_size);
                if (Dm.truthy(buckled)) //buckled can become unbuckled, apparently
                    buckled.moving_from_pull = null;
            }
            return dot;
        }

        object old_direction = dir;
        turf old_loc = loc as turf;

        if (Dm.truthy(pulling))
            await update_pull_movespeed();

        dot = await base.Move(newloc, direct, glide_size_override, update_dir);

        if (moving_diagonally != FIRST_DIAG_STEP && isliving(pulledby))
        {
            mob_living puller = pulledby as mob_living;
            await puller.set_pull_offsets(this, puller.grab_state);
        }

        if (Dm.truthy(active_storage))
        {
            // tg: :1069-1072  the reach check of the open storage UI (active_storage.parent, IsReachableBy, hide_contents)
            // GAP(lead): /datum/storage (datums/storage/storage.dm) has no procs declared; storage UIs are M4a.
            Dm.NotTranslated("code/modules/mob/living/living.dm:1069", "Move: hiding an out-of-reach open storage (storage, M4a)");
        }

        if (!Dm.truthy(buckled) && !Dm.truthy(moving_diagonally) && loc != old_loc)
        {
            object blood_flow = get_bleed_rate();
            bool health_check = body_position == LYING_DOWN && prob(Dm.num(get_brute_loss()) * 200 / maxHealth);
            bool bleeding_check = Dm.gt(blood_flow, 3) && prob(Dm.num(blood_flow) * 16);
            if (health_check || bleeding_check)
                await make_blood_trail(newloc as turf, old_loc, old_direction, direct);
        }
        return dot;
    }

    ///Called by mob Move() when the lying_angle is different than zero, to better visually simulate crawling.
    // tg: code/modules/mob/living/living.dm:1084
    public virtual async DmTask lying_angle_on_movement(object direct = null)
    {
        if (Dm.truthy(buckled) && !Dm.eq(buckled.buckle_lying, NO_BUCKLE_LYING))
        {
            await set_lying_angle(buckled.buckle_lying);
            return;
        }

        if (Dm.truthy(Dm.bitand(direct, EAST)))
            await set_lying_angle(LYING_ANGLE_EAST);
        else if (Dm.truthy(Dm.bitand(direct, WEST)))
            await set_lying_angle(LYING_ANGLE_WEST);
    }

    // tg: code/modules/mob/living/living.dm:1098
    public virtual async DmTask<object> itch(obj_item_bodypart target_part = null, float? damage = null, float? can_scratch = null, float? silent = null)
    {
        // Gaps RG2 (D339): itching powder.
        damage ??= 0.5f;
        can_scratch ??= TRUE;
        silent ??= FALSE;
        if (Dm.truthy(Dm.bitand(mob_biotypes, Dm.bitor(MOB_ROBOTIC, MOB_SPIRIT))))
            return FALSE;
        bool will_scratch = Dm.truthy(can_scratch) && !Dm.truthy(incapacitated);
        float applied_damage = 0;
        if (will_scratch && Dm.truthy(damage))
            applied_damage = Dm.num(await apply_damage(damage, damagetype: BRUTE, def_zone: target_part));
        if (Dm.truthy(silent))
            return applied_damage > 0 ? TRUE : FALSE;
        object visible_part = target_part == null ? I18n.T("side") : target_part.plaintext_zone;
        visible_message(Dm.truthy(can_scratch) ? span_warning(I18n.T("[] scratches [] [].", this, p_their(), visible_part)) : "", span_warning(Dm.truthy(can_scratch) ? I18n.T("Your [] itches. You scratch it.", visible_part) : I18n.T("Your [] itches. ", visible_part)));
        return TRUE;
    }

    // tg: code/modules/mob/living/living.dm:1111
    public override void experience_pressure_difference(object pressure_difference = null, object direction = null, float? pressure_resistance_prob_delta = null)
    {
        pressure_resistance_prob_delta ??= 0;
        playsound(this, Dm.resource("sound/effects/space_wind.ogg"), 50, TRUE); // tg: code/modules/mob/living/living.dm:1112
        if (Dm.truthy(buckled) || Dm.truthy(mob_negates_gravity()))
            return;
        // DEVIATION(DV259): the dev fly is never moved by the air, in space too, where the fly's negated gravity does not hold (the owner, 2026-11-02)
        if (HAS_TRAIT_FROM(this, TRAIT_MOVE_FLYING, ADMIN_TRAIT))
            return;

        // DEVIATION(DV15): a capsule's steps do not push move_delay; a player walking the capsule resists as one moving by client/Move
        if (client != null && (client.move_delay >= world.time + world.tick_lag * 2 || LocomotionSeam.Steers(this)))
            pressure_resistance_prob_delta -= 30;

        DmList turfs_to_check = Dm.list();

        if (!Dm.truthy(has_limbs))
        {
            turf T = get_step(this, Dm.num(angle2dir(Dm.num(dir2angle(direction)) + 90))) as turf;
            if (T != null)
                Dm.append(ref turfs_to_check, T);

            T = get_step(this, Dm.num(angle2dir(Dm.num(dir2angle(direction)) - 90))) as turf;
            if (T != null)
                Dm.append(ref turfs_to_check, T);

            foreach (object t in Dm.each(turfs_to_check))
            {
                T = t as turf;
                if (Dm.truthy(T.density))
                {
                    pressure_resistance_prob_delta -= 20;
                    continue;
                }
                foreach (atom_movable AM in Dm.each<atom_movable>(T))
                {
                    if (Dm.truthy(AM.density) && Dm.truthy(AM.anchored))
                    {
                        pressure_resistance_prob_delta -= 20;
                        break;
                    }
                }
            }
        }
        base.experience_pressure_difference(pressure_difference, direction, pressure_resistance_prob_delta);
    }

    // tg: code/modules/mob/living/living.dm:1141  (M4b slice 3: resisting, stop drop and roll)
    public override object can_resist()
    {
        if (Dm.num(next_move) > world.time)
            return FALSE;
        if (HAS_TRAIT(this, TRAIT_INCAPACITATED))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/living.dm:1148  (M4b slice 3)
    public virtual void resist()
    {
        // DEFAULT_QUEUE_OR_CALL_VERB(VERB_CALLBACK(src, PROC_REF(execute_resist))) (__DEFINES/verb_manager.dm:32, :25-28): the
        // macro builds its callback argument twice, once to queue and once to call.
        if (!Dm.truthy(_queue_verb(Dm.@new<datum_callback_verb_callback>(this, nameof(execute_resist)), VERB_DEFAULT_QUEUE_THRESHOLD, null)))
            Dm.@new<datum_callback_verb_callback>(this, nameof(execute_resist)).InvokeAsync();
    }

    ///proc extender of [/mob/living/verb/resist] meant to make the process queable if the server is overloaded when the verb is called
    // tg: code/modules/mob/living/living.dm:1152  (M4b slice 3)
    public virtual async DmTask execute_resist()
    {
        if (!Dm.truthy(can_resist()))
            return;
        changeNext_move(CLICK_CD_RESIST);

        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_LIVING_RESIST]) ? (object)NONE : _SendSignal(COMSIG_LIVING_RESIST, Dm.list(this)), COMPONENT_BLOCK_RESIST)))
            return;

        //resisting grabs (as if it helps anyone...)
        if (!HAS_TRAIT(this, TRAIT_RESTRAINED) && Dm.truthy(pulledby))
        {
            log_combat(this, pulledby, "resisted grab");
            await resist_grab();
            return;
        }

        //unbuckling yourself
        if (Dm.truthy(buckled) && last_special <= world.time)
            await resist_buckle();

        //Breaking out of a container (Locker, sleeper, cryo...)
        else if (loc != get_turf(this))
        {
            await loc.container_resist_act(this);
        }

        else if (Dm.truthy(Dm.bitand(mobility_flags, MOBILITY_MOVE)))
        {
            if (Dm.truthy(on_fire))
                await resist_fire(); //stop, drop, and roll
            else if (last_special <= world.time)
                await resist_restraints(); //trying to remove cuffs.
        }
    }

    // tg: code/modules/mob/living/living.dm:1183  (M4b slice 3: resisting)
    public override async DmTask<object> resist_grab(object moving_resist = null)
    {
        if (HAS_TRAIT(this, TRAIT_INCAPACITATED))
            return FALSE;

        DmList grab_stats = Dm.list(
            // Our effective grab state.
            // GRAB_PASSIVE is equal to 0, so if we have no other altering factors to our grab state, we can break free immediately on resist.
            pulledby.grab_state,
            /// The amount of damage inflicted on a failed resist attempt.
            rand(7, 13),
            // Base chance to escape a grab. Divided by effective grab state.
            BASE_GRAB_RESIST_CHANCE
        );
        if (Dm.truthy(pulledby._listen_lookup?[COMSIG_MOVABLE_GRABBED_RESISTING]))
            pulledby._SendSignal(COMSIG_MOVABLE_GRABBED_RESISTING, Dm.list(pulledby, this, grab_stats));

        float effective_grab_state = Dm.num(grab_stats[GRAB_STAT_EFFECTIVE_STATE]);
        float damage_on_resist_fail = Dm.num(grab_stats[GRAB_STAT_FAIL_DAMAGE]);
        float escape_chance = Dm.num(grab_stats[GRAB_STAT_ESCAPE_CHANCE]);

        if (body_position == LYING_DOWN) //If prone, treat the grab state as one higher
            effective_grab_state++;

        if (HAS_TRAIT(this, TRAIT_GRABWEAKNESS)) //If we have grab weakness from some source, treat the grab state as one higher
            effective_grab_state++;

        if (Dm.truthy(get_timed_status_effect_duration(typeof(datum_status_effect_staggered))) && (Dm.num(get_fire_loss()) + Dm.num(get_brute_loss())) >= 40) //If we are staggered, and we have at least 40 damage, treat the grab state as one higher.
            effective_grab_state++;

        if (HAS_TRAIT(this, TRAIT_GRABRESISTANCE)) //If we have grab resistance from some source, treat the grab state as one lower.
            effective_grab_state--;

        // If our puller is a human, and they have an active hand they're grabbing with (please don't ask how people grab without hands), then apply their unarmed values to the grab values
        if (ishuman(pulledby))
        {
            mob_living_carbon_human human_puller = (mob_living_carbon_human)pulledby;
            obj_item_bodypart grabbing_bodypart = human_puller.get_active_hand() as obj_item_bodypart;
            if (Dm.truthy(grabbing_bodypart))
            {
                damage_on_resist_fail += (rand(grabbing_bodypart.unarmed_damage_low, grabbing_bodypart.unarmed_damage_high)) + grabbing_bodypart.unarmed_grab_damage_bonus;
                effective_grab_state += grabbing_bodypart.unarmed_grab_state_bonus;
                escape_chance += grabbing_bodypart.unarmed_grab_escape_chance_bonus;
            }
        }

        //We only resist our grab state if we are currently in a grab equal to or greater than GRAB_AGGRESSIVE (1). Otherwise, break out immediately!
        if (effective_grab_state >= GRAB_AGGRESSIVE)
        {
            // see defines/combat.dm, this should be baseline 60%
            // Resist chance divided by the value imparted by your grab state. It isn't until you reach neckgrab that you gain a penalty to escaping a grab.
            float resist_chance = clamp(escape_chance / effective_grab_state, 0, 100);
            if (prob(resist_chance))
            {
                visible_message(span_danger(I18n.T("[] breaks free of []'s grip!", this, pulledby)),
                                span_danger(I18n.T("You break free of []'s grip!", pulledby)), null, null, pulledby);
                to_chat(pulledby, span_warning(I18n.T("[] breaks free of your grip!", this)));
                log_combat(pulledby, this, "broke grab");
                await pulledby.stop_pulling();
                return TRUE;
            }

            await adjust_stamina_loss(damage_on_resist_fail); //Do some stamina damage if we fail to resist
            visible_message(
                span_danger(I18n.T("[] struggles as they fail to break free of []'s grip!", this, pulledby)),
                span_warning(I18n.T("You struggle as you fail to break free of []'s grip!", pulledby)),
                null,
                null,
                pulledby
            );
            to_chat(pulledby, span_danger(I18n.T("[] struggles as they fail to break free of your grip!", this)));
            if (Dm.truthy(moving_resist) && Dm.truthy(client)) //we resisted by trying to move
                client.move_delay = world.time + 4 * SECONDS;
            return FALSE;
        }

        await pulledby.stop_pulling();
        return TRUE;
    }

    // tg: code/modules/mob/living/living.dm:1252
    public virtual async DmTask resist_buckle()
    {
        await buckled.user_unbuckle_mob(this, this);
    }

    // tg: code/modules/mob/living/living.dm:1255  (M4b slice 3)
    public virtual async DmTask<object> resist_fire()
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/living.dm:1258
    public virtual async DmTask resist_restraints()
    {
    }

    // tg: code/modules/mob/living/living.dm:1261
    public virtual async DmTask update_gravity(object gravity = null)
    {
        // Handle movespeed stuff
        float speed_change = max(0, Dm.num(gravity) - STANDARD_GRAVITY);
        if (Dm.truthy(speed_change))
            await add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_gravity), multiplicative_slowdown: speed_change);
        else
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_gravity));

        // Time to add/remove gravity alerts. sorry for the mess it's gotta be fast
        atom_movable_screen_alert gravity_alert = alerts?[ALERT_GRAVITY] as atom_movable_screen_alert;
        object sw__1 = gravity;
        if (Dm.in_to(sw__1, -INFINITY, NEGATIVE_GRAVITY))
        {
            if (!(gravity_alert is atom_movable_screen_alert_negative))
            {
                throw_alert(ALERT_GRAVITY, typeof(atom_movable_screen_alert_negative));
                ADD_TRAIT(this, TRAIT_MOVE_UPSIDE_DOWN, NEGATIVE_GRAVITY_TRAIT);
                matrix flipped_matrix = transform as matrix;
                flipped_matrix.b = -flipped_matrix.b;
                flipped_matrix.e = -flipped_matrix.e;
                animate(this, Dm.named("transform", flipped_matrix), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)), Dm.named("flags", ANIMATION_PARALLEL));
                add_offsets(NEGATIVE_GRAVITY_TRAIT, y_add: 4);
            }
        }
        else if (Dm.in_to(sw__1, NEGATIVE_GRAVITY + 0.01f, 0))
        {
            if (!(gravity_alert is atom_movable_screen_alert_weightless))
            {
                throw_alert(ALERT_GRAVITY, typeof(atom_movable_screen_alert_weightless));
                ADD_TRAIT(this, TRAIT_MOVE_FLOATING, NO_GRAVITY_TRAIT);
            }
        }
        else if (Dm.in_to(sw__1, 0.01f, STANDARD_GRAVITY))
        {
            if (Dm.truthy(gravity_alert))
                clear_alert(ALERT_GRAVITY);
        }
        else if (Dm.in_to(sw__1, STANDARD_GRAVITY + 0.01f, GRAVITY_DAMAGE_THRESHOLD - 0.01f))
            throw_alert(ALERT_GRAVITY, typeof(atom_movable_screen_alert_highgravity));
        else if (Dm.in_to(sw__1, GRAVITY_DAMAGE_THRESHOLD, INFINITY))
            throw_alert(ALERT_GRAVITY, typeof(atom_movable_screen_alert_veryhighgravity));

        // If we had no gravity alert, or the same alert as before, go home
        if (!Dm.truthy(gravity_alert) || alerts[ALERT_GRAVITY] == gravity_alert)
            return;
        // By this point we know that we do not have the same alert as we used to
        if (gravity_alert is atom_movable_screen_alert_weightless)
            REMOVE_TRAIT(this, TRAIT_MOVE_FLOATING, NO_GRAVITY_TRAIT);
        if (gravity_alert is atom_movable_screen_alert_negative)
        {
            REMOVE_TRAIT(this, TRAIT_MOVE_UPSIDE_DOWN, NEGATIVE_GRAVITY_TRAIT);
            matrix flipped_matrix = transform as matrix;
            flipped_matrix.b = -flipped_matrix.b;
            flipped_matrix.e = -flipped_matrix.e;
            animate(this, Dm.named("transform", flipped_matrix), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)), Dm.named("flags", ANIMATION_PARALLEL));
            remove_offsets(NEGATIVE_GRAVITY_TRAIT);
        }
    }

    // tg: code/modules/mob/living/living.dm:1316
    public virtual async DmTask<object> get_temperature(datum_gas_mixture environment = null)
    {
        object loc_temp = (Dm.truthy(environment) ? (object)environment.temperature : T0C);
        if (isobj(loc))
        {
            obj oloc = loc as obj;
            object obj_temp = await oloc.return_temperature();
            if (obj_temp != null)
                loc_temp = obj_temp;
        }
        else if (isspaceturf(get_turf(this)))
        {
            turf heat_turf = get_turf(this);
            loc_temp = heat_turf.temperature;
        }
        if (ismovable(loc))
        {
            atom_movable occupied_space = loc as atom_movable;
            loc_temp = Dm.add(Dm.mul(1 - occupied_space.contents_thermal_insulation, loc_temp), occupied_space.contents_thermal_insulation * bodytemperature);
        }
        return loc_temp;
    }

    // tg: code/modules/mob/living/living.dm:1333
    public virtual object can_track(mob_living user = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:1333", "can_track: not in M2 (M4a/M5/M10+/M4b by area)");
        return default;
    }

    // tg: code/modules/mob/living/living.dm:1355
    public virtual async DmTask harvest(mob_living user = null)
    {
        // tg's body is empty: used for extra objects etc. in butchering, the mobs override it
    }

    // tg: code/modules/mob/living/living.dm:1436
    public virtual object can_use_guns(obj_item G = null)//actually used for more than guns!
    {
        if (G.trigger_guard == TRIGGER_GUARD_NONE)
        {
            to_chat(this, span_warning(I18n.T("You are unable to fire this!")));
            return FALSE;
        }
        if (G.trigger_guard != TRIGGER_GUARD_ALLOW_ALL && (!ISADVANCEDTOOLUSER(this) && !HAS_TRAIT(this, TRAIT_GUN_NATURAL)))
        {
            to_chat(this, span_warning(I18n.T("You try to fire [], but can't use the trigger!", G)));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/living.dm:1445
    public virtual async DmTask update_stamina()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_STAMINA_UPDATE]))
            _SendSignal(COMSIG_LIVING_STAMINA_UPDATE, Dm.list(this));
        update_stamina_hud();
    }

    // tg: code/modules/mob/living/living.dm:1449
    [DmParams("shown_stamina_loss")]
    public override void update_stamina_hud(params object[] __extra)
    {
        object shown_stamina_loss = Dm.arg(__extra, 0);
        if (!Dm.truthy(client) || !Dm.truthy(hud_used))
            return;

        // tg: :1453  var/atom/movable/screen/stamina/stamina = hud_used.screen_objects[HUD_MOB_STAMINA]
        // The port's HUD makes no stamina meter (D107: the doll, the indicator, the fullscreens and the alerts; stamina is not part of
        // M2's medicine, scope 2g), so the lookup finds nothing and the proc returns at its `if(!stamina)` (:1454), as tg's does for a
        // HUD without the meter. The meter's states stay below for the milestone that adds the screen object.
        atom_movable_screen stamina = hud_used.screen_objects[HUD_MOB_STAMINA] as atom_movable_screen;
        if (stamina == null)
            return;

        if (stat == DEAD)
        {
            stamina.icon_state = "stamina_dead";
            return;
        }

        float stam_crit_threshold = maxHealth - crit_threshold;
        if (shown_stamina_loss == null)
            shown_stamina_loss = get_stamina_loss();

        if (Dm.ge(shown_stamina_loss, stam_crit_threshold))
            stamina.icon_state = "stamina_crit";
        else if (Dm.gt(shown_stamina_loss, 0) && maxHealth > 0)
            stamina.icon_state = Dm.text("stamina_[]", ceil(Dm.num(shown_stamina_loss) / (maxHealth * 0.2f)));
        else
            stamina.icon_state = "stamina_full";
    }

    // tg: code/modules/mob/living/living.dm:1489
    public virtual async DmTask<object> wabbajack(object what_to_randomize = null, float? change_flags = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:1489", "wabbajack: not in M2 (M4a/M5/M10+/M4b by area)");
        return default;
    }

    // tg: code/modules/mob/living/living.dm:1757
    public virtual async DmTask on_wabbajacked(mob_living new_mob = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:1757", "on_wabbajacked: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    //Callback proc to tone down spam from multiple sizzling frying oil dipping.
    // tg: code/modules/mob/living/living.dm:1771  (gaps RG1: the hot cooking oil's splash)
    public virtual void unfry_mob()
    {
        REMOVE_TRAIT(this, TRAIT_OIL_FRIED, "cooking_oil_react");
    }

    // tg: code/modules/mob/living/living.dm:1779  (M4b slice 3)
    public virtual object ignite_mob(object silent = null)
    {
        if (fire_stacks <= 0)
            return FALSE;

        datum_status_effect_fire_handler_fire_stacks fire_status = has_status_effect(typeof(datum_status_effect_fire_handler_fire_stacks)) as datum_status_effect_fire_handler_fire_stacks;
        if (!Dm.truthy(fire_status) || Dm.truthy(fire_status.on_fire))
            return FALSE;

        return fire_status.ignite(Dm.truthy(silent) ? TRUE : FALSE);
    }

    /**
     * Extinguish all fire on the mob
     *
     * This removes all fire stacks, fire effects, alerts, and moods
     * Signals the extinguishing.
     */
    // tg: code/modules/mob/living/living.dm:1795  (M4b slice 3)
    public virtual void extinguish_mob()
    {
        if (HAS_TRAIT(this, TRAIT_NO_EXTINGUISH)) //The everlasting flames will not be extinguished
            return;
        datum_status_effect_fire_handler_fire_stacks fire_status = has_status_effect(typeof(datum_status_effect_fire_handler_fire_stacks)) as datum_status_effect_fire_handler_fire_stacks;
        if (!Dm.truthy(fire_status) || !Dm.truthy(fire_status.on_fire))
            return;
        remove_status_effect(typeof(datum_status_effect_fire_handler_fire_stacks));
    }

    /**
     * Adjust the amount of fire stacks on a mob
     *
     * This modifies the fire stacks on a mob.
     *
     * Vars:
     * * stacks: int The amount to modify the fire stacks
     * * fire_type: type Type of fire status effect that we apply, should be subtype of /datum/status_effect/fire_handler/fire_stacks
     */
    // tg: code/modules/mob/living/living.dm:1813  (M4b slice 3)
    public virtual async DmTask adjust_fire_stacks(object stacks = null, Type fire_type = null)
    {
        fire_type ??= typeof(datum_status_effect_fire_handler_fire_stacks);
        if (Dm.num(stacks) < 0)
        {
            if (HAS_TRAIT(this, TRAIT_NO_EXTINGUISH)) //You can't reduce fire stacks of the everlasting flames
                return;
            stacks = max(-fire_stacks, Dm.num(stacks));
        }
        await apply_status_effect(fire_type, stacks);
    }

    // tg: code/modules/mob/living/living.dm:1820  (M4b slice 3)
    public virtual async DmTask adjust_wet_stacks(object stacks = null, Type wet_type = null)
    {
        wet_type ??= typeof(datum_status_effect_fire_handler_wet_stacks);
        if (HAS_TRAIT(this, TRAIT_NO_EXTINGUISH)) //The everlasting flames will not be extinguished
            return;
        if (Dm.num(stacks) < 0)
            stacks = max(fire_stacks, Dm.num(stacks));
        await apply_status_effect(wet_type, stacks);
    }

    /**
     * Set the fire stacks on a mob
     *
     * This sets the fire stacks on a mob, stacks are clamped between -20 and 20.
     * If the fire stacks are reduced to 0 then we will extinguish the mob.
     *
     * Vars:
     * * stacks: int The amount to set fire_stacks to
     * * fire_type: type Type of fire status effect that we apply, should be subtype of /datum/status_effect/fire_handler/fire_stacks
     * * remove_wet_stacks: bool If we remove all wet stacks upon doing this
     */
    // tg: code/modules/mob/living/living.dm:1839  (M4b slice 3)
    public virtual async DmTask set_fire_stacks(object stacks = null, Type fire_type = null, float? remove_wet_stacks = null)
    {
        fire_type ??= typeof(datum_status_effect_fire_handler_fire_stacks);
        remove_wet_stacks ??= TRUE;
        if (Dm.num(stacks) < 0) //Shouldn't happen, ever
            throw Dm.CRASH(Dm.text("set_fire_stacks received negative [] fire stacks", stacks), "code/modules/mob/living/living.dm", 1841);

        if (Dm.truthy(remove_wet_stacks))
            remove_status_effect(typeof(datum_status_effect_fire_handler_wet_stacks));

        if (Dm.num(stacks) == 0)
        {
            remove_status_effect(fire_type);
            return;
        }

        await apply_status_effect(fire_type, stacks, TRUE);
    }

    // tg: code/modules/mob/living/living.dm:1852  (M4b slice 3)
    public virtual async DmTask set_wet_stacks(object stacks = null, Type wet_type = null, float? remove_fire_stacks = null)
    {
        wet_type ??= typeof(datum_status_effect_fire_handler_wet_stacks);
        remove_fire_stacks ??= TRUE;
        if (Dm.num(stacks) < 0)
            throw Dm.CRASH(Dm.text("set_wet_stacks received negative [] wet stacks", stacks), "code/modules/mob/living/living.dm", 1854);

        if (Dm.truthy(remove_fire_stacks))
            remove_status_effect(typeof(datum_status_effect_fire_handler_fire_stacks));

        if (Dm.num(stacks) == 0)
        {
            remove_status_effect(wet_type);
            return;
        }

        await apply_status_effect(wet_type, stacks, TRUE);
    }

    //Share fire evenly between the two mobs
    //Called in MobBump() and Crossed()
    // tg: code/modules/mob/living/living.dm:1867
    public virtual async DmTask spreadFire(mob_living spread_to = null)
    {
        if (!(spread_to is mob_living))
            return;

        // can't spread fire to mobs that don't catch on fire
        if (HAS_TRAIT(spread_to, TRAIT_NOFIRE_SPREAD) || HAS_TRAIT(this, TRAIT_NOFIRE_SPREAD))
            return;

        datum_status_effect_fire_handler_fire_stacks fire_status = has_status_effect(typeof(datum_status_effect_fire_handler_fire_stacks)) as datum_status_effect_fire_handler_fire_stacks;
        datum_status_effect_fire_handler_fire_stacks their_fire_status = spread_to.has_status_effect(typeof(datum_status_effect_fire_handler_fire_stacks)) as datum_status_effect_fire_handler_fire_stacks;
        if (Dm.truthy(fire_status) && Dm.truthy(fire_status.on_fire))
        {
            if (Dm.truthy(their_fire_status) && Dm.truthy(their_fire_status.on_fire))
            {
                float firesplit = (fire_stacks + spread_to.fire_stacks) / 2;
                Type fire_type = (spread_to.fire_stacks > fire_stacks) ? their_fire_status.type : fire_status.type;
                await set_fire_stacks(firesplit, fire_type);
                await spread_to.set_fire_stacks(firesplit, fire_type);
                return;
            }

            await adjust_fire_stacks(-fire_stacks / 2, fire_status.type);
            await spread_to.adjust_fire_stacks(fire_stacks, fire_status.type);
            if (Dm.truthy(spread_to.ignite_mob()))
            {
                log_message(Dm.text("bumped into [] and set them on fire.", key_name(spread_to)), LOG_ATTACK);
            }
            return;
        }

        if (!Dm.truthy(their_fire_status) || !Dm.truthy(their_fire_status.on_fire))
            return;

        await spread_to.adjust_fire_stacks(-spread_to.fire_stacks / 2, their_fire_status.type);
        await adjust_fire_stacks(spread_to.fire_stacks, their_fire_status.type);
        ignite_mob();
    }

    /**
     * Gets the fire overlay to use for this mob
     *
     * Args:
     * * stacks: Current amount of fire_stacks
     * * on_fire: If we're lit on fire
     *
     * Return a mutable appearance, the overlay that will be applied.
     */
    // tg: code/modules/mob/living/living.dm:1908  (M4b slice 3)
    public virtual object get_fire_overlay(object stacks = null, object on_fire = null)
    {
        // RETURN_TYPE(/mutable_appearance)
        return null;
    }

    /// Create a fire overlay using the generic fire sprites
    // tg: code/modules/mob/living/living.dm:1913  (M4b slice 3)
    public virtual object make_generic_fire_overlay()
    {
        string fire_key = Dm.text("[]_[]_fire", base_pixel_x, base_pixel_y);
        if (!Dm.truthy(GLOB.fire_appearances[fire_key]))
        {
            mutable_appearance fire = mutable_appearance(
                Dm.resource("icons/mob/effects/onfire.dmi"),
                "generic_fire",
                ABOVE_ALL_MOB_LAYER,
                appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)
            ) as mutable_appearance;
            fire.pixel_x = -1 * base_pixel_x;
            fire.pixel_y = -1 * base_pixel_y;
            GLOB.fire_appearances[fire_key] = fire;
        }

        return GLOB.fire_appearances[fire_key];
    }

    /// Takes a fire overlay and generates an emissive appearance for it
    // tg: code/modules/mob/living/living.dm:1929  (M4b slice 3)
    public virtual object make_fire_emissive(mutable_appearance fire_overlay = null)
    {
        // return emissive_appearance(fire_overlay.icon, fire_overlay.icon_state, src, fire_overlay.layer): the emissive plane,
        // which the client's light does not use (D24)
        Dm.Deferred("code/modules/mob/living/living.dm:1930", "make_fire_emissive: emissive_appearance (the emissive plane, D24)");
        return null;
    }

    /**
     * Handles effects happening when mob is on normal fire
     *
     * Vars:
     * * seconds_per_tick
     * * times_fired
     * * fire_handler: Current fire status effect that called the proc
     */
    // tg: code/modules/mob/living/living.dm:1941  (M4b slice 3)
    public virtual async DmTask on_fire_stack(object seconds_per_tick = null, datum_status_effect_fire_handler_fire_stacks fire_handler = null)
    {
        return;
    }

    // tg: code/modules/mob/living/living.dm:1947
    public virtual async DmTask knockOver(mob_living_carbon C = null)
    {
        if (Dm.truthy(C.key)) //save us from monkey hordes
            C.visible_message(span_warning(pick(
                I18n.T("[] dives out of []'s way!", C, this),
                I18n.T("[] stumbles over []!", C, this),
                I18n.T("[] jumps out of []'s path!", C, this),
                I18n.T("[] trips over [] and falls!", C, this),
                I18n.T("[] topples over []!", C, this),
                I18n.T("[] leaps out of []'s way!", C, this)) as string));
        await C.Paralyze(40);
    }

    // tg: code/modules/mob/living/living.dm:1958  (pulling, 2026-10-15)
    public override object can_be_pulled(object user = null, object force = null)
    {
        return Dm.truthy(base.can_be_pulled(user, force)) && !Dm.truthy(buckled?.buckle_prevents_pull) ? TRUE : FALSE;
    }

    /// Called when mob changes from a standing position into a prone while lacking the ability to stand up at the moment.
    // tg: code/modules/mob/living/living.dm:1963
    [ShouldCallParent]
    public virtual async DmTask on_fall()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_THUD]))
            _SendSignal(COMSIG_LIVING_THUD, Dm.list(this));
        return;
    }

    // tg: code/modules/mob/living/living.dm:1969  (pulling, 2026-10-15: a moved puller lets go)
    public override async DmTask<object> forceMove(atom destination = null, params object[] __extra)
    {
        object dot = null;
        if (!Dm.truthy(currently_z_moving))
        {
            await stop_pulling();
            if (Dm.truthy(buckled) && !HAS_TRAIT(this, TRAIT_CANNOT_BE_UNBUCKLED))
                await buckled.unbuckle_mob(this, force: TRUE);
            if (Dm.truthy(has_buckled_mobs()))
                unbuckle_all_mobs(force: TRUE);
        }
        await refresh_gravity();
        dot = await base.forceMove(destination, __extra);
        if (Dm.truthy(dot) && Dm.truthy(client))
            await reset_perspective();
        return dot;
    }

    // tg: code/modules/mob/living/living.dm:1981
    public virtual async DmTask update_z(object new_z = null) // 1+ to register, null to unregister
    {
        if (Dm.eq(registered_z, new_z))
            return;
        if (Dm.truthy(registered_z))
            Dm.remove_at(datum_controller_subsystem_mobs.clients_by_zlevel, registered_z, this);
        if (client == null)
        {
            registered_z = null;
            return;
        }

        //Check the amount of clients exists on the Z level we're leaving from,
        //this excludes us because at this point we are not registered to any z level.
        object old_level_new_clients = (Dm.truthy(registered_z) ? (object)((DmList)datum_controller_subsystem_mobs.clients_by_zlevel[registered_z]).len : null);
        //No one is left after we're gone, recalculate AI status so eligible ones shut off
        if (Dm.truthy(registered_z) && Dm.eq(old_level_new_clients, 0))
        {
            // tg: code/modules/mob/living/living.dm:1995
            foreach (datum_ai_controller controller in Dm.each_cast<datum_ai_controller>(LAZYACCESS(SSai_controllers?.ai_controllers_by_zlevel, registered_z)))
                Waiting.NoSleep(controller.set_ai_status(controller.get_expected_ai_status()));
        }

        if (Dm.truthy(new_z))
        {
            //Check the amount of clients exists on the Z level we're moving towards, excluding ourselves.
            float new_level_old_clients = ((DmList)datum_controller_subsystem_mobs.clients_by_zlevel[new_z]).len;

            //We'll add ourselves to the list now so get_expected_ai_status() will know we're on the z level.
            Dm.append_at(datum_controller_subsystem_mobs.clients_by_zlevel, new_z, this);

            if (new_level_old_clients == 0) //No one was here before, wake up all the AIs.
            {
                // tg: code/modules/mob/living/living.dm:2007
                foreach (datum_ai_controller controller in Dm.each_cast<datum_ai_controller>(LAZYACCESS(SSai_controllers?.ai_controllers_by_zlevel, new_z)))
                {
                    //We don't set them directly on, for instances like AIs acting while dead and other cases that may exist in the future.
                    //This isn't a problem for AIs with a client since the client will prevent this from being called anyway.
                    Waiting.NoSleep(controller.set_ai_status(controller.get_expected_ai_status()));
                }
            }
        }

        registered_z = new_z;
    }

    // tg: code/modules/mob/living/living.dm:2013  (the first of two definitions on /mob/living; living_movement.dm:29 is the
    // later one, the slot's override, whose ..() calls this, rules 7.5)
    public async DmTask on_changed_z_level__d0(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        await update_z(new_turf?.z);
    }

    // tg: code/modules/mob/living/living.dm:2017
    public virtual void set_name()
    {
        if (identifier == 0)
            identifier = rand(1, 999);
        name = I18n.T("[] ([])", name, identifier);
        real_name = name;
    }

    // tg: code/modules/mob/living/living.dm:2023
    public virtual async DmTask mob_pickup(mob_living user = null)
    {
        obj_item_mob_holder holder = Dm.@new(inhand_holder_type, get_turf(this), this, held_state, head_icon, held_lh, held_rh, worn_slot_flags) as obj_item_mob_holder;
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_SCOOPED_UP]))
            _SendSignal(COMSIG_LIVING_SCOOPED_UP, Dm.list(this, user, holder));
        user.visible_message(span_warning(Dm.text("[] scoops up []!", user, this)));
        await user.put_in_hands(holder);
    }

    // M7 slice T3 (D164): the sight of a living mob. The flags and the lighting cutoffs it sets reach the player's client (protocol
    // version 31: the 3D client brightens the dark by the cutoffs and shows the turfs of SEE_TURFS through the walls).
    // tg: code/modules/mob/living/living.dm:2029
    public override void update_sight()
    {
        if (!Dm.truthy(client))
            return;

        if (stat == DEAD && !HAS_TRAIT(this, TRAIT_CORPSELOCKED))
        {
            if (Dm.truthy(SSmapping.level_trait(z, ZTRAIT_NOXRAY)))
                set_sight(NONE);
            else if (Dm.truthy(is_secret_level(z)))
                set_sight(Dm.initial(this, (mob p) => p.sight));
            else
                set_sight(Dm.bitor(SEE_MOBS, SEE_OBJS)); // DEVIATION(DV174): no SEE_TURFS, the 3D client would draw a corpse's view as mesons
            set_invis_see(SEE_INVISIBLE_OBSERVER);
            GhostNightVision.Refresh(this); // DV174: the corpse's dark is lifted only by its night-vision action, not by the goggles it died in
            return;
        }

        object new_sight = restore_initial_sight();

        if (client.eye != null && client.eye != this)
        {
            atom atom = client.eye;
            if (Dm.truthy(atom.update_remote_sight(this))) //returns TRUE if we override all other sight updates.
            {
                base.update_sight(); //still, call sync_lighting_plane_cutoff()
                return;
            }
        }

        obj_item_organ_eyes eyes = get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes != null)
        {
            set_invis_see(eyes.see_invisible);
            if (eyes.lighting_cutoff != null)
                lighting_cutoff = Dm.num(eyes.lighting_cutoff);
            if (eyes.color_cutoffs != null)
                lighting_color_cutoffs = blend_cutoff_colors(lighting_color_cutoffs, eyes.color_cutoffs) as DmList;
        }

        obj_item_clothing_glasses glasses = get_item_by_slot(ITEM_SLOT_EYES) as obj_item_clothing_glasses;
        if (glasses is obj_item_clothing_glasses && Dm.truthy(Dm.bitand(glasses.item_flags, IN_INVENTORY)))
        {
            set_invis_see(Dm.truthy(glasses.invis_override) ? glasses.invis_override : min(glasses.invis_view, see_invisible));
            if (glasses.lighting_cutoff != null)
                lighting_cutoff = max(lighting_cutoff ?? 0, Dm.num(glasses.lighting_cutoff));
            if (Dm.truthy(length(glasses.color_cutoffs)))
                lighting_color_cutoffs = blend_cutoff_colors(lighting_color_cutoffs, glasses.color_cutoffs) as DmList;
        }

        // An average (ranging from 1 to 100) of the lighting_color_cutoffs values.
        // Used to avoid the hardcoded lighting cutoff from overly stacking with the more specific lighting color cutoffs from eyes and glasses
        // (or innate in the case of some mobs), with the exception of night vision I guess.
        float avg_light_color_cutoff = Dm.num(Dm.div(Dm.add(Dm.add(lighting_color_cutoffs[1], lighting_color_cutoffs[2]), lighting_color_cutoffs[3]), 3));

        if (HAS_TRAIT(this, TRAIT_MESON_VISION))
        {
            new_sight = Dm.bitor(new_sight, SEE_TURFS);
            lighting_cutoff = max(lighting_cutoff ?? 0, LIGHTING_CUTOFF_REAL_LOW - avg_light_color_cutoff);
        }

        if (HAS_TRAIT(this, TRAIT_THERMAL_VISION))
        {
            new_sight = Dm.bitor(new_sight, SEE_MOBS);
            lighting_cutoff = max(lighting_cutoff ?? 0, LIGHTING_CUTOFF_MEDIUM - avg_light_color_cutoff);
        }

        if (HAS_TRAIT(this, TRAIT_XRAY_VISION))
            new_sight = Dm.bitor(new_sight, Dm.bitor(Dm.bitor(SEE_TURFS, SEE_MOBS), SEE_OBJS));

        if (HAS_TRAIT(this, TRAIT_MATERIAL_VISON))
            new_sight = Dm.bitor(new_sight, SEE_OBJS);

        if (Dm.truthy(SSmapping.level_trait(z, ZTRAIT_NOXRAY)))
            new_sight = NONE;

        if (HAS_TRAIT(this, TRAIT_TRUE_NIGHT_VISION))
            lighting_cutoff = max(lighting_cutoff ?? 0, LIGHTING_CUTOFF_HIGH);

        if (HAS_TRAIT(this, TRAIT_ECHOLOCATOR))
        {
            new_sight = Dm.bitor(new_sight, Dm.bitor(SEE_MOBS, SEE_TURFS));
            lighting_cutoff = max(lighting_cutoff ?? 0, LIGHTING_CUTOFF_FULLBRIGHT);
        }

        set_sight(new_sight);
        base.update_sight();
    }

    ///Part of the [/mob/living/update_sight()] stack. This shouldn't be used outside of it.
    // tg: code/modules/mob/living/living.dm:2099
    [ShouldCallParent]
    public virtual object restore_initial_sight()
    {
        float init_sight = Dm.initial(this, (mob p) => p.sight);
        //we cannot see mobs and/or objects unless we have thermals/xray/material vision, but we can still see turfs to navigate around
        if (HAS_TRAIT(this, TRAIT_MOVE_VENTCRAWLING))
            init_sight = Dm.num(Dm.bitor(init_sight, Dm.bitor(SEE_TURFS, BLIND)));
        init_sight = Dm.num(Dm.bitor(init_sight, (!Dm.truthy(_listen_lookup?[COMSIG_LIVING_RESTORE_INITIAL_SIGHT]) ? NONE : _SendSignal(COMSIG_LIVING_RESTORE_INITIAL_SIGHT, Dm.list(this)))));
        lighting_cutoff = Dm.initial(this, (mob p) => p.lighting_cutoff);
        lighting_color_cutoffs = Dm.list(lighting_cutoff_red, lighting_cutoff_green, lighting_cutoff_blue);
        return Dm.initial(this, (mob p) => p.sight);
    }

    // tg: code/modules/mob/living/living.dm:2111
    public virtual async DmTask<object> mob_try_pickup(mob_living user = null, float? instant = null)
    {
        instant ??= FALSE;
        if (!ishuman(user) && (Dm.num(user.mob_size) <= Dm.num(mob_size) || !Dm.truthy(user.num_hands)))
        {
            if (!Dm.truthy(user.num_hands))
                return null;
            if (Dm.num(user.mob_size) <= Dm.num(mob_size))
            {
                to_chat(user, span_warning(Dm.text("[] is too big to pick up!", this)));
                return null;
            }
        }
        if (!Dm.truthy(user.get_empty_held_indexes()))
        {
            to_chat(user, span_warning("Your hands are full!"));
            return FALSE;
        }
        if (buckled != null)
        {
            to_chat(user, span_warning(Dm.text("[] is buckled to something!", this)));
            return FALSE;
        }
        if (!Dm.truthy(instant))
        {
            user.visible_message(span_warning(Dm.text("[] starts trying to scoop up []!", user, this)), span_danger(Dm.text("You start trying to scoop up []...", this)), null, null, this);
            to_chat(this, span_userdanger(Dm.text("[] starts trying to scoop you up!", user)));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
                return FALSE;
        }
        await mob_pickup(user);
        return TRUE;
    }

    // tg: code/modules/mob/living/living.dm:2133
    public virtual async DmTask<object> get_static_viruses()
    {
        // M2 P2: translated (an organ's removal reads it on every human's qdel, organ_movement.dm:213).
        if (!Dm.truthy(LAZYLEN(diseases)))
            return null;
        DmList result = Dm.list();
        foreach (datum_disease D in Dm.each<datum_disease>(diseases))
        {
            object static_virus = await D.Copy(); // gaps VI1
            Dm.append(result, static_virus);
        }
        return result;
    }

    // tg: code/modules/mob/living/living.dm:2142
    public override async DmTask<object> reset_perspective(atom new_eye = null, params object[] __extra)
    {
        // M8x slice C: a mob in a machine looks through it (the cryo cell's impaired fullscreen, cryo.dm:659)
        if (!Dm.truthy(await base.reset_perspective(new_eye, __extra)))
            return null;
        update_sight();
        update_fullscreen();
        update_pipe_vision();
        return null;
    }

    /// Proc used to handle the fullscreen overlay updates, realistically meant for the reset_perspective() proc.
    // tg: code/modules/mob/living/living.dm:2150
    public virtual void update_fullscreen()
    {
        if (Dm.truthy(client.eye) && client.eye != this)
        {
            atom client_eye = client.eye as atom;
            client_eye.get_remote_view_fullscreens(this);
        }
        else
            clear_fullscreen("remote_view", 0);
    }

    // tg: code/modules/mob/living/ventcrawling.dm:132
    public virtual void update_pipe_vision(float full_refresh = FALSE)
    {
        if (ai_controller != null && client == null) // we don't care about pipe vision if we have an AI controller with no client (typically means we are clientless).
            return;

        update_sight();

        // the pipe images and the lighting and pipecrawl plane masters' colours of a ventcrawler: ventcrawling is not ported
        Dm.Deferred("code/modules/mob/living/ventcrawling.dm:139", "update_pipe_vision: the ventcrawler's pipe images and plane masters (ventcrawling)");
    }

    // tg: code/modules/mob/living/living.dm:2297
    public virtual async DmTask move_to_error_room()
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2297", "move_to_error_room: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    /**
     * Changes the inclination angle of a mob, used by humans and others to differentiate between standing up and prone positions.
     *
     * In BYOND-angles 0 is NORTH, 90 is EAST, 180 is SOUTH and 270 is WEST.
     * This usually means that 0 is standing up, 90 and 270 are horizontal positions to right and left respectively, and 180 is upside-down.
     * Mobs that do now follow these conventions due to unusual sprites should require a special handling or redefinition of this proc, due to the density and layer changes.
     * The return of this proc is the previous value of the modified lying_angle if a change was successful (might include zero), or null if no change was made.
     */
    // tg: code/modules/mob/living/living.dm:2317
    public virtual async DmTask<object> set_lying_angle(object new_lying = null)
    {
        object dot = null;
        if (Dm.eq(new_lying, lying_angle))
            return null;
        dot = lying_angle;
        lying_angle = Dm.num(new_lying);
        if (lying_angle != lying_prev)
        {
            await update_transform();
            lying_prev = lying_angle;
        }
        return dot;
    }

    /**
     * add_body_temperature_change Adds modifications to the body temperature
     *
     * This collects all body temperature changes that the mob is experiencing to the list body_temp_changes
     * the aggrogate result is used to derive the new body temperature for the mob
     *
     * arguments:
     * * key_name (str) The unique key for this change, if it already exist it will be overridden
     * * amount (int) The amount of change from the base body temperature
     */
    // tg: code/modules/mob/living/living.dm:2337
    public virtual void add_body_temperature_change(object key_name = null, object amount = null)
    {
        body_temp_changes[Dm.text("[]", key_name)] = amount;
    }

    /**
     * remove_body_temperature_change Removes the modifications to the body temperature
     *
     * This removes the recorded change to body temperature from the body_temp_changes list
     *
     * arguments:
     * * key_name (str) The unique key for this change that will be removed
     */
    // tg: code/modules/mob/living/living.dm:2348
    public virtual void remove_body_temperature_change(object key_name = null)
    {
        body_temp_changes.Remove(key_name);
    }

    /**
     * get_body_temp_normal_change Returns the aggregate change to body temperature
     *
     * This aggregates all the changes in the body_temp_changes list and returns the result
     */
    // tg: code/modules/mob/living/living.dm:2356
    public virtual object get_body_temp_normal_change()
    {
        object total_change = 0;
        if (Dm.truthy(body_temp_changes.len))
        {
            foreach (object change in Dm.each(body_temp_changes))
                total_change = Dm.add(total_change, body_temp_changes[Dm.text("[]", change)]);
        }
        return total_change;
    }

    /**
     * get_body_temp_normal Returns the mobs normal body temperature with any modifications applied
     *
     * This applies the result from proc/get_body_temp_normal_change() against the BODYTEMP_NORMAL and returns the result
     *
     * arguments:
     * * apply_change (optional) Default True This applies the changes to body temperature normal
     */
    // tg: code/modules/mob/living/living.dm:2371
    public virtual object get_body_temp_normal(float? apply_change = null)
    {
        apply_change ??= TRUE;
        if (!Dm.truthy(apply_change))
            return BODYTEMP_NORMAL;
        return Dm.add(BODYTEMP_NORMAL, get_body_temp_normal_change());
    }

    ///Returns the body temperature at which this mob will start taking heat damage.
    // tg: code/modules/mob/living/living.dm:2377
    public virtual object get_body_temp_heat_damage_limit()
    {
        return BODYTEMP_HEAT_DAMAGE_LIMIT;
    }

    ///Returns the body temperature at which this mob will start taking cold damage.
    // tg: code/modules/mob/living/living.dm:2381
    public virtual object get_body_temp_cold_damage_limit()
    {
        return BODYTEMP_COLD_DAMAGE_LIMIT;
    }

    // tg: code/modules/mob/living/living.dm:2431
    public virtual object can_look_up()
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2431", "can_look_up: not in M2 (M4a/M5/M10+/M4b by area)");
        return default;
    }

    // tg: code/modules/mob/living/living.dm:2438
    public virtual async DmTask end_look()
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2438", "end_look: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:2446  (syndicate N1c: a camera eye's level change)
    public virtual void on_looking_z_level_change(turf old_loc = null, turf new_loc = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_LOOK_Z_CHANGE]))
            _SendSignal(COMSIG_LIVING_LOOK_Z_CHANGE, Dm.list(this, old_loc, new_loc));
    }

    // tg: code/modules/mob/living/living.dm:2455
    public virtual async DmTask look_up()
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2455", "look_up: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:2472
    public virtual object get_looking_turf(object direction = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2472", "get_looking_turf: not in M2 (M4a/M5/M10+/M4b by area)");
        return default;
    }

    // tg: code/modules/mob/living/living.dm:2498
    public virtual async DmTask look_down()
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2498", "look_down: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:2514
    public override async DmTask<object> set_stat(object new_stat = null)
    {
        object dot = null;
        dot = await base.set_stat(new_stat);
        if (dot == null || Dm.eq(dot, stat))
            return dot;

        // All the traits associated with any of a mob's stat
        // Adding any traits below should also be done in here
        DmList removed_traits = Dm.list(
            TRAIT_DEAF,
            TRAIT_FLOORED,
            TRAIT_HANDS_BLOCKED,
            TRAIT_INCAPACITATED,
            TRAIT_KNOCKEDOUT,
            TRAIT_FORCE_WHISPER
        );
        // All the traits associated with the mob's current stat
        DmList added_traits = Dm.list();

        switch (stat) // Current stat
        {
            case STABLE:
                log_combat(this, this, "left crit");
                break;

            case SOFT_CRIT:
                log_combat(this, this, "entered soft crit");
                added_traits.Add(
                    TRAIT_FLOORED,
                    TRAIT_HANDS_BLOCKED,
                    TRAIT_INCAPACITATED,
                    TRAIT_FORCE_WHISPER
                );
                break;

            case HARD_CRIT:
                log_combat(this, this, "entered hard crit");
                added_traits.Add(
                    TRAIT_DEAF,
                    TRAIT_FLOORED,
                    TRAIT_HANDS_BLOCKED,
                    TRAIT_INCAPACITATED,
                    TRAIT_KNOCKEDOUT
                );
                break;

            case DEAD:
                log_combat(this, this, "died");
                added_traits.Add(
                    TRAIT_DEAF,
                    TRAIT_FLOORED,
                    TRAIT_HANDS_BLOCKED,
                    TRAIT_INCAPACITATED,
                    TRAIT_KNOCKEDOUT
                );
                break;
        }

        if (stat == DEAD)
        {
            remove_from_alive_mob_list();
            add_to_dead_mob_list();
        }
        else if (Dm.eq(dot, DEAD))
        {
            remove_from_dead_mob_list();
            add_to_alive_mob_list();
        }

        add_traits(added_traits, STAT_TRAIT);
        remove_traits(Dm.sub(removed_traits, added_traits) as DmList, STAT_TRAIT);
        update_succumb_action();
        return dot;
    }

    // tg: code/modules/mob/living/living.dm:2577
    public virtual async DmTask<object> set_buckled(object new_buckled = null)
    {
        object dot = null;
        if (new_buckled == buckled)
            return dot;
        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_SET_BUCKLED]))
            this._SendSignal(COMSIG_LIVING_SET_BUCKLED, Dm.list(this, new_buckled));
        dot = buckled;
        buckled = (atom_movable)new_buckled;
        if (Dm.truthy(buckled))
        {
            if (!HAS_TRAIT(buckled, TRAIT_NO_IMMOBILIZE))
                ADD_TRAIT(this, TRAIT_IMMOBILIZED, BUCKLED_TRAIT);
            float sw__1 = buckled.buckle_lying;
            if (sw__1 == NO_BUCKLE_LYING) // The buckle doesn't force a lying angle.
                REMOVE_TRAIT(this, TRAIT_FLOORED, BUCKLED_TRAIT);
            else if (sw__1 == 0) // Forcing to a standing position.
            {
                REMOVE_TRAIT(this, TRAIT_FLOORED, BUCKLED_TRAIT);
                await set_body_position(STANDING_UP);
                await set_lying_angle(0);
            }
            else // Forcing to a lying position.
            {
                ADD_TRAIT(this, TRAIT_FLOORED, BUCKLED_TRAIT);
                await set_body_position(LYING_DOWN);
                await set_lying_angle(buckled.buckle_lying);
            }
        }
        else
        {
            remove_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_FLOORED), BUCKLED_TRAIT);
            if (Dm.truthy(dot)) // We unbuckled from something.
            {
                atom_movable old_buckled = (atom_movable)dot;
                if (old_buckled.buckle_lying == 0 && (Dm.truthy(resting) || HAS_TRAIT(this, TRAIT_FLOORED))) // The buckle forced us to stay up (like a chair)
                    await set_lying_down(); // We want to rest or are otherwise floored, so let's drop on the ground.
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/living.dm:2604
    public override object set_pulledby(object new_pulledby = null)
    {
        object dot = null;
        dot = base.set_pulledby(new_pulledby);
        update_incapacitated();
        return dot;
    }

    /// Updates the grab state of the mob and updates movespeed
    // tg: code/modules/mob/living/living.dm:2609
    public override async DmTask<object> setGrabState(object newstate = null)
    {
        object dot = null;
        dot = await base.setGrabState(newstate);
        switch (grab_state)
        {
            case GRAB_PASSIVE:
                remove_movespeed_modifier(MOVESPEED_ID_MOB_GRAB_STATE);
                break;
            case GRAB_AGGRESSIVE:
                await add_movespeed_modifier(typeof(datum_movespeed_modifier_grab_slowdown_aggressive));
                break;
            case GRAB_NECK:
                await add_movespeed_modifier(typeof(datum_movespeed_modifier_grab_slowdown_neck));
                break;
            case GRAB_KILL:
                await add_movespeed_modifier(typeof(datum_movespeed_modifier_grab_slowdown_kill));
                break;
        }
        return dot;
    }

    // tg: code/modules/mob/living/living.dm:2622
    /// Sprite to show for photocopying mob butts (gaps S3: the photocopier)
    public virtual object get_butt_sprite()
    {
        return null;
    }

    ///Proc to modify the value of num_legs and hook behavior associated to this event.
    // tg: code/modules/mob/living/living.dm:2626
    public virtual object set_num_legs(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(num_legs, new_value))
            return null;
        dot = num_legs;
        num_legs = Dm.num(new_value);
        hud_used?.update_locked_slots();
        return dot;
    }

    ///Proc to modify the value of usable_legs and hook behavior associated to this event.
    // tg: code/modules/mob/living/living.dm:2634
    public virtual async DmTask<object> set_usable_legs(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(usable_legs, new_value))
            return null;
        if (Dm.lt(new_value, 0)) // Sanity check
        {
            stack_trace(Dm.text("[] had set_usable_legs() called on them with a negative value!", this), "code/modules/mob/living/living.dm", 2638);
            new_value = 0;
        }

        dot = usable_legs;
        usable_legs = Dm.num(new_value);
        await update_usable_leg_status();
        return dot;
    }

    /**
     * Proc that updates the status of the mob's legs without setting its leg value to something else.
     */
    // tg: code/modules/mob/living/living.dm:2648
    public virtual async DmTask update_usable_leg_status()
    {

        if (usable_legs > 0) // Gained leg usage.
        {
            REMOVE_TRAIT(this, TRAIT_FLOORED, LACKING_LOCOMOTION_APPENDAGES_TRAIT);
            REMOVE_TRAIT(this, TRAIT_IMMOBILIZED, LACKING_LOCOMOTION_APPENDAGES_TRAIT);
        }
        else if (!Dm.truthy(Dm.bitand(movement_type, Dm.bitor(FLYING, FLOATING)))) //Lost leg usage, not flying.
        {
            if (!Dm.truthy(usable_legs))
            {
                ADD_TRAIT(this, TRAIT_FLOORED, LACKING_LOCOMOTION_APPENDAGES_TRAIT);
                if (!Dm.truthy(usable_hands))
                    ADD_TRAIT(this, TRAIT_IMMOBILIZED, LACKING_LOCOMOTION_APPENDAGES_TRAIT);
            }
        }

        if (usable_legs < default_num_legs)
        {
            float limbless_slowdown = (default_num_legs - usable_legs) * 3;
            if (!Dm.truthy(usable_legs) && usable_hands < default_num_hands)
                limbless_slowdown += (default_num_hands - usable_hands) * 3;
            DmList slowdown_mods = Dm.list();
            if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_LIMBLESS_SLOWDOWN]))
                _SendSignal(COMSIG_LIVING_LIMBLESS_SLOWDOWN, Dm.list(this, limbless_slowdown, slowdown_mods));
            foreach (object num in Dm.each(slowdown_mods))
                limbless_slowdown = Dm.num(Dm.mul(limbless_slowdown, num));
            if (limbless_slowdown == 0)
            {
                remove_movespeed_modifier(typeof(datum_movespeed_modifier_limbless));
                return;
            }
            await add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_limbless), multiplicative_slowdown: limbless_slowdown);
        }
        else
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_limbless));
    }

    ///Proc to modify the value of num_hands and hook behavior associated to this event.
    // tg: code/modules/mob/living/living.dm:2676
    public virtual object set_num_hands(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(num_hands, new_value))
            return null;
        dot = num_hands;
        num_hands = Dm.num(new_value);
        hud_used?.update_locked_slots();
        return dot;
    }

    ///Proc to modify the value of usable_hands and hook behavior associated to this event.
    // tg: code/modules/mob/living/living.dm:2684
    public virtual object set_usable_hands(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(usable_hands, new_value))
            return null;
        if (Dm.lt(new_value, 0)) // Sanity check
        {
            stack_trace(Dm.text("[] had set_usable_hands() called on them with a negative value!", this), "code/modules/mob/living/living.dm", 2688);
            new_value = 0;
        }
        dot = usable_hands;
        usable_hands = Dm.num(new_value);

        if (Dm.gt(new_value, dot)) // Gained hand usage.
            REMOVE_TRAIT(this, TRAIT_IMMOBILIZED, LACKING_LOCOMOTION_APPENDAGES_TRAIT);
        else if (!Dm.truthy(Dm.bitand(movement_type, Dm.bitor(FLYING, FLOATING))) && !Dm.truthy(usable_hands) && !Dm.truthy(usable_legs)) //Lost a hand, not flying, no hands left, no legs.
            ADD_TRAIT(this, TRAIT_IMMOBILIZED, LACKING_LOCOMOTION_APPENDAGES_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/living.dm:2706
    public virtual object will_escape_storage()
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2706", "will_escape_storage: not in M2 (M4a/M5/M10+/M4b by area)");
        return default;
    }

    /// Changes the value of the [living/body_position] variable. Call this before set_lying_angle()
    // tg: code/modules/mob/living/living.dm:2714
    public virtual async DmTask<object> set_body_position(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(body_position, new_value))
            return null;
        if ((Dm.eq(new_value, LYING_DOWN)) && !Dm.truthy(Dm.bitand(mobility_flags, MOBILITY_LIEDOWN)))
            return null;
        dot = body_position;
        body_position = Dm.num(new_value);
        if (body_position != LYING_DOWN)
            dm_turned_side = 0; // DEVIATION(DV686): the side the radial's Turn over put the body on is forgotten when it gets up
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_SET_BODY_POSITION]))
            _SendSignal(COMSIG_LIVING_SET_BODY_POSITION, Dm.list(this, new_value, dot));
        if (Dm.eq(new_value, LYING_DOWN)) // From standing to lying down.
            await on_lying_down();
        else // From lying down to standing up.
            await on_standing_up();
        update_rest_hud_icon();
        return dot;
    }

    /// Proc to append behavior to the condition of being floored. Called when the condition starts.
    // tg: code/modules/mob/living/living.dm:2730
    public virtual async DmTask on_floored_start()
    {
        await on_fall();
        if (body_position == STANDING_UP) //force them on the ground
        {
            await set_body_position(LYING_DOWN);
            await set_lying_angle(pick(LYING_ANGLE_EAST, LYING_ANGLE_WEST));
        }
    }

    /// Proc to append behavior to the condition of being floored. Called when the condition ends.
    // tg: code/modules/mob/living/living.dm:2737
    public virtual void on_floored_end()
    {
        if (!Dm.truthy(resting))
            get_up();
    }

    /// Proc to append behavior to the condition of being handsblocked. Called when the condition starts.
    // tg: code/modules/mob/living/living.dm:2743
    public virtual async DmTask on_handsblocked_start()
    {
        if (Dm.truthy(active_storage))
            active_storage.hide_contents(this);
        await drop_all_held_items();
        add_traits(Dm.list(TRAIT_UI_BLOCKED, TRAIT_PULL_BLOCKED), TRAIT_HANDS_BLOCKED);
    }

    /// Proc to append behavior to the condition of being handsblocked. Called when the condition ends.
    // tg: code/modules/mob/living/living.dm:2751
    public virtual void on_handsblocked_end()
    {
        remove_traits(Dm.list(TRAIT_UI_BLOCKED, TRAIT_PULL_BLOCKED), TRAIT_HANDS_BLOCKED);
    }

    /// Returns the attack damage type of a living mob such as [BRUTE].
    // tg: code/modules/mob/living/living.dm:2756
    public virtual object get_attack_type()
    {
        return BRUTE;
    }

    // tg: code/modules/mob/living/living.dm:2765
    public virtual object get_exp_list(object minutes = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2765", "get_exp_list: not in M2 (M4a/M5/M10+/M4b by area)");
        return default;
    }

    // tg: code/modules/mob/living/living.dm:2785
    public virtual async DmTask tram_slam_land()
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2785", "tram_slam_land: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:2803
    public virtual void ominous_nosebleed()
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2803", "ominous_nosebleed: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:2811
    public virtual object Write_Memory(object dead = null, object gibbed = null)
    {
        if (HAS_TRAIT(this, TRAIT_DONT_WRITE_MEMORY)) //always prevent data from being written.
            return FALSE;
        // for selective behaviors that may or may not prevent data from being written.
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_LIVING_WRITE_MEMORY]) ? NONE : _SendSignal(COMSIG_LIVING_WRITE_MEMORY, Dm.list(this, dead, gibbed)), COMPONENT_DONT_WRITE_MEMORY)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/living.dm:2821
    public virtual async DmTask admin_give_speech_impediment(mob admin = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2821", "admin_give_speech_impediment: an admin's speech impediment (the admin tools, M10+)");
    }

    // tg: code/modules/mob/living/living.dm:2842  (gaps MS1)
    public virtual async DmTask admin_add_mood_event(mob admin = null)
    {
        if (!Dm.truthy(admin) || !Dm.truthy(check_rights(NONE)))
            return;

        DmList mood_events = typesof(typeof(datum_mood_event));

        object chosen = await tgui_input_list(admin, I18n.T("What mood event?"), I18n.T("Add Mood Event"), mood_events);
        if (!Dm.truthy(chosen) || QDELETED(this) || !Dm.truthy(check_rights(NONE)))
            return;

        await mob_mood.add_mood_event(Dm.text("[]", rand(1, 50)), chosen);
    }

    // tg: code/modules/mob/living/living.dm:2854  (gaps MS1)
    public virtual async DmTask admin_remove_mood_event(mob admin = null)
    {
        if (!Dm.truthy(admin) || !Dm.truthy(check_rights(NONE)))
            return;

        DmList mood_events = Dm.list();
        foreach (object category in Dm.each(mob_mood.mood_events))
        {
            datum_mood_event @event = mob_mood.mood_events[category] as datum_mood_event;
            mood_events[@event] = category;
        }


        datum_mood_event chosen = await tgui_input_list(admin, I18n.T("What mood event?"), I18n.T("Remove Mood Event"), mood_events) as datum_mood_event;
        if (!Dm.truthy(chosen) || QDELETED(this) || !Dm.truthy(check_rights(NONE)))
            return;

        mob_mood.clear_mood_event(mood_events[chosen]);
    }

    /// Adds a mood event to the mob
    // tg: code/modules/mob/living/living.dm:2871  (gaps MS1)
    public virtual async DmTask add_mood_event(object category = null, object type = null, params object[] __extra)
    {
        if (QDELETED(mob_mood))
            return;

        DmList args = Dm.args(category, type, __extra);
        if (ispath(type, typeof(datum_mood_event_conditional)))
            await Dm.Call(mob_mood, nameof(datum_mood.add_conditional_mood_event), Dm.arglist(args));
        else
            await Dm.Call(mob_mood, nameof(datum_mood.add_mood_event), Dm.arglist(args));
    }

    /// Clears a mood event from the mob
    // tg: code/modules/mob/living/living.dm:2881  (gaps MS1)
    public virtual void clear_mood_event(object category = null)
    {
        if (QDELETED(mob_mood))
            return;
        mob_mood.clear_mood_event(category);
    }

    // tg: code/modules/mob/living/living.dm:2887  (gaps S4c: the arcade)
    public virtual void won_game()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_WON_VIDEOGAME]))
            _SendSignal(COMSIG_MOB_WON_VIDEOGAME, Dm.list(this));
    }

    // tg: code/modules/mob/living/living.dm:2892  (gaps S4c: the arcade)
    public virtual void lost_game()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_LOST_VIDEOGAME]))
            _SendSignal(COMSIG_MOB_LOST_VIDEOGAME, Dm.list(this));
    }

    // tg: code/modules/mob/living/living.dm:2897  (gaps S4c: the arcade)
    public virtual async DmTask played_game()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_PLAYED_VIDEOGAME]))
            _SendSignal(COMSIG_MOB_PLAYED_VIDEOGAME, Dm.list(this));
        await add_mood_event("gaming", typeof(datum_mood_event_gaming));
    }

    /**
     * Helper proc for basic and simple animals to return true if the passed sentience type matches theirs
     * Living doesn't have a sentience type though so it always returns false if not a basic or simple mob
     */
    // tg: code/modules/mob/living/living.dm:2906
    public virtual object compare_sentience_type(object compare_type = null) => FALSE;

    /// Proc called when TARGETED by a lazarus injector
    // tg: code/modules/mob/living/living.dm:2910  (gaps MD3: the lazarus injector)
    public virtual async DmTask lazarus_revive(mob_living reviver = null, object malfunctioning = null)
    {
        await revive(HEAL_ALL);
        add_faction(FACTION_NEUTRAL);
        if (!Dm.truthy(malfunctioning))
            await befriend(reviver);
        object lazarus_policy = Dm.truthy(get_policy(ROLE_LAZARUS_GOOD)) ? get_policy(ROLE_LAZARUS_GOOD) : I18n.T("The lazarus injector has brought you back to life! You are now friendly to everyone.");
        if (Dm.truthy(malfunctioning))
        {
            reviver.log_message(Dm.text("has revived mob [] with a malfunctioning lazarus injector.", key_name(this)), LOG_GAME);
            if (mind != null)
                await mind.enslave_mind_to_creator(reviver);
            to_chat(this, span_userdanger(I18n.T("Serve [], and assist [] in completing [] goals at any cost.", reviver.real_name, reviver.p_them(), reviver.p_their())));
            lazarus_policy = Dm.truthy(get_policy(ROLE_LAZARUS_BAD)) ? get_policy(ROLE_LAZARUS_BAD) : I18n.T("You have been revived by a malfunctioning lazarus injector! You are now enslaved by whoever revived you.");
        }
        to_chat(this, span_boldnotice(Dm.str(lazarus_policy)));
    }

    // tg: code/modules/mob/living/living.dm:2925
    public virtual async DmTask<object> befriend(mob_living new_friend = null)
    {
        if (Dm.truthy(new_friend._listen_lookup?[COMSIG_LIVING_MADE_NEW_FRIEND]))
            new_friend._SendSignal(COMSIG_LIVING_MADE_NEW_FRIEND, Dm.list(new_friend, this));
        if (QDELETED(new_friend))
            return null;
        object friend_ref = REF(new_friend);
        if (Dm.truthy(has_ally(friend_ref)))
            return FALSE;
        add_ally(friend_ref);
        (ai_controller as datum_ai_controller)?.insert_blackboard_key_lazylist(BB_FRIENDS_LIST, new_friend);

        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_BEFRIENDED]))
            _SendSignal(COMSIG_LIVING_BEFRIENDED, Dm.list(this, new_friend));
        return TRUE;
    }

    // tg: code/modules/mob/living/living.dm:2940
    public virtual object unfriend(mob_living old_friend = null)
    {
        object friend_ref = REF(old_friend);
        if (!Dm.truthy(has_ally(friend_ref)))
            return FALSE;
        remove_ally(friend_ref);
        (ai_controller as datum_ai_controller)?.remove_thing_from_blackboard_key(BB_FRIENDS_LIST, old_friend);

        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_UNFRIENDED]))
            _SendSignal(COMSIG_LIVING_UNFRIENDED, Dm.list(this, old_friend));
        return TRUE;
    }

    // tg: code/modules/mob/living/living.dm:2955
    // syndicate X1a (D316): the contractor's ransom (the cargo account's payment came with the economy, gaps P4a, the hostage on the black
    // market with the markets, gaps P4d)
    public virtual DmTask<object> process_capture(object ransom_price = null, object black_market_price = null)
    {
        if (Dm.num(ransom_price) > 0)
        {
            datum_bank_account cargo_account = SSeconomy.get_dep_account(ACCOUNT_CAR);

            if (cargo_account != null) //Just in case
                cargo_account.adjust_money(-min(Dm.num(ransom_price), cargo_account.account_balance)); //Not so much, especially for competent cargo. Plus this can't be mass-triggered like it has been done with contractors
            priority_announce("One of your crew was captured by a rival organisation - we've needed to pay their ransom to bring them back. As is policy we've taken a portion of the station's funds to offset the overall cost.", "Nanotrasen Asset Protection", has_important_message: TRUE);
        }

        ///The price should be high enough that the contractor can't just buy 'em back with their cut alone.
        var market_item = Dm.@new<datum_market_item_hostage>(this, Dm.truthy(black_market_price) ? black_market_price : ransom_price);
        ((datum_market)SSmarket.markets[typeof(datum_market_blackmarket)]).add_item(market_item);

        if (mind != null)
            ADD_TRAIT(mind, TRAIT_HAS_BEEN_KIDNAPPED, TRAIT_GENERIC);
        return DmTask<object>.FromResult(market_item);
    }

    // tg: code/modules/mob/living/living.dm:2972
    public virtual async DmTask admin_give_hallucination(mob admin = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2972", "admin_give_hallucination: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:2988
    public virtual async DmTask admin_give_delusion(mob admin = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:2988", "admin_give_delusion: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:3002
    public virtual async DmTask admin_give_guardian(mob admin = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:3002", "admin_give_guardian: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:3044
    public virtual async DmTask lookup()
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:3044", "lookup: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:3061
    public virtual async DmTask lookdown()
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:3061", "lookdown: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:3081
    public virtual object tally_physical_credits()
    {
        // Gaps package S2: the money a player carries, for the vending window (the money items live since the economy, gaps P4a)
        //Here is all the possible non-ID payment methods.
        DmList counted_money = Dm.list();
        float physical_cash_total = 0;
        foreach (obj_item credit in Dm.each_cast<obj_item>(typecache_filter_list(get_all_contents() as DmList, GLOB.allowed_money))) //Coins, cash, and credits.
        {
            physical_cash_total += Dm.num(credit.get_item_credit_value());
            Dm.append(ref counted_money, credit);
        }

        if (Dm.truthy(is_type_in_typecache(pulling, GLOB.allowed_money))) //Coins(Pulled).
        {
            obj_item counted_credit = (obj_item)pulling;
            physical_cash_total += Dm.num(counted_credit.get_item_credit_value());
            Dm.append(ref counted_money, counted_credit);
        }
        return round(physical_cash_total);
    }

    /// Returns an arbitrary number which very roughly correlates with how buff you look
    // tg: code/modules/mob/living/living.dm:3096  (gaps SL1, D333)
    public virtual object calculate_fitness()
    {
        object athletics_level = (mind?.get_skill_level(typeof(datum_skill_athletics)) is var or__1 && Dm.truthy(or__1) ? or__1 : 1);
        float damage = (Dm.num(melee_damage_lower) + Dm.num(melee_damage_upper)) / 2;

        return ceil(damage * (ceil(Dm.num(athletics_level) / 2)) * Dm.num(maxHealth));
    }

    /// Create a report string about how strong this person looks, generated in a somewhat arbitrary fashion
    // tg: code/modules/mob/living/living.dm:3103  (gaps SL1, D333)
    public virtual async DmTask<object> compare_fitness(mob_living scouter = null)
    {
        if (HAS_TRAIT(this, TRAIT_UNKNOWN_APPEARANCE))
            return span_warning(I18n.T("It's impossible to tell whether this person lifts."));

        object our_fitness_level = calculate_fitness();
        object their_fitness_level = scouter.calculate_fitness();

        object comparative_fitness = Dm.truthy(their_fitness_level) ? Dm.div(our_fitness_level, their_fitness_level) : 1;

        if (Dm.gt(comparative_fitness, 2))
        {
            await scouter.set_jitter_if_lower(Dm.num(comparative_fitness) * SECONDS);
            return I18n.T("[] []", span_notice(I18n.T("You'd estimate [] fitness level at about...", p_their())), span_boldwarning(I18n.T("What?!? []???", our_fitness_level)));
        }

        return span_notice(I18n.T("You'd estimate [] fitness level at about []. []", p_their(), our_fitness_level, Dm.le(comparative_fitness, 0.33f) ? I18n.T("Pathetic.") : ""));
    }

    // tg: code/modules/mob/living/living.dm:3119
    public virtual object bee_friendly() // gaps P2: the bees and the apiary
    {
        if (Dm.truthy(Dm.bitand(mob_biotypes, MOB_PLANT)))
            return TRUE;
        obj_item_clothing suit = get_item_by_slot(ITEM_SLOT_OCLOTHING) as obj_item_clothing;
        obj_item_clothing hat = get_item_by_slot(ITEM_SLOT_HEAD) as obj_item_clothing;
        if (suit == null || hat == null)
            return FALSE;
        if (Dm.truthy(Dm.bitand(Dm.bitand(suit.clothing_flags, hat.clothing_flags), THICKMATERIAL)))
            return TRUE;
        return null;
    }

    // tg: code/modules/mob/living/living.dm:3130
    public virtual async DmTask block_projectile_effects()
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:3130", "block_projectile_effects: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:3146
    public virtual async DmTask end_block_effects(object selected_overlay = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:3146", "end_block_effects: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    /// Returns the string form of the def_zone we have hit.
    // tg: code/modules/mob/living/living.dm:3152
    public virtual object check_hit_limb_zone_name(object hit_zone = null)
    {
        // The M7 projectiles slice
        if (Dm.truthy(has_limbs))
            return hit_zone;
        return null;
    }

    // tg: code/modules/mob/living/living.dm:3156
    public virtual void painful_scream(float? force = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living.dm:3156", "painful_scream: not in M2 (M4a/M5/M10+/M4b by area)");
    }

    // tg: code/modules/mob/living/living.dm:3161
    public virtual void set_pull_force(object new_pull_force = null)
    {
        if (Dm.eq(pull_force, new_pull_force))
            return;
        pull_force = Dm.num_or_null(new_pull_force);
        pull_force_change();
    }

    // tg: code/modules/mob/living/living.dm:3167
    public virtual void pull_force_change()
    {
        if (!Dm.truthy(pull_force) || HAS_TRAIT(this, TRAIT_PULL_BLOCKED))
        {
            // GAP(lead): remove_verb and add_verb (__HELPERS/verbs.dm) and /mob/living/verb/pulled are verbs, the player
            // command layer (rules 7.10, M9-M10); pulling is M9.
            Dm.Deferred("code/modules/mob/living/living.dm:3169", "pull_force_change: remove_verb(src, /mob/living/verb/pulled), the Pull verb (verbs, M10+)");
        }
        else
            Dm.Deferred("code/modules/mob/living/living.dm:3171", "pull_force_change: add_verb(src, /mob/living/verb/pulled), the Pull verb (verbs, M10+)");
    }

    // tg: code/modules/mob/living/living.dm:3174
    public virtual object get_generic_humanoid_static_appearance()
    {
        image static_image = image(Dm.resource("icons/effects/effects.dmi"), this, "static") as image;
        static_image.@override = TRUE;
        static_image.name = "unknown humanoid";

        return static_image;
    }
}

// tg: code/modules/mob/living/living.dm:626
public partial class mob : atom_movable
{
    // tg: code/modules/mob/living/living.dm:626
    public virtual object get_contents()
    {
        return null;
    }


    // tg: code/modules/mob/living/living.dm:1180
    public virtual async DmTask<object> resist_grab(object moving_resist = null)
    {
        return FALSE;
    }
}

// tg: code/modules/mob/living/living.dm:2384
[DmPath("/atom/movable/looking_holder")]
public partial class atom_movable_looking_holder : atom_movable
{
}

// tg: code/modules/mob/living/living.dm:1361 (M4a P2 at the lead's assignment: P1's chain asks it before every Ctrl, Alt,
// Alt-right and Ctrl-Shift click)
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living.dm:1361
    public override async DmTask<object> can_perform_action(atom target = null, object action_bitflags = null)
    {
        if (!(target is atom))
            throw Dm.CRASH("Missing target arg for can_perform_action", "code/modules/mob/living/living.dm", 1363);

        if (IS_UNCONSCIOUS_OR_CRIT(this))
        {
            to_chat(this, span_warning(I18n.T("You are not conscious enough for this action!")));
            return FALSE;
        }

        if (!Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_IGNORE_INCAPACITATED)))
        {
            float ignore_flags = NONE;
            if (Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_IGNORE_RESTRAINED)))
                ignore_flags = Dm.bitor(ignore_flags, INCAPABLE_RESTRAINTS);
            if (!Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_CHECK_GRAB)))
                ignore_flags = Dm.bitor(ignore_flags, INCAPABLE_GRAB);

            if (Dm.truthy(INCAPACITATED_IGNORING(this, ignore_flags)))
            {
                to_chat(this, span_warning(I18n.T("You are incapacitated at the moment!")));
                return FALSE;
            }
        }

        // If the MOBILITY_UI bitflag is not set it indicates the mob's hands are cutoff, blocked, or handcuffed
        // Note - AI's and borgs have the MOBILITY_UI bitflag set even though they don't have hands
        // Also if it is not set, the mob could be incapcitated, knocked out, unconscious, asleep, EMP'd, etc.
        if (!Dm.truthy(Dm.bitand(mobility_flags, MOBILITY_UI)) && !Dm.truthy(Dm.bitand(action_bitflags, ALLOW_RESTING)))
        {
            to_chat(this, span_warning(I18n.T("You don't have the mobility for this!")));
            return FALSE;
        }

        // NEED_HANDS is already checked by MOBILITY_UI for humans so this is for silicons
        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_HANDS)))
        {
            if (HAS_TRAIT(this, TRAIT_HANDS_BLOCKED))
            {
                to_chat(this, span_warning(I18n.T("You hands are blocked for this action!")));
                return FALSE;
            }
            if (!Dm.truthy(can_hold_items(target as obj_item))) // almost redundant if it weren't for mobs
            {
                to_chat(this, span_warning(I18n.T("You don't have the hands for this action!")));
                return FALSE;
            }
        }

        if (!Dm.truthy(Dm.bitand(action_bitflags, BYPASS_ADJACENCY)) && (Dm.truthy(Dm.bitand(action_bitflags, NOT_INSIDE_TARGET)) || recursive_loc_check__local(this, target) == null) && !Dm.truthy(await target.IsReachableBy(this)))
        {
            // HAS_SILICON_ACCESS(src) (__DEFINES/is_helpers.dm:178): GAP(lead): isAdminGhostAI (mob_helpers.dm:357) is not
            // declared (no admin ghosts in M4a); ispAI expanded inline.
            if (HAS_TRAIT(this, TRAIT_SILICON_ACCESS) && !istype(this, typeof(mob_living_silicon_pai)))
            {
                if (!Dm.truthy(Dm.bitand(action_bitflags, ALLOW_SILICON_REACH))) // silicons can ignore range checks (except pAIs)
                {
                    if (!Dm.truthy(Dm.bitand(action_bitflags, SILENT_ADJACENCY)))
                        to_chat(this, span_warning(I18n.T("You are too far away!")));
                    return FALSE;
                }
            }
            else // just a normal carbon mob
            {
                if (Dm.truthy(Dm.bitand(action_bitflags, FORBID_TELEKINESIS_REACH)))
                {
                    if (!Dm.truthy(Dm.bitand(action_bitflags, SILENT_ADJACENCY)))
                        to_chat(this, span_warning(I18n.T("You are too far away!")));
                    return FALSE;
                }

                datum_dna mob_DNA = has_dna() as datum_dna;
                if (mob_DNA == null || !Dm.truthy(mob_DNA.check_mutation(typeof(datum_mutation_telekinesis))) || !tkMaxRangeCheck__local(this, target))
                {
                    if (!Dm.truthy(Dm.bitand(action_bitflags, SILENT_ADJACENCY)))
                        to_chat(this, span_warning(I18n.T("You are too far away!")));
                    return FALSE;
                }
            }
        }

        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_VENTCRAWL)) && !HAS_TRAIT(this, TRAIT_VENTCRAWLER_NUDE) && !HAS_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS))
        {
            to_chat(this, span_warning(I18n.T("You wouldn't fit!")));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_DEXTERITY)) && !ISADVANCEDTOOLUSER(this))
        {
            to_chat(this, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_LITERACY)) && !Dm.truthy(is_literate()))
        {
            to_chat(this, span_warning(I18n.T("You can't comprehend any of this!")));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_LIGHT)) && !Dm.truthy(has_light_nearby()) && !Dm.truthy(has_nightvision()))
        {
            to_chat(this, span_warning(I18n.T("You need more light to do this!")));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_GRAVITY)) && !Dm.truthy(has_gravity()))
        {
            to_chat(this, span_warning(I18n.T("You need gravity to do this!")));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/__HELPERS/game.dm:214 /proc/recursive_loc_check, the branch for an atom `type` (the one can_perform_action
    // uses). GAP(lead): no GlobalProcs member yet: a local copy (I1 cleanup).
    private static atom recursive_loc_check__local(atom_movable target, atom type)
    {
        atom atom_to_find = target;
        if (atom_to_find == type)
            return atom_to_find;

        while (atom_to_find != type)
        {
            if (atom_to_find.loc == null)
                return null;
            atom_to_find = atom_to_find.loc;
        }
        return atom_to_find;
    }

    // tg: code/modules/mob/living/living.dm:1358
    public override object can_hold_items(obj_item I = null)
    {
        return (Dm.truthy(base.can_hold_items(I)) && HAS_TRAIT(this, TRAIT_CAN_HOLD_ITEMS) && Dm.truthy(usable_hands)) ? TRUE : FALSE;
    }

    // tg: code/_onclick/telekinesis.dm:214 /proc/tkMaxRangeCheck (TK_MAXRANGE, telekinesis.dm:7, is 15). GAP(lead): not
    // declared (telekinesis is M8): a local copy (I1 cleanup).
    private static bool tkMaxRangeCheck__local(mob user, atom target)
    {
        float d = get_dist(user, target);
        if (d > 15)
        {
            user.balloon_alert(user, I18n.T("can't TK, too far!"));
            return false;
        }
        return true;
    }
}

// tg: code/modules/mob/living/living.dm:1776  (M4b slice 3)
public partial class datum_controller_global_vars
{
    /// Global list that containes cached fire overlays for mobs
    // tg: code/modules/mob/living/living.dm:1776  GLOBAL_LIST_EMPTY(fire_appearances)
    public DmList fire_appearances;

    // tg: code/modules/mob/living/living.dm:1776  GLOBAL_LIST_EMPTY(fire_appearances): GLOBAL_MANAGED (__DEFINES/__globals.dm:5-9)
    public virtual void InitGlobalfire_appearances()
    {
        fire_appearances = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(fire_appearances));
    }
}

// tg: code/modules/mob/living/living.dm:1307 (interleaved types, rules 1.9; M5x slice 5)
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living.dm:1307
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (move_resist == INFINITY)
            return null;
        if (Dm.num(current_size) >= STAGE_SIX) //your puny magboots/wings/whatever will not save you against supermatter singularity
            await throw_at(singularity, 14, 3, this, TRUE);
        else if (!Dm.truthy(mob_negates_gravity()))
            await LocomotionSeam.PullOrStep(this, singularity); // DEVIATION(DV357)
        return null;
    }
}

public partial class mob_living : mob
{
    //Recursive function to find everything a mob is holding. Really shitty proc tbh.
    // tg: code/modules/mob/living/living.dm:781
    public override object get_contents()
    {
        DmList ret = Dm.list();
        Dm.combine(ref ret, contents); //add our contents
        foreach (atom iter_atom in Dm.each<atom>(ret)) //iterate storage objects
            Dm.combine(ref ret, iter_atom.atom_storage?.return_inv());
        foreach (obj_item_folder folder in Dm.each<obj_item_folder>(ret)) //very snowflakey-ly iterate folders
            Dm.combine(ref ret, folder.contents);
        return ret;
    }
}

// tg: code/modules/mob/living/living.dm:1094 (interleaved types, rules 1.9; gaps X5)
public partial class mob_living_carbon_alien_adult : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/living.dm:1094
    public override async DmTask lying_angle_on_movement(object direct = null)
    {
        return;
    }
}

// tg: code/modules/mob/living/living.dm:1472 (interleaved types, rules 1.9; gaps X5)
public partial class mob_living_carbon_alien : mob_living_carbon
{
    // tg: code/modules/mob/living/living.dm:1472
    public override async DmTask update_stamina()
    {
        return;
    }
}
