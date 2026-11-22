// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/trash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M7 lighting slice L1, D168): the base trash item and the melted candle, so the burnt flare and the melted candle
// that a flare leaves are live items; the boritos bags and the crushed and food cans (gaps OD1, D333). The var blocks are generated.

//Added by Jack Rost
// tg: code/game/objects/items/trash.dm:2
public partial class obj_item_trash : obj_item
{
    // tg: code/game/objects/items/trash.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        turf T = get_turf(this) as turf;
        if (T != null && Dm.truthy(is_station_level(T.z)))
            SSblackbox.record_feedback("tally", "station_mess_created", 1, name);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/items/trash.dm:19
    public override object Destroy(float? force = null)
    {
        turf T = get_turf(this) as turf;
        if (T != null && Dm.truthy(is_station_level(T.z)))
            SSblackbox.record_feedback("tally", "station_mess_destroyed", 1, name);
        return base.Destroy(force);
    }
}

// tg: code/game/objects/items/trash.dm:117
public partial class obj_item_trash_candle : obj_item_trash
{
    // tg: code/game/objects/items/trash.dm:123
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_floor_placeable)));
        return dot;
    }
}

// tg: code/game/objects/items/trash.dm:49  (gaps OD1)
public partial class obj_item_trash_boritos : obj_item_trash
{
    // tg: code/game/objects/items/trash.dm:52
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_aluminium), 1));
    }
}

// tg: code/game/objects/items/trash.dm:134  (gaps OD1)
public partial class obj_item_trash_can : obj_item_trash
{
    // tg: code/game/objects/items/trash.dm:139
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            return dot;
        pixel_x = rand(-4, 4);
        pixel_y = rand(-4, 4);
        return dot;
    }

    // tg: code/game/objects/items/trash.dm:146
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_aluminium), 10));
    }
}

// tg: code/game/objects/items/trash.dm:150  (gaps OD1)
public partial class obj_item_trash_can_food : obj_item_trash_can
{
    // tg: code/game/objects/items/trash.dm:155
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CUSTOM_TAP_SOUND, INNATE_TRAIT);
        return dot;
    }
}
