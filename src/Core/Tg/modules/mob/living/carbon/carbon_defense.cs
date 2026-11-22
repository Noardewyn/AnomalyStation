// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/carbon_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with its module (a later milestone). Holds the [DmPath] of a type that an M2 slot signature or
// var block names, in the earliest .dme file that declares it (rules 2.3); written by the M2 contracts step
// (docs/ai/specs/m2-ownership.md), and the root of help_shake_act (M2 P2: the species' help click calls it). M6 slice 4: the
// carbon's attack message (send_item_attack_message, with the wound details) and dismembering_strike. The M7 shocks slice:
// electrocute_act and secondary_shock. The M7 delamination slice: bioscramble and init_bioscrambler_lists. M2 P2 translates
// the oxygen loss overrides and check_passout (:481-509: oxygen loss knocks a carbon out at OXYLOSS_PASSOUT_THRESHOLD).
// M8 slice B: is_pepper_proof (condensed capsaicin). M8x slice A: get_organic_health (the cryo cell).

// tg: code/modules/mob/living/carbon/carbon_defense.dm:59 (M6 slice 4: the attack message with the wound details)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:59
    public override object send_item_attack_message(obj_item weapon = null, mob_living user = null, object hit_area = null, object def_zone = null)
    {
        // In the future replace these with parent call if the item attack message proc is ever unshittified
        if (Dm.truthy(Dm.bitand((!Dm.truthy(weapon._listen_lookup?[COMSIG_SEND_ITEM_ATTACK_MESSAGE_OBJECT]) ? (object)NONE : weapon._SendSignal(COMSIG_SEND_ITEM_ATTACK_MESSAGE_OBJECT, Dm.list(weapon, this, user))), SIGNAL_MESSAGE_MODIFIED)))
            return TRUE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_SEND_ITEM_ATTACK_MESSAGE_CARBON]) ? (object)NONE : this._SendSignal(COMSIG_SEND_ITEM_ATTACK_MESSAGE_CARBON, Dm.list(this, weapon, user))), SIGNAL_MESSAGE_MODIFIED)))
            return TRUE;

        if (!Dm.truthy(weapon.force) && !Dm.truthy(Dm.length(weapon.attack_verb_simple)) && !Dm.truthy(Dm.length(weapon.attack_verb_continuous)))
            return null;
        obj_item_bodypart hit_bodypart = get_bodypart(def_zone as string) as obj_item_bodypart;
        if (hit_bodypart == null) // ??
            return base.send_item_attack_message(weapon, user, hit_area, def_zone);

        // Sanity in case one is null for some reason
        float picked_index = rand(Dm.max(Dm.length(weapon.attack_verb_simple), Dm.length(weapon.attack_verb_continuous)));

        object message_verb_continuous = "attacks";
        object message_verb_simple = "attack";
        object message_hit_area = get_hit_area_message(hit_area);
        // Sanity in case one is... longer than the other?
        if (Dm.truthy(picked_index) && Dm.length(weapon.attack_verb_continuous) >= picked_index)
            message_verb_continuous = weapon.attack_verb_continuous[picked_index];
        if (Dm.truthy(picked_index) && Dm.length(weapon.attack_verb_simple) >= picked_index)
            message_verb_simple = weapon.attack_verb_simple[picked_index];

        string extra_wound_details = "";

        if (Dm.eq(weapon.damtype, BRUTE) && Dm.truthy(hit_bodypart.can_dismember__proc()))
        {
            float has_exterior = Dm.bitand(hit_bodypart.bio_status, ANATOMY_EXTERIOR);
            float has_interior = Dm.bitand(hit_bodypart.bio_status, ANATOMY_INTERIOR);

            bool exterior_ready_to_dismember = (!Dm.truthy(has_exterior) || Dm.truthy(Dm.bitand(hit_bodypart.mangled_state, BODYPART_MANGLED_EXTERIOR)));
            bool interior_ready_to_dismember = (!Dm.truthy(has_interior) || Dm.truthy(Dm.bitand(hit_bodypart.mangled_state, BODYPART_MANGLED_INTERIOR)));

            if ((exterior_ready_to_dismember && interior_ready_to_dismember) || Dm.truthy(hit_bodypart.dismemberable_by_total_damage()))
                extra_wound_details = I18n.T(", threatening to sever it entirely");
            else if (Dm.truthy(has_interior) && (Dm.truthy(has_exterior) && exterior_ready_to_dismember) && Dm.truthy(weapon.get_sharpness()))
            {
                object bone_text = hit_bodypart.get_internal_description();
                extra_wound_details = I18n.T(", [] through to the []", Dm.eq(weapon.get_sharpness(), SHARP_EDGED) ? I18n.T("slicing") : I18n.T("piercing"), bone_text);
            }
            else if (Dm.truthy(has_exterior) && ((Dm.truthy(has_interior) && interior_ready_to_dismember) && Dm.truthy(weapon.get_sharpness())))
            {
                object tissue_text = hit_bodypart.get_external_description();
                extra_wound_details = I18n.T(", [] at the remaining []", Dm.eq(weapon.get_sharpness(), SHARP_EDGED) ? I18n.T("slicing") : I18n.T("piercing"), tissue_text);
            }
        }

        string attack_message_spectator = I18n.T("[] [][] with [][]!", this, message_verb_continuous, message_hit_area, weapon, extra_wound_details);
        string attack_message_victim = I18n.T("You're [][] with [][]!", message_verb_continuous, message_hit_area, weapon, extra_wound_details);
        string attack_message_attacker = I18n.T("You [] [][] with [][]!", message_verb_simple, this, message_hit_area, weapon, extra_wound_details);
        if (Dm.@in(user, viewers(this, null)))
        {
            attack_message_spectator = I18n.T("[] [] [][] with [][]!", user, message_verb_continuous, this, message_hit_area, weapon, extra_wound_details);
            attack_message_victim = I18n.T("[] [] you[] with [][]!", user, message_verb_continuous, message_hit_area, weapon, extra_wound_details);
        }
        if (user == this)
            attack_message_victim = I18n.T("You [] yourself[] with [][]!", message_verb_simple, message_hit_area, weapon, extra_wound_details);
        visible_message(span_danger(I18n.T("[]", attack_message_spectator)),
            span_userdanger(I18n.T("[]", attack_message_victim)), null, COMBAT_MESSAGE_RANGE, user);
        if (user != this)
            to_chat(user, span_danger(I18n.T("[]", attack_message_attacker)));
        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:186 (M6 slice 4)
