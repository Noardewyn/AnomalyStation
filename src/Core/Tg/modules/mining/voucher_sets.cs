// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/voucher_sets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file; the sets are var blocks (generated) but the base's procs.

/**
 * # Voucher Set
 *
 * A set consisting of a various equipment that can be then used as a reward for redeeming a voucher.
 *
 * See [Voucher Redeemer](/datum/element/voucher_redeemer) for where these sets are handled.
 */
// tg: code/modules/mining/voucher_sets.dm:8
public partial class datum_voucher_set : datum
{
    // tg: code/modules/mining/voucher_sets.dm:22
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(length(set_items)))
        {
            stack_trace(Dm.text("Voucher set [] has no items set.", type), "code/modules/mining/voucher_sets.dm", 25);
            return;
        }
        if (icon == null)
            icon = Dm.initial((Type)set_items[1], "icon");
        if (icon_state == null)
            icon_state = Dm.initial((Type)set_items[1], "icon_state");
        if (name == null)
            stack_trace(Dm.text("Voucher set [] has no name set.", type), "code/modules/mining/voucher_sets.dm", 32);
    }

    // tg: code/modules/mining/voucher_sets.dm:34
    public virtual void spawn_set(atom spawn_loc = null)
    {
        foreach (object item in Dm.each(set_items))
            Dm.@new((Type)item, spawn_loc);
    }
}
