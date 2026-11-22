// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/xeno/xeno.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the xenomorph antagonist and its hive (the captive hive of the research specimen egg), their
// objectives and round-end reports, and the alien's mind_initialize that makes every alien with a mind a xenomorph. The var blocks
// are generated (D111).

// tg: code/modules/antagonists/xeno/xeno.dm:8
public partial class datum_team_xeno : datum_team
{
    /// Captive Xeno has been found dead, regardless of location.
    // tg: code/modules/antagonists/xeno/xeno.dm:2  #define CAPTIVE_XENO_DEAD "captive_xeno_dead" (#undef at :173)
    internal const string CAPTIVE_XENO_DEAD = "captive_xeno_dead";
    /// Captive Xeno has been found alive and within the captivity area.
    // tg: code/modules/antagonists/xeno/xeno.dm:4  #define CAPTIVE_XENO_FAIL "captive_xeno_failed" (#undef at :174)
    internal const string CAPTIVE_XENO_FAIL = "captive_xeno_failed";
    /// Captive Xeno has been found alive and outside of the captivity area.
    // tg: code/modules/antagonists/xeno/xeno.dm:6  #define CAPTIVE_XENO_PASS "captive_xeno_escaped" (#undef at :175)
    internal const string CAPTIVE_XENO_PASS = "captive_xeno_escaped";

    //Simply lists them.
    // tg: code/modules/antagonists/xeno/xeno.dm:12
    public override object roundend_report()
    {
        DmList parts = Dm.list();
        Dm.append(parts, span_header(I18n.T("The [] were:", name)));
        Dm.append(parts, printplayerlist(members));
        return Dm.text("<div class='panel redborder'>[]</div>", parts.Join("<br>"));
    }
}

// tg: code/modules/antagonists/xeno/xeno.dm:18
public partial class datum_antagonist_xeno : datum_antagonist
{
    // tg: code/modules/antagonists/xeno/xeno.dm:26
    public override async DmTask on_gain()
    {
        await forge_objectives();
        await base.on_gain();
    }

    // tg: code/modules/antagonists/xeno/xeno.dm:30
    [DmParams("new_team")]
    public override async DmTask create_team(datum_team team = null)
    {
        datum_team_xeno new_team = team as datum_team_xeno;
        if (team == null)
        {
            foreach (datum_antagonist_xeno X in Dm.each<datum_antagonist_xeno>(GLOB.antagonists))
            {
                if (X.owner == null || X.xeno_team == null)
                    continue;
                xeno_team = X.xeno_team;
                return;
            }
            xeno_team = await Dm.new_async<datum_team_xeno>();
        }
        else
        {
            if (!(new_team is datum_team_xeno))
                throw Dm.CRASH("Wrong xeno team type provided to create_team", "code/modules/antagonists/xeno/xeno.dm", 40);
            xeno_team = new_team;
        }
    }

    // tg: code/modules/antagonists/xeno/xeno.dm:43
    public override object get_team()
    {
        return xeno_team;
    }

    // tg: code/modules/antagonists/xeno/xeno.dm:46
    public override async DmTask<object> get_preview_icon()
    {
        return finish_preview_icon(Dm.icon(Dm.resource("icons/mob/nonhuman-player/alien.dmi"), "alienh")); // uni_icon: the port's icons are BYOND's
    }

    // tg: code/modules/antagonists/xeno/xeno.dm:49
    public override async DmTask forge_objectives(params object[] __extra)
    {
        datum_objective_advance_hive objective = Dm.@new<datum_objective_advance_hive>();
        objective.owner = owner;
        Dm.append(ref objectives, objective);
    }
}

// tg: code/modules/antagonists/xeno/xeno.dm:54
public partial class datum_antagonist_xeno_captive : datum_antagonist_xeno
{
    // tg: code/modules/antagonists/xeno/xeno.dm:59
    [DmParams("new_team")]
    public override async DmTask create_team(datum_team team = null)
    {
        datum_team_xeno_captive new_team = team as datum_team_xeno_captive;
        if (team == null)
        {
            foreach (datum_antagonist_xeno_captive captive_xeno in Dm.each<datum_antagonist_xeno_captive>(GLOB.antagonists))
            {
                if (captive_xeno.owner == null || captive_xeno.captive_team == null)
                    continue;
                captive_team = captive_xeno.captive_team;
                return;
            }
            captive_team = await Dm.new_async<datum_team_xeno_captive>();
            captive_team.progenitor = owner;
        }
        else
        {
            if (!(new_team is datum_team_xeno_captive))
                throw Dm.CRASH("Wrong xeno team type provided to create_team", "code/modules/antagonists/xeno/xeno.dm", 70);
            captive_team = new_team;
        }
    }

    // tg: code/modules/antagonists/xeno/xeno.dm:73
    public override object get_team()
    {
        return captive_team;
    }

    // tg: code/modules/antagonists/xeno/xeno.dm:76
    public override async DmTask forge_objectives(params object[] __extra)
    {
        datum_objective_escape_captivity objective = Dm.@new<datum_objective_escape_captivity>();
        objective.owner = owner;
        Dm.append(ref objectives, objective);
        await base.forge_objectives(__extra);
    }
}

