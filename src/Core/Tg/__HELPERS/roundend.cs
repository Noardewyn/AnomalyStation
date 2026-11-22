// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/roundend.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice H): the round's end (declare_completion), the round-end feedback's counts, the survivor and personal reports and
// their display (DV192: in chat, the port's client has no browser window), standard_reboot. Syndicate A1 (D316): the antagonists' data
// and report (gather_antag_data, antag_report, cmp_antag_category, printplayer, printplayerlist, printobjectives), the antagonist HUD for
// all and the antagonists in the game log at the round's end, build_roundend_report as far as its antagonists. Not here: the nuke disk
// (N1), the newscaster, the hardcore random score, the medals, the achievements, the report button and the report files (the statistics
// and the logs: not ported). The AI laws' report came with silicons SI5; the economic summary (market_report) with gaps P4d, but for the
// restaurant venues' lines (SSrestaurant, not ported); the station goals' report (goal_report) with gaps R1; the end credits' roll and
// the commendations (handle_hearts) with gaps R5.

// tg: code/__HELPERS/roundend.dm:9
public partial class datum_controller_subsystem_ticker : datum_controller_subsystem
{
    // tg: code/__HELPERS/roundend.dm:1  #define POPCOUNT_SURVIVORS "survivors", POPCOUNT_ESCAPEES "escapees", POPCOUNT_SHUTTLE_ESCAPEES "shuttle_escapees"
    private const string POPCOUNT_SURVIVORS = "survivors"; //Not dead at roundend
    private const string POPCOUNT_ESCAPEES = "escapees"; //Not dead and on centcom/shuttles marked as escaped
    private const string POPCOUNT_SHUTTLE_ESCAPEES = "shuttle_escapees"; //Emergency shuttle only.

    // #define PERCENT(val) (round((val)*100, 0.1)) (__DEFINES/maths.dm)
    private static float PERCENT(float val) => (float)Math.Round(val * 100 / 0.1, MidpointRounding.AwayFromZero) * 0.1f;

    // tg: code/__HELPERS/roundend.dm:9
    public virtual DmList gather_roundend_feedback()
    {
        gather_antag_data();
        record_nuke_disk_location();
        // tg: :12-14  the round's end data file (round_end_data.json in the log directory) with every mob's data: the logs (M10+)
        Dm.Deferred("code/__HELPERS/roundend.dm:12", "gather_roundend_feedback: the round end data file and its mob data (the logs, not ported)");
        float num_survivors = 0; //Count of non-brain non-eye mobs with mind that are alive
        float num_escapees = 0; //Above and on centcom z
        float num_shuttle_escapees = 0; //Above and on escape shuttle
        DmList shuttle_areas = null;
        if (SSshuttle?.emergency != null)
            shuttle_areas = SSshuttle.emergency.shuttle_areas;

        foreach (mob M in Dm.each<mob>(GLOB.mob_list))
        {
            if (isnewplayer(M))
                continue;
            if (M.mind == null)
                continue;
            if (!Dm.eq(M.stat, DEAD) && !isbrain(M) && !iseyemob(M))
            {
                num_survivors++;
                if (datum_controller_subsystem_shuttle.EMERGENCY_ESCAPED_OR_ENDGAMED() && (Dm.truthy(M.onCentCom()) || Dm.truthy(M.onSyndieBase())))
                {
                    num_escapees++;
                    if (Dm.truthy(shuttle_areas?[get_area(M)]))
                        num_shuttle_escapees++;
                }
            }
        }

        var end_state = Dm.@new<datum_station_state>();
        end_state.count();
        float station_integrity = min(PERCENT(GLOB.start_state?.score(end_state) ?? 0), 100);
        // tg: :97-100  SSblackbox.record_feedback(...) of the counts: the statistics (the statistics database, not ported)

        DmList dot = Dm.list();
        dot[POPCOUNT_SURVIVORS] = num_survivors;
        dot[POPCOUNT_ESCAPEES] = num_escapees;
        dot[POPCOUNT_SHUTTLE_ESCAPEES] = num_shuttle_escapees;
        dot["station_integrity"] = station_integrity;
        return dot;
    }

