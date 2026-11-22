// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/food_and_drink.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: food sells at its venue value, silver-poisoned food for nothing. The var blocks are generated.

// tg: code/modules/cargo/exports/food_and_drink.dm:5
public partial class datum_export_food : datum_export
{
    // tg: code/modules/cargo/exports/food_and_drink.dm:13
    public override object get_base_cost(obj exported_item = null)
    {
        var @object = (obj_item_food)exported_item;
        if (HAS_TRAIT(@object, TRAIT_FOOD_SILVER))
            return FOOD_PRICE_WORTHLESS;

        return Dm.truthy(@object.venue_value) ? @object.venue_value : base.get_base_cost(exported_item);
    }
}
