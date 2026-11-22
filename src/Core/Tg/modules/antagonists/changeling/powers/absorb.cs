// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/absorb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the Absorb DNA power (the neck grab, the three stages of the proboscis with the absorbing loop,
// the profile, the true absorption, the nutrition and languages, the stolen memories, objectives and speech, a changeling victim's
// power and its fall), the hivemind member the absorbed player becomes (an imaginary friend of the changeling's that speaks in the
// changeling hivemind, can exit it, and is returned to its body on revival), the exit action and the changeling's eject action.
// The var blocks are generated (D111).

// tg: code/modules/antagonists/changeling/powers/absorb.dm:1
public partial class datum_action_changeling_absorb_dna : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/absorb.dm:12
    [DmParams("owner")]
    public override async DmTask<object> can_sting(mob_living owner = null, mob_living target = null)
    {
        if (!Dm.truthy(await base.can_sting(owner, target)))
            return null;

        if (Dm.truthy(is_absorbing))
        {
            owner.balloon_alert(owner, I18n.T("already absorbing!"));
            return null;
        }

        if (owner.pulling == null || !iscarbon(owner.pulling))
        {
            owner.balloon_alert(owner, I18n.T("needs grab!"));
            return null;
        }
        if (owner.grab_state <= GRAB_NECK)
        {
            owner.balloon_alert(owner, I18n.T("needs tighter grip!"));
            return null;
        }

        mob_living_carbon target__1 = owner.pulling as mob_living_carbon;
        datum_antagonist_changeling changeling = IS_CHANGELING(owner) as datum_antagonist_changeling;
        return changeling.can_absorb_dna(target__1 as mob_living_carbon_human);
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:31
    [DmParams("owner")]
    public override async DmTask<object> sting_action(mob_living owner = null, mob_living target = null)
    {
        // SHOULD_CALL_PARENT(FALSE): the only reason to call parent is for proper blackbox logging, and we do that ourselves in a snowflake way

        datum_antagonist_changeling changeling = IS_CHANGELING(owner) as datum_antagonist_changeling;
        mob_living_carbon_human target__1 = owner.pulling as mob_living_carbon_human;
        is_absorbing = TRUE;

        if (!Dm.truthy(await attempt_absorb(target__1)))
            return null;

        SSblackbox.record_feedback("nested tally", "changeling_powers", 1, Dm.list("Absorb DNA", "4"));
        owner.visible_message(span_danger(I18n.T("[] sucks the fluids from []!", owner, target__1)), span_notice(I18n.T("We have absorbed [].", target__1)));

        if (target__1.client != null && target__1.mind != null)
        {
            mob_eye_imaginary_friend_hivemind new_member = Dm.@new<mob_eye_imaginary_friend_hivemind>(target__1.loc);
            await new_member.AddComponent(typeof(datum_component_temporary_body), Dm.named("old_mind", target__1.mind), Dm.named("return_on_revive", TRUE));
            new_member.real_name = target__1.real_name;
            new_member.gender = target__1.gender;
            new_member.human_icon = await get_flat_human_icon(null, target__1.mind.assigned_role, target__1.client.prefs) as icon;
            new_member.PossessByPlayer(target__1.ckey);
            await new_member.attach_to_owner(owner);
        }

        float true_absorbtion = Dm.num(target__1.client != null || target__1.mind != null || target__1.last_mind != null);
        if (!Dm.truthy(true_absorbtion))
            to_chat(owner, span_changeling(span_bold(I18n.T("You absorb [], but their weak DNA is not enough to satisfy your hunger.", target__1))));

        if (!Dm.truthy(changeling.has_profile_with_dna(target__1.dna)))
        {
            await changeling.add_new_profile(target__1);
            if (Dm.truthy(true_absorbtion))
                changeling.true_absorbs++;
        }

        if (owner.nutrition < NUTRITION_LEVEL_WELL_FED)
            await owner.set_nutrition(min((owner.nutrition + target__1.nutrition), NUTRITION_LEVEL_WELL_FED));

        // Absorb a lizard, speak Draconic.
        owner.copy_languages(target__1, LANGUAGE_ABSORB);

        if (target__1.mind != null && owner.mind != null)//if the victim and owner have minds
            await absorb_memories(target__1);

        qdel(absorbing_loop);
        is_absorbing = FALSE;

        changeling.adjust_chemicals(10);
        if (Dm.truthy(true_absorbtion))
            changeling.can_respec++;

        if (target__1.stat != DEAD)
            target__1.investigate_log("has died from being changeling absorbed.", INVESTIGATE_DEATHS);
        await target__1.death(FALSE);
        target__1.Drain();
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:84
    public virtual async DmTask absorb_memories(mob_living_carbon_human target = null)
    {
        datum_mind suckedbrain = target.mind;

        datum_antagonist_changeling changeling = IS_CHANGELING(owner) as datum_antagonist_changeling;

        foreach (object memory_type in Dm.each(suckedbrain.memories))
        {
            datum_memory stolen_memory = suckedbrain.memories[memory_type] as datum_memory;
            changeling.stolen_memories[stolen_memory.name] = stolen_memory.generate_story(STORY_CHANGELING_ABSORB, STORY_FLAG_NO_STYLE);
        }
        suckedbrain.wipe_memory();

        foreach (datum_antagonist antagonist_datum in Dm.each_cast<datum_antagonist>(suckedbrain.antag_datums))
        {
            DmList all_objectives = antagonist_datum.objectives.Copy();
            if (Dm.truthy(antagonist_datum.antag_memory))
                changeling.antag_memory += I18n.T("[]'s antagonist memories: [].", target, antagonist_datum.antag_memory);
            if (!Dm.truthy(LAZYLEN(all_objectives)))
                continue;
            changeling.antag_memory += I18n.T(" Objectives:");
            float obj_count = 1;
            foreach (datum_objective objective in Dm.each_cast<datum_objective>(all_objectives))
            {
                if (objective == null) //nulls? in my objective list? it's more likely than you think.
                    continue;
                changeling.antag_memory += I18n.T(" Objective #[]: [].", obj_count++, objective.explanation_text);
                DmList other_owners = Dm.sub(objective.get_owners(), suckedbrain) as DmList;
                if (!Dm.truthy(other_owners.len))
                    continue;
                foreach (datum_mind conspirator in Dm.each_cast<datum_mind>(other_owners))
                    changeling.antag_memory += I18n.T(" Objective Conspirator: [].", conspirator.name);
            }
        }
        changeling.antag_memory += I18n.T(" That's all [] had. ", target);

        //Some of target's recent speech, so the changeling can attempt to imitate them better.
        //Recent as opposed to all because rounds tend to have a LOT of text.

        DmList recent_speech = target.copy_recent_speech();

        if (Dm.truthy(recent_speech.len))
        {
            changeling.antag_memory += I18n.T("Some of []'s speech patterns, we should study these to better impersonate []: ", target, target.p_them());
            to_chat(owner, span_boldnotice(I18n.T("Some of []'s speech patterns, we should study these to better impersonate []!", target, target.p_them())));
            foreach (object spoken_memory in Dm.each(recent_speech))
            {
                changeling.antag_memory += Dm.text(" \"[]\"", spoken_memory);
                to_chat(owner, span_notice(Dm.text("\"[]\"", spoken_memory)));
            }
            changeling.antag_memory += I18n.T(". We have no more knowledge of []'s speech patterns. ", target);
            to_chat(owner, span_boldnotice(I18n.T("We have no more knowledge of []'s speech patterns.", target)));
        }

        datum_antagonist_changeling target_ling = IS_CHANGELING(target) as datum_antagonist_changeling;
        if (target_ling != null)//If the target was a changeling, suck out their extra juice and objective points!
        {
            to_chat(owner, span_boldnotice(I18n.T("[] was one of us. We have absorbed their power.", target)));

            // Gain half of their genetic points.
            float genetic_points_to_add = round(target_ling.total_genetic_points / 2);
            changeling.genetic_points += genetic_points_to_add;
            changeling.total_genetic_points += genetic_points_to_add;

            // And half of their chemical charges.
            float chems_to_add = round(target_ling.total_chem_storage / 2);
            changeling.adjust_chemicals(chems_to_add);
            changeling.total_chem_storage += chems_to_add;

            // And of course however many they've absorbed, we've absorbed
            changeling.absorbed_count += target_ling.absorbed_count;

            // Lastly, make them not a ling anymore. (But leave their objectives for round-end purposes).
            DmList copied_objectives = target_ling.objectives.Copy();
            await target.mind.remove_antag_datum(typeof(datum_antagonist_changeling));
            datum_antagonist_fallen_changeling fallen = await target.mind.add_antag_datum(typeof(datum_antagonist_fallen_changeling)) as datum_antagonist_fallen_changeling;
            fallen.objectives = copied_objectives;
        }
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:151
    public virtual async DmTask<object> attempt_absorb(mob_living_carbon_human target = null)
    {
        for (float absorbing_iteration = 1, absorbing_iteration__end = 3; absorbing_iteration <= absorbing_iteration__end; absorbing_iteration++)
        {
            switch (absorbing_iteration)
            {
                case 1:
                    to_chat(owner, span_notice(I18n.T("This creature is compatible. We must hold still...")));
                    break;
                case 2:
                    owner.visible_message(span_warning(I18n.T("[] extends a proboscis!", owner)), span_notice(I18n.T("We extend a proboscis.")));
                    break;
                case 3:
                    absorbing_loop = Dm.@new<datum_looping_sound_changeling_absorb>(owner, Dm.named("start_immediately", TRUE));
                    owner.visible_message(span_danger(I18n.T("[] stabs [] with the proboscis!", owner, target)), span_notice(I18n.T("We stab [] with the proboscis.", target)));
                    to_chat(target, span_userdanger(I18n.T("You feel a sharp stabbing pain!")));
                    await target.take_overall_damage(40);
                    break;
            }

            SSblackbox.record_feedback("nested tally", "changeling_powers", 1, Dm.list("Absorb DNA", Dm.text("[]", absorbing_iteration)));
            if (!Dm.truthy(await do_after(owner, 15 * SECONDS, target, cog_icon: null)))
            {
                owner.balloon_alert(owner, I18n.T("interrupted!"));
                qdel(absorbing_loop);
                is_absorbing = FALSE;
                return FALSE;
            }
        }
        return TRUE;
    }
}

// tg: code/modules/antagonists/changeling/powers/absorb.dm:172
public partial class mob_eye_imaginary_friend_hivemind : mob_eye_imaginary_friend
{
    // tg: code/modules/antagonists/changeling/powers/absorb.dm:181
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CHANGELING_HIVEMIND, INNATE_TRAIT);
        datum_action_innate_exit_hivemind exit_action = Dm.@new<datum_action_innate_exit_hivemind>(this);
        Waiting.NoSleep(exit_action.Grant(this));
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:187
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        client.eye = (owner is var or__1 && Dm.truthy(or__1) ? (atom)or__1 : this);
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:191
    public override void greet()
    {
        string greet_message = "";
        greet_message += separator_hr(span_danger(span_slightly_larger(I18n.T("You are absorbed by the changeling!"))));
        greet_message += I18n.T("You are now a part of the changeling hivemind, and can communicate with them freely by speaking. Your knowledge may be useful to them...");
        greet_message += I18n.T("<br>You may also choose to <a href='byond://?src=[];exit_hivemind=1'>exit the hivemind</a> if you prefer to observe instead.", REF(this));
        greet_message += I18n.T("<br>Either way, if your body or brain is revived, you will be returned to it as normal.");

        to_chat(this, boxed_message(span_changeling(greet_message)));

        if (!Dm.truthy(alerted))
        {
            alerted = TRUE;
            alert_hivemind(I18n.T("You sense the presence of [] in the hivemind.", real_name));
        }
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:207
    [DmParams("message", "range", "source", "bubble_type", "spans", "message_language", "message_mods", "forced")]
    public override async DmTask send_speech(object message = null, float? range = null, atom_movable source = null, object bubble_type = null, DmList spans = null, object message_language = null, DmList message_mods = null, object forced = null, object tts_message = null, DmList tts_filter = null)
    {
        message_mods ??= Dm.list();
        // chops any message mods, even though they won't actually do anything
        message = get_message_mods(message, message_mods);
        // forces message through the changeling saymode
        datum_saymode_changeling saymode = SSradio.saymodes[Dm.initial(typeof(datum_saymode_changeling), (datum_saymode_changeling p) => p.key)] as datum_saymode_changeling;
        await saymode.handle_message(this, message, spans, message_language, message_mods);
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:214
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        object dot = await base.Topic(href, href_list);
        if (Dm.truthy(href_list["exit_hivemind"]) && !QDELETED(this))
            await exit_hivemind();
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:219
    public virtual async DmTask exit_hivemind()
    {
        object response = await tgui_alert(this, I18n.T("Are you sure you want to exit the hivemind? You can't re-enter it, though you can still be revived."), I18n.T("Confirm Exit"), Dm.list(I18n.T("Exit"), I18n.T("Stay")));
        if (!Dm.eq(response, I18n.T("Exit")) || QDELETED(this))
            return;
        ghostize(TRUE);
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:226
    public override object ghostize(float? can_reenter_corpse = null, float? forced = null)
    {
        object dot = base.ghostize(can_reenter_corpse, forced);
        if (Dm.truthy(forced))
            return dot;

        alert_hivemind(I18n.T("You sense the presence of [] disappear from the hivemind.", real_name));
        if (!QDELING(this))
            qdel(this);
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:235
    public virtual void alert_hivemind(object message = null)
    {
        datum_saymode_changeling saymode = SSradio.saymodes[Dm.initial(typeof(datum_saymode_changeling), (datum_saymode_changeling p) => p.key)] as datum_saymode_changeling;
        foreach (mob ling_mob in Dm.each_cast<mob>(Dm.sub(saymode.get_lings(), this)))
            to_chat(ling_mob, span_changeling(I18n.T("<i>[]</i>", message)));
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:240
    public override async DmTask attach_to_owner(mob_living imaginary_friend_owner = null)
    {
        await base.attach_to_owner(imaginary_friend_owner);
        if (client != null)
            client.eye = owner;
        if (Dm.locate<datum_action_changeling_eject_from_hivemind>(owner.actions) != null)
            return;
        datum_action_changeling_eject_from_hivemind ejector = Dm.@new<datum_action_changeling_eject_from_hivemind>(owner);
        await ejector.Grant(owner);
    }

    // tg: code/modules/antagonists/changeling/powers/absorb.dm:248
    public override object Destroy(float? force = null)
    {
        foreach (mob_eye_imaginary_friend_hivemind other_member in Dm.each<mob_eye_imaginary_friend_hivemind>(Dm.sub(owner.imaginary_group, this)))
            return base.Destroy(force);

        datum_action_changeling_eject_from_hivemind ejector = Dm.locate<datum_action_changeling_eject_from_hivemind>(owner.actions);
        qdel(ejector);
        return base.Destroy(force);
    }
}

// tg: code/modules/antagonists/changeling/powers/absorb.dm:256
public partial class datum_action_innate_exit_hivemind : datum_action_innate
{
    // tg: code/modules/antagonists/changeling/powers/absorb.dm:261
    public override async DmTask<object> Activate()
    {
        mob_eye_imaginary_friend_hivemind member = owner as mob_eye_imaginary_friend_hivemind;
        await member.exit_hivemind();
        return null;
    }
}

/// Allow the changeling to nuke people griffing them
// tg: code/modules/antagonists/changeling/powers/absorb.dm:266
public partial class datum_action_changeling_eject_from_hivemind : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/absorb.dm:273
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        DmList freeloaders = Dm.list();
        foreach (mob_eye_imaginary_friend_hivemind freeloader in Dm.each<mob_eye_imaginary_friend_hivemind>(user.imaginary_group))
            freeloaders[freeloader.real_name] = freeloader;

        if (!Dm.truthy(length(freeloaders)))
        {
            stack_trace("Tried to eject from hivemind with no hivemind members!", "code/modules/antagonists/changeling/powers/absorb.dm", 279);
            qdel(this);
            return FALSE;
        }

        object chosen = await tgui_input_list(owner, I18n.T("Choose a member to eject from the hivemind."), I18n.T("Eject Hivemind Member"), freeloaders);
        mob_eye_imaginary_friend_hivemind freeloader__1 = freeloaders[chosen] as mob_eye_imaginary_friend_hivemind;
        if (QDELETED(freeloader__1) || QDELETED(this))
            return FALSE;

        to_chat(freeloader__1, span_userdanger(I18n.T("You have been ejected from the changeling hivemind!")));
        qdel(freeloader__1);
        await base.sting_action(user, target);
        return TRUE;
    }
}
