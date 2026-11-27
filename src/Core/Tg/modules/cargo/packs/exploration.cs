// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/packs/exploration.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the exploration packs that the drones' trade contracts unlock; the catering crate's fill is their one proc. The
// var blocks are generated (D111).

// tg: code/modules/cargo/packs/exploration.dm:16
public partial class datum_supply_pack_exploration_catering : datum_supply_pack_exploration
{
    // tg: code/modules/cargo/packs/exploration.dm:23
    public override async DmTask<object> fill(obj container = null)
    {
        object dot = await base.fill(container);
        if (!prob(30))
            return dot;

        foreach (obj_item_food food_item in Dm.each<obj_item_food>(container))
        {
            // makes all of our items GROSS
            food_item.name = Dm.text("spoiled []", food_item.name);
            await food_item.AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("foodtypes", GROSS));
        }
        return dot;
    }
}
