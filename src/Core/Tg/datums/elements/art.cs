// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/art.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J3, D298: the crayon's art): the base element; gaps V1: the revolution's art; the commoner's comes with its job. The var
// block is generated.

// tg: code/datums/elements/art.dm:2
public partial class datum_element_art : datum_element
{
    // tg: code/datums/elements/art.dm:7
    [DmParams("target", "impress")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object impress = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isatom(target) || isarea(target))
            return ELEMENT_INCOMPATIBLE;
        impressiveness = Dm.num(impress);
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        return dot;
    }

    // tg: code/datums/elements/art.dm:14
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, COMSIG_ATOM_EXAMINE);
        return base.Detach(target, __extra);
    }

    // tg: code/datums/elements/art.dm:18
    public virtual void apply_moodlet(atom source = null, mob_living user = null, object impress = null)
    {
        string msg = null;
        float impression = Dm.num(impress);
        if (impression >= GREAT_ART)
        {
            Waiting.NoSleep(user.add_mood_event("artgreat", typeof(datum_mood_event_artgreat)));
            msg = I18n.T("What \\a []. So []!", pick(Dm.list("masterpiece", "chef-d'oeuvre")), pick(Dm.list("trascended", "awe-inspiring", "bewitching", "impeccable")));
        }
        else if (impression >= GOOD_ART)
        {
            Waiting.NoSleep(user.add_mood_event("artgood", typeof(datum_mood_event_artgood)));
            msg = I18n.T("[] a [] art piece, easy on the keen eye.", source.p_Theyre(), pick(Dm.list("respectable", "commendable", "laudable")));
        }
        else if (impression >= BAD_ART)
        {
            Waiting.NoSleep(user.add_mood_event("artok", typeof(datum_mood_event_artok)));
            msg = I18n.T("[] fair to middling, enough to be called an \"art object\".", source.p_Theyre());
        }
        else if (impression >= 0)
        {
            Waiting.NoSleep(user.add_mood_event("artbad", typeof(datum_mood_event_artbad)));
            msg = I18n.T("Wow, [] suck[].", source.p_they(), source.p_s());
        }

        user.visible_message(span_notice(I18n.T("[] stops and looks intently at [].", user, source)),
            span_notice(I18n.T("You appraise []... []", source, msg)));
    }

    // tg: code/datums/elements/art.dm:39
    [SignalHandler]
    public virtual void on_examine(atom source = null, mob user = null, DmList examine_texts = null)
    {
        if (!isliving(user))
            return;
        if (!Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, source)))
            Waiting.Detach(appraise(source, user)); //Do not sleep the proc.
    }

    // tg: code/datums/elements/art.dm:46
    public virtual async DmTask appraise(atom source = null, mob user = null)
    {
        to_chat(user, span_notice(I18n.T("You start appraising []...", source)));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: source)))
            return;
        float mult = 1;
        if (Dm.truthy(source.uses_integrity))
            mult = Dm.num(source.get_integrity()) / Dm.num(source.max_integrity);
        apply_moodlet(source, user as mob_living, impressiveness * mult);
    }
}

// tg: code/datums/elements/art.dm:54  (gaps V1: the revolutionaries' graffiti)
public partial class datum_element_art_rev : datum_element_art
{
    // tg: code/datums/elements/art.dm:56
    public override void apply_moodlet(atom source = null, mob_living user = null, object impress = null)
    {
        object msg;
        if (Dm.truthy(user.mind?.has_antag_datum(typeof(datum_antagonist_rev))))
        {
            Waiting.NoSleep(user.add_mood_event("artgreat", typeof(datum_mood_event_artgreat)));
            msg = I18n.T("What \\a [] []. So []!", pick(I18n.T("masterpiece"), I18n.T("chef-d'oeuvre")), source.p_theyre(), pick(I18n.T("subversive"), I18n.T("revolutionary"), I18n.T("unitizing"), I18n.T("egalitarian")));
        }
        else
        {
            Waiting.NoSleep(user.add_mood_event("artbad", typeof(datum_mood_event_artbad)));
            msg = I18n.T("Wow, [] suck[].", source.p_they(), source.p_s());
        }

        user.visible_message(span_notice(I18n.T("[] stops to inspect [].", user, source)),
            span_notice(I18n.T("You appraise [], inspecting the fine craftsmanship of the proletariat... []", source, msg)));
    }
}
