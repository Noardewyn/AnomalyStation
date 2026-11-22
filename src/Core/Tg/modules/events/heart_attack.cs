// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/heart_attack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4 (D327): the whole file but the admins' setups (/datum/event_admin_setup, as the other events'): a crewman whose heart can stop
// (three times as likely after a lot of junk food) gets a heart attack; the athletics skill protects a fit one (gaps SL1). The Healthy
// achievement waits for the achievements (owner-deferred, D333). The var blocks are generated (D111).

// tg: code/modules/events/heart_attack.dm:1
public partial class datum_round_event_control_heart_attack : datum_round_event_control
{
    // tg: code/modules/events/heart_attack.dm:15
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);
        if (!Dm.truthy(dot))
            return dot;

        generate_candidates(); //generating candidates and checking in can_spawn_event prevents extreme edge case of there being the 40 minimum players, with all being ineligible for a heart attack when the event fires
        if (Dm.length(heart_attack_candidates) > 0)
            return TRUE;
        return null;
    }

    /**
     * Performs initial analysis of which living players are eligible to be selected for a heart attack.
     *
     * Traverses player_list and checks entries against a series of reviews to see if they should even be considered for a heart attack,
     * and at what weight should they be eligible to receive it. The check for whether or not a heart attack should be "blocked" by something is done
     * later, at the round_event level, so this proc mostly just checks users for whether or not a heart attack should be possible.
     */
    // tg: code/modules/events/heart_attack.dm:31
    public virtual void generate_candidates()
    {
        heart_attack_candidates.Cut();
        foreach (mob_living_carbon_human candidate in Dm.each<mob_living_carbon_human>(shuffle(GLOB.player_list)))
        {
            if (IS_UNCONSCIOUS_OR_CRIT(candidate) || !Dm.truthy(candidate.can_heartattack()) || Dm.truthy(candidate.has_status_effect(typeof(datum_status_effect_heart_attack))) || Dm.truthy(candidate.undergoing_cardiac_arrest()))
                continue;
            if (!Dm.truthy(Dm.bitand(candidate.mind?.assigned_role?.job_flags, JOB_CREW_MEMBER)))//only crewmembers can get one, a bit unfair for some ghost roles and it wastes the event
                continue;
            if (candidate.satiety <= -60 && !Dm.truthy(candidate.has_status_effect(typeof(datum_status_effect_exercised)))) //Multiple junk food items recently //No foodmaxxing for the achievement
                heart_attack_candidates[candidate] = 3;
            else
                heart_attack_candidates[candidate] = 1;
        }
    }
}

// tg: code/modules/events/heart_attack.dm:42
public partial class datum_round_event_heart_attack : datum_round_event
{
    // tg: code/modules/events/heart_attack.dm:47
    public override async DmTask<object> start()
    {
        var heart_control = (datum_round_event_control_heart_attack)control;
        Dm.append(victims, heart_control.heart_attack_candidates);
        heart_control.heart_attack_candidates.Cut();
        while (quantity > 0 && Dm.length(victims) > 0)
        {
            if (await attack_heart())
                quantity--;
        }
        return null;
    }

    /**
     * Picks a victim from a list and attempts to give them a heart attack
     *
     * Performs a pick_weight on a list of potential victims. Once selected, the "winner"
     * will receive heart disease. Returns TRUE if a heart attack is successfully given, and
     * FALSE if something blocks it.
     */
    // tg: code/modules/events/heart_attack.dm:63
    public virtual async DmTask<bool> attack_heart()
    {
        var winner = (mob_living_carbon_human)pick_weight(victims);
        // Our fitness level can potentially block a heart attack outright.
        object fitness_protection_probability = winner.mind?.get_skill_modifier(typeof(datum_skill_athletics), SKILL_RANDS_MODIFIER);

        if (prob(Dm.num(Dm.mul(fitness_protection_probability, 2)))) //Stuff that should "block" a heart attack rather than just deny eligibility for one goes here.
        {
            winner.visible_message(span_warning(I18n.T("[] grunts and clutches their chest for a moment, catching [] breath.", winner, winner.p_their())), span_medal(I18n.T("Your chest lurches in pain for a brief moment, which quickly fades. You feel like you've just avoided a serious health disaster.")), span_hear(I18n.T("You hear someone's breathing sharpen for a moment, followed by a sigh of relief.")), 4);
            winner.playsound_local(get_turf(winner), Dm.resource("sound/effects/health/slowbeat.ogg"), 40, 0, channel: CHANNEL_HEARTBEAT, use_reverb: FALSE);
            await winner.Stun(3 * SECONDS);
            // if(winner.client) winner.client.give_award(/datum/award/achievement/misc/healthy, winner): the achievements (M10)
            message_admins(Dm.text("[] has just survived a random heart attack!", winner)); //time to spawn them a trophy :)
            Dm.remove(victims, winner);
        }
        else
        {
            await winner.apply_status_effect(typeof(datum_status_effect_heart_attack));
            announce_to_ghosts(winner);
            Dm.remove(victims, winner);
            return true;
        }
        return false;
    }
}