public partial class mob_living : mob
{
    /**
     * Attempt to dismember a limb of the mob, as an attack from a limb destroyer (a mob with limb_destroyer)
     *
     * Unless the attacker is an NPC, in which case it disregards the zone and picks a random one
     *
     * Cannot dismember heads
     *
     * Returns a falsy value (null) on success, and a truthy value (the hit zone) on failure
     */
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:186
    public virtual async DmTask<object> dismembering_strike(mob_living attacker = null, object dam_zone = null)
    {
        return dam_zone;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:189 (M6 slice 4)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:189
    public override async DmTask<object> dismembering_strike(mob_living attacker = null, object dam_zone = null)
    {
        if (!Dm.truthy(attacker.limb_destroyer))
            return dam_zone;
        obj_item_bodypart affecting = null;
        if (Dm.truthy(dam_zone) && attacker.client != null)
            affecting = get_bodypart(get_random_valid_zone(dam_zone) as string) as obj_item_bodypart;
        else
        {
            DmList things_to_ruin = shuffle(bodyparts.Copy()) as DmList;
            foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(things_to_ruin))
            {
                if (Dm.eq(bodypart.body_zone, BODY_ZONE_HEAD) || Dm.eq(bodypart.body_zone, BODY_ZONE_CHEST))
                    continue;
                if (affecting == null || ((Dm.num(affecting.get_damage()) / affecting.max_damage) < (Dm.num(bodypart.get_damage()) / bodypart.max_damage)))
                    affecting = bodypart;
            }
        }

        if (affecting != null)
        {
            dam_zone = affecting.body_zone;
            if (Dm.num(affecting.get_damage()) >= affecting.max_damage && Dm.truthy(await affecting.dismember()))
                return null;
        }

        return dam_zone;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:218 (the M7 shocks slice)
public partial class mob_living_carbon : mob_living
{
    ///Adds to the parent by also adding functionality to propagate shocks through pulling and doing some fluff effects.
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:218
    [DmParams("shock_damage", "source", "siemens_coeff", "flags", "jitter_time", "stutter_time", "stun_duration")]
    public override async DmTask<object> electrocute_act(object shock_damage = null, object source = null, float? siemens_coeff = null, float? flags = null, params object[] __extra)
    {
        float? jitter_time = Dm.arg<float?>(__extra, 0);
        float? stutter_time = Dm.arg<float?>(__extra, 1);
        float? stun_duration = Dm.arg<float?>(__extra, 2);
        siemens_coeff ??= 1;
        flags ??= NONE;
        jitter_time ??= 20 * SECONDS;
        stutter_time ??= 4 * SECONDS;
        stun_duration ??= 4 * SECONDS;
        object dot = null;
        dot = await base.electrocute_act(shock_damage, source, siemens_coeff, flags, Dm.forward(__extra, jitter_time, stutter_time, stun_duration));
        if (!Dm.truthy(dot))
            return dot;
        //Propagation through pulling, fireman carry
        if (!Dm.truthy(Dm.bitand(flags, SHOCK_ILLUSION)))
        {
            if (Dm.truthy(undergoing_cardiac_arrest()))
                set_heartattack(FALSE);
            DmList shocking_queue = Dm.list();
            if (iscarbon(pulling) && !Dm.eq(source, pulling))
                Dm.append(ref shocking_queue, pulling);
            if (iscarbon(pulledby) && !Dm.eq(source, pulledby))
                Dm.append(ref shocking_queue, pulledby);
            if (iscarbon(buckled) && !Dm.eq(source, buckled))
                Dm.append(ref shocking_queue, buckled);
            foreach (mob_living_carbon carried in Dm.each<mob_living_carbon>(buckled_mobs))
            {
                if (!Dm.eq(source, carried))
                    Dm.append(ref shocking_queue, carried);
            }
            //Found our victims, now lets shock them all
            foreach (object victim in Dm.each(shocking_queue))
            {
                mob_living_carbon C = victim as mob_living_carbon;
                await C.electrocute_act(Dm.mul(shock_damage, 0.75f), this, 1, flags, jitter_time, stutter_time, stun_duration);
            }
        }
        //Stun
        float should_stun = Dm.num((!Dm.truthy(Dm.bitand(flags, SHOCK_TESLA)) || Dm.num(siemens_coeff) > 0.5f) && !Dm.truthy(Dm.bitand(flags, SHOCK_NOSTUN)));
        float stun = Dm.not(Dm.bitand(flags, SHOCK_KNOCKDOWN));
        float immediately_stun = (should_stun is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.not(Dm.bitand(flags, SHOCK_DELAY_STUN)));
        if (Dm.truthy(immediately_stun))
        {
            if (Dm.truthy(stun))
            {
                // intended effect here is to floor you immediately if you are shocked twice in quick succession
                // or to keep you floored if you are already incapacitated otherwise
                if (Dm.truthy(incapacitated))
                    await Paralyze(stun_duration);
                // otherwise it just stuns you upright - until the second shock, which floors you
                else
                    await Stun(stun_duration);
            }
            else
                await Knockdown(stun_duration);
        }
        //Jitter and other fluff.
        do_jitter_animation(300);
        await adjust_jitter(jitter_time); // M8 slice E: the jitter status effect is translated
        await adjust_stutter(stutter_time);
        if (Dm.truthy(should_stun))
            _addtimer(Dm.@new<datum_callback>(this, nameof(secondary_shock), stun, Dm.num(stun_duration) * 1.5f), 2 * SECONDS, file: "code/modules/mob/living/carbon/carbon_defense.dm", line: 260);
        return shock_damage;
    }

    ///Called slightly after electrocute act to apply a secondary stun.
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:264
    public virtual async DmTask secondary_shock(object stun = null, object stun_duration = null)
    {
        if (Dm.truthy(stun))
            await Paralyze(stun_duration);
        else
            await Knockdown(stun_duration);
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:271 (M6 slice 1: the blood a touch shares)
public partial class mob_living_carbon : mob_living
{
    /// When another mob touches us, they may messy us up.
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:271
    [DmParams("who_touched_us", "messy_slots")]
    public virtual async DmTask share_blood_on_touch(mob_living_carbon_human who_touched_us = null, params object[] __extra)
    {
        return;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:274
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:274
    [DmParams("who_touched_us", "messy_slots")]
    public override async DmTask share_blood_on_touch(mob_living_carbon_human who_touched_us = null, params object[] __extra)
    {
        object messy_slots = Dm.arg(__extra, 0) ?? Dm.bitor(ITEM_SLOT_ICLOTHING, ITEM_SLOT_OCLOTHING);
        if (who_touched_us == null || !Dm.truthy(messy_slots))
            return;

        foreach (obj_item thing in Dm.each_cast<obj_item>(who_touched_us.get_equipped_items()))
        {
            if (Dm.truthy(Dm.bitand(thing.body_parts_covered, HANDS)) && prob(GET_ATOM_BLOOD_DNA_LENGTH(thing) * 25))
            {
                await add_blood_DNA_to_items(GET_ATOM_BLOOD_DNA(thing), messy_slots);
                return;
            }
        }

        if (prob(blood_in_hands * GET_ATOM_BLOOD_DNA_LENGTH(who_touched_us) * 10))
        {
            await add_blood_DNA_to_items(GET_ATOM_BLOOD_DNA(who_touched_us), messy_slots);
            who_touched_us.blood_in_hands -= 1;
        }
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:287
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:287  (the messages, the tail pulls and the hug's warmth since M9 slice F)
    public virtual async DmTask help_shake_act(mob_living_carbon helper = null, object force_friendly = null)
    {
        if (Dm.truthy(on_fire))
        {
            to_chat(helper, span_warning(I18n.T("You can't put [] out with just your bare hands!", p_them())));
            return;
        }

        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_CARBON_PRE_MISC_HELP]) ? NONE : this._SendSignal(COMSIG_CARBON_PRE_MISC_HELP, Dm.list(this, helper))), COMPONENT_BLOCK_MISC_HELP)))
            return;

        if (helper == this)
        {
            check_self_for_injuries();
            return;
        }

        if (body_position == LYING_DOWN)
        {
            if (Dm.truthy(buckled))
            {
                to_chat(helper, span_warning(I18n.T("You need to unbuckle [] first to do that!", this)));
                return;
            }
            helper.visible_message(span_notice(I18n.T("[] shakes [] trying to get [] up!", helper, this, p_them())),
                            null, span_hear(I18n.T("You hear the rustling of clothes.")), DEFAULT_MESSAGE_RANGE, Dm.list(helper, this));
            to_chat(helper, span_notice(I18n.T("You shake [] trying to pick [] up!", this, p_them())));
            to_chat(this, span_notice(I18n.T("[] shakes you to get you up!", helper)));
        }
        else if (Dm.eq(check_zone(helper.zone_selected), BODY_ZONE_HEAD) && Dm.truthy(get_bodypart(BODY_ZONE_HEAD))) //Headpats!
        {
            helper.visible_message(span_notice(I18n.T("[] gives [] a pat on the head to make [] feel better!", helper, this, p_them())),
                        null, span_hear(I18n.T("You hear a soft patter.")), DEFAULT_MESSAGE_RANGE, Dm.list(helper, this));
            to_chat(helper, span_notice(I18n.T("You give [] a pat on the head to make [] feel better!", this, p_them())));
            to_chat(this, span_notice(I18n.T("[] gives you a pat on the head to make you feel better! ", helper)));

            await share_blood_on_touch(helper as mob_living_carbon_human, Dm.bitor(ITEM_SLOT_HEAD, ITEM_SLOT_MASK));
            if (HAS_TRAIT(this, TRAIT_BADTOUCH))
                to_chat(helper, span_warning(I18n.T("[] looks visibly upset as you pat [] on the head.", this, p_them())));
        }
        else if (Dm.eq(helper.zone_selected, BODY_ZONE_PRECISE_GROIN) && get_organ_by_type(typeof(obj_item_organ_tail)) != null)
        {
            helper.visible_message(span_notice(I18n.T("[] pulls on []'s tail!", helper, this)),
                        null, span_hear(I18n.T("You hear a soft patter.")), DEFAULT_MESSAGE_RANGE, Dm.list(helper, this));
            to_chat(helper, span_notice(I18n.T("You pull on []'s tail!", this)));
            to_chat(this, span_notice(I18n.T("[] pulls on your tail!", helper)));
            if (HAS_TRAIT(this, TRAIT_BADTOUCH)) //How dare they!
                to_chat(helper, span_warning(I18n.T("[] makes a grumbling noise as you pull on [] tail.", this, p_their())));
            else
                await add_mood_event("tailpulled", typeof(datum_mood_event_tailpulled));
        }
        else if (Dm.eq(helper.zone_selected, BODY_ZONE_PRECISE_GROIN) && (get_item_by_slot(ITEM_SLOT_HEAD) is obj_item_clothing_head_costume_kitty || get_item_by_slot(ITEM_SLOT_HEAD) is obj_item_clothing_head_collectable_kitty))
        {
            obj_item_clothing_head faketail = get_item_by_slot(ITEM_SLOT_HEAD) as obj_item_clothing_head; // Should probably be COMSIG_CARBON_PRE_MISC_HELP
            helper.visible_message(span_danger(I18n.T("[] pulls on []'s tail... and it rips off!", helper, this)),
                        null, span_hear(I18n.T("You hear a ripping sound.")), DEFAULT_MESSAGE_RANGE, Dm.list(helper, this));
            to_chat(helper, span_danger(I18n.T("You pull on []'s tail... and it rips off!", this)));
            to_chat(this, span_userdanger(I18n.T("[] pulls on your tail... and it rips off!", helper)));
            playsound(loc, Dm.resource("sound/effects/cloth_rip.ogg"), 75, TRUE);
            await dropItemToGround(faketail);
            await helper.put_in_hands(faketail);
            await helper.add_mood_event("rippedtail", typeof(datum_mood_event_rippedtail));
        }
        else
        {
            if (helper.grab_state >= GRAB_AGGRESSIVE)
            {
                helper.visible_message(span_notice(I18n.T("[] embraces [] in a tight bear hug!", helper, this)),
                            null, span_hear(I18n.T("You hear the rustling of clothes.")), DEFAULT_MESSAGE_RANGE, Dm.list(helper, this));
                to_chat(helper, span_notice(I18n.T("You wrap [] into a tight bear hug!", this)));
                to_chat(this, span_notice(I18n.T("[] squeezes you super tightly in a firm bear hug!", helper)));
            }
            else
            {
                helper.visible_message(span_notice(I18n.T("[] hugs [] to make [] feel better!", helper, this, p_them())),
                            null, span_hear(I18n.T("You hear the rustling of clothes.")), DEFAULT_MESSAGE_RANGE, Dm.list(helper, this));
                to_chat(helper, span_notice(I18n.T("You hug [] to make [] feel better!", this, p_them())));
                to_chat(this, span_notice(I18n.T("[] hugs you to make you feel better!", helper)));
            }

            await share_blood_on_touch(helper as mob_living_carbon_human, Dm.bitor(Dm.bitor(ITEM_SLOT_HEAD, ITEM_SLOT_MASK), ITEM_SLOT_GLOVES));
            // Warm them up with hugs
            share_bodytemperature(helper);

            // No moodlets for people who hate touches
            if (!HAS_TRAIT(this, TRAIT_BADTOUCH))
            {
                if (helper.grab_state >= GRAB_AGGRESSIVE)
                    await add_mood_event("hug", typeof(datum_mood_event_bear_hug));
                else
                {
                    if (bodytemperature > helper.bodytemperature)
                    {
                        if (!HAS_TRAIT(helper, TRAIT_BADTOUCH))
                            await helper.add_mood_event("hug", typeof(datum_mood_event_warmhug), this); // Hugger got a warm hug (Unless they hate hugs)
                        await add_mood_event("hug", typeof(datum_mood_event_hug)); // Receiver always gets a mood for being hugged
                    }
                    else
                        await add_mood_event("hug", typeof(datum_mood_event_warmhug), helper); // You got a warm hug
                }
            }
            else
            {
                if (helper.grab_state >= GRAB_AGGRESSIVE)
                    await add_mood_event("hug", typeof(datum_mood_event_bad_touch_bear_hug));
            }

            // Let people know if they hugged someone really warm or really cold
            if (helper.bodytemperature > BODYTEMP_HEAT_DAMAGE_LIMIT)
                to_chat(this, span_warning(I18n.T("It feels like [] is over heating as [] hug[] you.", helper, helper.p_they(), helper.p_s())));
            else if (helper.bodytemperature < BODYTEMP_COLD_DAMAGE_LIMIT)
                to_chat(this, span_warning(I18n.T("It feels like [] is freezing as [] hug[] you.", helper, helper.p_they(), helper.p_s())));

            if (bodytemperature > BODYTEMP_HEAT_DAMAGE_LIMIT)
                to_chat(helper, span_warning(I18n.T("It feels like [] is over heating as you hug [].", this, p_them())));
            else if (bodytemperature < BODYTEMP_COLD_DAMAGE_LIMIT)
                to_chat(helper, span_warning(I18n.T("It feels like [] is freezing as you hug [].", this, p_them())));

            // helper.mob_mood: a moodless friendly helper's read is a runtime in tg; the port skips the friendly hug's mood instead of
            // unwinding the whole help (DV555)
            if ((HAS_TRAIT(helper, TRAIT_FRIENDLY) || Dm.truthy(force_friendly)) && helper.mob_mood != null)
            {
                if (helper.mob_mood.sanity >= SANITY_GREAT)
                {
                    Dm.@new<obj_effect_temp_visual_heart>(loc);
                    await add_mood_event("friendly_hug", typeof(datum_mood_event_besthug), helper);
                }
                else if (helper.mob_mood.sanity >= SANITY_DISTURBED)
                    await add_mood_event("friendly_hug", typeof(datum_mood_event_betterhug), helper);
            }

            if (HAS_TRAIT(this, TRAIT_BADTOUCH))
                to_chat(helper, span_warning(I18n.T("[] looks visibly upset as you hug [].", this, p_them())));
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_CARBON_HELP_ACT]))
            this._SendSignal(COMSIG_CARBON_HELP_ACT, Dm.list(this, helper));
        if (Dm.truthy(helper._listen_lookup?[COMSIG_CARBON_HELPED]))
            helper._SendSignal(COMSIG_CARBON_HELPED, Dm.list(helper, this));

