// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/medical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 6 (gauze, owner Q2): the medical stack's procs (medical.dm:61-372: the heal loop every medical stack shares; the
// gauze's right click goes through it), the wrap (medical.dm:395-569: applied to a limb by the limb_applicable component) and
// the gauze (medical.dm:571-665, the improvised gauze's var block is generated). The var blocks are generated (D111). M8
// slice D: the other medical stacks (bruise pack, suture, ointment, mesh, aloe, bone gel, poultice, bandage), at the end.

// tg: code/game/objects/items/stacks/medical.dm:1
public partial class obj_item_stack_medical : obj_item_stack
{
    // tg: code/game/objects/items/stacks/medical.dm:61
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;
        if (!Dm.truthy(begin_heal_loop(interacting_with as mob_living, user, auto_change_zone: TRUE)))
            return NONE; // [ITEM_INTERACT_BLOCKING] would be redundant as we are nobludgeon
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/stacks/medical.dm:68
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;
        if (!Dm.truthy(begin_heal_loop(interacting_with as mob_living, user, auto_change_zone: FALSE)))
            return NONE; // see above
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/stacks/medical.dm:75
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_item_context();
        return dot;
    }

    // tg: code/game/objects/items/stacks/medical.dm:79
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (!isliving(target))
            return NONE;
        if (iscarbon(target))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Auto Heal";
            context[SCREENTIP_CONTEXT_RMB] = "Manual Heal";
        }
        else
        {
            context[SCREENTIP_CONTEXT_LMB] = "Heal";
        }
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/items/stacks/medical.dm:89
    public override async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        await base.apply_fantasy_bonuses(bonus);
        if (Dm.truthy(heal_brute))
            heal_brute = modify_fantasy_variable("heal_brute", heal_brute, bonus);
        if (Dm.truthy(heal_burn))
            heal_burn = modify_fantasy_variable("heal_burn", heal_burn, bonus);
        if (Dm.truthy(stop_bleeding))
            stop_bleeding = modify_fantasy_variable("stop_bleeding", stop_bleeding, Dm.num(bonus) / 10);
        if (Dm.truthy(sanitization))
            sanitization = modify_fantasy_variable("sanitization", sanitization, Dm.num(bonus) / 10);
        if (Dm.truthy(flesh_regeneration))
            flesh_regeneration = modify_fantasy_variable("flesh_regeneration", flesh_regeneration, Dm.num(bonus) / 10);
    }

    // tg: code/game/objects/items/stacks/medical.dm:102
    public override async DmTask remove_fantasy_bonuses(object bonus = null)
    {
        heal_brute = reset_fantasy_variable("heal_brute", heal_brute);
        heal_burn = reset_fantasy_variable("heal_burn", heal_burn);
        stop_bleeding = reset_fantasy_variable("stop_bleeding", stop_bleeding);
        sanitization = reset_fantasy_variable("sanitization", sanitization);
        flesh_regeneration = reset_fantasy_variable("flesh_regeneration", flesh_regeneration);
        await base.remove_fantasy_bonuses(bonus);
    }

    /// Used to begin the recursive healing loop.
    /// Returns TRUE if we entered the loop, FALSE if we didn't
    // tg: code/game/objects/items/stacks/medical.dm:112
    public virtual object begin_heal_loop(mob_living patient = null, mob_living user = null, float? auto_change_zone = null)
    {
        auto_change_zone ??= TRUE;
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, patient)))
            return FALSE;
        object heal_zone = check_zone(user.zone_selected);
        if (!Dm.truthy(try_heal_checks(patient, user, heal_zone)))
            return FALSE;
        SSblackbox.record_feedback("nested tally", "medical_item_used", 1, Dm.list(Dm.text("[]", Dm.truthy(auto_change_zone) ? "auto" : "manual"), Dm.text("[]", GetType())));
        patient.balloon_alert(user, I18n.T("[] []...", apply_verb, parse_zone(heal_zone)));
        Waiting.Detach(try_heal(patient, user, heal_zone, FALSE, iscarbon(patient) && Dm.truthy(auto_change_zone) ? TRUE : FALSE)); // auto change is useless for non-carbons
        return TRUE;
    }

    /**
     * What actually handles printing the message that we're starting to heal someone, and trying to heal them
     *
     * This proc is recursively called until we run out of charges OR until the patient is fully healed
     * OR until the target zone is fully healed (if auto_change_zone is FALSE)
     *
     * * patient - The mob we're trying to heal
     * * user - The mob that's trying to heal the patient
     * * healed_zone - The zone we're trying to heal on the patient
     * Disregarded if auto_change_zone is TRUE
     * * silent - If we should not print the message that we're starting to heal the patient
     * Used so looping the proc doesn't spam messages
     * * auto_change_zone - Handles the behavior when we finish healing a zone
     * If auto_change_zone is set to TRUE, it picks the next most damaged zone to heal
     * If auto_change_zone is set to FALSE, it'll give the user a chance to pick a new zone to heal
     * If continuous is set to true, it will play the continuous sound for healing
     */
    // tg: code/game/objects/items/stacks/medical.dm:140
    public virtual async DmTask try_heal(mob_living patient = null, mob_living user = null, object healed_zone = null, float? silent = null, float? auto_change_zone = null, float? continuous = null)
    {
        silent ??= FALSE;
        auto_change_zone ??= TRUE;
        continuous ??= FALSE;
        if (Dm.truthy(heal_begin_sound) && !Dm.truthy(continuous))
            playsound(patient, heal_begin_sound, 75, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);
        if (patient == user)
        {
            if (!Dm.truthy(silent))
                user.visible_message(
                    span_notice(I18n.T("[] starts to apply [] on []self...", user, this, user.p_them())),
                    span_notice(I18n.T("You begin applying [] on yourself...", this)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            if (!Dm.truthy(await do_after(
                user,
                self_delay * (Dm.truthy(auto_change_zone) ? 1 : 0.9f),
                patient,
                extra_checks: Dm.@new<datum_callback>(this, nameof(can_heal), patient, user, healed_zone))))
                return;
            if (!Dm.truthy(auto_change_zone))
                healed_zone = check_zone(user.zone_selected);
            if (!Dm.truthy(try_heal_checks(patient, user, healed_zone)))
                return;
        }
        else if (Dm.truthy(other_delay))
        {
            if (!Dm.truthy(silent))
                user.visible_message(
                    span_notice(I18n.T("[] starts to apply [] on [].", user, this, patient)),
                    span_notice(I18n.T("You begin applying [] on []...", this, patient)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            if (!Dm.truthy(await do_after(
                user,
                other_delay * (Dm.truthy(auto_change_zone) ? 1 : 0.9f),
                patient,
                extra_checks: Dm.@new<datum_callback>(this, nameof(can_heal), patient, user, healed_zone))))
                return;
            if (!Dm.truthy(auto_change_zone))
                healed_zone = check_zone(user.zone_selected);
            if (!Dm.truthy(try_heal_checks(patient, user, healed_zone)))
                return;
        }
        else
        {
            if (!Dm.truthy(silent))
                user.visible_message(
                    span_notice(I18n.T("[] applies [] on [].", user, this, patient)),
                    span_notice(I18n.T("You apply [] on [].", this, patient)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        }

        if (iscarbon(patient))
        {
            if (!Dm.truthy(await heal_carbon(patient as mob_living_carbon, user, healed_zone)))
                return;
        }
        else if (isanimal_or_basicmob(patient))
        {
            if (!Dm.truthy(await heal_simplemob(patient, user)))
                return;
        }
        else
        {
            throw Dm.CRASH(Dm.text("Stack medical item healing a non-carbon, non-animal mob [] ([])", patient, patient.GetType()), "code/game/objects/items/stacks/medical.dm", 195);
        }
        if (!Dm.truthy(await use(1)) || !Dm.truthy(repeating) || Dm.num(amount) <= 0)
        {
            atom alert_loc = QDELETED(this) ? user : this;
            alert_loc.balloon_alert(user, Dm.truthy(repeating) ? I18n.T("all used up!") : I18n.T("treated []", parse_zone(healed_zone)));
            play_heal_end_sound(patient);
            return;
        }
        if (Dm.truthy(heal_continuous_sound) && (Dm.truthy(continuous) || !Dm.truthy(silent)))
            playsound(patient, heal_continuous_sound, 75, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);
        log_combat(user, patient, "healed", this);

        // first, just try looping
        // 1. we can keep healing the current target
        // 2. the user's changed their target (and thus we should heal that limb instead)
        object preferred_target = check_zone(user.zone_selected);
        if (Dm.truthy(try_heal_checks(patient, user, preferred_target, silent: TRUE)))
        {
            if (!Dm.eq(preferred_target, healed_zone))
                patient.balloon_alert(user, I18n.T("[] []...", apply_verb, parse_zone(preferred_target)));
            await try_heal(patient, user, preferred_target, TRUE, auto_change_zone, TRUE);
            return;
        }

        // second, handle what happens otherwise
        if (!iscarbon(patient))
        {
            // behavior 0: non-carbons have no limbs so we can assume they are fully healed
            patient.balloon_alert(user, I18n.T("fully treated"));
        }
        else if (Dm.truthy(auto_change_zone))
        {
            // behavior 1: automatically pick another zone to heal
            await try_heal_auto_change_zone(patient as mob_living_carbon, user, preferred_target, healed_zone);
        }
        else
        {
            // behavior 2: assess injury, giving the user time to manually pick another zone
            await try_heal_manual_target(patient as mob_living_carbon, user);
        }
        play_heal_end_sound(patient);
    }

    // ADAPTED: tg writes this heal_end_sound block out three times (medical.dm:200-205, :228-233, :509-514); one helper here.
    private void play_heal_end_sound(mob_living patient)
    {
        if (islist(heal_end_sound))
        {
            foreach ((object sound, object volume) in Dm.pairs((DmList)heal_end_sound))
                playsound(patient, sound, Dm.truthy(volume) ? volume : 75, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);
        }
        else if (Dm.truthy(heal_end_sound))
        {
            playsound(patient, heal_end_sound, 75, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);
        }
    }

    // tg: code/game/objects/items/stacks/medical.dm:236
    private async DmTask try_heal_auto_change_zone(mob_living_carbon patient = null, mob_living user = null, object preferred_target = null, object last_zone = null)
    {
        // PRIVATE_PROC(TRUE)

        DmList other_affected_limbs = Dm.list();
        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(patient.get_bodyparts()))
        {
            if (!Dm.truthy(try_heal_checks(patient, user, limb.body_zone, silent: TRUE)))
                continue;
            Dm.append(ref other_affected_limbs, limb.body_zone);
        }

        if (!Dm.truthy(Dm.length(other_affected_limbs)))
        {
            patient.balloon_alert(user, I18n.T("fully treated"));
            return;
        }

        object next_picked = Dm.@in(preferred_target, other_affected_limbs) ? preferred_target : other_affected_limbs[1];
        if (!Dm.eq(next_picked, last_zone))
            patient.balloon_alert(user, I18n.T("[] []...", apply_verb, parse_zone(next_picked)));
        await try_heal(patient, user, next_picked, silent: TRUE, auto_change_zone: TRUE, continuous: TRUE);
    }

    // tg: code/game/objects/items/stacks/medical.dm:254
    private async DmTask try_heal_manual_target(mob_living_carbon patient = null, mob_living user = null)
    {
        // PRIVATE_PROC(TRUE)

        patient.balloon_alert(user, I18n.T("assessing injury..."));
        if (!Dm.truthy(await do_after(user, 1 * SECONDS, patient)))
            return;
        object new_zone = check_zone(user.zone_selected);
        if (!Dm.truthy(try_heal_checks(patient, user, new_zone)))
            return;
        patient.balloon_alert(user, I18n.T("[] []...", apply_verb, parse_zone(new_zone)));
        await try_heal(patient, user, new_zone, silent: TRUE, auto_change_zone: FALSE, continuous: TRUE);
    }

    /// Checks if the passed patient can be healed by the passed user
    // tg: code/game/objects/items/stacks/medical.dm:267
    public virtual object can_heal(mob_living patient = null, mob_living user = null, object healed_zone = null, float? silent = null)
    {
        silent ??= FALSE;
        return patient.try_inject(user, healed_zone, injection_flags: Dm.truthy(silent) ? NONE : INJECT_TRY_SHOW_ERROR_MESSAGE);
    }

    /// Checks a bunch of stuff to see if we can heal the patient, including can_heal
    /// Gives a feedback if we can't ultimatly heal the patient (unless silent is TRUE)
    // tg: code/game/objects/items/stacks/medical.dm:272
    public virtual object try_heal_checks(mob_living patient = null, mob_living user = null, object healed_zone = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.@in(healed_zone, patient.get_all_limbs()))
            healed_zone = BODY_ZONE_CHEST;

        if (!Dm.truthy(can_heal(patient, user, healed_zone, silent)))
        {
            // has its own feedback
            return FALSE;
        }
        if (!Dm.truthy(works_on_dead) && Dm.eq(patient.stat, DEAD))
        {
            if (!Dm.truthy(silent))
                patient.balloon_alert(user, I18n.T("[] dead!", patient.p_theyre()));
            return FALSE;
        }

        if (iscarbon(patient))
        {
            mob_living_carbon carbon_patient = patient as mob_living_carbon;
            obj_item_bodypart affecting = carbon_patient.get_bodypart((string)healed_zone) as obj_item_bodypart;
            if (affecting == null) //Missing limb?
            {
                if (!Dm.truthy(silent))
                    carbon_patient.balloon_alert(user, I18n.T("no []!", parse_zone(healed_zone)));
                return FALSE;
            }
            if (!Dm.truthy(IS_ORGANIC_LIMB(affecting))) //Limb must be organic to be healed - RR
            {
                if (!Dm.truthy(silent))
                    carbon_patient.balloon_alert(user, I18n.T("[] is not organic!", affecting.plaintext_zone));
                return FALSE;
            }

            datum_wound_burn_flesh any_burn_wound = Dm.locate<datum_wound_burn_flesh>(affecting.wounds);
            bool can_heal_burn_wounds = (Dm.truthy(flesh_regeneration) || Dm.truthy(sanitization)) && Dm.truthy(any_burn_wound?.can_be_ointmented_or_meshed());
            bool can_suture_bleeding = Dm.truthy(stop_bleeding) && Dm.num(affecting.cached_bleed_rate) > 0;
            bool brute_to_heal = Dm.truthy(heal_brute) && Dm.num(affecting.brute_dam) > 0;
            bool burn_to_heal = Dm.truthy(heal_burn) && Dm.num(affecting.burn_dam) > 0;

            if (!brute_to_heal && !burn_to_heal && !can_heal_burn_wounds && !can_suture_bleeding)
            {
                if (!Dm.truthy(silent))
                {
                    if (!brute_to_heal && Dm.truthy(stop_bleeding)) // no brute, no bleeding
                        carbon_patient.balloon_alert(user, I18n.T("[] is not bleeding or bruised!", affecting.plaintext_zone));
                    else if (!burn_to_heal && (Dm.truthy(flesh_regeneration) || Dm.truthy(sanitization)) && any_burn_wound != null) // no burns, existing burn wounds are treated
                        carbon_patient.balloon_alert(user, I18n.T("[] is fully treated, give it time!", affecting.plaintext_zone));
                    else if (!Dm.truthy(affecting.brute_dam) && !Dm.truthy(affecting.burn_dam)) // not hurt at all
                        carbon_patient.balloon_alert(user, I18n.T("[] is not hurt!", affecting.plaintext_zone));
                    else // probably hurt in some way but we are not the right item for this
                        carbon_patient.balloon_alert(user, I18n.T("can't heal [] with []!", affecting.plaintext_zone, name));
                }
                return FALSE;
            }
            return TRUE;
        }

        if (isanimal_or_basicmob(patient))
        {
            if (!Dm.truthy(heal_brute)) // only brute can heal
            {
                if (!Dm.truthy(silent))
                    patient.balloon_alert(user, I18n.T("can't heal with []!", name));
                return FALSE;
            }
            if (!Dm.truthy(Dm.bitand(patient.mob_biotypes, MOB_ORGANIC)))
            {
                if (!Dm.truthy(silent))
                    patient.balloon_alert(user, I18n.T("no organic tissue!"));
                return FALSE;
            }
            if (Dm.eq(patient.health, patient.maxHealth))
            {
                if (!Dm.truthy(silent))
                    patient.balloon_alert(user, I18n.T("not hurt!"));
                return FALSE;
            }
            return TRUE;
        }

        return FALSE;
    }

    /// The healing effects on a carbon patient.
    /// Since we have extra details for dealing with bodyparts, we get our own fancy proc.
    /// Still returns TRUE on success and FALSE on fail
    // tg: code/game/objects/items/stacks/medical.dm:335
    public virtual async DmTask<object> heal_carbon(mob_living_carbon patient = null, mob_living user = null, object healed_zone = null)
    {
        obj_item_bodypart affecting = patient.get_bodypart((string)healed_zone) as obj_item_bodypart;
        user.visible_message(
            span_green(I18n.T("[] applies [] on []'s [].", user, this, patient, affecting.plaintext_zone)),
            span_green(I18n.T("You apply [] on []'s [].", this, patient, affecting.plaintext_zone)),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        float previous_damage = Dm.num(affecting.get_damage());
        if (Dm.truthy(await affecting.heal_damage(heal_brute, heal_burn)))
            await patient.update_damage_overlays();
        if (Dm.truthy(stop_bleeding))
        {
            foreach (datum_wound wound in Dm.each_cast<datum_wound>(affecting.wounds))
            {
                if (Dm.truthy(wound.blood_flow))
                {
                    await wound.adjust_blood_flow(-1 * Dm.num(stop_bleeding) * (user == patient ? 0.7f : 1));
                    break; // one at a time
                }
            }
            affecting.adjustBleedStacks(-1 * Dm.num(stop_bleeding), 0);
        }
        if (Dm.truthy(flesh_regeneration) || Dm.truthy(sanitization))
        {
            foreach (datum_wound_burn_flesh wound in Dm.each<datum_wound_burn_flesh>(affecting.wounds))
            {
                if (Dm.truthy(wound.can_be_ointmented_or_meshed()))
                {
                    wound.flesh_healing += Dm.num(flesh_regeneration);
                    wound.sanitization += Dm.num(sanitization);
                    break; // one at a time
                }
            }
        }
        await post_heal_effects(max(previous_damage - Dm.num(affecting.get_damage()), 0), patient, user);
        return TRUE;
    }

    /// Healing a simple mob, just an adjust_brute_loss() call
    // tg: code/game/objects/items/stacks/medical.dm:361
    public virtual async DmTask<object> heal_simplemob(mob_living patient = null, mob_living user = null)
    {
        await patient.adjust_brute_loss(-1 * (Dm.num(heal_brute) * Dm.num(patient.maxHealth) / 100));
        user.visible_message(
            span_green(I18n.T("[] applies [] on [].", user, this, patient)),
            span_green(I18n.T("You apply [] on [].", this, patient)),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        return TRUE;
    }

    ///Override this proc for special post heal effects. Only called for carbon patients.
    // tg: code/game/objects/items/stacks/medical.dm:371
    public virtual async DmTask post_heal_effects(object amount_healed = null, mob_living_carbon healed_mob = null, mob_living user = null)
    {
        return;
    }
}

// tg: code/game/objects/items/stacks/medical.dm:395
public partial class obj_item_stack_medical_wrap : obj_item_stack_medical
{
    // tg: code/game/objects/items/stacks/medical.dm:405
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_limb_applicable),
            Dm.named("apply_category", LIMB_ITEM_GAUZE),
            Dm.named("override_existing", TRUE),
            Dm.named("can_apply", Dm.@new<datum_callback>(this, nameof(can_gauze_limb))),
            Dm.named("do_apply", Dm.@new<datum_callback>(this, nameof(do_gauze_limb))),
            Dm.named("on_apply", Dm.@new<datum_callback>(this, nameof(on_gauze_limb)))));
        RegisterSignals(this, Dm.list(COMSIG_ITEM_APPLIED_TO_LIMB, COMSIG_ITEM_UNAPPLIED_FROM_LIMB), nameof(update_wounds));
        return dot;
    }

    // tg: code/game/objects/items/stacks/medical.dm:417
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return NONE; // uses component
    }

    /// Callback for limb applicability component
    // tg: code/game/objects/items/stacks/medical.dm:421
    public virtual object can_gauze_limb(mob user = null, mob_living patient = null, obj_item_bodypart limb = null)
    {
        bool can_gauze = Dm.truthy(always_applicable);
        foreach (datum_wound wound in Dm.each_cast<datum_wound>(limb.wounds))
        {
            if (Dm.truthy(Dm.bitand(wound.wound_flags, ACCEPTS_GAUZE)))
            {
                can_gauze = true;
                break;
            }
        }

        float dot = NONE;
        bool surgery_prepped = HAS_TRAIT(limb, TRAIT_READY_TO_OPERATE);
        if (!surgery_prepped)
            dot = Dm.bitor(dot, LIMB_APPLICABLE_BLOCK_ITEM_INTERACTION);

        if (!can_gauze)
        {
            if (!surgery_prepped)
                patient.balloon_alert(user, Dm.truthy(LAZYLEN(limb.wounds)) ? I18n.T("can't gauze!") : I18n.T("no wounds!"));
            dot = Dm.bitor(dot, LIMB_APPLICABLE_BLOCK_APPLICATION);
            return dot;
        }

        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (current_gauze != null && (Dm.num(current_gauze.absorption_capacity) * 1.2f > Dm.num(absorption_capacity))) // ignore if our new wrap is < 20% better than the current one, so someone doesn't bandage it 5 times in a row
        {
            if (!surgery_prepped)
                patient.balloon_alert(user, Dm.pick(I18n.T("already bandaged!"), I18n.T("bandage is clean!"))); // good enough
            dot = Dm.bitor(dot, LIMB_APPLICABLE_BLOCK_APPLICATION);
            return dot;
        }
        return dot;
    }

    /// Callback for limb applicability component
    // tg: code/game/objects/items/stacks/medical.dm:447
    public virtual async DmTask<object> do_gauze_limb(mob user = null, mob_living patient = null, obj_item_bodypart limb = null)
    {
        bool scanned_wound = false;
        bool any_wound = false;
        foreach (datum_wound wound in Dm.each_cast<datum_wound>(limb.wounds))
        {
            if (Dm.truthy(Dm.bitand(wound.wound_flags, ACCEPTS_GAUZE)))
            {
                any_wound = true;
                if (HAS_TRAIT(wound, TRAIT_WOUND_SCANNED))
                    scanned_wound = true;
            }
            if (any_wound && scanned_wound)
                break;
        }

        float treatment_delay = user == patient ? self_delay : other_delay;
        if (any_wound)
        {
            if (scanned_wound)
            {
                treatment_delay *= 0.5f;
                if (user == patient)
                    user.visible_message(
                        span_warning(I18n.T("[] begins expertly wrapping the wounds on []'s [] with []...", user, user.p_their(), limb.plaintext_zone, this)),
                        span_warning(I18n.T("You begin quickly wrapping the wounds on your [] with [], keeping the holo-image indications in mind...", limb.plaintext_zone, this)),
                        visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
                else
                    user.visible_message(
                        span_warning(I18n.T("[] begins expertly wrapping the wounds on []'s [] with []...", user, patient, limb.plaintext_zone, this)),
                        span_warning(I18n.T("You begin quickly wrapping the wounds on []'s [] with [], keeping the holo-image indications in mind...", patient, limb.plaintext_zone, this)),
                        visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            }
            else
            {
                if (user == patient)
                    user.visible_message(
                        span_warning(I18n.T("[] begins wrapping the wounds on [] [] with []...", user, user.p_their(), limb.plaintext_zone, this)),
                        span_warning(I18n.T("You begin wrapping the wounds on your [] with []...", limb.plaintext_zone, this)),
                        visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
                else
                    user.visible_message(
                        span_warning(I18n.T("[] begins wrapping the wounds on []'s [] with []...", user, patient, limb.plaintext_zone, this)),
                        span_warning(I18n.T("You begin wrapping the wounds on []'s [] with []...", patient, limb.plaintext_zone, this)),
                        visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            }
        }
        else
        {
            treatment_delay *= 1.25f;
            if (user == patient)
                user.visible_message(
                    span_notice(I18n.T("[] begins to wrap []'s [] with []...", user, patient, limb.plaintext_zone, this)),
                    span_notice(I18n.T("You begin to wrap your [] with []...", limb.plaintext_zone, this)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            else
                user.visible_message(
                    span_notice(I18n.T("[] begins to wrap []'s [] with []...", user, patient, limb.plaintext_zone, this)),
                    span_notice(I18n.T("You begin to wrap []'s [] with []...", patient, limb.plaintext_zone, this)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        }

        if (Dm.truthy(heal_begin_sound))
            playsound(this, heal_begin_sound, 75, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);

        if (!Dm.truthy(await do_after(user, treatment_delay, patient)))
            return FALSE;

        if (islist(heal_end_sound))
        {
            foreach ((object sound, object volume) in Dm.pairs((DmList)heal_end_sound))
                playsound(patient, sound, Dm.truthy(volume) ? volume : 75, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);
        }
        else if (Dm.truthy(heal_end_sound))
        {
            playsound(patient, heal_end_sound, 75, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);
        }
        return TRUE;
    }

    /// Callback for limb applicability component
    // tg: code/game/objects/items/stacks/medical.dm:519
    public virtual async DmTask on_gauze_limb(mob user = null, mob_living patient = null, obj_item_bodypart limb = null)
    {
        bool any_wound = false;
        foreach (datum_wound wound in Dm.each_cast<datum_wound>(limb.wounds))
        {
            if (Dm.truthy(Dm.bitand(wound.wound_flags, ACCEPTS_GAUZE)))
            {
                any_wound = true;
                break;
            }
        }

        patient.balloon_alert(user, I18n.T("wrapped []", limb.plaintext_zone));
        if (any_wound)
        {
            if (user == patient)
                user.visible_message(
                    span_green(I18n.T("[] applies [] to []'s [].", user, this, user.p_their(), limb.plaintext_zone)),
                    span_green(I18n.T("You bandage the wounds on your [].", limb.plaintext_zone)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            else
                user.visible_message(
                    span_green(I18n.T("[] applies [] to []'s [].", user, this, patient, limb.plaintext_zone)),
                    span_green(I18n.T("You bandage the wounds on []'s [].", patient, limb.plaintext_zone)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        }
        else
        {
            if (user == patient)
                user.visible_message(
                    span_notice(I18n.T("[] applies [] to []'s [].", user, this, user.p_their(), limb.plaintext_zone)),
                    span_notice(I18n.T("You wrap your [] with [].", limb.plaintext_zone, this)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            else
                user.visible_message(
                    span_notice(I18n.T("[] applies [] to []'s [].", user, this, patient, limb.plaintext_zone)),
                    span_notice(I18n.T("You wrap []'s [] with [].", patient, limb.plaintext_zone, this)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        }

        if (Dm.truthy(limb.cached_bleed_rate))
            await add_mob_blood(patient);
        // Dressing burns provides a "one-time" bonus to sanitization and healing
        // However, any notable infection will reduce the effectiveness of this bonus
        foreach (datum_wound_burn_flesh wound in Dm.each<datum_wound_burn_flesh>(limb.wounds))
        {
            wound.sanitization += Dm.num(sanitization) * (Dm.num(wound.infection) > 0.1f ? 0.2f : 1);
            wound.flesh_healing += Dm.num(flesh_regeneration) * (Dm.num(wound.infection) > 0.1f ? 0 : 1);
        }
    }

    /// Used via signal to update wounds
    // tg: code/game/objects/items/stacks/medical.dm:564
    [SignalHandler]
    public virtual void update_wounds(datum source = null, obj_item_bodypart limb = null)
    {
        worn_icon_state = loc == limb ? Dm.text("[][]", limb.body_zone, rand(1, 3)) : "nothing";
        foreach (datum_wound gauzed in Dm.each_cast<datum_wound>(limb.wounds))
            gauzed.update_inefficiencies();
        if (limb.owner is mob_living_carbon owner)
            Waiting.NoSleep(owner.update_damage_overlays());
    }
}

// tg: code/game/objects/items/stacks/medical.dm:571
public partial class obj_item_stack_medical_wrap_gauze : obj_item_stack_medical_wrap
{
    // tg: code/game/objects/items/stacks/medical.dm:595
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_cellulose), 2));
    }

    // tg: code/game/objects/items/stacks/medical.dm:598
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WIRECUTTER) || Dm.truthy(held_item?.get_sharpness()))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Shred into cloth";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/items/stacks/medical.dm:604
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        float base_cap = Dm.num(Dm.initial(this, (obj_item_stack_medical_wrap_gauze g) => g.absorption_capacity));
        if (!Dm.truthy(base_cap))
            return dot;

        string initial_name = (string)Dm.initial(this, (obj_item_stack_medical_wrap_gauze g) => g.name);
        if (Dm.num(absorption_capacity) <= 0)
            name = I18n.T("used []", initial_name);
        else if (Dm.num(absorption_capacity) <= base_cap * 0.2f)
            name = I18n.T("dirty []", initial_name);
        else if (Dm.num(absorption_capacity) <= base_cap * 0.8f)
            name = I18n.T("worn []", initial_name);
        else
            name = initial_name;
        return dot;
    }

    // tg: code/game/objects/items/stacks/medical.dm:619
    public override object can_merge(obj_item_stack check = null, float? inhand = null)
    {
        object dot = base.can_merge(check, inhand);
        if (!Dm.truthy(dot))
            return dot;
        // need to be in +- 0.5 dirtiness of each other
        // otherwise you can merge a completely used bandage with a brand new one, which would magically unuse it
        obj_item_stack_medical_wrap_gauze other = check as obj_item_stack_medical_wrap_gauze;
        if (Dm.num(other.absorption_capacity) < Dm.num(absorption_capacity) - 0.25f || Dm.num(other.absorption_capacity) > Dm.num(absorption_capacity) + 0.25f)
            return FALSE;
        return dot;
    }

    // tg: code/game/objects/items/stacks/medical.dm:629
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = await base.wash(clean_types, __extra);
        if (!Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_HARD_DECAL))) // gotta scrub realllly hard to clean gauze
            return dot;
        times_cleaned += 1;
        float clean_to = Dm.num(Dm.initial(this, (obj_item_stack_medical_wrap_gauze g) => g.absorption_capacity)) * (3 / (times_cleaned + 3));
        if (Dm.num(absorption_capacity) < clean_to)
        {
            absorption_capacity = clean_to;
            update_appearance(UPDATE_NAME);
            dot = Dm.bitor(dot, COMPONENT_CLEANED);
        }
        return dot;
    }

    // tg: code/game/objects/items/stacks/medical.dm:643
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER) || Dm.truthy(tool.get_sharpness()))
        {
            if (Dm.num(get_amount()) < 2)
            {
                balloon_alert(user, I18n.T("not enough gauze!"));
                return ITEM_INTERACT_BLOCKING;
            }
            Dm.@new<obj_item_stack_sheet_cloth>(tool.drop_location());
            if (Dm.truthy(await IsReachableBy(user)))
                user.visible_message(span_notice(I18n.T("[] cuts [] into pieces of cloth with [].", user, this, tool)),
                    span_notice(I18n.T("You cut [] into pieces of cloth with [].", this, tool)),
                    span_hear(I18n.T("You hear cutting.")));
            else //telekinesis
                visible_message(span_notice(I18n.T("[] cuts [] into pieces of cloth.", tool, this)),
                    blind_message: span_hear(I18n.T("You hear cutting.")));
            await use(Dm.num(absorption_capacity) <= Dm.num(Dm.initial(this, (obj_item_stack_medical_wrap_gauze g) => g.absorption_capacity)) * 0.5f ? 1 : 2);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/stacks/medical.dm:662
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins tightening [] around [] neck! It looks like [] forgot how to use medical supplies!", user, this, user.p_their(), user.p_they())));
        return OXYLOSS;
    }
}

// M8 slice D: the other medical stacks (medical.dm:374-394, :690-965). Their healing is the shared heal loop above with each
// type's numbers (generated var blocks); the procs here are their own.

// tg: code/game/objects/items/stacks/medical.dm:375
public partial class obj_item_stack_medical_bruise_pack : obj_item_stack_medical
{
    // tg: code/game/objects/items/stacks/medical.dm:388
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_medicine_c2_libital), 10));
    }

    // tg: code/game/objects/items/stacks/medical.dm:391
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is bludgeoning []self with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        return BRUTELOSS;
    }
}

