// Translated from tgstation (snapshot 1d99bd7f): code/datums/mood_events/_mood_event.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file. The var block is generated (D111) but for category, which the converter types as a typepath
// (rule V3: tg assigns typepaths to other vars named category) while it holds the moodlet's text key or REF.

// tg: code/datums/mood_events/_mood_event.dm:1
[DmPath("/datum/mood_event")]
public partial class datum_mood_event : datum
{
    /**
     * A category to put multiple mood events. If one of the mood events in the category
     * is active while another mood event (from the same category) is triggered it will remove
     * the effects of the current mood event and replace it with the new one
     */
    // tg: code/datums/mood_events/_mood_event.dm:15  VAR_FINAL/category
    public object category;

    // tg: code/datums/mood_events/_mood_event.dm:28
    [DmParams("category")]
    public override async DmTask New(params object[] __extra)
    {
        object category = Dm.arg(__extra, 0);
        this.category = category;
    }

    // tg: code/datums/mood_events/_mood_event.dm:31
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(owner))
        {
            remove_effects();
            owner = null;
        }
        return base.Destroy(force);
    }

    /**
     * Called when this datum is created, checks if the passed mob can experience this mood event
     *
     * * home - the mood datum we are being added to
     * * who - the mob to check
     * * ... - any other arguments that are passed to the mood event
     *
     * Return TRUE if the mob can experience this mood event
     * Return FALSE if the mob should be unaffected
     */
    // tg: code/datums/mood_events/_mood_event.dm:47
    [ShouldCallParent]
    public virtual object can_effect_mob(datum_mood home = null, mob_living who = null, params object[] __extra)
    {
        if (Dm.truthy(LAZYLEN(required_job)) && !Dm.truthy(is_type_in_list(who.mind?.assigned_role, required_job)))
            return FALSE;

        if (Dm.truthy(Dm.bitand(event_flags, MOOD_EVENT_WHIMSY)))
        {
            // Whimsical people get positive whimsical moodlets
            // Non-whimsical people get negative whimsical moodlets
            bool is_whimsical = Dm.truthy(LAZYACCESS(who.personalities, typeof(datum_personality_whimsical))); // HAS_PERSONALITY (__DEFINES/mood.dm:109)
            if (mood_change >= 0 && !is_whimsical)
                return FALSE;
            if (mood_change < 0 && is_whimsical)
                return FALSE;
        }

        if (Dm.truthy(Dm.bitand(event_flags, MOOD_EVENT_ART)) && Dm.truthy(LAZYACCESS(who.personalities, typeof(datum_personality_unimaginative))))
            return FALSE;

        if (Dm.truthy(Dm.bitand(event_flags, MOOD_EVENT_PAIN)) && HAS_TRAIT(who, TRAIT_ANALGESIA))
            return FALSE;

        return TRUE;
    }

    /**
     * Wrapper for the mood event being added to a mob
     *
     * * home - the mood datum we are being added to
     * * who - the mob to add the mood event to
     * * mood_args - any other arguments that are passed to the mood event
     */
    // tg: code/datums/mood_events/_mood_event.dm:76
    [ShouldNotOverride]
    public virtual async DmTask on_add(datum_mood home = null, mob_living who = null, DmList mood_args = null)
    {
        owner = who;

        if (Dm.truthy(Dm.bitand(event_flags, MOOD_EVENT_ART)) && Dm.truthy(LAZYACCESS(who.personalities, typeof(datum_personality_creative))))
            mood_change *= 1.2f;

        if (Dm.truthy(Dm.bitand(event_flags, MOOD_EVENT_SPIRITUAL)) && !HAS_TRAIT(who, TRAIT_SPIRITUAL))
            mood_change *= 0.2f;

        if (Dm.truthy(Dm.bitand(event_flags, MOOD_EVENT_FOOD)))
        {
            if (Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_ascetic))))
                mood_change *= 0.75f;
            if (Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_gourmand))))
                mood_change *= 1.25f;
        }

        if (Dm.truthy(Dm.bitand(event_flags, MOOD_EVENT_FEAR)))
        {
            if (Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_cowardly))))
                mood_change *= 1.25f;
            if (Dm.truthy(LAZYACCESS(owner.personalities, typeof(datum_personality_brave))))
                mood_change *= 0.75f;
        }

        await Dm.Call(this, nameof(add_effects), Dm.arglist(mood_args));
        if (Dm.truthy(who.mind?.holy_role) && Dm.truthy(GLOB.deity))
        {
            on_add__god_regex ??= Dm.regex(@"\bgods?\b", "ig");
            description = on_add__god_regex.Replace(description, Dm.text("[]$2", GLOB.deity));
        }

        mood_change = floor(mood_change);

        timeout = Dm.num(timeout) * max((mood_change > 0) ? home.positive_moodlet_length_modifier : home.negative_moodlet_length_modifier, 0.1f);
        if (Dm.truthy(timeout))
            _addtimer(Dm.@new<datum_callback>(home, nameof(datum_mood.clear_mood_event), category), timeout, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_NO_HASH_WAIT), file: "code/datums/mood_events/_mood_event.dm", line: 108);
    }

    // tg: code/datums/mood_events/_mood_event.dm:101  on_add's var/static/regex/god_regex (a proc-level static, rules 10.6)
    private static ref regex on_add__god_regex => ref TgState.Current.datum_mood_event__on_add__god_regex;

    /**
     * Called when added to a mob
     *
     * * ... - Any arguments passed to add_mood_event after the typepath are passed here
     */
    // tg: code/datums/mood_events/_mood_event.dm:115
    public virtual async DmTask add_effects(params object[] __extra)
    {
        return;
    }

    /**
     * Called when the event is cleared from a mob
     */
    // tg: code/datums/mood_events/_mood_event.dm:121
    public virtual void remove_effects()
    {
        return;
    }

    /**
     * Called when we get added to a mood datum, but a mood of our type in our category already
     *
     * * home - the mood datum we are being added to
     * * ... - any other arguments that are passed to the mood event
     *
     * Return BLOCK_NEW_MOOD to stop the new mood event from being added
     * Return ALLOW_NEW_MOOD to allow the new mood event to be added - note this implicitly deletes [src]
     */
    // tg: code/datums/mood_events/_mood_event.dm:133
    public virtual object be_refreshed(datum_mood home = null, params object[] __extra)
    {
        // Base behavior is refresh the timer
        if (Dm.truthy(timeout))
            _addtimer(Dm.@new<datum_callback>(home, nameof(datum_mood.clear_mood_event), category), timeout, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_NO_HASH_WAIT), file: "code/datums/mood_events/_mood_event.dm", line: 136);
        return BLOCK_NEW_MOOD;
    }

    /**
     * Called when we get added to a mood datum, but a mood of a different type is in our category
     *
     * * home - the mood datum we are being added to
     * * new_event - the new mood event that is being added
     * * ... - any other arguments that are passed to the mood event
     *
     * Return BLOCK_NEW_MOOD to stop the new mood event from being added
     * Return ALLOW_NEW_MOOD to allow the new mood event to be added - note this implicitly deletes [src]
     */
    // tg: code/datums/mood_events/_mood_event.dm:149
    public virtual object be_replaced(datum_mood home = null, datum_mood_event new_event = null, params object[] __extra)
    {
        return ALLOW_NEW_MOOD;
    }
}

