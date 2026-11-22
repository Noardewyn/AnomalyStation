// Translated from tgstation (snapshot 1d99bd7f): code/datums/brain_damage/creepy_trauma.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps B1 (D327): the whole file, the obsession (psychotic schizophrenia): the trauma picks a crewman to obsess over, makes its
// owner the obsessed antagonist (modules/antagonists/obsessed/obsessed.cs) with its objectives, counts the time spent near the
// obsession, the hugs and the stares, and steadies the creep's grip and stamina near the obsession. The var block is generated
// (D111); its moods are live since gaps MS1.

// tg: code/datums/brain_damage/creepy_trauma.dm:1
public partial class datum_brain_trauma_special_obsessed : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/creepy_trauma.dm:28
    public override async DmTask<object> on_gain()
    {
        //setup, linking, etc//
        if (obsession == null)//admins didn't set one
        {
            obsession = find_obsession() as mob_living;
            if (obsession == null)//we didn't find one
            {
                lose_text = "";
                return FALSE;
            }
        }

        gain_text = span_warning(I18n.T("You hear a sickening, raspy voice in your head. It wants one small task of you..."));
        antagonist = await owner.mind.add_antag_datum(typeof(datum_antagonist_obsessed)) as datum_antagonist_obsessed;
        antagonist.trauma = this;
        RegisterSignal(obsession, COMSIG_MOB_EYECONTACT, nameof(stare));
        RegisterSignal(obsession, COMSIG_QDELETING, nameof(obession_deleted));
        object dot = await base.on_gain();
        //antag stuff//
        await antagonist.forge_objectives(obsession.mind);
        await antagonist.greet();
        log_game(Dm.text("[] has developed an obsession with [].", key_name(antagonist), key_name(obsession)));
        RegisterSignal(owner, COMSIG_CARBON_HELPED, nameof(on_hug));
        RegisterSignal(owner, COMSIG_MOVABLE_GRABBED_RESISTING, nameof(grab_resisting));
        RegisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE_MODIFIERS, nameof(on_damage_mod));
        RegisterSignal(owner, COMSIG_MOB_MIND_TRANSFERRED_OUT_OF, nameof(on_mind_lost));
        RegisterSignal(owner, COMSIG_MOB_MIND_TRANSFERRED_INTO, nameof(on_mind_gain));
        await owner.apply_status_effect(typeof(datum_status_effect_desensitized), REF(this), DESENSITIZED_THRESHOLD);
        await owner.apply_status_effect(typeof(datum_status_effect_speech_stutter_obsession), INFINITY);
        return dot;
    }

    // tg: code/datums/brain_damage/creepy_trauma.dm:54
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (obsession == null)
        {
            viewing = FALSE;
            return;
        }

        // viewing needs to be updated regardless of the obsession's state
        viewing = Dm.num(IN_GIVEN_RANGE(owner, obsession, 7) && Dm.@in(owner, viewers(7, obsession)));
        if (!Dm.truthy(viewing))
        {
            if (Dm.truthy(witnessed_death))
                return;

            time_spent_away += Dm.num(seconds_per_tick) * SECONDS;
            time_spend_creeping = 0 * SECONDS;
            if (time_spent_away > 3 * MINUTES) //3 minutes
                await owner.add_mood_event("creeping", typeof(datum_mood_event_notcreepingsevere), obsession.name);
            else
                await owner.add_mood_event("creeping", typeof(datum_mood_event_notcreeping), obsession.name);
            return;
        }

        if (obsession.stat == DEAD)
        {
            if (!Dm.truthy(witnessed_death))
            {
                witnessed_death = TRUE;
                await owner.add_mood_event("creeping", typeof(datum_mood_event_creeping_dead));
            }
            return;
        }

        witnessed_death = FALSE;
        await owner.add_mood_event("creeping", typeof(datum_mood_event_creeping), obsession.name);
        total_time_creeping += Dm.num(seconds_per_tick) * SECONDS;
        time_spend_creeping += Dm.num(seconds_per_tick) * SECONDS;
        time_spent_away = 0 * SECONDS;
        foreach (datum_objective_spendtime objective in Dm.each<datum_objective_spendtime>(antagonist?.objectives))
            objective.timer -= Dm.num(seconds_per_tick) * SECONDS;
    }

    // tg: code/datums/brain_damage/creepy_trauma.dm:87
    public override async DmTask on_lose(object silent = null)
    {
        await base.on_lose(silent);
        if (antagonist != null)
            antagonist.trauma = null;
        antagonist = null;
        if (Dm.truthy(await owner.mind.remove_antag_datum(typeof(datum_antagonist_obsessed))))
            await owner.mind.add_antag_datum(typeof(datum_antagonist_former_obsessed));
        owner.clear_mood_event("creeping");
        if (obsession != null)
        {
            log_game(Dm.text("[] is no longer obsessed with [].", key_name(owner), key_name(obsession)));
            UnregisterSignal(obsession, Dm.list(
                COMSIG_MOB_EYECONTACT,
                COMSIG_QDELETING));
            obsession = null;
        }

        UnregisterSignal(owner, Dm.list(
            COMSIG_CARBON_HELPED,
            COMSIG_MOVABLE_GRABBED_RESISTING,
            COMSIG_MOB_APPLY_DAMAGE_MODIFIERS,
            COMSIG_MOB_MIND_TRANSFERRED_INTO,
            COMSIG_MOB_MIND_TRANSFERRED_OUT_OF));
        owner.remove_status_effect(typeof(datum_status_effect_desensitized), REF(this));
        owner.remove_status_effect(typeof(datum_status_effect_speech_stutter_obsession));
    }

    // tg: code/datums/brain_damage/creepy_trauma.dm:112
    [SignalHandler]
    public virtual void obession_deleted(datum source = null)
    {
        obsession = null;
    }

    // tg: code/datums/brain_damage/creepy_trauma.dm:116
    public override void handle_speech(datum source = null, DmList speech_args = null)
    {
        if (Dm.truthy(viewing) && !Dm.truthy(witnessed_death) && prob(12 * max(1 - (time_spend_creeping / (40 * SECONDS)), 0.02f)))
            _addtimer(Dm.@new<datum_callback>(this, nameof(do_something_nervous)), rand(1 * SECONDS, 3 * SECONDS), file: "code/datums/brain_damage/creepy_trauma.dm", line: 118);
    }

    /// Singal proc for [COMSIG_CARBON_HELPED], when our obsessed helps (hugs) our obsession, increases hug count
    // tg: code/datums/brain_damage/creepy_trauma.dm:121
    [SignalHandler]
    public virtual void on_hug(datum source = null, mob_living hugged = null)
    {
        if (hugged != obsession)
            return;

        foreach (datum_objective_hug objective in Dm.each<datum_objective_hug>(antagonist?.objectives))
            objective.hugs_needed -= 1;
    }

    /// Signal proc for [COMSIG_MOVABLE_GRABBED_RESISTING] to improve the obsessed's grabs
    // tg: code/datums/brain_damage/creepy_trauma.dm:131
    [SignalHandler]
    public virtual void grab_resisting(datum source = null, mob_living grabbed = null, DmList grab_stats = null)
    {
        if (HAS_TRAIT(owner, TRAIT_FEARLESS))
            return;

        DmList all_targets = Dm.list();
        foreach (datum_objective objective in Dm.each<datum_objective>(antagonist?.objectives))
        {
            if (objective.target == null)
                continue;
            Dm.combine(all_targets, objective.target);
        }

        // We always grab our obsession, or similar targets, with extra strength
        if (grabbed == obsession || (Dm.@in(grabbed.mind, all_targets)))
        {
            grab_stats[GRAB_STAT_EFFECTIVE_STATE] = Dm.add(grab_stats[GRAB_STAT_EFFECTIVE_STATE], 1);
            grab_stats[GRAB_STAT_FAIL_DAMAGE] = Dm.add(grab_stats[GRAB_STAT_FAIL_DAMAGE], 5);
        }

        // If we're hanging with our obsession for a while, the bonus applies to any mob (though to a lesser extent)
        else if (Dm.truthy(is_defensive()))
        {
            grab_stats[GRAB_STAT_EFFECTIVE_STATE] = Dm.add(grab_stats[GRAB_STAT_EFFECTIVE_STATE], 1);
            grab_stats[GRAB_STAT_ESCAPE_CHANCE] = Dm.add(grab_stats[GRAB_STAT_ESCAPE_CHANCE], 10); // 10% EASIER to escape
        }
    }

    /// Signal proc for [COMSIG_MOB_APPLY_DAMAGE_MODIFIERS], take less stamina damage when near our obsession
    // tg: code/datums/brain_damage/creepy_trauma.dm:154
    [SignalHandler]
    public virtual void on_damage_mod(datum source = null, DmList damage_mods = null, object damage = null, object damage_type = null, params object[] __extra)
    {
        if (HAS_TRAIT(owner, TRAIT_FEARLESS))
            return;

        if (Dm.eq(damage_type, STAMINA) && Dm.truthy(is_defensive()))
            Dm.append(damage_mods, 0.75f);
    }

    /// Checks if if we're being defensive over our obsession
    // tg: code/datums/brain_damage/creepy_trauma.dm:164
    public virtual object is_defensive()
    {
        if (time_spend_creeping >= 20 * SECONDS)
            return TRUE;
        if (IS_UNCONSCIOUS(obsession))
            return Dm.num(Dm.@in(owner, viewers(7, obsession)));
        return FALSE;
    }

    // tg: code/datums/brain_damage/creepy_trauma.dm:171
    public virtual async DmTask do_something_nervous()
    {
        if (QDELETED(owner) || IS_UNCONSCIOUS(owner) || HAS_TRAIT(owner, TRAIT_FEARLESS))
            return;

        switch (rand(1, 10))
        {
            case >= 1 and <= 4:
                await owner.adjust_jitter_up_to(10 * SECONDS, 20 * SECONDS);
                await owner.adjust_dizzy_up_to(10 * SECONDS, 20 * SECONDS);
                to_chat(owner, span_warning(I18n.T("You feel a bit nervous.")));
                break;
            case >= 5 and <= 8:
                Waiting.Detach(owner.emote("cough"));
                to_chat(owner, span_warning(I18n.T("You clear your throat.")));
                break;
            case 9:
                Waiting.Detach(owner.emote("laugh"));
                to_chat(owner, span_warning(I18n.T("You chuckle nervously.")));
                break;
            case 10:
                Waiting.Detach(owner.emote("blink"));
                await owner.adjust_eye_blur_up_to(10 * SECONDS, 20 * SECONDS);
                to_chat(owner, span_warning(I18n.T("You forget to blink for a moment.")));
                break;
        }
    }

    // if the creep examines first, then the obsession examines them, have a 50% chance to possibly blow their cover. wearing a mask avoids this risk
    // tg: code/datums/brain_damage/creepy_trauma.dm:192
    [SignalHandler]
    public virtual object stare(datum source = null, mob_living examining_mob = null, object triggering_examiner = null)
    {
        if (examining_mob != owner || !Dm.truthy(triggering_examiner) || prob(50))
            return null;

        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.to_chat), obsession, span_warning(I18n.T("You catch [] staring at you...", examining_mob)), 3), 0, file: "code/datums/brain_damage/creepy_trauma.dm", line: 198);
        return COMSIG_BLOCK_EYECONTACT;
    }

    // tg: code/datums/brain_damage/creepy_trauma.dm:201
    [SignalHandler]
    public virtual void on_mind_lost(datum source = null, mob new_body = null, datum_mind the_mind = null)
    {
        Waiting.Detach(antagonist.store_datum());
    }

    // tg: code/datums/brain_damage/creepy_trauma.dm:206
    [SignalHandler]
    public virtual void on_mind_gain(datum source = null, mob new_body = null, datum_mind the_mind = null)
    {
        Waiting.Detach(antagonist.restore_datum(the_mind));
    }

    // tg: code/datums/brain_damage/creepy_trauma.dm:211
    public virtual object find_obsession()
    {
        DmList generic_pool = Dm.list();
        DmList special_pool = Dm.list();

        DmList trait_obsessions = Dm.list(
            Dm.kv(JOB_MIME, TRAIT_MIME_FAN),
            Dm.kv(JOB_CLOWN, TRAIT_CLOWN_ENJOYER),
            Dm.kv(JOB_CHAPLAIN, TRAIT_SPIRITUAL));

        foreach (datum_mind crewmember in Dm.each_cast<datum_mind>(get_crewmember_minds()))
        {
            if (!ishuman(crewmember.current) || crewmember.current.stat == DEAD || crewmember.current == owner || !Dm.truthy(GET_CLIENT(crewmember.current)))
                continue;

            object job = crewmember.assigned_role.title;
            if (Dm.truthy(trait_obsessions[job]) && HAS_MIND_TRAIT(owner, trait_obsessions[job]))
                Dm.append(special_pool, crewmember.current);

            Dm.append(generic_pool, crewmember.current);
        }

        if (Dm.truthy(length(special_pool)))
            return pick(special_pool);

        if (Dm.truthy(length(generic_pool)))
            return pick(generic_pool);

        return null;
    }
}
