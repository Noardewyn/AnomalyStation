// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/check_antagonists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A1 (D316): the whole file, the Round Status page of Check Antagonists (the admin console's check-antagonists,
// D275). Its page is tg's browse() HTML in the port's Browser window (DV216). Its links to the admin tools that are not ported (the
// player panel, the shuttle and round controls, CTF, the variable editor) reach nothing: the holder's Topic keys
// are not translated (not-translated.md section 130); PM, FLW-less Show Objective, Check Teams and (syndicate A3) the Dynamic Panel work.

//I wish we had interfaces sigh, and i'm not sure giving team and antag common root is a better solution here

// tg: code/modules/admin/check_antagonists.dm:4
public partial class datum_antagonist : datum
{
    //Name shown on antag list
    // tg: code/modules/admin/check_antagonists.dm:4
    public virtual object antag_listing_name()
    {
        if (owner == null)
            return "Unassigned";
        if (owner.current != null)
            return Dm.text("<a href='byond://?_src_=holder;[];adminplayeropts=[]'>[]</a> ", HrefToken(), REF(owner.current), owner.current.real_name);
        else
            return Dm.text("<a href='byond://?_src_=vars;[];Vars=[]'>[]</a> ", HrefToken(), REF(owner), owner.name);
    }

    //Whatever interesting things happened to the antag admins should know about
    //Include additional information about antag in this part
    // tg: code/modules/admin/check_antagonists.dm:14
    public virtual object antag_listing_status()
    {
        if (owner == null)
            return "(Unassigned)";
        if (owner.current == null)
            return "<font color=red>(Body destroyed)</font>";
        else
        {
            if (owner.current.stat == DEAD)
                return "<font color=red>(DEAD)</font>";
            else if (owner.current.client == null)
                return "(No client)";
        }
        return null;
    }

    //Builds the common FLW PM TP commands part
    //Probably not going to be overwritten by anything but you never know
    // tg: code/modules/admin/check_antagonists.dm:27
    public virtual object antag_listing_commands()
    {
        if (owner == null)
            return null;
        DmList parts = Dm.list();
        Dm.append(ref parts, Dm.text("<a href='byond://?priv_msg=[]'>PM</a>", Dm.ckey(owner.key)));
        if (owner.current != null) //There's body to follow
            Dm.append(ref parts, Dm.text("<a href='byond://?_src_=holder;[];adminplayerobservefollow=[]'>FLW</a>", HrefToken(), REF(owner.current)));
        else
            Dm.append(ref parts, "");
        Dm.append(ref parts, Dm.text("<a href='byond://?_src_=holder;[];traitor=[]'>Show Objective</a>", HrefToken(), REF(owner)));
        return parts; //Better as one cell or two/three
    }

    //Builds table row for the antag
    // Jim (Status) FLW PM TP
    // tg: code/modules/admin/check_antagonists.dm:41
    public virtual object antag_listing_entry()
    {
        DmList parts = Dm.list();
        if (Dm.truthy(show_name_in_check_antagonists))
            Dm.append(ref parts, Dm.text("[]([])", antag_listing_name(), name));
        else
            Dm.append(ref parts, antag_listing_name());
        Dm.append(ref parts, antag_listing_status());
        Dm.append(ref parts, antag_listing_commands());
        return Dm.text("<tr><td>[]</td></tr>", parts.Join("</td><td>"));
    }
}

