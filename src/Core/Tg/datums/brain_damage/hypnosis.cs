// Translated from tgstation (snapshot 1d99bd7f): code/datums/brain_damage/hypnosis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the hypnosis a trance leaves (the hypnotic flash and grenade): the phrase, its alert, its
// echoes and its highlight in what the victim hears; gaps B1 (D327): the hypnotized antagonist datum with its fixation objective
// (modules/antagonists/hypnotized/hypnotized.cs).

// tg: code/datums/brain_damage/hypnosis.dm:1
public partial class datum_brain_trauma_hypnosis : datum_brain_trauma
{
    // tg: code/datums/brain_damage/hypnosis.dm:15
    public override async DmTask New(params object[] __extra)
    {
        object phrase = Dm.arg(__extra, 0);
        if (!Dm.truthy(phrase))
        {
            qdel(this);
            return;
        }
        hypnotic_phrase = Dm.text("[]", phrase);
        try
        {
            target_phrase = Dm.@new<regex>(Dm.text("(\\b[]\\b)", REGEX_QUOTE(hypnotic_phrase)), "ig");
        }
        catch (System.Exception ex)
        {
            exception e = Dm.exception(ex);
            stack_trace(Dm.text("[] on []:[]", e, e.file, e.line), "code/datums/brain_damage/hypnosis.dm", 24);
            qdel(this);
        }
        await base.New(__extra);
    }

    // tg: code/datums/brain_damage/hypnosis.dm:27
    public override async DmTask<object> on_gain()
    {
        message_admins(Dm.text("[] was hypnotized with the phrase '[]'.", ADMIN_LOOKUPFLW(owner), hypnotic_phrase));
        owner.log_message(Dm.text("was hypnotized with the phrase '[]'.", hypnotic_phrase), LOG_GAME);
        to_chat(owner, span_reallybig(span_hypnophrase(Dm.text("[]", hypnotic_phrase))));
        to_chat(owner, span_notice(Dm.text("[]", pick(Dm.list(
            I18n.T("Something about this sounds... right, for some reason. You feel like you should follow these words."),
            I18n.T("These words keep echoing in your mind. You find yourself completely fascinated by them."),
            I18n.T("You feel a part of your mind repeating this over and over. You need to follow these words."),
            I18n.T("You feel your thoughts focusing on this phrase... you can't seem to get it out of your head."),
            I18n.T("Your head hurts, but this is all you can think of. It must be vitally important."))))));
        to_chat(owner, span_boldwarning(I18n.T("You've been hypnotized by this sentence. You must follow these words. If it isn't a clear order, you can freely interpret how to do so, as long as you act like the words are your highest priority.")));
        atom_movable_screen_alert_hypnosis hypno_alert = owner.throw_alert(ALERT_HYPNOSIS, typeof(atom_movable_screen_alert_hypnosis)) as atom_movable_screen_alert_hypnosis;
        await owner.mind.add_antag_datum(typeof(datum_antagonist_hypnotized));
        antagonist = owner.mind.has_antag_datum(typeof(datum_antagonist_hypnotized)) as datum_antagonist_hypnotized;
        antagonist.trauma = this;

        // Add the phrase to objectives
        datum_objective fixation = Dm.@new<datum_objective>();
        fixation.explanation_text = hypnotic_phrase;
        fixation.completed = TRUE;
        antagonist.objectives = Dm.list(fixation);

        if (hypno_alert != null) // throw_alert gives null for a mob that refuses the alert (tg would runtime here)
            hypno_alert.desc = I18n.T("\"[]\"... your mind seems to be fixated on this concept.", hypnotic_phrase);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/hypnosis.dm:54
    public override async DmTask on_lose(object silent = null)
    {
        message_admins(Dm.text("[] is no longer hypnotized with the phrase '[]'.", ADMIN_LOOKUPFLW(owner), hypnotic_phrase));
        owner.log_message(Dm.text("is no longer hypnotized with the phrase '[]'.", hypnotic_phrase), LOG_GAME);
        to_chat(owner, span_userdanger(I18n.T("You suddenly snap out of your hypnosis. The phrase '[]' no longer feels important to you.", hypnotic_phrase)));
        owner.clear_alert(ALERT_HYPNOSIS);
        await base.on_lose(silent);
        if (antagonist != null)
            antagonist.trauma = null;
        await owner.mind.remove_antag_datum(typeof(datum_antagonist_hypnotized));
        antagonist = null;
    }

    // tg: code/datums/brain_damage/hypnosis.dm:65
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (SPT_PROB(1, seconds_per_tick))
        {
            if (prob(50))
            {
                to_chat(owner, span_hypnophrase(Dm.text("<i>...[]...</i>", LOWER_TEXT(hypnotic_phrase))));
            }
            else
            {
                await owner.cause_hallucination(
                    typeof(datum_hallucination_chat),
                    "hypnosis",
                    Dm.named("force_radio", TRUE),
                    Dm.named("specific_message", span_hypnophrase(Dm.text("[]", hypnotic_phrase))));
            }
        }
    }

    // tg: code/datums/brain_damage/hypnosis.dm:78
    public override void handle_hearing(datum source = null, DmList hearing_args = null)
    {
        if (HAS_TRAIT(owner, TRAIT_DEAF) || owner == hearing_args[HEARING_SPEAKER])
            return;
        hearing_args[HEARING_RAW_MESSAGE] = target_phrase.Replace(hearing_args[HEARING_RAW_MESSAGE], span_hypnophrase("$1"));
    }
}
