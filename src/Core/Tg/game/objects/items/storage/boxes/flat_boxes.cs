// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/flat_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 10: the flat box (its init: the undertile element, hidden under a floor tile and tilting it) and the plain box's
// flatten_box (an empty plain box flattened into a flat box with the cardboard sound; its caller is the jump emote). Gaps SR1
// (D333): the fentanyl patch box's init. The var blocks are generated.

// tg: code/game/objects/items/storage/boxes/flat_boxes.dm:1
public partial class obj_item_storage_box_flat : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/flat_boxes.dm:8
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE, INVISIBILITY_OBSERVER, Dm.named("use_anchor", TRUE), Dm.named("tilt_tile", TRUE)));
        return dot;
    }
}

// tg: code/game/objects/items/storage/boxes/_boxes.dm:2
public partial class obj_item_storage_box : obj_item_storage
{
    // tg: code/game/objects/items/storage/boxes/flat_boxes.dm:12
    public virtual void flatten_box()
    {
        if (istype(loc, typeof(obj_item_storage)) || type != typeof(obj_item_storage_box) || Dm.truthy(length(contents)))
            return;

        obj flat_box = Dm.@new<obj_item_storage_box_flat>(drop_location());
        playsound(this, Dm.resource("sound/items/handling/materials/cardboard_drop.ogg"), 50, TRUE); // tg: code/game/objects/items/storage/boxes/flat_boxes.dm:17

        flat_box.pixel_x = pixel_x;
        flat_box.pixel_y = pixel_y;

        qdel(this);
    }
}

// tg: code/game/objects/items/storage/boxes/flat_boxes.dm:24
public partial class obj_item_storage_box_flat_fentanylpatches : obj_item_storage_box_flat
{
    // tg: code/game/objects/items/storage/boxes/flat_boxes.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        for (float i = 1; i <= 3; i++)
            Dm.@new<obj_item_reagent_containers_applicator_patch_fent>(this);
        return dot;
    }
}
