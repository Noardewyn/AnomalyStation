// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/guardian_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the file's two global lists (its #defines are generated).

public partial class datum_controller_global_vars
{
    /// List of all guardians currently extant
    // tg: code/__DEFINES/guardian_defines.dm:22  GLOBAL_LIST_EMPTY(parasites)
    public DmList parasites;

    // tg: code/__DEFINES/guardian_defines.dm:22  (GLOBAL_MANAGED(parasites, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalparasites()
    {
        parasites = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(parasites));
    }

    /// Assoc list of guardian theme singletons
    // tg: code/__DEFINES/guardian_defines.dm:25  GLOBAL_LIST_INIT(guardian_themes, list(...))
    public DmList guardian_themes;

    // tg: code/__DEFINES/guardian_defines.dm:25  (GLOBAL_MANAGED(guardian_themes, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalguardian_themes()
    {
        guardian_themes = Dm.list(
            Dm.kv(GUARDIAN_THEME_TECH, Dm.@new<datum_guardian_fluff_tech>()),
            Dm.kv(GUARDIAN_THEME_MAGIC, Dm.@new<datum_guardian_fluff>()),
            Dm.kv(GUARDIAN_THEME_CARP, Dm.@new<datum_guardian_fluff_carp>()),
            Dm.kv(GUARDIAN_THEME_MINER, Dm.@new<datum_guardian_fluff_miner>())
        );
        Dm.append(ref gvars_datum_init_order, nameof(guardian_themes));
    }
}