/// Subtype of mood event that iterates over all subtypes of itself to find a suitable one to apply
// tg: code/datums/mood_events/_mood_event.dm:153
public partial class datum_mood_event_conditional : datum_mood_event
{
    // tg: code/datums/mood_events/_mood_event.dm:159
    public override object can_effect_mob(datum_mood home = null, mob_living who = null, params object[] __extra)
    {
        DmList args = Dm.args(home, who, __extra);
        return Dm.truthy(base.can_effect_mob(home, who, __extra)) ? Waiting.NoSleep(Dm.Call(this, nameof(condition_fulfilled), Dm.arglist(args.Copy(2)))) : FALSE;
    }

    /**
     * Is the condition for this mood event fulfilled for the given mob?
     *
     * * who - the mob to check
     * * ... - any other arguments that are passed to the mood event
     */
    // tg: code/datums/mood_events/_mood_event.dm:168
    public virtual object condition_fulfilled(mob_living who = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/datums/mood_events/_mood_event.dm:171
    public override object be_replaced(datum_mood home = null, datum_mood_event new_event = null, params object[] __extra)
    {
        return Dm.initial(new_event, (datum_mood_event_conditional p) => p.priority) > Dm.initial(this, (datum_mood_event_conditional p) => p.priority) ? ALLOW_NEW_MOOD : BLOCK_NEW_MOOD;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/mood_events/_mood_event.dm:101  /datum/mood_event/on_add's var/static/regex/god_regex
    internal regex datum_mood_event__on_add__god_regex;
}
