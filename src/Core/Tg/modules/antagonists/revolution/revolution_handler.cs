// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/revolution/revolution_handler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps V1 (D327): the whole file: the revolution's handler (the heads of staff as the team's mutiny targets, the shuttle held while it
// lasts, the promotion of new heads every 5 minutes, the revolution's victory and the station's, which deconverts every revolutionary
// and blacklists the head revolutionaries from revival) and can_be_headrev. The handler's var block is hand-written: its result holds a
// number (REVOLUTION_VICTORY, STATION_VICTORY), which the types tool would type as a Type path (rules 3.5).

public partial class datum_controller_global_vars
{
    // tg: code/modules/antagonists/revolution/revolution_handler.dm:1  GLOBAL_DATUM(revolution_handler, /datum/revolution_handler)
    public datum_revolution_handler revolution_handler;

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:1  (GLOBAL_UNMANAGED(revolution_handler), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalrevolution_handler()
    {
        return;
    }
}

// tg: code/modules/antagonists/revolution/revolution_handler.dm:3
public partial class datum_revolution_handler : datum
{
    /// The revolution team
    // tg: code/modules/antagonists/revolution/revolution_handler.dm:5
    public datum_team_revolution revs;

    /// The objective of the heads of staff, aka to kill the headrevs.
    // tg: code/modules/antagonists/revolution/revolution_handler.dm:8
    public DmList heads_objective;

