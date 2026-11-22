// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/boxing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps E3, D327: the Spider Clan gift's shuttle loan leaves a balaclava by the victim) but the floortile balaclava's
// fishing modifier (a Dm.Deferred: fishing, M10+). The var blocks are generated (D111).

// tg: code/modules/clothing/masks/boxing.dm:1
public partial class obj_item_clothing_mask_balaclava : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/boxing.dm:12
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        return null;
    }
}

// tg: code/modules/clothing/masks/boxing.dm:15
public partial class obj_item_clothing_mask_floortilebalaclava : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/boxing.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/masks/boxing.dm:29", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)"); //tacticool
        return dot;
    }

    // tg: code/modules/clothing/masks/boxing.dm:31
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        return null;
    }
}

// tg: code/modules/clothing/masks/boxing.dm:34
public partial class obj_item_clothing_mask_luchador : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/boxing.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", strings("luchador_replacement.json", "luchador")), Dm.named("end_string", " OLE!"), Dm.named("end_string_chance", 25), Dm.named("uppercase", TRUE), Dm.named("slots", ITEM_SLOT_MASK)));
        return dot;
    }
}
