// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/moth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the original moth cap's goggles (the scope zoom of its item action); its fishing difficulty element waits for
// fishing. The var blocks are generated (D111).

// tg: code/modules/clothing/head/moth.dm:12
public partial class obj_item_clothing_head_mothcap_original : obj_item_clothing_head_mothcap
{
    // tg: code/modules/clothing/head/moth.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_scope), Dm.named("range_modifier", 1.2f), Dm.named("zoom_method", ZOOM_METHOD_ITEM_ACTION), Dm.named("item_action_type", typeof(datum_action_item_action_hands_free_moth_googles))));
        Dm.Deferred("code/modules/clothing/head/moth.dm:18", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -4): fishing (M10+)");
        return dot;
    }
}
