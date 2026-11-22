// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/datums/operative_team.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1a, D316: is_infiltrator_docked_at_syndiebase; N1b: the team) but the leaves: the report's badass icon
// (icon2html, the chat icons) and the admin panel's links (HrefToken links, the admin tools, M10: the listing carries their text). The
// var blocks are generated (D111).

public static partial class GlobalProcs
{
    /// Returns whether or not syndicate operatives escaped.
    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:300
    public static bool is_infiltrator_docked_at_syndiebase()
    {
        var infiltrator_port = SSshuttle.getShuttle("syndicate") as obj_docking_port_mobile_infiltrator;

        var nukie_template = GLOB.lazy_templates[LAZY_TEMPLATE_KEY_NUKIEBASE] as datum_lazy_template_nukie_base;
        if (nukie_template == null)
            return false; // if its not even loaded, cant be docked

        foreach (datum_turf_reservation loaded_area in Dm.each<datum_turf_reservation>(nukie_template.reservations))
        {
            object infiltrator_turf = get_turf(infiltrator_port);
            if (Dm.@in(infiltrator_turf, loaded_area.reserved_turfs))
                return true;
        }
        return false;
    }
}

// tg: code/modules/antagonists/nukeop/datums/operative_team.dm:4
public partial class datum_team_nuclear : datum_team
{
    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:1  #define SPAWN_AT_BASE "Nuke base"
    private const string SPAWN_AT_BASE = "Nuke base";
    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:2  #define SPAWN_AT_INFILTRATOR "Infiltrator"
    private const string SPAWN_AT_INFILTRATOR = "Infiltrator";

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:12
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        syndicate_name = GlobalProcs.syndicate_name();

        var maingoal = await Dm.new_async(core_objective) as datum_objective;
        maingoal.team = this;
        Dm.append(ref objectives, maingoal);

