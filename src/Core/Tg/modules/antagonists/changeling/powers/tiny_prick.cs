// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/tiny_prick.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the sting base (preparing and retracting the sting with its HUD icon, the sting's range by
// pathfinding, the feedback) and every sting: transformation, false armblade (with its dull blade), extract DNA, mute, blind,
// hallucination and cryogenic. The var blocks are generated (D111).

// tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:1
public partial class datum_action_changeling_sting : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:7
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        // SHOULD_CALL_PARENT(FALSE) //We are snowflaked from parent
        mob user = owner;
        if (user == null || user.mind == null)
            return null;
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        if (changeling == null)
            return null;
        if (changeling.chosen_sting == null)
            await set_sting(user);
        else
            unset_sting(user);
        return null;
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:21
    public virtual async DmTask set_sting(mob user = null)
    {
        to_chat(user, span_notice(I18n.T("We prepare our sting. Alt+click or click the middle mouse button on a target to sting them.")));
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        changeling.chosen_sting = this;

        atom_movable_screen_ling_sting sting = user.hud_used?.screen_objects[HUD_CHANGELING_STING] as atom_movable_screen_ling_sting;
        if (sting != null)
        {
            sting.icon_state = button_icon_state;
            sting.SetInvisibility(0, id: type);
        }
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:31
    public virtual void unset_sting(mob user = null)
    {
        to_chat(user, span_warning(I18n.T("We retract our sting, we can't sting anyone for now.")));
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        changeling.chosen_sting = null;

        atom_movable_screen_ling_sting sting = user.hud_used?.screen_objects[HUD_CHANGELING_STING] as atom_movable_screen_ling_sting;
        if (sting != null)
        {
            sting.icon_state = null;
            sting.RemoveInvisibility(type);
        }
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:47
    public override async DmTask<object> can_sting(mob_living user = null, mob_living target = null)
    {
        if (!Dm.truthy(await base.can_sting(user, target)))
            return null;
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        if (changeling.chosen_sting == null)
            to_chat(user, I18n.T("We haven't prepared our sting yet!"));
        if (!iscarbon(target))
            return null;
        if (!isturf(user.loc))
            return null;
        if (!Dm.truthy(length(await get_path_to(user, target, max_distance: changeling.sting_range, simulated_only: FALSE))))
            return null; // no path within the sting's range is found. what a weird place to use the pathfinding system
        if (Dm.truthy(IS_CHANGELING(target)))
        {
            sting_feedback(user, target);
            changeling.chem_charges -= chemical_cost;
        }
        return 1;
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:64
    public override object sting_feedback(mob_living user = null, mob_living target = null)
    {
        if (target == null)
            return null;
        to_chat(user, span_notice(I18n.T("We stealthily sting [].", target.name)));
        if (Dm.truthy(IS_CHANGELING(target)))
            to_chat(target, span_warning(I18n.T("You feel a tiny prick.")));
        return 1;
    }
}

// tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:41
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:41
    public virtual void unset_sting()
    {
        if (mind != null)
        {
            datum_antagonist_changeling changeling = mind.has_antag_datum(typeof(datum_antagonist_changeling)) as datum_antagonist_changeling;
            if (changeling?.chosen_sting != null)
                changeling.chosen_sting.unset_sting(this);
        }
    }
}


// tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:73
public partial class datum_action_changeling_sting_transformation : datum_action_changeling_sting
{
    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:90
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.Grant(grant_to, __extra);
        await build_all_button_icons(UPDATE_BUTTON_NAME);
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:94
    public override void update_button_name(atom_movable_screen_movable_action_button button = null, float? force = null, params object[] __extra)
    {
        base.update_button_name(button, force, __extra);
        button.desc += I18n.T(" Lasts [] for humans, but duration is paused while dead or in stasis.", DisplayTimeText(sting_duration));
        button.desc += I18n.T(" Costs [] chemicals.", chemical_cost);
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:99
    public override object Destroy(float? force = null)
    {
        selected_dna = null;
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:103
    public override async DmTask set_sting(mob user = null)
    {
        selected_dna = null;
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        datum_changeling_profile new_selected_dna = await changeling.select_dna() as datum_changeling_profile;
        if (QDELETED(this) || QDELETED(changeling) || QDELETED(user))
            return;
        if (new_selected_dna == null || changeling.chosen_sting != null || selected_dna != null) // selected other sting or other DNA while sleeping
            return;
        if (Dm.truthy(verify_valid_species) && Dm.@in(TRAIT_NO_DNA_COPY, ((datum_species)new_selected_dna.dna.species).inherent_traits))
        {
            user.balloon_alert(user, I18n.T("dna incompatible!"));
            return;
        }
        selected_dna = new_selected_dna;
        await base.set_sting(user);
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:117
    public override async DmTask<object> can_sting(mob_living user = null, mob_living target = null)
    {
        object dot = null;
        dot = await base.can_sting(user, target);
        if (!Dm.truthy(dot))
            return dot;
        // Similar checks here are ran to that of changeling can_absorb_dna -
        // Logic being that if their DNA is incompatible with us, it's also bad for transforming
        if (!iscarbon(target)
            || !Dm.truthy(target.has_dna())
            || HAS_TRAIT(target, TRAIT_HUSK)
            || HAS_TRAIT(target, TRAIT_BADDNA)
            || (HAS_TRAIT(target, TRAIT_NO_DNA_COPY) && !HAS_TRAIT(target, TRAIT_LESSER_HUMANOID))) // sure, go ahead, make a monk-clone
        {
            user.balloon_alert(user, I18n.T("incompatible DNA!"));
            return FALSE;
        }
        if (Dm.truthy(target.has_status_effect(typeof(datum_status_effect_temporary_transformation_trans_sting))))
        {
            user.balloon_alert(user, I18n.T("already transformed!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:135
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        float final_duration = sting_duration;
        string final_message = span_notice(I18n.T("We transform [] into [].", target, selected_dna.dna.real_name));
        if (HAS_TRAIT(target, TRAIT_LESSER_HUMANOID))
        {
            final_duration = INFINITY;
            final_message = span_warning(I18n.T("Our genes cry out as we transform the lesser form of [] into [] permanently!", target, selected_dna.dna.real_name));
        }

        if (Dm.truthy(await target.apply_status_effect(typeof(datum_status_effect_temporary_transformation_trans_sting), final_duration, selected_dna.dna)))
        {
            await base.sting_action(user, target);
            log_combat(user, target, "stung", "transformation sting", Dm.text(" new identity is '[]'", selected_dna.dna.real_name));
            to_chat(user, final_message);
            return TRUE;
        }
        return FALSE;
    }
}

// tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:149
public partial class datum_action_changeling_sting_false_armblade : datum_action_changeling_sting
{
    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:162
    public override async DmTask<object> can_sting(mob_living user = null, mob_living target = null)
    {
        if (!Dm.truthy(await base.can_sting(user, target)))
            return null;
        if (isliving(target))
        {
            mob_living L = target;
            if ((HAS_TRAIT(L, TRAIT_HUSK)) || !Dm.truthy(L.has_dna()))
            {
                user.balloon_alert(user, I18n.T("incompatible DNA!"));
                return FALSE;
            }
        }
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:172
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {

        obj_item held = target.get_active_held_item() as obj_item;
        if (held != null && !Dm.truthy(await target.dropItemToGround(held)))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to [] hand, we cannot grow a false armblade over it!", held, target.p_their())));
            return null;
        }

        await base.sting_action(user, target);
        log_combat(user, target, "stung", @object: "false armblade sting");
        if (HAS_TRAIT(target, TRAIT_LESSER_HUMANOID))
            to_chat(user, span_notice(I18n.T("Our genes cry out as we sting []!", target.name)));

        obj_item_melee_arm_blade_false blade = Dm.@new<obj_item_melee_arm_blade_false>(target, 1);
        await target.put_in_hands(blade);
        target.visible_message(span_warning(I18n.T("A grotesque blade forms around []'s arm!", target.name)), span_userdanger(I18n.T("Your arm twists and mutates, transforming into a horrific monstrosity!")), span_hear(I18n.T("You hear organic matter ripping and tearing!")));
        playsound(target, Dm.resource("sound/effects/blob/blobattack.ogg"), 30, TRUE);

        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_fake), target, blade), 1 * MINUTES, file: "code/modules/antagonists/changeling/powers/tiny_prick.dm", line: 189);
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:192
    public virtual async DmTask remove_fake(mob target = null, obj_item_melee_arm_blade_false blade = null)
    {
        playsound(target, Dm.resource("sound/effects/blob/blobattack.ogg"), 30, TRUE);
        target.visible_message(span_warning(I18n.T("With a sickening crunch, [] reforms [] [] into an arm!", target, target.p_their(), blade.name)),
            span_warning(I18n.T("[] reforms back to normal.", blade)), span_italics(I18n.T("You hear organic matter ripping and tearing!")));

        qdel(blade);
        await target.update_held_items();
    }
}

// tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:200
public partial class datum_action_changeling_sting_extract_dna : datum_action_changeling_sting
{
    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:208
    public override async DmTask<object> can_sting(mob_living user = null, mob_living target = null)
    {
        if (Dm.truthy(await base.can_sting(user, target)))
        {
            datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
            return changeling.can_absorb_dna(target as mob_living_carbon_human);
        }
        return null;
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:213
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        log_combat(user, target, "stung", "extraction sting");
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        if (!Dm.truthy(changeling.has_profile_with_dna(((mob_living_carbon_human)target).dna)))
            await changeling.add_new_profile((mob_living_carbon_human)target);
        return TRUE;
    }
}

// tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:221
public partial class datum_action_changeling_sting_mute : datum_action_changeling_sting
{
    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:229
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        log_combat(user, target, "stung", "mute sting");
        await target.adjust_silence(1 * MINUTES);
        return TRUE;
    }
}

// tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:235
public partial class datum_action_changeling_sting_blind : datum_action_changeling_sting
{
    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:243
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        obj_item_organ_eyes eyes = target.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes == null)
        {
            user.balloon_alert(user, I18n.T("no eyes!"));
            return FALSE;
        }

        if (Dm.truthy(IS_ROBOTIC_ORGAN(eyes)))
        {
            user.balloon_alert(user, I18n.T("robotic eyes!"));
            return FALSE;
        }

        await base.sting_action(user, target);
        log_combat(user, target, "stung", "blind sting");
        to_chat(target, span_danger(I18n.T("Your eyes burn horrifically!")));
        await eyes.apply_organ_damage(eyes.maxHealth * 0.8f);
        await target.adjust_temp_blindness(40 * SECONDS);
        await target.set_eye_blur_if_lower(80 * SECONDS);
        return TRUE;
    }
}

// tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:261
public partial class datum_action_changeling_sting_lsd : datum_action_changeling_sting
{
    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:270
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        log_combat(user, target, "stung", "LSD sting");
        _addtimer(Dm.@new<datum_callback>(this, nameof(hallucination_time), target), rand(30 * SECONDS, 60 * SECONDS), file: "code/modules/antagonists/changeling/powers/tiny_prick.dm", line: 273);
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:276
    public virtual async DmTask hallucination_time(mob_living_carbon target = null)
    {
        if (QDELETED(this) || QDELETED(target))
            return;
        await target.adjust_hallucinations(180 * SECONDS);
    }
}

// tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:281
public partial class datum_action_changeling_sting_cryo : datum_action_changeling_sting
{
    // tg: code/modules/antagonists/changeling/powers/tiny_prick.dm:289
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        log_combat(user, target, "stung", "cryo sting");
        if (target.reagents != null)
            await ((datum_reagents)target.reagents).add_reagent(typeof(datum_reagent_consumable_frostoil), 30);
        return TRUE;
    }
}
