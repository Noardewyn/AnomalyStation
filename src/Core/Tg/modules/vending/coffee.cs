// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/coffee.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the file's one proc (the vending shortage station trait empties the vendor at round start). The var blocks are generated.

// tg: code/modules/vending/coffee.dm:2
public partial class obj_machinery_vending_coffee : obj_machinery_vending
{
    // tg: code/modules/vending/coffee.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload) || !Dm.truthy(is_station_level(z)) || !HAS_TRAIT(SSstation, STATION_TRAIT_VENDING_SHORTAGE))
            return dot;

        foreach (datum_data_vending_product product_record in Dm.each_cast<datum_data_vending_product>(Dm.add(Dm.add(product_records, coin_records), hidden_records)))
        {
            product_record.amount = 0;
            credits_contained += rand(1, 5);
        }
        return dot;
    }
}
