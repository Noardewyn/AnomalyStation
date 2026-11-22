// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/team_panel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A1 (D316): the whole file, the admins' Team Listing (the admin console's check-teams, D275). Its page is tg's
// browse() HTML in the port's Browser window (DV216); BYOND's input() prompts are tgui's (DV340).

//Split into Team List -> Team Details ?
// tg: code/modules/admin/team_panel.dm:2
public partial class datum_admins : datum
{
    // tg: code/modules/admin/team_panel.dm:2
    public virtual object team_listing()
    {
        DmList content = Dm.list();
        foreach (datum_team T in Dm.each<datum_team>(GLOB.antagonist_teams))
        {
            Dm.append(ref content, Dm.text("<h3>[] - []</h3>", T.name, T.type));
            Dm.append(ref content, Dm.text("<a href='byond://?_src_=holder;[];team_command=rename_team;team=[]'>Rename</a>", HrefToken(), REF(T)));
            Dm.append(ref content, Dm.text("<a href='byond://?_src_=holder;[];team_command=delete_team;team=[]'>Delete</a>", HrefToken(), REF(T)));
            Dm.append(ref content, Dm.text("<a href='byond://?_src_=holder;[];team_command=communicate;team=[]'>Communicate</a>", HrefToken(), REF(T)));
            foreach (object command in Dm.each(T.get_admin_commands()))
                Dm.append(ref content, Dm.text("<a href='byond://?src=[];command=[]'>[]</a>", REF(T), command, command));
            Dm.append(ref content, "<br>");
            Dm.append(ref content, "Objectives:<br><ol>");
            foreach (datum_objective O in Dm.each<datum_objective>(T.objectives))
                Dm.append(ref content, Dm.text("<li>[] - <a href='byond://?_src_=holder;[];team_command=remove_objective;team=[];tobjective=[]'>Remove</a></li>", O.explanation_text, HrefToken(), REF(T), REF(O)));
            Dm.append(ref content, Dm.text("</ol><a href='byond://?_src_=holder;[];team_command=add_objective;team=[]'>Add Objective</a><br>", HrefToken(), REF(T)));
            Dm.append(ref content, "Members: <br><ul>");
            foreach (datum_mind M in Dm.each<datum_mind>(T.members))
                Dm.append(ref content, Dm.text("<li>[] - <a href='byond://?_src_=holder;[];team_command=remove_member;team=[];tmember=[]'>Remove Member</a></li>", M.name, HrefToken(), REF(T), REF(M)));
            Dm.append(ref content, Dm.text("</ul><a href='byond://?_src_=holder;[];team_command=add_member;team=[]'>Add Member</a>", HrefToken(), REF(T)));
            Dm.append(ref content, "<hr>");
        }
        Dm.append(ref content, Dm.text("<a href='byond://?_src_=holder;[];team_command=create_team'>Create Team</a><br>", HrefToken()));
        return content.Join("");
    }

    // tg: code/modules/admin/team_panel.dm:25
    public virtual async DmTask check_teams()
    {
        if (!Dm.truthy(SSticker.HasRoundStarted()))
        {
            await tgui_alert(usr, "The game hasn't started yet!");
            return;
        }

        datum_browser popup = Dm.@new<datum_browser>(usr, "teams", "Team Listing", 500, 500);
        popup.set_content(team_listing());
        popup.open();
    }

    // tg: code/modules/admin/team_panel.dm:34
    public virtual async DmTask admin_create_team(mob user = null)
    {
        object team_name = await stripped_input(user, "Team name ?");
        if (!Dm.truthy(team_name))
            return;
        datum_team T = Dm.@new<datum_team>();
        T.name = Dm.str(team_name);

        message_admins(Dm.text("[] created new [] antagonist team.", key_name_admin(usr), name));
        log_admin(Dm.text("[] created new [] antagonist team.", key_name(usr), name));
    }
}

// tg: code/modules/admin/team_panel.dm:44
public partial class datum_team : datum
{
    // tg: code/modules/admin/team_panel.dm:44
    public virtual async DmTask admin_rename(mob user = null)
    {
        string old_name = name;
        object team_name = await stripped_input(user, "new team name ?", "Team rename", old_name);
        if (!Dm.truthy(team_name))
            return;
        name = Dm.str(team_name);
        message_admins(Dm.text("[] renamed [] team to []", key_name_admin(usr), old_name, name));
        log_admin(Dm.text("[] renamed [] team to []", key_name(usr), old_name, name));
    }

    // tg: code/modules/admin/team_panel.dm:53
    public virtual async DmTask admin_communicate(mob user = null)
    {
        // DEVIATION(DV340): BYOND's input() as text|null is tgui's text prompt
        object message = await tgui_input_text(user, "Message for the team ?", "Team Message", encode: FALSE);
        if (!Dm.truthy(message))
            return;
        foreach (datum_mind M in Dm.each<datum_mind>(members))
            to_chat(M.current, message, confidential: TRUE);

        message_admins(Dm.text("[] messaged [] team with : []", key_name_admin(usr), name, message));
        log_admin(Dm.text("Team Message: [] -> [] team : []", key_name(usr), name, message));
    }

