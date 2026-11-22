// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/syndicate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate G3c, D316): the blood-red sneaksuit and with it the uplink's blood-red pajamas; syndicate N2c: the overwatch agent's
// tacticool turtleneck (gaps H1: its syndicate dye). Gaps CL1 (D333): the floor tile camouflage and the scrubs (their fishing difficulty
// waits for fishing). The var blocks are generated (D111).

// tg: code/modules/clothing/under/syndicate.dm:29
public partial class obj_item_clothing_under_syndicate_bloodred : obj_item_clothing_under_syndicate
{
    // tg: code/modules/clothing/under/syndicate.dm:39
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra); // AddElement(/datum/element/adjust_fishing_difficulty, -4): fishing (not-translated.md 114)
}

// tg: code/modules/clothing/under/syndicate.dm:63
public partial class obj_item_clothing_under_syndicate_tacticool : obj_item_clothing_under_syndicate
{
    // tg: code/modules/clothing/under/syndicate.dm:77
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(dot, I18n.T("It has a label that says cleaning this 'genuine' Waffle Corp. product with cleaning solutions other than Grime Liberator telelocational podcrystals will void the warranty."));
        Dm.append(dot, I18n.T("What on earth is a <font color='red'>tele</font>locational pod<font color='red'>crystal</font>?"));
        return dot;
    }

    // tg: code/modules/clothing/under/syndicate.dm:82  (gaps H1: the washing machine)
    public override async DmTask<object> dye_item(object dye_color = null, object dye_key_override = null)
    {
        if (Dm.eq(dye_color, DYE_SYNDICATE))
        {
            if (Dm.eq(dying_key, DYE_REGISTRY_JUMPSKIRT))
                special_wash(typeof(obj_item_clothing_under_syndicate_skirt));
            else
                special_wash(typeof(obj_item_clothing_under_syndicate));
            qdel(this);
            return null;
        }
        return await base.dye_item(dye_color, dye_key_override);
    }

    // tg: code/modules/clothing/under/syndicate.dm:92
    public virtual void special_wash(Type our_jumpsuit = null)
    {
        Dm.@new(our_jumpsuit, loc);
    }
}

// tg: code/modules/clothing/under/syndicate.dm:130
public partial class obj_item_clothing_under_syndicate_floortilecamo : obj_item_clothing_under_syndicate
{
    // tg: code/modules/clothing/under/syndicate.dm:139
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/under/syndicate.dm:141", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)"); //tacticool
        return dot;
    }
}

// tg: code/modules/clothing/under/syndicate.dm:174
public partial class obj_item_clothing_under_syndicate_scrubs : obj_item_clothing_under_syndicate
{
    // tg: code/modules/clothing/under/syndicate.dm:184
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/under/syndicate.dm:186", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -3): fishing (M10+)"); //FISH DOCTOR?!
        return dot;
    }
}