// tg: code/modules/admin/check_antagonists.dm:51
public partial class datum_admins : datum
{
    // tg: code/modules/admin/check_antagonists.dm:51
    public virtual object build_antag_listing()
    {
        DmList sections = Dm.list();
        DmList priority_sections = Dm.list();

        DmList all_teams = Dm.list();
        DmList all_antagonists = Dm.list();

        foreach (datum_antagonist A in Dm.each<datum_antagonist>(GLOB.antagonists))
        {
            if (A.owner == null)
                continue;
            Dm.combine(ref all_teams, A.get_team());
            Dm.append(ref all_antagonists, A);
        }

        foreach (datum_team T in Dm.each<datum_team>(all_teams))
        {
            foreach (datum_antagonist X in Dm.each<datum_antagonist>(all_antagonists))
            {
                if (X.get_team() == T)
                    Dm.remove(ref all_antagonists, X);
            }
            Dm.append(ref sections, T.antag_listing_entry());
        }

        sortTim(all_antagonists, "/proc/" + nameof(cmp_antag_category));

        object current_category = null;
        DmList current_section = Dm.list();
        for (float i = 1; i <= all_antagonists.len; i++)
        {
            datum_antagonist current_antag = (datum_antagonist)all_antagonists[i];
            datum_antagonist next_antag = null;
            if (i < all_antagonists.len)
                next_antag = (datum_antagonist)all_antagonists[i + 1];
            if (!Dm.truthy(current_category))
            {
                current_category = current_antag.roundend_category;
                Dm.append(ref current_section, Dm.text("<b>[]</b><br>", capitalize(current_category)));
                Dm.append(ref current_section, "<table cellspacing=5>");
            }
            Dm.append(ref current_section, current_antag.antag_listing_entry()); // Name - (Traitor) - FLW | PM | TP

            if (next_antag == null || !Dm.eq(next_antag.roundend_category, current_antag.roundend_category)) //End of section
            {
                Dm.append(ref current_section, "</table>");
                Dm.append(ref sections, current_section.Join(""));
                current_section.Cut();
                current_category = null;
            }
        }
        DmList all_sections = Dm.add(priority_sections, sections) as DmList;
        return all_sections.Join("<br>");
    }

