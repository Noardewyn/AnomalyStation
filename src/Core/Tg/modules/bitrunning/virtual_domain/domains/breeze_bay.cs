// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/virtual_domain/domains/breeze_bay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, the breeze bay: every fish caught with the domain's rods is worth two points towards the main crate. The var block is
// generated.

// tg: code/modules/bitrunning/virtual_domain/domains/breeze_bay.dm:1
public partial class datum_lazy_template_virtual_domain_breeze_bay : datum_lazy_template_virtual_domain
{
    // tg: code/modules/bitrunning/virtual_domain/domains/breeze_bay.dm:10
    public override async DmTask setup_domain(DmList created_atoms = null)
    {
        await base.setup_domain(created_atoms);

        foreach (obj_item_fishing_rod rod in Dm.each<obj_item_fishing_rod>(created_atoms))
            RegisterSignal(rod, COMSIG_FISHING_ROD_CAUGHT_FISH, nameof(on_fish_caught));
    }

    /// Eventually reveal the cache
    // tg: code/modules/bitrunning/virtual_domain/domains/breeze_bay.dm:17
    [SignalHandler]
    public virtual void on_fish_caught(datum source = null, object reward = null)
    {
        if (reward == null)
            return;

        Waiting.NoSleep(add_points(2));
    }
}