// tg: code/game/objects/items/stacks/medical.dm:690
public partial class obj_item_stack_medical_suture : obj_item_stack_medical
{
    // tg: code/game/objects/items/stacks/medical.dm:710
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_medicine_spaceacillin), 2));
    }
}

// tg: code/game/objects/items/stacks/medical.dm:714
public partial class obj_item_stack_medical_suture_medicated : obj_item_stack_medical_suture
{
    // tg: code/game/objects/items/stacks/medical.dm:721
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_medicine_polypyr), 1));
    }
}

// tg: code/game/objects/items/stacks/medical.dm:725
public partial class obj_item_stack_medical_ointment : obj_item_stack_medical
{
    // tg: code/game/objects/items/stacks/medical.dm:743
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_medicine_c2_lenturi), 10));
    }

    // tg: code/game/objects/items/stacks/medical.dm:746
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is squeezing [] into [] mouth! []n't [] know that stuff is toxic?", user, this, user.p_their(), capitalize(user.p_do()), user.p_they())));
        return TOXLOSS;
    }
}

// tg: code/game/objects/items/stacks/medical.dm:751
public partial class obj_item_stack_medical_mesh : obj_item_stack_medical
{
    // tg: code/game/objects/items/stacks/medical.dm:774
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(amount, max_amount)) //only seal full mesh packs
        {
            is_open = FALSE;
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/objects/items/stacks/medical.dm:780
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_medicine_spaceacillin), 2));
    }

    // tg: code/game/objects/items/stacks/medical.dm:783
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(is_open))
            return await base.update_icon_state(__extra);
        icon_state = "regen_mesh_closed";
        return null;
    }

    // tg: code/game/objects/items/stacks/medical.dm:788
    public override object try_heal_checks(mob_living patient = null, mob_living user = null, object healed_zone = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(is_open))
        {
            if (!Dm.truthy(silent))
                balloon_alert(user, I18n.T("open it first!"));
            return FALSE;
        }
        return base.try_heal_checks(patient, user, healed_zone, silent);
    }

    // tg: code/game/objects/items/stacks/medical.dm:795
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(is_open))
        {
            balloon_alert(user, I18n.T("open it first!"));
            return CLICK_ACTION_BLOCKING;
        }
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/stacks/medical.dm:801
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_open) && user.get_inactive_held_item() == this)
        {
            balloon_alert(user, I18n.T("open it first!"));
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/stacks/medical.dm:807
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_open))
        {
            is_open = TRUE;
            balloon_alert(user, I18n.T("opened"));
            update_appearance();
            playsound(this, Dm.resource("sound/items/poster/poster_ripped.ogg"), 20, TRUE);
            return null;
        }
        return await base.attack_self(user, modifiers, __extra);
    }
}

