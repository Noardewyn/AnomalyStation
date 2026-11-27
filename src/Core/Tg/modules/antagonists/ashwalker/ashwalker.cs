// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/ashwalker/ashwalker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the ash walker (the nest's team, its skeleton key recipe, its faction and its joy at a head on a pike) and the
// tribe's round-end report. The var blocks are generated.

// tg: code/modules/antagonists/ashwalker/ashwalker.dm:2
public partial class datum_antagonist_ashwalker : datum_antagonist
{
    // tg: code/modules/antagonists/ashwalker/ashwalker.dm:11
    [DmParams("ashwalker_team")]
    public override async DmTask create_team(datum_team team = null)
    {
        var ashwalker_team = team as datum_team_ashwalkers;
        if (ashwalker_team != null)
        {
            ashie_team = ashwalker_team;
            Dm.combine(ref objectives, ashie_team.objectives);
        }
        else
        {
            ashie_team = await Dm.new_async<datum_team_ashwalkers>();
        }
    }

    // tg: code/modules/antagonists/ashwalker/ashwalker.dm:18
    public override object get_team()
    {
        return ashie_team;
    }

    // tg: code/modules/antagonists/ashwalker/ashwalker.dm:21
    public override async DmTask on_body_transfer(mob_living old_body = null, mob_living new_body = null)
    {
        await base.on_body_transfer(old_body, new_body);
        UnregisterSignal(old_body, COMSIG_MOB_EXAMINATE);
        RegisterSignal(new_body, COMSIG_MOB_EXAMINATE, nameof(on_examinate));
    }

    // tg: code/modules/antagonists/ashwalker/ashwalker.dm:26
    public override async DmTask on_gain()
    {
        await base.on_gain();
        RegisterSignal(owner.current, COMSIG_MOB_EXAMINATE, nameof(on_examinate));
        owner.teach_crafting_recipe(typeof(datum_crafting_recipe_skeleton_key));
        owner.current.remove_faction(FACTION_NEUTRAL); // ashwalkers aren't neutral; they're ashwalker-aligned
    }

    // tg: code/modules/antagonists/ashwalker/ashwalker.dm:32
    public override async DmTask on_removal()
    {
        await base.on_removal();
        if (owner.current == null)
            return;
        UnregisterSignal(owner.current, COMSIG_MOB_EXAMINATE);
        owner.current.add_faction(FACTION_NEUTRAL);
    }

    // tg: code/modules/antagonists/ashwalker/ashwalker.dm:39
    public virtual async DmTask on_examinate(datum source = null, atom A = null)
    {
        // SIGNAL_HANDLER
        if (A is obj_structure_headpike)
            await ((mob_living)owner.current).add_mood_event("oogabooga", typeof(datum_mood_event_sacrifice_good));
    }
}

// tg: code/modules/antagonists/ashwalker/ashwalker.dm:46
public partial class datum_team_ashwalkers : datum_team
{
    // tg: code/modules/antagonists/ashwalker/ashwalker.dm:53
    public override object roundend_report()
    {
        DmList report = Dm.list();

        Dm.append(report, span_header(I18n.T("An Ash Walker Tribe inhabited the wastes...</span><br>")));
        if (Dm.truthy(length(members))) //The team is generated alongside the tendril, and it's entirely possible that nobody takes the role.
        {
            Dm.append(report, I18n.T("The []s were:", member_name));
            Dm.append(report, printplayerlist(members));

            var necropolis_objective = Dm.locate<datum_objective_protect_object>(objectives);

            if (necropolis_objective != null)
            {
                Dm.remove(ref objectives, necropolis_objective); //So we don't count it in the check for other objectives.
                Dm.append(report, I18n.T("<b>The [] was tasked with defending the Necropolis:</b>", name));
                if (Dm.truthy(necropolis_objective.check_completion()))
                    Dm.append(report, span_greentext(span_header(I18n.T("The nest stands! Glory to the Necropolis!<br>"))));
                else
                    Dm.append(report, span_redtext(span_header(I18n.T("The Necropolis was destroyed, the tribe has fallen...<br>"))));
            }

            if (Dm.truthy(length(objectives)))
            {
                Dm.append(report, span_header(I18n.T("The []'s other objectives were:", name)));
                printobjectives(objectives);
            }

            Dm.append(report, I18n.T("The [] managed to perform <b>[]</b> sacrifices to the Necropolis. From this, the Necropolis produced <b>[]</b> Ash Walker eggs.", name, sacrifices_made, eggs_created));
        }
        else
        {
            Dm.append(report, I18n.T("<b>But none of its eggs hatched!</b>"));
        }

        return Dm.text("<div class='panel redborder'>[]</div>", report.Join("<br>"));
    }
}
