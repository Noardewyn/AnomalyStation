// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/battery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 2 (the power storage): the whole file. The megacells are the SMES's stored energy (tg moved it out of the SMES to
// /obj/item/stock_parts/power_store/battery). The var blocks are generated (D111).

// tg: code/modules/power/battery.dm:1
public partial class obj_item_stock_parts_power_store_battery : obj_item_stock_parts_power_store
{
    // tg: code/modules/power/battery.dm:20
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_lithium), 60), Dm.kv(typeof(datum_reagent_iron), 10), Dm.kv(typeof(datum_reagent_silicon), 10));
    }
}

// tg: code/modules/power/battery.dm:89
public partial class obj_item_stock_parts_power_store_battery_infinite : obj_item_stock_parts_power_store_battery
{
    // tg: code/modules/power/battery.dm:96
    [DmParams("used", "force")]
    public override async DmTask<object> use(object used = null, params object[] __extra)
    {
        return used;
    }
}
