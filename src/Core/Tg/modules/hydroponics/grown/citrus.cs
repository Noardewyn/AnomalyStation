// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/citrus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/citrus.dm:28
public partial class obj_item_food_grown_citrus_lime : obj_item_food_grown_citrus
{
    // tg: code/modules/hydroponics/grown/citrus.dm:33
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_limejuice);
    }
}

// tg: code/modules/hydroponics/grown/citrus.dm:56
public partial class obj_item_food_grown_citrus_orange : obj_item_food_grown_citrus
{
    // tg: code/modules/hydroponics/grown/citrus.dm:63
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_orangejuice);
    }
}

// tg: code/modules/hydroponics/grown/citrus.dm:85
public partial class obj_item_food_grown_citrus_lemon : obj_item_food_grown_citrus
{
    // tg: code/modules/hydroponics/grown/citrus.dm:90
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_lemonjuice);
    }
}

// tg: code/modules/hydroponics/grown/citrus.dm:139
public partial class obj_item_food_grown_citrus_orange_3d : obj_item_food_grown_citrus
{
    // tg: code/modules/hydroponics/grown/citrus.dm:149
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_orangejuice);
    }

    // tg: code/modules/hydroponics/grown/citrus.dm:152
    public override async DmTask<object> pickup(mob user = null)
    {
        object dot = await base.pickup(user);
        icon_state = alt_icon as string;
        return dot;
    }

    // tg: code/modules/hydroponics/grown/citrus.dm:156
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        icon_state = Dm.initial(this, (atom a) => a.icon_state);
        return dot;
    }
}