    // tg: code/modules/admin/check_antagonists.dm:93
    public virtual async DmTask check_antagonists()
    {
        if (!Dm.truthy(SSticker.HasRoundStarted()))
        {
            await tgui_alert(usr, "The game hasn't started yet!");
            return;
        }
        DmList dat = Dm.list("<html><head><meta http-equiv='Content-Type' content='text/html; charset=UTF-8'><title>Round Status</title></head><body><h1><B>Round Status</B></h1>");
        Dm.append(ref dat, Dm.text("<a href='byond://?_src_=holder;[];gamemode_panel=1'>Dynamic Panel</a><br>", HrefToken()));
        Dm.append(ref dat, Dm.text("Round Duration: <B>[]</B><BR>", DisplayTimeText(Dm.num(world.time) - Dm.num(SSticker.round_start_time))));
        Dm.append(ref dat, "<B>Emergency shuttle</B><BR>");
        if (datum_controller_subsystem_shuttle.EMERGENCY_IDLE_OR_RECALLED())
            Dm.append(ref dat, Dm.text("<a href='byond://?_src_=holder;[];call_shuttle=1'>Call Shuttle</a><br>", HrefToken()));
        else if (SSshuttle.emergency != null) // a runtime in tg: a map without an emergency shuttle (SSshuttle only warns)
        {
            float timeleft = Dm.num(SSshuttle.emergency.timeLeft());
            if (Dm.eq(SSshuttle.emergency.mode, SHUTTLE_CALL))
            {
                Dm.append(ref dat, Dm.text("ETA: <a href='byond://?_src_=holder;[];edit_shuttle_time=1'>[]:[]</a><BR>", HrefToken(), Dm.mod(timeleft / 60, 60), add_leading(num2text(Dm.mod(timeleft, 60)), 2, "0")));
                Dm.append(ref dat, Dm.text("<a href='byond://?_src_=holder;[];call_shuttle=2'>Send Back</a><br>", HrefToken()));
            }
            else
                Dm.append(ref dat, Dm.text("ETA: <a href='byond://?_src_=holder;[];edit_shuttle_time=1'>[]:[]</a><BR>", HrefToken(), Dm.mod(timeleft / 60, 60), add_leading(num2text(Dm.mod(timeleft, 60)), 2, "0")));
        }
        Dm.append(ref dat, Dm.text("<a href='byond://?_src_=holder;[];end_round=[]'>End Round Now</a><br>", HrefToken(), REF(usr)));
        if (Dm.truthy(SSticker.delay_end))
            Dm.append(ref dat, Dm.text("<a href='byond://?_src_=holder;[];undelay_round_end=1'>Undelay Round End</a><br>", HrefToken()));
        else
            Dm.append(ref dat, Dm.text("<a href='byond://?_src_=holder;[];delay_round_end=1'>Delay Round End</a><br>", HrefToken()));
        Dm.append(ref dat, Dm.text("<a href='byond://?_src_=holder;[];ctf_toggle=1'>Enable/Disable CTF</a><br>", HrefToken()));
        Dm.append(ref dat, Dm.text("<a href='byond://?_src_=holder;[];rebootworld=1'>Reboot World</a><br>", HrefToken()));
        Dm.append(ref dat, Dm.text("<a href='byond://?_src_=holder;[];check_teams=1'>Check Teams</a>", HrefToken()));
        float connected_players = GLOB.clients.len;
        float lobby_players = 0;
        float observers = 0;
        float observers_connected = 0;
        float living_players = 0;
        float living_players_connected = 0;
        float antagonists = 0;
        float antagonists_dead = 0;
        float brains = 0;
        float other_players = 0;
        float living_skipped = 0;
        float drones = 0;
        float security = 0;
        float security_dead = 0;
        foreach (mob checked_mob in Dm.each<mob>(GLOB.mob_list))
        {
            if (Dm.truthy(checked_mob.ckey))
            {
                if (isnewplayer(checked_mob))
                {
                    lobby_players++;
                    continue;
                }
                else if (checked_mob.mind != null && !isbrain(checked_mob) && !isobserver(checked_mob))
                {
                    if (checked_mob.stat != DEAD)
                    {
                        if (isdrone(checked_mob))
                        {
                            drones++;
                            continue;
                        }
                        if (Dm.truthy(is_centcom_level(checked_mob.z)))
                        {
                            living_skipped++;
                            continue;
                        }
                        living_players++;
                        if (checked_mob.client != null)
                            living_players_connected++;
                    }
                    else if (Dm.truthy(checked_mob.ckey))
                    {
                        // This finds all dead mobs that still have a ckey inside them
                        // Ie, they have died, but have not ghosted
                        observers++;
                        if (checked_mob.client != null)
                            observers_connected++;
                    }

                    if (Dm.truthy(checked_mob.is_antag()))
                    {
                        antagonists++;
                        if (checked_mob.stat == DEAD)
                            antagonists_dead++;
                    }
                    if (Dm.truthy(checked_mob.mind.assigned_role?.departments_list?.Find(typeof(datum_job_department_security))))
                    {
                        security++;
                        if (checked_mob.stat == DEAD)
                            security_dead++;
                    }
                }
                else if (checked_mob.stat == DEAD || isobserver(checked_mob))
                {
                    observers++;
                    if (checked_mob.client != null)
                        observers_connected++;
                }
                else if (isbrain(checked_mob))
                    brains++;
                else
                    other_players++;
            }
        }
        Dm.append(ref dat, Dm.text("<BR><b><font color='blue' size='3'>Players:|[] ingame|[] connected|[] lobby|</font></b>", connected_players - lobby_players, connected_players, lobby_players));
        Dm.append(ref dat, Dm.text("<BR><b><font color='green'>Living Players:|[] active|[] disconnected|</font></b>", living_players_connected, living_players - living_players_connected));
        Dm.append(ref dat, Dm.text("<BR><b><font color='#e29300'>Antagonists Players:|[] ingame|[] alive|[] dead|</font></b>", antagonists, antagonists - antagonists_dead, antagonists_dead));
        Dm.append(ref dat, Dm.text("<BR><b><font color='#860e03'>Security Players:|[] ingame|[] alive|[] dead|</font></b>", security, security - security_dead, security_dead));
        Dm.append(ref dat, Dm.text("<BR><b><font color='#bf42f4'>SKIPPED \\[On centcom Z-level\\]: [] living players|[] living drones|</font></b>", living_skipped, drones));
        Dm.append(ref dat, Dm.text("<BR><b><font color='red'>Dead/Observing players:|[] active|[] disconnected|[] brains|</font></b>", observers_connected, observers - observers_connected, brains));
        if (other_players > 0)
            Dm.append(ref dat, Dm.text("<BR>[]", span_userdanger(Dm.text("[] players in invalid state or the statistics code is bugged!", other_players))));
        Dm.append(ref dat, "<br><br>");

        Dm.append(ref dat, build_antag_listing());

        Dm.append(ref dat, "</body></html>");
        datum_browser browser = Dm.@new<datum_browser>(usr, "roundstatus", "Round Status", 500, 500);
        browser.set_content(dat.Join(""));
        browser.open();
    }
}