        // when a nuke team is created, the infiltrator has not loaded in yet - it takes some time. so no nuke, we have to wait
        _addtimer(Dm.@new<datum_callback>(this, nameof(assign_nuke_delayed)), 5 * SECONDS, file: "code/modules/antagonists/nukeop/datums/operative_team.dm", line: 21);
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:23
    public override object roundend_report()
    {
        DmList parts = Dm.list();
        parts.Add(span_header(Dm.text("[] Operatives:", syndicate_name)));

        switch (get_result())
        {
            case NUKE_RESULT_FLUKE:
                parts.Add("<span class='redtext big'>Humiliating Syndicate Defeat</span>");
                parts.Add(Dm.text("<B>The crew of [] gave [] operatives back their bomb! The syndicate base was destroyed!</B> Next time, don't lose the nuke!", station_name(), syndicate_name));
                break;
            case NUKE_RESULT_NUKE_WIN:
                parts.Add("<span class='greentext big'>Syndicate Major Victory!</span>");
                parts.Add(Dm.text("<B>[] operatives have destroyed []!</B>", syndicate_name, station_name()));
                break;
            case NUKE_RESULT_NOSURVIVORS:
                parts.Add("<span class='neutraltext big'>Total Annihilation!</span>");
                parts.Add(Dm.text("<B>[] operatives destroyed [] but did not leave the area in time and got caught in the explosion.</B> Next time, don't lose the disk!", syndicate_name, station_name()));
                break;
            case NUKE_RESULT_WRONG_STATION:
                parts.Add("<span class='redtext big'>Crew Minor Victory!</span>");
                parts.Add(Dm.text("<B>[] operatives secured the authentication disk but blew up something that wasn't [].</B> Next time, don't do that!", syndicate_name, station_name()));
                break;
            case NUKE_RESULT_WRONG_STATION_DEAD:
                parts.Add(Dm.text("<span class='redtext big'>[] operatives have earned Darwin Award!</span>", syndicate_name));
                parts.Add(Dm.text("<B>[] operatives blew up something that wasn't [] and got caught in the explosion.</B> Next time, don't do that!", syndicate_name, station_name()));
                break;
            case NUKE_RESULT_HIJACK_DISK:
                parts.Add("<span class='greentext big'>Syndicate Miniscule Victory!</span>");
                parts.Add(Dm.text("<B>[] operatives failed to destroy [], but they managed to secure the disk and hijack the emergency shuttle, causing it to land on the syndicate base. Good job?</B>", syndicate_name, station_name()));
                break;
            case NUKE_RESULT_HIJACK_NO_DISK:
                parts.Add("<span class='greentext big'>Syndicate Insignificant Victory!</span>");
                parts.Add(Dm.text("<B>[] operatives failed to destroy [] or secure the disk, but they managed to hijack the emergency shuttle, causing it to land on the syndicate base. Good job?</B>", syndicate_name, station_name()));
                break;
            case NUKE_RESULT_CREW_WIN_SYNDIES_DEAD:
                parts.Add("<span class='redtext big'>Crew Major Victory!</span>");
                parts.Add(Dm.text("<B>The Research Staff has saved the disk and killed the [] Operatives</B>", syndicate_name));
                break;
            case NUKE_RESULT_CREW_WIN:
                parts.Add("<span class='redtext big'>Crew Major Victory!</span>");
                parts.Add(Dm.text("<B>The Research Staff has saved the disk and stopped the [] Operatives!</B>", syndicate_name));
                break;
            case NUKE_RESULT_DISK_LOST:
                parts.Add("<span class='neutraltext big'>Neutral Victory!</span>");
                parts.Add(Dm.text("<B>The Research Staff failed to secure the authentication disk but did manage to kill most of the [] Operatives!</B>", syndicate_name));
                break;
            case NUKE_RESULT_DISK_STOLEN:
                parts.Add("<span class='greentext big'>Syndicate Minor Victory!</span>");
                parts.Add(Dm.text("<B>[] operatives survived the assault but did not achieve the destruction of [].</B> Next time, don't lose the disk!", syndicate_name, station_name()));
                break;
            default:
                parts.Add("<span class='neutraltext big'>Neutral Victory</span>");
                parts.Add("<B>Mission aborted!</B>");
                break;
        }

        string text = span_header("<br>The syndicate operatives were:");
        string purchases = "";
        float TC_uses = 0;
        LAZYINITLIST(ref GLOB.uplink_purchase_logs_by_key);
        foreach (datum_mind syndicate in Dm.each<datum_mind>(members))
        {
            var H = GLOB.uplink_purchase_logs_by_key[syndicate.key] as datum_uplink_purchase_log;
            if (H != null)
            {
                TC_uses += Dm.num(H.total_spent);
                purchases += Dm.str(H.generate_render(show_key: FALSE));
            }
        }
        text += Dm.str(printplayerlist(members));
        text += "<br>";
        text += Dm.text("(Syndicates used [] TC) []", TC_uses, purchases);
        if (TC_uses == 0 && Dm.truthy(GLOB.station_was_nuked) && !are_all_operatives_dead())
        {
            // text += "<BIG>[icon2html('icons/ui/antags/badass.dmi', world, "badass")]</BIG>": the chat icons
            Dm.Deferred("code/modules/antagonists/nukeop/datums/operative_team.dm:79", "roundend_report: icon2html of the badass icon, operatives who nuked the station with no TC spent (the chat icons)");
        }

        parts.Add(text);

        return Dm.text("<div class='panel redborder'>[]</div>", parts.Join("<br>"));
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:85
    public override object antag_listing_name()
    {
        if (Dm.truthy(syndicate_name))
            return Dm.text("[] Syndicates", syndicate_name);
        else
            return "Syndicates";
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:91
    public override object antag_listing_entry()
    {
        string disk_report = "<b>Nuclear Disk(s)</b><br>";
        disk_report += "<table cellspacing=5>";
        foreach (obj_item_disk_nuclear N in Dm.each<obj_item_disk_nuclear>(SSpoints_of_interest.real_nuclear_disks))
        {
            disk_report += Dm.text("<tr><td>[], ", N.name);
            var disk_loc = N.loc as atom;
            while (disk_loc is not turf && disk_loc != null)
            {
                if (disk_loc is mob M)
                    disk_report += Dm.text("carried by <a href='byond://?_src_=holder;[];adminplayeropts=[]'>[]</a> ", HrefToken(), REF(M), M.real_name);
                if (disk_loc is obj O)
                    disk_report += Dm.text("in \\a [] ", O.name);
                disk_loc = disk_loc.loc as atom;
            }
            disk_report += Dm.text("in [] at ([], [], [])</td><td><a href='byond://?_src_=holder;[];adminplayerobservefollow=[]'>FLW</a></td></tr>",
                disk_loc?.loc, disk_loc?.x, disk_loc?.y, disk_loc?.z, HrefToken(), REF(N));
        }
        disk_report += "</table>";

        string post_report = null;

        bool war_declared = false;
        foreach (obj_item_circuitboard_computer_syndicate_shuttle board in Dm.each<obj_item_circuitboard_computer_syndicate_shuttle>(GLOB.syndicate_shuttle_boards))
        {
            if (Dm.truthy(board.challenge_start_time))
                war_declared = true;
        }

        string force_war_button = "";

        if (war_declared)
        {
            post_report += "<b>War declared.</b>";
            force_war_button = "\\[Force war\\]";
        }
        else
        {
            post_report += "<b>War not declared.</b>";
            var war_button = war_button_ref?.resolve() as obj_item_nuclear_challenge;
            if (war_button != null)
                force_war_button = Dm.text("<a href='byond://?_src_=holder;[];force_war=[]'>\\[Force war\\]</a>", HrefToken(), REF(war_button));
            else
                force_war_button = "\\[Cannot declare war, challenge button missing!\\]";
        }

        post_report += Dm.text("\n[]", force_war_button);
        post_report += Dm.text("\n<a href='byond://?_src_=holder;[];give_reinforcement=[]'>\\[Send Reinforcement\\]</a>", HrefToken(), REF(this));

        string final_report = Dm.str(base.antag_listing_entry());
        final_report += disk_report;
        final_report += post_report;
        return final_report;
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:136
    public virtual async DmTask rename_team(object new_name = null)
    {
        syndicate_name = new_name;
        name = Dm.text("[] Team", syndicate_name);
        foreach (datum_mind synd_mind in Dm.each<datum_mind>(members))
        {
            var synd_datum = synd_mind.has_antag_datum(typeof(datum_antagonist_nukeop)) as datum_antagonist_nukeop;
            if (synd_datum != null)
                await synd_datum.give_alias();
        }
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:143
    public virtual async DmTask admin_spawn_reinforcement(mob admin = null)
    {
        if (!Dm.truthy(check_rights_for(admin.client, R_ADMIN)))
            return;

        object infil_or_nukebase = await tgui_alert(
            admin,
            "Spawn them at the nuke base, or in the Infiltrator?",
            "Where to reinforce?",
            Dm.list(SPAWN_AT_BASE, SPAWN_AT_INFILTRATOR, "Cancel"));

        if (!Dm.truthy(infil_or_nukebase) || Dm.eq(infil_or_nukebase, "Cancel"))
            return;

        object tc_to_spawn = await tgui_input_number(admin, "How much TC to spawn with?", "TC", 0, 100);

        var chosen_one = await SSpolling.poll_ghost_candidates(
            check_jobban: ROLE_OPERATIVE,
            role: ROLE_OPERATIVE,
            poll_time: 30 * SECONDS,
            ignore_category: POLL_IGNORE_SYNDICATE,
            alert_pic: typeof(obj_structure_sign_poster_contraband_gorlex_recruitment),
            role_name_text: "emergency syndicate reinforcement",
            amount_to_pick: 1) as mob;

        if (chosen_one == null)
        {
            await tgui_alert(admin, "No candidates found.", "Recruitment Shortage", Dm.list("OK"));
            return;
        }

        turf spawn_loc = null;
        if (Dm.eq(infil_or_nukebase, SPAWN_AT_INFILTRATOR))
        {
            area spawn_in = null;
            // Prioritize EVA then hallway, if neither can be found default to the first area we can find
            foreach (Type area_type in new[] { typeof(area_shuttle_syndicate_eva), typeof(area_shuttle_syndicate_hallway), typeof(area_shuttle_syndicate) })
            {
                spawn_in = Dm.locate_type(area_type, GLOB.areas) as area; // I'd love to use areas_by_type but the Infiltrator is a unique area
                if (spawn_in != null)
                    break;
            }

            DmList options = Dm.list();
            if (spawn_in != null)
            {
                foreach (turf_open open_turf in Dm.each<turf_open>(spawn_in.get_turfs_from_all_zlevels()))
                {
                    if (Dm.truthy(await open_turf.is_blocked_turf()))
                        continue;
                    options.Add(open_turf);
                }
            }

            if (Dm.truthy(length(options)))
                spawn_loc = pick(options) as turf;
            else
                infil_or_nukebase = SPAWN_AT_BASE;
        }

        if (Dm.eq(infil_or_nukebase, SPAWN_AT_BASE))
            spawn_loc = pick(GLOB.nukeop_base_start) as turf;

        var nukie = Dm.@new<mob_living_carbon_human>(spawn_loc);
        chosen_one.client.prefs.safe_transfer_prefs_to(nukie, is_antag: TRUE);
        nukie.PossessByPlayer(chosen_one.key);

        var antag_datum = Dm.@new<datum_antagonist_nukeop>();
        antag_datum.send_to_spawnpoint = FALSE;
        antag_datum.give_bonus_tc = FALSE;
        antag_datum.nukeop_outfit = typeof(datum_outfit_syndicate_reinforcement);

        await nukie.mind.add_antag_datum(antag_datum, this);

        var uplink = nukie.mind.find_syndicate_uplink() as datum_component_uplink;
        uplink?.uplink_handler.set_telecrystals(tc_to_spawn);

        // add some pizzazz
        do_sparks(4, FALSE, spawn_loc);
        Dm.@new<obj_effect_temp_visual_teleport_abductor_syndi_teleporter>(spawn_loc);
        playsound(spawn_loc, SFX_SPARKS, 50, TRUE);
        playsound(spawn_loc, "sound/effects/phasein.ogg", 50, TRUE);

        await tgui_alert(admin, Dm.text("Reinforcement spawned at [] with [].", infil_or_nukebase, tc_to_spawn), "Reinforcements have arrived", Dm.list("God speed"));
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:219
    public virtual bool is_disk_rescued()
    {
        foreach (obj_item_disk_nuclear nuke_disk in Dm.each<obj_item_disk_nuclear>(SSpoints_of_interest.real_nuclear_disks))
        {
            //If emergency shuttle is in transit disk is only safe on it
            if (Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE))
            {
                if (!Dm.truthy(SSshuttle.emergency.is_in_shuttle_bounds(nuke_disk)))
                    return false;
            }
            //If shuttle escaped check if it's on centcom side
            else if (Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME))
            {
                if (!Dm.truthy(nuke_disk.onCentCom()))
                    return false;
            }
            else //Otherwise disk is safe when on station
            {
                var disk_turf = get_turf(nuke_disk) as turf;
                if (disk_turf == null || !Dm.truthy(is_station_level(disk_turf.z)))
                    return false;
            }
        }
        return true;
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:235
    public virtual bool are_all_operatives_dead()
    {
        foreach (datum_mind operative_mind in Dm.each<datum_mind>(members))
        {
            if (ishuman(operative_mind.current) && operative_mind.current.stat != DEAD)
                return false;
        }
        return true;
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:241
    public virtual int get_result()
    {
        bool shuttle_evacuated = datum_controller_subsystem_shuttle.EMERGENCY_ESCAPED_OR_ENDGAMED();
        bool shuttle_landed_base = Dm.truthy(SSshuttle.emergency.is_hijacked());
        bool disk_rescued = is_disk_rescued();
        bool syndies_didnt_escape = !is_infiltrator_docked_at_syndiebase();
        bool team_is_dead = are_all_operatives_dead();
        object station_was_nuked = GLOB.station_was_nuked;
        object station_nuke_source = GLOB.station_nuke_source;

        // The nuke detonated on the syndicate base
        if (Dm.eq(station_nuke_source, DETONATION_HIT_SYNDIE_BASE))
            return NUKE_RESULT_FLUKE;

        // The station was nuked
        if (Dm.truthy(station_was_nuked))
        {
            // The station was nuked and the infiltrator failed to escape
            if (syndies_didnt_escape)
                return NUKE_RESULT_NOSURVIVORS;
            // The station was nuked and the infiltrator escaped, and the nuke ops won
            else
                return NUKE_RESULT_NUKE_WIN;
        }

        // The station was not nuked, but something was
        else if (Dm.truthy(station_nuke_source) && !disk_rescued)
        {
            // The station was not nuked, but something was, and the syndicates didn't escape it
            if (syndies_didnt_escape)
                return NUKE_RESULT_WRONG_STATION_DEAD;
            // The station was not nuked, but something was, and the syndicates returned to their base
            else
                return NUKE_RESULT_WRONG_STATION;
        }

        // Nuke didn't blow, but nukies somehow hijacked the emergency shuttle to land at the base anyways.
        else if (shuttle_landed_base)
        {
            if (disk_rescued)
                return NUKE_RESULT_HIJACK_DISK;
            else
                return NUKE_RESULT_HIJACK_NO_DISK;
        }

        // No nuke went off, the station rescued the disk
        else if (disk_rescued)
        {
            // No nuke went off, the shuttle left, and the team is dead
            if (shuttle_evacuated && team_is_dead)
                return NUKE_RESULT_CREW_WIN_SYNDIES_DEAD;
            // No nuke went off, but the nuke ops survived
            else
                return NUKE_RESULT_CREW_WIN;
        }

        // No nuke went off, but the disk was left behind
        else
        {
            // No nuke went off, the disk was left, but all the ops are dead
            if (team_is_dead)
                return NUKE_RESULT_DISK_LOST;
            // No nuke went off, the disk was left, there are living ops, but the shuttle left successfully
            else if (shuttle_evacuated)
                return NUKE_RESULT_DISK_STOLEN;
        }

        stack_trace(Dm.text("[] - got an undefined / unexpected result.", type), file: "code/modules/antagonists/nukeop/datums/operative_team.dm", line: 297);
        return -1;
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:313
    public override async DmTask add_member(datum_mind new_member = null)
    {
        await base.add_member(new_member);
        SEND_SIGNAL(COMSIG_NUKE_TEAM_ADDITION, Dm.list(this, new_member.current));
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:317
    public virtual void assign_nuke_delayed(object attempts = null)
    {
        attempts ??= 0;
        if (!assign_nuke())
        {
            if (Dm.num(attempts) > 5)
                stack_trace("Failed to assign nuke to team after multiple attempts. This likely means the nuke was not found. Please investigate.",
                    file: "code/modules/antagonists/nukeop/datums/operative_team.dm", line: 320);
            else
                _addtimer(Dm.@new<datum_callback>(this, nameof(assign_nuke_delayed), Dm.num(attempts) + 1), 5 * SECONDS,
                    file: "code/modules/antagonists/nukeop/datums/operative_team.dm", line: 323);
            return;
        }

        foreach (datum_mind synd_mind in Dm.each<datum_mind>(members))
        {
            var synd_datum = synd_mind.has_antag_datum(typeof(datum_antagonist_nukeop)) as datum_antagonist_nukeop;
            synd_datum?.memorize_code();
        }
    }

    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:330
    public virtual bool assign_nuke()
    {
        var nuke = Dm.locate<obj_machinery_nuclearbomb_syndicate>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_nuclearbomb_syndicate)));
        if (nuke == null)
            return false;

        memorized_code = random_nukecode();
        tracked_nuke = nuke;
        if (Dm.eq(nuke.r_code, NUKE_CODE_UNSET))
            nuke.r_code = Dm.str(memorized_code);
        else //Already set by admins/something else?
            memorized_code = nuke.r_code;
        foreach (obj_machinery_nuclearbomb_beer beernuke in Dm.each<obj_machinery_nuclearbomb_beer>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_nuclearbomb_beer))))
            beernuke.r_code = Dm.str(memorized_code);
        return true;
    }
}

// tg: code/modules/antagonists/nukeop/datums/operative_team.dm:348
public partial class datum_team_nuclear_loneop : datum_team_nuclear
{
    // tg: code/modules/antagonists/nukeop/datums/operative_team.dm:350
    public override bool assign_nuke()
    {
        var nuke = Dm.locate<obj_machinery_nuclearbomb_selfdestruct>(SSmachines.get_machines_by_type(typeof(obj_machinery_nuclearbomb_selfdestruct)));
        if (nuke == null)
            return false;

        memorized_code = random_nukecode();
        tracked_nuke = nuke;
        if (Dm.eq(nuke.r_code, NUKE_CODE_UNSET))
            nuke.r_code = Dm.str(memorized_code);
        else //Already set by admins/something else?
            memorized_code = nuke.r_code;
        return true;
    }
}
