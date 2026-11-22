// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/antag_panel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A1 (D316): the whole file, the traitor panel. Its page is tg's browse() HTML in the port's Browser window (DV216);
// its links are the mind's Topic (datums/mind/_mind.cs) and the admin holder's (modules/admin/topic.cs). DV340: an inert antagonist
// type is shown greyed out (tg's linkOff), not offered.

public partial class datum_controller_global_vars
{
    // tg: code/modules/admin/antag_panel.dm:1  GLOBAL_VAR(antag_prototypes)
    public object antag_prototypes;

    // tg: code/modules/admin/antag_panel.dm:1  (GLOBAL_UNMANAGED(antag_prototypes), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalantag_prototypes()
    {
        return;
    }
}

//Things to do somewhere in the future (If you're reading this feel free to do any of these)
//Add HrefTokens to these
//Make this template or at least remove + "<br>" with joins where you can grasp the big picture.
//Span classes for the headers, wrap sections in div's and style them.
//Move common admin commands to /mob (maybe integrate with vv dropdown so the list is one thing with some flag where to show it)
//Move objective initialization/editing stuff from mind to objectives and completely remove mind.objectives

public static partial class GlobalProcs
{
    // tg: code/modules/admin/antag_panel.dm:10
    public static object cmp_antagpanel(datum_antagonist A = null, datum_antagonist B = null)
    {
        object a_cat = Dm.initial(A, (datum_antagonist p) => p.antagpanel_category);
        object b_cat = Dm.initial(B, (datum_antagonist p) => p.antagpanel_category);
        if (!Dm.truthy(a_cat) && !Dm.truthy(b_cat))
            return sorttext(Dm.initial(A, (datum_antagonist p) => p.name), Dm.initial(B, (datum_antagonist p) => p.name));
        return sorttext(b_cat, a_cat);
    }

    // tg: code/modules/admin/antag_panel.dm:24
    public static object listtrim(DmList L = null)
    {
        foreach (object x in Dm.each(L))
        {
            if (istext(x) && !Dm.truthy(x))
                Dm.remove(L, x);
        }
        return L;
    }
}

// tg: code/modules/admin/antag_panel.dm:17
public partial class datum_mind : datum
{
    // tg: code/modules/admin/antag_panel.dm:17
    public virtual async DmTask add_antag_wrapper(object antag_type = null, mob user = null)
    {
        datum_antagonist new_antag = Dm.@new(antag_type as Type) as datum_antagonist;
        await new_antag.admin_add(this, user);
        //If something gone wrong/admin-add assign another antagonist due to whatever clean it up
        if (new_antag.owner == null)
            qdel(new_antag);
    }
}

// tg: code/modules/admin/antag_panel.dm:30
public partial class datum_antagonist : datum
{
    // tg: code/modules/admin/antag_panel.dm:30
    public virtual object antag_panel()
    {
        DmList commands = Dm.list();
        foreach (object command in Dm.each(get_admin_commands()))
            Dm.append(ref commands, Dm.text("<a href='byond://?src=[];command=[]'>[]</a>", REF(this), command, command));
        string command_part = commands.Join(" | ");
        object data_part = antag_panel_data();
        object objective_part = antag_panel_objectives();

        DmList parts = listtrim(Dm.list(command_part, data_part, objective_part)) as DmList;

        return parts.Join("<br>");
    }

    // tg: code/modules/admin/antag_panel.dm:42
    public virtual object antag_panel_objectives()
    {
        string result = "<i><b>Personal Objectives</b></i>:<br>";
        if (objectives.len == 0)
            result += "EMPTY<br>";
        else
        {
            float obj_count = 1;
            foreach (object o in Dm.each(objectives))
            {
                datum_objective objective = (datum_objective)o;
                result += Dm.text("<B>[]</B>: [] <a href='byond://?src=[];obj_edit=[]'>Edit</a> <a href='byond://?src=[];obj_delete=[]'>Delete</a> <a href='byond://?src=[];obj_completed=[]'><font color=[]>[]</font></a> <br>",
                    obj_count, objective.explanation_text, REF(owner), REF(objective), REF(owner), REF(objective), REF(owner), REF(objective),
                    Dm.truthy(objective.check_completion()) ? "green" : "red", Dm.truthy(objective.completed) ? "Mark as incomplete" : "Mark as complete");
                obj_count++;
            }
        }
        result += Dm.text("<a href='byond://?src=[];obj_add=1;target_antag=[]'>Add objective</a><br>", REF(owner), REF(this));
        result += Dm.text("<a href='byond://?src=[];obj_prompt_custom=1;target_antag=[]'>Prompt custom objective entry</a><br>", REF(owner), REF(this));
        result += Dm.text("<a href='byond://?src=[];obj_announce=1'>Announce objectives</a><br>", REF(owner));
        return result;
    }
}

