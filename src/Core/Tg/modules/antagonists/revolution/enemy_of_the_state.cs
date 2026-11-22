// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/revolution/enemy_of_the_state.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps V1 (D327): the whole file: the enemy of the state, what a living head revolutionary becomes when the station wins (exile or
// hijack). The var block is generated (D111).

/**
 * When the station wins, any remaining living headrevs become Enemies of the State, a small solo antagonist.
 * They either have the choice to fuck off and do their own thing, or try and regain their honor with a hijack.
 */
// tg: code/modules/antagonists/revolution/enemy_of_the_state.dm:6
public partial class datum_antagonist_enemy_of_the_state : datum_antagonist
{
    // tg: code/modules/antagonists/revolution/enemy_of_the_state.dm:13
    public override async DmTask forge_objectives(params object[] __extra)
    {
        var exile_choice = Dm.@new<datum_objective_exile>();
        exile_choice.owner = owner;
        exile_choice.objective_name = "Choice";
        Dm.append(ref objectives, exile_choice);

        var hijack_choice = Dm.@new<datum_objective_hijack>();
        hijack_choice.owner = owner;
        hijack_choice.objective_name = "Choice";
        Dm.append(ref objectives, hijack_choice);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/revolution/enemy_of_the_state.dm:24
    public override async DmTask on_gain()
    {
        owner.add_memory(typeof(datum_memory_revolution_rev_defeat));
        // LAZYADD(owner.special_statuses, "Exiled Head Revolutionary")
        await forge_objectives();
        await base.on_gain();
    }

    // tg: code/modules/antagonists/revolution/enemy_of_the_state.dm:30
    public override async DmTask greet()
    {
        await base.greet();
        to_chat(owner, span_userdanger(I18n.T("The revolution is dead.")));
        to_chat(owner, span_bolddanger(I18n.T("You're an enemy of the state to Nanotrasen. You're a loose end to the Syndicate.")));
        to_chat(owner, Dm.text("<b>[]</b>", I18n.T("It's time to live out your days as an exile... Or go out in one last big bang.")));
        await owner.announce_objectives();
    }

    // tg: code/modules/antagonists/revolution/enemy_of_the_state.dm:37
    public override object roundend_report()
    {
        DmList report = Dm.list();

        if (owner == null)
            throw Dm.CRASH("Antagonist datum without owner", "code/modules/antagonists/revolution/enemy_of_the_state.dm", 41);

        Dm.append(ref report, printplayer(owner));

        //needs to complete only one objective, not all

        bool option_chosen = false;
        bool badass = false;
        if (Dm.truthy(objectives.len))
        {
            Dm.append(ref report, printobjectives(objectives));
            foreach (datum_objective objective in Dm.each<datum_objective>(objectives))
            {
                if (Dm.truthy(objective.check_completion()))
                {
                    option_chosen = true;
                    if (objective is datum_objective_hijack)
                        badass = true;
                    break;
                }
            }
        }

        if (objectives.len == 0 || option_chosen)
        {
            if (badass)
            {
                Dm.append(ref report, Dm.text("<span class='greentext big'>[]</span>", I18n.T("Major [] Victory", name)));
                Dm.append(ref report, Dm.text("<B>[]</B>", I18n.T("[] chose the badass option, and hijacked the shuttle!", name)));
            }
            else
            {
                Dm.append(ref report, Dm.text("<span class='greentext big'>[]</span>", I18n.T("Minor [] Victory", name)));
                Dm.append(ref report, Dm.text("<B>[]</B>", I18n.T("[] has survived as an exile!", name)));
            }
        }
        else
            Dm.append(ref report, Dm.text("<span class='redtext big'>[]</span>", I18n.T("The [] has failed!", name)));

        return report.Join("<br>");
    }
}
