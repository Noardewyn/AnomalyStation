// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/costume.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CL1, D333): the emotion mask's reskin (the reskin component, datums/components/reskinnable_atom.dm, is inert: a
// Deferred leaf, not-translated.md 114) and the kitsune mask worn as a mask or pushed up as a hat. The rest of the file is var-only.
// The var blocks are generated (D111).

// tg: code/modules/clothing/masks/costume.dm:26
public partial class obj_item_clothing_mask_joy : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/costume.dm:34
    public override async DmTask setup_reskins()
    {
        // AddComponent(/datum/component/reskinable_item, /datum/atom_skin/joy_mask, infinite = TRUE)
        Dm.Deferred("code/modules/clothing/masks/costume.dm:35", "setup_reskins: AddComponent(/datum/component/reskinable_item, /datum/atom_skin/joy_mask): the reskin menu (M10+)");
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/clothing/masks/costume.dm:51
public partial class obj_item_clothing_mask_kitsune : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/costume.dm:67
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(up))
        {
            Dm.append(ref dot, I18n.T("Use in-hand to wear as a mask!"));
            return dot;
        }
        else
            Dm.append(ref dot, I18n.T("Use in-hand to wear as a hat!"));
        return dot;
    }

    // tg: code/modules/clothing/masks/costume.dm:75
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        alternate_worn_layer = Dm.truthy(up) ? ABOVE_BODY_FRONT_HEAD_LAYER : null;
        flags_inv = Dm.truthy(up) ? NONE : Dm.bitor(HIDEFACE, HIDEFACIALHAIR);
        return null;
    }
}
