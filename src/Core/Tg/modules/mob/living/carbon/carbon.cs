// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/carbon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the M2 subset translated (M2 P2, scope.md 2e "carbon": Initialize, Destroy, the reagent holder, the body
// position, updatehealth, set_health, update_stat, revive, heal_and_revive, fully_heal, can_be_revived, the bodyparts,
// cmp_organ_slot_asc, can_resist, the bleeding totals, lying down and on_fall (D296), the blood type and nutrition; M2 F1: the name in
// fully_replace_character_name). The HUD procs
// (update_damage_hud, update_health_hud, carbon.dm:480-598) are M2 P3's partial; throw_impact, cuffs, vomit,
// VV come with M4a and M9 (their roots below stay Dm.NotTranslated; gaps SP1: tail wagging); M6 slice 2: the
// wounds of fully_heal and generate_fake_scars; M6 slice 3b: Topic (D154) but its paper note; M8 slice E: can_defib and
// its heart, brain and client checks. Gaps RG2: spew_organ (spewium), itch (itching powder) and the addictions in fully_heal.

// tg: code/modules/mob/living/carbon/carbon.dm:1
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon.dm:1
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        create_carbon_reagents();
        Waiting.NoSleep(update_body(TRUE)); // update_body(is_creating = TRUE)
        Dm.mask(ref living_flags, Dm.bitnot(STOP_OVERLAY_UPDATE_BODY_PARTS));

        register_context();

        Dm.append(ref GLOB.carbon_list, this);
        ADD_TRAIT(this, TRAIT_CAN_HOLD_ITEMS, INNATE_TRAIT); // Carbons are assumed to be innately capable of having arms, we check their arms count instead
        ADD_TRAIT(this, TRAIT_CAN_THROW_ITEMS, INNATE_TRAIT); // same here
        breathing_loop = Dm.@new<datum_looping_sound_breathing>(this, Dm.named("_direct", TRUE));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:14
    public override object Destroy(float? force = null)
    {
        object dot = null;
        //This must be done first, so the mob ghosts correctly before DNA etc is nulled
        dot = base.Destroy(force);

        Dm.combine(ref living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS);
        real_bodypart_cache.Cut();
        QDEL_LIST(hand_bodyparts);
        QDEL_LIST(organs);
        QDEL_LIST(bodyparts);
        QDEL_LIST(implants);
        foreach (object wound in Dm.each(all_wounds)) // these LAZYREMOVE themselves when deleted so no need to remove the list here
            qdel(wound);
        foreach (object scar in Dm.each(all_scars))
            qdel(scar);
        remove_from_all_data_huds();
        QDEL_NULL(ref dna);
        QDEL_NULL(ref breathing_loop);
        Dm.remove(ref GLOB.carbon_list, this);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:94
    public virtual object canBeHandcuffed()
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:97
    public virtual void create_carbon_reagents()
    {
        if (reagents != null)
            return;

        create_reagents(1000, REAGENT_HOLDER_ALIVE);
    }

    // tg: code/modules/mob/living/carbon/carbon_defense.dm:1  #define SHAKE_ANIMATION_OFFSET 4: carbon.dm reads it for the
    // tourniquet's removal (a file-local define of carbon_defense.dm, the port's copy here)
    private const int SHAKE_ANIMATION_OFFSET__carbon = 4;

    // tg: code/modules/mob/living/carbon/carbon.dm:103  (D154: a chat link reaches it through ClientSeam.Topic)
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        await base.Topic(href, href_list);
        if (Dm.truthy(href_list["embedded_object"]))
        {
            obj_item_bodypart limb = Dm.locate_ref(href_list["embedded_limb"]) is obj_item_bodypart located_limb && Dm.@in(located_limb, bodyparts) ? located_limb : null;
            if (limb == null)
                return null;
            obj_item weapon = Dm.locate_ref(href_list["embedded_object"]) is obj_item located_weapon && Dm.@in(located_weapon, limb.embedded_objects) ? located_weapon : null;
            if (weapon == null || weapon.loc != this) //no item, no limb, or item is not in limb or in the person anymore
                return null;
            await ((datum_embedding)weapon.get_embed()).rip_out(usr as mob_living);
            return null;
        }

        if (Dm.truthy(href_list["remove_tourniquet"]))
        {
            obj_item_bodypart limb = Dm.locate_ref(href_list["remove_tourniquet"]) is obj_item_bodypart located_limb && Dm.@in(located_limb, bodyparts) ? located_limb : null;
            mob_living patient = limb?.owner;
            obj_item tourniquet = LAZYACCESS(limb?.applied_items, LIMB_ITEM_TOURNIQUET) as obj_item;
            if (QDELETED(limb) || QDELETED(patient) || QDELETED(tourniquet))
                return null;
            balloon_alert_to_viewers(I18n.T("removing tourniquet..."));
            if (!Dm.truthy(await do_after(usr as atom_movable, 4 * SECONDS, target: this)))
                return null;
            if (QDELETED(limb) || QDELETED(patient) || QDELETED(tourniquet) || limb.owner != patient || tourniquet.loc != limb)
                return null;
            float direction = prob(50) ? -1 : 1;
            animate(this, Dm.named("pixel_w", SHAKE_ANIMATION_OFFSET__carbon * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_OUT)), Dm.named("flags", Dm.bitor(ANIMATION_PARALLEL, ANIMATION_RELATIVE)));
            animate(Dm.named("pixel_w", SHAKE_ANIMATION_OFFSET__carbon * -2 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_w", SHAKE_ANIMATION_OFFSET__carbon * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)), Dm.named("flags", ANIMATION_RELATIVE));

            balloon_alert_to_viewers(I18n.T("tourniquet removed"));
            await ((mob)usr).put_in_hands(tourniquet);
            return null;
        }

        if (Dm.truthy(href_list["gauze_limb"]))
        {
            obj_item_bodypart gauzed = Dm.locate_ref(href_list["gauze_limb"]) is obj_item_bodypart located_limb && Dm.@in(located_limb, bodyparts) ? located_limb : null;
            mob_living patient = gauzed?.owner;
            obj_item_stack_medical_wrap current_gauze = LAZYACCESS(gauzed?.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
            if (QDELETED(gauzed) || QDELETED(patient) || QDELETED(current_gauze))
                return null;
            // rest of the sanity is handled in the proc itself
            await gauzed.help_remove_gauze(usr as mob_living);
            return null;
        }

        if (Dm.truthy(href_list["show_paper_note"]))
        {
            // paper_note.show_through_camera(usr): paper (its UI) comes with its module (M9); the port's chat never carries this link
            Dm.NotTranslated("code/modules/mob/living/carbon/carbon.dm:141", "Topic: show_paper_note, the paper's show_through_camera (paper, M10+)");
        }
        return null;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:152  (M8y slice A: buckling)
    public override async DmTask resist_buckle()
    {
        if (!HAS_TRAIT(this, TRAIT_RESTRAINED))
        {
            await buckled.user_unbuckle_mob(this, this);
            return;
        }

        change_next_special_move(CLICK_CD_BREAKOUT);
        float buckle_cd = 1 * MINUTES;

        if (Dm.truthy(handcuffed))
            buckle_cd = Dm.num(handcuffed.breakouttime);

        visible_message(span_warning(I18n.T("[] attempts to unbuckle []self!", this, p_them())),
                    span_notice(I18n.T("You attempt to unbuckle yourself... (This will take around [] and you must stay still.)", DisplayTimeText(buckle_cd))));

        if (!Dm.truthy(await do_after(this, buckle_cd, target: this, timed_action_flags: IGNORE_HELD_ITEM, cog_icon: null)))
        {
            if (Dm.truthy(buckled))
                to_chat(this, span_warning(I18n.T("You fail to unbuckle yourself!")));
            return;
        }

        if (QDELETED(this) || buckled == null)
            return;

        await buckled.user_unbuckle_mob(this, this);
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:178  (M4b slice 3: stop, drop and roll)
    public override async DmTask<object> resist_fire()
    {
        return Dm.truthy(await apply_status_effect(typeof(datum_status_effect_stop_drop_roll))) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:181  (M7x slice E: restraints)
    public override async DmTask resist_restraints()
    {
        obj_item restraint = get_attached_restraint() as obj_item;
        if (Dm.truthy(restraint))
        {
            change_next_special_move(restraint.resist_cooldown);
            await cuff_resist(restraint);
        }
    }

    // Get FIRST restraint we are trying to resist from
    // tg: code/modules/mob/living/carbon/carbon.dm:188
    public virtual object get_attached_restraint()
    {
        DmList restraints = get_all_attached_restraints() as DmList;
        if (Dm.truthy(Dm.length(restraints)))
            return restraints[1];
        return null;
    }

    // Get ALL attached restraints we are trying to resist from
    // tg: code/modules/mob/living/carbon/carbon.dm:194
    public virtual object get_all_attached_restraints()
    {
        DmList dot = Dm.list();
        if (Dm.truthy(handcuffed))
            Dm.append(ref dot, handcuffed);
        if (Dm.truthy(legcuffed))
            Dm.append(ref dot, legcuffed);
        return dot;
    }

    /**
     * Helper to break the cuffs from hands
     * @param {obj/item} cuffs - The cuffs to break
     * @param {number} breakouttime - The time it takes to break the cuffs. Use SECONDS/MINUTES defines
     * @param {number} cuff_break - Speed multiplier, 0 is default, see _DEFINES\combat.dm
     */
    // tg: code/modules/mob/living/carbon/carbon.dm:207
    public virtual async DmTask<object> cuff_resist(obj_item cuffs = null, object breakouttime = null, float? cuff_break = null)
    {
        cuff_break ??= 0;
        object dot = null;
        if (cuff_break != INSTANT_CUFFBREAK && Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_MOB_REMOVING_CUFFS]) ? (object)NONE : _SendSignal(COMSIG_MOB_REMOVING_CUFFS, Dm.list(this, cuffs)), COMSIG_MOB_BLOCK_CUFF_REMOVAL)))
            return null; //The blocking object should sent a fluff-appropriate to_chat about cuff removal being blocked
        if (Dm.truthy(DOING_INTERACTION(this, REF(cuffs))))
        {
            to_chat(this, span_warning(I18n.T("You're already attempting to remove []!", cuffs)));
            return null;
        }

        if (breakouttime == null)
            breakouttime = cuffs.breakouttime;
        if (!Dm.truthy(cuff_break))
        {
            visible_message(span_warning(I18n.T("[] attempts to remove []!", this, cuffs)));
            to_chat(this, span_notice(I18n.T("You attempt to remove []... (This will take around [] and you need to stand still.)", cuffs, DisplayTimeText(breakouttime))));
            if (Dm.truthy(await do_after(this, breakouttime, target: this, timed_action_flags: IGNORE_HELD_ITEM, cog_icon: null, interaction_key: REF(cuffs))))
                dot = await clear_cuffs(cuffs, cuff_break);
            else
                to_chat(this, span_warning(I18n.T("You fail to remove []!", cuffs)));
        }
        else if (cuff_break == FAST_CUFFBREAK)
        {
            breakouttime = 5 * SECONDS;
            visible_message(span_warning(I18n.T("[] is trying to break []!", this, cuffs)));
            to_chat(this, span_notice(I18n.T("You attempt to break []... (This will take around 5 seconds and you need to stand still.)", cuffs)));
            if (Dm.truthy(await do_after(this, breakouttime, target: this, timed_action_flags: IGNORE_HELD_ITEM)))
                dot = await clear_cuffs(cuffs, cuff_break);
            else
                to_chat(this, span_warning(I18n.T("You fail to break []!", cuffs)));
        }
        else if (cuff_break == INSTANT_CUFFBREAK)
        {
            dot = await clear_cuffs(cuffs, cuff_break);
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:236
    public virtual async DmTask uncuff(float? break_strength = null)
    {
        break_strength ??= INFINITY;
        foreach (obj_item restraint in Dm.each_cast<obj_item>(get_all_attached_restraints()))
        {
            if (restraint.breakouttime >= break_strength)
                continue;
            await dropItemToGround(restraint, TRUE);
            changeNext_move(0);
        }
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:243
    public virtual async DmTask<object> clear_cuffs(obj_item I = null, object cuff_break = null)
    {
        if (!Dm.truthy(I.loc) || Dm.truthy(buckled))
            return FALSE;
        DmList all_restraints = get_all_attached_restraints() as DmList;
        if (!Dm.@in(I, all_restraints))
            return FALSE;
        visible_message(span_danger(I18n.T("[] manages to [] []!", this, Dm.truthy(cuff_break) ? "break" : "remove", I)));
        to_chat(this, span_notice(I18n.T("You successfully [] [].", Dm.truthy(cuff_break) ? "break" : "remove", I)));

        if (Dm.truthy(cuff_break))
        {
            qdel(I);
            return TRUE;
        }
        else
        {
            await dropItemToGround(I, TRUE);
            return TRUE;
        }
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:259
    public virtual async DmTask accident(obj_item I = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/carbon.dm:259", "accident: not in M2 (M4a items in hands)");
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:293
    public virtual async DmTask<object> vomit(float? vomit_flags = null, Type vomit_type = null, float? lost_nutrition = null, float? distance = null, float? purge_ratio = null)
    {
        vomit_flags ??= VOMIT_CATEGORY_DEFAULT;
        vomit_type ??= typeof(obj_effect_decal_cleanable_vomit_toxic);
        lost_nutrition ??= 10;
        distance ??= 1;
        purge_ratio ??= 0.1f;
        object force = Dm.bitand(vomit_flags, MOB_VOMIT_FORCE);
        if ((HAS_TRAIT(this, TRAIT_NOHUNGER) || HAS_TRAIT(this, TRAIT_TOXINLOVER)) && !Dm.truthy(force))
            return FALSE;

        if (!Dm.truthy(force) && HAS_TRAIT(this, TRAIT_STRONG_STOMACH))
            lost_nutrition *= 0.5f;

        if (Dm.truthy(_listen_lookup?[COMSIG_CARBON_VOMITED]))
            _SendSignal(COMSIG_CARBON_VOMITED, Dm.list(this, distance, force));

        // cache some stuff that we'll need later (at least multiple times)
        float starting_dir = dir;
        object message = Dm.bitand(vomit_flags, MOB_VOMIT_MESSAGE);
        object stun = Dm.bitand(vomit_flags, MOB_VOMIT_STUN);
        object knockdown = Dm.bitand(vomit_flags, MOB_VOMIT_KNOCKDOWN);
        object blood = Dm.bitand(vomit_flags, MOB_VOMIT_BLOOD);

        if (!Dm.truthy(force) && !Dm.truthy(blood) && (nutrition < 100))
        {
            if (Dm.truthy(message))
            {
                visible_message(
                    span_warning(I18n.T("[] dry heaves!", this)),
                    span_userdanger(I18n.T("You try to throw up, but there's nothing in your stomach!"))
                );
            }
            if (Dm.truthy(stun))
            {
                float stun_time = 20 * SECONDS;
                if (HAS_TRAIT(this, TRAIT_STRONG_STOMACH))
                    stun_time *= 0.5f;
                await Stun(stun_time);
            }
            if (Dm.truthy(knockdown))
                await Knockdown(20 * SECONDS);
            return TRUE;
        }

        if (Dm.truthy(is_mouth_covered())) //make this add a blood/vomit overlay later it'll be hilarious
        {
            if (Dm.truthy(message))
            {
                visible_message(span_danger(I18n.T("[] throws up all over []self!", this, p_them())), span_userdanger(I18n.T("You throw up all over yourself!")));
                await add_mood_event("vomit", typeof(datum_mood_event_vomitself));
            }
            distance = 0;
        }
        else if (Dm.truthy(message))
        {
            visible_message(
                span_danger(I18n.T("[] throws up!", this)),
                span_userdanger(I18n.T("You throw up!"))
            );
            if (!HAS_TRAIT(this, TRAIT_VOMIT_SLURPER))
                await add_mood_event("vomit", typeof(datum_mood_event_vomit));
        }

        if (Dm.truthy(stun))
        {
            float stun_time = 8 * SECONDS;
            if (!Dm.truthy(blood) && HAS_TRAIT(this, TRAIT_STRONG_STOMACH))
                stun_time *= 0.5f;
            await Stun(stun_time);
        }
        if (Dm.truthy(knockdown))
            await Knockdown(8 * SECONDS);

        playsound(this, Dm.resource("sound/effects/splat.ogg"), 50, TRUE); // tg: code/modules/mob/living/carbon/carbon.dm:349

        object need_mob_update = FALSE;
        turf location = get_turf(this);
        if (!Dm.truthy(blood))
        {
            await adjust_nutrition(-lost_nutrition);
            need_mob_update = Dm.add(need_mob_update, await adjust_tox_loss(-3, updating_health: FALSE));
        }

        for (float i = 0; i <= Dm.num(distance); i++)
        {
            if (Dm.truthy(blood))
            {
                if (location != null)
                    await add_splatter_floor(location);
                if (Dm.truthy(Dm.bitand(vomit_flags, MOB_VOMIT_HARM)))
                    need_mob_update = Dm.add(need_mob_update, await adjust_brute_loss(3, updating_health: FALSE));
            }
            else
            {
                if (location != null)
                    await location.add_vomit_floor(this, vomit_type, vomit_flags, purge_ratio); // call purge when doing detoxicfication to pump more chems out of the stomach.
            }

            location = get_step(location, starting_dir);
            if (location != null && Dm.truthy(await location.is_blocked_turf()))
                break;
        }
        if (Dm.truthy(need_mob_update)) // so we only have to call updatehealth() once as opposed to n times
            await updatehealth();

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:385
    public virtual async DmTask expel_ingested(atom bite = null, object amount = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/carbon.dm:385", "expel_ingested: not in M2 (M8 reagents)");
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:393
    public virtual async DmTask spew_organ(float? power = null, float? amt = null)
    {
        // Gaps RG2 (D339): spewium's overdose.
        power ??= 5;
        amt ??= 1;
        for (float i = 1; i <= amt; i++)
        {
            if (!Dm.truthy(Dm.length(organs)))
                break; //Guess we're out of organs!
            obj_item_organ guts = pick(organs) as obj_item_organ;
            turf T = get_turf(this);
            await guts.Remove(this);
            await guts.forceMove(T);
            atom throw_target = get_edge_target_turf(guts, dir) as atom;
            await guts.throw_at(throw_target, power, 4, this);
        }
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:405
    public override async DmTask<object> fully_replace_character_name(object oldname = null, object newname = null, float? log_new_name = null)
    {
        log_new_name ??= FALSE;
        object dot = null;
        dot = await base.fully_replace_character_name(oldname, newname, log_new_name);
        if (!Dm.truthy(dot))
            return dot;

        dna?.real_name = real_name;
        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        my_head?.real_name = (string)real_name;
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:415
    public override async DmTask<object> set_body_position(object new_value = null)
    {
        object dot = null;
        dot = await base.set_body_position(new_value);
        if (dot == null)
            return dot;
        if (Dm.eq(new_value, LYING_DOWN))
            await add_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling));
        else
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling));
        return dot;
    }

    //Updates the mob's health from bodyparts and mob damage variables
    // tg: code/modules/mob/living/carbon/carbon.dm:426
    public override async DmTask updatehealth()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;
        float total_burn = 0;
        float total_brute = 0;
        foreach (obj_item_bodypart BP in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            total_brute += (BP.brute_dam * BP.body_damage_coeff);
            total_burn += (BP.burn_dam * BP.body_damage_coeff);
        }
        set_health(round(maxHealth - Dm.num(get_oxy_loss()) - Dm.num(get_tox_loss()) - total_burn - total_brute, DAMAGE_PRECISION));
        update_stat();
        await update_stamina();

        /// The amount of burn damage needed to be done for this mob to be husked
        float husk_threshold = ((obj_item_bodypart)get_bodypart(BODY_ZONE_CHEST)).max_damage * -1;

        if (((maxHealth - total_burn) < husk_threshold) && stat == DEAD)
            become_husk(BURN);
        med_hud_set_health();
        if (stat == SOFT_CRIT)
            await add_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_softcrit));
        else
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_softcrit));
        // SEND_SIGNAL(src, COMSIG_LIVING_HEALTH_UPDATE) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_HEALTH_UPDATE]))
            this._SendSignal(COMSIG_LIVING_HEALTH_UPDATE, Dm.list(this));
    }

    /**
     * Calculates how visually impaired the mob is by their equipment and other factors
     *
     * This is where clothing adds its various vision limiting effects, such as welding helmets
     */
    // tg: code/modules/mob/living/carbon/carbon.dm:455
    public virtual async DmTask update_tint()
    {
        float tint = 0;
        foreach (obj_item_clothing worn_item in Dm.each<obj_item_clothing>(get_equipped_items(INCLUDE_ABSTRACT)))
            tint += worn_item.tint;

        obj_item_organ_eyes eyes = get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes != null)
            tint += eyes.tint;

        if (tint >= TINT_BLIND)
            await become_blind(EYES_COVERED);

        else if (tint >= TINT_DARKENED)
        {
            cure_blind(EYES_COVERED);
            overlay_fullscreen("tint", typeof(atom_movable_screen_fullscreen_impaired), 2);
        }

        else if (tint >= TINT_MILD)
        {
            cure_blind(EYES_COVERED);
            overlay_fullscreen("tint", typeof(atom_movable_screen_fullscreen_impaired), 1);
        }

        else
        {
            cure_blind(EYES_COVERED);
            clear_fullscreen("tint", 0 * SECONDS);
        }
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:600
    public override object set_health(object new_value = null)
    {
        object dot = null;
        dot = base.set_health(new_value);
        // GAP(lead): CONFIG_GET(flag/near_death_experience) reads /datum/config_entry/flag/near_death_experience
        // (controllers/configuration/entries/game_options.dm), which has no mirror; a flag entry's default is FALSE.
        object near_death_experience__gap = null;
        if (Dm.truthy(near_death_experience__gap))
        {
            if (Dm.gt(dot, HEALTH_THRESHOLD_NEARDEATH))
            {
                if (health <= HEALTH_THRESHOLD_NEARDEATH && !HAS_TRAIT(this, TRAIT_NODEATH))
                    ADD_TRAIT(this, TRAIT_SIXTHSENSE, "near-death");
            }
            else if (health > HEALTH_THRESHOLD_NEARDEATH)
                REMOVE_TRAIT(this, TRAIT_SIXTHSENSE, "near-death");
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:610
    public override void update_stat()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;
        if (stat != DEAD)
        {
            if (health <= dead_threshold && !HAS_TRAIT(this, TRAIT_NODEATH))
            {
                Waiting.NoSleep(death());
                return;
            }
            if (health <= hardcrit_threshold && !HAS_TRAIT(this, TRAIT_NOHARDCRIT))
                Waiting.NoSleep(set_stat(HARD_CRIT));
            else if (health <= crit_threshold && !HAS_TRAIT(this, TRAIT_NOSOFTCRIT))
                Waiting.NoSleep(set_stat(SOFT_CRIT));
            else
                Waiting.NoSleep(set_stat(STABLE));
        }
        update_damage_hud();
        update_health_hud();
        update_stamina_hud();
        med_hud_set_status();
    }

    //called when we get cuffed/uncuffed
    // tg: code/modules/mob/living/carbon/carbon.dm:630
    public virtual async DmTask update_handcuffed()
    {
        if (Dm.truthy(handcuffed))
        {
            await drop_all_held_items();
            await stop_pulling();
            throw_alert(ALERT_HANDCUFFED, typeof(atom_movable_screen_alert_restrained_handcuffed), new_master: handcuffed);
            await add_mood_event("handcuffed", typeof(datum_mood_event_handcuffed));
        }
        else
        {
            clear_alert(ALERT_HANDCUFFED);
            clear_mood_event("handcuffed");
        }
        await update_mob_action_buttons(); //some of our action buttons might be unusable when we're handcuffed.
        update_worn_handcuffs();
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:642
    public override async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        full_heal_flags ??= NONE;
        excess_healing ??= 0;
        force_grab_ghost ??= FALSE;
        if (Dm.truthy(excess_healing))
        {
            adjust_blood_volume(Dm.num(excess_healing) * 2);

            foreach (obj_item_organ target_organ in Dm.each_cast<obj_item_organ>(organs))
            {
                if (!Dm.truthy(target_organ.damage))
                    continue;

                await target_organ.apply_organ_damage(Dm.num(excess_healing) * -1, required_organ_flag: ORGAN_ORGANIC); //1 excess = 5 organ damage healed
            }
        }

        return await base.revive(full_heal_flags, excess_healing, force_grab_ghost);
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:654
    public override async DmTask<object> heal_and_revive(float? heal_to = null, object revive_message = null)
    {
        heal_to ??= 75;
        object dot = null;
        // We can't heal them if they're missing a heart
        if (Dm.truthy(needs_heart()) && !Dm.truthy(get_organ_slot(ORGAN_SLOT_HEART)))
            return FALSE;

        // We can't heal them if they're missing their lungs
        if (!HAS_TRAIT(this, TRAIT_NOBREATH) && !((dna == null ? null : ((datum_species)dna.species).mutantlungs) == null) && !Dm.truthy(get_organ_slot(ORGAN_SLOT_LUNGS)))
            return FALSE;

        // And we can't heal them if they're missing their liver
        if (!HAS_TRAIT(this, TRAIT_LIVERLESS_METABOLISM) && !((dna == null ? null : ((datum_species)dna.species).mutantliver) == null) && !Dm.truthy(get_organ_slot(ORGAN_SLOT_LIVER)))
            return FALSE;

        dot = await base.heal_and_revive(heal_to, revive_message);
        if (Dm.truthy(dot)) // if revived successfully
            set_heartattack(FALSE);

        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:673
    public override async DmTask fully_heal(float? heal_flags = null)
    {
        heal_flags ??= HEAL_ALL;

        // Should be handled via signal on embedded, or via heal on bodypart
        // Otherwise I don't care to give it a separate flag
        await remove_all_embedded_objects();

        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_NEGATIVE_DISEASES)))
        {
            foreach (datum_disease disease in Dm.each_cast<datum_disease>(diseases))
            {
                if (!Dm.eq(disease.severity, DISEASE_SEVERITY_POSITIVE))
                    await disease.cure(FALSE); // gaps VI1
            }
        }

        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_WOUNDS)))
        {
            foreach (datum_wound wound in Dm.each_cast<datum_wound>(all_wounds))
                await wound.remove_wound();
        }

        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_LIMBS)))
        {
            await regenerate_limbs();
            foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(get_bodyparts(include_stumps: TRUE)))
                await limb.remove_surgical_state(ALL);
        }

        if (Dm.truthy(Dm.bitand(heal_flags, Dm.bitor(HEAL_REFRESH_ORGANS, HEAL_ORGANS))))
            await regenerate_organs(remove_hazardous: Dm.not(Dm.not(Dm.bitand(heal_flags, HEAL_REFRESH_ORGANS))));

        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_TRAUMAS)))
        {
            cure_all_traumas(TRAUMA_RESILIENCE_MAGIC);
            // Addictions are like traumas
            foreach (object addiction_type in Dm.each(GLOB.addictions))
                mind?.remove_addiction_points(addiction_type, MAX_ADDICTION_POINTS); //Remove the addiction!
        }

        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_RESTRAINTS)))
        {
            QDEL_NULL(ref handcuffed);
            QDEL_NULL(ref legcuffed);
        }

        await base.fully_heal(heal_flags);
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:708
    public override async DmTask do_strange_reagent_revival(object healing_amount = null)
    {
        set_heartattack(FALSE);
        await base.do_strange_reagent_revival(healing_amount);
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:712
    public override object can_be_revived()
    {
        // GAP(lead): /datum/status_effect/zombie (species_types/zombies.dm:47) is not declared; its id is "zombified" (:48),
        // the value /datum/status_effect/zombie::id reads.
        if (HAS_TRAIT_NOT_FROM(this, TRAIT_HUSK, "zombified"))
            return FALSE;
        if (!HAS_TRAIT(this, TRAIT_BRAINLESS_CARBON) && !Dm.truthy(get_organ_by_type(typeof(obj_item_organ_brain))))
            return FALSE;
        return base.can_be_revived();
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:719
    [ShouldBePure]
    public virtual object can_defib()
    {
        if (HAS_TRAIT(this, TRAIT_SUICIDED))
            return DEFIB_FAIL_SUICIDE;

        if (HAS_TRAIT(this, TRAIT_HUSK))
            return DEFIB_FAIL_HUSK;

        if (IS_FAKE_KEY(key))
            return DEFIB_NOGRAB_AGHOST;

        if (HAS_TRAIT(this, TRAIT_DEFIB_BLACKLISTED))
            return DEFIB_FAIL_BLACKLISTED;

        if ((Dm.num(get_brute_loss()) >= MAX_REVIVE_BRUTE_DAMAGE) || (Dm.num(get_fire_loss()) >= MAX_REVIVE_FIRE_DAMAGE))
            return DEFIB_FAIL_TISSUE_DAMAGE;

        object heart_status = (!Dm.truthy(_listen_lookup?[COMSIG_CARBON_DEFIB_HEART_CHECK]) ? (object)NONE : _SendSignal(COMSIG_CARBON_DEFIB_HEART_CHECK, Dm.list(this))) is var h && Dm.truthy(h)
            ? h
            : can_defib_heart(get_organ_by_type(typeof(obj_item_organ_heart)) as obj_item_organ_heart);
        if (Dm.truthy(heart_status))
            return heart_status;

        object brain_status = (!Dm.truthy(_listen_lookup?[COMSIG_CARBON_DEFIB_BRAIN_CHECK]) ? (object)NONE : _SendSignal(COMSIG_CARBON_DEFIB_BRAIN_CHECK, Dm.list(this))) is var b && Dm.truthy(b)
            ? b
            : can_defib_brain(get_organ_by_type(typeof(obj_item_organ_brain)) as obj_item_organ_brain);
        if (Dm.truthy(brain_status))
            return brain_status;

        return DEFIB_POSSIBLE;
    }

    /// Return a defib status based on the heart organ provided
    // tg: code/modules/mob/living/carbon/carbon.dm:747
    public virtual object can_defib_heart(obj_item_organ_heart heart_organ = null)
    {
        if (!Dm.truthy(needs_heart()))
            return NONE;

        if (QDELETED(heart_organ))
            return DEFIB_FAIL_NO_HEART;

        if (Dm.truthy(Dm.bitand(heart_organ.organ_flags, ORGAN_FAILING)))
            return DEFIB_FAIL_FAILING_HEART;

        return NONE;
    }

    /// Return a defib status based on the brain organ provided
    // tg: code/modules/mob/living/carbon/carbon.dm:760
    public virtual object can_defib_brain(obj_item_organ_brain brain_organ = null)
    {
        if (QDELETED(brain_organ))
            return DEFIB_FAIL_NO_BRAIN;

        if (Dm.truthy(Dm.bitand(brain_organ.organ_flags, ORGAN_FAILING)))
            return DEFIB_FAIL_FAILING_BRAIN;

        if (Dm.truthy(brain_organ.suicided) || (brain_organ.brainmob != null && HAS_TRAIT(brain_organ.brainmob, TRAIT_SUICIDED)))
            return DEFIB_FAIL_NO_INTELLIGENCE;

        return NONE;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:772
    public virtual object can_defib_client()
    {
        return (HAS_TRAIT(this, TRAIT_MIND_TEMPORARILY_GONE) || client != null || Dm.truthy(get_ghost(FALSE, TRUE))) && Dm.truthy(Dm.bitand(can_defib(), DEFIB_REVIVABLE_STATES)) ? TRUE : FALSE;
    }

    /// Creates body parts for this carbon completely from scratch.
    /// Optionally takes a map of body zones to what type to instantiate instead of them.
    // tg: code/modules/mob/living/carbon/carbon.dm:790
    public virtual async DmTask create_bodyparts(DmList overrides = null)
    {
        DmList bodyparts_paths = (overrides?.Copy() is var or__1 && Dm.truthy(or__1) ? or__1 : bodyparts.Copy());
        bodyparts = Dm.list();
        foreach (Type bodypart_path in Dm.each_cast<Type>(bodyparts_paths))
        {
            object real_body_part_path = (overrides?[Dm.initial(bodypart_path, (obj_item_bodypart p) => p.body_zone)] is var or__2 && Dm.truthy(or__2) ? or__2 : bodypart_path);
            obj_item_bodypart bodypart_instance = await Dm.new_async(real_body_part_path as Type) as obj_item_bodypart;
            await bodypart_instance.try_attach_limb(this, FALSE, TRUE);
        }

        bodyparts = sort_list(bodyparts, "/proc/" + nameof(GlobalProcs.cmp_bodypart_by_body_part_asc)) as DmList;
    }

    /// Called when a new hand is added
    // tg: code/modules/mob/living/carbon/carbon.dm:801
    public virtual void on_added_hand(obj_item_bodypart_arm new_hand = null, object hand_index = null)
    {
        if (Dm.gt(hand_index, hand_bodyparts.len))
            hand_bodyparts.len = Dm.num(hand_index);
        hand_bodyparts[hand_index] = new_hand;
    }

    /// Cleans up references to a hand when it is dismembered or deleted
    // tg: code/modules/mob/living/carbon/carbon.dm:807
    public virtual void on_lost_hand(obj_item_bodypart_arm lost_hand = null)
    {
        hand_bodyparts[lost_hand.held_index] = null;
    }

    ///Proc to hook behavior on bodypart additions. Do not directly call. You're looking for [/obj/item/bodypart/proc/try_attach_limb()].
    // tg: code/modules/mob/living/carbon/carbon.dm:811
    [ShouldNotOverride]
    public virtual async DmTask add_bodypart(obj_item_bodypart new_bodypart = null, object special = null, object lazy = null)
    {
        new_bodypart.on_adding(this);
        Dm.append(ref bodyparts, new_bodypart);
        if (!Dm.truthy(IS_STUMP(new_bodypart)))
            real_bodypart_cache[new_bodypart.body_zone] = new_bodypart;
        await new_bodypart.update_owner(this);

        // Apply a bodypart effect or merge with an existing one, for stuff like plant limbs regenning in light
        foreach (Type effect_type in Dm.each_cast<Type>(new_bodypart.bodypart_effects))
            await apply_status_effect(effect_type, type, new_bodypart, special, lazy);

        // Tell the organs in the bodyparts that we are in a mob again
        foreach (obj_item_organ organ in Dm.each<obj_item_organ>(new_bodypart))
            await organ.mob_insert(this);

        object sw__1 = new_bodypart.body_part;
        if (Dm.eq(sw__1, LEG_LEFT) || Dm.eq(sw__1, LEG_RIGHT))
        {
            if (!Dm.truthy(IS_STUMP(new_bodypart)))
            {
                set_num_legs(num_legs + 1);
                if (!Dm.truthy(new_bodypart.bodypart_disabled))
                    await set_usable_legs(usable_legs + 1);
            }
        }
        else if (Dm.eq(sw__1, ARM_LEFT) || Dm.eq(sw__1, ARM_RIGHT))
        {
            if (!Dm.truthy(IS_STUMP(new_bodypart)))
            {
                set_num_hands(num_hands + 1);
                if (!Dm.truthy(new_bodypart.bodypart_disabled))
                    set_usable_hands(usable_hands + 1);
            }
        }

        synchronize_bodytypes();
        synchronize_bodyshapes();
    }

    ///Proc to hook behavior on bodypart removals.  Do not directly call. You're looking for [/obj/item/bodypart/proc/drop_limb()].
    // tg: code/modules/mob/living/carbon/carbon.dm:844
    [ShouldNotOverride]
    public virtual async DmTask remove_bodypart(obj_item_bodypart old_bodypart = null, object special = null)
    {
        if (Dm.truthy(special))
        {
            foreach (obj_item_organ organ in Dm.each<obj_item_organ>(old_bodypart))
                await organ.bodypart_remove(limb_owner: this, movement_flags: NO_ID_TRANSFER);
        }
        else
        {
            foreach (obj_item_organ organ in Dm.each<obj_item_organ>(old_bodypart))
                await organ.mob_remove(this, Dm.num_or_null(special));
        }

        await old_bodypart.on_removal(this);
        Dm.remove(ref bodyparts, old_bodypart);
        Dm.remove(ref real_bodypart_cache, old_bodypart.body_zone);

        object sw__1 = old_bodypart.body_part;
        if (Dm.eq(sw__1, LEG_LEFT) || Dm.eq(sw__1, LEG_RIGHT))
        {
            if (!Dm.truthy(IS_STUMP(old_bodypart)))
            {
                set_num_legs(num_legs - 1);
                if (!Dm.truthy(old_bodypart.bodypart_disabled))
                    await set_usable_legs(usable_legs - 1);
                // DEVIATION(DV572): the bloody feet go with a leg
                (GetComponent(typeof(datum_component_bloodysoles_feet)) as datum_component_bloodysoles_feet)?.update_icon();
            }
        }
        else if (Dm.eq(sw__1, ARM_LEFT) || Dm.eq(sw__1, ARM_RIGHT))
        {
            if (!Dm.truthy(IS_STUMP(old_bodypart)))
            {
                set_num_hands(num_hands - 1);
                if (!Dm.truthy(old_bodypart.bodypart_disabled))
                    set_usable_hands(usable_hands - 1);
            }
        }

        if (!Dm.truthy(special) && old_bodypart.stump_typepath != null)
        {
            if (old_bodypart.type == old_bodypart.stump_typepath)
                stack_trace("Attempted to replace a stump with a stump", "code/modules/mob/living/carbon/carbon.dm", 870);
            else
            {
                obj_item_bodypart stump = await Dm.new_async(old_bodypart.stump_typepath) as obj_item_bodypart;
                stump.bodyshape = old_bodypart.bodyshape;
                stump.bodytype = old_bodypart.bodytype;
                await stump.add_biostate(Dm.bitand(old_bodypart.biological_state, Dm.bitnot(BIO_JOINTED)));
                if (!Dm.truthy(await stump.try_attach_limb(this, special: TRUE)))
                    // the only way this can happen is if the stump is rejected via signal
                    // not much we can do about that besides hope they know what they're doing
                    qdel(stump);
            }
        }

        synchronize_bodytypes();
        synchronize_bodyshapes();
    }

    ///Updates the bodypart speed modifier based on our bodyparts.
    // tg: code/modules/mob/living/carbon/carbon.dm:887
    public virtual async DmTask update_bodypart_speed_modifier()
    {
        float final_modification = 0;
        foreach (obj_item_bodypart_leg bodypart in Dm.each<obj_item_bodypart_leg>(get_bodyparts()))
            final_modification += Dm.num(bodypart.speed_modifier);
        // GAP(P1): /datum/movespeed_modifier/bodypart (modules/movespeed/modifiers/innate.dm:14) is not declared. Not on the
        // M2 path: a human's legs have speed_modifier 0, so nothing calls this proc (default_parts.dm:440-454).
        Dm.NotTranslated("code/modules/mob/living/carbon/carbon.dm:891", "update_bodypart_speed_modifier: add_or_update_variable_movespeed_modifier(/datum/movespeed_modifier/bodypart, update = TRUE, multiplicative_slowdown = final_modification)");
    }
}

// tg: code/modules/mob/living/carbon/carbon.dm:893 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/carbon/carbon.dm:893
    public static object cmp_organ_slot_asc(object slot_a = null, object slot_b = null)
    {
        return GLOB.organ_process_order.Find(slot_a) - GLOB.organ_process_order.Find(slot_b);
    }
}

// tg: code/modules/mob/living/carbon/carbon.dm:896 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon.dm:896
    public virtual object get_footprint_sprite()
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/carbon.dm:896", "get_footprint_sprite: not in M2 (M3 footprints)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:1021
    public override object can_resist()
    {
        return (Dm.num(bodyparts.len > 2) is var and__1 && !Dm.truthy(and__1) ? (object)and__1 : base.can_resist());
    }

    /// if any of our bodyparts are bleeding
    // tg: code/modules/mob/living/carbon/carbon.dm:1042
    public virtual object is_bleeding()
    {
        if (!Dm.truthy(CAN_HAVE_BLOOD(this)))
            return FALSE;
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            if (Dm.truthy(part.cached_bleed_rate))
                return TRUE;
        }
        return null;
    }

    /// get our total bleedrate
    // tg: code/modules/mob/living/carbon/carbon.dm:1050
    public virtual object get_total_bleed_rate()
    {
        if (!Dm.truthy(CAN_HAVE_BLOOD(this)))
            return FALSE;

        float total_bleed_rate = 0;
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
            total_bleed_rate += part.cached_bleed_rate;

        return total_bleed_rate;
    }

    /**
     * generate_fake_scars()- for when you want to scar someone, but you don't want to hurt them first. These scars don't count for temporal scarring (hence, fake)
     *
     * If you want a specific wound scar, pass that wound type as the second arg, otherwise you can pass a list like WOUND_LIST_SLASH to generate a random cut scar.
     *
     * Arguments:
     * * num_scars- A number for how many scars you want to add
     * * forced_type- Which wound or category of wounds you want to choose from, WOUND_LIST_BLUNT, WOUND_LIST_SLASH, or WOUND_LIST_BURN (or some combination). If passed a list, picks randomly from the listed wounds. Defaults to all 3 types
     */
    // tg: code/modules/mob/living/carbon/carbon.dm:1069
    public virtual async DmTask generate_fake_scars(object num_scars = null, object forced_type = null)
    {
        for (float i = 1; i <= Dm.num(num_scars); i++)
        {
            datum_scar scaries = Dm.@new<datum_scar>();
            obj_item_bodypart scar_part = pick(bodyparts) as obj_item_bodypart;

            object wound_type = null;
            if (Dm.truthy(forced_type))
            {
                if (islist(forced_type))
                    wound_type = pick(forced_type as DmList);
                else
                    wound_type = forced_type;
            }
            else
            {
                foreach (Type path in Dm.each_cast<Type>(shuffle(GLOB.all_wound_pregen_data)))
                {
                    datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[path];
                    if (Dm.truthy(pregen_data.can_be_applied_to(scar_part, random_roll: TRUE)))
                    {
                        wound_type = path;
                        break;
                    }
                }
            }

            if (Dm.truthy(wound_type)) // can feasibly happen, if its an inorganic limb/cant be wounded/scarred
            {
                datum_wound phantom_wound = (datum_wound)await Dm.new_async(wound_type as Type);
                scaries.generate(scar_part, phantom_wound);
                scaries.fake = TRUE;
                QDEL_NULL(ref phantom_wound);
            }
        }
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:1094
    /// Returns whether or not the carbon should be able to be shocked
    public virtual object should_electrocute(object power_source = null)
    {
        if (ismecha(loc))
            return FALSE;

        if (!Dm.truthy(get_powernet_info_from_source(power_source)))
            return FALSE;

        if (HAS_TRAIT(this, TRAIT_SHOCKIMMUNE))
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:1107
    public virtual async DmTask adjust_skillchip_complexity_modifier(object delta = null)
    {
        skillchip_complexity_modifier += Dm.num(delta);

        obj_item_organ_brain brain = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;

        if (brain == null)
            return;

        await brain.update_skillchips();
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:1119
    public virtual async DmTask<object> set_handcuffed(object new_value = null)
    {
        if (handcuffed == new_value)
            return FALSE;
        object dot = handcuffed;
        handcuffed = new_value as obj_item;
        if (Dm.truthy(dot))
        {
            if (!Dm.truthy(handcuffed))
                REMOVE_TRAIT(this, TRAIT_RESTRAINED, HANDCUFFED_TRAIT);
        }
        else if (Dm.truthy(handcuffed))
        {
            ADD_TRAIT(this, TRAIT_RESTRAINED, HANDCUFFED_TRAIT);
        }
        await update_handcuffed();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:148
    public override async DmTask on_fall()
    {
        await base.on_fall();
        if (loc != null)
            await loc.handle_fall(this); //it's loc so it doesn't call the mob's handle_fall which does nothing
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:1131
    public override async DmTask on_lying_down(object new_lying_angle = null)
    {
        await base.on_lying_down(new_lying_angle);
        if (buckled == null || (buckled.buckle_lying != 0 && buckled.buckle_lying != NO_BUCKLE_LYING))
            await lying_angle_on_lying_down(new_lying_angle);
    }

    /// Special carbon interaction on lying down, to transform its sprite by a rotation.
    // tg: code/modules/mob/living/carbon/carbon.dm:1138
    public virtual async DmTask lying_angle_on_lying_down(object new_lying_angle = null)
    {
        if (Dm.truthy(new_lying_angle))
            await set_lying_angle(new_lying_angle);
        else if (buckled != null && buckled.buckle_lying != NO_BUCKLE_LYING)
            await set_lying_angle(buckled.buckle_lying);
        else
            await set_lying_angle(pick(LYING_ANGLE_EAST, LYING_ANGLE_WEST));
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:1167
    public virtual async DmTask attach_rot(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
            return;
        if (!Dm.truthy(Dm.bitand(mob_biotypes, Dm.bitor(MOB_ORGANIC, MOB_UNDEAD))))
            return;
        await AddComponent(typeof(datum_component_rot), 6 * MINUTES, 10 * MINUTES, 1); // gaps VI1
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:1184
    public virtual object can_touch_acid(atom acided_atom = null, object acid_power = null, object acid_volume = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/carbon.dm:1184", "can_touch_acid: not in M2 (M5 acid)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:1195
    public virtual object can_touch_burning(atom burning_atom = null, object acid_power = null, object acid_volume = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/carbon.dm:1195", "can_touch_burning: not in M2 (M4b fire)");
        return default;
    }

    /// Goes through the organs and bodyparts of the mob and updates their blood_dna_info, in case their blood type has changed (via set_species() or otherwise)
    // tg: code/modules/mob/living/carbon/carbon.dm:1204
    public virtual void update_cached_blood_dna_info()
    {
        DmList blood_dna_info = get_blood_dna_list() as DmList;
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(organs))
            organ.blood_dna_info = blood_dna_info;
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
            bodypart.blood_dna_info = blood_dna_info;
    }

    /// Setter for changing a mob's blood type
    // tg: code/modules/mob/living/carbon/carbon.dm:1212
    [ShouldCallParent]
    public virtual void set_blood_type(object new_blood_type = null, float? update_cached_blood_dna_info = null)
    {
        update_cached_blood_dna_info ??= TRUE;

        if (dna == null)
            return;

        if (istext(new_blood_type) || ispath(new_blood_type, typeof(datum_blood_type)))
            new_blood_type = get_blood_type(new_blood_type);
        if (!(new_blood_type is datum_blood_type))
            return;

        if (Dm.eq(get_bloodtype(), new_blood_type)) // already has this blood type, we don't need to do anything.
            return;

        dna.blood_type = new_blood_type as datum_blood_type;
        if (Dm.truthy(update_cached_blood_dna_info))
            this.update_cached_blood_dna_info();
        // SEND_SIGNAL(src, COMSIG_CARBON_CHANGED_BLOOD_TYPE, new_blood_type, update_cached_blood_dna_info) (rules 11.1 (e))
        if (Dm.truthy(this._listen_lookup?[COMSIG_CARBON_CHANGED_BLOOD_TYPE]))
            this._SendSignal(COMSIG_CARBON_CHANGED_BLOOD_TYPE, Dm.list(this, new_blood_type, update_cached_blood_dna_info));
    }

    /// Helper to cleanly trigger tail wagging
    /// Accepts an optional timeout after which we remove the tail wagging
    /// Returns true if successful, false otherwise
    // tg: code/modules/mob/living/carbon/carbon.dm:1239
    public virtual async DmTask<object> wag_tail(float? timeout = null)
    {
        timeout ??= INFINITY;
        obj_item_organ_tail wagged = get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
        if (wagged == null)
            return FALSE;
        return await wagged.start_wag(this, timeout);
    }

    /// Helper to cleanly stop all tail wagging
    /// Returns true if successful, false otherwise
    // tg: code/modules/mob/living/carbon/carbon.dm:1247
    public virtual async DmTask<object> unwag_tail() // can't unwag a tail
    {
        obj_item_organ_tail unwagged = get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
        if (unwagged == null)
            return FALSE;
        return await unwagged.stop_wag(this);
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:1271  (the M7 projectiles slice)
    public override object check_hit_limb_zone_name(object hit_zone = null)
    {
        if (Dm.truthy(get_bodypart((string)hit_zone)))
            return hit_zone;
        // When a limb is missing the damage is actually passed to the chest
        return BODY_ZONE_CHEST;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:1277
    public override object get_bloodtype()
    {
        if (!Dm.truthy(CAN_HAVE_BLOOD(this)))
            return null;
        return dna?.blood_type;
    }

    // tg: code/modules/mob/living/carbon/carbon.dm:1283
    public override async DmTask update_nutrition()
    {
        await base.update_nutrition();
        // Force a weight update in case we're stasis'd and don't tick
        if (HAS_TRAIT_FROM(this, TRAIT_FAT, OBESITY))
        {
            if (overeatduration >= OVEREAT_TIME_LIMIT)
                return;

            to_chat(this, span_notice(I18n.T("You feel fit again!")));
            remove_traits(Dm.list(TRAIT_FAT, TRAIT_OFF_BALANCE_TACKLER), OBESITY);
            return;
        }

        if (overeatduration >= OVEREAT_TIME_LIMIT)
        {
            to_chat(this, span_danger(I18n.T("You suddenly feel blubbery!")));
            add_traits(Dm.list(TRAIT_FAT, TRAIT_OFF_BALANCE_TACKLER), OBESITY);
        }
    }
}

// tg: code/modules/mob/living/carbon/carbon.dm:33 (M4a P2: the hitby subsets, at the lead's assignment)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon.dm:33  (waitfor = FALSE inherited from /atom/movable/throw_impact; this
    // definition sleeps itself (Paralyze, take_bodypart_damage), so it has the wrapper of waiting-model.md 5.4)
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(throw_impact__carbon(hit_atom, throwingdatum, __extra, dot), dot);
    }

    private async DmTask<object> throw_impact__carbon(atom hit_atom, datum_thrownthing throwingdatum, object[] __extra, DotBox<object> dot)
    {
        dot.Value = base.throw_impact(hit_atom, throwingdatum, __extra);
        float hurt = TRUE;
        float extra_speed = 0;
        float oof_noise = FALSE; //We smacked something with denisty, so play a noise
        mob thrower = throwingdatum?.get_thrower() as mob;
        if (thrower != this)
            extra_speed = min(max(0, Dm.num(throwingdatum.speed) - Dm.num(Dm.initial(this, (atom_movable p) => p.throw_speed))), CARBON_MAX_IMPACT_SPEED_BONUS);

        if (throwingdatum is datum_thrownthing)
            hurt = Dm.num(!Dm.truthy(throwingdatum.gentle));
        if (Dm.truthy(hurt) && Dm.truthy(hit_atom.density))
        {
            if (isturf(hit_atom))
            {
                await Paralyze(2 * SECONDS);
                await take_bodypart_damage(10 + 5 * extra_speed, check_armor: TRUE, wound_bonus: extra_speed * 5);
            }
            else if (istype(hit_atom, typeof(obj_structure)) && Dm.truthy(extra_speed)) // isstructure, inline
            {
                await Paralyze(1 * SECONDS);
                await take_bodypart_damage(5 + 5 * extra_speed, check_armor: TRUE, wound_bonus: extra_speed * 5);
            }
            else if (!iscarbon(hit_atom) && Dm.truthy(extra_speed))
                await take_bodypart_damage(5 * extra_speed, check_armor: TRUE, wound_bonus: extra_speed * 5);
            visible_message(span_danger(I18n.T("[] crashes into [][]!", this, hit_atom, Dm.truthy(extra_speed) ? I18n.T(" really hard") : "")),
                span_userdanger(I18n.T("You violently crash into [][]!", hit_atom, Dm.truthy(extra_speed) ? I18n.T(" extra hard") : "")));
            log_combat(hit_atom, this, "crashes ");
            oof_noise = TRUE;
        }

        if (iscarbon(hit_atom) && hit_atom != this)
        {
            mob_living_carbon victim = hit_atom as mob_living_carbon;
            float blocked = FALSE;
            if (Dm.truthy(Dm.bitand(victim.movement_type, FLYING)))
                return dot.Value;
            if (!Dm.truthy(hurt))
                return dot.Value;

            if (Dm.eq(dot.Value, SUCCESSFUL_BLOCK) || Dm.truthy(await victim.check_block(this, 0, Dm.text("[]", name), LEAP_ATTACK)))
                blocked = TRUE;

            await take_bodypart_damage(10 + 5 * extra_speed, check_armor: TRUE, wound_bonus: extra_speed * 5);
            await Paralyze(2 * SECONDS);
            oof_noise = TRUE;

            if (Dm.truthy(blocked))
            {
                visible_message(span_danger(I18n.T("[] crashes into [][], but [] blocked the worst of it!", this, victim, Dm.truthy(extra_speed) ? I18n.T(" really hard") : "", victim)),
                    span_userdanger(I18n.T("You violently crash into [][], but [] managed to block the worst of it!", victim, Dm.truthy(extra_speed) ? I18n.T(" extra hard") : "", victim)));
                log_combat(this, victim, "crashed into and was blocked by");
                return dot.Value;
            }
            else if (HAS_TRAIT(victim, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED))
            {
                await victim.take_bodypart_damage(10 + 5 * extra_speed, check_armor: TRUE, wound_bonus: extra_speed * 5);
                await victim.apply_damage(10 + 10 * extra_speed, STAMINA);
                await victim.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 2, 10 * SECONDS);
                visible_message(span_danger(I18n.T("[] crashes into [][], but [] was able to stay on their feet!", this, victim, Dm.truthy(extra_speed) ? I18n.T(" really hard") : "", victim)),
                    span_userdanger(I18n.T("You violently crash into [][], but [] managed to stay on their feet!", victim, Dm.truthy(extra_speed) ? I18n.T(" extra hard") : "", victim)));
            }
            else
            {
                await victim.Paralyze(2 * SECONDS);
                await victim.take_bodypart_damage(10 + 5 * extra_speed, check_armor: TRUE, wound_bonus: extra_speed * 5);
                visible_message(span_danger(I18n.T("[] crashes into [][], knocking them both over!", this, victim, Dm.truthy(extra_speed) ? I18n.T(" really hard") : "")),
                    span_userdanger(I18n.T("You violently crash into [][]!", victim, Dm.truthy(extra_speed) ? I18n.T(" extra hard") : "")));
            }
            log_combat(this, victim, "crashed into");
        }

        if (Dm.truthy(oof_noise))
        {
            playsound(this, Dm.resource("sound/items/weapons/punch1.ogg"), 50, TRUE); // tg: code/modules/mob/living/carbon/carbon.dm:92
        }
        return dot.Value;
    }
}

// Gaps RG2 (D339): itching powder scratches a random organic limb.
// tg: code/modules/mob/living/carbon/carbon.dm:1253
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon.dm:1253
    public override async DmTask<object> itch(obj_item_bodypart target_part = null, float? damage = null, float? can_scratch = null, float? silent = null)
    {
        if (target_part == null)
            target_part = get_bodypart(Dm.str(get_random_valid_zone(even_weights: TRUE))) as obj_item_bodypart;
        if (!Dm.truthy(IS_ORGANIC_LIMB(target_part)) || Dm.truthy(Dm.bitand(target_part.bodypart_flags, BODYPART_PSEUDOPART)))
            return FALSE;
        return await base.itch(target_part, damage, can_scratch, silent);
    }
}