// tg: code/game/objects/items/stacks/medical.dm:817
public partial class obj_item_stack_medical_mesh_advanced : obj_item_stack_medical_mesh
{
    // tg: code/game/objects/items/stacks/medical.dm:826
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_aloejuice), 1));
    }

    // tg: code/game/objects/items/stacks/medical.dm:829
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(is_open))
            return await base.update_icon_state(__extra);
        icon_state = "aloe_mesh_closed";
        return null;
    }
}

// tg: code/game/objects/items/stacks/medical.dm:835
public partial class obj_item_stack_medical_aloe : obj_item_stack_medical
{
    // tg: code/game/objects/items/stacks/medical.dm:851
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_badrecipe), rand(10 * SECONDS, 15 * SECONDS), FALSE)); // gaps H2
        return dot;
    }

    // tg: code/game/objects/items/stacks/medical.dm:855
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_aloejuice), 1));
    }
}

// tg: code/game/objects/items/stacks/medical.dm:862
public partial class obj_item_stack_medical_bone_gel : obj_item_stack_medical
{
    // tg: code/game/objects/items/stacks/medical.dm:876
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_bone_dust), 10), Dm.kv(typeof(datum_reagent_carbon), 10));
    }

    // tg: code/game/objects/items/stacks/medical.dm:879
    public override object get_surgery_tool_overlay(object tray_extended = null)
    {
        return "gel" + (Dm.truthy(tray_extended) ? "" : "_out");
    }

    // tg: code/game/objects/items/stacks/medical.dm:882
    [DmParams("patient", "user")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        target_mob.balloon_alert(user, I18n.T("no fractures!"));
        return null;
    }

    // tg: code/game/objects/items/stacks/medical.dm:886
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (!iscarbon(user))
            return null;
        mob_living_carbon patient = user as mob_living_carbon;
        patient.visible_message(span_suicide(I18n.T("[] is squirting all of [] into [] mouth! That's not proper procedure! It looks like [] trying to commit suicide!", patient, this, patient.p_their(), patient.p_theyre())));
        if (!Dm.truthy(await do_after(patient, 2 * SECONDS)))
        {
            patient.visible_message(span_suicide(I18n.T("[] screws up like an idiot and still dies anyway!", patient)));
            return BRUTELOSS;
        }

        await patient.emote("scream");
        foreach (obj_item_bodypart bone in Dm.each_cast<obj_item_bodypart>(patient.get_bodyparts()))
        {
            // fine to just, use these raw, its a meme anyway
            var oof_ouch = Dm.@new<datum_wound_blunt_bone_severe>();
            await oof_ouch.apply_wound(bone, wound_source: "bone gel");
            var oof_OUCH = Dm.@new<datum_wound_blunt_bone_critical>();
            await oof_OUCH.apply_wound(bone, wound_source: "bone gel");
        }
        foreach (object zone in Dm.each(patient.get_all_limbs()))
            await patient.apply_damage(60, BRUTE, zone);
        await use(1);
        return BRUTELOSS;
    }
}

// tg: code/game/objects/items/stacks/medical.dm:911
public partial class obj_item_stack_medical_poultice : obj_item_stack_medical
{
    // tg: code/game/objects/items/stacks/medical.dm:933
    public override async DmTask post_heal_effects(object amount_healed = null, mob_living_carbon healed_mob = null, mob_living user = null)
    {
        await base.post_heal_effects(amount_healed, healed_mob, user);
        playsound(this, Dm.resource("sound/misc/soggy.ogg"), 30, TRUE);
        await healed_mob.adjust_oxy_loss(amount_healed);
    }
}

// tg: code/game/objects/items/stacks/medical.dm:939
public partial class obj_item_stack_medical_bandage : obj_item_stack_medical
{
    // tg: code/game/objects/items/stacks/medical.dm:955
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_medicine_c2_libital), 2));
    }
}