    // tg: code/__HELPERS/roundend.dm:137  (syndicate N1a, D316)
    public virtual void record_nuke_disk_location()
    {
        int disk_count = 1;
        foreach (obj_item_disk_nuclear nuke_disk in Dm.each<obj_item_disk_nuclear>(SSpoints_of_interest.real_nuclear_disks))
        {
            DmList data = Dm.list();
            var disk_turf = get_turf(nuke_disk) as turf;
            if (disk_turf != null)
            {
                data["x"] = disk_turf.x;
                data["y"] = disk_turf.y;
                data["z"] = disk_turf.z;
            }
            var outer = get_atom_on_turf(nuke_disk, typeof(mob_living), TRUE) as atom;
            if (outer != nuke_disk)
            {
                if (isliving(outer))
                {
                    var disk_holder = (mob_living)outer;
                    data["holder"] = disk_holder.real_name;
                }
                else
                    data["holder"] = outer.name;
            }

            SSblackbox.record_feedback("associative", "roundend_nukedisk", disk_count, data);
            disk_count++;
        }
    }

    // tg: code/__HELPERS/roundend.dm:108
    public virtual void gather_antag_data()
    {
        float team_gid = 1;
        DmList team_ids = Dm.list();

        foreach (datum_antagonist A in Dm.each<datum_antagonist>(GLOB.antagonists))
        {
            if (A.owner == null)
                continue;

            DmList antag_info = Dm.list();
            antag_info["key"] = A.owner.key;
            antag_info["name"] = A.owner.name;
            antag_info["antagonist_type"] = A.type;
            antag_info["antagonist_name"] = A.name; //For auto and custom roles
            antag_info["objectives"] = Dm.list();
            antag_info["team"] = Dm.list();
            datum_team T = A.get_team() as datum_team;
            if (T != null)
            {
                ((DmList)antag_info["team"])["type"] = T.type;
                ((DmList)antag_info["team"])["name"] = T.name;
                if (!Dm.truthy(team_ids[T]))
                    team_ids[T] = team_gid++;
                ((DmList)antag_info["team"])["id"] = team_ids[T];
            }

            if (Dm.truthy(A.objectives.len))
            {
                foreach (datum_objective O in Dm.each<datum_objective>(A.objectives))
                {
                    string result = Dm.truthy(O.check_completion()) ? "SUCCESS" : "FAIL";
                    Dm.append((DmList)antag_info["objectives"], Dm.list(Dm.list(Dm.kv("objective_type", O.type), Dm.kv("text", O.explanation_text), Dm.kv("result", result))));
                }
            }
            SSblackbox.record_feedback("associative", "antagonists", 1, antag_info);
        }
    }