        await adjust_status_effects_on_shake_up();
        await set_resting(FALSE);
        if (body_position != STANDING_UP && !Dm.truthy(resting) && !Dm.truthy(buckled) && !HAS_TRAIT(this, TRAIT_FLOORED))
            get_up(TRUE);

        playsound(loc, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 50, TRUE, -1); // tg: code/modules/mob/living/carbon/carbon_defense.dm:398

        // Shake animation
        if (Dm.truthy(incapacitated))
            shake_up_animation();
    }

    /// Check ourselves to see if we've got any shrapnel, return true if we do. This is a much simpler version of what humans do, we only indicate we're checking ourselves if there's actually shrapnel
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:411
    public virtual object check_self_for_injuries()
    {
        if (IS_UNCONSCIOUS(this))
            return null;

        bool embeds = false;
        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            foreach (obj_item weapon in Dm.each_cast<obj_item>(limb.embedded_objects))
            {
                if (!embeds)
                {
                    embeds = true;
                    // this way, we only visibly try to examine ourselves if we have something embedded, otherwise we'll still hug ourselves :)
                    visible_message(span_notice(I18n.T("[] examines []self.", this, p_them())),
                        span_notice(I18n.T("You check yourself for shrapnel.")), visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
                }
                bool harmless = Dm.truthy(((datum_embedding)weapon.get_embed()).is_harmless());
                string stuck_wordage = harmless ? I18n.T("stuck to") : I18n.T("embedded in");
                // [icon2html(weapon, src)]: the item's picture in the chat line (chat icons, M9)
                Dm.Deferred("code/modules/mob/living/carbon/carbon_defense.dm:425", "check_self_for_injuries: icon2html of an embedded object, its picture in the chat line (chat icons, M10+)");
                string embed_text = I18n.T("\t <a href='byond://?src=[];embedded_object=[];embedded_limb=[]'> There is [] \\a [] [] your []!</a>", REF(this), REF(weapon), REF(limb), "", weapon, stuck_wordage, limb.plaintext_zone);
                if (harmless)
                    to_chat(this, span_italics(span_notice(embed_text)));
                else
                    to_chat(this, span_boldwarning(embed_text));
            }
        }
        return embeds ? TRUE : FALSE;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:666 (interleaved type, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:665
    public override object get_shove_flags(mob_living shover = null, obj_item weapon = null)
    {
        object dot = base.get_shove_flags(shover, weapon);
        dot = Dm.bitor(dot, SHOVE_CAN_STAGGER);
        if (Dm.truthy(IsKnockdown()) && !Dm.truthy(IsParalyzed()) && HAS_TRAIT(this, TRAIT_DAZED))
            dot = Dm.bitor(dot, SHOVE_CAN_KICK_SIDE);
        if (HAS_TRAIT(this, TRAIT_NO_SIDE_KICK)) // added as an extra check, just in case
            dot = Dm.bitand(dot, Dm.bitnot(SHOVE_CAN_KICK_SIDE));
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:123 (interleaved type, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:123
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);

        // gaps VI1: the contact diseases spread both ways (the toucher is a living mob in tg: user is typed human)
        mob_living toucher = user as mob_living;
        foreach (object thing in Dm.each(diseases))
        {
            datum_disease D = (datum_disease)thing;
            if (Dm.truthy(Dm.bitand(D.spread_flags, DISEASE_SPREAD_CONTACT_SKIN)) && toucher != null)
                await toucher.ContactContractDisease(D);
        }

        if (toucher != null)
        {
            foreach (object thing in Dm.each(toucher.diseases))
            {
                datum_disease D = (datum_disease)thing;
                if (Dm.truthy(Dm.bitand(D.spread_flags, DISEASE_SPREAD_CONTACT_SKIN)))
                    await ContactContractDisease(D);
            }
        }

        if (Dm.truthy(dot))
            return TRUE;

        foreach (datum_wound wounds in Dm.each_cast<datum_wound>(all_wounds))
        {
            if (Dm.truthy(await wounds.try_handling(user as mob_living)))
                return TRUE;
        }

        return FALSE;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:481 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:481
    public override async DmTask<object> adjust_oxy_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        updating_health ??= TRUE;
        object dot = null;
        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_NOBREATH))
            amount = min(Dm.num(amount), 0); //Prevents oxy damage but not healing

        dot = await base.adjust_oxy_loss(amount, updating_health, forced, required_biotype);
        check_passout();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon_defense.dm:493
    public override async DmTask<object> set_oxy_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        updating_health ??= TRUE;
        object dot = null;
        dot = await base.set_oxy_loss(amount, updating_health, forced, required_biotype);
        check_passout();
        return dot;
    }

    /**
    * Check to see if we should be passed out from oxyloss
    */
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:500
    [SignalHandler]
    public virtual void check_passout()
    {
        if (HAS_TRAIT(this, TRAIT_NO_OXYLOSS_PASSOUT))
        {
            REMOVE_TRAIT(this, TRAIT_KNOCKEDOUT, OXYLOSS_TRAIT);
            return;
        }

        object mob_oxyloss = get_oxy_loss();
        if (Dm.num(mob_oxyloss) >= OXYLOSS_PASSOUT_THRESHOLD)
            ADD_TRAIT(this, TRAIT_KNOCKEDOUT, OXYLOSS_TRAIT);
        else
            REMOVE_TRAIT(this, TRAIT_KNOCKEDOUT, OXYLOSS_TRAIT);
    }

    // tg: code/modules/mob/living/carbon/carbon_defense.dm:512  (M8x slice A: the cryo cell's full-health test)
    public override object get_organic_health()
    {
        float dot = health;
        foreach (obj_item_bodypart limb in Dm.each<obj_item_bodypart>(get_bodyparts()))
        {
            if (!Dm.truthy(IS_ORGANIC_LIMB(limb)))
                dot += (limb.brute_dam * limb.body_damage_coeff) + (limb.burn_dam * limb.body_damage_coeff);
        }
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:518 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:518  (pulling, 2026-10-15; the self grasp, 2026-10-20)
    public override async DmTask<object> grabbedby(mob_living user = null, float? supress_message = null)
    {
        supress_message ??= FALSE;
        if (user != this)
            return await base.grabbedby(user, supress_message);

        obj_item_bodypart grasped_part = get_bodypart(zone_selected as string) as obj_item_bodypart;
        if (!Dm.truthy(grasped_part?.can_be_grasped()))
            return null;
        float starting_hand_index = active_hand_index;
        if (starting_hand_index == grasped_part.held_index)
        {
            to_chat(this, span_danger(I18n.T("You can't grasp your [] with itself!", grasped_part.name)));
            return null;
        }

        float bleed_rate = grasped_part.cached_bleed_rate;
        string bleeding_text = (Dm.truthy(bleed_rate) ? I18n.T(", trying to stop the bleeding") : "");
        to_chat(this, span_warning(I18n.T("You try grasping at your [][]...", grasped_part.name, bleeding_text)));
        if (!Dm.truthy(await do_after(this, 0.75f * SECONDS)))
        {
            to_chat(this, span_danger(I18n.T("You fail to grasp your [].", grasped_part.name)));
            return null;
        }

        obj_item_hand_item_self_grasp grasp = Dm.@new<obj_item_hand_item_self_grasp>();
        if (starting_hand_index != active_hand_index || !Dm.truthy(await put_in_active_hand(grasp)))
        {
            to_chat(this, span_danger(I18n.T("You fail to grasp your [].", grasped_part.name)));
            QDEL_NULL(ref grasp);
            return null;
        }
        grasp.grasp_limb(grasped_part);
        return null;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:544 (interleaved types, rules 1.9)
public partial class obj_item_bodypart : obj_item
{
    /// If TRUE, the owner of this bodypart can try grabbing it to slow bleeding, as well as various other effects.
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:545
    public virtual object can_be_grasped()
    {
        if (Dm.truthy(cached_bleed_rate))
            return TRUE;

        foreach (datum_wound iterated_wound in Dm.each_cast<datum_wound>(wounds))
        {
            if (Dm.truthy(Dm.bitand(iterated_wound.wound_flags, CAN_BE_GRASPED)))
                return TRUE;
        }

        return FALSE;
    }
}

/// an abstract item representing you holding your own limb to staunch the bleeding, see [/mob/living/carbon/proc/grabbedby] will probably need to find somewhere else to put this.
// tg: code/modules/mob/living/carbon/carbon_defense.dm:556
[DmPath("/obj/item/hand_item/self_grasp")]
public partial class obj_item_hand_item_self_grasp : obj_item_hand_item
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:568
    public override object Destroy(float? force = null)
    {
        force ??= FALSE;
        if (user != null)
        {
            to_chat(user, span_warning(I18n.T("You stop holding onto your[].", grasped_part != null ? Dm.text(" []", grasped_part.name) : I18n.T("self"))));
            UnregisterSignal(user, COMSIG_QDELETING);
        }
        if (grasped_part != null)
        {
            UnregisterSignal(grasped_part, Dm.list(COMSIG_CARBON_REMOVE_LIMB, COMSIG_QDELETING));
            grasped_part.grasped_by = null;
            grasped_part.refresh_bleed_rate();
        }
        grasped_part = null;
        user = null;
        return base.Destroy(force);
    }

    /// The limb or the whole damn person we were grasping got deleted or dismembered, so we don't care anymore
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:581
    [SignalHandler]
    public virtual void qdel_void()
    {
        qdel(this);
    }

    /// We've already cleared that the bodypart in question is bleeding in [the place we create this][/mob/living/carbon/proc/grabbedby], so set up the connections
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:586
    public virtual object grasp_limb(obj_item_bodypart grasping_part = null)
    {
        user = grasping_part.owner;
        if (!istype(user, typeof(mob_living_carbon)))
        {
            stack_trace(Dm.text("[] attempted to try_grasp() with [] user", this, isdatum(user) ? user.GetType() : user == null ? "null" : user), "code/modules/mob/living/carbon/carbon_defense.dm", 590);
            qdel(this);
            return null;
        }

        grasped_part = grasping_part;
        grasped_part.grasped_by = this;
        grasped_part.refresh_bleed_rate();
        RegisterSignal(user, COMSIG_QDELETING, nameof(qdel_void));
        RegisterSignals(grasped_part, Dm.list(COMSIG_CARBON_REMOVE_LIMB, COMSIG_QDELETING), nameof(qdel_void));

        float bleed_rate = grasped_part.cached_bleed_rate;
        string bleeding_text = (Dm.truthy(bleed_rate) ? I18n.T(", trying to stop the bleeding") : "");
        user.visible_message(span_danger(I18n.T("[] grasps at [] [][].", user, user.p_their(), grasped_part.name, bleeding_text)), span_notice(I18n.T("You grab hold of your [] tightly.", grasped_part.name)), vision_distance: COMBAT_MESSAGE_RANGE);
        playsound(get_turf(this), Dm.resource("sound/items/weapons/thudswoosh.ogg"), 50, TRUE, -1);
        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:44
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:44
    public override object try_catch_item(obj_item item = null, float? skip_throw_mode_check = null, float? try_offhand = null)
    {
        skip_throw_mode_check ??= FALSE;
        try_offhand ??= FALSE;
        object dot = null;
        dot = base.try_catch_item(item, skip_throw_mode_check, try_offhand);
        if (Dm.truthy(dot))
            throw_mode_off(THROW_MODE_TOGGLE);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon_defense.dm:49
    public override object can_catch_item(float? skip_throw_mode_check = null, float? try_offhand = null)
    {
        skip_throw_mode_check ??= FALSE;
        try_offhand ??= FALSE;
        if (!Dm.truthy(skip_throw_mode_check) && !Dm.truthy(throw_mode))
            return FALSE;
        return base.can_catch_item(skip_throw_mode_check, try_offhand);
    }

    // tg: code/modules/mob/living/carbon/carbon_defense.dm:54
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        hitpush ??= TRUE;
        blocked ??= FALSE;
        if (!Dm.truthy(skipcatch) && Dm.truthy(try_catch_item(hitting_atom as obj_item)))
            return TRUE;
        return await base.hitby(hitting_atom, skipcatch, hitpush, blocked, throwingdatum);
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:404 (M4v slice 4: the shake of a mob shaken up from the floor)
public partial class mob : atom_movable
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:1  #define SHAKE_ANIMATION_OFFSET 4 (file-local, #undef at :673)
    private const int SHAKE_ANIMATION_OFFSET = 4;

    // tg: code/modules/mob/living/carbon/carbon_defense.dm:404
    public virtual void shake_up_animation()
    {
        float direction = prob(50) ? -1 : 1;
        animate(this, Dm.named("pixel_w", SHAKE_ANIMATION_OFFSET * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_OUT)), Dm.named("flags", Dm.bitor(ANIMATION_PARALLEL, ANIMATION_RELATIVE)));
        animate(Dm.named("pixel_w", SHAKE_ANIMATION_OFFSET * -2 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_w", SHAKE_ANIMATION_OFFSET * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)), Dm.named("flags", ANIMATION_RELATIVE));
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:3 (interleaved types, rules 1.9; M5x slice 3: the eyes and ears a
// blast's flash and bang reach)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:3
    public override object get_eye_protection()
    {
        float dot = Dm.num(base.get_eye_protection());
        if (Dm.truthy(is_blind()) && !Dm.truthy(is_blind_from(Dm.list(TRAIT_STATUS_EFFECT(Dm.initial(typeof(datum_status_effect_knocked_out), (datum_status_effect p) => p.id)), HYPNOCHAIR_TRAIT))))
            return INFINITY; //For all my homies that can not see in the world
        obj_item_organ_eyes eyes = get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes == null)
            return INFINITY; //Can't get flashed without eyes
        dot += eyes.flash_protect;
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon_defense.dm:12
    public override async DmTask sound_damage(object damage = null, object deafen = null)
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;
        obj_item_organ_ears ears = get_organ_slot(ORGAN_SLOT_EARS) as obj_item_organ_ears;
        if (QDELETED(ears))
            return;
        if (Dm.truthy(damage))
            await ears.apply_organ_damage(Dm.num(damage) * ears.damage_multiplier);
        if (Dm.truthy(deafen))
            ears.adjust_temporary_deafness(deafen);
    }

    // tg: code/modules/mob/living/carbon/carbon_defense.dm:23
    public override object get_ear_protection(float? ignore_deafness = null)
    {
        ignore_deafness ??= FALSE;
        obj_item_organ_ears ears = get_organ_slot(ORGAN_SLOT_EARS) as obj_item_organ_ears;
        return Dm.add(base.get_ear_protection(ignore_deafness), ears?.bang_protect);
    }

    // tg: code/modules/mob/living/carbon/carbon_defense.dm:32
    public override object is_ears_covered()
    {
        foreach (obj_item worn_thing in Dm.each_cast<obj_item>(get_equipped_items(INCLUDE_ABSTRACT)))
        {
            if (Dm.truthy(Dm.bitand(worn_thing.flags_cover, EARS_COVERED)))
                return worn_thing;
        }
        return null;
    }

    // tg: code/modules/mob/living/carbon/carbon_defense.dm:37  (the M7 projectiles slice)
    public override async DmTask check_projectile_dismemberment(obj_projectile proj = null, object def_zone = null)
    {
        obj_item_bodypart affecting = get_bodypart((string)def_zone) as obj_item_bodypart;
        if (Dm.truthy(affecting) && Dm.truthy(affecting.can_dismember__proc()) && !Dm.truthy(Dm.bitand(affecting.bodypart_flags, BODYPART_UNREMOVABLE)) && Dm.num(affecting.get_damage()) >= (affecting.max_damage - proj.dismemberment))
        {
            if (!Dm.truthy(await affecting.dismember(proj.damtype)) || !Dm.truthy(proj.catastropic_dismemberment))
                return;
            await apply_damage(proj.damage, proj.damtype, BODY_ZONE_CHEST, wound_bonus: proj.wound_bonus); //stops a projectile blowing off a limb effectively doing no damage. Mostly relevant for sniper rifles.
        }
    }

    // tg: code/modules/mob/living/carbon/carbon_defense.dm:432
    public override async DmTask<object> flash_act(float? intensity = null, float? override_blindness_check = null, float? affect_silicon = null, float? visual = null, Type type = null, float? length = null)
    {
        intensity ??= 1;
        override_blindness_check ??= 0;
        affect_silicon ??= 0;
        visual ??= 0;
        type ??= typeof(atom_movable_screen_fullscreen_flash);
        length ??= 25;
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_MOB_FLASH_OVERRIDE_CHECK]) ? NONE : _SendSignal(COMSIG_MOB_FLASH_OVERRIDE_CHECK, Dm.list(this, this)), FLASH_OVERRIDDEN))) //Check for behavior overrides before doing the act itself. If we have a behavior override, we handle everything there and skip the rest
            return FLASH_COMPLETED;

        obj_item_organ_eyes eyes = get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes == null) //can't flash what can't see!
            return null;

        object dot = await base.flash_act(intensity, override_blindness_check, affect_silicon, visual, type, length);

        float damage = intensity.Value - Dm.num(get_eye_protection());
        if (Dm.truthy(dot)) // we've been flashed
        {
            if (Dm.truthy(visual))
                return dot;

            if (damage == 1)
            {
                to_chat(this, span_warning(I18n.T("Your eyes sting a little.")));
                if (prob(40))
                    await eyes.apply_organ_damage(1);
            }
            else if (damage == 2)
            {
                to_chat(this, span_warning(I18n.T("Your eyes burn.")));
                await eyes.apply_organ_damage(rand(2, 4));
            }
            else if (damage >= 3)
            {
                to_chat(this, span_warning(I18n.T("Your eyes itch and burn severely!")));
                await eyes.apply_organ_damage(rand(12, 16));
            }

            if (Dm.num(eyes.damage) > 10)
            {
                await adjust_temp_blindness(damage * 2 * SECONDS);
                await set_eye_blur_if_lower(damage * rand(6 * SECONDS, 12 * SECONDS));

                if (Dm.num(eyes.damage) > Dm.num(eyes.low_threshold))
                {
                    if (!Dm.truthy(is_nearsighted_from(EYE_DAMAGE)) && prob(Dm.num(eyes.damage) - Dm.num(eyes.low_threshold)))
                    {
                        to_chat(this, span_warning(I18n.T("Your eyes start to burn badly!")));
                        await eyes.apply_organ_damage(eyes.low_threshold);
                    }
                    else if (!Dm.truthy(is_blind()) && prob(Dm.num(eyes.damage) - Dm.num(eyes.high_threshold)))
                    {
                        to_chat(this, span_warning(I18n.T("You can't see anything!")));
                        await eyes.apply_organ_damage(eyes.maxHealth);
                    }
                }
                else
                    to_chat(this, span_warning(I18n.T("Your eyes are really starting to hurt. This can't be good for you!")));
            }
            return TRUE;
        }
        else if (damage == 0 && prob(20)) // just enough protection
            to_chat(this, span_notice(I18n.T("Something bright flashes in the corner of your vision!")));
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:605 (the M7 delamination slice: the bioscrambler anomaly)
public partial class mob_living_carbon : mob_living
{
    /// Randomise a body part and organ of this mob
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:606
    public virtual async DmTask<object> bioscramble(object scramble_source = null)
    {
        if (!Dm.truthy(Dm.bitand(mob_biotypes, MOB_ORGANIC)))
            return FALSE;

        if (HAS_TRAIT(this, TRAIT_GENELESS))
            return FALSE;

        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
            return FALSE;

        if (Dm.ge(run_armor_check(attack_flag: BIO, silent: TRUE), 100))
        {
            to_chat(this, span_warning(I18n.T("Your armor shields you from []!", scramble_source)));
            return FALSE;
        }

        if (!Dm.truthy(length(GLOB.bioscrambler_valid_organs)) || !Dm.truthy(length(GLOB.bioscrambler_valid_parts)))
            init_bioscrambler_lists();

        float changed_something = FALSE;
        Type new_organ_type = pick(GLOB.bioscrambler_valid_organs) as Type;
        obj_item_organ replaced = get_organ_slot(Dm.initial(new_organ_type, (obj_item_organ p) => p.slot)) as obj_item_organ;
        // tg reads new_organ.zone off the typepath, which DM reads as the type's default (rules 10.3)
        // DEVIATION(DV89): an organ or bodypart type whose own tg code is not translated (inert, D112) is not put in: that slot is left
        // as it is, as DV27 refuses an inert type's interactions (238 of tg's 311 organ types and 46 of its 140 bodypart types)
        if ((!Dm.truthy(replaced) || (!Dm.truthy(Dm.bitand(replaced.organ_flags, ORGAN_BIOSCRAMBLE_INCOMPATIBLE)) && !Dm.truthy(Dm.bitand(replaced.flags_1, HOLOGRAM_1)))) && Dm.truthy(get_bodypart((string)deprecise_zone(Dm.initial(new_organ_type, (obj_item_organ p) => p.zone)))) && !InertSeam.IsInertType(new_organ_type))
        {
            changed_something = TRUE;
            obj_item_organ new_organ = Dm.@new(new_organ_type) as obj_item_organ;
            await new_organ.replace_into(this);
            new_organ.organ_flags = Dm.bitor(new_organ.organ_flags, ORGAN_MUTANT);
        }

        if (!HAS_TRAIT(this, TRAIT_NODISMEMBER))
        {
            Type new_part_type = pick(GLOB.bioscrambler_valid_parts) as Type;
            obj_item_bodypart picked_user_part = get_bodypart((string)Dm.initial(new_part_type, (obj_item_bodypart p) => p.body_zone)) as obj_item_bodypart;
            // BODYPART_CAN_BE_BIOSCRAMBLED (__DEFINES/mobs.dm:170), inline (rules 11.1)
            if (Dm.truthy(picked_user_part) && (!Dm.truthy(Dm.bitand(picked_user_part.bodytype, BODYTYPE_ROBOTIC | BODYTYPE_LARVA_PLACEHOLDER | BODYTYPE_GOLEM | BODYTYPE_PEG))
                && !Dm.truthy(Dm.bitand(picked_user_part.flags_1, HOLOGRAM_1))
                && !Dm.truthy(Dm.bitand(picked_user_part.bodypart_flags, BODYPART_STUMP))) && !InertSeam.IsInertType(new_part_type)) // DEVIATION(DV89)
            {
                changed_something = TRUE;
                obj_item_bodypart new_part = Dm.@new(new_part_type) as obj_item_bodypart;
                await new_part.replace_limb(this);
                if (Dm.truthy(picked_user_part))
                    qdel(picked_user_part);
            }
        }

        if (!Dm.truthy(changed_something))
        {
            to_chat(this, span_notice(I18n.T("Your augmented body protects you from []!", scramble_source)));
            return FALSE;
        }
        await update_body(TRUE);
        balloon_alert(this, I18n.T("something has changed about you"));
        return TRUE;
    }

    /// Fill in the lists of things we can bioscramble into people
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:650
    public virtual void init_bioscrambler_lists()
    {
        DmList body_parts = (DmList)Dm.add(Dm.add(Dm.add(typesof(typeof(obj_item_bodypart_chest)), typesof(typeof(obj_item_bodypart_head))), subtypesof(typeof(obj_item_bodypart_arm))), subtypesof(typeof(obj_item_bodypart_leg)));
        foreach (Type part in Dm.each_cast<Type>(body_parts.Copy()))
        {
            // BODYPART_CAN_BE_BIOSCRAMBLED(part) on the typepath reads its defaults (rules 10.3)
            if (!Dm.truthy(is_type_in_typecache(part, GLOB.bioscrambler_parts_blacklist))
                && !Dm.truthy(Dm.bitand(Dm.initial(part, (obj_item_bodypart p) => p.bodytype), BODYTYPE_ROBOTIC | BODYTYPE_LARVA_PLACEHOLDER | BODYTYPE_GOLEM | BODYTYPE_PEG))
                && !Dm.truthy(Dm.bitand(Dm.initial(part, (obj_item_bodypart p) => p.flags_1), HOLOGRAM_1))
                && !Dm.truthy(Dm.bitand(Dm.initial(part, (obj_item_bodypart p) => p.bodypart_flags), BODYPART_STUMP)))
                continue;
            Dm.remove(ref body_parts, part);
        }
        GLOB.bioscrambler_valid_parts = body_parts;

        DmList organs = subtypesof(typeof(obj_item_organ));
        foreach (Type organ_type in Dm.each_cast<Type>(organs.Copy()))
        {
            if (!Dm.truthy(is_type_in_typecache(organ_type, GLOB.bioscrambler_organs_blacklist)) && !Dm.truthy(Dm.bitand(Dm.initial(organ_type, (obj_item_organ p) => p.organ_flags), ORGAN_ROBOTIC)))
                continue;
            Dm.remove(ref organs, organ_type);
        }
        GLOB.bioscrambler_valid_organs = organs;
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:27 (M8 slice B: condensed capsaicin, the pepper spray)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:27
    public override object is_pepper_proof(float? check_flags = null)
    {
        check_flags ??= ALL;
        if (get_organ_by_type(typeof(obj_item_organ_eyes)) is obj_item_organ_eyes eyes && Dm.truthy(eyes.pepperspray_protect))
            return eyes;
        return null;
    }
}

// Gaps package C1.
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:145
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(((mob_living)user).combat_mode))
        {
            foreach (datum_wound wounds in Dm.each_cast<datum_wound>(all_wounds))
            {
                if (Dm.truthy(await wounds.try_handling((mob_living)user)))
                    return TRUE;
            }
        }

        return await base.attack_animal(user, modifiers);
    }
}

