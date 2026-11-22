// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate package A3, D316): the midround base, the from-ghosts and from-living bases, the midround traitor and mass traitors;
// syndicate N1c: the from-ghosts base's poll of the ghosts (SSpolling, since X2) and the nuclear operatives; N3: the clown operatives;
// gaps K1: the space changelings and the mass space changelings; gaps X5: the xenomorphs' alien infestation. Inert (DV346, never
// picked): spiders, pirates, the wizard, the blob, the blood worms, the nightmare, the space dragon, the abductors, the space
// ninja, the revenant, the paradox clone, the voidwalker, the fugitives, the morph, the slaughter demon, the
// malfunctioning AI, the blob infection and the obsessed.

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1
public partial class datum_dynamic_ruleset_midround : datum_dynamic_ruleset
{
    /**
     * Collect candidates handles getting the broad pool of players we want to pick from
     *
     * You can sleep in this - say, if you wanted to poll players.
     */
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:14
    public virtual async DmTask<object> collect_candidates()
    {
        return Dm.list();
    }

    /**
     * Called when the ruleset is selected for false alarm
     */
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:20
    public virtual async DmTask false_alarm()
    {
        return;
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:224
public partial class datum_dynamic_ruleset_midround_from_ghosts : datum_dynamic_ruleset_midround
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:228
    public override object can_be_selected()
    {
        // SHOULD_CALL_PARENT(TRUE)
        return Dm.num(Dm.truthy(base.can_be_selected()) && Dm.truthy(Dm.bitand(GLOB.ghost_role_flags, GHOSTROLE_MIDROUND_EVENT)));
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:232
    public override object get_candidate_mind(mob candidate = null)
    {
        // Ghost roles will always get a fresh mind
        return Dm.@new<datum_mind>(candidate.key);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:236
    public override async DmTask prepare_for_role(datum_mind candidate = null)
    {
        mob_living body = create_ruleset_body() as mob_living;
        if (body == null)
            return;
        await candidate.transfer_to(body, force_key_move: TRUE); // yoinks the candidate's client
        if (ishuman(body) && Dm.truthy(await apply_prefs_to_body(body as mob_living_carbon_human)))
            on_prefs_applied(body as mob_living_carbon_human);
    }

    /**
     * Handles making the body for the candidate
     *
     * Handling loc is not necessary here - you can do it in assign_role
     *
     * Returning null will skip body creation entirely, though you will be expected to do it yourself in assign_role
     */
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:251
    public virtual object create_ruleset_body()
    {
        return Dm.@new<mob_living_carbon_human>();
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:254
    public override async DmTask<object> collect_candidates()
    {
        object readable_poll_role = Dm.truthy(candidate_role) ? candidate_role : pref_flag;
        if (readable_poll_role == null)
        {
            stack_trace(Dm.text("[]: No candidate role or pref_flag set, give it a human readable candidate roll at the bare minimum.", config_tag), "code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm", 257);
            readable_poll_role = "Some Midround Antagonist Without A Role Set (Yell At Coders)";
        }

        return await SSpolling.poll_candidates(
            group: await trim_candidates(Dm.bitor(GLOB.dead_player_list, GLOB.current_observers_list) as DmList) as DmList,
            question: Dm.text("Looking for volunteers to become [] for []", span_notice(Dm.str(readable_poll_role)), span_danger(Dm.str(name))),
            // check_jobban = list(ROLE_SYNDICATE, jobban_flag || pref_flag), // Not necessary, handled in trim_candidates()
            // role = pref_flag, // Not necessary, handled in trim_candidates()
            poll_time: 30 * SECONDS,
            alert_pic: signup_atom_appearance,
            role_name_text: readable_poll_role);
    }

    /**
     * Handles prepping the body with the candidate's prefs
     *
     * Applies prefs to a given body. Usually that's what you want, but sometimes you don't, in which case you can override this proc.
     * Returns TRUE if prefs were applied
     */
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:276
    public virtual async DmTask<object> apply_prefs_to_body(mob_living_carbon_human body = null)
    {
        body.client?.prefs.safe_transfer_prefs_to(body);
        await body.dna.remove_all_mutations();
        body.dna.update_dna_identity();
        return TRUE;
    }

    /**
     * Handles anything extra you want to happen after applying prefs
     */
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:285
    public virtual void on_prefs_applied(mob_living_carbon_human body = null)
    {
        return;
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:311
public partial class datum_dynamic_ruleset_midround_from_ghosts_nukies : datum_dynamic_ruleset_midround_from_ghosts
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:332
    public override async DmTask<object> create_execute_args()
    {
        return Dm.list(
            Dm.@new<datum_team_nuclear>(),
            get_most_experienced(selected_minds, pref_flag));
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:338
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        var nuke_team = Dm.arg<datum_team>(__extra, 0);
        object most_experienced = Dm.arg(__extra, 1);
        if (most_experienced == candidate)
            await candidate.add_antag_datum(typeof(datum_antagonist_nukeop_leader), nuke_team); // moves to nuke base for us
        else
            await candidate.add_antag_datum(typeof(datum_antagonist_nukeop), nuke_team); // moves to nuke base for us
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:344
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

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1112
public partial class datum_dynamic_ruleset_midround_from_living : datum_dynamic_ruleset_midround
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1116
    public override async DmTask<object> collect_candidates()
    {
        return GLOB.alive_player_list;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1119
    public override async DmTask<object> is_valid_candidate(mob candidate = null, client candidate_client = null)
    {
        if (Dm.eq(candidate.stat, DEAD) || candidate.mind == null)
            return FALSE;
        // only pick members of the crew
        if (!Dm.truthy(job_check(candidate)))
            return FALSE;
        if (!Dm.truthy(antag_check(candidate)))
            return FALSE;
        // checks for stuff like bitrunner avatars and ghost mafia
        if (HAS_TRAIT(candidate, TRAIT_MIND_TEMPORARILY_GONE) || HAS_TRAIT(candidate, TRAIT_TEMPORARY_BODY))
            return FALSE;
        // SEND_SIGNAL(candidate, COMSIG_MOB_MIND_BEFORE_MIDROUND_ROLL, src, pref_flag) & CANCEL_ROLL (rules 11.1 (e), a value)
        if (Dm.truthy(Dm.bitand(!Dm.truthy(candidate._listen_lookup?[COMSIG_MOB_MIND_BEFORE_MIDROUND_ROLL]) ? (object)NONE : candidate._SendSignal(COMSIG_MOB_MIND_BEFORE_MIDROUND_ROLL, Dm.list(candidate, this, pref_flag)), CANCEL_ROLL)))
            return FALSE;
        return await base.is_valid_candidate(candidate, candidate_client);
    }

    /// Checks if the candidate is a valid job for this ruleset - by default you probably only want crew members. (Return FALSE to mark the candidate invalid)
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1135
    public virtual object job_check(mob candidate = null)
    {
        if (!Dm.truthy(Dm.bitand(candidate.mind.assigned_role.job_flags, JOB_CREW_MEMBER)))
            return FALSE;
        if (Dm.@in(candidate.mind.assigned_role.title, get_blacklisted_roles()))
            return FALSE;
        return TRUE;
    }

    /// Checks if the candidate is an antag - most of the time you don't want to double dip. (Return FALSE to mark the candidate invalid)
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1143
    public virtual object antag_check(mob candidate = null)
    {
        return Dm.num(!Dm.truthy(candidate.is_antag()));
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1147
public partial class datum_dynamic_ruleset_midround_from_living_traitor : datum_dynamic_ruleset_midround_from_living
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1161
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        await candidate.add_antag_datum(typeof(datum_antagonist_traitor));
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1164
    public override async DmTask false_alarm()
    {
        priority_announce(
            "Attention crew, it appears that someone on your station has hijacked your telecommunications and broadcasted an unknown signal.",
            Dm.text("[] High-Priority Update", command_name())
        );
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:380
public partial class datum_dynamic_ruleset_midround_from_ghosts_nukies_clown : datum_dynamic_ruleset_midround_from_ghosts_nukies
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:390
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        var nuke_team = Dm.arg<datum_team>(__extra, 0);
        object most_experienced = Dm.arg(__extra, 1);
        if (most_experienced == candidate)
            await candidate.add_antag_datum(typeof(datum_antagonist_nukeop_leader_clownop), nuke_team); // moves to nuke base for us
        else
            await candidate.add_antag_datum(typeof(datum_antagonist_nukeop_clownop), nuke_team); // moves to nuke base for us
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1186 (silicons SI8, D326)
public partial class datum_dynamic_ruleset_midround_from_living_malf_ai : datum_dynamic_ruleset_midround_from_living
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1202
    public override object get_always_blacklisted_roles() => Dm.list();

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1205
    public override object job_check(mob candidate = null) => candidate.mind.assigned_role is datum_job_ai ? TRUE : FALSE;

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1208
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        await candidate.add_antag_datum(typeof(datum_antagonist_malf_ai));
        if (!prob(33) || !isAI(candidate.current))
            return;
        priority_announce("Ion storm detected near the station. Please check all AI-controlled equipment for errors.", "Anomaly Alert", ANNOUNCER_IONSTORM);
        mob_living_silicon new_malf_ai = (mob_living_silicon)candidate.current;
        obj_machinery_ai_law_rack_base rack = new_malf_ai.get_law_rack() as obj_machinery_ai_law_rack_base;
        if (rack != null)
            await rack.scramble_ai_rack(
                base_ion_prob: 100,
                sub_ion_prob: 10,
                ion_limit: 1);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:1222
    public override object can_be_selected() => Dm.truthy(base.can_be_selected()) && !HAS_TRAIT(SSstation, STATION_TRAIT_HUMAN_AI) ? TRUE : FALSE;
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:703 (gaps K1)
public partial class datum_dynamic_ruleset_midround_from_ghosts_space_changeling : datum_dynamic_ruleset_midround_from_ghosts
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:717
    public override object create_ruleset_body()
    {
        return null; // handled by generate_changeling_meteor() entirely
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:720
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        await generate_changeling_meteor(candidate);
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:436  (gaps X5, D333: the alien infestation, larvae from a vent)
public partial class datum_dynamic_ruleset_midround_from_ghosts_xenomorph : datum_dynamic_ruleset_midround_from_ghosts
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:456
    [DmParams("dynamic_config")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        max_antag_cap = Dm.num(max_antag_cap) + Dm.num(prob(50)); // 50% chance to get a second xeno, free!
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:460
    public override object can_be_selected()
    {
        return Dm.num(Dm.truthy(base.can_be_selected()) && Dm.length(find_vent_spawns()) > 0);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:463
    public override async DmTask execute()
    {
        await base.execute();
        _addtimer(Dm.@new<datum_callback>(this, nameof(announce_xenos)), rand(375, 600) * SECONDS, file: "code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm", line: 465);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:467
    public virtual async DmTask announce_xenos()
    {
        priority_announce(I18n.T("Unidentified lifesigns detected coming aboard []. Secure any exterior access, including ducting and ventilation.", station_name()), I18n.T("Lifesign Alert"), ANNOUNCER_ALIENS);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:470
    public override async DmTask false_alarm()
    {
        await announce_xenos();
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:473
    public override object create_ruleset_body()
    {
        return Dm.@new<mob_living_carbon_alien_larva>();
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:476
    public override async DmTask<object> create_execute_args()
    {
        return Dm.list(find_vent_spawns());
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_midround.dm:479
    [DmParams("candidate", "vent_list")]
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        DmList vent_list = Dm.arg<DmList>(__extra, 0);
        // xeno login gives antag datums
        obj vent = (Dm.length(vent_list) >= 2 ? pick_n_take(vent_list) : vent_list[1]) as obj;
        await ((mob_living)candidate.current).move_into_vent(vent as obj_machinery_atmospherics_components);
    }
}