    // tg: code/__HELPERS/roundend.dm:214
    public virtual async DmTask declare_completion(object was_forced = null)
    {
        was_forced ??= END_ROUND_AS_NORMAL;
        // set waitfor = FALSE: the caller (fire) runs it detached

        foreach (datum_callback roundend_callbacks in Dm.each<datum_callback>(round_end_events))
            roundend_callbacks.InvokeAsync();
        // LAZYCLEARLIST(round_end_events) (__HELPERS/_lists.dm), expanded inline
        round_end_events?.Cut();

        // tg: :221  var/speed_round = (STATION_TIME_PASSED() <= 10 MINUTES): the speed round achievement (below)

        if (reboot_hud == null)
            reboot_hud = Dm.@new<atom_movable_screen_reboot_timer>();
        foreach (client C in Dm.each<client>(GLOB.clients))
        {
            if (C?.credits == null) // gaps R5
                C?.RollCredits();
            // COOLDOWN_FINISHED(GLOB, web_sound_cooldown): an admin's web sound (not ported) never holds the title music back
            C?.playtitlemusic(volume_multiplier: 0.5f);
            // tg: :230-231  the speed round achievement (the achievements, not ported)
            C.screen.Add(reboot_hud);
            // tg: :233  HandleRandomHardcoreScore(C): the hardcore random score (the statistics database, not ported)
        }

        DmList popcount = gather_roundend_feedback();
        display_report(popcount);

        CHECK_TICK();

        // Add AntagHUD to everyone, see who was really evil the whole time!
        foreach (datum_atom_hud_alternate_appearance_basic_antagonist_hud antagonist_hud in Dm.each<datum_atom_hud_alternate_appearance_basic_antagonist_hud>(GLOB.active_alternate_appearances))
        {
            foreach (object player in Dm.each(GLOB.player_list))
                antagonist_hud.show_to(player as mob);
        }

        CHECK_TICK();

        //Set news report and mode result
        SSdynamic.set_round_result();

        to_chat(Dm.world, span_infoplain(span_big(span_bold(I18n.T("<BR><BR><BR>The round has ended.")))));
        log_game("The round has ended.");
        // tg: :252-258  the TGS chat announcements and the cross-server news report (not ported)

        CHECK_TICK();

        handle_hearts(); // gaps R5
        set_observer_default_invisibility(0, span_warning(I18n.T("The round is over! You are now visible to the living.")));

        CHECK_TICK();

        //These need update to actually reflect the real antagonists
        //Print a list of antagonists to the server log
        DmList total_antagonists = Dm.list();
        //Look into all mobs in world, dead or alive
        foreach (datum_antagonist A in Dm.each<datum_antagonist>(GLOB.antagonists))
        {
            if (A.owner == null)
                continue;
            if (!Dm.@in(A.name, total_antagonists))
                total_antagonists[A.name] = Dm.list();
            Dm.append((DmList)total_antagonists[A.name], Dm.text("[]", key_name(A.owner)));
        }

        CHECK_TICK();

        //Now print them all into the log!
        log_game("Antagonists at round end were...");
        foreach (object antag_name in Dm.each(total_antagonists))
        {
            DmList L = (DmList)total_antagonists[antag_name];
            log_game(Dm.text("[]s :[].", antag_name, L.Join(", ")));
        }

        CHECK_TICK();
        await SSdbcore.SetRoundEnd();

        //Collects persistence features
        SSpersistence.collect_data();
        SSpersistent_paintings.save_paintings();
        // tg: :292-296  SSblackbox.Seal and the TGS round-end event (not ported)

        await sleep(5 * SECONDS);
        ready_for_reboot = TRUE;
        standard_reboot();
    }

    // tg: code/__HELPERS/roundend.dm:304
    public virtual void standard_reboot()
    {
        if (Dm.truthy(ready_for_reboot))
        {
            if (Dm.truthy(GLOB.station_was_nuked))
                Waiting.Detach(Reboot("Station destroyed by Nuclear Device.", "nuke"));
            else
                Waiting.Detach(Reboot("Round ended.", "proper completion"));
        }
        else
        {
            throw Dm.CRASH("Attempted standard reboot without ticker roundend completion", "code/__HELPERS/roundend.dm", 311);
        }
    }

