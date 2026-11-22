// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/frozen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the ice cream holders of the space freezy, the sundae and the honkdae translated (gaps package H2, D333: the ice
// cream vat's readers); the rest of the file is generated or waits for its package.

// tg: code/modules/food_and_drinks/food/frozen.dm:33
public partial class obj_item_food_spacefreezy : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/frozen.dm:49
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponent(typeof(datum_component_ice_cream_holder));
    }
}

// tg: code/modules/food_and_drinks/food/frozen.dm:53
public partial class obj_item_food_sundae : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/frozen.dm:69
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponent(typeof(datum_component_ice_cream_holder), Dm.named("y_offset", -2), Dm.named("sweetener", typeof(datum_reagent_consumable_caramel)));
    }
}

// tg: code/modules/food_and_drinks/food/frozen.dm:73
public partial class obj_item_food_honkdae : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/frozen.dm:89
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponent(typeof(datum_component_ice_cream_holder), Dm.named("y_offset", -2)); //The sugar will react with the banana forming laughter. Honk!
    }
}
