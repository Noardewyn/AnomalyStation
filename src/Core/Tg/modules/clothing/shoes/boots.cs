// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/boots.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the jackboots' and work boots' Initialize: the knife slot (the shoes' pockets, datums/storage/subtypes/pockets.cs); J3: the element that
// lights a match on the boot; syndicate N1 (D316): the operatives' combat boots; gaps CL1 (D333): the floortile jackboots, the winter, Russian and
// pirate boots (their pockets; the fishing difficulty elements wait for fishing). The var blocks are generated.

// tg: code/modules/clothing/shoes/boots.dm:1
public partial class obj_item_clothing_shoes_combat : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/boots.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ignites_matches)));
        return dot;
    }
}

// tg: code/modules/clothing/shoes/boots.dm:46
public partial class obj_item_clothing_shoes_jackboots : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/boots.dm:62
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ignites_matches)));
        return dot;
    }
}

// tg: code/modules/clothing/shoes/boots.dm:73
public partial class obj_item_clothing_shoes_jackboots_floortile : obj_item_clothing_shoes_jackboots
{
    // tg: code/modules/clothing/shoes/boots.dm:81
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/shoes/boots.dm:83", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)"); //tacticool
        return dot;
    }
}

// tg: code/modules/clothing/shoes/boots.dm:85
public partial class obj_item_clothing_shoes_winterboots : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/boots.dm:102
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ignites_matches)));
        return dot;
    }
}

// tg: code/modules/clothing/shoes/boots.dm:137
public partial class obj_item_clothing_shoes_workboots : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/boots.dm:151
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ignites_matches)));
        return dot;
    }
}

// tg: code/modules/clothing/shoes/boots.dm:167
public partial class obj_item_clothing_shoes_russian : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/boots.dm:176
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ignites_matches)));
        return dot;
    }
}

// tg: code/modules/clothing/shoes/boots.dm:193
public partial class obj_item_clothing_shoes_pirate : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/boots.dm:199
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        Dm.Deferred("code/modules/clothing/shoes/boots.dm:202", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -4): fishing (M10+)");
        return dot;
    }
}