// tg: code/modules/admin/antag_panel.dm:60
public partial class datum_mind : datum
{
    // tg: code/modules/admin/antag_panel.dm:60
    public virtual object get_common_admin_commands()
    {
        string common_commands = "<span>Common Commands:</span>";
        if (ishuman(current))
            common_commands += Dm.text("<a href='byond://?src=[];common=undress'>undress</a>", REF(this));
        else if (iscyborg(current))
        {
            mob_living_silicon_robot R = (mob_living_silicon_robot)current;
            if (Dm.truthy(R.emagged))
                common_commands += Dm.text("<a href='byond://?src=[];silicon=Unemag'>Unemag</a>", REF(this));
        }
        else if (isAI(current))
        {
            mob_living_silicon_ai A = (mob_living_silicon_ai)current;
            if (Dm.truthy(A.connected_robots.len))
            {
                foreach (mob_living_silicon_robot R in Dm.each<mob_living_silicon_robot>(A.connected_robots))
                {
                    if (Dm.truthy(R.emagged))
                    {
                        common_commands += Dm.text("<a href='byond://?src=[];silicon=unemagcyborgs'>Unemag slaved cyborgs</a>", REF(this));
                        break;
                    }
                }
            }
        }
        return common_commands;
    }

    /**
     * Returns a list of "statuses" this mind has - like "Infected", "Mindshielded", etc
     */
    // tg: code/modules/admin/antag_panel.dm:80
    public virtual object get_special_statuses()
    {
        DmList result = LAZYCOPY(special_statuses);
        if (current == null)
            Dm.append(ref result, span_bad("No body!"));
        if (current != null && HAS_TRAIT(current, TRAIT_MINDSHIELD))
            Dm.append(ref result, span_good("Mindshielded"));
        if (current != null && HAS_MIND_TRAIT(current, TRAIT_UNCONVERTABLE))
            Dm.append(ref result, span_good("Unconvertable"));
        return result;
    }

    /**
     * Returns a list of "roles" this mind has - like "Traitor", "Ex Head Rev", "Emagged", etc
     */
    // tg: code/modules/admin/antag_panel.dm:93
    public virtual object get_special_roles()
    {
        DmList roles = LAZYCOPY(special_roles);
        if (iscyborg(current))
        {
            mob_living_silicon_robot robot = (mob_living_silicon_robot)current;
            if (Dm.truthy(robot.emagged))
                Dm.append(ref roles, "Emagged");
        }
        return roles;
    }

