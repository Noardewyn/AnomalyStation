// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/virtual_domain/domains/beach_bar.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, the beach bar: every drink drunk from the bar's glasses, its vended ones included, is worth half a point towards the
// main crate. The var block is generated.

// tg: code/modules/bitrunning/virtual_domain/domains/beach_bar.dm:1
public partial class datum_lazy_template_virtual_domain_beach_bar : datum_lazy_template_virtual_domain
{
    // tg: code/modules/bitrunning/virtual_domain/domains/beach_bar.dm:11
    public override async DmTask setup_domain(DmList created_atoms = null)
    {
        await base.setup_domain(created_atoms);

        foreach (obj_item_reagent_containers_cup_glass drink in Dm.each<obj_item_reagent_containers_cup_glass>(created_atoms))
            RegisterSignal(drink, COMSIG_GLASS_DRANK, nameof(on_drink_drank));
        foreach (obj_machinery_vending vending_machine in Dm.each<obj_machinery_vending>(created_atoms))
            RegisterSignal(vending_machine, COMSIG_VENDING_DISPENSED, nameof(on_vended_item));
    }

    // tg: code/modules/bitrunning/virtual_domain/domains/beach_bar.dm:20
    [SignalHandler]
    public virtual void on_vended_item(obj_machinery_vending vending_machine = null, obj_item vended_item = null)
    {
        if (vended_item is obj_item_reagent_containers_cup_glass)
            RegisterSignal(vended_item, COMSIG_GLASS_DRANK, nameof(on_drink_drank));
    }

    /// Eventually reveal the cache
    // tg: code/modules/bitrunning/virtual_domain/domains/beach_bar.dm:26
    [SignalHandler]
    public virtual void on_drink_drank(datum source = null)
    {
        Waiting.NoSleep(add_points(0.5f));
    }
}
