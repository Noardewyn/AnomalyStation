// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/drunk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RG1 (D333, D339): the whole file. The drunk value that falls by 4% and 0.01 every two seconds; tipsy below 6 and drunk from
// 6 with the psychotic sound environment and the "drunk" alert, the slurring, the dizziness and confusion, the vomiting, the
// fearlessness, the blurry eyes, the toxin and brain damage and the blackout. The client draws the dizziness, the blur, the alert
// and the reverb it already knew. The moodlets are live since gaps MS1; the personalities are owner-deferred, so no
// one is bibulous or whimsical. The var blocks are generated (D111).

/**
 * The drunk status effect.
 * Slowly decreases in drunk_value over time, causing effects based on that value.
 */
// tg: code/datums/status_effects/debuffs/drunk.dm:8
public partial class datum_status_effect_inebriated : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/drunk.dm:2  #define TIPSY_THRESHOLD 6 (file-local, #undef at :248)
    /// The threshld which determine if someone is tipsy vs drunk
    internal const float TIPSY_THRESHOLD = 6;

    // tg: code/datums/status_effects/debuffs/drunk.dm:19
    [DmParams("new_owner", "drunk_value")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object drunk_value = Dm.arg(__extra, 0);
        drunk_value ??= 0;
        object dot = await base.on_creation(new_owner, Dm.forward(__extra, drunk_value));
        await set_drunk_value(drunk_value);
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/drunk.dm:23
    public override object get_examine_text(mob examiner = null)
    {
        // Dead people don't look drunk
        if (IS_DEAD_OR_FAKING(owner))
            return null;

        // Having your face covered conceals your drunkness
        if (iscarbon(owner))
        {
            mob_living_carbon carbon_owner = (mob_living_carbon)owner;
            if (Dm.truthy(Dm.bitand(carbon_owner.obscured_slots, HIDEFACE)))
                return null;
        }

        // .01s are used in case the drunk value ends up to be a small decimal.
        // switch(drunk_value): the first matching range
        if (drunk_value >= 11 && drunk_value <= 21)
            return span_warning(I18n.T("[] [] slightly flushed.", owner.p_They(), owner.p_are()));
        if (drunk_value >= 21.01f && drunk_value <= 41)
            return span_warning(I18n.T("[] [] flushed.", owner.p_They(), owner.p_are()));
        if (drunk_value >= 41.01f && drunk_value <= 51)
            return span_warning(I18n.T("[] [] quite flushed and [] breath smells of alcohol.", owner.p_They(), owner.p_are(), owner.p_their()));
        if (drunk_value >= 51.01f && drunk_value <= 61)
            return span_warning(I18n.T("[] [] very flushed and [] movements jerky, with breath reeking of alcohol.", owner.p_They(), owner.p_are(), owner.p_their()));
        if (drunk_value >= 61.01f && drunk_value <= 91)
            return span_warning(I18n.T("[] look[] like a drunken mess.", owner.p_They(), owner.p_s()));
        if (drunk_value >= 91.01f && drunk_value <= INFINITY)
            return span_warning(I18n.T("[] [] a shitfaced, slobbering wreck.", owner.p_They(), owner.p_are()));

        return null;
    }

    /// Sets the drunk value to set_to, deleting if the value drops to 0 or lower
    // tg: code/datums/status_effects/debuffs/drunk.dm:52
    public virtual async DmTask set_drunk_value(object set_to = null)
    {
        if (!isnum(set_to))
            throw Dm.CRASH(Dm.text("[] - invalid value passed to set_drunk_value. (Got: [])", type, set_to), "code/datums/status_effects/debuffs/drunk.dm", 54);
        if (Dm.truthy(iron_liver))
            set_to = min(51, Dm.num(set_to));
        drunk_value = Dm.num(set_to);
        if (drunk_value <= 0)
            qdel(this);
    }

    // tg: code/datums/status_effects/debuffs/drunk.dm:61
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        // Drunk value does not decrease while dead or in stasis
        if (owner.stat == DEAD || HAS_TRAIT(owner, TRAIT_STASIS))
            return null;

        // Every tick, the drunk value decrases by
        // 4% the current drunk_value + 0.01
        // (until it reaches 0 and terminates)
        await set_drunk_value(drunk_value - (0.01f + drunk_value * 0.04f));
        if (QDELETED(this))
            return null;

        await on_tick_effects();
        return null;
    }

    /// Side effects done by this level of drunkness on tick.
    // tg: code/datums/status_effects/debuffs/drunk.dm:76
    public virtual async DmTask on_tick_effects()
    {
        return;
    }
}

/**
 * Stage 1 of drunk, applied at drunk values between 0 and 6.
 * Basically is the "drunk but no side effects" stage.
 */
// tg: code/datums/status_effects/debuffs/drunk.dm:84
public partial class datum_status_effect_inebriated_tipsy : datum_status_effect_inebriated
{
    // tg: code/datums/status_effects/debuffs/drunk.dm:86
    public override async DmTask set_drunk_value(object set_to = null)
    {
        await base.set_drunk_value(set_to);
        if (QDELETED(this))
            return;

        // Become fully drunk at over than 6 drunk value
        if (drunk_value >= TIPSY_THRESHOLD)
            await owner.apply_status_effect(typeof(datum_status_effect_inebriated_drunk), drunk_value);
    }
}

