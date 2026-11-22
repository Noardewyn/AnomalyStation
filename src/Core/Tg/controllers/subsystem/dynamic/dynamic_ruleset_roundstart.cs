// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate package A3, D316): the roundstart base (is_valid_candidate, ruleset_forced_job_check: its job_typepath is a Type,
// Appendix A), the traitors, extended, (syndicate X4) the blood brothers, (N1) the nuclear operatives and (N3) the clown operatives,
// (silicons SI8) the malfunctioning AI, (gaps V1) the revolution, (gaps K1) the changelings. Inert (DV346, never picked): the blood worms, the
// heretics, the wizard, the blood cult, the spies, meteor (the meteors' spawning and the meteor mode controller are not ported) and nations.

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:1
public partial class datum_dynamic_ruleset_roundstart : datum_dynamic_ruleset
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:8
    public override async DmTask<object> is_valid_candidate(mob candidate = null, client candidate_client = null)
    {
        if (candidate.mind == null)
            return FALSE;
        // Checks that any other roundstart ruleset hasn't already picked this guy
        foreach (datum_dynamic_ruleset_roundstart ruleset in Dm.each_cast<datum_dynamic_ruleset_roundstart>(SSdynamic.queued_rulesets))
        {
            if (Dm.@in(candidate.mind, ruleset.selected_minds))
                return FALSE;
        }
        return await base.is_valid_candidate(candidate, candidate_client);
    }

    /// Helpful proc - to use if your ruleset forces a job - which ensures a candidate can play the passed job typepath
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:18
    public virtual async DmTask<object> ruleset_forced_job_check(mob candidate = null, client candidate_client = null, Type job_typepath = null)
    {
        object title = Dm.initial(job_typepath, "title"); // job_typepath::title
        // Malf AI can only go to people who want to be AI
        if (!Dm.truthy(candidate_client.prefs.job_preferences[title]))
            return FALSE;
        // And only to people who can actually be AI this round
        if (!Dm.eq(SSjob.check_job_eligibility(candidate as mob_dead_new_player, SSjob.get_job_type(job_typepath) as datum_job, Dm.text("[] Candidacy", name)), JOB_AVAILABLE))
            return FALSE;
        // (Something else forced us to play a job that isn't AI)
        object forced_job = LAZYACCESS(SSjob.forced_occupations, candidate);
        if (Dm.truthy(forced_job) && !Dm.eq(forced_job, job_typepath))
            return FALSE;
        // (Something else forced us NOT to play AI)
        if (Dm.@in(title, LAZYACCESS(SSjob.prevented_occupations, candidate)))
            return FALSE;
        return TRUE;
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:35
public partial class datum_dynamic_ruleset_roundstart_traitor : datum_dynamic_ruleset_roundstart
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:43
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        await candidate.add_antag_datum(typeof(datum_antagonist_traitor));
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:77
public partial class datum_dynamic_ruleset_roundstart_blood_brother : datum_dynamic_ruleset_roundstart
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:86
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        await candidate.add_antag_datum(typeof(datum_antagonist_brother));
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:89 (gaps K1)
public partial class datum_dynamic_ruleset_roundstart_changeling : datum_dynamic_ruleset_roundstart
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:98
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        await candidate.add_antag_datum(typeof(datum_antagonist_changeling));
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:239
public partial class datum_dynamic_ruleset_roundstart_nukies : datum_dynamic_ruleset_roundstart
{
    private const int ELEVATOR_WIDTH = 6;
    private const int ELEVATOR_HEIGHT = 5;

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:256
    public override DmTask prepare_for_role(datum_mind candidate = null)
    {
        // they all get the normal operative job, even the leader. (leader's job is updated when they get the antag datum)
        // all this ultimately matters for is 1. ensuring they *don't* get a normal job and 2. spawning them in the elevator
        LAZYSET(ref SSjob.forced_occupations, candidate, typeof(datum_job_nuclear_operative));
        return DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:261
    public override async DmTask execute()
    {
        await base.execute();
        _addtimer(Dm.@new<datum_callback>(this, nameof(load_nukie_base)), 3 * SECONDS,
            file: "code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm", line: 263);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:268
    public virtual async DmTask load_nukie_base()
    {
        await SSmapping.lazy_load_template(LAZY_TEMPLATE_KEY_NUKIEBASE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(teleport_elevator_contents)), 2 * SECONDS,
            file: "code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm", line: 270);
        _addtimer(Dm.@new<datum_callback>(this, nameof(open_nukie_elevator)), 3 * SECONDS,
            file: "code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm", line: 271);
        _addtimer(Dm.@new<datum_callback>(this, nameof(call_infiltrator)), 5 * SECONDS,
            file: "code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm", line: 272);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:274
    public virtual async DmTask teleport_elevator_contents()
    {
        var interior = Dm.locate<obj_effect_landmark_nukeop_elevator_interior>(GLOB.landmarks_list);
        var exterior = Dm.locate<obj_effect_landmark_nukeop_elevator_exterior>(GLOB.landmarks_list);
        if (interior == null || exterior == null)
        {
            stack_trace("Failed to find nukie elevator landmarks during load!", file: "code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm", line: 278);
            message_admins("Failed to find nukie elevator landmarks during load, you might have some stuck nukies that need help.");
            return;
        }

        var top_right_interior = get_turf(interior) as turf;
        var top_right_exterior = get_turf(exterior) as turf;

        DmList elevator_turfs = block(
            top_right_interior.x,
            top_right_interior.y,
            top_right_interior.z,
            top_right_interior.x - ELEVATOR_WIDTH + 1,
            top_right_interior.y - ELEVATOR_HEIGHT + 1,
            top_right_interior.z);

        foreach (turf elevator_turf in Dm.each_cast<turf>(elevator_turfs))
        {
            var destination_turf = locate(
                top_right_exterior.x - (top_right_interior.x - elevator_turf.x),
                top_right_exterior.y - (top_right_interior.y - elevator_turf.y),
                top_right_exterior.z) as turf;
            if (destination_turf == null)
            {
                stack_trace("Failed to find destination turf for nukie elevator!", file: "code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm", line: 302);
                message_admins("Failed to find corresponding elevator turf for nuke ops, you might have some stuck nukies that need help.");
                continue;
            }

            foreach (atom_movable thing in Dm.each<atom_movable>(elevator_turf.contents.Copy()))
            {
                if (Dm.truthy(thing.anchored) || thing.orbit_target != null)
                    continue;
                await thing.forceMove(destination_turf);
            }
        }
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:311
    public virtual async DmTask open_nukie_elevator()
    {
        foreach (obj_machinery_door_poddoor shutter in Dm.each<obj_machinery_door_poddoor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_poddoor))))
        {
            if (Dm.eq(shutter.id_tag, "nukiespawnlift"))
                await shutter.open();
        }
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:316
    public virtual async DmTask call_infiltrator()
    {
        foreach (datum_mind leader_mind in Dm.each_cast<datum_mind>(selected_minds))
        {
            var nuke_leader = leader_mind.has_antag_datum(typeof(datum_antagonist_nukeop_leader)) as datum_antagonist_nukeop_leader;
            if (nuke_leader != null)
                await nuke_leader.spawn_infiltrator();
        }
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:324
    public override async DmTask<object> create_execute_args()
    {
        return Dm.list(
            Dm.@new<datum_team_nuclear>(),
            get_most_experienced(selected_minds, pref_flag));
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:330
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        var nuke_team = Dm.arg<datum_team>(__extra, 0);
        object most_experienced = Dm.arg(__extra, 1);
        if (most_experienced == candidate)
        {
            var nuke_leader = Dm.@new<datum_antagonist_nukeop_leader>();
            nuke_leader.send_to_spawnpoint = FALSE; // because they get spawned in the elevator
            nuke_leader.spawn_ship = FALSE; // spawned manually later
            await candidate.add_antag_datum(nuke_leader, nuke_team);
        }
        else
        {
            var nukie = Dm.@new<datum_antagonist_nukeop>();
            nukie.send_to_spawnpoint = FALSE; // ditto
            await candidate.add_antag_datum(nukie, nuke_team);
        }
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:341
    public override object round_result()
    {
        var nukie = (selected_minds[1] as datum_mind).has_antag_datum(typeof(datum_antagonist_nukeop)) as datum_antagonist_nukeop;
        var nuke_team = nukie.get_team() as datum_team_nuclear;
        object result = nuke_team.get_result();
        switch (Dm.num(result))
        {
            case NUKE_RESULT_FLUKE:
                SSticker.mode_result = "loss - syndicate nuked - disk secured";
                SSticker.news_report = NUKE_SYNDICATE_BASE;
                break;
            case NUKE_RESULT_NUKE_WIN:
                SSticker.mode_result = "win - syndicate nuke";
                SSticker.news_report = STATION_DESTROYED_NUKE;
                break;
            case NUKE_RESULT_NOSURVIVORS:
                SSticker.mode_result = "halfwin - syndicate nuke - did not evacuate in time";
                SSticker.news_report = STATION_DESTROYED_NUKE;
                break;
            case NUKE_RESULT_WRONG_STATION:
                SSticker.mode_result = "halfwin - blew wrong station";
                SSticker.news_report = NUKE_MISS;
                break;
            case NUKE_RESULT_WRONG_STATION_DEAD:
                SSticker.mode_result = "halfwin - blew wrong station - did not evacuate in time";
                SSticker.news_report = NUKE_MISS;
                break;
            case NUKE_RESULT_CREW_WIN_SYNDIES_DEAD:
                SSticker.mode_result = "loss - evacuation - disk secured - syndi team dead";
                SSticker.news_report = OPERATIVES_KILLED;
                break;
            case NUKE_RESULT_CREW_WIN:
                SSticker.mode_result = "loss - evacuation - disk secured";
                SSticker.news_report = OPERATIVES_KILLED;
                break;
            case NUKE_RESULT_DISK_LOST:
                SSticker.mode_result = "halfwin - evacuation - disk not secured";
                SSticker.news_report = OPERATIVE_SKIRMISH;
                break;
            case NUKE_RESULT_DISK_STOLEN:
                SSticker.mode_result = "halfwin - detonation averted";
                SSticker.news_report = OPERATIVE_SKIRMISH;
                break;
            default:
                SSticker.mode_result = "halfwin - interrupted";
                SSticker.news_report = OPERATIVE_SKIRMISH;
                break;
        }
        return null;
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:487
public partial class datum_dynamic_ruleset_roundstart_extended : datum_dynamic_ruleset_roundstart
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:494
    public override async DmTask execute()
    {
        // No midrounds no latejoins
        foreach (object category in Dm.each(SSdynamic.rulesets_to_spawn))
            SSdynamic.rulesets_to_spawn[category] = 0;
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:376
public partial class datum_dynamic_ruleset_roundstart_nukies_clown : datum_dynamic_ruleset_roundstart_nukies
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:383
    public override DmTask prepare_for_role(datum_mind candidate = null)
    {
        LAZYSET(ref SSjob.forced_occupations, candidate, typeof(datum_job_nuclear_operative_clown_operative));
        return DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:386
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        object most_experienced = Dm.arg(__extra, 1);
        if (most_experienced == candidate)
            await candidate.add_antag_datum(typeof(datum_antagonist_nukeop_leader_clownop));
        else
            await candidate.add_antag_datum(typeof(datum_antagonist_nukeop_clownop));
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:46 (silicons SI8, D326)
public partial class datum_dynamic_ruleset_roundstart_malf_ai : datum_dynamic_ruleset_roundstart
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:62
    public override object get_always_blacklisted_roles() => Dm.list();

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:65
    public override async DmTask<object> is_valid_candidate(mob candidate = null, client candidate_client = null)
        => Dm.truthy(await base.is_valid_candidate(candidate, candidate_client)) && Dm.truthy(await ruleset_forced_job_check(candidate, candidate_client, typeof(datum_job_ai))) ? TRUE : FALSE;

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:68
    public override DmTask prepare_for_role(datum_mind candidate = null)
    {
        LAZYSET(ref SSjob.forced_occupations, candidate, typeof(datum_job_ai));
        return DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:71
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        await candidate.add_antag_datum(typeof(datum_antagonist_malf_ai));
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:74
    public override object can_be_selected() => Dm.truthy(base.can_be_selected()) && !HAS_TRAIT(SSstation, STATION_TRAIT_HUMAN_AI) ? TRUE : FALSE;
}

// Gaps V1 (D327): the roundstart revolution (dormant head revolutionaries revealed 7 minutes in, if enough heads of staff came).
// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:392
public partial class datum_dynamic_ruleset_roundstart_revolution : datum_dynamic_ruleset_roundstart
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:411
    public override object get_always_blacklisted_roles()
    {
        DmList dot = base.get_always_blacklisted_roles() as DmList;
        foreach (datum_job job in Dm.each_cast<datum_job>(SSjob.all_occupations))
        {
            if (Dm.truthy(Dm.bitand(job.job_flags, JOB_HEAD_OF_STAFF)))
                Dm.combine(ref dot, job.title);
        }
        return dot;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:417
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        LAZYADD(ref candidate.special_roles, "Dormant Head Revolutionary");
        _addtimer(Dm.@new<datum_callback>(this, nameof(reveal_head), candidate), 7 * MINUTES, TIMER_DELETE_ME, file: "code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm", line: 419);
        await DmTask.CompletedTask;
    }

    /// Reveals the headrev after a set amount of time
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:422
    public virtual async DmTask reveal_head(datum_mind candidate = null)
    {
        LAZYREMOVE(ref candidate.special_roles, "Dormant Head Revolutionary");

        float head_check = 0;
        foreach (mob player in Dm.each_cast<mob>(get_active_player_list(alive_check: TRUE, afk_check: TRUE)))
        {
            if (Dm.truthy(Dm.bitand(player.mind?.assigned_role.job_flags ?? 0, JOB_HEAD_OF_STAFF)))
                head_check++;
        }

        if (head_check < heads_necessary)
        {
            log_dynamic(Dm.text("[]: Not enough heads of staff were present to start a revolution.", config_tag));
            _addtimer(Dm.@new<datum_callback>(this, nameof(revs_execution_failed)), 1 * MINUTES, Dm.bitor(TIMER_UNIQUE, TIMER_DELETE_ME), file: "code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm", line: 432);
            return;
        }

        if (!Dm.truthy(can_be_headrev(candidate, TRUE)))
        {
            log_dynamic(Dm.text("[]: [] was not eligible to be a headrev after the timer expired - finding a replacement.", config_tag, key_name(candidate)));
            await find_another_headrev();
            return;
        }

        GLOB.revolution_handler ??= Dm.@new<datum_revolution_handler>();
        var new_head = Dm.@new<datum_antagonist_rev_head>();
        new_head.give_flash = TRUE;
        new_head.give_hud = TRUE;
        new_head.remove_clumsy = TRUE;
        new_head.roundstart = TRUE;
        await candidate.add_antag_datum(new_head, GLOB.revolution_handler.revs);
        await GLOB.revolution_handler.start_revolution();
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:449
    public virtual async DmTask find_another_headrev()
    {
        foreach (mob_living_carbon_human upstanding_citizen in Dm.each<mob_living_carbon_human>(GLOB.player_list))
        {
            if (!Dm.truthy(can_be_headrev(upstanding_citizen.mind, TRUE)))
                continue;
            await reveal_head(upstanding_citizen.mind);
            log_dynamic(Dm.text("[]: [] was selected as a replacement headrev.", config_tag, key_name(upstanding_citizen)));
            return;
        }

        log_dynamic(Dm.text("[]: Failed to find a replacement headrev.", config_tag));
        _addtimer(Dm.@new<datum_callback>(this, nameof(revs_execution_failed)), 1 * MINUTES, Dm.bitor(TIMER_UNIQUE, TIMER_DELETE_ME), file: "code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm", line: 458);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_roundstart.dm:460
    public virtual void revs_execution_failed()
    {
        if (GLOB.revolution_handler != null)
            return;
        // Execution is effectively cancelled by this point, but it's not like we can go back and refund it
        Dm.append(ref SSdynamic.unreported_rulesets, this);
        name = Dm.str(Dm.add(name, " (Canceled)"));
        log_dynamic(Dm.text("[]: All headrevs were ineligible after the timer expired, and no replacements could be found. Ruleset canceled.", config_tag));
        message_admins(Dm.text("[]: All headrevs were ineligible after the timer expired, and no replacements could be found. Ruleset canceled.", config_tag));
    }
}
