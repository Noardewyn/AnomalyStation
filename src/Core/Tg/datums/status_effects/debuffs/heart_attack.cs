// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/heart_attack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice B: the whole file (the heart attack that a defibrillator's shock, a baton's minor shock and an electrocution ease, and
// the desperation after it). The coughs: M9 slice B; the medical HUD's status image is M8 slice F. The var blocks are generated.

// tg: code/datums/status_effects/debuffs/heart_attack.dm:12
public partial class datum_status_effect_heart_attack : datum_status_effect
{
    ///Stage two of the heart attack.
    // tg: code/datums/status_effects/debuffs/heart_attack.dm:2  (file-local, #undef at the end)
    private const float ATTACK_STAGE_TWO = 100;
    ///Stage three of the heart attack.
    private const float ATTACK_STAGE_THREE = 70;
    ///Stage four of the heart attack.
    private const float ATTACK_STAGE_FOUR = 35;
    ///If we reduce heart damage enough, it will recover on its own.
    private const float ATTACK_CURE_THRESHOLD = 160;
    ///What is the max oxyloss we're willing to deal, to prevent people from passing out early.
    private const float OXYLOSS_MAXIMUM = 40;

    // tg: code/datums/status_effects/debuffs/heart_attack.dm:21
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (owner is not mob_living_carbon_human human_owner || !Dm.truthy(human_owner.can_heartattack()))
            return FALSE;
        RegisterSignal(owner, COMSIG_CARBON_LOSE_ORGAN, nameof(on_organ_removed));
        RegisterSignal(owner, COMSIG_LIVING_MINOR_SHOCK, nameof(minor_shock));
        RegisterSignal(owner, COMSIG_HEARTATTACK_DEFIB, nameof(defib_shock));
        RegisterSignal(owner, COMSIG_LIVING_ELECTROCUTE_ACT, nameof(electrocuted));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/heart_attack.dm:31  (the earlier of the file's two on_remove definitions, rules 2.5)
    public object on_remove__d0()
    {
        UnregisterSignal(owner, Dm.list(COMSIG_CARBON_LOSE_ORGAN, COMSIG_LIVING_MINOR_SHOCK, COMSIG_HEARTATTACK_DEFIB, COMSIG_LIVING_ELECTROCUTE_ACT));
        return null;
    }

    // tg: code/datums/status_effects/debuffs/heart_attack.dm:34
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (owner is not mob_living_carbon_human human_owner || !Dm.truthy(human_owner.can_heartattack()))
        {
            qdel(this); //No heart? No effects.
            return null;
        }

        if (time_until_stoppage > ATTACK_CURE_THRESHOLD)
        {
            owner.visible_message(span_nicegreen(I18n.T("[] relaxes [] body and stops clutching at [] chest!", owner, owner.p_their(), owner.p_their())), span_nicegreen(I18n.T("The pain in your chest has subsided. You're cured!")));
            qdel(this);
            return null;
        }

        float oxyloss_sum = 0; //A sum of the oxyloss we will inflict by the end of this cycle.

        if (time_until_stoppage > ATTACK_STAGE_THREE)
        {
            if (SPT_PROB(5, seconds_between_ticks))
            {
                owner.playsound_local(owner, Dm.resource("sound/effects/singlebeat.ogg"), 25, FALSE, use_reverb: FALSE);
                await owner.adjust_stamina_loss(5);
            }
        }

        if (time_until_stoppage <= ATTACK_STAGE_TWO && time_until_stoppage > ATTACK_STAGE_THREE) //This coughing gets replaced with worse coughing, no need to stack it.
        {
            if (SPT_PROB(10, seconds_between_ticks))
            {
                await owner.emote("cough");
                await owner.adjust_stamina_loss(10);
                oxyloss_sum += 4;
            }
        }

        if (time_until_stoppage <= ATTACK_STAGE_THREE) //At this point, we start with chat messages and make it clear that something is very wrong.
        {
            if (!Dm.truthy(visible))
            {
                ADD_TRAIT(owner, TRAIT_DISEASELIKE_SEVERITY_HIGH, type);
                Dm.Deferred("code/datums/status_effects/debuffs/heart_attack.dm:61", "tick: owner.med_hud_set_status(), the medical HUD's status image (data HUDs, M8 slice F)");
                visible = TRUE; //We do not reset this status until it's fully cured. Once it's been made apparent, there's no reason to hide it again until it is resolved. It will only confuse players.
            }
            if (SPT_PROB(15, seconds_between_ticks))
            {
                to_chat(owner, span_danger(I18n.T("You feel a sharp pain in your chest!")));
                if (SPT_PROB(15, seconds_between_ticks))
                    await human_owner.vomit(VOMIT_CATEGORY_DEFAULT, lost_nutrition: 95);
                await owner.emote("cough");
                oxyloss_sum += 1;
            }
            if (SPT_PROB(8, seconds_between_ticks))
            {
                to_chat(owner, span_danger(I18n.T("You feel very weak and dizzy...")));
                await owner.adjust_confusion_up_to(6 * SECONDS, 10 * SECONDS);
                await owner.adjust_stamina_loss(20);
                await owner.emote("cough");
                oxyloss_sum += 8;
            }
        }

        if (time_until_stoppage <= ATTACK_STAGE_FOUR) //And now we compound it with even worse effects.
        {
            if (SPT_PROB(5, seconds_between_ticks))
            {
                to_chat(owner, span_userdanger(I18n.T("It feels like you're shutting down...")));
                await owner.adjust_dizzy_up_to(4 * SECONDS, 10 * SECONDS);
                await owner.adjust_eye_blur_up_to(4 * SECONDS, 20 * SECONDS);
                await owner.adjust_stamina_loss(20);
            }

            if (SPT_PROB(5, seconds_between_ticks))
            {
                await owner.emote("cough");
                if (SPT_PROB(5, seconds_between_ticks))
                {
                    to_chat(owner, span_userdanger(I18n.T("You cough. Everything goes dark. You're going to die soon.")));
                    await owner.adjust_temp_blindness(10 * SECONDS); //Are you panicking yet? You should be panicking by now.
                }
                else
                {
                    to_chat(owner, span_userdanger(I18n.T("As you cough, your chest surges in pain and darkness closes in around your sight.")));
                    await owner.adjust_temp_blindness(2 * SECONDS);
                    await owner.adjust_eye_blur_up_to(4 * SECONDS, 20 * SECONDS);
                }
                oxyloss_sum += 8;
                await owner.Paralyze(1 * SECONDS);
            }
            oxyloss_sum += 3;
        }

        if (Dm.num(owner.get_oxy_loss()) < OXYLOSS_MAXIMUM) //A bad enough roll on the verge of passing out might still push you over into unconciousness for a few seconds...?
            await owner.adjust_oxy_loss(oxyloss_sum);

        if (time_until_stoppage <= 0)
        {
            if (!IS_UNCONSCIOUS_OR_CRIT(owner))
                to_chat(owner, span_userdanger(I18n.T("You feel a terrible pain in your chest, as if your heart has stopped!")));
            await owner.adjust_eye_blur(20 * SECONDS);
            human_owner.set_heartattack(TRUE);
            await owner.apply_status_effect(typeof(datum_status_effect_heart_desperation)); // To give the victim a final chance to shock their heart before losing consciousness
            Type flash_type = typeof(atom_movable_screen_fullscreen_flash);
            // GAP(lead, modules/client/preferences.cs): owner.client?.prefs?.read_preference(/datum/preference/toggle/darkened_flash):
            // the client's prefs stay null until the M9 preferences (as in shake_camera), so the null-conditional read is null.
            object darkened_flash__gap = null;
            if (Dm.truthy(darkened_flash__gap))
                flash_type = typeof(atom_movable_screen_fullscreen_flash_black);
            owner.overlay_fullscreen("flash", flash_type);
            _addtimer(Dm.@new<datum_callback>(owner, nameof(mob.clear_fullscreen), "flash", 1 * SECONDS), 1 * SECONDS, file: "code/datums/status_effects/debuffs/heart_attack.dm", line: 110);
            qdel(this);
            return FALSE;
        }

        if (time_until_stoppage <= ATTACK_STAGE_TWO)
        {
            if (time_until_stoppage <= ATTACK_STAGE_FOUR)
                owner.playsound_local(owner, Dm.resource("sound/effects/singlebeat.ogg"), 100, FALSE, channel: CHANNEL_HEARTBEAT, use_reverb: FALSE);
            else
                owner.playsound_local(owner, Dm.resource("sound/effects/health/slowbeat.ogg"), 25, FALSE, channel: CHANNEL_HEARTBEAT, use_reverb: FALSE);
        }
        else
        {
            owner.stop_sound_channel(CHANNEL_HEARTBEAT);
        }

        time_until_stoppage--;
        return null;
    }

