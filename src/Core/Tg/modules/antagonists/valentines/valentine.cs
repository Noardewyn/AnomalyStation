// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/valentines/valentine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the valentine (a date to protect, a zeroth law for a silicon lover, the in-love hearts over the
// date), the third wheel and their round-end report. The var blocks are generated (D111).

// tg: code/modules/antagonists/valentines/valentine.dm:1
public partial class datum_antagonist_valentine : datum_antagonist
{
    // tg: code/modules/antagonists/valentines/valentine.dm:12
    public override async DmTask forge_objectives(params object[] __extra)
    {
        var objective = Dm.@new<datum_objective_protect_valentine>();
        objective.owner = owner;
        objective.target = date;
        // DEVIATION(DV431): tg never refreshes the objective's text after setting its target, so a valentine's objective reads "Nothing"
        objective.update_explanation_text();
        Dm.append(ref objectives, objective);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/valentines/valentine.dm:18
    public override async DmTask on_gain()
    {
        await forge_objectives();

        if (isAI(owner.current))
        {
            var ai_lover = (mob_living_silicon_ai)owner.current;
            ai_lover.laws.set_zeroth_law(
                I18n.T("Protect your date, []. All other laws still apply in situations not pertaining to your date.", date),
                I18n.T("Be a good wingman for your master AI. Assist them in protecting [] date, [].", ai_lover.p_their(), date));
            ai_lover.laws.show_laws();
            await ai_lover.try_sync_laws();
        }

        if (iscyborg(owner.current))
        {
            var borg_lover = (mob_living_silicon_robot)owner.current;
            if (borg_lover.connected_ai != null)
            {
                await borg_lover.set_connected_ai(null);
                borg_lover.lawupdate = FALSE;
                borg_lover.laws.set_zeroth_law(I18n.T("Protect your date, []. All other laws still apply in situations not relating to your date.", date));
                borg_lover.laws.show_laws();
            }
        }

        await base.on_gain();
    }

    // tg: code/modules/antagonists/valentines/valentine.dm:40
    public override async DmTask<object> apply_innate_effects(mob_living mob_override = null)
    {
        var lover = mob_override ?? owner.current as mob_living;
        return await lover.apply_status_effect(typeof(datum_status_effect_in_love), date.current);
    }

    // tg: code/modules/antagonists/valentines/valentine.dm:44
    public override async DmTask remove_innate_effects(mob_living mob_override = null)
    {
        var lover = mob_override ?? owner.current as mob_living;
        lover.remove_status_effect(typeof(datum_status_effect_in_love));
        await DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/valentines/valentine.dm:48
    public override async DmTask greet()
    {
        to_chat(owner, span_boldwarning(I18n.T("You're on a date with []! Protect [] at all costs. This takes priority over all other loyalties.", date.name, date.p_them())));
        await DmTask.CompletedTask;
    }

    //Squashed up a bit
    // tg: code/modules/antagonists/valentines/valentine.dm:53
    public override object roundend_report()
    {
        var dates_valentine = date?.has_antag_datum(GetType()) as datum_antagonist_valentine;
        if (dates_valentine == null)
            return span_redtext(I18n.T("[] had no date!", owner.name));

        dates_valentine.show_in_roundend = FALSE; // We show up for them instead
        var our_objective = Dm.locate<datum_objective_protect_valentine>(objectives);
        var dates_objective = Dm.locate<datum_objective_protect_valentine>(dates_valentine.objectives);
        object we_survived = dates_objective?.check_completion();
        object dates_survived = our_objective?.check_completion();

        if (Dm.truthy(we_survived) && Dm.truthy(dates_survived))
            return span_greentext(I18n.T("[] and [] had a successful date!", owner.name, date.name));
        else if (Dm.truthy(we_survived))
            return span_redtext(I18n.T("[] failed to protect [], [] date!", owner.name, date.name, owner.p_their()));
        else if (Dm.truthy(dates_survived))
            return span_redtext(I18n.T("[] failed to protect [], [] date!", date.name, owner.name, date.p_their()));
        return span_redtext(I18n.T("[] and [] both failed to protect each other on their date!", owner.name, date.name));
    }
}

// tg: code/modules/antagonists/valentines/valentine.dm:72
public partial class datum_antagonist_valentine_third_wheel : datum_antagonist_valentine
{
    // tg: code/modules/antagonists/valentines/valentine.dm:77
    public override object roundend_report()
    {
        var our_objective = Dm.locate<datum_objective_protect_valentine>(objectives);
        if (Dm.truthy(our_objective?.check_completion()))
            return span_greentext(I18n.T("[] was a third wheel, but protected []!", owner.name, date.name));

        return span_redtext(I18n.T("[] was a third wheel, but failed to protect []!", owner.name, date.name));
    }
}

// tg: code/modules/antagonists/valentines/valentine.dm:84
public partial class datum_objective_protect_valentine : datum_objective_protect
{
    // tg: code/modules/antagonists/valentines/valentine.dm:88
    public override void update_explanation_text()
    {
        explanation_text = I18n.T("Protect [], your date.", target.name);
    }
}
