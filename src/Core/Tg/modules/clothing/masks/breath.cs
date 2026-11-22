// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/breath.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The breath masks (M4c slice 1: the mask internals breathe through, pushed out of the way and back with alt-click or in
// hand); gaps CL1 (D333): the suicide act (the tube around the neck). The var blocks are generated (D111).

// tg: code/modules/clothing/masks/breath.dm:1
public partial class obj_item_clothing_mask_breath : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/breath.dm:22  suicide_act: live-partial (suicides, M10).

    // tg: code/modules/clothing/masks/breath.dm:26
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(adjustable))
            await adjust_visor(user as mob_living);
        return null;
    }

    // tg: code/modules/clothing/masks/breath.dm:30
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(adjustable))
            return null;
        await adjust_visor(user as mob_living);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/clothing/masks/breath.dm:36
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(adjustable))
            Dm.append(ref dot, span_notice(I18n.T("Alt-click [] to adjust it.", this)));
        return dot;
    }

    // tg: code/modules/clothing/masks/breath.dm:22
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is wrapping \\the []'s tube around [] neck! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        return OXYLOSS;
    }
}

// tg: code/modules/clothing/masks/breath.dm:52
public partial class obj_item_clothing_mask_breath_muzzle : obj_item_clothing_mask_breath
{
    // tg: code/modules/clothing/masks/breath.dm:66
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_muffles_speech)));
        return dot;
    }

    // tg: code/modules/clothing/masks/breath.dm:70
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        if (user.get_item_by_slot(ITEM_SLOT_MASK) == this)
        {
            to_chat(user, span_warning(I18n.T("You need help taking this off!")));
            return null;
        }
        return await base.attack_paw(user, modifiers);
    }

    // tg: code/modules/clothing/masks/breath.dm:76
    public override object examine_tags(mob user = null)
    {
        DmList dot = null;
        dot = base.examine_tags(user) as DmList;
        dot["surgical"] = I18n.T("Does not block surgery on covered bodyparts.");
        return dot;
    }
}