    // tg: code/__HELPERS/roundend.dm:459  (silicons SI5, D326)
    public virtual string law_report()
    {
        DmList parts = Dm.list();
        bool borg_spacer = false; //inserts an extra linebreak to separate AIs from independent borgs, and then multiple independent borgs.
        //Silicon laws report
        foreach (mob_living_silicon_ai aiPlayer in Dm.each_cast<mob_living_silicon_ai>(GLOB.ai_list))
        {
            datum_mind aiMind = aiPlayer.deployed_shell?.mind ?? aiPlayer.mind;
            if (aiMind != null)
            {
                parts.Add(I18n.T("<b>[]</b> (Played by: <b>[]</b>)'s laws [] were:", aiPlayer.name, aiMind.key,
                    !Dm.eq(aiPlayer.stat, DEAD) ? I18n.T("at the end of the round") : I18n.T("when it was []", span_redtext(I18n.T("deactivated")))));
                parts.Add(aiPlayer.laws.get_law_list(include_zeroth: TRUE));
            }

            parts.Add(I18n.T("<b>Total law changes: []</b>", aiPlayer.law_change_counter));

            if (Dm.truthy(Dm.length(aiPlayer.connected_robots)))
            {
                float borg_num = Dm.length(aiPlayer.connected_robots);
                parts.Add(I18n.T("<br><b>[]</b>'s minions were:", aiPlayer.real_name));
                foreach (mob_living_silicon_robot robo in Dm.each<mob_living_silicon_robot>(aiPlayer.connected_robots))
                {
                    borg_num--;
                    if (robo.mind != null)
                        parts.Add(I18n.T("<b>[]</b> (Played by: <b>[]</b>)[][]", robo.name, robo.mind.key, Dm.eq(robo.stat, DEAD) ? " " + span_redtext(I18n.T("(Deactivated)")) : "", borg_num != 0 ? ", " : ""));
                }
            }
            if (!borg_spacer)
                borg_spacer = true;
        }

        foreach (mob_living_silicon_robot robo in Dm.each<mob_living_silicon_robot>(GLOB.silicon_mobs))
        {
            if (robo.connected_ai == null && robo.mind != null)
            {
                parts.Add(I18n.T("[]<b>[]</b> (Played by: <b>[]</b>) [] Its laws were:", borg_spacer ? "<br>" : "", robo.name, robo.mind.key,
                    !Dm.eq(robo.stat, DEAD) ? I18n.T("[] as an AI-less borg!", span_greentext(I18n.T("survived"))) : I18n.T("was [] the rigors of being a cyborg without an AI.", span_redtext(I18n.T("unable to survive")))));

                if (robo != null) //How the hell do we lose robo between here and the world messages directly above this?
                    parts.Add(robo.laws.get_law_list(include_zeroth: TRUE));

                if (!borg_spacer)
                    borg_spacer = true;
            }
        }

        if (Dm.truthy(parts.len))
            return Dm.text("<div class='panel stationborder'>[]</div>", parts.Join("<br>"));
        else
            return "";
    }

    //Common part of the report
    // tg: code/__HELPERS/roundend.dm:314
    public virtual object build_roundend_report()
    {
        DmList parts = Dm.list();

        //AI laws
        parts.Add(law_report()); // silicons SI5 (D326)

        CHECK_TICK();

        //Antagonists
        Dm.append(ref parts, antag_report());

        // tg: :325  parts += hardcore_random_report(): the hardcore random score (the statistics database, not ported)

        CHECK_TICK();
        //Medals
        Dm.append(ref parts, medal_report()); // gaps A1
        //Station Goals
        Dm.append(ref parts, goal_report()); // gaps R1
        //Economy & Money
        Dm.append(ref parts, market_report()); // gaps P4d
        // tg: :335  cheevo_report(): the achievements (not ported)

        list_clear_nulls(parts);

        return parts.Join("");
    }

    // tg: code/__HELPERS/roundend.dm:584  (gaps A1: the medals' commendations)
    public virtual string medal_report()
    {
        if (Dm.truthy(GLOB.commendations.len))
        {
            DmList parts = Dm.list();
            Dm.append(ref parts, span_header(I18n.T("Medal Commendations:")));
            foreach (object com in Dm.each(GLOB.commendations))
                Dm.append(ref parts, com);
            return Dm.text("<div class='panel stationborder'>[]</div>", parts.Join("<br>"));
        }
        return "";
    }