// tg: code/modules/mob/living/carbon/carbon_defense.dm:153 (interleaved types, rules 1.9; gaps package C5: a monkey's bite)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defense.dm:153
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        mob_living_carbon_human biter = user as mob_living_carbon_human;

        if (Dm.truthy(try_inject(biter, injection_flags: INJECT_TRY_SHOW_ERROR_MESSAGE)))
        {
            foreach (datum_disease D in Dm.each_cast<datum_disease>(diseases))
            {
                if (Dm.truthy(Dm.bitand(D.spread_flags, DISEASE_SPREAD_CONTACT_SKIN)) && prob(85))
                    await biter.ContactContractDisease(D); // gaps VI1
            }
        }

        foreach (datum_disease D in Dm.each_cast<datum_disease>(biter.diseases))
        {
            if (Dm.truthy(Dm.bitand(D.spread_flags, DISEASE_SPREAD_CONTACT_SKIN)))
                await ContactContractDisease(D); // gaps VI1
        }

        if (!Dm.truthy(biter.combat_mode))
        {
            await help_shake_act(biter);
            return FALSE;
        }

        object dot = await base.attack_paw(user, modifiers);
        if (Dm.truthy(dot)) //successful monkey bite.
        {
            foreach (datum_disease D in Dm.each_cast<datum_disease>(biter.diseases))
            {
                if (Dm.truthy(Dm.bitand(D.spread_flags, DISEASE_SPREAD_SPECIAL | DISEASE_SPREAD_NON_CONTAGIOUS)))
                    continue;
                await ForceContractDisease(D);
            }
        }
        return dot;
    }
}
