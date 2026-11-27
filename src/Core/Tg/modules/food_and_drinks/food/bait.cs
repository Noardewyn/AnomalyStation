// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/bait.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fishing baits' qualities (traits that the fishing reads when a bait is on a rod; the fishing is not ported,
// so nothing reads them yet), the natural bait without one, the synthetic dough ball that every fish takes and its unconsumable
// kind. The var blocks are generated.

// tg: code/modules/food_and_drinks/food/bait.dm:1
public partial class obj_item_food_bait : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/bait.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, bait_quality, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/bait.dm:34
public partial class obj_item_food_bait_natural : obj_item_food_bait
{
    // tg: code/modules/food_and_drinks/food/bait.dm:46
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        REMOVE_TRAIT(this, bait_quality, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/bait.dm:63
public partial class obj_item_food_bait_doughball_synthetic : obj_item_food_bait_doughball
{
    // tg: code/modules/food_and_drinks/food/bait.dm:70
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_OMNI_BAIT, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/bait.dm:88
public partial class obj_item_food_bait_doughball_synthetic_unconsumable : obj_item_food_bait_doughball_synthetic
{
    // tg: code/modules/food_and_drinks/food/bait.dm:90
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_BAIT_UNCONSUMABLE, INNATE_TRAIT);
        return dot;
    }
}