    // tg: code/__HELPERS/roundend.dm:497  (gaps R1: the station goals)
    public virtual object goal_report()
    {
        DmList goals = SSstation.get_station_goals() as DmList;
        if (!Dm.truthy(Dm.length(goals)))
            return null;

        DmList parts = Dm.list();
        foreach (datum_station_goal goal in Dm.each_cast<datum_station_goal>(SSstation.get_station_goals()))
            Dm.append(ref parts, goal.get_result());
        return Dm.text("<div class='panel stationborder'><ul>[]</ul></div>", parts.Join(""));
    }

    ///Generate a report for how much money is on station, as well as the richest crewmember on the station.
    // tg: code/__HELPERS/roundend.dm:508  (gaps P4d)
    public virtual string market_report()
    {
        DmList parts = Dm.list();

        ///total service income
        float tourist_income = 0;
        ///This is the richest account on station at roundend.
        datum_bank_account mr_moneybags = null;
        ///This is the station's total wealth at the end of the round.
        float station_vault = 0;
        ///How many players joined the round.
        float total_players = GLOB.joined_player_list?.len ?? 0;
        DmList typecache_bank = TgState.Current.datum_controller_subsystem_ticker__market_report__typecache_bank ??= typecacheof(Dm.list(typeof(datum_bank_account_department), typeof(datum_bank_account_remote))) as DmList;
        foreach (object i in Dm.each(SSeconomy.bank_accounts_by_id))
        {
            var current_acc = (datum_bank_account)SSeconomy.bank_accounts_by_id[i];
            if (Dm.truthy(typecache_bank[current_acc.type]))
                continue;
            station_vault += current_acc.account_balance;
            if (mr_moneybags == null || mr_moneybags.account_balance < current_acc.account_balance)
                mr_moneybags = current_acc;
        }
        Dm.append(ref parts, I18n.T("<div class='panel stationborder'><span class='header'>Station Economic Summary:</span><br>"));
        Dm.append(ref parts, I18n.T("<span class='service'>Service Statistics:</span><br>"));
        // tg: :530-533  for(var/venue_path in SSrestaurant.all_venues): each venue's customers served and income. The restaurant venues
        // (SSrestaurant, the robot customers) are not ported, so service earns no tourist income.
        Dm.append(ref parts, I18n.T("In total, they earned [] [][]<br>", tourist_income, MONEY_NAME, tourist_income != 0 ? "!" : "..."));
        log_econ(Dm.text("Roundend service income: [] [].", tourist_income, MONEY_NAME));

        // tg: :537-543  award_service(the service achievements) on the tourist income: the achievements (not ported); no income here

        if (tourist_income == 0)
            Dm.append(ref parts, Dm.text("[]<br>", span_redtext(I18n.T("Service did not earn any []...", MONEY_NAME))));
        else if (tourist_income <= 2000)
            Dm.append(ref parts, Dm.text("[]<br>", span_redtext(I18n.T("Centcom is displeased. Come on service, surely you can do better than that."))));
        else if (tourist_income <= 4999)
            Dm.append(ref parts, Dm.text("[]<br>", span_greentext(I18n.T("Centcom is satisfied with service's job today."))));
        else
            Dm.append(ref parts, I18n.T("<span class='reallybig greentext'>Centcom is incredibly impressed with service today! What a team!</span><br>"));

        Dm.append(ref parts, I18n.T("<b>General Statistics:</b><br>"));
        Dm.append(ref parts, I18n.T("There were [] [] collected by crew this shift.<br>", station_vault, MONEY_NAME));
        if (total_players > 0)
        {
            Dm.append(ref parts, I18n.T("An average of [] [] were collected.<br>", station_vault / total_players, MONEY_NAME));
            log_econ(Dm.text("Roundend [] total: [] []. Average []: []", MONEY_NAME_SINGULAR, station_vault, MONEY_NAME, MONEY_NAME_CAPITALIZED, station_vault / total_players));
        }
        if (mr_moneybags != null)
            Dm.append(ref parts, I18n.T("The most affluent crew member at shift end was <b>[] with []</b> []!</div>", mr_moneybags.account_holder, mr_moneybags.account_balance, MONEY_SYMBOL));
        else
            Dm.append(ref parts, I18n.T("Somehow, nobody made any money this shift! This'll result in some budget cuts...</div>"));
        SSblackbox.record_feedback("amount", "mail_opened", SSeconomy.mail_opened);
        Dm.append(ref parts, I18n.T("The crew delivered and opened [] pieces of mail[]<br>", SSeconomy.mail_opened, Dm.num(SSeconomy.mail_opened) > 1 ? "!" : "..."));
        return parts.Join("");
    }

