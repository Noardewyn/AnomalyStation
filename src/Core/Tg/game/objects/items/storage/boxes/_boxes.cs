// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the box base (Initialize, the illustration overlay, folding by attack_self). Later: suicide_act (M10, a
// live-partial row).

// tg: code/game/objects/items/storage/boxes/_boxes.dm:2
public partial class obj_item_storage_box : obj_item_storage
{
    // tg: code/game/objects/items/storage/boxes/_boxes.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (foldable_result == typeof(obj_item_stack_sheet_cardboard))
            Waiting.NoSleep(set_custom_materials(Dm.list(Dm.kv(typeof(datum_material_cardboard), SHEET_MATERIAL_AMOUNT))));
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/_boxes.dm:37
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(illustration))
            Dm.append(ref dot, illustration);
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/_boxes.dm:42
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await base.attack_self(user, modifiers, __extra);

        if (!Dm.truthy(foldable_result) || Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
            return null;
        if (contents.len > 0)
        {
            balloon_alert(user, I18n.T("items inside!"));
            return null;
        }
        if (!ispath(foldable_result))
            return null;

        obj_item result = await Dm.new_async(foldable_result, user.drop_location()) as obj_item;
        balloon_alert(user, I18n.T("folded"));
        qdel(this);
        await user.put_in_hands(result);
        return null;
    }
}
