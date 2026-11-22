// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_items/special.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the entries' procs (the entries and their categories are the generated var blocks).

// tg: code/modules/uplink/uplink_items/special.dm:12
public partial class datum_uplink_item_special_autosurgeon : datum_uplink_item_special
{
    // tg: code/modules/uplink/uplink_items/special.dm:17
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_CYBERNETIC_REVOLUTION))
            purchasable_from = Dm.num(Dm.bitor(purchasable_from, UPLINK_TRAITORS));
    }
}
