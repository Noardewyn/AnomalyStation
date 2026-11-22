// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate package A3, D316): the latejoin base and the latejoin traitor; gaps V1: the revolution; gaps K1: the changeling.

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:1
public partial class datum_dynamic_ruleset_latejoin : datum_dynamic_ruleset
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:6
    public override object set_config_value(object new_var = null, object new_val = null)
    {
        if (Dm.eq(new_var, nameof(min_antag_cap)) || Dm.eq(new_var, nameof(max_antag_cap)))
            return FALSE;
        return base.set_config_value(new_var, new_val);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:16
    public override async DmTask<object> is_valid_candidate(mob candidate = null, client candidate_client = null)
    {
        if (candidate.mind == null)
            return FALSE;
        if (Dm.@in(candidate.mind.assigned_role.title, get_blacklisted_roles()))
            return FALSE;
        return await base.is_valid_candidate(candidate, candidate_client);
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:24
public partial class datum_dynamic_ruleset_latejoin_traitor : datum_dynamic_ruleset_latejoin
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:35
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        await candidate.add_antag_datum(typeof(datum_antagonist_traitor));
    }
}

// Gaps V1 (D327): the latejoin revolution (a provocateur revealed a minute after joining, if three heads of staff are aboard).
// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:53
public partial class datum_dynamic_ruleset_latejoin_revolution : datum_dynamic_ruleset_latejoin
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:66
    public override object can_be_selected()
    {
        if (GLOB.revolution_handler != null)
            return FALSE;
        float head_check = 0;
        foreach (mob player in Dm.each_cast<mob>(get_active_player_list(alive_check: TRUE, afk_check: TRUE)))
        {
            if (Dm.truthy(Dm.bitand(player.mind.assigned_role.job_flags, JOB_HEAD_OF_STAFF)))
                head_check++;
        }
        return head_check >= heads_necessary ? TRUE : FALSE;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:75
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

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:81
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        LAZYADD(ref candidate.special_roles, "Dormant Head Revolutionary");
        _addtimer(Dm.@new<datum_callback>(this, nameof(reveal_head), candidate), 1 * MINUTES, TIMER_DELETE_ME, file: "code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm", line: 83);
        await DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:85
    public virtual async DmTask reveal_head(datum_mind candidate = null)
    {
        LAZYREMOVE(ref candidate.special_roles, "Dormant Head Revolutionary");

        float head_check = 0;
        foreach (mob player in Dm.each_cast<mob>(get_active_player_list(alive_check: TRUE, afk_check: TRUE)))
        {
            if (Dm.truthy(Dm.bitand(player.mind?.assigned_role.job_flags ?? 0, JOB_HEAD_OF_STAFF)))
                head_check++;
        }

        if (head_check < heads_necessary - 1) // little bit of leeway
        {
            Dm.append(ref SSdynamic.unreported_rulesets, this);
            name = Dm.str(Dm.add(name, " (Canceled)"));
            log_dynamic(Dm.text("[]: Not enough heads of staff were present to start a revolution.", config_tag));
            return;
        }

        if (!Dm.truthy(can_be_headrev(candidate)))
        {
            Dm.append(ref SSdynamic.unreported_rulesets, this);
            name = Dm.str(Dm.add(name, " (Canceled)"));
            log_dynamic(Dm.text("[]: [] was ineligible after the timer expired. Ruleset canceled.", config_tag, key_name(candidate)));
            message_admins(Dm.text("[]: [] was ineligible after the timer expired. Ruleset canceled.", config_tag, key_name(candidate)));
            return;
        }

        GLOB.revolution_handler ??= Dm.@new<datum_revolution_handler>();
        var new_head = Dm.@new<datum_antagonist_rev_head>();
        new_head.give_flash = TRUE;
        new_head.give_hud = TRUE;
        new_head.remove_clumsy = TRUE;
        await candidate.add_antag_datum(new_head, GLOB.revolution_handler.revs);
        await GLOB.revolution_handler.start_revolution();
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:38 (gaps K1)
public partial class datum_dynamic_ruleset_latejoin_changeling : datum_dynamic_ruleset_latejoin
{
    // tg: code/controllers/subsystem/dynamic/dynamic_ruleset_latejoin.dm:50
    public override async DmTask assign_role(datum_mind candidate = null, params object[] __extra)
    {
        await candidate.add_antag_datum(typeof(datum_antagonist_changeling));
    }
}
