// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/networks/bitrunning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the apology letter (gaps S3, D298) and SSbitrunning with its domain list (gaps BR1, D339, D341). The var blocks are
// generated (D111).

// tg: code/controllers/subsystem/networks/bitrunning.dm:4 SUBSYSTEM_DEF(bitrunning)
public partial class datum_controller_subsystem_bitrunning : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/networks/bitrunning.dm:1  (#undef at :65)
    private const string REDACTED = "???";

    // The New() that SUBSYSTEM_DEF(bitrunning) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSbitrunning) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/networks/bitrunning.dm:3
    public override async DmTask New(params object[] __extra)
    {
        if (SSbitrunning != this)
        {
            if (SSbitrunning is datum_controller_subsystem_bitrunning)
            {
                Recover();
                qdel(SSbitrunning);
            }
            SSbitrunning = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/networks/bitrunning.dm:9
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        InitializeDomains();
        await DmTask.CompletedTask;
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/networks/bitrunning.dm:13
    public virtual void InitializeDomains()
    {
        foreach (object path in Dm.each(subtypesof(typeof(datum_lazy_template_virtual_domain))))
            Dm.append(ref all_domains, Dm.@new((Type)path));
    }

    /// Compiles a list of available domains.
    // tg: code/controllers/subsystem/networks/bitrunning.dm:18
    public virtual object get_available_domains(object scanner_tier = null, object points = null)
    {
        DmList levels = Dm.list();

        foreach (datum_lazy_template_virtual_domain domain in Dm.each_cast<datum_lazy_template_virtual_domain>(all_domains))
        {
            if (Dm.truthy(Dm.bitand(domain.domain_flags, DOMAIN_TEST_ONLY)))
                continue;
            object can_view = domain.can_view_name(scanner_tier, points);
            object can_view_reward = domain.can_view_reward(scanner_tier, points);

            object grade_symbol = GLOB.bitrunning_grades[domain.best_grade];

            // UNTYPED_LIST_ADD(levels, list(...)) (__DEFINES/_helpers.dm), expanded inline
            levels.Add(Dm.list(Dm.list(
                Dm.kv("announce_ghosts", domain.announce_to_ghosts),
                Dm.kv("cost", domain.cost),
                Dm.kv("desc", Dm.truthy(can_view) ? domain.desc : I18n.T("Limited scanning capabilities. Cannot infer domain details.")),
                Dm.kv("difficulty", domain.difficulty),
                Dm.kv("id", domain.key),
                Dm.kv("is_modular", domain.is_modular),
                Dm.kv("has_secondary_objectives", Dm.truthy(counterlist_sum(domain.secondary_loot)) ? TRUE : FALSE),
                Dm.kv("name", Dm.truthy(can_view) ? domain.name : REDACTED),
                Dm.kv("reward", Dm.truthy(can_view_reward) ? domain.reward_points : REDACTED),
                Dm.kv("grade_symbol", grade_symbol))));
        }

        return levels;
    }

    // tg: code/controllers/subsystem/networks/bitrunning.dm:44
    public virtual object pick_secondary_loot(object completed_domain = null)
    {
        datum_lazy_template_virtual_domain domain = completed_domain as datum_lazy_template_virtual_domain;
        object choice;

        if (Dm.truthy(counterlist_sum(domain.secondary_loot)))
        {
            choice = pick_weight(domain.secondary_loot);
            domain.secondary_loot[choice] = Dm.sub(domain.secondary_loot[choice], 1);
        }
        else
        {
            choice = typeof(obj_item_paper_paperslip_bitrunning_error);
            throw Dm.CRASH(Dm.text("Virtual domain [] tried to pick secondary objective loot, but secondary_loot list was empty.", domain.name), "code/controllers/subsystem/networks/bitrunning.dm", 53);
        }
        return choice;
    }
}

// tg: code/controllers/subsystem/networks/bitrunning.dm:56
public partial class obj_item_paper_paperslip_bitrunning_error : obj_item_paper_paperslip
{
    // tg: code/controllers/subsystem/networks/bitrunning.dm:60
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        default_raw_text = "Your reward for collecting the encrypted curiosity failed to arrive, please report this to technical support.";
        return base.Initialize(mapload, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/networks/bitrunning.dm:4  SUBSYSTEM_DEF(bitrunning): GLOBAL_REAL(SSbitrunning, /datum/controller/subsystem/bitrunning)
    internal datum_controller_subsystem_bitrunning SSbitrunning;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_bitrunning SSbitrunning => ref TgState.Current.SSbitrunning;
}
