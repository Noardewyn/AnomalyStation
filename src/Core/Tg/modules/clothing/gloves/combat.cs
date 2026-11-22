// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/combat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the wizard's and the floortile combat gloves; their fishing difficulty element waits for
// fishing). The var blocks are generated (D111).

// tg: code/modules/clothing/gloves/combat.dm:21
public partial class obj_item_clothing_gloves_combat_wizard : obj_item_clothing_gloves_combat
{
    // tg: code/modules/clothing/gloves/combat.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/gloves/combat.dm:30", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)"); //something something wizard casting
        return dot;
    }
}

// tg: code/modules/clothing/gloves/combat.dm:32
public partial class obj_item_clothing_gloves_combat_floortile : obj_item_clothing_gloves_combat
{
    // tg: code/modules/clothing/gloves/combat.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/gloves/combat.dm:40", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)"); //tacticool
        return dot;
    }
}
