// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/exploration_site.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only GLOBAL_LIST_EMPTY(exploration_sites) translated (gaps RD3, D333: the exploration scan experiments read it); the
// exploration sites, their generation and the exodrone's scans wait for the explorer drones (not-translated.md section 195), so the
// list stays empty and no exploration scan experiment completes.

// tg: code/modules/explorer_drone/exploration_site.dm:2
public partial class datum_controller_global_vars
{
    /// All exploration site instances
    // tg: code/modules/explorer_drone/exploration_site.dm:2  GLOBAL_LIST_EMPTY(exploration_sites)
    public DmList exploration_sites;

    // tg: code/modules/explorer_drone/exploration_site.dm:2  (GLOBAL_MANAGED(exploration_sites, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalexploration_sites()
    {
        exploration_sites = Dm.list();
        Dm.append(ref gvars_datum_init_order, "exploration_sites");
    }
}
