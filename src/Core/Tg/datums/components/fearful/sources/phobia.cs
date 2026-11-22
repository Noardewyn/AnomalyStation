// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/fearful/sources/phobia.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3b, D316: the phobias' terror source). The var blocks are generated (D111).

/// Complex terror source that increases buildup whenever the owner hears, sees, or tries to say something they're afraid of
// tg: code/datums/components/fearful/sources/phobia.dm:2
public partial class datum_terror_handler_phobia_source : datum_terror_handler
{
    // tg: code/datums/components/fearful/sources/phobia.dm:21
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        var new_owner = Dm.arg(__extra, 0) as mob_living;
        RegisterSignal(new_owner, COMSIG_MOB_SAY, nameof(handle_speech));
        RegisterSignal(new_owner, COMSIG_MOVABLE_HEAR, nameof(handle_hearing));
    }

    // tg: code/datums/components/fearful/sources/phobia.dm:26
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(owner, Dm.list(COMSIG_MOB_SAY, COMSIG_MOVABLE_HEAR));
        return base.Destroy(force);
    }

    // tg: code/datums/components/fearful/sources/phobia.dm:30
    public virtual bool can_trigger()
    {
        return !HAS_TRAIT(owner, TRAIT_FEARLESS) && !HAS_TRAIT(owner, TRAIT_MIND_TEMPORARILY_GONE) && !IS_UNCONSCIOUS(owner);
    }

    // tg: code/datums/components/fearful/sources/phobia.dm:33
    public override async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        object dot = await base.tick(seconds_per_tick, terror_buildup);
        if (!can_trigger())
            return dot;

        // If we're still scared from the last trigger, keep adding just a tiiiiny buildup to prevent it from fading away
        if (!(scare_cooldown <= world.time)) // COOLDOWN_FINISHED(src, scare_cooldown)
            return 0.01f;

        if (!(check_cooldown <= world.time) || Dm.truthy(owner.is_blind()))
            return dot;

        check_cooldown = world.time + PHOBIA_CHECK_DELAY; // COOLDOWN_START(src, check_cooldown, PHOBIA_CHECK_DELAY)
        object view_range = owner.client?.view is var or__1 && Dm.truthy(or__1) ? or__1 : world.view;
        if (Dm.truthy(LAZYLEN(trigger_objs)))
        {
            foreach (obj seen_thing in Dm.each<obj>(view(view_range, owner)))
            {
                if (is_scary_item(seen_thing))
                    return freak_out(seen_thing);
            }
        }

        if (Dm.truthy(LAZYLEN(trigger_turfs)))
        {
            foreach (turf @checked in Dm.each<turf>(view(view_range, owner)))
            {
                if (Dm.truthy(is_type_in_typecache(@checked, trigger_turfs)))
                    return freak_out(@checked);
            }
        }

        if (Dm.truthy(LAZYLEN(trigger_mobs)) || Dm.truthy(LAZYLEN(trigger_species)) || Dm.truthy(LAZYLEN(trigger_objs)))
        {
            foreach (mob_living @checked in Dm.each<mob_living>(view(view_range, owner)))
            {
                if (@checked != owner && is_scary_mob(@checked))
                    return freak_out(@checked);
            }
        }
        return dot;
    }

    /// Returns true if this item should be scary to us
    // tg: code/datums/components/fearful/sources/phobia.dm:62
    public virtual bool is_scary_item(obj @checked = null)
    {
        if (QDELETED(@checked) || !Dm.truthy(is_type_in_typecache(@checked, trigger_objs)) || Dm.num(@checked.invisibility) > Dm.num(owner.see_invisible))
            return false;

        if (!isitem(@checked) || !ismob(@checked.loc) || HAS_TRAIT(@checked.loc, TRAIT_UNKNOWN_APPEARANCE))
            return true;

        var checked_item = (obj_item)@checked;
        return !HAS_TRAIT(checked_item, TRAIT_EXAMINE_SKIP);
    }

    // tg: code/datums/components/fearful/sources/phobia.dm:72
    public virtual bool is_scary_mob(mob_living @checked = null)
    {
        if (Dm.num(@checked.invisibility) > Dm.num(owner.see_invisible) || Dm.num(@checked.alpha) == 0)
            return false;

        if (Dm.truthy(is_type_in_typecache(@checked, trigger_mobs)))
            return true;

        if (!ishuman(@checked))
            return false;

        var as_human = (mob_living_carbon_human)@checked;
        if (Dm.truthy(LAZYLEN(trigger_species)))
        {
            // Can't be racist(?) if you can't see their face
            if (Dm.truthy(is_type_in_typecache(as_human.dna?.species, trigger_species)) && !Dm.truthy(as_human.is_face_obscured()))
                return true;
        }

        if (!Dm.truthy(LAZYLEN(trigger_objs)))
            return false;

        foreach (obj_item equipped in Dm.each_cast<obj_item>(as_human.get_visible_items()))
        {
            if (is_scary_item(equipped))
                return true;
        }

        return false;
    }

    // tg: code/datums/components/fearful/sources/phobia.dm:97
    [SignalHandler]
    public virtual void handle_hearing(datum source = null, DmList hearing_args = null)
    {
        if (!can_trigger() || !(scare_cooldown <= world.time)) // COOLDOWN_FINISHED(src, scare_cooldown)
            return;

        // Words can't trigger you if you can't hear them *taps head*
        if (HAS_TRAIT(owner, TRAIT_DEAF) || owner == hearing_args[HEARING_SPEAKER] || !Dm.truthy(owner.has_language(hearing_args[HEARING_LANGUAGE])))
            return;

        // trigger_regex is null only in a bare world without tg's strings (construct_phobia_regex): no word matches
        if (Dm.truthy(trigger_regex?.Find(hearing_args[HEARING_RAW_MESSAGE])))
        {
            // To react AFTER the chat message
            _addtimer(Dm.@new<datum_callback>(this, nameof(freak_out), null, trigger_regex.group[2]), 1 * SECONDS,
                file: "code/datums/components/fearful/sources/phobia.dm", line: 108);
            hearing_args[HEARING_RAW_MESSAGE] = trigger_regex.Replace(hearing_args[HEARING_RAW_MESSAGE], Dm.text("[]$3", span_phobia("$2")));
        }
    }

    // tg: code/datums/components/fearful/sources/phobia.dm:112
    [SignalHandler]
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        if (!can_trigger())
            return;

        if (Dm.num(trigger_regex?.Find(speech_args[SPEECH_MESSAGE])) == 0) // null in a bare world without tg's strings
            return;

        bool stutter = prob(50);
        bool whisper = prob(30);

        if (!stutter && !whisper)
            return;

        if (whisper)
            speech_args[SPEECH_SPANS] = Dm.bitor(speech_args[SPEECH_SPANS], SPAN_SMALL_VOICE);
        if (stutter)
            Waiting.NoSleep(owner.set_stutter_if_lower(4 * SECONDS));
        to_chat(owner, span_warning(I18n.T("You struggle to say the word \"[]\"!", span_phobia(Dm.text("[]", trigger_regex.group[2])))));
    }

    // tg: code/datums/components/fearful/sources/phobia.dm:133
    public virtual object freak_out(object reason = null)
    {
        scare_cooldown = world.time + 12 * SECONDS; // COOLDOWN_START(src, scare_cooldown, 12 SECONDS)
        string message = Dm.str(pick("spooks you to the bone", "shakes you up", "terrifies you", "sends you into a panic", "sends chills down your spine"));
        if (istext(reason))
        {
            to_chat(owner, span_bolddanger(I18n.T("Hearing [] []!", span_phobia(reason as string), message)));
            Waiting.NoSleep(owner.add_mood_event("phobia_minor", typeof(datum_mood_event_startled)));
            // Because this is called from a signal and not the main process, we need to add the buildup by hand
            if (component.terror_buildup < TERROR_BUILDUP_PASSIVE_MAXIMUM)
                component.terror_buildup = min(component.terror_buildup + PHOBIA_WORD_TERROR_BUILDUP, TERROR_BUILDUP_PASSIVE_MAXIMUM);
            return null;
        }

        if (mood_event_type != null)
            Waiting.NoSleep(owner.add_mood_event("phobia", mood_event_type));

        if (isatom(reason))
        {
            var as_atom = (atom)reason;
            to_chat(owner, span_bolddanger(I18n.T("Seeing [] []!", span_phobia(Dm.text("[]", as_atom.name)), message)));
        }
        else
            to_chat(owner, span_bolddanger(I18n.T("Something []!", message)));
        return PHOBIA_FREAKOUT_TERROR_BUILDUP;
    }

    // tg: code/datums/components/fearful/sources/phobia.dm:154
    public override object on_hug(mob_living hugger = null)
    {
        if (is_scary_mob(hugger))
            return HUG_TERROR_AMOUNT;
        return 0;
    }
}

/// Snowflake handler for hemophobia which triggers on bloodied items and mobs
// tg: code/datums/components/fearful/sources/phobia.dm:160
public partial class datum_terror_handler_phobia_source_blood : datum_terror_handler_phobia_source
{
    // tg: code/datums/components/fearful/sources/phobia.dm:162
    public override bool is_scary_item(obj @checked = null)
    {
        if (Dm.truthy(GET_ATOM_BLOOD_DNA_LENGTH(@checked)))
            return true;
        return base.is_scary_item(@checked);
    }

    // tg: code/datums/components/fearful/sources/phobia.dm:167
    public override bool is_scary_mob(mob_living @checked = null)
    {
        if (Dm.truthy(GET_ATOM_BLOOD_DNA_LENGTH(@checked)))
            return true;
        return base.is_scary_mob(@checked);
    }
}
