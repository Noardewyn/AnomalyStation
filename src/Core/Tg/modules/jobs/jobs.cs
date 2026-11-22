// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/jobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only get_full_job_name translated (gaps B1: the voice of god finds the jobs its speaker names); GLOB.exp_specialmap comes
// with the job playtime tracking.

public static partial class GlobalProcs
{
    // tg: code/modules/jobs/jobs.dm:33
    public static object get_full_job_name(object job = null)
    {
        get_full_job_name__cap_expand ??= Dm.@new<regex>("cap(?!tain)");
        get_full_job_name__cmo_expand ??= Dm.@new<regex>("cmo");
        get_full_job_name__hos_expand ??= Dm.@new<regex>("hos");
        get_full_job_name__hop_expand ??= Dm.@new<regex>("hop");
        get_full_job_name__rd_expand ??= Dm.@new<regex>("rd");
        get_full_job_name__ce_expand ??= Dm.@new<regex>("ce");
        get_full_job_name__qm_expand ??= Dm.@new<regex>("qm");
        get_full_job_name__sec_expand ??= Dm.@new<regex>("(?<!security )officer");
        get_full_job_name__engi_expand ??= Dm.@new<regex>("(?<!station )engineer");
        get_full_job_name__atmos_expand ??= Dm.@new<regex>("atmos tech");
        get_full_job_name__doc_expand ??= Dm.@new<regex>("(?<!medical )doctor|medic(?!al)");
        get_full_job_name__mine_expand ??= Dm.@new<regex>("(?<!shaft )miner");
        get_full_job_name__chef_expand ??= Dm.@new<regex>("chef");
        get_full_job_name__borg_expand ??= Dm.@new<regex>("(?<!cy)borg");

        job = LOWER_TEXT(job);
        job = get_full_job_name__cap_expand.Replace(job, "captain");
        job = get_full_job_name__cmo_expand.Replace(job, "chief medical officer");
        job = get_full_job_name__hos_expand.Replace(job, "head of security");
        job = get_full_job_name__hop_expand.Replace(job, "head of personnel");
        job = get_full_job_name__rd_expand.Replace(job, "research director");
        job = get_full_job_name__ce_expand.Replace(job, "chief engineer");
        job = get_full_job_name__qm_expand.Replace(job, "quartermaster");
        job = get_full_job_name__sec_expand.Replace(job, "security officer");
        job = get_full_job_name__engi_expand.Replace(job, "station engineer");
        job = get_full_job_name__atmos_expand.Replace(job, "atmospheric technician");
        job = get_full_job_name__doc_expand.Replace(job, "medical doctor");
        job = get_full_job_name__mine_expand.Replace(job, "shaft miner");
        job = get_full_job_name__chef_expand.Replace(job, "cook");
        job = get_full_job_name__borg_expand.Replace(job, "cyborg");
        return job;
    }

    // tg: code/modules/jobs/jobs.dm:34-47  get_full_job_name's var/static/regex expansions (rules 10.6; built on the first call: a
    // regex has no world id)
    private static ref regex get_full_job_name__cap_expand => ref TgState.Current.GlobalProcs__get_full_job_name__cap_expand;
    private static ref regex get_full_job_name__cmo_expand => ref TgState.Current.GlobalProcs__get_full_job_name__cmo_expand;
    private static ref regex get_full_job_name__hos_expand => ref TgState.Current.GlobalProcs__get_full_job_name__hos_expand;
    private static ref regex get_full_job_name__hop_expand => ref TgState.Current.GlobalProcs__get_full_job_name__hop_expand;
    private static ref regex get_full_job_name__rd_expand => ref TgState.Current.GlobalProcs__get_full_job_name__rd_expand;
    private static ref regex get_full_job_name__ce_expand => ref TgState.Current.GlobalProcs__get_full_job_name__ce_expand;
    private static ref regex get_full_job_name__qm_expand => ref TgState.Current.GlobalProcs__get_full_job_name__qm_expand;
    private static ref regex get_full_job_name__sec_expand => ref TgState.Current.GlobalProcs__get_full_job_name__sec_expand;
    private static ref regex get_full_job_name__engi_expand => ref TgState.Current.GlobalProcs__get_full_job_name__engi_expand;
    private static ref regex get_full_job_name__atmos_expand => ref TgState.Current.GlobalProcs__get_full_job_name__atmos_expand;
    private static ref regex get_full_job_name__doc_expand => ref TgState.Current.GlobalProcs__get_full_job_name__doc_expand;
    private static ref regex get_full_job_name__mine_expand => ref TgState.Current.GlobalProcs__get_full_job_name__mine_expand;
    private static ref regex get_full_job_name__chef_expand => ref TgState.Current.GlobalProcs__get_full_job_name__chef_expand;
    private static ref regex get_full_job_name__borg_expand => ref TgState.Current.GlobalProcs__get_full_job_name__borg_expand;
}

public sealed partial class TgState
{
    // tg: code/modules/jobs/jobs.dm:34-47  get_full_job_name's var/static/regex cap_expand ... borg_expand
    internal regex GlobalProcs__get_full_job_name__cap_expand;
    internal regex GlobalProcs__get_full_job_name__cmo_expand;
    internal regex GlobalProcs__get_full_job_name__hos_expand;
    internal regex GlobalProcs__get_full_job_name__hop_expand;
    internal regex GlobalProcs__get_full_job_name__rd_expand;
    internal regex GlobalProcs__get_full_job_name__ce_expand;
    internal regex GlobalProcs__get_full_job_name__qm_expand;
    internal regex GlobalProcs__get_full_job_name__sec_expand;
    internal regex GlobalProcs__get_full_job_name__engi_expand;
    internal regex GlobalProcs__get_full_job_name__atmos_expand;
    internal regex GlobalProcs__get_full_job_name__doc_expand;
    internal regex GlobalProcs__get_full_job_name__mine_expand;
    internal regex GlobalProcs__get_full_job_name__chef_expand;
    internal regex GlobalProcs__get_full_job_name__borg_expand;
}