/**
 * Stage 2 of being drunk, applied at drunk values between 6 and onward.
 * Has all the main side effects of being drunk, scaling up as they get more drunk.
 */
// tg: code/datums/status_effects/debuffs/drunk.dm:100
public partial class datum_status_effect_inebriated_drunk : datum_status_effect_inebriated
{
    // tg: code/datums/status_effects/debuffs/drunk.dm:102
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        owner.sound_environment_override = SOUND_ENVIRONMENT_PSYCHOTIC;
        await owner.add_mood_event(id, typeof(datum_mood_event_drunk), drunk_value);
        owner.clear_mood_event(Dm.text("[]_after", id));
        RegisterSignal(owner, COMSIG_MOB_FIRED_GUN, nameof(drunk_gun_fired));
        RegisterSignal(owner, COMSIG_MOVABLE_GRABBED_RESISTING, nameof(grabbed_resisting));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/drunk.dm:110
    public override async DmTask<object> on_remove()
    {
        await clear_effects();
        return await base.on_remove();
    }

    // Going from "drunk" to "tipsy" should remove effects like on_remove
    // tg: code/datums/status_effects/debuffs/drunk.dm:115
    public override async DmTask be_replaced()
    {
        await clear_effects();
        await base.be_replaced();
    }

    /// Clears any side effects we set due to being drunk.
    // tg: code/datums/status_effects/debuffs/drunk.dm:120
    public virtual async DmTask clear_effects()
    {
        owner.clear_mood_event(id);
        if (!QDELING(owner) && Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_bibulous)))) // HAS_PERSONALITY (__DEFINES/mood.dm:109)
            await owner.add_mood_event(Dm.text("[]_after", id), typeof(datum_mood_event_drunk_after));

        if (owner.sound_environment_override == SOUND_ENVIRONMENT_PSYCHOTIC)
            owner.sound_environment_override = SOUND_ENVIRONMENT_NONE;

        UnregisterSignal(owner, COMSIG_MOB_FIRED_GUN);
        UnregisterSignal(owner, COMSIG_MOVABLE_GRABBED_RESISTING);
        REMOVE_TRAIT(owner, TRAIT_FEARLESS, TRAIT_STATUS_EFFECT(id));
    }

    // tg: code/datums/status_effects/debuffs/drunk.dm:132
    [SignalHandler]
    public virtual void drunk_gun_fired(datum source = null, obj_item_gun gun = null, atom firing_at = null, object @params = null, object zone = null, object bonus_spread_values = null)
    {
        // excusing the bartender, because shotgun
        if (HAS_TRAIT(owner, TRAIT_DRUNKEN_BRAWLER))
            return;
        // what makes me a good demoman?
        if (istype(gun, typeof(obj_item_gun_grenadelauncher)) || istype(gun, typeof(obj_item_gun_ballistic_revolver_grenadelauncher)))
            return;
        DmList spread = (DmList)bonus_spread_values;
        spread[MAX_BONUS_SPREAD_INDEX] = Dm.num(spread[MAX_BONUS_SPREAD_INDEX]) + (drunk_value * 0.5f);
    }

    // tg: code/datums/status_effects/debuffs/drunk.dm:143
    [SignalHandler]
    public virtual void grabbed_resisting(datum source = null, mob_living grabbed = null, DmList grab_stats = null)
    {
        if (!HAS_TRAIT(owner, TRAIT_DRUNKEN_BRAWLER))
            return;

        grab_stats[GRAB_STAT_EFFECTIVE_STATE] = Dm.num(grab_stats[GRAB_STAT_EFFECTIVE_STATE]) + 1;
        grab_stats[GRAB_STAT_FAIL_DAMAGE] = Dm.num(grab_stats[GRAB_STAT_FAIL_DAMAGE]) + clamp((Dm.num(owner.get_fire_loss()) + Dm.num(owner.get_brute_loss())) / 10, 3, 20);
    }

    // tg: code/datums/status_effects/debuffs/drunk.dm:152
    public override async DmTask set_drunk_value(object set_to = null)
    {
        await base.set_drunk_value(set_to);
        if (QDELETED(this))
            return;
        // Return to "tipsyness" when we're below 6.
        if (drunk_value < TIPSY_THRESHOLD)
        {
            await owner.apply_status_effect(typeof(datum_status_effect_inebriated_tipsy), drunk_value);
            return;
        }

        // tg reads owner.mob_mood unguarded; a moodless mob's runtime there ends the proc, which the null read here does as well
        datum_mood_event_drunk moodlet = owner.mob_mood?.mood_events?[id] as datum_mood_event_drunk;
        if (istype(moodlet, typeof(datum_mood_event_drunk)))
            moodlet.update_change(drunk_value);
    }

    // tg: code/datums/status_effects/debuffs/drunk.dm:165
    public override async DmTask on_tick_effects()
    {
        // Handle the Ballmer Peak.
        // If our owner is a scientist (has the trait "TRAIT_SCIENTIST_LIVER"), there's a 5% chance
        // that they'll say one of the special "ballmer message" lines, depending their drunk-ness level.
        obj_item_organ_liver liver_organ = owner.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver_organ != null && HAS_TRAIT(liver_organ, TRAIT_SCIENTIST_LIVER) && prob(5) && Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_whimsical)))) // HAS_PERSONALITY (__DEFINES/mood.dm:109)
        {
            if (drunk_value >= BALLMER_PEAK_LOW_END && drunk_value <= BALLMER_PEAK_HIGH_END)
                await owner.say(pick_list_replacements(VISTA_FILE, "ballmer_good_msg"), forced: "ballmer");

            if (drunk_value > BALLMER_PEAK_WINDOWS_ME) // by this point you're into windows ME territory
                await owner.say(pick_list_replacements(VISTA_FILE, "ballmer_windows_me_msg"), forced: "ballmer");
        }

        // Drunk slurring scales in intensity based on how drunk we are -at 16 you will likely not even notice it,
        // but when we start to scale up you definitely will
        if (drunk_value >= 16)
            await owner.adjust_timed_status_effect(4 * SECONDS, typeof(datum_status_effect_speech_slurring_drunk), max_duration: 20 * SECONDS);

        // And drunk people will always lose jitteriness
        await owner.adjust_jitter(-6 * SECONDS);

        // Over 41, we have a 30% chance to gain confusion, and we will always have 20 seconds of dizziness.
        if (drunk_value >= 41)
        {
            if (prob(30))
                await owner.adjust_confusion(2 * SECONDS);

            await owner.set_dizzy_if_lower(20 * SECONDS);
        }

        // Over 51, we have a 3% chance to gain a lot of confusion and vomit, and we will always have 50 seconds of dizziness
        if (drunk_value >= 51)
        {
            await owner.set_dizzy_if_lower(50 * SECONDS);
            if (prob(3))
            {
                await owner.adjust_confusion(15 * SECONDS);
                if (iscarbon(owner))
                {
                    mob_living_carbon carbon_owner = (mob_living_carbon)owner;
                    await carbon_owner.vomit(VOMIT_CATEGORY_DEFAULT); // Vomiting clears toxloss - consider this a blessing
                }
            }
            ADD_TRAIT(owner, TRAIT_FEARLESS, TRAIT_STATUS_EFFECT(id));
        }
        else
        {
            REMOVE_TRAIT(owner, TRAIT_FEARLESS, TRAIT_STATUS_EFFECT(id));
        }

        // Over 71, we will constantly have blurry eyes
        if (drunk_value >= 71)
            await owner.set_eye_blur_if_lower((drunk_value * 2 * SECONDS) - 140 * SECONDS);

        // Over 81, we will gain constant toxloss
        if (drunk_value >= 81)
        {
            await owner.adjust_tox_loss(1);
            if (!IS_UNCONSCIOUS_OR_CRIT(owner) && prob(5))
                to_chat(owner, span_warning(I18n.T("Maybe you should lie down for a bit...")));
        }

        // Over 91, we gain even more toxloss, brain damage, and have a chance of dropping into a long sleep
        if (drunk_value >= 91)
        {
            await owner.adjust_tox_loss(1);
            await owner.adjust_organ_loss(ORGAN_SLOT_BRAIN, 0.4f);
            if (!IS_UNCONSCIOUS_OR_CRIT(owner))
                await attempt_to_blackout();
        }

        // And finally, over 100 - let's be honest, you shouldn't be alive by now.
        if (drunk_value >= 101)
            await owner.adjust_tox_loss(2);
    }

    // tg: code/datums/status_effects/debuffs/drunk.dm:225
    public virtual async DmTask attempt_to_blackout()
    {
        mob_living_carbon drunkard = (mob_living_carbon)owner;
        if (Dm.truthy(drunkard.has_trauma_type(typeof(datum_brain_trauma_severe_split_personality_blackout)))) // prevent ping spamming
        {
            if (prob(10))
            {
                to_chat(owner, span_warning(I18n.T("You stumbled and fall over!")));
                await owner.slip(1 * SECONDS);
            }
            return;
        }
        if (Dm.truthy(await drunkard.gain_trauma(typeof(datum_brain_trauma_severe_split_personality_blackout), TRAUMA_LIMIT_ABSOLUTE)))
        {
            drunk_value -= 70; //So that the drunk personality can spice things up without being killed by liver failure
            return;
        }
        // SSshuttle.emergency?: a world without an emergency shuttle (a test room, a map with none) has no shuttle to miss
        if (Dm.eq(SSshuttle.emergency?.mode, SHUTTLE_DOCKED) && Dm.truthy(is_station_level(owner.z))) // Don't put us in a deep sleep if the shuttle's here. QoL, mainly.
            to_chat(owner, span_warning(I18n.T("You're so tired... but you can't miss that shuttle...")));
        else
            await owner.Sleeping(90 * SECONDS);
    }
}
