// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/collectable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the collectable hats' Initialize (the series element, datums/elements/series.cs); the pirate, wizard and SWAT
// hats' fishing difficulty elements wait for fishing. The var blocks are generated (D111).

// tg: code/modules/clothing/head/collectable.dm:4
public partial class obj_item_clothing_head_collectable : obj_item_clothing_head
{
    // tg: code/modules/clothing/head/collectable.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_series), typeof(obj_item_clothing_head_collectable), "Super duper collectable hats"));
        return dot;
    }
}

// tg: code/modules/clothing/head/collectable.dm:112
public partial class obj_item_clothing_head_collectable_pirate : obj_item_clothing_head_collectable
{
    // tg: code/modules/clothing/head/collectable.dm:119
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/head/collectable.dm:121", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -4): fishing (M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/head/collectable.dm:137
public partial class obj_item_clothing_head_collectable_wizard : obj_item_clothing_head_collectable
{
    // tg: code/modules/clothing/head/collectable.dm:145
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/head/collectable.dm:147", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -2): fishing (M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/head/collectable.dm:184
public partial class obj_item_clothing_head_collectable_swat : obj_item_clothing_head_collectable
{
    // tg: code/modules/clothing/head/collectable.dm:194
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/head/collectable.dm:196", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 2): fishing (M10+)");
        return dot;
    }
}