    // tg: code/__HELPERS/roundend.dm:341
    public virtual object survivor_report(DmList popcount = null)
    {
        DmList parts = Dm.list();
        bool station_evacuated = datum_controller_subsystem_shuttle.EMERGENCY_ESCAPED_OR_ENDGAMED();

        if (Dm.truthy(GLOB.round_id))
        {
            // tg: :346-347  the CONFIG roundstatsurl page's link (an openLink Topic, which the client does not open): the id alone
            object info = GLOB.round_id;
            Dm.append(ref parts, Dm.text("[]Round ID: <b>[]</b>", FOURSPACES, info));
        }
        Dm.append(ref parts, Dm.text("[]Map: []", FOURSPACES, SSmapping.current_map?.return_map_name()));
        Dm.append(ref parts, Dm.text("[]Shift Duration: <B>[]</B>", FOURSPACES, DisplayTimeText(Dm.num(Dm.world.time) - Dm.num(SSticker.round_start_time))));
        Dm.append(ref parts, Dm.text("[]Station Integrity: <B>[]</B>", FOURSPACES, Dm.truthy(GLOB.station_was_nuked) ? span_redtext("Destroyed") : Dm.text("[]%", popcount["station_integrity"])));
        float total_players = Dm.num(GLOB.joined_player_list?.len ?? 0);
        if (total_players > 0)
        {
            Dm.append(ref parts, Dm.text("[]Total Population: <B>[]</B>", FOURSPACES, total_players));
            if (station_evacuated)
            {
                Dm.append(ref parts, Dm.text("<BR>[]Evacuation Rate: <B>[] ([]%)</B>", FOURSPACES, popcount[POPCOUNT_ESCAPEES], PERCENT(Dm.num(popcount[POPCOUNT_ESCAPEES]) / total_players)));
                Dm.append(ref parts, Dm.text("[](on emergency shuttle): <B>[] ([]%)</B>", FOURSPACES, popcount[POPCOUNT_SHUTTLE_ESCAPEES], PERCENT(Dm.num(popcount[POPCOUNT_SHUTTLE_ESCAPEES]) / total_players)));
            }
            Dm.append(ref parts, Dm.text("[]Survival Rate: <B>[] ([]%)</B>", FOURSPACES, popcount[POPCOUNT_SURVIVORS], PERCENT(Dm.num(popcount[POPCOUNT_SURVIVORS]) / total_players)));
            // tg: :358-365  the first death (SSblackbox.first_death: the statistics, M10+)
        }

        // tg: :367-369 (syndicate A3)
        Dm.append(ref parts, Dm.text("[]Round: []", FOURSPACES, SSdynamic.current_tier?.name)); // a round ended before its setup (a test's) has no tier: tg's would runtime
        foreach (datum_dynamic_ruleset rule in Dm.each_cast<datum_dynamic_ruleset>(Dm.sub(SSdynamic.executed_rulesets, SSdynamic.unreported_rulesets)))
            Dm.append(ref parts, Dm.text("[][]- <b>[]</b> ([])", FOURSPACES, FOURSPACES, rule.name, rule.config_tag));

        return parts.Join("<br>");
    }