    /// Cooldown between head revs being promoted
    // tg: code/modules/antagonists/revolution/revolution_handler.dm:11  COOLDOWN_DECLARE(rev_head_promote_cd)
    public float rev_head_promote_cd = 0;

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:13
    public object result;

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:3  (a hand-written var block, types-converter.md 3.4)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        heads_objective = Dm.list();
    }

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:15
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        revs = Dm.@new<datum_team_revolution>();
    }

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:18
    public virtual async DmTask start_revolution()
    {
        if (Dm.truthy(Dm.bitand(datum_flags, DF_ISPROCESSING)) || Dm.truthy(result))
            return;
        START_PROCESSING(SSprocessing, this);
        SSshuttle.registerHostileEnvironment(this);

        foreach (datum_mind mutiny_target in Dm.each_cast<datum_mind>(SSjob.get_all_heads()))
        {
            var new_target = Dm.@new<datum_objective_mutiny>();
            new_target.team = revs;
            new_target.target = mutiny_target;
            new_target.update_explanation_text();
            Dm.append(ref revs.objectives, new_target);
        }

        RegisterSignal(SSdcs, COMSIG_GLOB_JOB_AFTER_LATEJOIN_SPAWN, nameof(update_objectives));
        rev_head_promote_cd = world.time + 5 * MINUTES; // COOLDOWN_START(src, rev_head_promote_cd, 5 MINUTES)
        await DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:34
    public virtual async DmTask cleanup()
    {
        STOP_PROCESSING(SSprocessing, this);
        SSshuttle.clearHostileEnvironment(this);
        UnregisterSignal(SSdcs, COMSIG_GLOB_JOB_AFTER_LATEJOIN_SPAWN);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:39
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = null;
        if (Dm.truthy(check_rev_victory()))
        {
            Waiting.NoSleep(declare_revs_win());
            dot = PROCESS_KILL;
        }
        else if (Dm.truthy(check_heads_victory()))
        {
            Waiting.NoSleep(declare_heads_win());
            dot = PROCESS_KILL;
        }

        if (Dm.eq(dot, PROCESS_KILL))
        {
            Waiting.NoSleep(cleanup());
            return dot;
        }

        if (rev_head_promote_cd <= world.time) // COOLDOWN_FINISHED(src, rev_head_promote_cd)
        {
            Waiting.NoSleep(revs.update_rev_heads());
            rev_head_promote_cd = world.time + 5 * MINUTES; // COOLDOWN_START(src, rev_head_promote_cd, 5 MINUTES)
        }

        return dot;
    }

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:58
    [SignalHandler]
    public virtual void update_objectives(datum source = null, datum_job job = null, mob_living spawned = null)
    {
        if (!Dm.truthy(Dm.bitand(job.job_flags, JOB_HEAD_OF_STAFF)))
            return;

        var new_target = Dm.@new<datum_objective_mutiny>();
        new_target.team = revs;
        new_target.target = spawned.mind;
        new_target.update_explanation_text();
        Dm.append(ref revs.objectives, new_target);
    }

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:70
    public virtual async DmTask declare_revs_win()
    {
        foreach (datum_mind headrev_mind in Dm.each_cast<datum_mind>(revs.ex_headrevs))
        {
            var real_headrev = headrev_mind.current as mob_living;
            if (real_headrev == null)
                continue;
            add_memory_in_range(real_headrev, 5, typeof(datum_memory_revolution_rev_victory), Dm.named("protagonist", real_headrev));
            await add_personality_mood_to_viewers(real_headrev, "revwin", Dm.list(
                Dm.kv(typeof(datum_personality_nt_loyalist), typeof(datum_mood_event_loyalist_revs_win)),
                Dm.kv(typeof(datum_personality_nt_disillusioned), typeof(datum_mood_event_disillusioned_revs_win))
            ), range: 5);
        }

        result = REVOLUTION_VICTORY;
    }

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:83
    public virtual async DmTask declare_heads_win()
    {
        // Save rev lists before we remove the antag datums.
        revs.save_members();

        // Remove everyone as a revolutionary
        foreach (datum_mind rev_mind in Dm.each_cast<datum_mind>(revs.members))
        {
            var rev_antag = rev_mind.has_antag_datum(typeof(datum_antagonist_rev)) as datum_antagonist_rev;
            if (rev_antag != null)
            {
                await rev_antag.remove_revolutionary(DECONVERTER_STATION_WIN);
                if (Dm.@in(rev_mind, revs.ex_headrevs))
                    LAZYADD(ref rev_mind.special_roles, "Former Head Revolutionary");
                else
                    LAZYADD(ref rev_mind.special_roles, "Former Revolutionary");
            }
        }

        // If the revolution was quelled, make rev heads unable to be revived through pods
        foreach (datum_mind rev_head in Dm.each_cast<datum_mind>(revs.ex_headrevs))
        {
            if (rev_head.current != null)
                ADD_TRAIT(rev_head.current, TRAIT_DEFIB_BLACKLISTED, REF(this));
        }

        foreach (datum_objective_mutiny head_tracker in Dm.each<datum_objective_mutiny>(revs.objectives))
        {
            var head_of_staff = head_tracker.target?.current as mob_living;
            if (head_of_staff != null)
            {
                add_memory_in_range(head_of_staff, 5, typeof(datum_memory_revolution_heads_victory), Dm.named("protagonist", head_of_staff));
                await add_personality_mood_to_viewers(head_of_staff, "revlost", Dm.list(
                    Dm.kv(typeof(datum_personality_nt_loyalist), typeof(datum_mood_event_loyalist_revs_lost)),
                    Dm.kv(typeof(datum_personality_nt_disillusioned), typeof(datum_mood_event_disillusioned_revs_lost))
                ), range: 5);
            }
        }

        priority_announce(I18n.T("It appears the mutiny has been quelled. Please return yourself and your incapacitated colleagues to work. We have remotely blacklisted the head revolutionaries in your medical records to prevent accidental revival."), null, null, null, Dm.text("[] Loyalty Monitoring Division", command_name()));

        result = STATION_VICTORY;
    }

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:116
    public virtual object check_rev_victory()
    {
        foreach (datum_objective_mutiny objective in Dm.each<datum_objective_mutiny>(revs.objectives))
        {
            if (!Dm.truthy(objective.check_completion()))
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/antagonists/revolution/revolution_handler.dm:122
    public virtual object check_heads_victory()
    {
        // List of headrevs we're currently tracking
        DmList included_headrevs = Dm.list();
        // List of current headrevs
        var current_headrevs = revs.get_head_revolutionaries() as DmList;
        // A copy of the head of staff objective list, since we're going to be modifying the original list.
        DmList heads_objective_copy = heads_objective.Copy();

        bool objective_complete = true;
        // Here, we check current head of staff objectives and remove them if the target doesn't exist as a headrev anymore
        foreach (datum_objective_mutiny objective in Dm.each<datum_objective_mutiny>(heads_objective_copy))
        {
            if (!Dm.@in(objective.target, current_headrevs))
            {
                Dm.remove(ref heads_objective, objective);
                continue;
            }
            if (!Dm.truthy(objective.check_completion()))
                objective_complete = false;
            Dm.append(ref included_headrevs, objective.target);
        }

        // Here, we check current headrevs and add them as objectives if they didn't exist as a head of staff objective before.
        // Additionally, we make sure the objective is not completed by running the check_completion check on them.
        foreach (datum_mind rev_mind in Dm.each_cast<datum_mind>(current_headrevs))
        {
            if (!Dm.@in(rev_mind, included_headrevs))
            {
                var objective = Dm.@new<datum_objective_mutiny>();
                objective.target = rev_mind;
                if (!Dm.truthy(objective.check_completion()))
                    objective_complete = false;
                Dm.append(ref heads_objective, objective);
            }
        }

        return objective_complete ? TRUE : FALSE;
    }
}

public static partial class GlobalProcs
{
    /// Checks if someone is valid to be a headrev
    // tg: code/modules/antagonists/revolution/revolution_handler.dm:153
    public static object can_be_headrev(datum_mind candidate = null, float? roundstart = null)
    {
        roundstart ??= FALSE;
        turf head_turf = get_turf(candidate.current);
        if (Dm.truthy(considered_afk(candidate)))
            return FALSE;
        if (!Dm.truthy(considered_alive(candidate)))
            return FALSE;
        if (!Dm.truthy(is_station_level(head_turf.z)))
            return FALSE;
        if (Dm.truthy(candidate.current.is_antag()))
            return FALSE;
        client candidate_client = GET_CLIENT(candidate.current) as client;
        if (!Dm.@in(ROLE_REV_HEAD, candidate_client.prefs.be_special) && (Dm.truthy(roundstart) || !Dm.@in(ROLE_PROVOCATEUR, candidate_client.prefs.be_special)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(candidate.assigned_role.job_flags, JOB_HEAD_OF_STAFF)))
            return FALSE;
        if (HAS_MIND_TRAIT(candidate.current, TRAIT_UNCONVERTABLE))
            return FALSE;
        return TRUE;
    }
}
