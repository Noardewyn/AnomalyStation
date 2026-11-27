// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/jobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the quartermaster's overcoat's and the detective's trench coat's Initialize (the allowed lists), the apron's and the atmos
// overalls' worn pictures. The digitigrade masks of the apron and the overalls (generate_digitigrade_icons) came with the lizards' legs
// (gaps SP1); the overalls' emissive glow is DEVIATION DV12. Gaps CL1 (D333): the
// detective's suit jacket's allowed list; the overalls' and the surgical apron's fishing difficulty. The var blocks
// are generated.

// tg: code/modules/clothing/suits/jobs.dm:51
public partial class obj_item_clothing_suit_apron_overalls : obj_item_clothing_suit_apron
{
    // tg: code/modules/clothing/suits/jobs.dm:66
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -4));
        return dot;
    }
}

// tg: code/modules/clothing/suits/jobs.dm:156
public partial class obj_item_clothing_suit_jacket_det_suit : obj_item_clothing_suit_jacket
{
    // tg: code/modules/clothing/suits/jobs.dm:163
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = GLOB.detective_vest_allowed;
        return dot;
    }
}

// tg: code/modules/clothing/suits/jobs.dm:267
public partial class obj_item_clothing_suit_jacket_quartermaster : obj_item_clothing_suit_jacket
{
    // tg: code/modules/clothing/suits/jobs.dm:273
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(
            typeof(obj_item_stamp),
            typeof(obj_item_storage_bag_mail),
            typeof(obj_item_universal_scanner),
            typeof(obj_item_melee_baton_telescopic)
        ));
        return dot;
    }
}

// tg: code/modules/clothing/suits/jobs.dm:130
public partial class obj_item_clothing_suit_toggle_jacket_det_trench : obj_item_clothing_suit_toggle_jacket
{
    // tg: code/modules/clothing/suits/jobs.dm:145
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = GLOB.detective_vest_allowed;
        return dot;
    }
}

// tg: code/modules/clothing/suits/jobs.dm:356
public partial class obj_item_clothing_suit_apron_surgical : obj_item_clothing_suit_apron
{
    // tg: code/modules/clothing/suits/jobs.dm:380
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -3)); // FISH DOCTOR?!
        return dot;
    }
}

// tg: code/modules/clothing/suits/jobs.dm:6 (gaps SP1: the apron's digitigrade legs)
public partial class obj_item_clothing_suit_apron : obj_item_clothing_suit
{
    // tg: code/modules/clothing/suits/jobs.dm:37
    public override object generate_digitigrade_icons(icon base_icon = null, object greyscale_colors = null)
    {
        icon legs = Dm.icon(SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_digitigrade), greyscale_colors), "apron_worn");
        return apply_icon_mask(base_icon, LEGS_MASK, legs);
    }
}

// tg: code/modules/clothing/suits/jobs.dm:431
public partial class obj_item_clothing_suit_atmos_overalls : obj_item_clothing_suit
{
    // tg: code/modules/clothing/suits/jobs.dm:460 (gaps SP1: the overalls' digitigrade legs)
    public override object generate_digitigrade_icons(icon base_icon = null, object greyscale_colors = null)
    {
        icon legs = Dm.icon(SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_digitigrade), greyscale_colors), "apron_worn");
        return apply_icon_mask(base_icon, LEGS_MASK, legs);
    }

    // tg: code/modules/clothing/suits/jobs.dm:468
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        // DEVIATION(DV12): . += emissive_appearance((bodyshape & BODYSHAPE_CERULEAN) ? CERULEAN_SUIT_FILE : icon_file, "[icon_state]-emissive", src,
        // alpha = src.alpha, effect_type = EMISSIVE_SPECULAR), the overalls' glow on the emissive plane, which the 3D client does not have
        if (!Dm.truthy(isinhands))
            Dm.Deferred("code/modules/clothing/suits/jobs.dm:471", "worn_overlays: emissive_appearance of the atmospherics overalls' lights (DEVIATION DV12)");
        return dot;
    }
}