    // tg: code/datums/status_effects/debuffs/heart_attack.dm:124
    public override object get_examine_text(mob examiner = null)
    {
        // tg's `!time_until_stoppage <= ATTACK_STAGE_THREE` reads (!time_until_stoppage) <= 70: true, so the text always shows
        if (!((Dm.truthy(time_until_stoppage) ? 0 : 1) <= ATTACK_STAGE_THREE))
            return null;
        var human_owner = (mob_living_carbon_human)owner;
        bool cannot_grasp = (human_owner.usable_hands <= 0 || Dm.truthy(Dm.bitand(owner.incapacitated, INCAPABLE_RESTRAINTS)) || HAS_TRAIT(owner, TRAIT_HANDS_BLOCKED));
        return span_warning(Dm.text("[] look[] to be doubling over", owner.p_they(), owner.p_s()) + (cannot_grasp ? I18n.T(" in pain!") : I18n.T(", clutching [] chest in pain!", owner.p_their())));
    }

    // tg: code/datums/status_effects/debuffs/heart_attack.dm:131
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_DISEASELIKE_SEVERITY_HIGH, type);
        Dm.Deferred("code/datums/status_effects/debuffs/heart_attack.dm:133", "on_remove: owner.med_hud_set_status(), the medical HUD's status image (data HUDs, M8 slice F)");
        return on_remove__d0();
    }

    ///End the heart attack due to heart removal.
    // tg: code/datums/status_effects/debuffs/heart_attack.dm:137
    [SignalHandler]
    public virtual void on_organ_removed(datum source = null, obj_item organ = null)
    {
        if (istype(organ, typeof(obj_item_organ_heart)))
            qdel(this);
    }

    ///Slightly reduces your timer. Can cure you if you really really want.
    // tg: code/datums/status_effects/debuffs/heart_attack.dm:143
    [SignalHandler]
    public virtual void minor_shock(datum source = null)
    {
        time_until_stoppage += 18; //Good for keeping yourself up. Won't be easy to get over the cure threshold by yourself. You're going to need security beating the crap out of you with stunbatons, but it'll work.
        if (prob(50)) //Also good for crafty solos who want to stunbaton themselves back to health. Timing will be key.
            to_chat(owner, span_nicegreen(I18n.T("Something about being shocked makes the pain in your chest ease up!")));
    }

    ///Makes major progress towards curing the attack.
    // tg: code/datums/status_effects/debuffs/heart_attack.dm:150  (tg names the sender, the shocked human, source)
    [SignalHandler]
    public virtual void defib_shock(datum source = null)
    {
        time_until_stoppage += 50; //Three shocks should save pretty much anyone.
        owner.visible_message(span_nicegreen(I18n.T("[] seems to be relieved of their pain as they're shocked by the []!", owner, source)), span_nicegreen(I18n.T("The [] shocks your heart awake, and you feel the pain in your chest ease up!", source)));
    }

    ///Slightly reduces your timer, just like the minor shock signal. Slightly more relief because these use cases are generally more dangerous.
    // tg: code/datums/status_effects/debuffs/heart_attack.dm:156
    [SignalHandler]
    public virtual void electrocuted(datum source = null, object shock_damage = null)
    {
        time_until_stoppage += (20 + Dm.num(shock_damage) * 1.15f);
        if (prob(50))
            to_chat(owner, span_nicegreen(I18n.T("Something about being electrocuted makes the pain in your chest ease up!")));
    }
}