    // tg: code/modules/admin/antag_panel.dm:101
    public virtual async DmTask traitor_panel()
    {
        if (!Dm.truthy(SSticker.HasRoundStarted()))
        {
            await tgui_alert(usr, "Not before round-start!", "Alert");
            return;
        }
        if (QDELETED(this))
        {
            await tgui_alert(usr, "This mind doesn't have a mob, or is deleted! For some reason!", "Edit Memory");
            return;
        }

        string @out = Dm.text("<B>[]</B>[]<br>", name, (current != null && !Dm.eq(current.real_name, name)) ? Dm.text(" (as [])", current.real_name) : "");
        @out += Dm.text("Mind currently owned by key: [] []<br>", key, Dm.truthy(active) ? "(synced)" : "(not synced)");
        @out += Dm.text("Assigned role: []. <a href='byond://?src=[];role_edit=1'>Edit</a><br>", assigned_role.title, REF(this));
        @out += Dm.text("<a href='byond://?_src_=holder;[];check_teams=1'>Show Teams</a><br><br>", HrefToken());

        DmList special_statuses = Dm.bitor(get_special_roles(), get_special_statuses()) as DmList;
        if (Dm.truthy(length(special_statuses)))
            @out += Dm.text("Roles: []<br>", jointext(special_statuses, " | "));

        if (!Dm.truthy(GLOB.antag_prototypes))
        {
            GLOB.antag_prototypes = Dm.list();
            foreach (object antag_type in Dm.each(subtypesof(typeof(datum_antagonist))))
            {
                datum_antagonist A = Dm.@new(antag_type as Type) as datum_antagonist;
                object cat_id = A.antagpanel_category;
                if (!Dm.truthy(((DmList)GLOB.antag_prototypes)[cat_id]))
                    ((DmList)GLOB.antag_prototypes)[cat_id] = Dm.list(A);
                else
                    Dm.append((DmList)((DmList)GLOB.antag_prototypes)[cat_id], A);
            }
        }
        sortTim(GLOB.antag_prototypes as DmList, "/proc/" + nameof(cmp_text_asc), associative: TRUE);

        DmList sections = Dm.list();
        DmList priority_sections = Dm.list();

        foreach (object antag_category in Dm.each(GLOB.antag_prototypes))
        {
            string category_header = Dm.text("<i><b>[]:</b></i>", antag_category);
            DmList antag_header_parts = Dm.list(category_header);

            datum_antagonist current_antag = null;
            DmList possible_admin_antags = Dm.list();

            foreach (datum_antagonist prototype in Dm.each<datum_antagonist>(((DmList)GLOB.antag_prototypes)[antag_category]))
            {
                datum_antagonist A = has_antag_datum(prototype.type) as datum_antagonist;
                if (A != null)
                {
                    //We got the antag
                    if (current_antag == null)
                        current_antag = A;
                    else
                        continue; //Let's skip subtypes of what we already shown.
                }
                else if (Dm.truthy(prototype.show_in_antagpanel))
                {
                    // DEVIATION(DV340): an inert antagonist type (its package not merged yet) is greyed out, as one that cannot be owned
                    if (Dm.truthy(prototype.can_be_owned(this)) && !Attribute.IsDefined(prototype.GetType(), typeof(DmInertAttribute), inherit: false))
                        Dm.append(ref possible_admin_antags, Dm.text("<a href='byond://?src=[];add_antag=[]' title='[]'>[]</a>", REF(this), prototype.type, prototype.type, prototype.name));
                    else
                        Dm.append(ref possible_admin_antags, Dm.text("<a class='linkOff'>[]</a>", prototype.name));
                }
                else
                {
                    //We don't have it and it shouldn't be shown as an option to be added.
                    continue;
                }
            }

            if (current_antag == null) //Show antagging options
            {
                if (Dm.truthy(possible_admin_antags.len))
                {
                    Dm.append(ref antag_header_parts, span_highlight("None"));
                    Dm.append(ref antag_header_parts, possible_admin_antags);
                }
                else
                {
                    //If there's no antags to show in this category skip the section completely
                    continue;
                }
            }
            else //Show removal and current one
            {
                Dm.combine(ref priority_sections, antag_category);
                Dm.append(ref antag_header_parts, span_bad(Dm.text("[]", current_antag.name)));
                Dm.append(ref antag_header_parts, Dm.text("<a href='byond://?src=[];remove_antag=[]'>Remove</a>", REF(this), REF(current_antag)));
                Dm.append(ref antag_header_parts, Dm.text("<a href='byond://?src=[];open_antag_vv=[]'>Open VV</a>", REF(this), REF(current_antag)));
            }

            //We aren't antag of this category, grab first prototype to check the prefs (This is pretty vague but really not sure how else to do this)
            datum_antagonist pref_source = current_antag;
            if (pref_source == null)
            {
                foreach (datum_antagonist prototype in Dm.each<datum_antagonist>(((DmList)GLOB.antag_prototypes)[antag_category]))
                {
                    if (!Dm.truthy(prototype.show_in_antagpanel))
                        continue;
                    pref_source = prototype;
                    break;
                }
            }
            if (Dm.truthy(pref_source.pref_flag))
                Dm.append(ref antag_header_parts, Dm.truthy(pref_source.enabled_in_preferences(this)) ? "Enabled in Prefs" : "Disabled in Prefs");

            //Traitor : None | Traitor | IAA
            // Command1 | Command2 | Command3
            // Secret Word : Banana
            // Objectives:
            // 1.Do the thing [a][b]
            // [a][b]
            // Memory:
            // Uplink Code: 777 Alpha
            string cat_section = antag_header_parts.Join(" | ") + "<br>";
            if (current_antag != null)
                cat_section += Dm.str(current_antag.antag_panel());
            sections[antag_category] = cat_section;
        }

        foreach (object s in Dm.each(priority_sections))
            @out += Dm.str(sections[s]);
        foreach (object s in Dm.each(Dm.sub(sections, priority_sections)))
            @out += Dm.str(sections[s]);

        @out += "<br>";

        //Uplink
        if (ishuman(current))
        {
            string uplink_info = "<i><b>Uplink</b></i>:";
            datum_component_uplink U = find_syndicate_uplink() as datum_component_uplink;
            if (U != null)
            {
                if (Dm.truthy(check_rights(R_FUN, 0)))
                    uplink_info += Dm.text(", <a href='byond://?src=[];common=crystals'>[]</a> TC", REF(this), U.uplink_handler.telecrystals);
                else
                    uplink_info += Dm.text(", [] TC", U.uplink_handler.telecrystals);
            }
            else
                uplink_info += Dm.text("<a href='byond://?src=[];common=uplink'>give</a>", REF(this));
            uplink_info += "."; //hiel grammar

            @out += uplink_info + "<br>";
        }
        //Other stuff
        @out += Dm.str(get_common_admin_commands());

        datum_browser panel = Dm.@new<datum_browser>(usr, "traitorpanel", "", 600, 600);
        panel.set_content(@out);
        panel.open();
        return;
    }
}
