// Translated from tgstation (snapshot 1d99bd7f): code/datums/communications.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice G5, D272): the communications controller's announcements and its messages to the consoles (their printed copy
// came with the paperwork, gaps S3). Syndicate A3 (D316): the round-start command report (queue_roundstart_report,
// send_roundstart_report with SSdynamic's threat advisory, get_main_report_content); its station goals came with gaps R1 and the
// station traits' lines with gaps R2. Its leaf: the logo picture (the asset transport, DV44).

// tg: code/datums/communications.dm:5  GLOBAL_DATUM_INIT(communications_controller, /datum/communciations_controller, new)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/datums/communications.dm:5
    public datum_communciations_controller communications_controller;

    // tg: code/datums/communications.dm:5  (GLOBAL_MANAGED(communications_controller, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcommunications_controller()
    {
        communications_controller = Dm.@new<datum_communciations_controller>();
        Dm.append(ref gvars_datum_init_order, "communications_controller");
    }
}

// tg: code/datums/communications.dm:7
public partial class datum_communciations_controller : datum
{
    // tg: code/datums/communications.dm:1  #define COMMUNICATION_COOLDOWN (30 SECONDS), COMMUNICATION_COOLDOWN_AI (30 SECONDS)
    private const float COMMUNICATION_COOLDOWN = 300;
    private const float COMMUNICATION_COOLDOWN_AI = 300;