    // tg: code/modules/admin/team_panel.dm:63
    public virtual async DmTask admin_add_objective(mob user = null)
    {
        //any antag with get_team == src => add objective to that antag
        //otherwise create new custom antag
        if (!Dm.truthy(GLOB.admin_objective_list))
            generate_admin_objective_list();

        // DEVIATION(DV340): BYOND's input() as null|anything in ... is tgui's list prompt
        object selected_type = await tgui_input_list(usr, "Select objective type:", "Objective type", GLOB.admin_objective_list);
        selected_type = GLOB.admin_objective_list[selected_type];
        if (!Dm.truthy(selected_type))
            return;

        datum_objective O = Dm.@new(selected_type as Type) as datum_objective;
        O.team = this;
        await O.admin_edit(user);
        Dm.combine(ref objectives, O);

        object custom_antag_name = null;

        foreach (datum_mind M in Dm.each<datum_mind>(members))
        {
            datum_antagonist team_antag = null;
            foreach (datum_antagonist A in Dm.each<datum_antagonist>(M.antag_datums))
            {
                if (A.get_team() == this)
                    team_antag = A;
            }
            if (team_antag == null)
            {
                team_antag = Dm.@new<datum_antagonist_custom>();
                if (!Dm.truthy(custom_antag_name))
                {
                    custom_antag_name = await stripped_input(user, "Custom team antagonist name:", "Custom antag", "Antagonist");
                    if (!Dm.truthy(custom_antag_name))
                        custom_antag_name = "Team Member";
                }
                team_antag.name = Dm.str(custom_antag_name);
                await M.add_antag_datum(team_antag, this);
            }
            Dm.combine(ref team_antag.objectives, O);
        }

        message_admins(Dm.text("[] added objective \"[]\" to []", key_name_admin(usr), O.explanation_text, name));
        log_admin(Dm.text("[] added objective \"[]\" to []", key_name(usr), O.explanation_text, name));
    }

    // tg: code/modules/admin/team_panel.dm:99
    public virtual void admin_remove_objective(mob user = null, datum_objective O = null)
    {
        foreach (datum_mind M in Dm.each<datum_mind>(members))
        {
            foreach (datum_antagonist A in Dm.each<datum_antagonist>(M.antag_datums))
                Dm.remove(ref A.objectives, O);
        }
        Dm.remove(ref objectives, O);

        message_admins(Dm.text("[] removed objective \"[]\" from []", key_name_admin(usr), O.explanation_text, name));
        log_admin(Dm.text("[] removed objective \"[]\" from []", key_name(usr), O.explanation_text, name));
        //qdel maybe
    }

    // tg: code/modules/admin/team_panel.dm:109
    public virtual async DmTask admin_add_member(mob user = null)
    {
        DmList minds = Dm.list();
        foreach (mob M in Dm.each<mob>(GLOB.mob_list))
        {
            if (M.mind != null)
                Dm.combine(ref minds, M.mind);
        }
        // DEVIATION(DV340): BYOND's input() as null|anything in ... is tgui's list prompt
        datum_mind value = await tgui_input_list(usr, "Select new member:", "New team member", sort_names(minds) as DmList) as datum_mind;
        if (value == null)
            return;

        message_admins(Dm.text("[] added [] as a member of [] team", key_name_admin(usr), key_name_admin(value), name));
        log_admin(Dm.text("[] added [] as a member of [] team", key_name(usr), key_name(value), name));

        await add_member(value);
    }

    // tg: code/modules/admin/team_panel.dm:123
    public virtual async DmTask admin_remove_member(mob user = null, datum_mind M = null)
    {
        message_admins(Dm.text("[] removed [] from [] team", key_name_admin(usr), key_name_admin(M), name));
        log_admin(Dm.text("[] removed [] from [] team", key_name(usr), key_name(M), name));
        await remove_member(M);
    }

    //After a bit of consideration i block team deletion if there's any members left until unified objective handling is in.
    // tg: code/modules/admin/team_panel.dm:129
    public virtual void admin_delete(mob user = null)
    {
        if (members.len > 0)
        {
            to_chat(user, "Team has members left, remove them first and make sure you know what you're doing.", confidential: TRUE);
            return;
        }
        qdel(this);
    }

    // tg: code/modules/admin/team_panel.dm:135
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        if (!Dm.truthy(check_rights(R_ADMIN)))
            return null;

        object commands = get_admin_commands();
        foreach (object admin_command in Dm.each(commands))
        {
            if (Dm.eq(href_list["command"], admin_command))
            {
                datum_callback C = ((DmList)commands)[admin_command] as datum_callback;
                await C.Invoke(usr);
                return null;
            }
        }
        return null;
    }

    // tg: code/modules/admin/team_panel.dm:146
    public virtual object get_admin_commands()
    {
        return Dm.list();
    }
}