    // tg: code/__HELPERS/roundend.dm:614
    public virtual object antag_report()
    {
        DmList result = Dm.list();
        DmList all_teams = Dm.list();
        DmList all_antagonists = Dm.list();

        foreach (object team in Dm.each(GLOB.antagonist_teams))
            Dm.combine(ref all_teams, team);

        foreach (object a in Dm.each(GLOB.antagonists))
        {
            datum_antagonist antagonists = (datum_antagonist)a;
            if (antagonists.owner == null)
                continue;
            Dm.combine(ref all_antagonists, antagonists);
        }

        foreach (object t in Dm.each(all_teams))
        {
            datum_team active_teams = (datum_team)t;
            //check if we should show the team
            if (!Dm.truthy(active_teams.show_roundend_report))
            {
                Dm.remove(ref all_teams, active_teams);
                continue;
            }

            Dm.append(ref result, active_teams.roundend_report());
            Dm.append(ref result, " "); //newline between teams
            CHECK_TICK();
        }

        object currrent_category = null;
        datum_antagonist previous_category = null;

        sortTim(all_antagonists, "/proc/" + nameof(cmp_antag_category));

        foreach (datum_antagonist antagonists in Dm.each<datum_antagonist>(all_antagonists))
        {
            if (!Dm.truthy(antagonists.show_in_roundend))
                continue;
            // if the antag datum is associated with a team that appeared in the report, skip it.
            datum_team antag_team = antagonists.get_team() as datum_team;
            if (antag_team != null && Dm.@in(antag_team, all_teams))
                continue;
            if (!Dm.eq(antagonists.roundend_category, currrent_category))
            {
                if (previous_category != null)
                {
                    Dm.append(ref result, previous_category.roundend_report_footer());
                    Dm.append(ref result, "</div>");
                }
                Dm.append(ref result, "<div class='panel redborder'>");
                Dm.append(ref result, antagonists.roundend_report_header());
                currrent_category = antagonists.roundend_category;
                previous_category = antagonists;
            }
            Dm.append(ref result, antagonists.roundend_report());
            Dm.append(ref result, "<br><br>");
            CHECK_TICK();
        }

        if (Dm.truthy(all_antagonists.len))
        {
            datum_antagonist last = (datum_antagonist)all_antagonists[all_antagonists.len];
            Dm.append(ref result, last.roundend_report_footer());
            Dm.append(ref result, "</div>");
        }

        return result.Join("");
    }

    // tg: code/__HELPERS/roundend.dm:401
    public virtual void show_roundend_report(client C = null, object report_type = null)
    {
        // DEVIATION(DV192): the report in the player's chat as a boxed message. tg opens it in a browser window ("roundend", 800 x 600,
        // with roundend.css), and keeps the player's and the server's last reports in files for the report button's "last round";
        // the port's client has no browser window and the files are the logs (not ported).
        object content = Dm.list(personal_report(C), GLOB.common_report).Join("");
        to_chat(C, Dm.text("<div class='boxed_message'>[]</div>", content));
    }

    // tg: code/__HELPERS/roundend.dm:423
    public virtual object personal_report(client C = null, DmList popcount = null)
    {
        DmList parts = Dm.list();
        mob M = C.mob;
        if (M.mind != null && !isnewplayer(M))
        {
            if (!Dm.eq(M.stat, DEAD) && !isbrain(M))
            {
                if (datum_controller_subsystem_shuttle.EMERGENCY_ESCAPED_OR_ENDGAMED())
                {
                    if (!Dm.truthy(M.onCentCom()) && !Dm.truthy(M.onSyndieBase()))
                    {
                        Dm.append(ref parts, "<div class='panel stationborder'>");
                        Dm.append(ref parts, Dm.text("<span class='marooned'>You managed to survive, but were marooned on []...</span>", station_name()));
                    }
                    else
                    {
                        Dm.append(ref parts, "<div class='panel greenborder'>");
                        Dm.append(ref parts, span_greentext(Dm.text("You managed to survive the events on [] as [].", station_name(), M.real_name)));
                    }
                }
                else
                {
                    Dm.append(ref parts, "<div class='panel greenborder'>");
                    Dm.append(ref parts, span_greentext(Dm.text("You managed to survive the events on [] as [].", station_name(), M.real_name)));
                }
            }
            else
            {
                Dm.append(ref parts, "<div class='panel redborder'>");
                Dm.append(ref parts, span_redtext(Dm.text("You did not survive the events on []...", station_name())));
            }
        }
        else
        {
            Dm.append(ref parts, "<div class='panel stationborder'>");
        }
        Dm.append(ref parts, "<br>");
        Dm.append(ref parts, GLOB.survivor_report);
        Dm.append(ref parts, "</div>");

        return parts.Join("");
    }