    // tg: code/datums/communications.dm:34
    public virtual object can_announce(mob_living user = null, object is_silicon = null)
    {
        if (Dm.truthy(is_silicon) && silicon_message_cooldown <= Dm.world.time) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm), inline
            return TRUE;
        else if (!Dm.truthy(is_silicon) && nonsilicon_message_cooldown <= Dm.world.time)
            return TRUE;
        else
            return FALSE;
    }

    // tg: code/datums/communications.dm:42
    public virtual async DmTask<object> make_announcement(mob_living user = null, object is_silicon = null, object input = null, object syndicate = null, DmList players = null)
    {
        if (!Dm.truthy(can_announce(user, is_silicon)))
            return FALSE;
        if (Dm.truthy(is_silicon))
        {
            minor_announce(html_decode(input), Dm.text("[] announces:", user.name), players: players);
            silicon_message_cooldown = Dm.world.time + COMMUNICATION_COOLDOWN_AI; // COOLDOWN_START, inline
        }
        else
        {
            var message_data = (DmList)user.treat_message(input);
            if (Dm.truthy(syndicate))
                priority_announce(html_decode(message_data["message"]), null, Dm.resource("sound/announcer/announcement/announce_syndi.ogg"), ANNOUNCEMENT_TYPE_SYNDICATE, has_important_message: TRUE, players: players, color_override: "red");
            else
                priority_announce(html_decode(message_data["message"]), null, Dm.resource("sound/announcer/announcement/announce.ogg"), ANNOUNCEMENT_TYPE_CAPTAIN, has_important_message: TRUE, players: players);
            nonsilicon_message_cooldown = Dm.world.time + COMMUNICATION_COOLDOWN;
        }
        // tg: :54  user.log_talk(input, LOG_SAY, tag="priority announcement"): the say log (the admin logs, M10+)
        message_admins(Dm.text("[] has made a priority announcement.", key_name_admin(user))); // tg: ADMIN_LOOKUPFLW(user): the admin links (M10+)
        return null;
    }

    // tg: code/datums/communications.dm:58
    public virtual void send_message(datum_comm_message sending = null, object print = null, object unique = null, object contains_advanced_html = null)
    {
        print ??= TRUE;
        unique ??= FALSE;
        contains_advanced_html ??= FALSE;
        foreach (obj_machinery_computer_communications C in Dm.each<obj_machinery_computer_communications>(GLOB.shuttle_caller_list))
        {
            if (!Dm.truthy(Dm.bitand(C.machine_stat, Dm.bitor(BROKEN, NOPOWER))) && Dm.truthy(is_station_level(C.z)))
            {
                if (Dm.truthy(unique))
                {
                    C.add_message(sending);
                }
                else //We copy the message for each console, answers and deletions won't be shared
                {
                    var M = Dm.@new<datum_comm_message>(sending.title, sending.content, sending.possible_answers?.Copy());
                    C.add_message(M);
                }
                if (Dm.truthy(print)) // tg: :67-72 (gaps S3: the printed copy)
                {
                    obj_item_paper printed_paper = Dm.@new<obj_item_paper>(C.loc);
                    printed_paper.name = I18n.T("paper - '[]'", sending.title);
                    printed_paper.add_raw_text(Dm.text("</center>[]", sending.content), advanced_html: contains_advanced_html);
                    printed_paper.color = "#deebff";
                    printed_paper.update_appearance();
                }
            }
        }
    }

    // Called AFTER everyone is equipped with their job
    // tg: code/datums/communications.dm:74
    public virtual void queue_roundstart_report()
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(send_roundstart_report)), rand(waittime_l, waittime_h), file: "code/datums/communications.dm", line: 75);
    }

    // tg: code/datums/communications.dm:77
    public virtual async DmTask<object> send_roundstart_report(object greenshift = null)
    {
        if (Dm.truthy(block_command_report)) //If we don't want the report to be printed just yet, we put it off until it's ready
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(send_roundstart_report), greenshift), 10 * SECONDS, file: "code/datums/communications.dm", line: 79);
            return null;
        }

        string dot = "";
        // . += "<center><img src='[SSassets.transport.get_asset_url("nanotrasen-logo")]' width='50%'></center><hr>" (:83): the asset
        // transport is the client's (DV44), which draws no web picture in the paper
        Dm.Deferred("code/datums/communications.dm:83", "send_roundstart_report: the Nanotrasen logo picture of the report (the asset transport, DV44)");
        dot += Dm.text("<center><h2>[], TCD [] []</h2></center><hr>", command_name(), time2text(world.realtime, "DDD, MMM DD"), CURRENT_STATION_YEAR);
        dot += Dm.truthy(command_report_main_content) ? command_report_main_content : Dm.str(get_main_report_content());
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_dynamic_report)))) // CONFIG_GET(flag/no_dynamic_report)
        {
            if (greenshift == null)
                greenshift = Dm.num(Dm.eq(SSdynamic.current_tier.tier, 0));
        }
        else
        {
            object dynamic_report = SSdynamic.get_advisory_report();
            if (greenshift == null) // if we're not forced to be greenshift or not - check if we are an actual greenshift
                greenshift = Dm.num(Dm.eq(SSdynamic.current_tier.tier, 0) && Dm.eq(dynamic_report, Dm.initial(typeof(datum_dynamic_tier_greenshift), "advisory_report")));

            dot += "<hr><h3>Nanotrasen Department of Intelligence Threat Advisory, Spinward Sector:</h3>";
            dot += Dm.str(dynamic_report);
        }

        // tg: :98  (gaps R1: the station goals)
        await SSstation.generate_station_goals(Dm.truthy(greenshift) ? (object)INFINITY : config.Get(typeof(datum_config_entry_number_station_goal_budget))); // CONFIG_GET(number/station_goal_budget)

        DmList station_goal_strings = Dm.list();
        if (Dm.truthy(greenshift))
        {
            Dm.append(ref station_goal_strings, "All special orders have been authorized for the shift. Feel free to pick one your crew wishes to specialize in - you are not expected to complete them all.");
            announced_greenshift = TRUE;
        }
        else
        {
            foreach (datum_station_goal station_goal in Dm.each_cast<datum_station_goal>(SSstation.get_station_goals()))
            {
                station_goal.on_report();
                Dm.append(ref station_goal_strings, station_goal.get_report());
            }
        }

        if (Dm.length(station_goal_strings) > 0) // if we have any special orders to report, add them in
        {
            dot += Dm.text("<hr><h4>Special Orders for []:</h4>", station_name());
            dot += station_goal_strings.Join("<hr>");
        }

        DmList trait_list_strings = Dm.list();
        foreach (datum_station_trait station_trait in Dm.each_cast<datum_station_trait>(SSstation.station_traits))
        {
            if (!Dm.truthy(station_trait.show_in_report))
                continue;
            Dm.append(ref trait_list_strings, Dm.text("[]<BR>", station_trait.get_report()));
        }
        if (Dm.length(trait_list_strings) > 0)
            dot += "<hr><h4>Identified shift divergencies:</h4>" + trait_list_strings.Join("");

        if (Dm.truthy(Dm.length(command_report_footnotes)))
        {
            string footnote_pile = "";

            foreach (datum_command_footnote footnote in Dm.each_cast<datum_command_footnote>(command_report_footnotes))
            {
                footnote_pile += Dm.text("[]<BR>", footnote.message);
                footnote_pile += Dm.text("~ <i>[]</i><BR>", footnote.signature);
                footnote_pile += "<BR>";
            }

            dot += "<hr><h4>Additional Notes: </h4>" + footnote_pile;
        }

        print_command_report(dot, Dm.text("[] Status Summary", command_name()), announce: FALSE, contains_advanced_html: TRUE);
        if (Dm.truthy(greenshift))
        {
            priority_announce(
                Dm.text("Thanks to the tireless efforts of our security and intelligence divisions, there are currently no credible threats to []. All station construction projects have been authorized. Have a secure shift!", station_name()),
                "Security Report",
                ((datum_centcom_announcer)SSstation.announcer).get_rand_report_sound(),
                color_override: "green"
            );
        }
        else if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_roundstart_blue_alert)))) // CONFIG_GET(flag/roundstart_blue_alert)
        {
            if (Dm.lt(SSsecurity_level.get_current_level_as_number(), SEC_LEVEL_BLUE))
                SSsecurity_level.set_level(SEC_LEVEL_BLUE, announce: FALSE);
            priority_announce(
                Dm.text("[]\n\nA summary has been copied and printed to all communications consoles.", SSsecurity_level.current_security_level.elevating_to_announcement),
                "Security level elevated.",
                ANNOUNCER_INTERCEPT,
                color_override: SSsecurity_level.current_security_level.announcement_color
            );
        }
        else
        {
            priority_announce(
                "A summary of the station's situation has been copied and printed to all communications consoles.",
                "Security Report",
                ((datum_centcom_announcer)SSstation.announcer).get_rand_report_sound()
            );
        }

        return dot;
    }

    /// Return a random flavor/meme report to use in the command report
    // tg: code/datums/communications.dm:165
    public virtual object get_main_report_content()
    {
        if (istype(SSstation.announcer, typeof(datum_centcom_announcer_intern)))
            return pick_list_replacements("flavor_reports.json", "intern_reports");
        return pick_list_replacements("flavor_reports.json", "reports");
    }
}