//Xeno Objectives
// tg: code/modules/antagonists/xeno/xeno.dm:83
public partial class datum_objective_escape_captivity : datum_objective
{
    // tg: code/modules/antagonists/xeno/xeno.dm:85
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        explanation_text = I18n.T("Escape from captivity.");
    }

    // tg: code/modules/antagonists/xeno/xeno.dm:88  (tg reads get_area of the objective's mind, which is no atom: the area is null,
    // so the objective is always complete, as in tg)
    public override object check_completion()
    {
        if (!is_in_captivity_area(get_area(owner)))
            return TRUE;
        return null;
    }

    // DM's istype(area, an area instance) tests the instance's type; a null second argument is never a match. The captive objective
    // and the captive team both test it (xeno.dm:89, :148).
    internal static bool is_in_captivity_area(object tested_area)
    {
        area captivity = GLOB.communications_controller?.captivity_area;
        return captivity != null && istype(tested_area, captivity.type);
    }
}

// tg: code/modules/antagonists/xeno/xeno.dm:92
public partial class datum_objective_advance_hive : datum_objective
{
    // tg: code/modules/antagonists/xeno/xeno.dm:94
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        explanation_text = I18n.T("Survive and advance the Hive.");
    }

    // tg: code/modules/antagonists/xeno/xeno.dm:97
    public override object check_completion()
    {
        return Dm.num(owner.current != null && owner.current.stat != DEAD);
    }
}

///Captive Xenomorphs team
// tg: code/modules/antagonists/xeno/xeno.dm:101
public partial class datum_team_xeno_captive : datum_team_xeno
{
    // tg: code/modules/antagonists/xeno/xeno.dm:106
    public override object roundend_report()
    {
        DmList parts = Dm.list();
        float escape_count = 0; //counts the number of xenomorphs that were born in captivity who ended the round outside of it
        float captive_count = 0; //counts the number of xenomorphs born in captivity who remained there until the end of the round (losers)

        Dm.append(parts, span_header(I18n.T("The [] were: <br>", name)));

        if (Dm.eq(check_captivity(progenitor.current as mob_living), CAPTIVE_XENO_PASS))
            Dm.append(parts, span_greentext(I18n.T("The progenitor of this hive was [], as [], who successfully escaped captivity!", progenitor.key, progenitor)) + "<br>");
        else
            Dm.append(parts, span_redtext(I18n.T("The progenitor of this hive was [], as [], who failed to escape captivity", progenitor.key, progenitor)) + "<br>");

        foreach (datum_mind alien_mind in Dm.each<datum_mind>(members))
        {
            if (alien_mind == progenitor)
                continue;

            switch (Dm.str(check_captivity(alien_mind.current as mob_living)))
            {
                case CAPTIVE_XENO_DEAD:
                    Dm.append(parts, I18n.T("[] while trying to escape captivity!", printplayer(alien_mind, fleecheck: FALSE)));
                    break;
                case CAPTIVE_XENO_FAIL:
                    Dm.append(parts, I18n.T("[] in captivity!", printplayer(alien_mind, fleecheck: FALSE)));
                    captive_count++;
                    break;
                case CAPTIVE_XENO_PASS:
                    Dm.append(parts, I18n.T("[] and managed to []", printplayer(alien_mind, fleecheck: FALSE), span_greentext(I18n.T("escape captivity!"))));
                    escape_count++;
                    break;
            }
        }

        Dm.append(parts, I18n.T("<br> <span class='neutraltext big'> Overall, [] xenomorphs remained alive and in captivity, and [] managed to escape!</span> <br>", captive_count, escape_count));

        string thank_you_message = null;
        if (captive_count > escape_count)
            thank_you_message = I18n.T("xenobiological containment architecture");
        else
            thank_you_message = I18n.T("xenofauna combat effectiveness");

        Dm.append(parts, I18n.T("<span class='neutraltext'>Nanotrasen thanks the crew of [] for providing much needed research data on <b>[]</b>.</span>", station_name(), thank_you_message));

        return Dm.text("<div class='panel redborder'>[]</div> <br>", parts.Join("<br>"));
    }

    // tg: code/modules/antagonists/xeno/xeno.dm:144
    public virtual object check_captivity(mob_living captive_alien = null)
    {
        if (captive_alien == null || captive_alien.stat == DEAD)
            return CAPTIVE_XENO_DEAD;

        if (datum_objective_escape_captivity.is_in_captivity_area(get_area(captive_alien)))
            return CAPTIVE_XENO_FAIL;

        return CAPTIVE_XENO_PASS;
    }
}

//XENO
// tg: code/modules/antagonists/xeno/xeno.dm:154
public partial class mob_living_carbon_alien : mob_living_carbon
{
    // tg: code/modules/antagonists/xeno/xeno.dm:154
    public override async DmTask mind_initialize()
    {
        await base.mind_initialize();
        if (!Dm.truthy(mind.has_antag_datum(typeof(datum_antagonist_xeno))))
        {
            if (Dm.truthy(GLOB.communications_controller.xenomorph_egg_delivered) && datum_objective_escape_captivity.is_in_captivity_area(get_area(this)))
                await mind.add_antag_datum(typeof(datum_antagonist_xeno_captive));
            else
                await mind.add_antag_datum(typeof(datum_antagonist_xeno));

            mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_xenomorph)) as datum_job);
        }
    }

    // tg: code/modules/antagonists/xeno/xeno.dm:164
    public override async DmTask on_wabbajacked(mob_living new_mob = null)
    {
        await base.on_wabbajacked(new_mob);
        if (mind == null)
            return;
        if (isalien(new_mob))
            return;
        await mind.remove_antag_datum(typeof(datum_antagonist_xeno));
        mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_unassigned)) as datum_job);
    }
}
