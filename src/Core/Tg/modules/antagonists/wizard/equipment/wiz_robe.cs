// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/wizard/equipment/wiz_robe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CL1, D333): the wizard hats' and robes' Initialize, so the station's costume kinds (the theatre's fake wizard hat and
// robe, the witch's and the tape ones) boot live: the casting clothing trait. The fishing difficulty element waits for fishing (M10+,
// not-translated.md section 209). The paper robe's stickmen (ui_action_click, conjure_stickmen) are the wizard's (the paper wizard row).
// The var blocks are generated (D111).

// tg: code/modules/antagonists/wizard/equipment/wiz_robe.dm:1
public partial class obj_item_clothing_head_wizard : obj_item_clothing_head
{
    // tg: code/modules/antagonists/wizard/equipment/wiz_robe.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CASTING_CLOTHING, INNATE_TRAIT);
        Dm.Deferred("code/modules/antagonists/wizard/equipment/wiz_robe.dm:20", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, fishing_modifier): fishing (M10+)"); //A wizard always practices his casting (ba dum tsh)
        return dot;
    }
}

// tg: code/modules/antagonists/wizard/equipment/wiz_robe.dm:114
public partial class obj_item_clothing_suit_wizrobe : obj_item_clothing_suit
{
    // tg: code/modules/antagonists/wizard/equipment/wiz_robe.dm:131
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CASTING_CLOTHING, INNATE_TRAIT);
        Dm.Deferred("code/modules/antagonists/wizard/equipment/wiz_robe.dm:134", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, fishing_modifier): fishing (M10+)"); //A wizard always practices his casting (ba dum tsh)
        return dot;
    }
}
