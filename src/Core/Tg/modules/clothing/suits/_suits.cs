// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/_suits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// /obj/item/clothing/suit's worn_overlays, separate_worn_overlays and update_clothes_damaged_state (M4a clothing: suits are put on,
// taken off and drawn on the wearer). Gaps CL1 (D333): generate_digitigrade_icons is declared and deferred (a server-side icon
// mask, D104; the GAGS of digitigrade clothing, not-translated.md 61). The vars and their defaults are generated. [DmPath] is
// here: the earliest .dme file that declares the type (rules 2.3).

// tg: code/modules/clothing/suits/_suits.dm:1
[DmPath("/obj/item/clothing/suit")]
public partial class obj_item_clothing_suit : obj_item_clothing
{
    // tg: code/modules/clothing/suits/_suits.dm:24
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;

        if (Dm.truthy(damaged_clothes))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/effects/item_damage.dmi"), Dm.text("damaged[]", blood_overlay_type)));

        mob_living_carbon_human wearer = loc as mob_living_carbon_human;
        if (!ishuman(wearer) || !Dm.truthy(wearer.w_uniform))
            return dot;
        obj_item_clothing_under undershirt = wearer.w_uniform as obj_item_clothing_under;
        if (!(undershirt is obj_item_clothing_under))
            return dot;
        foreach (obj_item_clothing_accessory accessory in Dm.each_cast<obj_item_clothing_accessory>(undershirt.attached_accessories))
        {
            if (Dm.truthy(accessory.above_suit))
                Dm.append(ref dot, accessory.generate_accessory_overlay(undershirt));
        }
        return dot;
    }

    // tg: code/modules/clothing/suits/_suits.dm:42
    public override async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = await base.separate_worn_overlays(standing, draw_target, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;
        object blood_overlay = await get_blood_overlay(blood_overlay_type, bodyshape);
        if (Dm.truthy(blood_overlay))
            Dm.append(ref dot, blood_overlay);
        return dot;
    }

    // tg: code/modules/clothing/suits/_suits.dm:50
    public override async DmTask update_clothes_damaged_state(float? damaged_state = null)
    {
        damaged_state ??= CLOTHING_DAMAGED;
        await base.update_clothes_damaged_state(damaged_state);
        if (ismob(loc))
        {
            mob M = loc as mob;
            await M.update_worn_oversuit();
        }
    }

    // tg: code/modules/clothing/suits/_suits.dm:56
    public override object generate_digitigrade_icons(icon base_icon = null, object greyscale_colors = null)
    {
        icon new_legs = Dm.icon(SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_digitigrade), greyscale_colors), "oversuit_worn");
        return apply_icon_mask(base_icon, LEGS_MASK, new_legs);
    }
}
