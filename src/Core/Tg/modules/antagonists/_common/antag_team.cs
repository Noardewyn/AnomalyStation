// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/_common/antag_team.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A1 (D316): the whole file. The var block is generated.

public partial class datum_controller_global_vars
{
    // tg: code/modules/antagonists/_common/antag_team.dm:1  GLOBAL_LIST_EMPTY(antagonist_teams)
    public DmList antagonist_teams;

    // tg: code/modules/antagonists/_common/antag_team.dm:1  (GLOBAL_MANAGED(antagonist_teams, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalantagonist_teams()
    {
        antagonist_teams = Dm.list();
        Dm.append(ref gvars_datum_init_order, "antagonist_teams");
    }
}

//A barebones antagonist team.
// tg: code/modules/antagonists/_common/antag_team.dm:4
public partial class datum_team : datum
{
    // tg: code/modules/antagonists/_common/antag_team.dm:19
    [DmParams("starting_members")]
    public override async DmTask New(params object[] __extra)
    {
        object starting_members = Dm.arg(__extra, 0);
        await base.New(__extra);
        Dm.append(GLOB.antagonist_teams, this);
        if (Dm.truthy(starting_members))
        {
            if (islist(starting_members))
            {
                foreach (datum_mind M in Dm.each<datum_mind>(starting_members))
                    await add_member(M);
            }
            else
                await add_member(starting_members as datum_mind);
        }
    }

    // tg: code/modules/antagonists/_common/antag_team.dm:29
    public override object Destroy(float? force = null)
    {
        Dm.remove(GLOB.antagonist_teams, this);
        members = null;
        foreach (object objective in Dm.each(objectives))
        {
            if (((datum_objective)objective).team == this)
                ((datum_objective)objective).team = null;
        }
        objectives = null;
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/_common/antag_team.dm:38
    public virtual async DmTask add_member(datum_mind new_member = null)
    {
        Dm.combine(ref members, new_member);
    }

    // tg: code/modules/antagonists/_common/antag_team.dm:41
    public virtual async DmTask remove_member(datum_mind member = null)
    {
        Dm.remove(ref members, member);
    }

    // tg: code/modules/antagonists/_common/antag_team.dm:44
    public virtual async DmTask add_objective(datum_objective new_objective = null, float? needs_target = null)
    {
        needs_target ??= FALSE;
        new_objective.team = this;
        if (Dm.truthy(needs_target))
            await new_objective.find_target(dupe_search_range: Dm.list(this));
        new_objective.update_explanation_text();
        Dm.append(ref objectives, new_objective);
    }

    //Display members/victory/failure/objectives for the team
    // tg: code/modules/antagonists/_common/antag_team.dm:52
    public virtual object roundend_report()
    {
        DmList report = Dm.list();

        Dm.append(ref report, span_header(Dm.text("\\The []:", name)));
        Dm.append(ref report, Dm.text("The []s were:", member_name));
        Dm.append(ref report, printplayerlist(members));

        if (Dm.truthy(objectives.len))
        {
            Dm.append(ref report, span_header("Team had following objectives:"));
            bool win = true;
            float objective_count = 1;
            foreach (object o in Dm.each(objectives))
            {
                datum_objective objective = (datum_objective)o;
                if (!Dm.truthy(objective.check_completion()))
                    win = false;
                Dm.append(ref report, Dm.text("<B>Objective #[]</B>: [] []", objective_count, objective.explanation_text, objective.get_roundend_success_suffix()));
                objective_count++;
            }
            if (win)
                Dm.append(ref report, span_greentext(Dm.text("The [] was successful!", name)));
            else
                Dm.append(ref report, span_redtext(Dm.text("The [] have failed!", name)));
        }

        return Dm.text("<div class='panel redborder'>[]</div>", report.Join("<br>"));
    }

    /**
     * Finds all minds in a team antagonist, then checks their individual datums
     * Checks if that antag datum is in the team, then checks if it's the right datum
     * If it all passes, they're put in the list
     *
     * Args:
     * antag_datum - The antag datum path we are looking for
     * include_subtypes - Whether we allow suntypes of the antag_datum, otherwise it will stricly be type
     */
    // tg: code/modules/antagonists/_common/antag_team.dm:85
    public virtual object get_team_antags(object antag_datum = null, float? include_subtypes = null)
    {
        include_subtypes ??= TRUE;
        DmList antag_list = Dm.list();
        foreach (object m in Dm.each(members))
        {
            datum_mind team_mind = (datum_mind)m;
            foreach (datum_antagonist individual_datum in Dm.each<datum_antagonist>(team_mind.antag_datums))
            {
                if (individual_datum.get_team() != this) //only let the antag datum part of our team go through
                    continue;

                if (!Dm.truthy(antag_datum)) //there's no antag_datum to check
                    Dm.append(ref antag_list, individual_datum);
                if (Dm.truthy(include_subtypes) && istype(individual_datum, antag_datum as Type))
                    Dm.append(ref antag_list, individual_datum);
                else if (Dm.eq(individual_datum.type, antag_datum))
                    Dm.append(ref antag_list, individual_datum);
            }
        }

        return antag_list;
    }

    /// Builds section for the team
    // tg: code/modules/antagonists/_common/antag_team.dm:102
    public virtual object antag_listing_entry()
    {
        //NukeOps:
        // Jim (Status) FLW PM TP
        // Joe (Status) FLW PM TP
        //Disk:
        // Deep Space FLW
        DmList parts = Dm.list();
        Dm.append(ref parts, Dm.text("<b>[]</b><br>", antag_listing_name()));
        Dm.append(ref parts, "<table cellspacing=5>");
        foreach (object antag_entry in Dm.each(get_team_antags()))
            Dm.append(ref parts, ((datum_antagonist)antag_entry).antag_listing_entry());
        Dm.append(ref parts, "</table>");
        return parts.Join("");
    }

    ///Custom names for individuals in a team
    // tg: code/modules/antagonists/_common/antag_team.dm:117
    public virtual object antag_listing_name()
    {
        return name;
    }
}
