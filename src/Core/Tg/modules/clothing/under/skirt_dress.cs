// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/skirt_dress.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CL1, D333): the striped dress (its spray can recolouring waits for the GAGS recolouring), the wedding dress and the
// evening gown (their fishing difficulty waits for fishing); the rest of the file is var-only. The var blocks are generated (D111).

// tg: code/modules/clothing/under/skirt_dress.dm:15
public partial class obj_item_clothing_under_dress_striped : obj_item_clothing_under_dress
{
    // tg: code/modules/clothing/under/skirt_dress.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/under/skirt_dress.dm:12", "Initialize: AddElement(/datum/element/gags_recolorable): the spray can's GAGS recolouring (M10+)");
        Waiting.NoSleep(update_icon(UPDATE_OVERLAYS));
        return dot;
    }
}

// tg: code/modules/clothing/under/skirt_dress.dm:41
public partial class obj_item_clothing_under_dress_wedding_dress : obj_item_clothing_under_dress
{
    // tg: code/modules/clothing/under/skirt_dress.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/under/skirt_dress.dm:52", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 4): fishing (M10+)"); //You aren't going to fish with this are you?
        return dot;
    }
}

// tg: code/modules/clothing/under/skirt_dress.dm:54
public partial class obj_item_clothing_under_dress_eveninggown : obj_item_clothing_under_dress
{
    // tg: code/modules/clothing/under/skirt_dress.dm:67
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/under/skirt_dress.dm:69", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 4): fishing (M10+)"); //You aren't going to fish with this are you?
        return dot;
    }
}