    // tg: code/__HELPERS/roundend.dm:450
    public virtual void display_report(DmList popcount = null)
    {
        GLOB.common_report = build_roundend_report();
        GLOB.survivor_report = survivor_report(popcount);
        // tg: :453  log_roundend_report(): the report's files (the logs, not ported)
        foreach (client C in Dm.each<client>(GLOB.clients))
        {
            show_roundend_report(C);
            // tg: :456  give_show_report_button(C): the action button that opens the report again (the browser window, DV192)
            CHECK_TICK();
        }
    }
}

// tg: code/__HELPERS/roundend.dm:668 (syndicate package A1, D316)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/roundend.dm:668
    public static object cmp_antag_category(datum_antagonist A = null, datum_antagonist B = null)
    {
        return sorttext(B.roundend_category, A.roundend_category);
    }

    // tg: code/__HELPERS/roundend.dm:701
    public static object printplayer(datum_mind ply = null, object fleecheck = null)
    {
        string jobtext = "";
        if (!is_unassigned_job(ply.assigned_role))
            jobtext = Dm.text(" the <b>[]</b>", ply.assigned_role.title);
        string text = Dm.text("<b>[]</b> was <b>[]</b>[] and", ply.key, ply.name, jobtext);
        if (ply.current != null)
        {
            if (ply.current.stat == DEAD)
                text += Dm.text(" []", span_redtext("died"));
            else
                text += Dm.text(" []", span_greentext("survived"));
            if (Dm.truthy(fleecheck))
            {
                turf T = get_turf(ply.current);
                if (T == null || !Dm.truthy(is_station_level(T.z)))
                    text += Dm.text(" while []", span_redtext("fleeing the station"));
            }
            if (!Dm.eq(ply.current.real_name, ply.name))
                text += Dm.text(" as <b>[]</b>", ply.current.real_name);
        }
        else
            text += Dm.text(" []", span_redtext("had their body destroyed"));
        return text;
    }

    // tg: code/__HELPERS/roundend.dm:721
    public static object printplayerlist(DmList players = null, object fleecheck = null)
    {
        DmList parts = Dm.list();

        Dm.append(ref parts, "<ul class='playerlist'>");
        foreach (datum_mind M in Dm.each<datum_mind>(players))
            Dm.append(ref parts, Dm.text("<li>[]</li>", printplayer(M, fleecheck)));
        Dm.append(ref parts, "</ul>");
        return parts.Join("");
    }

    // tg: code/__HELPERS/roundend.dm:731
    public static object printobjectives(DmList objectives = null)
    {
        if (objectives == null || !Dm.truthy(objectives.len))
            return null;
        DmList objective_parts = Dm.list();
        float count = 1;
        foreach (datum_objective objective in Dm.each<datum_objective>(objectives))
        {
            Dm.append(ref objective_parts, Dm.text("<b>[] #[]</b>: [] []", objective.objective_name, count, objective.explanation_text, objective.get_roundend_success_suffix()));
            count++;
        }
        return objective_parts.Join("<br>");
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/roundend.dm:519  market_report's var/static/list/typecache_bank (a proc static, rules 10.6)
    internal DmList datum_controller_subsystem_ticker__market_report__typecache_bank;
}
