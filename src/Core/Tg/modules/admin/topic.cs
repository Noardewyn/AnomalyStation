// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/topic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10x slice G6: the admin help's links): CheckAdminHref and the holder's Topic as far as its first keys, the tickets' (ahelp,
// ahelp_tickets); syndicate A1 (D316): the antagonist tools' keys (traitor, check_teams, team_command); syndicate A3: gamemode_panel;
// syndicate N1c: force_war, give_reinforcement. Not translated: every other key
// (the admin panels' links: the port's admin tools are the console's commands, D275).

// tg: code/modules/admin/holder2.dm:12
public partial class datum_admins : datum
{
    // tg: code/modules/admin/topic.dm:1
    public virtual bool CheckAdminHref(object href = null, DmList href_list = null)
    {
        object auth = href_list["admin_token"];
        bool dot = Dm.truthy(auth) && (Dm.eq(auth, href_token) || Dm.eq(auth, GLOB.href_token));
        if (dot)
            return dot;
        string msg = !Dm.truthy(auth) ? "no" : "a bad";
        message_admins(Dm.text("[] clicked an href with [] authorization key!", key_name_admin(usr), msg));
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_debug_admin_hrefs)))) // CONFIG_GET(flag/debug_admin_hrefs)
        {
            message_admins("Debug mode enabled, call not blocked. Please ask your coders to review this round's logs.");
            log_world(Dm.text("UAH: []", href));
            return true;
        }
        log_admin_private(Dm.text("[] clicked an href with [] authorization key! []", key_name(usr), msg, href));
        return false;
    }

    // tg: code/modules/admin/topic.dm:14
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        await base.Topic(href, href_list);

        if (usr.client != owner || !Dm.truthy(check_rights(0)))
        {
            message_admins(Dm.text("[] has attempted to override the admin panel!", usr.key));
            log_admin(Dm.text("[] tried to use the admin panel without authorization.", key_name(usr)));
            return null;
        }

        if (!CheckAdminHref(href, href_list))
            return null;

        if (Dm.truthy(href_list["ahelp"]))
        {
            if (!Dm.truthy(check_rights(R_ADMIN, TRUE)))
                return null;

            object ahelp_ref = href_list["ahelp"];
            var AH = Dm.locate_ref(ahelp_ref) as datum_admin_help;
            if (AH != null)
                await AH.Action(href_list["ahelp_action"]);
            else
                to_chat(usr, Dm.text("Ticket [] has been deleted!", ahelp_ref), confidential: TRUE);
        }
        else if (Dm.truthy(href_list["ahelp_tickets"]))
            GLOB.ahelp_tickets.BrowseTickets(Dm.text2num(href_list["ahelp_tickets"]));

        // tg: code/modules/admin/topic.dm:78 (syndicate package A3: the Round Status page's Dynamic Panel link)
        else if (Dm.truthy(href_list["gamemode_panel"]))
            await dynamic_panel(usr);

        // tg: code/modules/admin/topic.dm:940 (syndicate package A1: Check Antagonists' Show Objective link)
        else if (Dm.truthy(href_list["traitor"]))
        {
            if (!Dm.truthy(check_rights(R_ADMIN)))
                return null;

            if (!Dm.truthy(SSticker.HasRoundStarted()))
            {
                await tgui_alert(usr, "The game hasn't started yet!");
                return null;
            }

            object M = Dm.locate_ref(href_list["traitor"]);
            if (!ismob(M))
            {
                datum_mind D = M as datum_mind;
                if (D == null)
                {
                    to_chat(usr, "This can only be used on instances of type /mob and /mind", confidential: TRUE);
                    return null;
                }
                else
                    await D.traitor_panel();
            }
            else
            {
                // SSadmin_verbs.dynamic_invoke_verb(usr, /datum/admin_verb/show_traitor_panel, M): the admin verb's body (admingame.dm:450),
                // as the admin console's traitor-panel runs it (D275)
                datum_mind target_mind = ((mob)M).mind;
                if (target_mind == null)
                    to_chat(usr, "This mob has no mind!", confidential: TRUE);
                else
                    await target_mind.traitor_panel();
            }
            return null;
        }

        // tg: code/modules/admin/topic.dm:1171 (syndicate package A1: the team listing)
        else if (Dm.truthy(href_list["check_teams"]))
        {
            if (!Dm.truthy(check_rights(R_ADMIN)))
                return null;
            await check_teams();
        }

        else if (Dm.truthy(href_list["team_command"]))
        {
            if (!Dm.truthy(check_rights(R_ADMIN)))
                return null;
            datum_team T = null;
            switch (Dm.str(href_list["team_command"]))
            {
                case "create_team":
                    await admin_create_team(usr);
                    break;
                case "rename_team":
                    T = team_of(href_list["team"]);
                    if (T != null)
                        await T.admin_rename(usr);
                    break;
                case "communicate":
                    T = team_of(href_list["team"]);
                    if (T != null)
                        await T.admin_communicate(usr);
                    break;
                case "delete_team":
                    T = team_of(href_list["team"]);
                    if (T != null)
                        T.admin_delete(usr);
                    break;
                case "add_objective":
                    T = team_of(href_list["team"]);
                    if (T != null)
                        await T.admin_add_objective(usr);
                    break;
                case "remove_objective":
                {
                    T = team_of(href_list["team"]);
                    if (T == null)
                        return null;
                    object found = Dm.locate_ref(href_list["tobjective"]);
                    datum_objective O = Dm.@in(found, T.objectives) ? found as datum_objective : null;
                    if (O != null)
                        T.admin_remove_objective(usr, O);
                    break;
                }
                case "add_member":
                    T = team_of(href_list["team"]);
                    if (T != null)
                        await T.admin_add_member(usr);
                    break;
                case "remove_member":
                {
                    T = team_of(href_list["team"]);
                    if (T == null)
                        return null;
                    object found = Dm.locate_ref(href_list["tmember"]);
                    datum_mind M = Dm.@in(found, T.members) ? found as datum_mind : null;
                    if (M != null)
                        await T.admin_remove_member(usr, M);
                    break;
                }
            }
            await check_teams();
        }
        // tg: code/modules/admin/topic.dm:1346  (syndicate N1c: the nuclear team's links in the antagonists' listing)
        else if (Dm.truthy(href_list["force_war"]))
        {
            if (!Dm.truthy(check_rights(R_ADMIN)))
                return null;
            var button = Dm.locate_ref(href_list["force_war"]) as obj_item_nuclear_challenge;
            await button.force_war();
        }
        // tg: code/modules/admin/topic.dm:1352
        else if (Dm.truthy(href_list["give_reinforcement"]))
        {
            object found = Dm.locate_ref(href_list["give_reinforcement"]);
            var nuketeam = Dm.@in(found, GLOB.antagonist_teams) ? found as datum_team_nuclear : null;
            await nuketeam.admin_spawn_reinforcement(usr);
        }
        // the rest of the keys: the admin panels (not translated)
        return null;
    }

    // locate(href_list["team"]) in GLOB.antagonist_teams (topic.dm:1182 and the other team commands)
    private static datum_team team_of(object team_ref)
    {
        object found = Dm.locate_ref(team_ref);
        return Dm.@in(found, GLOB.antagonist_teams) ? found as datum_team : null;
    }
}