///Alternative to penthrite that keeps you up for a few seconds after having a heart attack. Gives a bit of time to call for help regardless of when/where you've collapsed.
// tg: code/datums/status_effects/debuffs/heart_attack.dm:163
public partial class datum_status_effect_heart_desperation : datum_status_effect
{
    /// List of traits to add/remove from our subject when we are in their system
    // tg: code/datums/status_effects/debuffs/heart_attack.dm:168  var/static/list/subject_traits = list(...)  (rules 10.6: a TgState field, below)
    public static ref DmList subject_traits => ref TgState.Current.datum_status_effect_heart_desperation__subject_traits;

    // tg: code/datums/status_effects/debuffs/heart_attack.dm:175
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        owner.add_traits(subject_traits, type);
        await owner.Knockdown(2 * SECONDS, 2 * SECONDS);
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/heart_attack.dm:180
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        await owner.Knockdown(2 * SECONDS, 2 * SECONDS);
        await owner.set_silence_if_lower(2 * SECONDS);
        return null;
    }

    // tg: code/datums/status_effects/debuffs/heart_attack.dm:184
    public override async DmTask<object> on_remove()
    {
        owner.remove_traits(subject_traits, type);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/status_effects/debuffs/heart_attack.dm:168-173  /datum/status_effect/heart_desperation/var/static/list/subject_traits
    internal DmList datum_status_effect_heart_desperation__subject_traits = Dm.list(
        TRAIT_STABLEHEART,
        TRAIT_NOHARDCRIT,
        TRAIT_NOSOFTCRIT,
        TRAIT_NOCRITDAMAGE);
}
